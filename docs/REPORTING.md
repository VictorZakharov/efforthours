# Reporting and explanation

## Purpose

EffortHours reports must make large estimates reviewable without changing their
hours or hiding calculation lineage. The canonical estimate remains lossless;
compact views are deterministic projections with stable drill-down IDs.

## Canonical report

`EstimateReport` v1 is the canonical repository estimate contract:

```text
eh estimate <repository-or-evidence.json> --format json
```

It contains the complete work-item ledger, evidence references, assumptions,
exclusions, uncertainty, verification state, diagnostics, and optional pricing.
Views never become an independent source of truth and never recalculate effort.

Change and portfolio commands have their own canonical v1 reports under the same
general rules: observed selection evidence, inferred normalization, estimated
work, reconciliation, diagnostics, and pricing stay distinct.

The multi-repository author-period manifest contract has a deliberately asymmetric
execution/report boundary. Local repository paths and raw identity aliases are
accepted only as execution selectors. The portfolio report retains their
order-independent manifest digest plus stable contributor, repository, and head
IDs, immutable head objects, contributor match kinds, and head reachability. The
older direct `--author` form remains backward compatible and continues to retain
its caller-supplied aliases. Manifest reports also retain exclusive contributor-
match and per-repository head-reachability groups, deterministic zero summaries,
low/expected/high allocation arithmetic, signed reconciliation deltas, and stable
item/adjustment lineage. Contributor and head groups are alternative views of one
authoritative portfolio total; shared EHE is never copied into several additive
personal rows.

Direct diagnostic `FB5312` and manifest diagnostic `FB5326` record the retained
exact in-window candidate count, its repository bound, and privacy-safe
direct/co-author counts by requested public contributor ID. Raw aliases remain
excluded. An over-limit failure carries the same breakdown and the exact observed
count, or a stated lower bound only after the separate diagnostic ceiling.

Manifest author-period report diagnostic `FB5325` contains deterministic,
privacy-safe cache request/hit, unique-key, revisit-miss, byte, eviction, and
declared retention counts for snapshot, immutable file-analysis, inventory, and
Git-blob reuse, plus lazy Git object-metadata request/hit/unique/eviction counts,
including how many incremental inventories used repository-level batch diffs. It
contains no local paths, raw aliases, source excerpts, or wall-clock values. The
CLI writes nine measured execution-phase durations to stderr after a successful
manifest run. Those non-semantic timings remain excluded from the canonical
portfolio report and EHE calculations.

Time-bucketed comparison mode adds a separate
`change-portfolio-comparison-report` v1 wrapper around that canonical report. It
contains the bucket/contributor matrix, exact totals, optional caller-supplied
capacity ratios, trend inputs/statistics, privacy-safe repository-shard lineage,
reuse counters, and operational phase/progress/resource observations. Operational
fields are intentionally excluded from `verification.semanticDigest`; they may
vary across identical runs and never affect EHE. `--format markdown --report-view
trend` renders a complete generic trend report. `--report-view findings` renders a
generic anonymized engineering-findings report from the same structured data,
without agent-authored arithmetic or unsupported causal guesses. Both require the
caller to choose the exact output path for low-level manifest comparisons.

The explicit GitHub-assisted `change today` view is the bounded exception: it may
write JSON or concise Markdown directly to stdout or atomically replace an
explicit output path. It wraps the same canonical source portfolio and adds
paired `asOf`, managed-cache discovery, engineering scope-profile, and scope-
summary fields. Discovery records only a versioned protocol, scope digest,
identity-source category, completeness, bounded repository/head/PR/query/page/
object counts, acquired bytes, and elapsed time. It contains no owner, repository
display name, PR number, alias, source excerpt, credential, or execution-only path.
Scope identity participates in semantic/checkpoint digests; operational discovery
and timing fields do not.

Today Markdown reports status, snapshot coverage, EHE and X low/expected/high,
the explicit capacity policy and actual-hours formula, selected/admitted/scope-
empty changes, compact repository-attributed expected EHE, scope identity and
important exclusions, estimator identities, checkpoint reuse separately from
within-run analysis reuse, end-to-end/per-phase timings, and interpretation limits.
It emits no chart, one-point OLS/R-squared, first/latest change, duplicate series,
or synthetic `0%`. A complete no-match selection is an explicit zero result. An
incomplete run preserves a privacy-safe root phase/category/digest and omits every
aggregate and ratio rather than substituting zero.

Comparison reports record `contributorNormalization`. The default `joint` view
keeps exclusive and shared contributor-match sets additive to the jointly
reconciled portfolio, so allocations can change with manifest membership. The
optional `isolated` view emits one membership-stable canonical series per
contributor. Shared commits can then occur in several contributor series, making
those series explicitly non-additive; they never replace the one authoritative
joint portfolio total. Trend Markdown charts the portfolio and every contributor
series, publishes their exact line order and numeric fallback, and states that
coverage is limited to manifest repositories and objects reachable from pinned
local heads.

Derived capacity cells use `yyyy-MM` calendar-month bucket IDs and
`week-yyyy-MM-dd` calendar-week IDs, where the date is the Monday start in the
manifest timezone. Custom IDs come from the bucket manifest. Validation reports
the exact missing and unexpected public cells. `eh schema show` accepts either a
bare schema stem or its full `.schema.json` filename.

