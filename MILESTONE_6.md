# Milestone 6: Reporting and Agent Usability

## Status

Implemented on August 5, 2026. This milestone adds lossless drill-down paths and
compact projections around the existing estimate contract. It does not calibrate
the seed priors or claim that the resulting estimates are production-ready.

## Objective

Make a completed estimate inexpensive for a human or host AI to review without
requiring the full repository or the repetitive sliced work-item ledger in the
ordinary case.

Milestone 6 must provide:

- deterministic repository, category, scope, capability, and review views;
- an explanation query that expands any compact capability back to its work items,
  evidence facts, rule lineage, assumptions, exclusions, and uncertainty;
- a documented and separately versioned 2026 US senior-contractor rate card;
- readable Markdown and compact machine-readable JSON;
- measured output sizes and an initial usefulness review; and
- compatibility with the complete v1 estimate report.

## Compatibility decisions

The existing `EstimateReport` remains the canonical, lossless estimate contract.
The existing command below continues to emit that contract:

```text
fairbill estimate <repository-or-evidence.json> --format json
```

New report views are projections. They do not change hours, confidence, evidence,
or pricing. A projection always names the source estimate schema and estimator
version so it cannot be confused with the canonical report.

Milestone 6 adds optional v1 schemas rather than changing required properties in
the existing estimate, work-item, evidence, or rate-card schemas.

The default rate card does change ordinary CLI pricing behavior: an estimate uses
the bundled default rate unless `--no-rate` is supplied. Callers can continue to
provide an exact `--hourly-rate`; an override never changes EHE.

## CLI surface

The target command shape is:

```text
fairbill estimate <repository-or-evidence.json>
  [--profile <implementation|recreation>]
  [--view <full|repository|category|scope|work-item|review>]
  [--format <json|markdown>]
  [--compact]
  [--no-rate | --hourly-rate <amount> [--currency <code>]]

fairbill report <estimate.json>
  [--view <full|repository|category|scope|work-item|review>]
  [--format <json|markdown>]
  [--compact]

fairbill explain <repository-or-evidence.json>
  --item <work-item-or-capability-id>
  [--profile <implementation|recreation>]
  [--format <json|markdown>]
  [--compact]

fairbill rate info
fairbill rate show
```

`estimate` defaults to the full view for compatibility. `report` defaults to the
review view because its purpose is compression. `--compact` removes insignificant
JSON whitespace and is rejected with Markdown rather than being silently ignored.

`explain` accepts a repository directory or repository-evidence JSON so it can
return the actual evidence facts. It does not execute target code. A caller with
only a saved estimate can still inspect the full work-item lineage in that estimate,
but must retain evidence JSON to expand evidence IDs into facts.

## Projection contract

A single versioned estimate-view contract supports five projections:

1. `repository` contains identity, assumptions, verification, totals, counts,
   diagnostics, and the separate gap total.
2. `category` adds one aggregate per represented effort category.
3. `scope` adds one aggregate per project, package, module, or fallback source
   scope.
4. `work-item` adds compact capability groups rather than repeating every sliced
   0.5-to-8-hour part.
5. `review` combines category totals, the most material scopes, and a bounded
   review queue for a host AI.

All projections retain:

- repository identity and source digest;
- profile and contractor baseline;
- estimator version;
- EHE and optional replacement-cost totals;
- default or caller-supplied rate-card identity;
- represented, capability, scope, and gap counts;
- verification state;
- diagnostics; and
- an explicit statement that gap work is excluded.

Category and scope aggregates include low/expected/high hours, represented item and
capability counts, and expected-hour-weighted confidence. Cost, when present, is
derived from the same fixed hourly rate as the canonical report.

### Capability grouping

The seed estimator partitions large capabilities into small work-item parts. A
compact report reverses only that presentation split:

