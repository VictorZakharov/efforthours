# Independent daily Change and supported capability growth

The producer fixes use `independent-local-day-change/1.0.0`,
`change-portfolio/0.4.0`, and `change-seed/0.20.0+seed-rules/0.4.0`.
They correct two mechanisms diagnosed by the consumer audit without fitting
estimator priors to private numerical totals. Change EHE remains experimental
and uncalibrated. The private source audit is not a committed fixture.

## Daily normalization

Previously all selected changes were reconciled across the entire interval and
then allocated to days. Adding an earlier overlapping change could lower a later
day even though its selected inputs were unchanged. Existing low-level joint
modes retain that behavior and identity for compatibility.

`eh calendar` and low-level `--bucket independent-day` now group selected inputs
by local date and repository before reconciliation. Within-day exact/overlap
normalization is unchanged. Immutable snapshot/static analysis, bounded sessions,
and durable checkpoints still run as one batch. Daily category and adjustment
ledgers are retained in JSON, validated against repository and batch totals, and
bound into semantic identity. Day/project low/expected/high values are additive
across dates. Complete no-match dates are zero; failure never exposes an aggregate.
HTML and text disclose the measurement alongside estimator and selection policy.

Calendar defaults to committer dates and prompts for `committer|author` in
interactive mode. `--date-field author` selects author timestamps explicitly.
Merges remain excluded, valid coauthors included, and reachable pinned heads
remain authoritative. Timestamps select evidence; they do not measure work time.
Contributor attribution remains joint within each day. Independent days require
calendar-day buckets and reject isolated contributor normalization.

## Growth in broad capabilities

Previously positive growth in an existing capability always used a bounded
modification range, capped at eight expected hours. Many distinct functions or
test cases could therefore collapse into a small modification budget.

Represented production/test semantic-unit growth now preserves the componentwise
larger of that modification range and the positive normalized repository
capability marginal. These budgets are alternatives. Located semantic facts and
owning-scope aggregate structure facts bind growth to represented changed paths.
File/display partition count alone cannot enable growth. Existing repository
normalization, duplicate/generated exclusions, diminishing returns, and small-task
decomposition remain in force. No private category offset or stock total is a
rule parameter. Other capability categories retain their existing rules.

## Distinct measurements and limits

Independent selected-day Change represents selected normalized deltas. Replacement
stock remains the existing native snapshot daily series with signed differences;
stock growth can be negative. Coherent opening-to-closing Change is a third
calculation and can include unselected interleaved changes. None must be forced
to equal another through scaling, clamping, absolute values, pricing, or per-commit
summation. Daily lineage enables category/project diagnosis without concealing
these different selection and model operands.

Arbitrary alternative commit partitions within a day are not guaranteed to yield
identical selected-delta portfolios. Cross-day invariance and display-partition
invariance are the guarantees of this correction. A new cross-report stock/endpoint
diagnostic command is outside this producer fix; existing stock reports and daily
category lineage remain available separately. The private archives and native
receipts were not supplied, so their exact audit totals are not independently
reproduced or certified here. No frozen admission report or seed prior changes.

Byte-identical bodies added more than once in one final delta now retain one
deterministic represented path; other additions retain exact-duplicate lineage
and zero body effort. This also prevents fallback and validation charges from
rewarding copies of newly added source. Existing copies already present in the
base retain their prior exclusion.

## Alpha.29 final-state regression investigation

The public synthetic investigation starts from the exact alpha.29 producer tree
`c18e1b7aba337caa42746e85cc481457c18336d8`. No private source, pinned selection,
receipt, numerical assessment, or fitted coefficient is used. This record does
not reproduce or explain the numerical totals of the private projects.

After rebasing onto merged PR #239 (`be480fa741d1983cd00e69b6897ca7ab9fe7b459`),
all synthetic measurements below reproduce under `change-portfolio/0.5.0` and
unchanged `change-seed/0.20.0`. Exact composition deduplication does not correct
either partial-reversal retention or projected marginal normalization.

