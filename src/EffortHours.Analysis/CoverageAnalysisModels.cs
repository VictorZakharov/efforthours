using EffortHours.Contracts.V1;

namespace EffortHours.Analysis;

internal readonly record struct CoverageCounters(
    decimal LinesCovered,
    decimal LinesTotal,
    decimal BranchesCovered,
    decimal BranchesTotal,
    decimal FunctionsCovered,
    decimal FunctionsTotal)
{
    public bool HasMeasurements =>
        LinesTotal > 0m || BranchesTotal > 0m || FunctionsTotal > 0m;

    public CoverageCounters Add(CoverageCounters other) => new(
        checked(LinesCovered + other.LinesCovered),
        checked(LinesTotal + other.LinesTotal),
        checked(BranchesCovered + other.BranchesCovered),
        checked(BranchesTotal + other.BranchesTotal),
        checked(FunctionsCovered + other.FunctionsCovered),
        checked(FunctionsTotal + other.FunctionsTotal));

    public CoverageCounters PreferAvailable(CoverageCounters fallback) => new(
        LinesTotal > 0m ? LinesCovered : fallback.LinesCovered,
        LinesTotal > 0m ? LinesTotal : fallback.LinesTotal,
        BranchesTotal > 0m ? BranchesCovered : fallback.BranchesCovered,
        BranchesTotal > 0m ? BranchesTotal : fallback.BranchesTotal,
        FunctionsTotal > 0m ? FunctionsCovered : fallback.FunctionsCovered,
        FunctionsTotal > 0m ? FunctionsTotal : fallback.FunctionsTotal);
}

internal sealed record CoveragePercentages(
    decimal? Lines,
    decimal? Branches,
    decimal? Functions)
{
    public bool HasMeasurements => Lines is not null || Branches is not null || Functions is not null;
}

internal sealed record CoverageSourceResult(
    string ReportedPath,
    CoverageCounters Counters);

internal sealed record CoverageReportData(
    string Format,
    IReadOnlyList<CoverageSourceResult> Sources,
    CoverageCounters OverallCounters,
    CoveragePercentages? OverallPercentages = null)
{
    public bool HasMeasurements =>
        OverallCounters.HasMeasurements ||
        OverallPercentages?.HasMeasurements == true ||
        Sources.Any(source => source.Counters.HasMeasurements);
}

internal sealed record CoverageProductionScope(
    string Scope,
    string Directory,
    string Ecosystem)
{
    public string Key => $"{Ecosystem}:{Scope}";
}

internal sealed record CoverageSourceFile(
    string Path,
    IReadOnlyList<string> Ecosystems);

internal sealed class CoverageScopeIndex
{
    private readonly IReadOnlyList<CoverageSourceFile> _files;

    public CoverageScopeIndex(RepositoryEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ProductionScopes = CreateScopes(evidence);
        _files = [.. evidence.Facts
            .Where(fact => fact.Kind == EvidenceKinds.File)
            .Where(IsMaintainedProductionSource)
            .Select(CreateSourceFile)
            .Where(file => file.Ecosystems.Count > 0)
            .OrderBy(file => file.Path, StringComparer.Ordinal)];
    }

    public IReadOnlyList<CoverageProductionScope> ProductionScopes { get; }

    public bool TryResolve(string reportedPath, out CoverageProductionScope? scope)
    {
        scope = null;
        string normalized = NormalizeReportedPath(reportedPath);
        if (normalized.Length == 0)
        {
            return false;
        }

        CoverageSourceFile? file = FindUniqueFile(normalized, StringComparison.Ordinal) ??
            FindUniqueFile(normalized, StringComparison.OrdinalIgnoreCase);
        if (file is null)
        {
            return false;
        }

        scope = ProductionScopes
            .Where(candidate => file.Ecosystems.Contains(candidate.Ecosystem, StringComparer.Ordinal))
            .Where(candidate => IsWithin(file.Path, candidate.Directory))
            .OrderByDescending(candidate => candidate.Directory.Length)
            .ThenBy(candidate => candidate.Key, StringComparer.Ordinal)
            .FirstOrDefault();
        return scope is not null;
    }

