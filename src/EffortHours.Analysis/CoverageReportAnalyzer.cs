using System.Globalization;
using System.Security.Cryptography;
using System.Xml;
using EffortHours.Contracts.V1;

namespace EffortHours.Analysis;

public sealed class CoverageReportAnalyzer : IRepositoryEvidenceAnalyzer
{
    public const string AnalyzerName = "efforthours.coverage-analyzer";
    public const string AnalyzerVersion = "0.1.0";

    private const long MaximumReportBytes = 128L * 1024L * 1024L;
    private readonly IRepositoryFileSystem _fileSystem;

    public CoverageReportAnalyzer()
        : this(PhysicalRepositoryFileSystem.Instance)
    {
    }

    public CoverageReportAnalyzer(IRepositoryFileSystem fileSystem)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
    }

    public string Ecosystem => "common";

    public bool AppliesToAllRepositories => true;

    public async Task<RepositoryAnalysisContribution> AnalyzeAsync(
        string repositoryPath,
        RepositoryEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(evidence);

        EvidenceFact[] candidates = [.. evidence.Facts
            .Where(IsCoverageArtifact)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        if (candidates.Length == 0)
        {
            return new RepositoryAnalysisContribution();
        }

        string rootPath = _fileSystem.GetFullPath(repositoryPath);
        CoverageScopeIndex scopeIndex = new(evidence);
        List<EvidenceFact> facts = [];
        List<Diagnostic> diagnostics = [];
        foreach (EvidenceFact candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await AnalyzeArtifactAsync(
                rootPath,
                candidate,
                scopeIndex,
                facts,
                diagnostics,
                cancellationToken).ConfigureAwait(false);
        }

        diagnostics.Add(CoverageEvidence.Diagnostic(
            "FB2500",
            DiagnosticSeverity.Information,
            "Coverage artifacts were parsed statically; EffortHours did not execute tests or generate coverage."));
        facts.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        diagnostics.Sort(CoverageEvidence.CompareDiagnostics);
        return new RepositoryAnalysisContribution
        {
            Facts = facts,
            Diagnostics = diagnostics,
        };
    }

    private async Task AnalyzeArtifactAsync(
        string rootPath,
        EvidenceFact candidate,
        CoverageScopeIndex scopeIndex,
        List<EvidenceFact> facts,
        List<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        string relativePath = candidate.Scope;
        CoverageArtifactKind kind = ClassifyArtifact(relativePath);
        decimal bytes = candidate.Measurements
            .Where(measurement => measurement.Name == "bytes")
            .Sum(measurement => measurement.Value);
        if (bytes > MaximumReportBytes)
        {
            diagnostics.Add(CoverageEvidence.Diagnostic(
                "FB2501",
                DiagnosticSeverity.Warning,
                $"Coverage artifact '{relativePath}' exceeds the {MaximumReportBytes.ToString(CultureInfo.InvariantCulture)}-byte static parsing limit and was not measured.",
                relativePath));
            return;
        }

        if (!TryResolvePath(rootPath, relativePath, out string fullPath) ||
            !_fileSystem.FileExists(fullPath) ||
            (_fileSystem.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
        {
            diagnostics.Add(CoverageEvidence.Diagnostic(
                "FB2502",
                DiagnosticSeverity.Warning,
                $"Coverage artifact '{relativePath}' changed or became unsafe after inventory and was not measured.",
                relativePath));
            return;
        }

        RepositoryFileMetadata metadata = _fileSystem.GetFileMetadata(fullPath);
        if (!metadata.Exists || metadata.Length > MaximumReportBytes)
        {
            diagnostics.Add(CoverageEvidence.Diagnostic(
                metadata.Length > MaximumReportBytes ? "FB2501" : "FB2502",
                DiagnosticSeverity.Warning,
                metadata.Length > MaximumReportBytes
                    ? $"Coverage artifact '{relativePath}' exceeds the {MaximumReportBytes.ToString(CultureInfo.InvariantCulture)}-byte static parsing limit and was not measured."
                    : $"Coverage artifact '{relativePath}' changed after inventory and was not measured.",
                relativePath));
            return;
        }

        string? expectedDigest = FindTagValue(candidate.Tags, "sha256:");
        try
        {
            (CoverageReportData? report, string digest) = await ParseAndHashAsync(
                fullPath,
                kind,
                cancellationToken).ConfigureAwait(false);
            if (expectedDigest is null || !digest.Equals(expectedDigest, StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(CoverageEvidence.Diagnostic(
                    "FB2502",
                    DiagnosticSeverity.Warning,
                    $"Coverage artifact '{relativePath}' changed after inventory and its measurements were discarded.",
                    relativePath));
                return;
            }

            if (report is null || !report.HasMeasurements)
            {
                diagnostics.Add(CoverageEvidence.Diagnostic(
                    "FB2503",
                    DiagnosticSeverity.Information,
                    $"Coverage artifact '{relativePath}' is not a supported LCOV or Cobertura report with usable measurements.",
                    relativePath));
                return;
            }

            AddReportFacts(relativePath, report, scopeIndex, facts, diagnostics);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or XmlException or FormatException or
            OverflowException or InvalidOperationException)
        {
            diagnostics.Add(CoverageEvidence.Diagnostic(
                "FB2503",
                DiagnosticSeverity.Warning,
                $"Coverage artifact '{relativePath}' could not be parsed safely ({exception.GetType().Name}).",
                relativePath));
        }
    }

    private async Task<(CoverageReportData? Report, string Digest)> ParseAndHashAsync(
        string fullPath,
        CoverageArtifactKind kind,
        CancellationToken cancellationToken)
    {
        using SHA256 hash = SHA256.Create();
        await using Stream source = _fileSystem.OpenRead(fullPath, 64 * 1024);
        using Stream bounded = new BoundedReadStream(source, MaximumReportBytes);
        await using CryptoStream hashing = new(
            bounded,
            hash,
            CryptoStreamMode.Read,
            leaveOpen: false);
        CoverageReportData? report = kind switch
        {
            CoverageArtifactKind.Lcov => await LcovCoverageParser.ParseAsync(
                hashing,
                cancellationToken).ConfigureAwait(false),
            CoverageArtifactKind.Xml => await CoberturaCoverageParser.ParseAsync(
                hashing,
                cancellationToken).ConfigureAwait(false),
            _ => null,
        };
        await DrainAsync(hashing, cancellationToken).ConfigureAwait(false);
        string digest = Convert.ToHexString(hash.Hash ?? []).ToLowerInvariant();
        return (report, digest);
    }

    private static void AddReportFacts(
        string reportPath,
        CoverageReportData report,
        CoverageScopeIndex scopeIndex,
        List<EvidenceFact> facts,
        List<Diagnostic> diagnostics)
    {
        Dictionary<string, ScopedCoverage> scoped = new(StringComparer.Ordinal);
        int unmatched = 0;
        foreach (CoverageSourceResult source in report.Sources)
        {
            if (!scopeIndex.TryResolve(source.ReportedPath, out CoverageProductionScope? scope) ||
                scope is null)
            {
                unmatched++;
                continue;
            }

            ScopedCoverage current = scoped.GetValueOrDefault(scope.Key) ?? new ScopedCoverage(scope);
            scoped[scope.Key] = current with { Counters = current.Counters.Add(source.Counters) };
        }

        bool inferredSingleScope = false;
        if (scoped.Count == 0 && report.Sources.Count == 0 && scopeIndex.ProductionScopes.Count == 1)
        {
            CoverageProductionScope scope = scopeIndex.ProductionScopes[0];
            scoped.Add(scope.Key, new ScopedCoverage(scope, report.OverallCounters));
            inferredSingleScope = true;
        }

        if (unmatched > 0)
        {
            diagnostics.Add(CoverageEvidence.Diagnostic(
                "FB2504",
                DiagnosticSeverity.Warning,
                $"Coverage artifact '{reportPath}' contains {unmatched.ToString(CultureInfo.InvariantCulture)} source record(s) that could not be matched uniquely to maintained production files; unmatched paths were not emitted.",
                reportPath));
        }

        if (scoped.Count == 0)
        {
            diagnostics.Add(CoverageEvidence.Diagnostic(
                "FB2504",
                DiagnosticSeverity.Warning,
                $"Coverage artifact '{reportPath}' could not be assigned to a maintained production scope and was not valued.",
                reportPath));
            return;
        }

        bool useOverall = scoped.Count == 1 && unmatched == 0;
        foreach (ScopedCoverage item in scoped.Values.OrderBy(item => item.Scope.Key, StringComparer.Ordinal))
        {
            CoverageCounters counters = useOverall
                ? report.OverallCounters.PreferAvailable(item.Counters)
                : item.Counters;
            EvidenceFact? fact = CoverageEvidence.CreateFact(
                reportPath,
                report,
                item.Scope,
                counters,
                useOverall ? report.OverallPercentages : null,
                inferredSingleScope);
            if (fact is not null)
            {
                facts.Add(fact);
            }
        }
    }

    private static bool IsCoverageArtifact(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.File &&
        fact.Tags.Contains("role:coverage", StringComparer.Ordinal) &&
        fact.Tags.Contains("content:text", StringComparer.Ordinal) &&
        ClassifyArtifact(fact.Scope) != CoverageArtifactKind.Unsupported;

    private static CoverageArtifactKind ClassifyArtifact(string path)
    {
        string name = Path.GetFileName(path).ToLowerInvariant();
        string extension = Path.GetExtension(name);
        if (name == "lcov.info" || extension == ".lcov")
        {
            return CoverageArtifactKind.Lcov;
        }

        return extension == ".xml" ? CoverageArtifactKind.Xml : CoverageArtifactKind.Unsupported;
    }

    private static bool TryResolvePath(string rootPath, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (Path.IsPathRooted(relativePath))
        {
            return false;
        }

        string candidate = Path.GetFullPath(Path.Combine(
            rootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string resolved = Path.GetRelativePath(rootPath, candidate);
        if (resolved == ".." ||
            Path.IsPathRooted(resolved) ||
            resolved.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
            resolved.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }

    private static async Task DrainAsync(CryptoStream stream, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[16 * 1024];
        while (await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false) > 0)
        {
        }
    }

    private static string? FindTagValue(IEnumerable<string> tags, string prefix) => tags
        .FirstOrDefault(tag => tag.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];

    private enum CoverageArtifactKind
    {
        Unsupported,
        Lcov,
        Xml,
    }

    private sealed record ScopedCoverage(
        CoverageProductionScope Scope,
        CoverageCounters Counters = default);
}