### Retained intermediate growth

`Alpha29FinalStateDiscrepancyTests` uses the real scanner, Roslyn analyzer, seed
estimator, Change estimator, and portfolio reconciler with in-memory C# snapshots.
One maintained file opens with one method and closes with two. The split variant
first expands that file, then removes every extra method except the one retained
at the endpoint. Both commits are selected on the same UTC date; their object
states form an exact chain. There is no interleaved or unselected work, copied
body, ambiguous path, mixed role, or difference in opening/closing artifacts.

Measured expected hours under the unchanged alpha.29 rules:

| Intermediate methods | Expansion Change | Reduction Change | Coherent endpoint | Independent selected day | Signed stock growth |
| --- | ---: | ---: | ---: | ---: | ---: |
| 101 | 25.25 | 2.00 | 2.00 | 25.25 | 0.25 |
| 401 | 79.50 | 2.00 | 2.00 | 79.50 | 0.25 |

The production category alone is 0.50 hours for the endpoint, 23.75 hours for the
101-method split, and 78.00 hours for the 401-method split. The other 1.50 hours
are bounded comprehension, review, and validation. The 101-method joint-mode
control also produces 25.25 hours. Reordering input rows leaves the result
unchanged. A complete reversal to the opening artifact yields zero, including
zero allocations. Serialized independent-day reports pass schema and semantic
validation, and expected allocations reconcile exactly.

The rule-level path is:

1. `ChangeWorkItemBuilder` retains supported positive capability growth instead
   of limiting it to the logical modification budget.
2. `ChangePortfolioIdentity.CategoryContributions` divides each source work item
   across its cited represented paths. Here there is only one source path, so
   allocation weights and category redistribution cannot cause the gap.
3. `ChangePortfolioTopology.FindExactRevertedPaths` removes only exact chains
   whose last state equals their first state. The partial reversal is not one.
4. `ChangePortfolioGroupNormalizer.NormalizeComponent` takes the componentwise
   maximum contribution for each remaining path/category, including exact chains.
   It retains the expansion's production effort despite the removal of almost
   all of that intermediate expansion. Shared categories use their maxima too.
5. Independent-day normalization calls this same normalizer inside the date.
   Moving reconciliation inside the day fixes window sensitivity but does not
   recompute an exact chain's coherent endpoint.

This is structural retention of discarded intermediate work, not merely a
stock-versus-Change measurement difference. The final-state counterexample needs
no private target value: larger discarded intermediate expansions change the
selected-day result while the final artifact stays identical. The maximum rule
predates alpha.29; the supported-growth correction makes its consequence larger.
Source inspection of alpha.28 confirms its positive existing-capability branch
used only the bounded modification range. This record does not claim an executed
alpha.28 numerical comparison.

### Endpoint versus signed stock: a separate budget bridge

`Alpha29CapabilityBudgetBridgeTests` isolates the budget mechanics with transparent
synthetic repository capabilities, independently of the native source fixture.
Production grows from 8 to 24 hours with supported method growth; a test
capability falls from 8 to 4. Both maintained paths are modified. The signed
repository capability difference is `+16 - 4 = +12` hours.

The source-owned variant has a 1-hour production modification alternative. Change
retains the larger 16-hour growth alternative, then charges 1 hour for bounded
removal of the declining test capability. Its production-plus-test budget is
therefore 17 hours, not 12. The 5-hour gap is exactly the absent negative stock
credit (4) plus positive removal work (1). Growth and modification are alternatives,
not stacked budgets. No fallback body work is charged.

A mixed-role variant binds the same growing capability to both source and test
paths. Its modification alternative is 1.50 hours, still below the 16-hour growth
budget. Equal four-unit path bands and role weights of 1.00/0.75 partition the
16 hours into 9.14 production and 6.86 tests. Adding the separate 1-hour removal
gives 9.14 production and 7.86 tests, conserving the same 17-hour body total.
Category differences can therefore include redistribution from other capability
categories; comparing one category's stock difference with its Change value does
not isolate that category's original capability marginal.

