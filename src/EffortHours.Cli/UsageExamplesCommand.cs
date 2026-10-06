namespace EffortHours.Cli;

internal static class UsageExamplesCommand
{
    private const string Topics = "repository, change, portfolio, calendar, report, review, inspect, agent, rebase";

    public static async Task<int> ExecuteAsync(string[] arguments, TextWriter stdout, TextWriter stderr)
    {
        if (arguments is ["--help"] or ["-h"] or ["help"])
        {
            await stdout.WriteLineAsync(Help).ConfigureAwait(false);
            return CliExitCodes.Success;
        }

        string selection = arguments.Length == 0 ? "popular" : arguments[0].ToLowerInvariant();
        if (arguments.Length > 1 || selection is not ("popular" or "all") &&
            !Recipes.Any(recipe => recipe.Topic == selection))
        {
            await stderr.WriteLineAsync("eh: Expected 'eh examples [popular|all|<topic>]'.").ConfigureAwait(false);
            await stderr.WriteLineAsync($"Topics: {Topics}.").ConfigureAwait(false);
            return CliExitCodes.UsageError;
        }

        await stdout.WriteLineAsync($"EffortHours usage examples ({selection})").ConfigureAwait(false);
        await stdout.WriteLineAsync("Replace <placeholders> with your inputs; run commands from the selected checkout when using '.'.").ConfigureAwait(false);
        await stdout.WriteLineAsync("Examples are printed only; no analysis, network access, or file writes are performed.").ConfigureAwait(false);
        await stdout.WriteLineAsync("EHE is experimental and uncalibrated replacement effort, not actual labor or productivity.").ConfigureAwait(false);
        foreach (Recipe recipe in Recipes.Where(recipe => selection == "all" ||
            (selection == "popular" ? recipe.Popular : recipe.Topic == selection)))
        {
            await stdout.WriteLineAsync().ConfigureAwait(false);
            await stdout.WriteLineAsync($"[{recipe.Topic}] {recipe.Title}").ConfigureAwait(false);
            await stdout.WriteLineAsync($"  {recipe.Command}").ConfigureAwait(false);
            await stdout.WriteLineAsync($"  {recipe.Description}").ConfigureAwait(false);
        }

        await stdout.WriteLineAsync().ConfigureAwait(false);
        await stdout.WriteLineAsync("More: eh examples all | eh examples <topic> | eh <command> --help").ConfigureAwait(false);
        await stdout.WriteLineAsync($"Topics: {Topics}.").ConfigureAwait(false);
        return CliExitCodes.Success;
    }

    private const string Help = """
        Usage:
          eh examples [popular|all|<topic>]
          eh --examples [popular|all|<topic>]

        No argument prints common workflows. 'all' prints every curated recipe;
        a topic prints only that workflow family. This is a guide, not an exhaustive
        flag reference. Use 'eh <command> --help' for complete command options.
        Topics: repository, change, portfolio, calendar, report, review, inspect, agent, rebase.
        Printing examples is offline, read-only, and does not execute the recipes.
        """;

    private sealed record Recipe(string Topic, string Title, string Command, string Description, bool Popular = false);

