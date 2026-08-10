using System.Text;

namespace EffortHours.Change;

internal enum GeneratedCustomizationOutcome
{
    NotDetected,
    Unchanged,
    FormattingOnly,
    Represented,
    Ambiguous,
    SourceUnavailable,
    TooLarge,
}

internal readonly record struct GeneratedCustomizationResult(
    GeneratedCustomizationOutcome Outcome,
    int EditRegions = 0,
    int MeaningfulRegionCount = 0,
    string? Detail = null)
{
    public string? TraceTag => Outcome switch
    {
        GeneratedCustomizationOutcome.Unchanged =>
            "normalization:generated-customization-unchanged",
        GeneratedCustomizationOutcome.FormattingOnly =>
            "normalization:generated-customization-formatting-only",
        GeneratedCustomizationOutcome.Represented =>
            "normalization:generated-customization-represented",
        GeneratedCustomizationOutcome.Ambiguous =>
            "normalization:generated-customization-ambiguous",
        _ => null,
    };
}

internal static class GeneratedCustomizationAnalyzer
{
    private const int MaximumRegions = 128;
    private const string ProjectionBoundary = "/*__EFFORTHOURS_CUSTOM_REGION__*/";

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    public static async Task<GeneratedCustomizationResult> AnalyzeAsync(
        string path,
        string? previousPath,
        ChangeSnapshotFile? baseFile,
        ChangeSnapshotFile? headFile,
        IChangeSnapshot baseSnapshot,
        IChangeSnapshot headSnapshot,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(baseSnapshot);
        ArgumentNullException.ThrowIfNull(headSnapshot);
        if ((baseFile is not null && !SupportsSourceReads(baseSnapshot)) ||
            (headFile is not null && !SupportsSourceReads(headSnapshot)))
        {
            return new GeneratedCustomizationResult(
                GeneratedCustomizationOutcome.SourceUnavailable);
        }

        if (baseFile?.Length > ContentChangeAnalyzer.MaximumTextBytes ||
            headFile?.Length > ContentChangeAnalyzer.MaximumTextBytes)
        {
            return new GeneratedCustomizationResult(GeneratedCustomizationOutcome.TooLarge);
        }

        byte[]? baseContent = baseFile is null
            ? null
            : await baseSnapshot.ReadAllBytesAsync(
                previousPath ?? path,
                cancellationToken).ConfigureAwait(false);
        byte[]? headContent = headFile is null
            ? null
            : await headSnapshot.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return Analyze(baseContent, headContent, path);
    }

    public static GeneratedCustomizationResult Analyze(
        byte[]? baseContent,
        byte[]? headContent,
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Extraction baseExtraction = Extract(baseContent);
        Extraction headExtraction = Extract(headContent);
        Extraction? invalid = !baseExtraction.IsValid
            ? baseExtraction
            : !headExtraction.IsValid
                ? headExtraction
                : null;
        if (invalid is not null)
        {
            return new GeneratedCustomizationResult(
                GeneratedCustomizationOutcome.Ambiguous,
                Detail: invalid.Value.Detail);
        }

        int markerCount = Math.Max(baseExtraction.MarkerRegionCount, headExtraction.MarkerRegionCount);
        int meaningfulCount = Math.Max(
            baseExtraction.MeaningfulRegionCount,
            headExtraction.MeaningfulRegionCount);
        if (markerCount == 0)
        {
            return new GeneratedCustomizationResult(GeneratedCustomizationOutcome.NotDetected);
        }

        if (string.Equals(baseExtraction.Projection, headExtraction.Projection, StringComparison.Ordinal))
        {
            return new GeneratedCustomizationResult(
                GeneratedCustomizationOutcome.Unchanged,
                MeaningfulRegionCount: meaningfulCount);
        }

        byte[] baseProjection = StrictUtf8.GetBytes(baseExtraction.Projection);
        byte[] headProjection = StrictUtf8.GetBytes(headExtraction.Projection);
        ContentChangeResult content = ContentChangeAnalyzer.Analyze(
            baseProjection,
            headProjection,
            path);
        return content.FormattingOnly
            ? new GeneratedCustomizationResult(
                GeneratedCustomizationOutcome.FormattingOnly,
                MeaningfulRegionCount: meaningfulCount)
            : new GeneratedCustomizationResult(
                GeneratedCustomizationOutcome.Represented,
                content.EditRegions,
                meaningfulCount);
    }

