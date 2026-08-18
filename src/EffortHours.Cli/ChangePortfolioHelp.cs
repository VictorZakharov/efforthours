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
        the report and non-semantic phase timings on stderr. Results are repository-attributed Change EHE, not
        actual labor, productivity, employee rankings, performance grades, or compensation advice.
        """;
}
