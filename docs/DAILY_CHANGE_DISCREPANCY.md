# Independent daily Change and supported capability growth

The current correction uses `independent-local-day-change/1.1.0`,
`change-portfolio/0.6.1`, and `change-seed/0.21.1+seed-rules/0.4.0`.
They correct two mechanisms diagnosed by the consumer audit without fitting
estimator priors to private numerical totals. Change EHE remains experimental
and uncalibrated. The private source audit is not a committed fixture.

## Daily normalization

Previously all selected changes were reconciled across the entire interval and
then allocated to days. Adding an earlier overlapping change could lower a later
day even though its selected inputs were unchanged. Existing low-level joint
modes retain joint interval allocation; exact endpoint valuation now follows the
separately versioned correction below.

`eh calendar` and low-level `--bucket independent-day` now group selected inputs
by local date and repository before reconciliation. Within-day normalization now
re-estimates exact selected final effects when immutable endpoint inventories prove
their complete selection. Immutable snapshot/static analysis, bounded sessions,
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

Alternative commit partitions with an exact selected endpoint inside one day
must now agree at every range point and category. Across dates, independent-day
normalization deliberately preserves each date's own endpoint context. Unproven
interleaved or branching selections retain conservative normalization and explicit
uncertainty. A new stock/endpoint diagnostic command remains outside this fix;
stock output is neither a clamp nor an estimator target.

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

The pre-correction reproduction in `Alpha29FinalStateDiscrepancyTests` used the
real scanner, Roslyn analyzer, seed
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

### Implemented correction and acceptance boundary

The retained-growth diagnostics are now endpoint-invariance acceptance tests.
`PartialReversalUsesSelectedFinalEndpointDespiteIntermediateExpansion` requires
identical low/expected/high totals and category ledgers for split and unsplit
changes, including a discarded 401-method expansion. A monotone split also agrees
with its direct endpoint. Exact complete reversal remains zero. Separate tests
reject omitted raw effects, broken chains, altered receipt inputs, and cancellation.
The full-context scope test requires unchanged normalization paths to remain
admitted; the narrow projection is retained only as a diagnostic utility.

Canonical Git Change inputs now retain complete admitted owning-scope context.
This explicitly costs more parsing; fixed cache, queue, concurrency, and read-buffer
bounds remain unchanged. No modification/removal prior, growth threshold, role
budget, or stock-total scaling changes.

Author-period execution prepares `selected-final-delta/1.0.0` receipts only after
exact active raw effects match the complete admitted endpoint inventories. The
canonical endpoint category ledger replaces intermediate maxima. Independent days
prove and re-estimate each local date separately; joint intervals can prove one
larger pair. Unselected effects, unsupported modes, links/submodules, broken
chains, or the bounded proof envelope fail closed to conservative normalization.

Source reports retain canonical isolated rows, exact expected allocations, signed
interaction adjustments, and digest/object lineage in `FB5336`. Checkpoints bind
joint versus independent-day policy and preserve the endpoint receipt. The process
regression compares direct and portfolio output with unchanged 400-method context,
then verifies exact warm checkpoint reuse and independent-day policy invalidation.
Old daily v1.0.0 artifacts remain accepted without rewriting frozen records.

The earlier tables describe the pre-correction synthetic behavior, not current
output or calibrated labels. Private Project A/B/C causal attribution and accuracy
remain unverified. The correction follows source-independent final-state invariants;
no private total, frozen teacher label, or sealed partition is a parameter.

Run the public semantic regressions from the repository root:

```text
dotnet test tests/EffortHours.Tests/EffortHours.Tests.csproj --configuration Release --filter "FullyQualifiedName~Alpha29FinalStateDiscrepancyTests|FullyQualifiedName~PositiveNegativeAndRoleBudgetsExplainEndpointVersusSignedStock"
dotnet test tests/EffortHours.EndToEndTests/EffortHours.EndToEndTests.csproj --configuration Release --filter "FullyQualifiedName~AuthorPeriodPartialReversalMatchesEndpointWithCompleteContextAndCheckpointReuse"
```

## Alpha.30 endpoint proof follow-up

The anonymized consumer audit reports that alpha.30 reduced the discrepancy but
proved no selected endpoint. Aggregate values and general ambiguity warnings do
not reveal which raw path or anchor failed. No private source or exact selected
objects are available in this repository, so this follow-up does not certify the
private projects' endpoint proof or category offsets.

Source inspection establishes two narrower proof limitations: timestamps order
raw effects even though dates need not be causal, and only the earliest base and
latest head are considered despite retained forks. A separate raw-effect check
also prevents represented-only rewrite suppression from erasing excluded changes
while claiming exact complete inventory equality.

`Alpha30EndpointGraphTests` and the native
`ForkMergeRetainedSquashAndReversedDatesMatchEndpointAndPreserveDailyCheckpoint`
fixture exercise a selected expansion/partial reversal alongside a separate
branch, a first-parent merge repeating the branch composition, and a retained
squash on another head. Dates deliberately reverse selected ancestry. The final
proof must reproduce direct endpoint low/expected/high categories, exact allocation,
and order invariance. The CLI fixture retains exact selection/merge policy, verifies
warm checkpoint reuse and unchanged source refs/worktree, and extends the date
window without changing the existing day's ledger.

`selected-final-delta/1.1.0` adds the bounded acyclic raw-state proof and selected
boundary search in `CHANGE_PORTFOLIOS.md`. Explicit rejection diagnostics identify
composition, inventory, unsupported-mode, suppression, and anchor-bound failure
without revealing paths. Competing state chains and unselected endpoint effects
remain unproven; no broad pair is substituted solely to reduce a total.

The separate `FB5210` bridge now exposes signed stock, omitted negative credits,
retained/omitted growth, modification excess, removal, role redistribution,
fallback, and change-level work by category. The existing synthetic 16-hour
production growth / 4-hour test-stock decline fixture checks the exact 17-hour
body budget and the equal/opposite 6.86-hour mixed-role redistribution. The bridge
is diagnostic and does not modify any numerical rule.

Consumer verification should rerun the original immutable selection with these
producer identities and inspect `FB5337` on any unproven group and `FB5210` on each
proven endpoint or explicit coherent Change run. This can explain the private
ledger; it does not by itself validate numerical accuracy or require equality
with signed replacement stock. No private total is fitted, clamped, or scaled.
