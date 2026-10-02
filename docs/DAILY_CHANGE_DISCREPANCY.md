# Daily Change EHE discrepancy investigation

Status: diagnostic baseline and correction scope. This document does not change
the shipped estimator, calendar command, measurement identities, or admission.
The synthetic regressions capture current defects; they are not desired behavior
or numerical effort labels. The private consumer audit is not a public fixture.

## Confirmed mechanisms

`ChangePortfolioGroupNormalizer` normalizes overlapping changes across the whole
selected repository interval. `ChangePortfolioAllocation` distributes that
normalized result to rows, and `ChangePortfolioComparisonBuilder` distributes
the allocated rows to time buckets. Adding an earlier overlapping change can
therefore change a later bucket despite preserving every selected later change.
That bucket is an allocation from a joint interval, not an independent daily
estimate. Existing calendar documentation already describes joint allocation;
consumers expecting independent days need a different calculation identity.

`ChangeWorkItemBuilder.Build` groups repository work items into capabilities. For
positive growth in an existing capability, it replaces `PositiveDifference` with
`ModificationRange`. `ChangeWorkItemRules` caps that logical budget at eight
expected hours. A scope containing many distinct additions can still be one
existing capability. `ChangeWorkItemDecomposition` splits display tasks after
this decision and cannot restore growth already removed from the budget.

The memory-only tests in `ChangeDailyWindowDiscrepancyTests` hold the target day's
canonical input report and count constant while demonstrating window sensitivity.
`ChangeExpansionDiscrepancyTests` supplies an existing production or integration-
test capability, 65 distinct added artifacts, changed normalized evidence, and
a substantial positive repository marginal; the category still receives eight
expected hours. These tests diagnose rule mechanics. They do not establish the
right replacement effort for any real project, test case, or source artifact.

## Three separate measurements

1. **Joint interval allocation:** one normalized interval allocated to dates.
   Membership and interval dependent; retained for existing consumers.
2. **Independent selected-day Change:** independently normalized selected changes
   within each local calendar day and repository, batched through shared immutable
   analysis. Adding other dates must not change that day's low/expected/high
   values, project cells, selected identities, or category totals.
3. **Replacement stock:** full artifact estimates at frozen endpoints. Its signed
   difference is separate from either Change calculation and may be negative.
   A chart requiring exact stock/calendar reconciliation must use these signed
   differences and disclose the stock identity.

A coherent opening-to-closing endpoint Change is also a useful diagnostic. It
must not silently replace an author-selected portfolio: unselected changes and
interleaved contexts can enter an endpoint. Commit-partition sensitivity inside
one day remains a separate model problem even after cross-day allocation is fixed.

## Correction acceptance boundary

- Add an explicitly identified independent-day batch mode with shared bounded
  repository sessions, immutable heads/cutoffs, reusable evidence/checkpoints,
  cancellation, complete-selection failure behavior, and exact day/project sums.
  Preserve the joint mode's identity and compatibility. HTML, text, and JSON must
  disclose which calculation generated the numbers.
- Reconcile overlap and duplicate merge representations within the selected day.
  Adding earlier or later dates cannot change an existing complete day's values.
  Freeze author versus committer selection, merge/coauthor policy, timezone/DST,
  and partial-day handling. A commit authored earlier but committed on the date
  enters a committer-selected day; an author-selected day follows its author date.
  Neither date proves when a person worked.
- Keep replacement stock in its existing native daily snapshot series. Add an
  explicit diagnostic bridge comparing independently selected Change, coherent
  endpoint Change where supported, and signed stock differences at repository
  and category level. Bind all reports to compatible immutable inputs/model
  identities and disclose unmatched selection or scope rather than forcing equality.
- Preserve supported substantial added functionality/test growth within an
  existing broad capability. Use attributable normalized evidence, with existing
  duplicate/generated/mechanical exclusions and diminishing returns. Merely
  splitting display items, files, or commits must not multiply a logical budget.
  A blanket restoration of summed repository work-item differences would revive
  the partition multiplication defect corrected in `change-seed/0.3.0`.
- Freeze source-backed tests with many distinct integration-test files/cases,
  production expansion, duplicate additions, and modified existing artifacts.
  Compare coherent endpoints and alternate commit partitions. Grouping or task
  decomposition alone cannot change represented effort. Include category offsets,
  not only an aggregate that can conceal cancellation.
- Change transparent rules only with a new estimator identity and the correctness
  exception in `CHANGE_MODEL_ADMISSION.md`. Keep frozen reports and priors intact,
  avoid private-case scaling or sealed-test tuning, and retain the experimental,
  uncalibrated boundary. Qualitative safeguards are not empirical admission.
- Consumer migration follows verified producer contracts and invariance checks.
  Do not describe the current joint calendar as independent daily work. No
  clamping, absolute-value conversion, stock scaling, rate adjustment, or isolated
  per-commit summation fixes either diagnosed mechanism.

## Remaining investigation

The private source archives and native receipts were not supplied to this PR.
Its anonymous arithmetic and exact category values have not been independently
reproduced here. The synthetic examples confirm the two producer mechanisms but
do not validate the audit's whole-stock totals or a preferred corrected total.
Implementation, source-backed expansion regressions, representation-sensitivity
checks, the native independent-day contract, stock diagnostics, and consumer
migration remain pending. Keep this PR a draft until that work is complete.
