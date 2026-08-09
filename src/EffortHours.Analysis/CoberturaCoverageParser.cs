using System.Globalization;
using System.Xml;

namespace EffortHours.Analysis;

internal static class CoberturaCoverageParser
{
    private const long MaximumDocumentCharacters = 128L * 1024L * 1024L;

    public static async Task<CoverageReportData?> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        XmlReaderSettings settings = new()
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true,
            MaxCharactersInDocument = MaximumDocumentCharacters,
            XmlResolver = null,
        };
        Dictionary<string, CoverageCounters> sources = new(StringComparer.Ordinal);
        CoverageCounters overall = default;
        CoveragePercentages? percentages = null;
        bool foundCoverageRoot = false;
        ClassState? currentClass = null;
        MethodState? currentMethod = null;

        using XmlReader reader = XmlReader.Create(stream, settings);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType == XmlNodeType.Element)
            {
                if (reader.Depth == 0)
                {
                    if (!reader.LocalName.Equals("coverage", StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }

                    foundCoverageRoot = true;
                    overall = ReadOverallCounters(reader);
                    percentages = ReadOverallPercentages(reader);
                }
                else if (reader.LocalName.Equals("class", StringComparison.OrdinalIgnoreCase))
                {
                    FlushClass(currentClass, sources);
                    currentClass = new ClassState(reader.GetAttribute("filename"), reader.Depth);
                    currentMethod = null;
                }
                else if (currentClass is not null &&
                    reader.LocalName.Equals("method", StringComparison.OrdinalIgnoreCase))
                {
                    currentMethod = new MethodState(reader.Depth);
                }
                else if (currentClass is not null &&
                    reader.LocalName.Equals("line", StringComparison.OrdinalIgnoreCase))
                {
                    ReadLine(reader, currentClass, currentMethod);
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement)
            {
                if (currentMethod is not null &&
                    reader.Depth == currentMethod.Depth &&
                    reader.LocalName.Equals("method", StringComparison.OrdinalIgnoreCase))
                {
                    currentClass!.FunctionsTotal++;
                    if (currentMethod.HasCoveredLine)
                    {
                        currentClass.FunctionsCovered++;
                    }

                    currentMethod = null;
                }
                else if (currentClass is not null &&
                    reader.Depth == currentClass.Depth &&
                    reader.LocalName.Equals("class", StringComparison.OrdinalIgnoreCase))
                {
                    FlushClass(currentClass, sources);
                    currentClass = null;
                    currentMethod = null;
                }
            }
        }

        FlushClass(currentClass, sources);
        if (!foundCoverageRoot)
        {
            return null;
        }

        CoverageSourceResult[] results = [.. sources
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new CoverageSourceResult(pair.Key, pair.Value))];
        CoverageCounters sourceTotals = results.Aggregate(
            default(CoverageCounters),
            (current, source) => current.Add(source.Counters));
        overall = overall.PreferAvailable(sourceTotals);
        CoverageReportData report = new("cobertura", results, overall, percentages);
        return report.HasMeasurements ? report : null;
    }

    private static CoverageCounters ReadOverallCounters(XmlReader reader) => new(
        ReadNonNegativeAttribute(reader, "lines-covered"),
        ReadNonNegativeAttribute(reader, "lines-valid"),
        ReadNonNegativeAttribute(reader, "branches-covered"),
        ReadNonNegativeAttribute(reader, "branches-valid"),
        0m,
        0m);

    private static CoveragePercentages? ReadOverallPercentages(XmlReader reader)
    {
        decimal? lines = ReadRateAttribute(reader, "line-rate");
        decimal? branches = ReadRateAttribute(reader, "branch-rate");
        CoveragePercentages percentages = new(lines, branches, null);
        return percentages.HasMeasurements ? percentages : null;
    }

    private static void ReadLine(
        XmlReader reader,
        ClassState currentClass,
        MethodState? currentMethod)
    {
        decimal hits = ReadNonNegativeAttribute(reader, "hits");
        if (currentMethod is not null)
        {
            currentMethod.HasCoveredLine |= hits > 0m;
            return;
        }

        currentClass.LinesTotal++;
        if (hits > 0m)
        {
            currentClass.LinesCovered++;
        }

        if (!bool.TryParse(reader.GetAttribute("branch"), out bool branch) || !branch)
        {
            return;
        }

        if (TryReadConditionCounts(
            reader.GetAttribute("condition-coverage"),
            out decimal covered,
            out decimal total))
        {
            currentClass.BranchesCovered += covered;
            currentClass.BranchesTotal += total;
        }
    }

    private static bool TryReadConditionCounts(
        string? value,
        out decimal covered,
        out decimal total)
    {
        covered = 0m;
        total = 0m;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        int open = value.IndexOf('(');
        int slash = value.IndexOf('/', open + 1);
        int close = value.IndexOf(')', slash + 1);
        return open >= 0 && slash > open && close > slash &&
            decimal.TryParse(
                value.AsSpan(open + 1, slash - open - 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out covered) &&
            decimal.TryParse(
                value.AsSpan(slash + 1, close - slash - 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out total) &&
            covered >= 0m && total > 0m && covered <= total;
    }

    private static decimal ReadNonNegativeAttribute(XmlReader reader, string name) =>
        decimal.TryParse(
            reader.GetAttribute(name),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out decimal value) &&
            value >= 0m
                ? value
                : 0m;

    private static decimal? ReadRateAttribute(XmlReader reader, string name)
    {
        if (!decimal.TryParse(
            reader.GetAttribute(name),
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out decimal value) ||
            value < 0m || value > 1m)
        {
            return null;
        }

        return decimal.Round(value * 100m, 4, MidpointRounding.AwayFromZero);
    }

    private static void FlushClass(
        ClassState? currentClass,
        Dictionary<string, CoverageCounters> sources)
    {
        if (currentClass is null || string.IsNullOrWhiteSpace(currentClass.SourcePath))
        {
            return;
        }

        CoverageCounters counters = new(
            currentClass.LinesCovered,
            currentClass.LinesTotal,
            currentClass.BranchesCovered,
            currentClass.BranchesTotal,
            currentClass.FunctionsCovered,
            currentClass.FunctionsTotal);
        if (counters.HasMeasurements)
        {
            sources[currentClass.SourcePath] = sources
                .GetValueOrDefault(currentClass.SourcePath)
                .Add(counters);
        }
    }

    private sealed class ClassState(string? sourcePath, int depth)
    {
        public string? SourcePath { get; } = sourcePath;

        public int Depth { get; } = depth;

        public decimal LinesCovered { get; set; }

        public decimal LinesTotal { get; set; }

        public decimal BranchesCovered { get; set; }

        public decimal BranchesTotal { get; set; }

        public decimal FunctionsCovered { get; set; }

        public decimal FunctionsTotal { get; set; }
    }

    private sealed class MethodState(int depth)
    {
        public int Depth { get; } = depth;

        public bool HasCoveredLine { get; set; }
    }
}
