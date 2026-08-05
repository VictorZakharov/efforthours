using System.Text;
using System.Text.RegularExpressions;

namespace Fairbill.Analysis;

internal sealed class IgnoreRule
{
    private readonly string _basePath;
    private readonly bool _directoryOnly;
    private readonly Regex _regex;

    private IgnoreRule(string basePath, bool negated, bool directoryOnly, Regex regex)
    {
        _basePath = basePath;
        IsNegated = negated;
        _directoryOnly = directoryOnly;
        _regex = regex;
    }

    public bool IsNegated { get; }

    public static bool TryCreate(string line, string basePath, out IgnoreRule? rule)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(basePath);

        rule = null;
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
        string globRegex = ConvertGlobToRegex(pattern);
        string expression = anchored || containsSlash
            ? $"^{globRegex}$"
            : $"(?:^|/){globRegex}$";
        Regex regex;
        try
        {
            regex = new Regex(expression, RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
        }
        catch (ArgumentException)
        {
            return false;
        }

        rule = new IgnoreRule(
            basePath,
            negated,
            directoryOnly,
            regex);
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

        return _regex.IsMatch(pathWithinBase);
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

    private static string ConvertGlobToRegex(string pattern)
    {
        StringBuilder builder = new();
        for (int index = 0; index < pattern.Length; index++)
        {
            char character = pattern[index];
            switch (character)
            {
                case '*':
                    if (index + 1 < pattern.Length && pattern[index + 1] == '*')
                    {
                        index++;
                        if (index + 1 < pattern.Length && pattern[index + 1] == '/')
                        {
                            index++;
                            builder.Append("(?:.*/)?");
                        }
                        else
                        {
                            builder.Append(".*");
                        }
                    }
                    else
                    {
                        builder.Append("[^/]*");
                    }

                    break;

                case '?':
                    builder.Append("[^/]");
                    break;

                case '[':
                    int closingBracket = pattern.IndexOf(']', index + 1);
                    if (closingBracket <= index + 1)
                    {
                        builder.Append("\\[");
                        break;
                    }

                    string characterClass = pattern[(index + 1)..closingBracket];
                    bool negatedClass = characterClass[0] is '!' or '^';
                    if (negatedClass)
                    {
                        characterClass = characterClass[1..];
                    }

                    if (characterClass.Length == 0)
                    {
                        builder.Append(Regex.Escape(pattern[index..(closingBracket + 1)]));
                        index = closingBracket;
                        break;
                    }

                    builder.Append('[');
                    if (negatedClass)
                    {
                        builder.Append('^');
                    }

                    foreach (char classCharacter in characterClass)
                    {
                        if (classCharacter is '\\' or ']' or '^')
                        {
                            builder.Append('\\');
                        }

                        builder.Append(classCharacter);
                    }

                    builder.Append(']');
                    index = closingBracket;
                    break;

                case '\\' when index + 1 < pattern.Length:
                    index++;
                    builder.Append(Regex.Escape(pattern[index].ToString()));
                    break;

                default:
                    builder.Append(Regex.Escape(character.ToString()));
                    break;
            }
        }

        return builder.ToString();
    }
}