Repository evidence is checkpointed by immutable repository/head/selection/model
digest unless `--no-checkpoint` is explicit. The default directory is
`<output>.eh-checkpoint`; an exact rerun reuses successful repository evidence and
a one-head change invalidates only its repository. Changing the output filename
also changes that default directory, so callers that want reuse across differently
named output files should supply one stable `--checkpoint` path. If any repository fails, the
requested JSON or Markdown file is still written with `status: incomplete`, the
root failure and last-progress context, and checkpoint lineage. It deliberately
omits the canonical source portfolio, bucket series, aggregate EHE, and trend and
returns a nonzero exit code.

## Repository views

Repository estimates support these presentations:

| View | Contents |
| --- | --- |
| `full` | Canonical lossless estimate report |
| `repository` | Identity, assumptions, verification, totals, counts, diagnostics, and gap total |
| `category` | Repository summary plus every represented effort category |
| `scope` | Repository summary plus project, package, module, and fallback scopes |
| `work-item` | Compact capability groups that reverse only deterministic part slicing |
| `review` | Categories, material scopes, and a bounded queue of material or uncertain capabilities |

The full estimate remains the default for `eh estimate`. `eh report` defaults to a
review-oriented projection. JSON can be compacted by removing insignificant
whitespace; Markdown is already a presentation format and does not accept compact
JSON behavior.

Every projection records the source schema and estimator identity and retains:

- repository identity and source digest;
- profile and contractor baseline;
- low, expected, and high EHE;
- optional rate and replacement cost;
- represented, capability, scope, and gap counts;
- verification and diagnostics; and
- an explicit statement that professionalization gaps are excluded from EHE.

Category and scope totals contain exact low/expected/high aggregation, item and
capability counts, and expected-hour-weighted confidence. Pricing, when present,
uses the same selected rate as the canonical report.

## Capability grouping

The estimator may partition a capability into deterministic work-item parts to
keep expected item size within its model boundary. A compact capability group may
reverse only that presentation split:

- the capability ID is the stable prefix before `:part-NNNN`;
- only the generated ` (part X of Y)` title suffix is removed;
- effort is the exact sum of all parts;
- confidence is the minimum part confidence;
- evidence, assumptions, exclusions, and uncertainty reasons are deduplicated;
- rule, scope, category, profile, and correlation lineage must agree; and
- inconsistent groups fail rather than being merged heuristically.

Independent capabilities never merge merely because they have similar titles or
share a scope.

## Bounded review view

The repository review view contains all category aggregates, at most 20 scopes
ordered by expected effort, and at most 12 unique capability summaries selected
from the largest and lowest-confidence or explicitly uncertain capabilities.
Stable IDs break ties. Omitted counts and expected effort remain visible.

These are presentation bounds, not a token, cost, or model-admission budget. The
unbounded scope and work-item views remain available, and every selected
capability retains an explanation path.

## Explanation

```text
eh explain <repository-or-evidence.json> --item <work-item-or-capability-id>
  [--profile <implementation|recreation>]
  [--format <json|markdown>]
  [--compact]
```

An explanation identifies whether the requested ID resolved to one work item or a
capability group and returns:

- repository, profile, baseline, estimator, and model identity;
- the compact capability summary;
- all matching canonical work items;
- referenced evidence facts available from the supplied evidence bundle;
- unresolved evidence IDs;
- rule IDs and versions; and
- calculation reasons, assumptions, exclusions, correlation groups, and
  uncertainty reasons.

Explanation does not read Git history or emit source excerpts. A caller must
retain repository evidence to expand an evidence ID into its full fact.

Saved Change reports use their separate command and explanation schema:

```text
eh change explain <change-estimate.json> --item <id>
```

That path can expand a Change work item or range-normalization lineage without
reopening the target repository.

## Output and compatibility rules

Canonical machine-readable documents use `canonical-json-document/1.0.0`.
Indented serialization uses LF regardless of the host operating system. JSON
written by the CLI to stdout or an explicit output path ends with exactly one
LF; files use UTF-8 without a byte-order mark. This boundary changes only
insignificant transport whitespace: schemas, property order, values, IDs,
reconciliation, and renderer projections remain unchanged.

- Machine-readable output goes to stdout unless `--output` is explicit.
- Diagnostics go to stderr.
- Output ordering and IDs are deterministic for identical inputs and versions.
- JSON output validates against the checked-in versioned schema.
- A projection must exactly reconcile to its canonical report.
- Cost changes may affect cost fields only; they cannot affect effort, grouping,
  confidence, or review selection.
- Source excerpts, secrets, configured values, and absolute target paths are not
  emitted in ordinary reports.
- Public schema changes require an explicit compatibility decision.

The additive v1 PR verification fields are optional so saved v1 documents from
before their introduction remain valid and deserialize unchanged. Newly generated
PR selections populate comparison provenance, acquisition mode, provider/raw/
represented counts, and count status together. This is an additive observability
extension: estimator identity, effort arithmetic, existing field meaning, compact
JSON compatibility, and non-PR documents are unchanged.

## Model status

Compact output improves reviewability but does not calibrate the estimator or make
an estimate production-ready. The recorded size and usefulness measurements are
in `REPORT_BENCHMARKS.md`. Optional host-AI packets add a stricter digest-bound
protocol around the review projection; see `HOST_REVIEW.md`.
