namespace EffortHours.Analyzers.JavaScript;

internal static class FrontendMarkupAnalyzer
{
    private const int MaximumCount = 2_000;
    private const int MaximumUnits = 40;

    public static FrontendMarkupMetrics Analyze(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        FrontendMarkupMetrics metrics = new();
        int index = 0;
        while (index < source.Length)
        {
            int opening = source.IndexOf('<', index);
            if (opening < 0)
            {
                AnalyzeText(source.AsSpan(index), metrics);
                break;
            }

            AnalyzeText(source.AsSpan(index, opening - index), metrics);
            if (source.AsSpan(opening).StartsWith("<!--", StringComparison.Ordinal))
            {
                int commentEnd = source.IndexOf("-->", opening + 4, StringComparison.Ordinal);
                index = commentEnd < 0 ? source.Length : commentEnd + 3;
                continue;
            }

            int tagEnd = FindTagEnd(source, opening + 1);
            if (tagEnd < 0)
            {
                break;
            }

            ReadOnlySpan<char> body = source.AsSpan(opening + 1, tagEnd - opening - 1).Trim();
            if (body.Length == 0 || body[0] is '/' or '!' or '?')
            {
                index = tagEnd + 1;
                continue;
            }

            int nameLength = 0;
            while (nameLength < body.Length && IsTagNameCharacter(body[nameLength]))
            {
                nameLength++;
            }

            if (nameLength == 0)
            {
                index = tagEnd + 1;
                continue;
            }

            string tagName = body[..nameLength].ToString().ToLowerInvariant();
            metrics.Elements = Cap(metrics.Elements + 1);
            if (IsStructuralElement(tagName))
            {
                metrics.StructuralElements = Cap(metrics.StructuralElements + 1);
            }

            if (tagName.Contains('-', StringComparison.Ordinal))
            {
                metrics.CustomElements = Cap(metrics.CustomElements + 1);
            }

            if (tagName == "form")
            {
                metrics.Forms = Cap(metrics.Forms + 1);
            }
            else if (tagName is "input" or "select" or "textarea" or "button" or "option")
            {
                metrics.FormControls = Cap(metrics.FormControls + 1);
            }

            AnalyzeAttributes(body[nameLength..], metrics);
            index = tagEnd + 1;
            if (tagName is "script" or "style" && !body.EndsWith("/", StringComparison.Ordinal))
            {
                int closing = source.IndexOf($"</{tagName}", index, StringComparison.OrdinalIgnoreCase);
                if (closing < 0)
                {
                    break;
                }

                index = closing;
            }
        }

        metrics.TemplateStructureUnits = Units(
            metrics.Elements + metrics.StructuralElements + metrics.CustomElements,
            5);
        metrics.TemplateBindingUnits = Units(
            metrics.Bindings + metrics.Directives + metrics.FormControls,
            4);
        return metrics;
    }

    private static void AnalyzeText(ReadOnlySpan<char> text, FrontendMarkupMetrics metrics)
    {
        metrics.Bindings = Cap(metrics.Bindings + Count(text, "{{"));
        metrics.Directives = Cap(metrics.Directives +
            Count(text, "@if") + Count(text, "@for") + Count(text, "@switch") +
            Count(text, "@defer") + Count(text, "{#if") + Count(text, "{#each"));
    }

