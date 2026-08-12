using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Scripting;

internal sealed class ScriptInvocationContextReader(ScriptTextReader reader)
{
    private const int MaximumInvocationContexts = 500;

    private static readonly string[] ContextRoles =
    [
        "test", "ci", "pipeline", "deploy", "provision", "release", "publish",
        "package", "pack", "build", "compile", "restore", "lint", "format",
    ];

    public async Task<ScriptInvocationReadResult> ReadAsync(
        IReadOnlyList<EvidenceFact> allFiles,
        IReadOnlyList<EvidenceFact> scripts,
        CancellationToken cancellationToken)
    {
        Dictionary<string, HashSet<ScriptRole>> roles = new(StringComparer.Ordinal);
        List<Diagnostic> diagnostics = [];
        Dictionary<string, string> exactScriptPaths = scripts.ToDictionary(
            script => script.Scope,
            script => script.Scope,
            StringComparer.Ordinal);
        Dictionary<string, string?> caseInsensitiveScriptPaths =
            CreateUnambiguousCaseInsensitivePaths(exactScriptPaths.Keys);
        EvidenceFact[] candidates = [.. allFiles
            .Where(IsInvocationContext)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        if (candidates.Length > MaximumInvocationContexts)
        {
            diagnostics.Add(ScriptEvidence.Diagnostic(
                "FB8506",
                DiagnosticSeverity.Information,
                $"Script invocation-role analysis read the first {MaximumInvocationContexts} of {candidates.Length} admitted context files; later invocation evidence was omitted deterministically."));
        }
        EvidenceFact[] contexts = [.. candidates.Take(MaximumInvocationContexts)];

        foreach (EvidenceFact context in contexts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ScriptTextReadResult read = await reader.ReadAsync(context, cancellationToken)
                .ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
                continue;
            }

            string invokerDirectory = NormalizeDirectory(context.Scope);
            foreach (ScriptReference reference in FindReferences(
                read.Text!,
                invokerDirectory,
                exactScriptPaths,
                caseInsensitiveScriptPaths))
            {
                ScriptRole role = RoleForContext(context, read.Text!, reference.Index);
                if (!roles.TryGetValue(reference.Path, out HashSet<ScriptRole>? scriptRoles))
                {
                    scriptRoles = [];
                    roles.Add(reference.Path, scriptRoles);
                }
                scriptRoles.Add(role);
            }
        }

        return new ScriptInvocationReadResult(
            roles.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<ScriptRole>)[.. pair.Value.OrderBy(value => value)],
                StringComparer.Ordinal),
            diagnostics);
    }

    private static bool IsInvocationContext(EvidenceFact fact)
    {
        if (fact.Kind != EvidenceKinds.File || fact.Tags.Any(tag => tag is
            "classification:generated" or "classification:minified" or
            "classification:vendored" or "content:binary")) return false;
        string? role = ScriptEvidence.TagValue(fact.Tags, "role:");
        return role is "package-manifest" or "build-configuration" or
            "ci-configuration" or "infrastructure" or "container-configuration";
    }

    private static IReadOnlyList<ScriptReference> FindReferences(
        string text,
        string invokerDirectory,
        Dictionary<string, string> exactScriptPaths,
        Dictionary<string, string?> caseInsensitiveScriptPaths)
    {
        string normalizedText = text.Replace('\\', '/');
        Dictionary<string, int> matches = new(StringComparer.Ordinal);
        for (int index = 0; index < normalizedText.Length; index++)
        {
            if (normalizedText[index] is '\'' or '"')
            {
                int end = normalizedText.IndexOf(normalizedText[index], index + 1);
                if (end > index + 1)
                    AddReference(normalizedText[(index + 1)..end], index + 1);
            }

            if (!IsReferenceCharacter(normalizedText[index])) continue;
            int start = index;
            while (index < normalizedText.Length && IsReferenceCharacter(normalizedText[index]))
                index++;
            AddReference(normalizedText[start..index], start);
            index--;
        }

        return [.. matches
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new ScriptReference(pair.Key, pair.Value))];

        void AddReference(string value, int index)
        {
            string candidate = value.Trim();
            while (candidate.StartsWith("./", StringComparison.Ordinal)) candidate = candidate[2..];
            if (candidate.Length == 0 ||
                candidate.StartsWith("../", StringComparison.Ordinal) ||
                candidate.Contains("/../", StringComparison.Ordinal) ||
                Path.IsPathRooted(candidate)) return;

            string? matched = ResolveReference(
                candidate,
                invokerDirectory,
                exactScriptPaths,
                caseInsensitiveScriptPaths);
            if (matched is null) return;
            if (!matches.TryGetValue(matched, out int previous) || index < previous)
                matches[matched] = index;
        }
    }

    private static Dictionary<string, string?> CreateUnambiguousCaseInsensitivePaths(
        IEnumerable<string> paths)
    {
        Dictionary<string, string?> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (string path in paths.Order(StringComparer.Ordinal))
        {
            if (!result.TryAdd(path, path)) result[path] = null;
        }
        return result;
    }

    private static string? ResolveReference(
        string candidate,
        string invokerDirectory,
        Dictionary<string, string> exactScriptPaths,
        Dictionary<string, string?> caseInsensitiveScriptPaths)
    {
        string? relative = invokerDirectory == "."
            ? null
            : $"{invokerDirectory}/{candidate}";
        if (exactScriptPaths.TryGetValue(candidate, out string? matched) ||
            relative is not null && exactScriptPaths.TryGetValue(relative, out matched))
            return matched;
        if (caseInsensitiveScriptPaths.TryGetValue(candidate, out matched) && matched is not null)
            return matched;
        return relative is not null &&
            caseInsensitiveScriptPaths.TryGetValue(relative, out matched)
                ? matched
                : null;
    }

    private static bool IsReferenceCharacter(char value) =>
        char.IsLetterOrDigit(value) || value is '.' or '/' or '_' or '-' or '+' or '@';

    private static ScriptRole RoleForContext(EvidenceFact context, string text, int match)
    {
        string? role = ScriptEvidence.TagValue(context.Tags, "role:");
        ScriptRole? mapped = role switch
        {
            "ci-configuration" => ScriptRole.Ci,
            "infrastructure" or "container-configuration" => ScriptRole.Infrastructure,
            "build-configuration" => ScriptRole.Build,
            _ => null,
        };
        if (mapped is not null) return mapped.Value;

        int start = Math.Max(0, match - 96);
        int length = match - start;
        string window = text.Substring(start, length).ToLowerInvariant();
        string? nearest = ContextRoles
            .Select(candidate => new
            {
                Value = candidate,
                Index = window.LastIndexOf(candidate, StringComparison.Ordinal),
            })
            .Where(candidate => candidate.Index >= 0)
            .OrderBy(candidate => Math.Abs((start + candidate.Index) - match))
            .ThenBy(candidate => Array.IndexOf(ContextRoles, candidate.Value))
            .Select(candidate => candidate.Value)
            .FirstOrDefault();
        if (nearest is not null)
        {
            return nearest switch
            {
                "test" => ScriptRole.Test,
                "ci" or "pipeline" => ScriptRole.Ci,
                "deploy" or "provision" => ScriptRole.Infrastructure,
                "release" or "publish" or "package" or "pack" => ScriptRole.Delivery,
                _ => ScriptRole.Build,
            };
        }
        return ScriptRole.Build;
    }

    private static string NormalizeDirectory(string path)
    {
        string? directory = Path.GetDirectoryName(path.Replace('/', Path.DirectorySeparatorChar));
        return string.IsNullOrEmpty(directory) ? "." : directory.Replace('\\', '/');
    }
}

internal sealed record ScriptReference(string Path, int Index);

internal sealed record ScriptInvocationReadResult(
    IReadOnlyDictionary<string, IReadOnlyList<ScriptRole>> RolesByPath,
    IReadOnlyList<Diagnostic> Diagnostics);
