using System.Text.Json;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal static class ChangeHistoricalRefreshCommand
{
    private const string Help = """
        Usage: eh change plan-refresh <comparison.json> --work-records <records.json>
                 --entries <refresh-manifest.json> [--fields notes|ehe|both]
                 [--entry-policy equal-matched-entries/1.0.0] [--compact] [--output <new-path>]
        Produces a private, reviewable JSON dry-run plan over explicitly selected entries.
        Preserves original snapshots and descriptions; replaces one managed annotation.
        Note and EHE permissions are independent; locked/invoiced/unknown states block edits.
        Unresolved evidence never produces zero-labor claims or fabricated EHE contributions.
        Offline, no Git/provider access or entry writes. Applying any plan needs separate
        confirmation of the exact range/entries and fresh permission/revision checks.
        See docs/HISTORICAL_NOTE_REFRESH.md. EHE remains experimental and uncalibrated.
        """;

    public static async Task<int> ExecuteAsync(string[] arguments, TextWriter stdout, TextWriter stderr, CancellationToken token)
    {
        if (arguments.Length == 0 || arguments.Any(value => value is "help" or "--help" or "-h"))
        { await stdout.WriteLineAsync(Help).ConfigureAwait(false); return arguments.Length == 0 ? CliExitCodes.UsageError : 0; }
        string? records = null, entries = null, output = null, entryPolicy = null;
        string fields = "notes";
        bool compact = false;
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int index = 1; index < arguments.Length; index++)
        {
            string option = arguments[index];
            if (!seen.Add(option)) return await Error("Duplicate refresh option.", CliExitCodes.UsageError);
            if (option == "--compact") { compact = true; continue; }
            if (++index == arguments.Length) return await Error("Refresh option requires a value.", CliExitCodes.UsageError);
            switch (option)
            {
                case "--work-records": records = arguments[index]; break;
                case "--entries": entries = arguments[index]; break;
                case "--fields": fields = arguments[index]; break;
                case "--entry-policy": entryPolicy = arguments[index]; break;
                case "--output": output = arguments[index]; break;
                default: return await Error("Unknown refresh option.", CliExitCodes.UsageError);
            }
        }
        if (records is null || entries is null || fields is not ("notes" or "ehe" or "both") ||
            entryPolicy is not null && entryPolicy != ChangeWorkdayReviewPolicies.EqualEntries)
            return await Error("Supply --work-records and --entries, explicit supported fields and optional entry policy.", CliExitCodes.UsageError);
        try
        {
            if (output is not null && File.Exists(output)) throw new ArgumentException("Refresh output must be a new file.");
            ChangePortfolioComparisonReport source = await ChangeWorkdayCommand.LoadAsync<ChangePortfolioComparisonReport>(arguments[0],
                SchemaNames.ChangePortfolioComparisonReport, ChangePortfolioLimits.MaximumRenderedOutputBytes, token);
            ChangeWorkRecordManifest work = await ChangeWorkdayCommand.LoadAsync<ChangeWorkRecordManifest>(records, SchemaNames.ChangeWorkRecordManifest, 1048576, token);
            ChangeHistoricalRefreshManifest input = await ChangeWorkdayCommand.LoadAsync<ChangeHistoricalRefreshManifest>(entries, SchemaNames.ChangeHistoricalRefreshManifest, 1048576, token);
            ChangeWorkdayReviewReport review = ChangeWorkdayReviewer.Review(source, work, entryPolicy);
            ChangeHistoricalRefreshPlan plan = ChangeHistoricalRefreshPlanner.Plan(review, input, fields);
            string json = compact ? ContractJson.SerializeCompact(plan) : ContractJson.Serialize(plan);
            if (!ContractSchemaValidator.Validate(SchemaNames.ChangeHistoricalRefreshPlan, json).IsValid)
                throw new InvalidOperationException("Refresh plan failed its public schema.");
            token.ThrowIfCancellationRequested();
            string rendered = json.ReplaceLineEndings("\n").TrimEnd() + "\n";
            if (output is null) await stdout.WriteAsync(rendered).ConfigureAwait(false);
            else await ChangeWorkdayCommand.WriteNewAsync(output, rendered, token).ConfigureAwait(false);
            return CliExitCodes.Success;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or JsonException)
        { return await Error("Historical refresh planning failed; verify complete digest-bound inputs and output permissions.", CliExitCodes.InvalidInput); }

        async Task<int> Error(string message, int exit)
        { await stderr.WriteLineAsync("eh: " + message).ConfigureAwait(false); return exit; }
    }
}