    private CoverageSourceFile? FindUniqueFile(string reportedPath, StringComparison comparison)
    {
        CoverageSourceFile[] exact = [.. _files.Where(file => file.Path.Equals(reportedPath, comparison))];
        if (exact.Length == 1)
        {
            return exact[0];
        }

        CoverageSourceFile[] suffix = [.. _files.Where(file =>
            reportedPath.EndsWith("/" + file.Path, comparison) ||
            file.Path.EndsWith("/" + reportedPath, comparison))];
        return suffix.Length == 1 ? suffix[0] : null;
    }

    private static IReadOnlyList<CoverageProductionScope> CreateScopes(RepositoryEvidence evidence)
    {
        List<CoverageProductionScope> scopes = [];
        foreach (EvidenceFact fact in evidence.Facts.Where(fact =>
            fact.Kind == EvidenceKinds.DotNetProject &&
            fact.Id.StartsWith("dotnet:project:", StringComparison.Ordinal) &&
            !fact.Tags.Contains("project-role:test", StringComparer.Ordinal)))
        {
            scopes.Add(new CoverageProductionScope(
                fact.Scope,
                ParentDirectory(fact.Scope),
                "dotnet"));
        }

        foreach (EvidenceFact fact in evidence.Facts.Where(fact =>
            fact.Kind == EvidenceKinds.JavaScriptPackage &&
            fact.Id.StartsWith("javascript:package:", StringComparison.Ordinal) &&
            !fact.Tags.Contains("package-role:test", StringComparer.Ordinal)))
        {
            scopes.Add(new CoverageProductionScope(
                fact.Scope,
                NormalizeDirectory(fact.Scope),
                "javascript"));
        }

        return [.. scopes
            .DistinctBy(scope => scope.Key, StringComparer.Ordinal)
            .OrderBy(scope => scope.Key, StringComparer.Ordinal)];
    }

    private static bool IsMaintainedProductionSource(EvidenceFact fact) =>
        fact.Tags.Contains("role:source", StringComparer.Ordinal) &&
        !fact.Tags.Contains("classification:test", StringComparer.Ordinal) &&
        !fact.Tags.Contains("classification:generated", StringComparer.Ordinal) &&
        !fact.Tags.Contains("classification:minified", StringComparer.Ordinal) &&
        !fact.Tags.Contains("classification:vendored", StringComparer.Ordinal) &&
        !fact.Tags.Contains("content:binary", StringComparer.Ordinal);

    private static CoverageSourceFile CreateSourceFile(EvidenceFact fact)
    {
        string[] ecosystems = [.. fact.Tags
            .Where(tag => tag.StartsWith("ecosystem:", StringComparison.Ordinal))
            .Select(tag => tag["ecosystem:".Length..] == "typescript"
                ? "javascript"
                : tag["ecosystem:".Length..])
            .Where(ecosystem => ecosystem is "dotnet" or "javascript")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(ecosystem => ecosystem, StringComparer.Ordinal)];
        return new CoverageSourceFile(fact.Scope, ecosystems);
    }

    private static string NormalizeReportedPath(string path)
    {
        string normalized = path.Trim().Replace('\\', '/');
        if (Uri.TryCreate(normalized, UriKind.Absolute, out Uri? uri) && uri.IsFile)
        {
            normalized = uri.LocalPath.Replace('\\', '/');
        }

        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized.Trim('/');
    }

    private static string ParentDirectory(string path)
    {
        string normalized = path.Replace('\\', '/');
        int separator = normalized.LastIndexOf('/');
        return separator < 0 ? "." : normalized[..separator];
    }

    private static string NormalizeDirectory(string path) =>
        string.IsNullOrWhiteSpace(path) ? "." : path.Trim('/').Replace('\\', '/');

    private static bool IsWithin(string path, string directory) =>
        directory == "." ||
        path.Equals(directory, StringComparison.Ordinal) ||
        path.StartsWith(directory + "/", StringComparison.Ordinal);
}
