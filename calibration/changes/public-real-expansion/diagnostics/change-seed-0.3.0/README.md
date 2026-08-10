# `change-seed/0.3.0` structural-correction diagnostic

This directory records a transparent Change-estimator correctness revision against
the frozen `efforthours-change-public-real-expansion/0.1.0` host-AI teacher corpus.
It is a development/validation diagnostic, not a new corpus, independent review,
calibration result, admission decision, or production-readiness claim.

The frozen alpha.2 reports, teacher corpus, review plan, and evaluations remain
unchanged under `0.1.0`. These candidate reports use
`change-seed/0.3.0+seed-rules/0.2.1` and live separately so their provenance cannot
be confused with the material originally reviewed.

## Correction boundary

The revision was expressed without repository-specific identifiers and frozen in
memory-only regressions before these reports were generated:

- repository work-item partitions for one capability/category/path set share one
  Change marginal budget instead of contributing their summed repository prior;
- the budget uses diminishing, bounded logical change units. Each maintained path
  contributes one to four units according to capped edit-region bands, so edit
  fragmentation cannot grow linearly into a full repository capability estimate;
- a capability newly detected on a modified artifact receives the same meaningful
  modification floor instead of inheriting a tiny classification delta; and
- genuinely distinct capabilities added on distinct final paths remain additive.

Repository `seed-rules/0.2.1`, reviewed target ranges, partitions, thresholds, and
dependencies do not change.

## Visible total diagnostics

All ranges are low / expected / high Equivalent Human Effort. They are not actual
labor.

| Partition | Case | Alpha.2 `0.2.0` | `0.3.0` candidate | Teacher |
|---|---|---:|---:|---:|
| development | BenchmarkDotNet #3211 | 3.25 / 5.75 / 10.50 h | 3.50 / 5.75 / 10.75 h | 3.75 / 6.00 / 9.75 h |
| development | p-limit #108 | 6.25 / 11.25 / 21.25 h | 2.75 / 5.00 / 9.50 h | 1.50 / 2.75 / 4.25 h |
| development | Zod #6354 | 22.00 / 38.75 / 74.25 h | 6.00 / 10.00 / 19.00 h | 6.00 / 10.25 / 16.25 h |
| validation | Spectre.Console #2162 | 6.25 / 11.00 / 21.25 h | 2.50 / 4.50 / 8.50 h | 2.75 / 4.50 / 7.25 h |
| validation | Axios #11094 | 13.50 / 23.75 / 45.50 h | 6.50 / 11.25 / 21.50 h | 7.00 / 11.50 / 18.75 h |

| Partition | Teacher expected | Alpha.2 expected / WAPE / bias | `0.3.0` expected / WAPE / bias |
|---|---:|---:|---:|
| development | 19.00 h | 55.75 h / 1.9605 / +1.9342 | 20.75 h / 0.1447 / +0.0921 |
| validation | 16.00 h | 34.75 h / 1.1719 / +1.1719 | 15.75 h / 0.0156 / -0.0156 |

Low/expected/high interval behavior remains mixed. The candidate high totals are
39.25 hours against 30.25 reviewed hours in development and 30.00 against 26.00
in validation. Aggregate expected agreement must not hide category or interval
disagreement.

## Category and lineage effects

| Partition | Category | Teacher expected | Alpha.2 | `0.3.0` |
|---|---|---:|---:|---:|
| development | production implementation | 5.50 h | 6.25 h | 5.00 h |
| development | unit testing | 4.25 h | 21.50 h | 2.75 h |
| development | security and accessibility | 1.25 h | 16.00 h | 2.00 h |
| development | CI/CD and infrastructure | 0.75 h | 1.75 h | 0.75 h |
| validation | production implementation | 4.25 h | 7.00 h | 3.25 h |
| validation | unit testing | 4.75 h | 19.75 h | 4.50 h |

The correction removes the diagnosed multiplication but introduces undershoot in
some visible categories. Change-level comprehension, validation, and review rules
are unchanged, including their existing disagreements.

Because consolidated budgets produce different work-item IDs and counts, exact
lineage matching also changes:

| Partition | Target matches | Source-item reference matches | Candidate-item matches |
|---|---:|---:|---:|
| development alpha.2 | 17 / 17 | 24 / 24 | 24 / 24 |
| development `0.3.0` | 14 / 17 | 14 / 24 | 14 / 17 |
| validation alpha.2 | 12 / 12 | 16 / 16 | 16 / 16 |
| validation `0.3.0` | 10 / 12 | 10 / 16 | 10 / 12 |

Repository/category totals include every candidate item; item-level metrics include
only fully matched targets and therefore disclose rather than conceal this mapping
loss.

## Held-out boundary and reproduction

Only the three development and two validation families have candidate reports in
this directory. No test-family candidate report or test evaluation was generated.

Reproduce each visible report with the immutable selectors in
[`../../REPRODUCING.md`](../../REPRODUCING.md), the source-built `0.3.0` CLI, and
an output path under this directory. Then pass only the three development reports
or only the two validation reports to `calibration change-evaluate` with the
frozen `0.1.0.teacher-corpus.json`. Do not evaluate the test partition.