- the capability ID is the stable work-item prefix before `:part-NNNN`;
- titles remove only the deterministic ` (part X of Y)` suffix;
- effort is the exact sum of the parts;
- confidence is the minimum part confidence;
- evidence IDs, assumptions, exclusions, and uncertainty reasons are deduplicated;
- rule, scope, category, profile, and correlation lineage must agree across parts;
  and
- inconsistent groups fail loudly rather than being merged heuristically.

This grouping reduces repetition without discarding the underlying ledger. It does
not merge independent capabilities merely because they share a title or scope.

### Bounded review view

The review view is optimized for an AI or human deciding where deeper inspection
is worthwhile. It contains all category aggregates, at most 20 scopes ordered by
expected effort, and at most 12 unique capability summaries selected from:

- the six largest expected-effort capabilities; and
- the six lowest-confidence or explicitly uncertain capabilities.

Stable IDs break ties. Counts and omitted expected effort disclose truncation. The
unbounded `scope` and `work-item` views remain available, and every compact
capability can be passed to `explain`.

## Explanation contract

An explanation contains:

- the requested ID and whether it resolved to one work item or a capability group;
- repository, profile, estimator, and model identity;
- the compact capability summary;
- every canonical work item in the match;
- every referenced evidence fact available in the supplied evidence bundle;
- any unresolved evidence IDs;
- rule IDs and versions;
- calculation reasons, assumptions, exclusions, correlation groups, and uncertainty
  reasons; and
- the static verification warning.

JSON output is schema-validated. Markdown presents the same lineage in tables and
short lists. Explanation never includes source excerpts or reads Git history.

## Default 2026 US contractor rate

### Result

The initial bundled rate card is:

| Point | USD/hour |
| --- | ---: |
| Low market reference | 125 |
| Default expected rate | 160 |
| High market reference | 200 |

It models a nationwide US independent senior software contractor. It is a
replacement-cost input, not a claim about the rate paid to a particular developer,
an employment wage, or a legally prescribed rate.

### Public-data derivation

The latest available US Bureau of Labor Statistics Occupational Employment and
Wage Statistics release is May 2025, published May 15, 2026. For Software
Developers, SOC 15-1252, its public API reports:

| OEWS point | Series | Wage |
| --- | --- | ---: |
| Median | `OEUN000000000000015125208` | $65.38/hour |
| 75th percentile | `OEUN000000000000015125209` | $82.68/hour |
| 90th percentile | `OEUN000000000000015125210` | $103.21/hour |

OEWS excludes self-employed people, so these are wage anchors rather than contractor
bill rates. Fairbill converts them transparently instead of claiming that BLS
measures independent-contractor prices.

The BLS Employer Costs for Employee Compensation table for March 2026, published
June 12, 2026, reports $72.99 total compensation and $50.53 wages and salaries per
hour for private-industry professional and related occupations. Their ratio is
`1.44448842`.

Fairbill then applies an explicit 75% billable-utilization assumption to cover
ordinary independent-contractor nonbillable time such as business development,
administration, leave, and bench time. This is a Fairbill policy assumption, not a
BLS measurement.

```text
raw bill rate = OEWS wage x (72.99 / 50.53) / 0.75
published rate = raw bill rate rounded to the nearest $5/hour
```

This yields $125.9209, $159.2404, and $198.7809 before rounding. The 75th-percentile
point becomes the $160 default because the modeled worker is a competent senior
contractor rather than the median software developer.

Primary sources:

- https://www.bls.gov/oes/tables.htm
- https://download.bls.gov/pub/time.series/oe/oe.txt
- https://www.bls.gov/news.release/ecec.t04.htm
- https://www.bls.gov/bls/linksite.htm

BLS states that its published material is public domain, apart from previously
copyrighted photographs and illustrations. Fairbill stores only the cited numeric
observations, series IDs, formula, and provenance; it does not redistribute BLS
branding or bulk data.

### Versioning and override behavior

The derivation is stored in a checked-in JSON artifact under
`rates/us-senior-contractor/` and embedded in a reusable pricing assembly. The
artifact has its own schema, semantic version, effective date, source release dates,
formula inputs, assumptions, and digest.

