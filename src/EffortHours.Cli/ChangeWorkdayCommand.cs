using System.Text;
using System.Text.Json;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Cli;

internal static class ChangeWorkdayCommand
{
    private const string Help = """
        Usage: eh change allocate-days <comparison.json> --workdays <workdays.json>
                 --policy equal-declared-days/1.0.0 [--format json|markdown] [--compact] [--output <new-path>]
        Reads a complete, joint, single-contributor calendar-day comparison and digest-bound
        external workday declarations. Equally allocates each category across declared dates,
        preserving exact EHE and existing reference capacity. Dates remain allocated and
        unresolved as original workdays. Logged hours are context, never allocation weights.
        Offline; no Git/provider calls, estimator rerun, input overwrite, or timesheet mutation.
        EHE remains experimental and uncalibrated. See 'eh docs show workday-allocation'.
        """;

    public static async Task<int> ExecuteAsync(string[] arguments, TextWriter stdout, TextWriter stderr, CancellationToken token)
    {
        if (arguments.Length == 0 || arguments.Any(value => value is "help" or "--help" or "-h"))
        {
            await stdout.WriteLineAsync(Help).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }
        string? workdays = null, policy = null, output = null;
        string format = "json";
        bool compact = false;
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int index = 1; index < arguments.Length; index++)
        {
            string option = arguments[index];
            if (!seen.Add(option)) return await Error("Duplicate allocation option.", CliExitCodes.UsageError);
            if (option == "--compact") { compact = true; continue; }
            if (++index == arguments.Length) return await Error("Allocation option requires a value.", CliExitCodes.UsageError);
            switch (option)
            {
                case "--workdays": workdays = arguments[index]; break;
                case "--policy": policy = arguments[index]; break;
                case "--output": output = arguments[index]; break;
                case "--format": format = arguments[index]; break;
                default: return await Error("Unknown allocation option.", CliExitCodes.UsageError);
            }
        }
        if (workdays is null || policy != ChangeWorkdayPolicies.EqualDeclaredDaysV1 ||
            format is not ("json" or "markdown") || compact && format != "json")
            return await Error("Supply --workdays and explicit --policy equal-declared-days/1.0.0; format must be json or markdown.", CliExitCodes.UsageError);
        try
        {
            if (output is not null && File.Exists(output))
                return await Error("Allocation --output must be a new file; existing inputs and reports are never overwritten.", CliExitCodes.InvalidInput);
            ChangePortfolioComparisonReport source = await LoadAsync<ChangePortfolioComparisonReport>(arguments[0],
                SchemaNames.ChangePortfolioComparisonReport, ChangePortfolioLimits.MaximumRenderedOutputBytes, token);
            ChangeWorkdayManifest manifest = await LoadAsync<ChangeWorkdayManifest>(workdays, SchemaNames.ChangeWorkdayManifest, 1048576, token);
            ChangeWorkdayAllocationReport report = ChangeWorkdayAllocator.Allocate(source, manifest, policy);
            string json = compact ? ContractJson.SerializeCompact(report) : ContractJson.Serialize(report);
            if (!ContractSchemaValidator.Validate(SchemaNames.ChangeWorkdayAllocationReport, json).IsValid)
                throw new InvalidOperationException("Allocation output failed its public schema.");
            string rendered = format == "markdown" ? ChangeWorkdayAllocationMarkdownRenderer.Render(report) : json.ReplaceLineEndings("\n").TrimEnd() + "\n";
            token.ThrowIfCancellationRequested();
            if (output is null) await stdout.WriteAsync(rendered).ConfigureAwait(false);
            else await WriteNewAsync(output, rendered, token).ConfigureAwait(false);
            return CliExitCodes.Success;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or JsonException)
        {
            return await Error("Workday allocation failed: " + exception.Message, CliExitCodes.InvalidInput);
        }

        async Task<int> Error(string message, int exit)
        {
            await stderr.WriteLineAsync("eh: " + message).ConfigureAwait(false);
            return exit;
        }
    }

    internal static async Task<T> LoadAsync<T>(string path, string schemaName, long maximumBytes, CancellationToken token)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > maximumBytes) throw new ArgumentException("Allocation input exceeds its byte bound.");
        using StreamReader reader = new(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: false);
        string json = await reader.ReadToEndAsync(token).ConfigureAwait(false);
        if (json.StartsWith('\uFEFF')) json = json[1..];
        if (!ContractSchemaValidator.Validate(schemaName, json).IsValid)
            throw new ArgumentException("Allocation input failed its public schema.");
        return ContractJson.Deserialize<T>(json);
    }

    internal static async Task WriteNewAsync(string path, string text, CancellationToken token)
    {
        string full = Path.GetFullPath(path);
        string temporary = full + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await File.WriteAllTextAsync(temporary, text, new UTF8Encoding(false), token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, full, overwrite: false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
