# Public real Change expansion

`efforthours-change-public-real-expansion/0.1.0` adds six immutable public
open-source pull-request families to the Change EHE calibration boundary: two
.NET, two JavaScript, and two TypeScript. The matrix was committed before
candidate analysis, and the host-AI teacher plan was committed before compilation
or access to candidate hours.

This is preliminary weak supervision. It has one disclosed host-AI teacher and no
independent correction, model fitting, prior change, held-out result, or
production-readiness claim.

## Frozen visible diagnostics

| Partition | Case | Released alpha.2 | Teacher |
|---|---|---:|---:|
| development | BenchmarkDotNet #3211 | 3.25 / 5.75 / 10.50 h | 3.75 / 6.00 / 9.75 h |
| development | p-limit #108 | 6.25 / 11.25 / 21.25 h | 1.50 / 2.75 / 4.25 h |
| development | Zod #6354 | 22.00 / 38.75 / 74.25 h | 6.00 / 10.25 / 16.25 h |
| validation | Spectre.Console #2162 | 6.25 / 11.00 / 21.25 h | 2.75 / 4.50 / 7.25 h |
| validation | Axios #11094 | 13.50 / 23.75 / 45.50 h | 7.00 / 11.50 / 18.75 h |
| test | ofetch #524 | withheld | 2.25 / 4.00 / 6.25 h |

All ranges are low / expected / high Equivalent Human Effort, not actual labor.
The test candidate comparison has not been computed or opened.

| Partition | Records | Teacher expected | Alpha.2 expected | Expected WAPE | Bias |
|---|---:|---:|---:|---:|---:|
| development | 3 | 19.00 h | 55.75 h | 1.9605 | +1.9342 |
| validation | 2 | 16.00 h | 34.75 h | 1.1719 | +1.1719 |
| test | 1 | 4.00 h | withheld | withheld | withheld |

The matrix exposes a structural defect rather than a reusable scale factor.
Repeated category partitions multiply logical work: Zod receives four unit-test
items and four security items, each category totaling 16.00 expected hours,
against teacher targets of 2.25 and 1.25 hours. Axios receives four unit-test
items totaling 14.50 hours against 3.50 hours; p-limit receives two production
items totaling 5.25 hours against 0.50 hour. BenchmarkDotNet's near-equal total is
also cancellation: alpha.2 assigns only 0.25 hour to production versus the
teacher's 2.50 hours while assigning more elsewhere.

Those disagreements must not be collapsed into a blanket ratio. A subsequent
general rule correction should consolidate repeated logical slices, preserve
meaningful marginal implementation, add synthetic guardrails, use a new estimator
version, and compare development and validation only. This corpus itself changes
no estimator prior or rule.

The committed material contains derived evidence, repository-relative paths,
hashes, review reasoning, and ranges. It contains no source checkout, source
excerpt, contributor activity, elapsed-time signal, or actual-labor label. See
[SOURCES.md](SOURCES.md) for provenance, [REPRODUCING.md](REPRODUCING.md) for the
mechanical commands, and [INDEPENDENT_REVIEW.md](INDEPENDENT_REVIEW.md) for the
blind follow-up boundary.
