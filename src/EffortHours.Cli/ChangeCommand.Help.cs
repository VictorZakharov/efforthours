namespace EffortHours.Cli;

internal sealed partial class ChangeCommand
{
    private const string HelpText = """
        Usage:
          eh change <repository> --commit <revision> [--parent <revision>] [options]
          eh change --repo <owner/name> --commit <revision> [--parent <revision>] [options]
          eh change <repository> --range <base>..<head> [options]
          eh change --repo <owner/name> --range <base>..<head> [options]
          eh change <repository> --base <revision> --head <revision> [options]
          eh change --repo <owner/name> --base <revision> --head <revision> [options]
          eh change <repository> --pr <number-or-url> [--repo <owner/name>] [options]
          eh change --pr <github-pr-url> [--fetch-missing] [options]
          eh change --pr <number> --repo <owner/name> [--fetch-missing] [options]
          eh change --base-path <directory> --head-path <directory> [options]
          eh change --base-evidence <evidence.json> --head-evidence <evidence.json> [options]
          eh change explain <change-estimate.json> --item <id> [options]
          eh change portfolio <repository> --pr <pr> --pr <pr> [options]
          eh change portfolio --manifest <portfolio.json> [options]
          eh change portfolio <repository> --author <alias> --since <instant> --until <instant> [options]
          eh change today --owner <owner> --author @me --timezone <zone>
            --include-open-prs --scope engineering --capacity-hours <hours> [options]
          eh change period --owner <owner> --author <identity> --period <named-period> [options]
          eh change compare-team --owner <owner> --contributors-from <owner/repository> [options]
          eh change review-days <comparison.json> --work-records <records.json> [--entry-policy equal-matched-entries/1.0.0]
          eh change allocate-days <comparison.json> --workdays <workdays.json> --policy equal-declared-days/1.0.0
          eh change scope show engineering

        Selectors:
          --commit <revision>  Compare one commit with its first parent; root uses the empty tree
          --parent <revision>  Required choice for a merge commit
          --range <base>..<head>
                               Compare the coherent final range and reconcile isolated commits
          --base <revision>    Explicit final base revision (requires --head)
          --head <revision>    Explicit final head revision (requires --base)
          --pr <number-or-url> Resolve one PR and compare its unique merge base/head
          --repo <owner/name>  Explicit GitHub repository for any checkout-free Git selector
          --fetch-missing      Explicitly resolve and acquire missing selected objects without updating refs;
                               checkout-free mode uses the private managed bare cache
          --base-path <path>   Statically scan one local base directory (requires --head-path)
          --head-path <path>   Statically scan one local head directory (requires --base-path)
          --base-evidence <path>
                               Load one saved repository-evidence base snapshot
          --head-evidence <path>
                               Load one saved repository-evidence head snapshot

        Output:
          --profile <implementation|recreation>  Estimation profile (default: implementation)
          --format <json|markdown>                Output format (default: json)
          --compact                               Emit compact JSON
          --hourly-rate <number>                  Override the bundled 2026 US rate
          --currency <code>                       Currency for an overridden rate (default: USD)
          --no-rate                               Omit rate and cost projection
          --output <path>                         Write output to an explicit path instead of stdout
          -h, --help                              Show this help

        Directory and evidence pairs work without Git or GitHub. Directory inputs are
        statically scanned and content-pinned; saved evidence has no source bodies, so
        modified maintained paths retain conservative normalization. Git mode reads
        immutable objects directly and does not check out. It does not fetch by default;
        --fetch-missing explicitly allows provider-backed Git mode to resolve immutable
        commits and acquire only those selected objects without updating local refs,
        FETCH_HEAD, the index, or a worktree. A full GitHub PR URL, or any Git selector
        with --repo, can run without a checkout through the private EffortHours bare
        cache. Without --fetch-missing, checkout-free mode performs no provider or network
        access and succeeds only from complete immutable resolution and object caches.
        No selector executes target code.
        The current change model is experimental and uncalibrated.
        """;
}
