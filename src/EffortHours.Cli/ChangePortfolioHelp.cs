namespace EffortHours.Cli;

internal static class ChangePortfolioHelp
{
    public const string Text = """
        Usage:
          eh change portfolio <repository> --pr <number-or-url> --pr <number-or-url> [options]
          eh change portfolio --manifest <portfolio.json> [options]
          eh change portfolio --author-period-manifest <manifest.json> [options]
          eh change portfolio <repository> --author <alias> [--author <alias> ...]
            --since <instant> --until <instant> [options]

        Selectors:
          --pr <number-or-url>       Repeat for each PR in one local repository
          --repo <owner/name>        Explicit GitHub repository for repeated --pr selectors
          --fetch-missing            Explicitly acquire missing selected PR objects without updating refs
          --manifest <path>          Versioned multi-repository PR manifest
          --author-period-manifest <path>
                                    Versioned multi-repository/multi-head author manifest
          --author <identity>        Exact author name/email/display alias; repeat for aliases
          --since <instant>          Inclusive interval start
          --until <instant>          Exclusive interval end
          --timezone <IANA-or-host>  Zone for offset-free interval values (default: UTC)
          --date-field <author|committer>
                                    Timestamp used only for interval selection (default: author)
          --coauthors <include|exclude>
                                    Select matching Co-authored-by trailers (default: include)
          --merge-policy <exclude|first-parent>
                                    Explicit merge behavior (default: exclude)
          --head <revision>          Pinned reachable-history boundary (default: HEAD)

        Time-bucketed comparison (author-period manifest only):
          --bucket <calendar-month|calendar-week>
                                    Split the selected interval into calendar buckets
          --bucket-manifest <path>  Use caller-supplied gap-free closed buckets
          --capacity-manifest <path>
                                    Optional positive capacity for every contributor/bucket
          --normalization <joint|isolated>
                                    Joint additive allocations or stable non-additive contributor series
          --report-view <trend|findings>
                                    Markdown report structure (default: trend)
          --generated-at <instant>  Inject an ISO-8601 report timestamp for reproducibility
          --title <text>            Override the generated report title
          --checkpoint <directory>  Override the checkpoint directory (default: <output>.eh-checkpoint)
          --no-checkpoint           Disable resumable repository-evidence checkpoints

        Output:
          --profile <implementation|recreation>  Estimation profile (default: implementation)
          --format <json|markdown>                Output format (default: json)
          --compact                               Emit compact JSON
          --hourly-rate <number>                  Override the bundled 2026 US rate
          --currency <code>                       Currency for an overridden rate (default: USD)
          --no-rate                               Omit rate and cost projection
          --output <path>                         Write output to an explicit path instead of stdout
          -h, --help                              Show this help

        PR selectors resolve immutable provider base-tip/head identities through optional gh
        support and compare their unique local merge base to the head. Provider objects must
        already exist locally unless --fetch-missing acquires objects through only the selected
        base and PR head refs without updating local refs, FETCH_HEAD, the index, or the worktree. Manifest repository paths and author aliases are
        execution-only and are not copied into reports. Author/time/co-author data selects
        immutable commits and never multiplies effort. Manifest author reports use exclusive
        contributor-match and head-reachability groups, retain zero rows, and count shared groups
        once without personal-share splits. Manifest runs emit privacy-safe reuse diagnostics in
        the report and non-semantic phase timings on stderr. Author manifests accept at most 64
        repositories. Time-bucketed reports treat repository evidence sessions as internal shards
        of one jointly reconciled portfolio; callers never join reports or add rounded totals.
        Calendar-month capacity bucket IDs use yyyy-MM (for example 2026-07); calendar-week
        IDs use week-yyyy-MM-dd with the Monday start date. Custom capacity IDs must exactly
        match the caller-supplied bucket manifest. Joint contributor series are additive but can
        change with manifest membership. Isolated contributor series are stable canonical sums,
        can overlap on shared commits, and never replace or add up to the joint portfolio total.
        Comparison output requires --output and can emit either the versioned JSON contract or a
        self-contained trend/findings Markdown report from the same calculation. Successful
        repository evidence is checkpointed atomically by immutable input/model digest. The
        default checkpoint directory is derived from the exact output path, so use an explicit
        --checkpoint path to preserve reuse when output filenames change. A failed
        shard leaves resumable evidence, writes an explicitly incomplete report, exits nonzero,
        and never substitutes zero or publishes aggregate EHE/trends.
        Results are repository-attributed Change EHE, not
        actual labor, productivity, employee rankings, performance grades, or compensation advice.
        """;
}
