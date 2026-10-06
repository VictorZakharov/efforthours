using System.Text.Json;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Cli;

internal static class ChangeRewriteReviewCommand
{
    private const string Help = """
        Usage: eh change review-rewrite <manifest.json> [--profile implementation|recreation]
                 [--scope engineering|all] [--format json|markdown] [--compact] [--output <new-path>]
        Offline immutable original/upstream/replay/retained range review. A declared replay
        snapshot isolates the novel retained artifact delta; conflicting replay paths and
        event dates retain explicit caller provenance. Missing replay/date evidence is unresolved.
        Comparisons are non-additive and do not replace a jointly reconciled portfolio.
        Never fetches objects, executes target code, recovers actual labor or writes timesheets.
        EHE remains experimental and uncalibrated. See docs/REWRITE_REPLAY_REVIEW.md.
        """;

    public static async Task<int> ExecuteAsync(string[] args, TextWriter stdout, TextWriter stderr, CancellationToken token)
    {
        if (args.Length == 0 || args.Any(value => value is "help" or "--help" or "-h"))
        {
            await stdout.WriteLineAsync(Help).ConfigureAwait(false);
            return args.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }
        string format = "json", scope = "engineering";
        string? output = null;
        EstimationProfile profile = EstimationProfile.Implementation;
        bool compact = false;
        HashSet<string> seen = new(StringComparer.Ordinal);
        for (int i = 1; i < args.Length; i++)
        {
            string option = args[i];
            if (!seen.Add(option)) return await Error("Duplicate rewrite review option.", CliExitCodes.UsageError);
            if (option == "--compact") { compact = true; continue; }
            if (++i == args.Length) return await Error("Rewrite review option requires a value.", CliExitCodes.UsageError);
            switch (option)
            {
                case "--format": format = args[i]; break;
                case "--scope": scope = args[i]; break;
                case "--output": output = args[i]; break;
                case "--profile":
                    if (args[i] is not ("implementation" or "recreation")) return await Error("Invalid rewrite review profile.", CliExitCodes.UsageError);
                    profile = args[i] == "implementation" ? EstimationProfile.Implementation : EstimationProfile.Recreation; break;
                default: return await Error("Unknown rewrite review option.", CliExitCodes.UsageError);
            }
        }
        if (format is not ("json" or "markdown") || scope is not ("engineering" or "all") || compact && format != "json")
            return await Error("Invalid rewrite review format or scope.", CliExitCodes.UsageError);
        try
        {
            if (output is not null && File.Exists(output)) return await Error("Rewrite review output must be a new file.", CliExitCodes.InvalidInput);
            ChangeRewriteReviewManifest input = await ChangeWorkdayCommand.LoadAsync<ChangeRewriteReviewManifest>(args[0],
                SchemaNames.ChangeRewriteReviewManifest, 1048576, token);
            input = input with { RepositoryPath = Path.GetFullPath(input.RepositoryPath, Path.GetDirectoryName(Path.GetFullPath(args[0]))!) };
            ChangeRewriteReviewReport report = await new ChangeRewriteReviewer().ReviewAsync(input, profile, scope == "engineering", token);
            string json = compact ? ContractJson.SerializeCompact(report) : ContractJson.Serialize(report);
            if (!ContractSchemaValidator.Validate(SchemaNames.ChangeRewriteReviewReport, json).IsValid)
                throw new InvalidOperationException("Rewrite review output failed its public schema.");
            string rendered = format == "markdown" ? ChangeRewriteReviewMarkdownRenderer.Render(report) : json.ReplaceLineEndings("\n").TrimEnd() + "\n";
            token.ThrowIfCancellationRequested();
            if (output is null) await stdout.WriteAsync(rendered).ConfigureAwait(false);
            else await ChangeWorkdayCommand.WriteNewAsync(output, rendered, token).ConfigureAwait(false);
            return report.Status == "unresolved-object-evidence" ? CliExitCodes.InvalidInput : CliExitCodes.Success;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or UnauthorizedAccessException or JsonException or ExternalCommandException)
        { return await Error("Rewrite review failed: " + exception.Message, CliExitCodes.InvalidInput); }

        async Task<int> Error(string message, int code)
        {
            await stderr.WriteLineAsync("eh: " + message).ConfigureAwait(false);
            return code;
        }
    }
}
