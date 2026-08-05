namespace Fairbill.Analyzers.JavaScript;

internal static class WorkspacePatternMatcher
{
    private const int MaximumSegments = 128;

    public static bool Includes(IReadOnlyList<string> patterns, string relativePath)
    {
        bool included = false;
        foreach (string rawPattern in patterns)
        {
            string pattern = rawPattern.Trim();
            bool excluded = pattern.StartsWith('!');
            if (excluded)
            {
                pattern = pattern[1..];
            }

            if (Matches(pattern, relativePath))
            {
                included = !excluded;
            }
        }

        return included;
    }

    public static bool Matches(string rawPattern, string rawPath)
    {
        string pattern = Normalize(rawPattern);
        string path = Normalize(rawPath);
        string[] patternSegments = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string[] pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (patternSegments.Length > MaximumSegments || pathSegments.Length > MaximumSegments)
        {
            return false;
        }

        bool[,] matches = new bool[patternSegments.Length + 1, pathSegments.Length + 1];
        matches[0, 0] = true;
        for (int patternIndex = 0; patternIndex < patternSegments.Length; patternIndex++)
        {
            string patternSegment = patternSegments[patternIndex];
            for (int pathIndex = 0; pathIndex <= pathSegments.Length; pathIndex++)
            {
                if (!matches[patternIndex, pathIndex])
                {
                    continue;
                }

                if (patternSegment == "**")
                {
                    matches[patternIndex + 1, pathIndex] = true;
                    if (pathIndex < pathSegments.Length)
                    {
                        matches[patternIndex, pathIndex + 1] = true;
                    }
                }
                else if (pathIndex < pathSegments.Length &&
                    MatchesSegment(patternSegment, pathSegments[pathIndex]))
                {
                    matches[patternIndex + 1, pathIndex + 1] = true;
                }
            }
        }

        return matches[patternSegments.Length, pathSegments.Length];
    }

    private static bool MatchesSegment(string pattern, string value)
    {
        int openBrace = pattern.IndexOf('{');
        int closeBrace = openBrace < 0 ? -1 : pattern.IndexOf('}', openBrace + 1);
        if (openBrace >= 0 && closeBrace > openBrace)
        {
            string prefix = pattern[..openBrace];
            string suffix = pattern[(closeBrace + 1)..];
            string[] alternatives = pattern[(openBrace + 1)..closeBrace].Split(',', 16);
            return alternatives.Any(alternative =>
                MatchesWildcard(prefix + alternative + suffix, value));
        }

        return MatchesWildcard(pattern, value);
    }

    private static bool MatchesWildcard(string pattern, string value)
    {
        int patternIndex = 0;
        int valueIndex = 0;
        int starIndex = -1;
        int starValueIndex = -1;
        while (valueIndex < value.Length)
        {
            if (patternIndex < pattern.Length &&
                (pattern[patternIndex] == '?' ||
                 char.ToUpperInvariant(pattern[patternIndex]) == char.ToUpperInvariant(value[valueIndex])))
            {
                patternIndex++;
                valueIndex++;
            }
            else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                starIndex = patternIndex++;
                starValueIndex = valueIndex;
            }
            else if (starIndex >= 0)
            {
                patternIndex = starIndex + 1;
                valueIndex = ++starValueIndex;
            }
            else
            {
                return false;
            }
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }

    private static string Normalize(string value)
    {
        string normalized = value.Replace('\\', '/').Trim();
        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        return normalized.Trim('/');
    }
}
