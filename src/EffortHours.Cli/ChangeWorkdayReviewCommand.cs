using System.Text.Json;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Cli;

internal static class ChangeWorkdayReviewCommand
{
    private const string Help = """
        Usage: eh change review-days <comparison.json> --work-records <records.json>
                 [--workdays <workdays.json> --workday-policy equal-declared-days/1.0.0]
                 [--entry-policy <entry-policy>] [--format json|markdown]
                 [--compact] [--output <new-path>]
        Reviews digest-bound implementation, meeting, PTO and mixed records against a
        complete joint single-contributor calendar-day comparison. Blank retained dates,
        missing records and unresolved repository relationships remain explicit discrepancies.
        Retained entry allocations use equal weights, a fixed eight-hour denominator and
        two-decimal contributions that conserve the rounded matched daily multiplier.
        Declared dates can use equal-declared-day-entries/1.0.0 to conserve one period
        multiplier; default retained review uses equal-matched-entries/1.0.0.
        Original workdays remain unresolved; unavailable values are omitted, never zero-filled.
        Offline; no Git/provider access, estimator rerun, input overwrite or timesheet mutation.
        EHE remains experimental and uncalibrated. See 'eh docs show workday-review'.
        """;

    public static async Task<int> ExecuteAsync(string[] arguments, TextWriter stdout, TextWriter stderr, CancellationToken token)
    {
        if (arguments.Length == 0 || arguments.Any(value => value is "help" or "--help" or "-h"))
        {
            await stdout.WriteLineAsync(Help).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }
        string? records = null, entryPolicy = null, output = null, workdays = null, workdayPolicy = null;
        string format = "json";
        bool compact = false;
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int index = 1; index < arguments.Length; index++)
        {
            string option = arguments[index];
            if (!seen.Add(option)) return await Error("Duplicate workday review option.", CliExitCodes.UsageError);
            if (option == "--compact") { compact = true; continue; }
            if (++index == arguments.Length) return await Error("Workday review option requires a value.", CliExitCodes.UsageError);
            switch (option)
            {
                case "--work-records": records = arguments[index]; break;
                case "--entry-policy": entryPolicy = arguments[index]; break;
                case "--workdays": workdays = arguments[index]; break;
                case "--workday-policy": workdayPolicy = arguments[index]; break;
                case "--output": output = arguments[index]; break;
                case "--format": format = arguments[index]; break;
                default: return await Error("Unknown workday review option.", CliExitCodes.UsageError);
            }
        }
        if (records is null || (workdays is null) != (workdayPolicy is null) || workdayPolicy is not null && workdayPolicy != ChangeWorkdayPolicies.EqualDeclaredDaysV1 ||
            entryPolicy is not null && entryPolicy != (workdays is null ? ChangeWorkdayReviewPolicies.EqualEntries : ChangeDeclaredWorkdayReviewPolicies.EqualEntries) ||
            format is not ("json" or "markdown") || compact && format != "json")
            return await Error("Supply work records, paired supported workday options and a matching entry policy; format must be json or markdown.", CliExitCodes.UsageError);
        try
        {
            if (output is not null && File.Exists(output))
                return await Error("Workday review --output must be a new file; existing artifacts are never overwritten.", CliExitCodes.InvalidInput);
            ChangePortfolioComparisonReport source = await ChangeWorkdayCommand.LoadAsync<ChangePortfolioComparisonReport>(arguments[0],
                SchemaNames.ChangePortfolioComparisonReport, ChangePortfolioLimits.MaximumRenderedOutputBytes, token);
            ChangeWorkRecordManifest manifest = await ChangeWorkdayCommand.LoadAsync<ChangeWorkRecordManifest>(records,
                SchemaNames.ChangeWorkRecordManifest, 1048576, token);
            ChangeWorkdayReviewReport report = workdays is null ? ChangeWorkdayReviewer.Review(source, manifest, entryPolicy)
                : ChangeDeclaredWorkdayReviewer.Review(source, manifest,
                    await ChangeWorkdayCommand.LoadAsync<ChangeWorkdayManifest>(workdays, SchemaNames.ChangeWorkdayManifest, 1048576, token), workdayPolicy!, entryPolicy);
            string json = compact ? ContractJson.SerializeCompact(report) : ContractJson.Serialize(report);
            if (!ContractSchemaValidator.Validate(SchemaNames.ChangeWorkdayReviewReport, json).IsValid)
                throw new InvalidOperationException("Workday review output failed its public schema.");
            string rendered = format == "markdown" ? ChangeWorkdayReviewMarkdownRenderer.Render(report) : json.ReplaceLineEndings("\n").TrimEnd() + "\n";
            token.ThrowIfCancellationRequested();
            if (output is null) await stdout.WriteAsync(rendered).ConfigureAwait(false);
            else await ChangeWorkdayCommand.WriteNewAsync(output, rendered, token).ConfigureAwait(false);
            return CliExitCodes.Success;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or JsonException)
        { return await Error("Workday review failed: " + exception.Message, CliExitCodes.InvalidInput); }

        async Task<int> Error(string message, int exit)
        {
            await stderr.WriteLineAsync("eh: " + message).ConfigureAwait(false);
            return exit;
        }
    }
}
