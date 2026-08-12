namespace EffortHours.Analyzers.Php;

internal static class PhpTemplateAnalyzer
{
    private static readonly string[] BladeControlDirectives =
        ["@if", "@elseif", "@foreach", "@forelse", "@for", "@while", "@switch"];

    public static PhpTemplateMetrics Analyze(string source, string path)
    {
        string lowerPath = path.ToLowerInvariant();
        bool blade = lowerPath.EndsWith(".blade.php", StringComparison.Ordinal);
        int tags = CountTags(source);
        if (!blade && tags == 0) return new PhpTemplateMetrics(0, 0, 0, 0, 0);

        int forms = Count(source, "<form", StringComparison.OrdinalIgnoreCase);
        int bindings = Count(source, "{{", StringComparison.Ordinal) +
            Count(source, "{!!", StringComparison.Ordinal) +
            Count(source, "<?=", StringComparison.Ordinal);
        int control = blade
            ? BladeControlDirectives.Sum(value => Count(source, value, StringComparison.OrdinalIgnoreCase))
            : 0;
        int explicitComponents = Count(source, "<x-", StringComparison.OrdinalIgnoreCase) +
            Count(source, "@component", StringComparison.OrdinalIgnoreCase);
        return new PhpTemplateMetrics(
            Math.Clamp(Math.Max(1, explicitComponents), 0, 50),
            Math.Clamp(forms, 0, 50),
            BoundedUnits(tags),
            BoundedUnits(bindings),
            BoundedUnits(control));
    }

    private static int CountTags(string source)
    {
        int count = 0;
        for (int index = 0; index + 1 < source.Length; index++)
        {
            if (source[index] != '<' || source[index + 1] is '/' or '!' or '?' ||
                !char.IsAsciiLetter(source[index + 1])) continue;
            count++;
            if (count >= 500) break;
        }
        return count;
    }

    private static int Count(string source, string value, StringComparison comparison)
    {
        int count = 0;
        int index = 0;
        while (count < 500 && (index = source.IndexOf(value, index, comparison)) >= 0)
        {
            count++;
            index += value.Length;
        }
        return count;
    }

    private static int BoundedUnits(int count) => count switch
    {
        <= 0 => 0,
        <= 4 => count,
        <= 16 => 4 + (int)Math.Ceiling((count - 4) / 2m),
        _ => Math.Min(24, 10 + (int)Math.Ceiling((count - 16) / 8m)),
    };
}
