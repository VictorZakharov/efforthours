# One-command daily EHE calendars

`eh calendar` creates a teammate's graph without AI, authored manifests, a website
build, or remote model access. It composes the existing immutable author-period
portfolio with independent reconciliation within each local day and repository.
Its calculation identity is `independent-local-day-change/1.0.0`; JSON retains
each day's category and overlap-adjustment ledger. Extending the selected date
range cannot change an existing day's numeric result.
The graph shows **repository-attributed Change EHE**, not snapshot stock changes,
actual labor, productivity, or an AI skill measurement. Existing estimator priors,
admission boundaries and saved reports remain intact. New reports add optional
daily lineage and advance the source and portfolio identities.

```text
eh calendar
eh calendar . --from 2026-09-01 --to 2026-09-30 --output ../calendar.html
eh calendar --all-repos --workspace ../projects --author developer@example.invalid --format text
eh calendar --project api=../api --project app=../app --author developer@example.invalid --output ../calendar.html
eh calendar --repo api=example/api --author developer@example.invalid --fetch-missing --output calendar.html
eh calendar --interactive --project api=../api --from 2026-09-01
```

## Defaults and interactive setup

Plain `eh calendar` prompts. Supplied flags run unattended unless `--interactive`
is explicit. Interactive prompts show defaults and accept Enter or replacement
values; explicit flags are preserved. EOF fails with agent-friendly flag guidance
rather than waiting indefinitely or accepting incomplete input. Prompts go to
stderr, leaving stdout available for the selected artifact.

Defaults are the last complete calendar month in the local timezone, eight
reference hours per calendar day, HTML, `HEAD`, and committer-date selection.
`--date-field author` explicitly selects author dates instead. Interactive setup
prompts for this choice. Merge commits are excluded; valid coauthor matches enter
the same selected change once. Neither timestamp establishes when work occurred.
Interactive setup suggests a file under the host temporary directory; unattended output defaults to stdout.
`--output -` means stdout. Identity defaults to the distinct configured Git
`user.email` values from selected local repositories. This is a visible execution
selector, never inferred from activity. Missing identity requires `--author`;
checkout-free projects require an explicit author. Repeat `--author` for aliases.

Inside a checkout, the default scope is that repository. Outside a checkout it is
repositories under the current directory. `--all-repos` enables the latter choice
inside a checkout, defaulting its workspace to the checkout's parent. `--workspace`
sets the discovery root explicitly. Interactive workspace discovery lists paths
and generated safe project IDs, then offers all or an exact comma-separated subset.
Explicit repeated `--project id=path` and `--repo id=owner/name` select an exact set
and may mix local and checkout-free inputs; they do not combine with discovery.

Discovery is filesystem-only and bounded to 10,000 directories and 256 checkouts.
It does not follow reparse points, hidden folders, `node_modules`, `bin`, or `obj`,
and stops descending at each Git root. It does not search the machine or discover
GitHub accounts. Normalized Git roots must map one-to-one to project IDs. Generated
IDs avoid embedding local directory names; callers can supply meaningful public
IDs through `--project`. The command resolves each selected head once before
constructing the manifest. Uncommitted work and unreachable branches are excluded.

## Dates, runtime, and acquisition

`--from` and `--to` are inclusive `yyyy-MM-dd` **complete local calendar dates**.
The internal interval ends at exclusive midnight after `--to`. The convenience
view accepts 1 through 512 days and excludes today/future dates; broader or partial
periods remain available through the existing low-level author-period commands.
This presentation envelope does not cap or truncate the underlying selector.
Timezone/DST rules use the existing parser and bucket policy; ambiguous or skipped
midnight requires another supported explicit low-level interval. DST changes do
not change the reference denominator per date.

Dates bound in-window selection and snapshot/static analysis. Complete reachable
metadata may still be traversed to preserve correctness for non-monotonic author
timestamps. A smaller date range is not a promise of proportional wall time.
The cancellable `--timeout-seconds` deadline defaults to 3600 (maximum 86400).
All existing ledger, cache, queue, checkpoint, and output budgets remain enforced.

Local runs are offline and read-only. `--repo` reuses the managed immutable Git
cache; only `--fetch-missing` authorizes provider resolution/object acquisition.
Interactive provider input offers that explicit opt-in with **no** as its default.
No provider discovery, target code, dependency installation, worktree changes,
or ref updates occur. Acquisition/selection/analysis progress goes to stderr,
including a five-second still-running/elapsed heartbeat during long operations,
native phase and completed-unit records, and final checkpoint counters. The
heartbeat is liveness, not a fabricated percentage or ETA. Ctrl+C cancels.

## One result, several views

`--format html|text|json` chooses standalone HTML, Markdown text, or the existing
schema-validated `change-portfolio-comparison-report`. HTML uses embedded styles
and script, no CDN, external fonts, telemetry, network requests, or dependencies.
A script-hash content security policy, escaped JSON, encoded HTML, and DOM
`textContent` keep data from becoming executable markup. The full daily table
remains usable without JavaScript.

The default interactive display is × EHE; Hours is a toggle. Project checkboxes,
All/None, day details, and the daily table derive from the same independent daily repository groups.
Low/expected/high project cells conserve every canonical portfolio bucket and total. Idle dates are zero after complete selection. Changing checkboxes
does not re-estimate, independently reconcile fragments, or multiply shared work.
Cross-repository copies retain the existing separate-repository boundary.

× EHE divides expected daily Change EHE by the explicit reference-hours denominator
(default eight, configurable with `--capacity-hours-per-day`). The overall ratio
divides total expected EHE by total reference hours across every included calendar
date, including idle dates. It never averages daily ratios or multiplies reference
hours by selected project count. The denominator is not measured labor or proof
of AI effectiveness. Planning ranges, estimator identity, source semantic digest,
scope limitations, and experimental/uncalibrated status remain visible.

## Reuse and failure

`--checkpoint` preserves immutable repository evidence across output filenames and
formats. With a file output the default is `<output>.eh-checkpoint`; stdout without
an explicit checkpoint uses no persistence. Views/capacity changes reuse evidence
without reanalysis. A changed selection or head invalidates its bound inputs.
Calendar cache-only diagnostics remain in execution observations rather than the
canonical source portfolio, preserving semantic identity across cold/warm views.
Reports/checkpoints must be outside selected source repositories.

The calendar treats a completely selected no-match interval as valid zero. A
failed repository retains successful checkpoints, returns nonzero, and produces
an incomplete artifact with safe failure lineage and no aggregate or graph.
Input/acquisition/cancellation failures preserve earlier output. JSON retains full
canonical lineage; HTML/text allowlist safe project IDs, dates, numeric operands,
estimator/source identities, and safe failure phase/category/digest. They do not
embed source, messages, identity aliases, execution paths, or provider locators.
Changing this CLI/reporting convenience does not admit or calibrate a model.

The added Change helper source participates in the existing conservative snapshot
implementation fingerprint. Saved snapshot receipts from earlier builds may need
their already-documented explicit epoch upgrade; they are never relabeled as
compatible. This does not change numerical priors or legacy report schemas.

Low-level `eh change portfolio --author-period-manifest ... --bucket independent-day`
uses the same batch policy. Existing `--bucket calendar-day` retains joint interval
allocation for compatibility. Within a day the existing overlap reconciliation
and joint contributor policy apply; isolated contributor normalization is rejected
for independent-day mode. Replacement stock remains the separate snapshot daily
series, whose signed differences can be negative. Change and stock are different
measurements and must not be substituted or forced to match.
