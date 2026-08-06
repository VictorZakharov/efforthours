# Public pilot seed baseline

## Status

This is the frozen initial comparison for `fairbill-public-pilot/0.1.0`, measured
on 2026-08-06 with `seed-rules/0.2.0` and
`calibration-metrics/1.0.0`. The completed review plan pins
`calibration-review-compiler/0.1.0`.

The labels have maturity `teacher-estimate`: one host-AI teacher reviewed source
and compressed evidence under `ehe-work-item/1.0.0`. There has been no independent
correction or adjudication. The candidate estimate was visible during review, so
the records may contain anchoring effects. These measurements are weak-supervision
diagnostics, not production accuracy, historical labor, or ground truth.

## Frozen repository-level results

| Partition | Repository | Reviewed expected | Seed expected | Expected error | WAPE | Bias | Reviewed expected inside seed range | Full reviewed range inside seed range |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| development | ardalis/GuardClauses | 118.50 h | 234.00 h | +115.50 h | 0.9747 | +0.9747 | no | no |
| validation | sindresorhus/p-queue | 167.00 h | 356.25 h | +189.25 h | 1.1332 | +1.1332 | yes | no |
| test | KristofferStrube/Blazor.FileSystemAccess | 116.00 h | 215.50 h | +99.50 h | 0.8578 | +0.8578 | yes | no |

Across these three repository observations, reviewed expected effort is 401.50
hours and seed expected effort is 805.75 hours. Absolute and signed error are both
404.25 hours, for repository-observation WAPE and aggregate bias of 1.0068. This
aggregate is descriptive only: three repositories and one teacher are far below
the evidence needed for an accuracy claim or learned-model admission.

All 99 reviewed targets matched, and all 228 candidate source work items remained
traceable. The largest category disagreements explain most of the overestimate:

- GuardClauses unit testing: 52.00 reviewed versus 157.75 seed hours. The suite is
  extensive, but repeated guard/type/boundary cases share strong authoring patterns.
- p-queue production implementation: 60.00 reviewed versus 285.25 seed hours. The
  TypeScript token-backed source backbone overvalues about 1,300 maintained source
  lines, while unit testing is low at 47.25 seed versus 65.00 reviewed hours.
- Blazor.FileSystemAccess UI: 41.00 reviewed versus 119.00 seed hours. The server
  and WebAssembly samples duplicate most pages; equivalent recreation should not
  reward copied behavior twice.

The seed ranges are broad enough to contain the reviewed expected total for the
validation and test records, but none fully contains the reviewed low/high range.
That combination—large point bias with broad intervals—is not acceptable evidence
of calibrated uncertainty.

## Review method

Expected hours were reasoned at capability level after inspecting public source
shape, behavior, tests, documentation, build/delivery files, and duplication. They
were not copied from seed hours or produced by applying a repository-wide
multiplier. Every capability was then split into explicit 0.5-to-8-hour reviewed
targets before totals were reconciled.

For consistency in this first teacher pass, low/high bounds were rounded to the
nearest quarter hour from one of three disclosed risk tiers:

- routine: 75% / 135% of expected;
- normal: 65% / 150% of expected; and
- high: 55% / 175% of expected.

These are review bounds, not probability quantiles. Later independent review and
held-out calibration may replace them.

## Reproduction

Analyze the exact revisions in [`SOURCES.md`](SOURCES.md), save canonical effort-only
estimates, compile the completed plan, and evaluate one explicit partition at a
time:

```text
fairbill estimate <snapshot> --no-rate --compact --output <estimate.json>
fairbill calibration compile 0.1.0.review-plan.json <estimate.json>... --output 0.1.0.corpus.json
fairbill calibration validate 0.1.0.corpus.json
fairbill calibration evaluate 0.1.0.corpus.json <matching-estimate.json> --partition <development|validation|test>
```

The checked-in `baseline-seed-rules-0.2.0-*.json` reports are the authoritative
schema-versioned outputs for this checkpoint.

## Known diagnostic excluded from the corpus

`canhorn/EventHorizon.Blazor.TypeScript.Interop.Generator` at
`854b34f952ffdadde34f0a735f8c84b30ff7996a` was considered as a mixed repository,
but its seed estimate exposed a classification failure: TypeScript fixtures and
declaration-like content inflated a JavaScript/TypeScript production backbone to
about 2,006.5 expected hours and the repository to 2,735.5 hours. Freezing that
record now would measure a known analyzer defect more than ordinary estimator
agreement. It is excluded from every partition and should become a targeted
analyzer/mutation fixture before reconsideration.

## Next gate

Do not tune against the test record. Before calling Milestone 7B complete or trying
local ML, add independently corrected labels, more repository families per
ecosystem and partition, synthetic mutation families, and frozen numerical model
admission thresholds.
