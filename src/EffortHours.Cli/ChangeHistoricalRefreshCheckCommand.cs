using System.Text.Json;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal static class ChangeHistoricalRefreshCheckCommand
{
    private const string Help = """
        Usage: eh change check-refresh <plan.json> --entries <current-refresh-manifest.json>
                 [--compact] [--output <new-path>]
        Offline dry-run preflight: re-read the same exact selected entry snapshots and
        independent permissions. Detect concurrent edits, missing IDs, extra selections,
        already-current notes and locks/invoices. Exit 3 with a blocked receipt when needed.
        Exit 1 for invalid inputs or expected operational failure (no valid receipt); usage
        errors exit 2 and cancellation exits 130. Preserve the native exit in wrappers.
        Never writes time entries. A successful check still requires separate user
        confirmation and an external atomic compare-and-set using the checked snapshot.
        See 'eh docs show historical-refresh-integration'.
        """;

    public static async Task<int> ExecuteAsync(string[] arguments, TextWriter stdout, TextWriter stderr, CancellationToken token)
    {
        if (arguments.Length == 0 || arguments.Any(value => value is "help" or "--help" or "-h"))
        {
            await stdout.WriteLineAsync(Help).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }
        string? entries = null, output = null;
        bool compact = false;
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int index = 1; index < arguments.Length; index++)
        {
            string option = arguments[index];
            if (!seen.Add(option)) return await Error("Duplicate preflight option.", CliExitCodes.UsageError);
            if (option == "--compact") { compact = true; continue; }
            if (++index == arguments.Length) return await Error("Preflight option requires a value.", CliExitCodes.UsageError);
            switch (option)
            {
                case "--entries": entries = arguments[index]; break;
                case "--output": output = arguments[index]; break;
                default: return await Error("Unknown preflight option.", CliExitCodes.UsageError);
            }
        }
        if (entries is null) return await Error("Supply --entries with freshly observed selected snapshots.", CliExitCodes.UsageError);
        try
        {
            if (output is not null && File.Exists(output)) throw new ArgumentException("Preflight output must be a new file.");
            var plan = await ChangeWorkdayCommand.LoadAsync<ChangeHistoricalRefreshPlan>(arguments[0],
                SchemaNames.ChangeHistoricalRefreshPlan, 16777216, token);
            var current = await ChangeWorkdayCommand.LoadAsync<ChangeHistoricalRefreshManifest>(entries,
                SchemaNames.ChangeHistoricalRefreshCurrent, 1048576, token);
            var check = ChangeHistoricalRefreshPreflight.Check(plan, current);
            string json = ContractJson.SerializeDocument(check, compact);
            if (!ContractSchemaValidator.Validate(SchemaNames.ChangeHistoricalRefreshCheck, json).IsValid || ContractValidation.Validate(check).Count != 0)
                throw new InvalidOperationException("Preflight output failed its contract.");
            token.ThrowIfCancellationRequested();
            if (output is null) await stdout.WriteAsync(json).ConfigureAwait(false);
            else await ChangeWorkdayCommand.WriteNewAsync(output, json, token).ConfigureAwait(false);
            if (check.Status == "blocked")
                return await Error("Historical refresh preflight is blocked; inspect the receipt and replan changed entries before confirmation.", CliExitCodes.RefreshCheckBlocked);
            return CliExitCodes.Success;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or JsonException)
        {
            return await Error("Historical refresh preflight failed; verify bounded plan/current inputs and output permissions.", CliExitCodes.RefreshCheckFailure);
        }
        async Task<int> Error(string message, int exit)
        { await stderr.WriteLineAsync("eh: " + message).ConfigureAwait(false); return exit; }
    }
}
