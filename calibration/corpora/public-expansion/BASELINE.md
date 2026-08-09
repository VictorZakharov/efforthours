# Public expansion seed baseline

## Status

This is the frozen initial comparison for `efforthours-public-expansion/0.1.0`,
measured on 2026-08-06 with `seed-rules/0.2.1` and
`calibration-metrics/1.0.0`. The completed review plan pins
`calibration-review-compiler/0.2.0`.

The labels have maturity `teacher-estimate`: one host-AI teacher reviewed source
and compressed evidence under `ehe-work-item/1.1.0`. Target-level candidate hours
were hidden while labels were formed, although repository totals were observed
during pipeline verification. There has been no independent correction or
adjudication. These measurements are weak-supervision diagnostics, not production
accuracy, historical labor, or ground truth.

The compiler version advances solely to represent explicit reviewed exclusions.
An exclusion is valid only as an exact `0/0/0` range with both rationale and a
`sizeException`; a partially positive zero range is invalid. Positive plans made
for compiler `0.1.0` remain reproducible, while that version intentionally rejects
zero exclusions.

See [`SECOND_REVIEW.md`](SECOND_REVIEW.md) for the independent-review boundary and
handoff procedure.

## Frozen repository-level results

| Partition | Repository | Reviewed expected | Seed expected | Expected error | WAPE | Bias | Reviewed expected inside seed range | Full reviewed range inside seed range |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| development | developit/mitt | 24.75 h | 31.50 h | +6.75 h | 0.2727 | +0.2727 | yes | no |
| validation | Tyrrrz/CliWrap | 191.50 h | 204.00 h | +12.50 h | 0.0653 | +0.0653 | yes | yes |
| test | nanostores/nanostores | 170.50 h | 185.00 h | +14.50 h | 0.0850 | +0.0850 | yes | yes |

Across these three repository observations, reviewed expected effort is 386.75
hours and seed expected effort is 420.50 hours. Absolute and signed error are both
33.75 hours, for repository-observation WAPE and aggregate bias of 0.0873. This
aggregate is descriptive only: three additional repositories and one teacher are
far below the evidence needed for an accuracy claim or learned-model admission.

All 133 reviewed targets remain traceable to their candidate source work items.
The most informative disagreements are category-specific rather than a single
repository-wide scale factor:

- mitt unit testing is 6.00 reviewed versus 10.25 seed hours; nineteen runtime
  cases and compile-time type checks reuse a compact test structure.
- CliWrap production implementation is 90.50 reviewed versus 110.50 seed hours,
  while unit testing is 44.00 reviewed versus 37.75 seed hours and documentation
  is 10.00 reviewed versus 6.75 seed hours.
- Four CliWrap targets are explicit false-positive exclusions: ordinary process
  stream I/O was classified as data persistence, contributing 10.25 seed hours.
- nanostores production implementation is 72.00 reviewed versus 106.50 seed hours,
  while unit testing is 52.00 reviewed versus 40.50 seed hours and documentation
  is 14.00 reviewed versus 7.25 seed hours.
- Two nanostores targets are explicit false-positive exclusions: framework-neutral
  store effects were classified as represented UI, contributing 8.00 seed hours.
  Its development benchmark is also explicitly excluded as a product entry point,
  although that zero candidate did not affect the numerical baseline.

The development seed range contains the reviewed expected total but not the full
reviewed range. Validation and test ranges contain both reviewed expected totals
and complete reviewed ranges. With one observation per partition, this is only a
diagnostic of interval behavior, not calibrated uncertainty evidence.

## 2026-08-08 analyzer-precision reevaluation

The seven explicit exclusions subsequently drove general analyzer corrections,
without changing `seed-rules/0.2.1`, the corpus, any reviewed target, or the original
baseline reports. `.NET` analyzer `0.3.2` qualifies ambiguous execute/query calls
with persistence context. JavaScript analyzer `0.4.1` requires UI-framework context
for state/effect/form-only UI evidence and excludes test or benchmark hashbangs from
product entry-point evidence.

| Partition | Repository | Reviewed expected | Frozen candidate | Corrected-analyzer candidate | WAPE / bias after | Target / candidate mapping after |
|---|---|---:|---:|---:|---:|---:|
| development | developit/mitt | 24.75 h | 31.50 h | 31.50 h | 0.2727 / +0.2727 | 14/14; 14/14 |
| validation | Tyrrrz/CliWrap | 191.50 h | 204.00 h | 192.25 h | 0.0039 / +0.0039 | 63/67; 63/63 |
| test | nanostores/nanostores | 170.50 h | 185.00 h | 169.75 h | 0.0044 / -0.0044 | 48/52; 48/48 |

These after-values are diagnostics, not evidence of generalization: the validation
and test exclusions motivated the corrections, so those observations are no longer
held out for this analyzer family. Four eliminated CliWrap persistence targets and
three eliminated nanostores entry-point/UI targets no longer map. One positive
nanostores manual-validation target also loses its old deterministic source
partition when the false UI work item disappears. Every corrected candidate work
item maps, and the reports preserve the target mismatch rather than interpreting it
as zero effort.

The unchanged 48-case public mutation baseline retains identical numeric totals and
categories and passes all 156 relations. That qualitative guardrail result supports
the classification boundary, but it does not calibrate hours.

## Review method

Expected effort was reasoned at capability level after static inspection of public
source shape, represented behavior, tests, documentation, build and delivery
files, generated or support artifacts, and duplication. It was not produced by a
repository-wide multiplier. Capability totals were then allocated across every
deterministic source partition so that calculation lineage remained complete.

Positive reviewed targets normally remain within 0.5 to 8 expected hours. Bounds
use a documented, rounded risk-tier policy rather than unsupported probability
claims. Exact-zero targets are reserved for independently reasoned exclusions and
must carry an explicit explanation. Static review did not execute target code;
discovered tests are assumed passing, and nanostores' configured 100 percent
coverage is declared-and-assumed rather than measured.

## Reproduction

Analyze the exact release snapshots in [`SOURCES.md`](SOURCES.md), save canonical
effort-only estimates, compile the completed plan, and evaluate one explicit
partition at a time:

```text
eh estimate <snapshot> --no-rate --compact --output <estimate.json>
eh calibration compile 0.1.0.review-plan.json <estimate.json>... --output 0.1.0.corpus.json
eh calibration validate 0.1.0.corpus.json
eh calibration evaluate 0.1.0.corpus.json <matching-estimate.json>... --partition <development|validation|test>
```

The checked-in `baseline-seed-rules-0.2.1-*.json` reports remain the authoritative
frozen outputs for the original checkpoint. The additive
`reevaluation-dotnet-0.3.2-javascript-0.4.1-*.json` reports record the later analyzer
diagnostic without rewriting that baseline.

## Next gate

Do not tune against the validation or test records. Before numerical model
admission, obtain independent corrections, add multiple repository families per
ecosystem and partition, freeze acceptance thresholds, and audit whether explicit
zero labels expose additional general analyzer defects before estimator fitting.
