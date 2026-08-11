using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Sql;

internal sealed record SqlScopeOwnership(
    string Scope,
    string Ecosystem,
    bool Standalone,
    bool Ambiguous);

internal sealed class SqlScopeResolver
{
    private readonly SqlOwnerCandidate[] _candidates;

    public SqlScopeResolver(RepositoryEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        _candidates =
        [
            .. evidence.Facts
                .Where(fact => fact.Kind == EvidenceKinds.DotNetProject &&
                    fact.Id.StartsWith("dotnet:project:", StringComparison.Ordinal))
                .Select(fact => new SqlOwnerCandidate(
                    fact.Scope,
                    ProjectDirectory(fact.Scope),
                    "dotnet")),
            .. evidence.Facts
                .Where(fact => fact.Kind == EvidenceKinds.JavaScriptPackage &&
                    fact.Id.StartsWith("javascript:package:", StringComparison.Ordinal))
                .Select(fact => new SqlOwnerCandidate(
                    fact.Scope,
                    NormalizeDirectory(fact.Scope),
                    "javascript")),
        ];
    }

    public SqlScopeOwnership Resolve(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        SqlOwnerCandidate[] matches =
        [
            .. _candidates
                .Where(candidate => IsWithin(path, candidate.Directory))
                .OrderByDescending(candidate => candidate.Directory.Length)
                .ThenBy(candidate => candidate.Ecosystem, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Scope, StringComparer.Ordinal),
        ];
        if (matches.Length == 0)
        {
            return Standalone(ambiguous: false);
        }

        int depth = matches[0].Directory.Length;
        SqlOwnerCandidate[] closest =
        [
            .. matches
                .Where(candidate => candidate.Directory.Length == depth)
                .Distinct(),
        ];
        return closest.Length == 1
            ? new SqlScopeOwnership(
                closest[0].Scope,
                closest[0].Ecosystem,
                Standalone: false,
                Ambiguous: false)
            : Standalone(ambiguous: true);
    }

    private static SqlScopeOwnership Standalone(bool ambiguous) => new(
        ".",
        "sql",
        Standalone: true,
        Ambiguous: ambiguous);

    private static string ProjectDirectory(string scope)
    {
        string normalized = scope.Replace('\\', '/');
        int separator = normalized.LastIndexOf('/');
        return separator < 0 ? "." : normalized[..separator];
    }

    private static string NormalizeDirectory(string scope) =>
        string.IsNullOrEmpty(scope) ? "." : scope.TrimEnd('/');

    private static bool IsWithin(string path, string directory) =>
        directory == "." ||
        path.Equals(directory, StringComparison.Ordinal) ||
        path.StartsWith(directory + "/", StringComparison.Ordinal);

    private sealed record SqlOwnerCandidate(
        string Scope,
        string Directory,
        string Ecosystem);
}