    private static readonly Recipe[] Recipes =
    [
        new("repository", "Estimate the current repository",
            "eh estimate . --profile implementation --format markdown --no-rate",
            "Estimate the current functional and quality state with detailed requirements supplied. Local, read-only; no Git history or target-code execution.", true),
        new("repository", "Save compact JSON for humans or agents",
            "eh estimate . --profile implementation --format json --compact --no-rate --output ../estimate.json",
            "Save a canonical estimate with stable evidence and work-item IDs; --no-rate omits cost without changing EHE. Choose an output outside the source tree.", true),
        new("repository", "Estimate recreation from a behavioral specification",
            "eh estimate . --profile recreation --format markdown --no-rate",
            "Include the additional architecture, data, interface and UX decisions represented by the artifact. Neither profile measures historical labor."),
        new("repository", "Analyze a GitHub snapshot without a checkout",
            "eh estimate --repo <owner/repository> --revision <commit-sha> --fetch-missing --format markdown --no-rate",
            "Explicitly authorize provider resolution and missing-object acquisition into the private cache. Omit --fetch-missing for an already cached offline rerun.", true),
        new("repository", "Save static evidence for reuse",
            "eh scan . --output ../evidence.json",
            "Save repository facts without estimating EHE. Later use: eh estimate ../evidence.json --no-rate. Does not build or run the target."),
        new("change", "Estimate one completed commit",
            "eh change . --commit HEAD --format markdown --no-rate",
            "Compare the selected immutable commit to its parent and value its normalized final delta, rather than commit activity.", true),
        new("change", "Estimate a branch's final delta",
            "eh change . --base <base-revision> --head <head-revision> --format markdown --no-rate",
            "Use existing local Git revisions; the final base-to-head functional and quality difference is authoritative.", true),
        new("change", "Estimate a pull request without a checkout",
            "eh change --repo <owner/repository> --pr <number> --fetch-missing --format markdown --no-rate",
            "Resolve immutable PR base/head identities and explicitly acquire missing objects. Requires authenticated GitHub access; does not run target code."),
        new("change", "Compare two non-Git directories",
            "eh change --base-path <before-directory> --head-path <after-directory> --format markdown --no-rate",
            "Estimate the normalized final difference between two local source trees, read-only."),
        new("portfolio", "Reconcile several pull requests",
            "eh change portfolio . --pr <first-number> --pr <second-number> --format markdown --no-rate",
            "Requires available PR identities and Git objects. Reconcile overlap and shared context jointly; do not add separate PR estimates."),
        new("portfolio", "Report today's GitHub-selected changes",
            "eh change today --owner <owner> --author \"@me\" --timezone <timezone> --include-open-prs --scope engineering --capacity-hours 8 --format markdown --no-rate",
            "Requires authenticated gh. Explicit provider-assisted discovery/acquisition selects today's reachable work. Capacity is only a reference denominator, not actual hours.", true),
        new("portfolio", "Report last month's daily changes",
            "eh change period --owner <owner> --author \"@me\" --period last-month --timezone <timezone> --scope engineering --breakdown day --capacity-hours-per-day 8 --format markdown --no-rate",
            "Requires authenticated gh; discovers retained history and produces one jointly reconciled period. Use an IANA timezone such as America/Toronto.", true),
        new("portfolio", "Preflight a frozen author-period manifest",
            "eh change portfolio --author-period-manifest <manifest.json> --preflight --no-rate",
            "Requires a valid manifest and its pinned local/cached heads. Measure selection and resource bounds without snapshot analysis or EHE estimation."),
        new("portfolio", "Calculate and resume a manifest comparison",
            "eh change portfolio --author-period-manifest <manifest.json> --bucket calendar-month --checkpoint ../eh-checkpoint --format json --compact --no-rate --output ../comparison.json",
            "Requires a valid author-period manifest. Reuse completed digest-bound evidence on reruns; never split the interval and sum independently reconciled reports. Keep outputs/checkpoints outside source trees.", true),
        new("calendar", "Create an offline daily graph",
            "eh calendar . --from <yyyy-MM-dd> --to <yyyy-MM-dd> --timezone <timezone> --author <git-email-or-name> --output ../calendar.html",
            "Use inclusive complete dates and existing local Git history. The offline HTML graph represents independently normalized daily Change EHE, not hours worked.", true),
        new("calendar", "Start guided calendar setup",
            "eh calendar",
            "Prompts for scope and settings, with last-month defaults. Inside a checkout starts with that repository; outside can discover repositories under the chosen workspace."),
        new("report", "Summarize an existing estimate",
            "eh report <estimate.json> --view review --format markdown",
            "Render a saved canonical repository estimate without rescanning source. Inspect important items and uncertainty before drawing conclusions.", true),
        new("report", "Explain a repository item",
            "eh explain <evidence.json> --item <capability-or-work-item-id> --profile implementation --format markdown",
            "Use saved scan evidence and an ID from the matching-profile estimate to inspect evidence and calculation lineage."),
        new("report", "Explain a Change item",
            "eh change explain <change-estimate.json> --item <work-item-id> --format markdown",
            "Use an ID from the saved Change report to inspect that item's calculation lineage."),
        new("review", "Prepare an optional AI review packet",
            "eh review packet . --compact --output ../review-packet.json",
            "Prepare a digest-bound, rate-free packet without source excerpts or calling any AI provider. The local estimate is complete without AI."),
        new("inspect", "Inspect a public output schema",
            "eh schema show change-author-period-manifest",
            "Print the embedded JSON schema to author or validate a manifest. Use eh schema list to discover every available schema."),
        new("inspect", "Inspect the shipped estimator",
            "eh model info",
            "Print bundled estimator identity and provenance without scanning a repository. Use eh model show for the full seed rules."),
        new("inspect", "Check the installed version",
            "eh version",
            "Print the installed informational version and source commit. Top-level eh --help also includes this version."),
        new("rebase", "Bound retained historical discovery to an explicit repository",
            "eh change period --owner <owner> --repository <owner/repository> --author <git-alias> --provider-login <login> --since <start> --until <end> --breakdown day --timezone <zone> --scope engineering --capacity-hours-per-day 8 --discovery-timeout-seconds 900 --max-acquired-mib 4096 --no-rate --output <period.json>",
            "Explicit GitHub access and missing-object acquisition; coverage is restricted to the named repository. Older author dates survive later committer dates. Acquisition is bounded and visible on stderr; lost intermediate history stays unresolved."),
        new("rebase", "Attribute a declared rewrite event from immutable evidence",
            "eh change portfolio --author-period-manifest <rewrite-pair.json> --bucket calendar-day --scope engineering --no-rate --output <rewrite-days.json>",
            "Local offline manifest: pin original/rewritten heads and actual old/new first parents in repositories[].rewriteEvents with declared-rewrite-event/1.0.0 and eventTimestamp. Joint reconciliation preserves replay once and allocates only the joint retained-budget remainder to the declared event; this is not causal resolution labor; see docs/REWRITE_EVENT_ATTRIBUTION.md. Missing proof fails."),
        new("rebase", "Review immutable replay evidence across multi-commit or squashed ranges",
            "eh change review-rewrite <replay-manifest.json> --scope engineering --format markdown",
            "Offline canonical original/upstream/replay/retained comparisons. Pin a replay snapshot with provenance to isolate the novel retained delta; declare the event date separately. Nonconflicting paths are verified, conflicting replay remains caller-declared and missing evidence stays unresolved. Comparisons are non-additive; see docs/REWRITE_REPLAY_REVIEW.md."),
        new("rebase", "Plan a historical note refresh without editing entries",
            "eh change plan-refresh <period.json> --work-records <records.json> --entries <refresh-manifest.json> --output <new-plan.json>",
            "Offline dry run over explicit dates and record IDs. Preserves original snapshots; independently checks note/EHE permissions and locked/invoiced restrictions. One managed annotation is idempotent; unresolved evidence never means zero labor. No entry writes; see docs/HISTORICAL_NOTE_REFRESH.md."),
        new("rebase", "Review external work records against retained dates",
            "eh change review-days <period.json> --work-records <records.json> --format markdown",
            "Offline digest-bound review: blank retained dates, missing records, mixed entries and repository-scope gaps stay unresolved. Optional --entry-policy equal-matched-entries/1.0.0 allocates only matched retained values with a fixed eight-hour denominator and conserved two-decimal contributions. See docs/WORKDAY_REVIEW.md; never edits inputs or timesheets."),
        new("rebase", "Allocate novel replay-range evidence within a joint daily budget",
            "eh change portfolio --author-period-manifest <replay-ranges.json> --bucket calendar-day --no-rate --output <replay-days.json>",
            "Local offline manifest: repositories[].replayEvents under declared-replay-range/1.0.0 binds old/original/new/replay/retained endpoints and separate replay/date provenance. Canonical comparisons remain non-additive; original effort is reserved first and event allocation is capped at the remaining joint member budget and any cap is explicit. Missing replay/date stays unresolved. See docs/REPLAY_RANGE_ATTRIBUTION.md."),
        new("rebase", "Use external workday records after a squash or rebase",
            "eh change review-days <period.json> --work-records <records.json> --workdays <workdays.json> --workday-policy equal-declared-days/1.0.0 --entry-policy equal-declared-day-entries/1.0.0",
            "Offline declared-date projection with exact dated implementation anchors and unchanged source totals. Conserves one fixed-eight period multiplier; logged time never supplies weights. The same options work with plan-refresh. External dates do not recover missing Git history; see docs/WORKDAY_REVIEW.md."),
        new("rebase", "Allocate a deduplicated period across external workday declarations",
            "eh change allocate-days <period.json> --workdays <workdays.json> --policy equal-declared-days/1.0.0 --output <new-allocated.json>",
            "Offline saved-artifact projection for one contributor and whole calendar days. Bind declarations to verification.semanticDigest; output is explicitly allocated with unresolved original workdays. Conserves every EHE category/range and existing capacity; logged hours never weight effort. See docs/WORKDAY_ALLOCATION.md. Does not edit inputs or timesheets."),
        new("agent", "Install guidance for Codex",
            "eh agent codex --install",
            "Explicitly write the packaged companion skill into Codex's skill directory. Use eh agent codex --check after updating EffortHours.")
    ];
}
