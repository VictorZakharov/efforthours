using System.Globalization;
using System.Text;

namespace EffortHours.Analysis;

internal static class LcovCoverageParser
{
    public static async Task<CoverageReportData?> ParseAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        Dictionary<string, CoverageCounters> sources = new(StringComparer.Ordinal);
        LcovRecord record = new();
        using StreamReader reader = new(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            bufferSize: 16 * 1024,
            leaveOpen: true);
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (line.StartsWith("SF:", StringComparison.Ordinal))
            {
                Flush(record, sources);
                record = new LcovRecord { SourcePath = line[3..].Trim() };
                continue;
            }

            if (line.Equals("end_of_record", StringComparison.Ordinal))
            {
                Flush(record, sources);
                record = new LcovRecord();
                continue;
            }

            ReadMeasurement(line, record);
        }

        Flush(record, sources);
        if (sources.Count == 0)
        {
            return null;
        }

        CoverageSourceResult[] results = [.. sources
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new CoverageSourceResult(pair.Key, pair.Value))];
        CoverageCounters overall = results.Aggregate(
            default(CoverageCounters),
            (current, source) => current.Add(source.Counters));
        return new CoverageReportData("lcov", results, overall);
    }

    private static void ReadMeasurement(string line, LcovRecord record)
    {
        if (TryReadSummary(line, "LF:", out decimal linesTotal))
        {
            record.LinesTotal = linesTotal;
            record.HasLinesSummary = true;
        }
        else if (TryReadSummary(line, "LH:", out decimal linesCovered))
        {
            record.LinesCovered = linesCovered;
        }
        else if (TryReadSummary(line, "BRF:", out decimal branchesTotal))
        {
            record.BranchesTotal = branchesTotal;
            record.HasBranchesSummary = true;
        }
        else if (TryReadSummary(line, "BRH:", out decimal branchesCovered))
        {
            record.BranchesCovered = branchesCovered;
        }
        else if (TryReadSummary(line, "FNF:", out decimal functionsTotal))
        {
            record.FunctionsTotal = functionsTotal;
            record.HasFunctionsSummary = true;
        }
        else if (TryReadSummary(line, "FNH:", out decimal functionsCovered))
        {
            record.FunctionsCovered = functionsCovered;
        }
        else if (line.StartsWith("DA:", StringComparison.Ordinal))
        {
            record.DataLinesTotal++;
            if (ReadDelimitedValue(line.AsSpan(3), 1) > 0m)
            {
                record.DataLinesCovered++;
            }
        }
        else if (line.StartsWith("BRDA:", StringComparison.Ordinal))
        {
            record.DataBranchesTotal++;
            ReadOnlySpan<char> taken = DelimitedValue(line.AsSpan(5), 3);
            if (!taken.SequenceEqual("-".AsSpan()) && ParseNonNegative(taken) > 0m)
            {
                record.DataBranchesCovered++;
            }
        }
        else if (line.StartsWith("FNDA:", StringComparison.Ordinal))
        {
            record.DataFunctionsTotal++;
            if (ReadDelimitedValue(line.AsSpan(5), 0) > 0m)
            {
                record.DataFunctionsCovered++;
            }
        }
    }

    private static bool TryReadSummary(string line, string prefix, out decimal value)
    {
        value = 0m;
        return line.StartsWith(prefix, StringComparison.Ordinal) &&
            decimal.TryParse(
                line.AsSpan(prefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out value) &&
            value >= 0m;
    }

    private static decimal ReadDelimitedValue(ReadOnlySpan<char> value, int index) =>
        ParseNonNegative(DelimitedValue(value, index));

    private static ReadOnlySpan<char> DelimitedValue(ReadOnlySpan<char> value, int index)
    {
        int start = 0;
        for (int current = 0; current < index; current++)
        {
            int separator = value[start..].IndexOf(',');
            if (separator < 0)
            {
                return [];
            }

            start += separator + 1;
        }

        int end = value[start..].IndexOf(',');
        return end < 0 ? value[start..] : value.Slice(start, end);
    }

    private static decimal ParseNonNegative(ReadOnlySpan<char> value) =>
        decimal.TryParse(
            value,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out decimal parsed) &&
            parsed >= 0m
                ? parsed
                : 0m;

    private static void Flush(
        LcovRecord record,
        Dictionary<string, CoverageCounters> sources)
    {
        if (string.IsNullOrWhiteSpace(record.SourcePath))
        {
            return;
        }

        CoverageCounters counters = new(
            record.HasLinesSummary ? record.LinesCovered : record.DataLinesCovered,
            record.HasLinesSummary ? record.LinesTotal : record.DataLinesTotal,
            record.HasBranchesSummary ? record.BranchesCovered : record.DataBranchesCovered,
            record.HasBranchesSummary ? record.BranchesTotal : record.DataBranchesTotal,
            record.HasFunctionsSummary ? record.FunctionsCovered : record.DataFunctionsCovered,
            record.HasFunctionsSummary ? record.FunctionsTotal : record.DataFunctionsTotal);
        if (!counters.HasMeasurements)
        {
            return;
        }

        sources[record.SourcePath] = sources.GetValueOrDefault(record.SourcePath).Add(counters);
    }

    private sealed class LcovRecord
    {
        public string? SourcePath { get; init; }

        public decimal LinesCovered { get; set; }

        public decimal LinesTotal { get; set; }

        public decimal BranchesCovered { get; set; }

        public decimal BranchesTotal { get; set; }

        public decimal FunctionsCovered { get; set; }

        public decimal FunctionsTotal { get; set; }

        public decimal DataLinesCovered { get; set; }

        public decimal DataLinesTotal { get; set; }

        public decimal DataBranchesCovered { get; set; }

        public decimal DataBranchesTotal { get; set; }

        public decimal DataFunctionsCovered { get; set; }

        public decimal DataFunctionsTotal { get; set; }

        public bool HasLinesSummary { get; set; }

        public bool HasBranchesSummary { get; set; }

        public bool HasFunctionsSummary { get; set; }
    }
}
