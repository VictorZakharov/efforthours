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

Manifest author-period report diagnostic `FB5325` contains deterministic,
privacy-safe cache request/hit, unique-key, revisit-miss, byte, eviction, and
declared retention counts for snapshot, immutable file-analysis, inventory, and
Git-blob reuse, including how many incremental inventories used repository-level
batch diffs. It contains no local paths, raw aliases, source excerpts, or
wall-clock values. The CLI writes
nine measured execution-phase durations to stderr after a successful manifest run;
those non-semantic timings are deliberately excluded from JSON/Markdown contracts,
digests, saved-report rendering, and EHE calculations.

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

## Model status

Compact output improves reviewability but does not calibrate the estimator or make
an estimate production-ready. The recorded size and usefulness measurements are
in `REPORT_BENCHMARKS.md`. Optional host-AI packets add a stricter digest-bound
protocol around the review projection; see `HOST_REVIEW.md`.
