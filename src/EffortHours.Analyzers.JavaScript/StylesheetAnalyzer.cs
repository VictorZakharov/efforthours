namespace EffortHours.Analyzers.JavaScript;

internal static class StylesheetAnalyzer
{
    private const int MaximumCount = 2_000;
    private const int MaximumUnits = 40;

    public static StylesheetMetrics Analyze(string source, string language)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        return language == "sass" && !source.Contains('{', StringComparison.Ordinal)
            ? AnalyzeIndentedSass(source)
            : AnalyzeBraced(source);
    }

    private static StylesheetMetrics AnalyzeBraced(string source)
    {
        string text = RemoveComments(source);
        StylesheetMetrics metrics = new();
        int statementStart = 0;
        char quote = '\0';
        for (int index = 0; index < text.Length; index++)
        {
            char current = text[index];
            if (quote != '\0')
            {
                if (current == '\\')
                {
                    index++;
                }
                else if (current == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (current is '\'' or '"')
            {
                quote = current;
                continue;
            }

            if (current == '{')
            {
                ReadOnlySpan<char> header = text.AsSpan(statementStart, index - statementStart).Trim();
                AnalyzeRuleHeader(header, metrics);
                statementStart = index + 1;
            }
            else if (current == ';')
            {
                AnalyzeDeclaration(text.AsSpan(statementStart, index - statementStart), metrics);
                statementStart = index + 1;
            }
            else if (current == '}')
            {
                AnalyzeDeclaration(text.AsSpan(statementStart, index - statementStart), metrics);
                statementStart = index + 1;
            }
        }

        Complete(metrics);
        return metrics;
    }

    private static StylesheetMetrics AnalyzeIndentedSass(string source)
    {
        StylesheetMetrics metrics = new();
        foreach (string rawLine in source.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            ReadOnlySpan<char> line = rawLine.AsSpan().Trim();
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("$", StringComparison.Ordinal) && line.Contains(':'))
            {
                metrics.DesignTokens = Cap(metrics.DesignTokens + 1);
                continue;
            }

            if (StartsWithAtRule(line, "@media") || StartsWithAtRule(line, "@container"))
            {
                metrics.ResponsiveSurfaces = Cap(metrics.ResponsiveSurfaces + 1);
                if (line.Contains("prefers-color-scheme", StringComparison.OrdinalIgnoreCase))
                {
                    metrics.AnimationThemeSurfaces = Cap(metrics.AnimationThemeSurfaces + 1);
                }

                continue;
            }

            if (StartsWithAtRule(line, "@keyframes"))
            {
                metrics.AnimationThemeSurfaces = Cap(metrics.AnimationThemeSurfaces + 1);
                continue;
            }

            int colon = line.IndexOf(':');
            if (colon > 0 && IsPropertyName(line[..colon].Trim()))
            {
                AnalyzeDeclaration(line, metrics);
                continue;
            }

            if (line[0] is not ('@' or '+' or '='))
            {
                metrics.RuleGroups = Cap(metrics.RuleGroups + 1);
                metrics.Selectors = Cap(metrics.Selectors + CountSelectors(line));
                if (IsThemeSelector(line))
                {
                    metrics.AnimationThemeSurfaces = Cap(metrics.AnimationThemeSurfaces + 1);
                }
            }
        }

        Complete(metrics);
        return metrics;
    }

    private static void AnalyzeRuleHeader(ReadOnlySpan<char> header, StylesheetMetrics metrics)
    {
        if (header.Length == 0)
        {
            return;
        }

        if (StartsWithAtRule(header, "@media") || StartsWithAtRule(header, "@container"))
        {
            metrics.ResponsiveSurfaces = Cap(metrics.ResponsiveSurfaces + 1);
            if (header.Contains("prefers-color-scheme", StringComparison.OrdinalIgnoreCase))
            {
                metrics.AnimationThemeSurfaces = Cap(metrics.AnimationThemeSurfaces + 1);
            }

            return;
        }

        if (StartsWithAtRule(header, "@keyframes") || StartsWithAtRule(header, "@-webkit-keyframes"))
        {
            metrics.AnimationThemeSurfaces = Cap(metrics.AnimationThemeSurfaces + 1);
            return;
        }

        if (header[0] == '@')
        {
            return;
        }

        metrics.RuleGroups = Cap(metrics.RuleGroups + 1);
        metrics.Selectors = Cap(metrics.Selectors + CountSelectors(header));
        if (IsThemeSelector(header))
        {
            metrics.AnimationThemeSurfaces = Cap(metrics.AnimationThemeSurfaces + 1);
        }
    }

    private static void AnalyzeDeclaration(ReadOnlySpan<char> statement, StylesheetMetrics metrics)
    {
        statement = statement.Trim();
        int colon = statement.IndexOf(':');
        if (colon <= 0)
        {
            return;
        }

        ReadOnlySpan<char> name = statement[..colon].Trim();
        if (name.StartsWith("--", StringComparison.Ordinal) ||
            name.StartsWith("$", StringComparison.Ordinal) ||
            (name.StartsWith("@", StringComparison.Ordinal) && !name.Contains(' ')))
        {
            metrics.DesignTokens = Cap(metrics.DesignTokens + 1);
        }

        if (name.Equals("animation", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("animation-name", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("transition", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("transition-property", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("color-scheme", StringComparison.OrdinalIgnoreCase))
        {
            metrics.AnimationThemeSurfaces = Cap(metrics.AnimationThemeSurfaces + 1);
        }
    }

    private static void Complete(StylesheetMetrics metrics)
    {
        metrics.StyleStructureUnits = Units(metrics.RuleGroups + metrics.Selectors, 5);
        metrics.DesignTokenUnits = Units(metrics.DesignTokens, 4);
    }

    private static int CountSelectors(ReadOnlySpan<char> header)
    {
        int selectors = 1;
        int squareDepth = 0;
        int parenthesisDepth = 0;
        char quote = '\0';
        foreach (char current in header)
        {
            if (quote != '\0')
            {
                if (current == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (current is '\'' or '"')
            {
                quote = current;
            }
            else if (current == '[')
            {
                squareDepth++;
            }
            else if (current == ']')
            {
                squareDepth = Math.Max(0, squareDepth - 1);
            }
            else if (current == '(')
            {
                parenthesisDepth++;
            }
            else if (current == ')')
            {
                parenthesisDepth = Math.Max(0, parenthesisDepth - 1);
            }
            else if (current == ',' && squareDepth == 0 && parenthesisDepth == 0)
            {
                selectors++;
            }
        }

        return Math.Min(MaximumCount, selectors);
    }

    private static bool IsThemeSelector(ReadOnlySpan<char> header) =>
        header.Contains("[data-theme", StringComparison.OrdinalIgnoreCase) ||
        header.Contains(".theme-", StringComparison.OrdinalIgnoreCase) ||
        header.Contains(":root", StringComparison.OrdinalIgnoreCase) ||
        header.Contains(".dark", StringComparison.OrdinalIgnoreCase) ||
        header.Contains(".light", StringComparison.OrdinalIgnoreCase);

    private static bool StartsWithAtRule(ReadOnlySpan<char> source, string value) =>
        source.StartsWith(value, StringComparison.OrdinalIgnoreCase) &&
        (source.Length == value.Length || char.IsWhiteSpace(source[value.Length]) || source[value.Length] == '(');

    private static bool IsPropertyName(ReadOnlySpan<char> value)
    {
        if (value.Length == 0 || value[0] is '.' or '#' or '&' or '[' or ':')
        {
            return false;
        }

        return value.ToString().All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
    }

    private static string RemoveComments(string source)
    {
        char[] result = source.ToCharArray();
        for (int index = 0; index + 1 < result.Length; index++)
        {
            if (result[index] != '/' || result[index + 1] != '*')
            {
                continue;
            }

            result[index++] = ' ';
            result[index++] = ' ';
            while (index < result.Length)
            {
                if (index + 1 < result.Length && result[index] == '*' && result[index + 1] == '/')
                {
                    result[index] = ' ';
                    result[index + 1] = ' ';
                    break;
                }

                if (result[index] is not ('\r' or '\n'))
                {
                    result[index] = ' ';
                }

                index++;
            }
        }

        return new string(result);
    }

    private static int Units(int count, int divisor) =>
        Math.Min(MaximumUnits, (count + divisor - 1) / divisor);

    private static int Cap(int value) => Math.Min(MaximumCount, value);
}

internal sealed class StylesheetMetrics
{
    public int RuleGroups { get; set; }

    public int Selectors { get; set; }

    public int ResponsiveSurfaces { get; set; }

    public int DesignTokens { get; set; }

    public int AnimationThemeSurfaces { get; set; }

    public int StyleStructureUnits { get; set; }

    public int DesignTokenUnits { get; set; }

    public void Merge(StylesheetMetrics other)
    {
        RuleGroups += other.RuleGroups;
        Selectors += other.Selectors;
        ResponsiveSurfaces += other.ResponsiveSurfaces;
        DesignTokens += other.DesignTokens;
        AnimationThemeSurfaces += other.AnimationThemeSurfaces;
        StyleStructureUnits += other.StyleStructureUnits;
        DesignTokenUnits += other.DesignTokenUnits;
    }
}