The existing v1 `RateCard` remains the report-facing contract. The detailed bundled
artifact is mapped into that contract. `rate info` emits concise metadata and
`rate show` emits the complete artifact.

`--hourly-rate` creates a caller-supplied rate card and may use a caller-supplied
three-letter currency. No currency conversion occurs. `--currency` without
`--hourly-rate`, or any rate option combined with `--no-rate`, is a usage error.

`TotalCost` continues to mean EHE low/expected/high multiplied by one selected
hourly rate. The market range is disclosed on the rate card but is not silently
cross-multiplied with effort uncertainty.

## Output-size measurement

`REPORT_BENCHMARKS.md` records the completed measurements for Fairbill itself and
small .NET, JavaScript/TypeScript, and mixed fixtures:

- UTF-8 bytes;
- Unicode characters;
- line count;
- a clearly labeled `ceiling(characters / 4)` token approximation;
- projection-to-full-report size ratio; and
- whether a reviewer can identify totals, dominant categories/scopes, gaps,
  warnings, low-confidence capabilities, and explanation IDs without source access.

The four-character approximation is used only as a provider-neutral size indicator.
It is not presented as an exact token count for any model. Exact tokenizer and
provider-cost measurements remain part of host-AI integration after representative
models and workflows are selected. On the Fairbill snapshot, review JSON was 7.4%
of compact full JSON and review Markdown was 3.6%; the detailed table, method, and
usefulness findings are in `REPORT_BENCHMARKS.md`.

## Testing requirements

Ordinary unit tests remain entirely memory-backed. Milestone 6 tests must cover:

- deterministic projections and capability IDs;
- aggregate totals exactly matching the canonical report;
- full, category, scope, work-item, and bounded review views;
- stable truncation counts and omitted effort;
- explanation expansion from in-memory evidence;
- missing and ambiguous explanation IDs;
- JSON Schema validity for every new output and model artifact;
- compact JSON round-tripping and size reduction;
- default rate, no-rate, and caller override behavior;
- rate changes affecting cost but never effort or view grouping;
- rate formula reproduction from the checked-in observations;
- Markdown readability and gap-exclusion labels; and
- CLI exit codes and stdout/stderr separation.

Physical files and subprocesses remain confined to `Fairbill.EndToEndTests` and
explicit benchmarks. `Fairbill.Tests` must not read or write temporary files,
reports, rate artifacts, or fixture trees.

## Exit criteria

Milestone 6 is complete when:

- all five projections are deterministic and schema-valid;
- compact capability summaries can be expanded without losing lineage;
- the review projection is materially smaller than the full estimate on Fairbill;
- a documented usefulness review and output-size table are checked in;
- the dated default rate is reproducible from public inputs and independently
  replaceable from EHE;
- full JSON compatibility and offline behavior are preserved;
- unit tests remain filesystem-free; and
- the CLI, package, and documentation expose the new workflow coherently.

## Completion evidence

The completed implementation passed the locked restore, formatting verification,
zero-warning Release build, 64 memory-only unit tests, and 14 disk-backed CLI
end-to-end cases. The `Fairbill.Tool` `0.6.0-alpha.1` package was built, inspected
for the pricing assembly and rate artifact, installed into an isolated local tool
path, and smoke-tested through `version`, `rate info`, and a compact review
estimate. `REPORT_BENCHMARKS.md` supplies the required output-size and usefulness
evidence.

## Deferred incremental-change mode

Milestone 6 reports the current repository as a whole. `CHANGE_ESTIMATION.md`
defines the deferred expansion for final snapshots, pull requests, commits,
revision ranges, and author-and-period portfolios. The core design will be
provider-neutral; an optional `gh` CLI adapter may resolve GitHub pull-request
inputs when available. Author and time can select an explicitly requested
portfolio, but they and other history metadata never become effort multipliers.