The native endpoint's 2.00-versus-0.25 gap is also distinct from retained history:
its 0.25-hour positive source marginal receives the 0.50-hour logical modification
floor, followed by 1.50 hours of bounded change-level work. Neither controlled
fixture proves the private coherent-endpoint production discrepancy has this
particular composition. A complete private bridge would need the capability
ledger, signed marginals, selected alternative budgets, role partitions, fallback
items, and change-level items, without using private totals as rule parameters.

### Changed-scope normalization is another independent operand

`ChangedScopeResetsMarginalNormalizationAgainstUnchangedContext` keeps a separate
400-method source file unchanged while the changed file grows from one to 101
methods. It uses the production `ChangeAnalysisScope.CreateForFiles` projection
and `ScopedRepositoryFileSystem` in memory, followed by the ordinary scanner and
seed estimator. The unchanged file is outside the selected source projection.
Full-snapshot production grows by 13.75 hours; the projected production grows by
23.75 hours for the identical changed body. No negative capability difference,
role redistribution, portfolio maximum, or intermediate history is needed.

The growth rule inherits the positive marginal of its input repository estimates.
A changed-scope estimate is a projection, not the full replacement stock. Seed
normalization and diminishing returns run inside that projection, so excluded
unchanged context can change the marginal rate. The same effect can differ
between individual commit scopes and a coherent endpoint's combined scope.
Consequently, a production-category gap must first separate full-versus-projected
capability marginals before attributing the rest to modification budgets or role
redistribution. Naming that projected difference a repository marginal does not
prove it retained the full repository's normalization context.

### Correction acceptance boundary

These tests characterize the defect and budget distinctions; passing them is not
acceptance of retained intermediate work. No estimator, prior, public schema,
policy identity, admission record, or shipped estimate changes in this investigation.
A correction should freeze and implement at least these relations:

- A fully selected exact object chain with no interleaving must value its coherent
  opening-to-closing represented delta. Partial reversals and monotone same-path
  growth must be compared with an unsplit equivalent at every range point.
- Discarded intermediate expansion must not raise that final value. Complete
  reversals, reintroductions, generated/duplicate exclusions, and distinct final
  capabilities still need their existing safety and additivity coverage.
- Independent local-day window invariance and exact category/project/contributor
  accounting must remain intact. Work crossing a day boundary requires an explicit
  policy; it must not silently change an earlier day's estimate.
- Interleaved, branching, or incompletely selected chains need explicit uncertainty
  and a defined conservative fallback. A global first-to-last range could include
  unselected work and is not a safe blanket replacement for portfolio normalization.
- Changed-scope growth must retain or explicitly account for unchanged owning-scope
  normalization context rather than restarting its marginal tiers. A correction
  must preserve bounded read-only analysis and distinguish full and projected
  operands in lineage; it cannot silently substitute a full scan for every row.
- Capability growth remains evidence-bound and uses the existing seed priors.
  Stock remains signed replacement effort; it is not a clamp, target, or scaling
  factor for Change. Category role partitions must conserve their original budgets.
- A behavior correction needs new versioned identities, governing contract updates,
  schema-valid reports, lineage, and bounded offline execution. The diagnostic
  assertions for retained overcount must then become final-state acceptance tests.

Private Project A/B/C causal attribution and numerical agreement remain unverified.
Cross-day invariance, exact accounting, and this synthetic mechanism diagnosis do
not establish calibration, accuracy, or production readiness.

Reproduce the diagnostic cases from the repository root:

```text
dotnet test tests/EffortHours.Tests/EffortHours.Tests.csproj --configuration Release --filter "FullyQualifiedName~Alpha29FinalStateDiscrepancyTests|FullyQualifiedName~PositiveNegativeAndRoleBudgetsExplainEndpointVersusSignedStock" --logger "console;verbosity=detailed"
```
