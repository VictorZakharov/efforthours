using System.Globalization;

namespace EffortHours.ScannerBenchmarks;

internal static class SupplementalBenchmarkFixtures
{
    public static (string FileName, string Content) Html(int index, int lines)
    {
        string content = lines == 1
            ? $"<!doctype html><!-- benchmark-{index:D7} -->\n"
            : $"<!doctype html><!-- benchmark-{index:D7} -->\n" + string.Concat(
                Enumerable.Range(1, lines - 1).Select(line =>
                    $"<section id=\"panel-{line:D4}\"><label for=\"value-{line:D4}\">Value</label>" +
                    $"<input id=\"value-{line:D4}\" required></section>\n"));
        return ($"page{index:D7}.html", content);
    }

    public static (string FileName, string Content) Css(int index, int lines) =>
        ($"styles{index:D7}.css", StyleContent(index, lines, "$", useVariable: false));

    public static (string FileName, string Content) Scss(int index, int lines) =>
        ($"styles{index:D7}.scss", StyleContent(index, lines, "$", useVariable: true));

    public static (string FileName, string Content) Sql(int index, int lines)
    {
        if (lines == 1)
        {
            return ($"migration{index:D7}.sql", $"CREATE TABLE benchmark_{index:D7} (id INTEGER PRIMARY KEY);\n");
        }

        string columns = string.Concat(
            Enumerable.Range(1, Math.Max(0, lines - 2)).Select(line =>
                $"    value_{line:D4} INTEGER,\n"));
        string content = $"CREATE TABLE benchmark_{index:D7} (\n" + columns +
            "    id INTEGER PRIMARY KEY);\n";
        return ($"migration{index:D7}.sql", content);
    }

    private static string StyleContent(int index, int lines, string variablePrefix, bool useVariable)
    {
        if (useVariable && lines == 1)
        {
            return $"{variablePrefix}spacing: 1rem;\n";
        }

        int ruleLines = useVariable ? lines - 1 : lines;
        string variable = useVariable ? $"{variablePrefix}spacing: 1rem;\n" : string.Empty;
        return variable + string.Concat(
            Enumerable.Range(1, ruleLines).Select(line =>
                $".component-{index:D7}-{line:D4} {{ display: grid; gap: " +
                (useVariable ? $"{variablePrefix}spacing" : "1rem") +
                $"; color: var(--color-{line.ToString(CultureInfo.InvariantCulture)}); }}\n"));
    }
}
