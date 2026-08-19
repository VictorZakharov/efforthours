using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.DotNet;

internal sealed partial class RazorFileAnalyzer(
    IRepositoryFileSystem fileSystem,
    string rootPath)
{
    private readonly IRepositoryFileSystem _fileSystem = fileSystem;
    private readonly string _rootPath = fileSystem.GetFullPath(rootPath);

    public async Task<RepositoryAnalysisContribution> AnalyzeAsync(
        string relativePath,
        string expectedSha256,
        string projectScope,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(Path.Combine(
            _rootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        RepositoryAnalysisArtifactCache? artifactCache =
            (_fileSystem as IRepositoryAnalysisArtifactCacheProvider)?.AnalysisArtifactCache;
        string? contentId = null;
        try
        {
            contentId = _fileSystem.GetFileMetadata(fullPath).ContentId;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }

        string? artifactKey = contentId is null
            ? null
            : AnalysisArtifactKey(
                contentId,
                expectedSha256,
                relativePath,
                projectScope);
        if (artifactKey is not null && artifactCache is not null)
        {
            return await artifactCache.GetOrCreateAsync(
                artifactKey,
                itemCancellationToken => AnalyzeUncachedAsync(
                    fullPath,
                    relativePath,
                    expectedSha256,
                    projectScope,
                    itemCancellationToken),
                cancellationToken).ConfigureAwait(false);
        }

        return await AnalyzeUncachedAsync(
            fullPath,
            relativePath,
            expectedSha256,
            projectScope,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<RepositoryAnalysisContribution> AnalyzeUncachedAsync(
        string fullPath,
        string relativePath,
        string expectedSha256,
        string projectScope,
        CancellationToken cancellationToken)
    {
        byte[] bytes;
        try
        {
            bytes = await _fileSystem.ReadAllBytesAsync(
                fullPath,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failure(
                relativePath,
                $"Could not inspect Razor file '{relativePath}': repository content could not be read.");
        }

        using IDisposable cpuLease = await RepositoryAnalysisConcurrency
            .AcquireFileAnalysisAsync(
                RepositoryAnalysisWorkKind.SemanticFileAnalysis,
                cancellationToken)
            .ConfigureAwait(false);
        string actualSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        if (!actualSha256.Equals(expectedSha256, StringComparison.Ordinal))
        {
            return Failure(
                relativePath,
                $"Razor file '{relativePath}' changed after common scanning; semantic evidence was skipped.");
        }

        string text;
        using (MemoryStream stream = new(bytes, writable: false))
        using (StreamReader reader = new(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false),
            detectEncodingFromByteOrderMarks: true))
        {
            text = reader.ReadToEnd();
        }

        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        int pageDirectives = 0;
        int injections = 0;
        int forms = 0;
        int codeBlocks = 0;
        int componentUsages = 0;
        int modelDirectives = 0;
        List<EvidenceLocation> locations = [];
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index].TrimStart();
            if (line.StartsWith("@page", StringComparison.Ordinal))
            {
                pageDirectives++;
                locations.Add(DotNetEvidence.Location(relativePath, index + 1, "@page"));
            }

            if (line.StartsWith("@inject", StringComparison.Ordinal))
            {
                injections++;
            }

            if (line.StartsWith("@model", StringComparison.Ordinal))
            {
                modelDirectives++;
            }

            if (line.Contains("<EditForm", StringComparison.Ordinal) ||
                line.Contains("<form", StringComparison.OrdinalIgnoreCase))
            {
                forms++;
            }

            if (line.Contains("@code", StringComparison.Ordinal) ||
                line.Contains("@functions", StringComparison.Ordinal))
            {
                codeBlocks++;
            }

            componentUsages += ComponentTagRegex().Count(line);
        }

        EvidenceFact fact = DotNetEvidence.Fact(
            $"dotnet:razor:{relativePath}",
            EvidenceKinds.UserInterface,
            projectScope,
            $"Razor page, view, or component structure detected in '{relativePath}'.",
            EvidenceSourceKind.Inferred,
            "Razor directive and markup-shape classification without rendering or execution",
            locations,
            [
                DotNetEvidence.Measurement("files", 1, "files"),
                DotNetEvidence.Measurement("page-directives", pageDirectives, "directives"),
                DotNetEvidence.Measurement("model-directives", modelDirectives, "directives"),
                DotNetEvidence.Measurement("injections", injections, "directives"),
                DotNetEvidence.Measurement("forms", forms, "forms"),
                DotNetEvidence.Measurement("code-blocks", codeBlocks, "blocks"),
                DotNetEvidence.Measurement("component-usages", componentUsages, "usages"),
            ],
            [Path.GetExtension(relativePath).Equals(".razor", StringComparison.OrdinalIgnoreCase)
                ? "razor-kind:component"
                : "razor-kind:view"]);
        return new RepositoryAnalysisContribution { Facts = [fact] };
    }

    private static string AnalysisArtifactKey(
        string contentId,
        string expectedSha256,
        string relativePath,
        string projectScope)
    {
        string identity = string.Join(
            '\0',
            contentId,
            expectedSha256,
            relativePath,
            projectScope);
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))
            .ToLowerInvariant();
        return $"dotnet-razor/{DotNetEvidence.AnalyzerVersion}/{digest}";
    }

    private static RepositoryAnalysisContribution Failure(string path, string message) => new()
    {
        Diagnostics =
        [
            DotNetEvidence.Diagnostic(
                "FB3103",
                DiagnosticSeverity.Warning,
                message,
                path),
        ],
    };

    [GeneratedRegex("<[A-Z][A-Za-z0-9_.]*(?:\\s|>|/)", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex ComponentTagRegex();
}