    private static void AnalyzeAttributes(ReadOnlySpan<char> attributes, FrontendMarkupMetrics metrics)
    {
        int index = 0;
        while (index < attributes.Length)
        {
            while (index < attributes.Length && (char.IsWhiteSpace(attributes[index]) || attributes[index] == '/'))
            {
                index++;
            }

            int start = index;
            while (index < attributes.Length &&
                !char.IsWhiteSpace(attributes[index]) && attributes[index] is not ('=' or '>' or '/'))
            {
                index++;
            }

            if (start == index)
            {
                index++;
                continue;
            }

            string name = attributes[start..index].ToString();
            bool directive = name.StartsWith('*') ||
                name.StartsWith("v-", StringComparison.OrdinalIgnoreCase) ||
                ContainsAngularDirective(name);
            bool binding = name.StartsWith('[') || name.StartsWith('(') ||
                name.StartsWith('#') || name.StartsWith(':') || name.StartsWith('@') ||
                name.StartsWith("bind:", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("ngModel", StringComparison.OrdinalIgnoreCase);
            if (directive)
            {
                metrics.Directives = Cap(metrics.Directives + 1);
            }

            if (binding || directive)
            {
                metrics.Bindings = Cap(metrics.Bindings + 1);
            }

            while (index < attributes.Length && char.IsWhiteSpace(attributes[index]))
            {
                index++;
            }

            if (index >= attributes.Length || attributes[index] != '=')
            {
                continue;
            }

            index++;
            while (index < attributes.Length && char.IsWhiteSpace(attributes[index]))
            {
                index++;
            }

            int valueStart = index;
            if (index < attributes.Length && attributes[index] is '\'' or '"')
            {
                char quote = attributes[index++];
                valueStart = index;
                while (index < attributes.Length && attributes[index] != quote)
                {
                    index++;
                }

                metrics.Bindings = Cap(metrics.Bindings + Count(attributes[valueStart..index], "{{"));
                index = Math.Min(attributes.Length, index + 1);
            }
            else
            {
                while (index < attributes.Length && !char.IsWhiteSpace(attributes[index]))
                {
                    index++;
                }

                metrics.Bindings = Cap(metrics.Bindings + Count(attributes[valueStart..index], "{{"));
            }
        }
    }

    private static int FindTagEnd(string source, int start)
    {
        char quote = '\0';
        for (int index = start; index < source.Length; index++)
        {
            char current = source[index];
            if (quote != '\0')
            {
                if (current == quote && (index == 0 || source[index - 1] != '\\'))
                {
                    quote = '\0';
                }

                continue;
            }

            if (current is '\'' or '"')
            {
                quote = current;
            }
            else if (current == '>')
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsTagNameCharacter(char value) =>
        char.IsAsciiLetterOrDigit(value) || value is '-' or ':' or '_';

    private static bool IsStructuralElement(string name) => name is
        "article" or "aside" or "canvas" or "details" or "dialog" or "fieldset" or
        "footer" or "form" or "header" or "main" or "nav" or "ol" or "section" or
        "table" or "ul";

    private static bool ContainsAngularDirective(string name) =>
        name.Contains("ngIf", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("ngFor", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("ngSwitch", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("ngModel", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("formGroup", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("formControl", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("routerLink", StringComparison.OrdinalIgnoreCase);

    private static int Count(ReadOnlySpan<char> source, string value)
    {
        int count = 0;
        int index = 0;
        while (index <= source.Length - value.Length && count < MaximumCount)
        {
            int found = source[index..].IndexOf(value, StringComparison.Ordinal);
            if (found < 0)
            {
                break;
            }

            count++;
            index += found + value.Length;
        }

        return count;
    }

    private static int Units(int count, int divisor) =>
        Math.Min(MaximumUnits, (count + divisor - 1) / divisor);

    private static int Cap(int value) => Math.Min(MaximumCount, value);
}

internal sealed class FrontendMarkupMetrics
{
    public int Elements { get; set; }

    public int StructuralElements { get; set; }

    public int Forms { get; set; }

    public int FormControls { get; set; }

    public int Bindings { get; set; }

    public int Directives { get; set; }

    public int CustomElements { get; set; }

    public int TemplateStructureUnits { get; set; }

    public int TemplateBindingUnits { get; set; }

    public void Merge(FrontendMarkupMetrics other)
    {
        Elements += other.Elements;
        StructuralElements += other.StructuralElements;
        Forms += other.Forms;
        FormControls += other.FormControls;
        Bindings += other.Bindings;
        Directives += other.Directives;
        CustomElements += other.CustomElements;
        TemplateStructureUnits += other.TemplateStructureUnits;
        TemplateBindingUnits += other.TemplateBindingUnits;
    }
}