    public static string Describe(GeneratedCustomizationResult result) =>
        result.Outcome switch
        {
            GeneratedCustomizationOutcome.NotDetected =>
                "The scanner classified the artifact as generated; unsupported generated-body effort is excluded.",
            GeneratedCustomizationOutcome.Unchanged =>
                "The scanner classified the artifact as generated. Its explicit <custom-code> regions are " +
                "unchanged, so the generated body and unchanged customization are excluded.",
            GeneratedCustomizationOutcome.FormattingOnly =>
                "The scanner classified the artifact as generated. Its explicit <custom-code> regions differ " +
                "only after conservative whitespace normalization, so no body effort is represented.",
            GeneratedCustomizationOutcome.Represented =>
                $"The scanner classified the artifact as generated, but {result.MeaningfulRegionCount} " +
                "explicitly delimited <custom-code> region(s) changed. Only those maintained regions are " +
                "represented; the generated body remains excluded.",
            GeneratedCustomizationOutcome.Ambiguous =>
                $"The scanner classified the artifact as generated. {result.Detail} No generated-body or " +
                "purported customization effort is represented.",
            GeneratedCustomizationOutcome.SourceUnavailable =>
                "The scanner classified the artifact as generated. Source bodies are unavailable, so custom " +
                "regions cannot be distinguished safely and the complete body is excluded.",
            GeneratedCustomizationOutcome.TooLarge =>
                "The scanner classified the artifact as generated. At least one blob exceeds the " +
                "eight-megabyte inspection limit, so custom regions cannot be distinguished safely and the " +
                "complete body is excluded.",
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };

    private static bool SupportsSourceReads(IChangeSnapshot snapshot) =>
        snapshot is not IRepositoryEvidenceChangeSnapshot analyzedSnapshot ||
        analyzedSnapshot.SupportsSourceReads;

    private static Extraction Extract(byte[]? content)
    {
        if (content is null)
        {
            return Extraction.Empty;
        }

        string text;
        try
        {
            text = StrictUtf8.GetString(content);
        }
        catch (DecoderFallbackException)
        {
            return Extraction.Invalid(
                "The generated artifact is not valid UTF-8 text, so custom regions cannot be isolated safely.");
        }

        if (text.AsSpan().Contains('\0'))
        {
            return Extraction.Invalid(
                "The generated artifact contains null bytes, so custom regions cannot be isolated safely.");
        }

        string[] lines = text
            .TrimStart('\uFEFF')
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        List<string> regions = [];
        StringBuilder? current = null;
        MarkerStyle currentStyle = default;
        int markerRegionCount = 0;
        foreach (string line in lines)
        {
            Marker? marker = ParseMarker(line.Trim());
            if (marker is { IsStart: true })
            {
                if (current is not null)
                {
                    return Extraction.Invalid(
                        "Nested <custom-code> markers are ambiguous and are excluded.");
                }

                markerRegionCount++;
                if (markerRegionCount > MaximumRegions)
                {
                    return Extraction.Invalid(
                        $"The generated artifact exceeds the {MaximumRegions}-region custom-code limit.");
                }

                current = new StringBuilder();
                currentStyle = marker.Value.Style;
                continue;
            }

            if (marker is { IsStart: false })
            {
                if (current is null || currentStyle != marker.Value.Style)
                {
                    return Extraction.Invalid(
                        "Unpaired or mixed-style <custom-code> markers are ambiguous and are excluded.");
                }

                regions.Add(current.ToString());
                current = null;
                continue;
            }

            current?.AppendLine(line);
        }

        if (current is not null)
        {
            return Extraction.Invalid(
                "An unclosed <custom-code> region is ambiguous and is excluded.");
        }

        string[] meaningful =
        [
            .. regions
                .Where(region => !string.IsNullOrWhiteSpace(region))
                .Select(region => region.TrimEnd('\r', '\n')),
        ];
        return new Extraction(
            true,
            markerRegionCount,
            meaningful.Length,
            string.Join($"\n{ProjectionBoundary}\n", meaningful),
            null);
    }

    private static Marker? ParseMarker(string line)
    {
        foreach ((string start, string end, MarkerStyle style) in MarkerPairs)
        {
            if (string.Equals(line, start, StringComparison.OrdinalIgnoreCase))
            {
                return new Marker(true, style);
            }

            if (string.Equals(line, end, StringComparison.OrdinalIgnoreCase))
            {
                return new Marker(false, style);
            }
        }

        return null;
    }

    private static readonly (string Start, string End, MarkerStyle Style)[] MarkerPairs =
    [
        ("// <custom-code>", "// </custom-code>", MarkerStyle.LineComment),
        ("/* <custom-code> */", "/* </custom-code> */", MarkerStyle.BlockComment),
        ("# <custom-code>", "# </custom-code>", MarkerStyle.HashComment),
        ("<!-- <custom-code> -->", "<!-- </custom-code> -->", MarkerStyle.MarkupComment),
    ];

    private readonly record struct Extraction(
        bool IsValid,
        int MarkerRegionCount,
        int MeaningfulRegionCount,
        string Projection,
        string? Detail)
    {
        public static Extraction Empty => new(true, 0, 0, string.Empty, null);

        public static Extraction Invalid(string detail) => new(false, 0, 0, string.Empty, detail);
    }

    private readonly record struct Marker(bool IsStart, MarkerStyle Style);

    private enum MarkerStyle
    {
        LineComment,
        BlockComment,
        HashComment,
        MarkupComment,
    }
}
