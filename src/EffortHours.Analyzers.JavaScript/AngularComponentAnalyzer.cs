using System.Text;

namespace EffortHours.Analyzers.JavaScript;

internal static class AngularComponentAnalyzer
{
    private static readonly HashSet<string> SupportedProperties = new(StringComparer.Ordinal)
    {
        "selector", "template", "templateUrl", "styles", "styleUrl", "styleUrls",
    };

    public static IReadOnlyList<AngularComponentMetadata> Analyze(
        string path,
        string packageScope,
        JavaScriptTokenization tokenization)
    {
        HashSet<string> componentDecorators = AngularComponentImportAnalyzer.FindDecoratorNames(tokenization);
        if (componentDecorators.Count == 0)
        {
            return [];
        }

        List<AngularComponentMetadata> components = [];
        for (int index = 0; index + 3 < tokenization.Tokens.Count; index++)
        {
            if (!tokenization.Is(index, "@") ||
                !IsNamedIdentifier(tokenization, index + 1, componentDecorators) ||
                !tokenization.Is(index + 2, "(") ||
                !tokenization.Is(index + 3, "{"))
            {
                continue;
            }

            int objectEnd = FindMatching(tokenization, index + 3, "{", "}");
            if (objectEnd < 0)
            {
                continue;
            }

            components.Add(ReadComponent(
                path,
                packageScope,
                tokenization,
                index,
                index + 3,
                objectEnd));
            index = objectEnd;
        }

        return components;
    }

    private static bool IsNamedIdentifier(
        JavaScriptTokenization tokens,
        int index,
        HashSet<string> names) =>
        index >= 0 &&
        index < tokens.Tokens.Count &&
        tokens.Tokens[index].Kind == JavaScriptTokenKind.Identifier &&
        names.Contains(tokens.Tokens[index].Span(tokens.Source).ToString());

    private static AngularComponentMetadata ReadComponent(
        string path,
        string packageScope,
        JavaScriptTokenization tokens,
        int decoratorIndex,
        int objectStart,
        int objectEnd)
    {
        List<string> inlineTemplates = [];
        List<string> inlineStyles = [];
        List<string> templateReferences = [];
        List<string> styleReferences = [];
        bool hasSelector = false;
        int staticProperties = 0;
        int dynamicProperties = 0;
        int depth = 1;
        for (int index = objectStart + 1; index < objectEnd; index++)
        {
            if (tokens.Is(index, "{"))
            {
                depth++;
                continue;
            }

            if (tokens.Is(index, "}"))
            {
                depth--;
                continue;
            }

            if (depth != 1 || !TryReadPropertyName(tokens, index, out string? property) ||
                !tokens.Is(index + 1, ":") || !SupportedProperties.Contains(property))
            {
                continue;
            }

            if (!TryReadStaticValues(tokens, index + 2, out IReadOnlyList<string>? values, out int valueEnd))
            {
                dynamicProperties++;
                continue;
            }

            staticProperties++;
            switch (property)
            {
                case "selector":
                    hasSelector = values.Count > 0;
                    break;
                case "template":
                    inlineTemplates.AddRange(values);
                    break;
                case "styles":
                    inlineStyles.AddRange(values);
                    break;
                case "templateUrl":
                    templateReferences.AddRange(values);
                    break;
                case "styleUrl":
                case "styleUrls":
                    styleReferences.AddRange(values);
                    break;
            }

            index = valueEnd;
        }

        return new AngularComponentMetadata(
            path,
            packageScope,
            tokens.Tokens[decoratorIndex].Line,
            hasSelector,
            inlineTemplates,
            inlineStyles,
            templateReferences,
            styleReferences,
            staticProperties,
            dynamicProperties);
    }

    private static bool TryReadPropertyName(
        JavaScriptTokenization tokens,
        int index,
        out string property)
    {
        property = string.Empty;
        if (index < 0 || index >= tokens.Tokens.Count)
        {
            return false;
        }

        JavaScriptToken token = tokens.Tokens[index];
        if (token.Kind == JavaScriptTokenKind.Identifier)
        {
            property = token.Span(tokens.Source).ToString();
            return true;
        }

        return token.Kind == JavaScriptTokenKind.String &&
            TryDecodeLiteral(token.Span(tokens.Source), out property);
    }

    private static bool TryReadStaticValues(
        JavaScriptTokenization tokens,
        int start,
        out IReadOnlyList<string> values,
        out int end)
    {
        values = [];
        end = start;
        if (start < 0 || start >= tokens.Tokens.Count)
        {
            return false;
        }

        JavaScriptToken token = tokens.Tokens[start];
        if (token.Kind is JavaScriptTokenKind.String or JavaScriptTokenKind.Template)
        {
            if (!TryDecodeLiteral(token.Span(tokens.Source), out string value))
            {
                return false;
            }

            values = [value];
            return true;
        }

        if (!tokens.Is(start, "["))
        {
            return false;
        }

        int closing = FindMatching(tokens, start, "[", "]");
        if (closing < 0)
        {
            return false;
        }

        List<string> items = [];
        for (int index = start + 1; index < closing; index++)
        {
            if (tokens.Is(index, ","))
            {
                continue;
            }

            JavaScriptToken item = tokens.Tokens[index];
            if (item.Kind is not (JavaScriptTokenKind.String or JavaScriptTokenKind.Template) ||
                !TryDecodeLiteral(item.Span(tokens.Source), out string value))
            {
                return false;
            }

            items.Add(value);
        }

        values = items;
        end = closing;
        return true;
    }

    private static int FindMatching(
        JavaScriptTokenization tokens,
        int start,
        string opening,
        string closing)
    {
        int depth = 0;
        for (int index = start; index < tokens.Tokens.Count; index++)
        {
            if (tokens.Is(index, opening))
            {
                depth++;
            }
            else if (tokens.Is(index, closing) && --depth == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryDecodeLiteral(ReadOnlySpan<char> literal, out string value)
    {
        value = string.Empty;
        if (literal.Length < 2 || literal[0] != literal[^1] || literal[0] is not ('\'' or '"' or '`'))
        {
            return false;
        }

        if (literal[0] == '`' && literal.Contains("${", StringComparison.Ordinal))
        {
            return false;
        }

        StringBuilder result = new(literal.Length - 2);
        for (int index = 1; index < literal.Length - 1; index++)
        {
            char current = literal[index];
            if (current != '\\')
            {
                result.Append(current);
                continue;
            }

            if (++index >= literal.Length - 1)
            {
                return false;
            }

            char escaped = literal[index];
            result.Append(escaped switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                'b' => '\b',
                'f' => '\f',
                'v' => '\v',
                '\'' => '\'',
                '"' => '"',
                '`' => '`',
                '\\' => '\\',
                _ => escaped,
            });
        }

        value = result.ToString();
        return true;
    }
}

internal sealed record AngularComponentMetadata(
    string SourcePath,
    string PackageScope,
    int Line,
    bool HasSelector,
    IReadOnlyList<string> InlineTemplates,
    IReadOnlyList<string> InlineStyles,
    IReadOnlyList<string> TemplateReferences,
    IReadOnlyList<string> StyleReferences,
    int StaticProperties,
    int DynamicProperties);
