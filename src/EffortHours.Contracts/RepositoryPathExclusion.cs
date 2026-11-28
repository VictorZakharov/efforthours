using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace EffortHours.Contracts;

/// <summary>
/// Reviewed repository-relative path exclusion applied at admission, before inventory, scanning, or estimation.
/// Globs use the ordered-area selector semantics: relative paths/directories, <c>*</c>, <c>**</c>, and <c>?</c>.
/// </summary>
public sealed class RepositoryPathExclusion
{
    public const string Policy = "repository-path-exclusion/1.0.0";
    public const int MaximumGlobs = 256;
    public const int MaximumGlobLength = 1024;

    // One automaton per glob, as for area selectors: a single alternation of hundreds of globs can exceed the
    // non-backtracking engine's automaton limit.
    private readonly Regex[] _patterns;

    private RepositoryPathExclusion(IReadOnlyList<string> globs)
    {
        Globs = [.. globs.Order(StringComparer.Ordinal)];
        _patterns = [.. Globs.Select(glob => new Regex("^" + Expression(glob) + "$",
            RegexOptions.CultureInvariant | RegexOptions.NonBacktracking))];
        Digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            ContractJson.SerializeCompact(new { Policy, Exclude = Globs }))));
    }

    /// <summary>Ordinal-sorted globs; authored order has no meaning.</summary>
    public IReadOnlyList<string> Globs { get; }

    /// <summary>Content digest of the policy and sorted globs.</summary>
    public string Digest { get; }

    public bool Excludes(string path) => Array.Exists(_patterns, pattern => pattern.IsMatch(path));

    /// <summary>Returns null when no exclusion is configured; throws <see cref="InvalidDataException"/> otherwise invalid.</summary>
    public static RepositoryPathExclusion? Create(IReadOnlyList<string>? globs)
    {
        if (globs is null) return null;
        IReadOnlyList<string> errors = Validate(globs);
        if (errors.Count > 0) throw new InvalidDataException(errors[0]);
        return new(globs);
    }

    public static IReadOnlyList<string> Validate(IReadOnlyList<string>? globs)
    {
        List<string> errors = [];
        if (globs is null) return errors;
        if (globs.Count is < 1 or > MaximumGlobs)
            errors.Add($"Path exclusion requires 1 to {MaximumGlobs} globs; omit 'exclude' to exclude nothing.");
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string? glob in globs)
        {
            if (string.IsNullOrWhiteSpace(glob) || glob.Length > MaximumGlobLength || glob.Contains('\\') ||
                glob.Contains(':') || glob.StartsWith('/') || glob.Any(char.IsControl) ||
                glob.Split('/').Any(segment => segment is "" or "." or ".."))
            {
                errors.Add("Exclusion globs must be nonempty safe repository-relative paths without '..', absolute roots, or backslashes.");
                continue;
            }
            if (glob.Contains('[') || glob.Contains(']') || glob.Contains('!'))
                errors.Add("Exclusion globs support relative paths, '*', '**', and '?' only.");
            else if (MatchesEverything(glob))
                errors.Add("An exclusion glob cannot exclude every repository path.");
            if (!seen.Add(glob)) errors.Add("Exclusion globs must be unique.");
        }
        return errors;
    }

    private static bool MatchesEverything(string glob)
    {
        string[] segments = glob.Split('/');
        return segments.All(segment => segment == "**") ||
            segments[^1] == "*" && segments[..^1].All(segment => segment == "**") && segments.Length > 1;
    }

    /// <summary>The ordered-area selector expression (anchored by callers).</summary>
    public static string Expression(string glob)
    {
        string expression = Regex.Escape(glob)
            .Replace(@"\*\*/", "(?:.*/)?", StringComparison.Ordinal)
            .Replace(@"\*\*", ".*", StringComparison.Ordinal)
            .Replace(@"\*", "[^/]*", StringComparison.Ordinal)
            .Replace(@"\?", "[^/]", StringComparison.Ordinal);
        if (!glob.Contains('*') && !glob.Contains('?')) expression += "(?:/.*)?";
        return expression;
    }
}
