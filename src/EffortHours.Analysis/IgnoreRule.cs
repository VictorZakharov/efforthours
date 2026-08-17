namespace EffortHours.Analysis;

internal sealed class IgnoreRule
{
    private readonly string _basePath;
    private readonly bool _directoryOnly;
    private readonly IgnoreGlobPattern _pattern;

    private IgnoreRule(
        string basePath,
        bool negated,
        bool directoryOnly,
        IgnoreGlobPattern pattern)
    {
        _basePath = basePath;
        IsNegated = negated;
        _directoryOnly = directoryOnly;
        _pattern = pattern;
    }

    public bool IsNegated { get; }

    public static bool TryCreate(string line, string basePath, out IgnoreRule? rule) =>
        TryCreate(line, basePath, out rule, out _);

    public static bool TryCreate(
        string line,
        string basePath,
        out IgnoreRule? rule,
        out IgnoreRuleCreationFailure failure)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(basePath);

        rule = null;
        failure = IgnoreRuleCreationFailure.None;
        string pattern = line.TrimEnd();
        if (pattern.Length == 0 || pattern[0] == '#')
        {
            return false;
        }

        bool negated = pattern[0] == '!';
        if (negated)
        {
            pattern = pattern[1..];
        }
        else if (pattern.StartsWith("\\!", StringComparison.Ordinal))
        {
            pattern = pattern[1..];
        }

        if (pattern.StartsWith("\\#", StringComparison.Ordinal))
        {
            pattern = pattern[1..];
        }

        if (pattern.Length == 0)
        {
            return false;
        }

        bool directoryOnly = pattern.EndsWith('/');
        if (directoryOnly)
        {
            pattern = pattern[..^1];
        }

        bool anchored = pattern.StartsWith('/');
        if (anchored)
        {
            pattern = pattern[1..];
        }

        if (pattern.Length == 0)
        {
            return false;
        }

        bool containsSlash = pattern.Contains('/', StringComparison.Ordinal);
        if (!IgnoreGlobPattern.TryCreate(
            pattern,
            matchFromSegmentBoundary: !anchored && !containsSlash,
            out IgnoreGlobPattern? matcher,
            out failure))
        {
            return false;
        }

        rule = new IgnoreRule(
            basePath,
            negated,
            directoryOnly,
            matcher!);
        return true;
    }

    public bool Matches(string normalizedPath, bool isDirectory)
    {
        if (_directoryOnly && !isDirectory)
        {
            return false;
        }

        string pathWithinBase;
        if (_basePath.Length == 0)
        {
            pathWithinBase = normalizedPath;
        }
        else
        {
            string prefix = _basePath + "/";
            if (!normalizedPath.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            pathWithinBase = normalizedPath[prefix.Length..];
        }

        return _pattern.IsMatch(pathWithinBase);
    }

    public static bool IsIgnored(
        IReadOnlyList<IgnoreRule> rules,
        string normalizedPath,
        bool isDirectory)
    {
        bool ignored = false;
        foreach (IgnoreRule rule in rules)
        {
            if (rule.Matches(normalizedPath, isDirectory))
            {
                ignored = !rule.IsNegated;
            }
        }

        return ignored;
    }
}
