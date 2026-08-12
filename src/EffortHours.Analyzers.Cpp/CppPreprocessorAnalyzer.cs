namespace EffortHours.Analyzers.Cpp;

internal static class CppPreprocessorAnalyzer
{
    public const int MaximumConditionalDepth = 128;

    public static CppPreprocessorAnalysis Analyze(IReadOnlyList<CppToken> tokens)
    {
        List<CppInclude> includes = [];
        int directives = 0;
        int macros = 0;
        int functions = 0;
        int variadic = 0;
        int tokenOperators = 0;
        int groups = 0;
        int branches = 0;
        int depth = 0;
        int maximumDepth = 0;
        int features = 0;
        int diagnostics = 0;
        int embeds = 0;
        bool pragmaOnce = false;
        bool includeGuard = false;
        bool malformed = false;
        string? firstGuard = null;

        foreach (CppToken token in tokens.Where(token => token.Kind == CppTokenKind.Preprocessor))
        {
            directives++;
            Directive directive = Parse(token.Text);
            switch (directive.Name)
            {
                case "include":
                case "include_next":
                    CppInclude? include = ParseInclude(directive.Body, token.Line);
                    if (include is not null) includes.Add(include);
                    else malformed = true;
                    break;
                case "define":
                    macros++;
                    string name = FirstIdentifier(directive.Body);
                    int nameIndex = directive.Body.IndexOf(name, StringComparison.Ordinal);
                    int afterName = nameIndex < 0 ? -1 : nameIndex + name.Length;
                    bool functionLike = afterName >= 0 && afterName < directive.Body.Length &&
                        directive.Body[afterName] == '(';
                    if (functionLike) functions++;
                    if (directive.Body.Contains("...", StringComparison.Ordinal)) variadic++;
                    if (directive.Body.Contains("##", StringComparison.Ordinal) ||
                        directive.Body.AsSpan().Contains('#')) tokenOperators++;
                    if (depth == 1 && firstGuard is not null && name == firstGuard) includeGuard = true;
                    break;
                case "if":
                case "ifdef":
                case "ifndef":
                    groups++;
                    branches++;
                    depth++;
                    maximumDepth = Math.Max(maximumDepth, depth);
                    if (maximumDepth > MaximumConditionalDepth) malformed = true;
                    if (directives == 1 && directive.Name == "ifndef")
                        firstGuard = FirstIdentifier(directive.Body);
                    if (IsFeatureTest(directive.Body)) features++;
                    break;
                case "elif":
                case "elifdef":
                case "elifndef":
                case "else":
                    branches++;
                    if (depth == 0) malformed = true;
                    if (IsFeatureTest(directive.Body)) features++;
                    break;
                case "endif":
                    if (depth == 0) malformed = true;
                    else depth--;
                    break;
                case "pragma":
                    pragmaOnce |= directive.Body.Trim().Equals("once", StringComparison.Ordinal);
                    break;
                case "error":
                case "warning":
                    diagnostics++;
                    break;
                case "embed":
                    embeds++;
                    break;
            }
        }

        malformed |= depth != 0;
        return new CppPreprocessorAnalysis
        {
            Includes = [.. includes.Distinct().OrderBy(include => include.Line)],
            Directives = directives,
            MacroDefinitions = macros,
            FunctionLikeMacros = functions,
            VariadicMacros = variadic,
            StringifyOrPasteMacros = tokenOperators,
            ConditionalGroups = groups,
            ConditionalBranches = branches,
            MaximumDepth = maximumDepth,
            FeatureTests = features,
            Diagnostics = diagnostics,
            Embeds = embeds,
            IncludeGuard = includeGuard,
            PragmaOnce = pragmaOnce,
            Malformed = malformed,
        };
    }

    private static Directive Parse(string text)
    {
        ReadOnlySpan<char> span = text.AsSpan().TrimStart();
        if (span.Length == 0 || span[0] != '#') return new Directive(string.Empty, string.Empty);
        span = span[1..].TrimStart();
        int end = 0;
        while (end < span.Length && (char.IsAsciiLetter(span[end]) || span[end] == '_')) end++;
        return new Directive(span[..end].ToString(), span[end..].TrimStart().ToString());
    }

    private static CppInclude? ParseInclude(string body, int line)
    {
        ReadOnlySpan<char> value = body.AsSpan().TrimStart();
        if (value.Length < 3) return null;
        char opening = value[0];
        char closing = opening == '"' ? '"' : opening == '<' ? '>' : '\0';
        if (closing == '\0') return null;
        int end = value[1..].IndexOf(closing);
        if (end < 0) return null;
        string target = value.Slice(1, end).ToString().Replace('\\', '/');
        if (target.Length == 0 || target.Contains('\0')) return null;
        return new CppInclude(target, opening == '"', line);
    }

    private static string FirstIdentifier(string value)
    {
        ReadOnlySpan<char> span = value.AsSpan().TrimStart();
        int length = 0;
        while (length < span.Length && (char.IsAsciiLetterOrDigit(span[length]) || span[length] == '_'))
            length++;
        return span[..length].ToString();
    }

    private static bool IsFeatureTest(string value) =>
        value.Contains("__has_", StringComparison.Ordinal) ||
        value.Contains("defined", StringComparison.Ordinal) ||
        value.Contains("__cplusplus", StringComparison.Ordinal) ||
        value.Contains("__STDC_VERSION__", StringComparison.Ordinal);

    private readonly record struct Directive(string Name, string Body);
}
