# Remaining capability-budget review

## Decision and boundary

Two general attribution defects are corrected: unlocated capabilities borrowing
all changed paths, and broad production source budgets using analyzer-recognized
test-only changes. The remaining numerical discrepancy is **not certified as
reasonable**, and several specialized-rule overlaps remain unresolved. This is a
correctness review, not calibration, admission, or approval to publish a website.

The comparison uses the consumer's frozen October 1 heads, cutoff
`2026-10-01T23:39:39.602Z`, Toronto timezone, implementation profile, committer
selection, first-parent merge policy, and 46 selected changes. No newer source
head was substituted. The released alpha.31 endpoint reports were reproduced
exactly before editing rules. Private manifests, native IDs, source paths, aliases,
object hashes, source content, and audit scripts remain outside tracked files.
Only anonymized aggregates and public synthetic regressions are published here.

Baseline: `change-seed/0.21.1+seed-rules/0.4.0`, portfolio 0.6.1.
Correction: `change-seed/0.21.2+seed-rules/0.4.0`, portfolio 0.6.2, .NET analyzer
0.3.6. Independent-day and selected-final-delta policies remain 1.1.0. This is a
source correction; no new package has been published by this review.

## Same-input results

All six base/head repository seed reports remain byte-identical. The saved stock
operand is therefore unchanged; it is a comparison operand, not a truth label.

| Project | Signed stock | Alpha.31 Change | Corrected Change | Movement |
| --- | ---: | ---: | ---: | ---: |
| A | 301.00 | 355.50 | 290.75 | -64.75 |
| B | 153.25 | 174.00 | 183.75 | +9.75 |
| C | 11.50 | 23.15 | 23.15 | 0.00 |
| Total | 465.75 | 552.65 | 497.65 | -55.00 |

The aggregate excess over saved stock moves from 86.90 to 31.90 hours. Its smaller
size is not an acceptance criterion; opposite project movements remain visible.

| Project | Stock | Growth omitted | Modification excess | Fallback | Change-level | Corrected total |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| A | 301.00 | -76.50 | 26.25 | 23.25 | 16.75 | 290.75 |
| B | 153.25 | -8.50 | 15.25 | 7.75 | 16.00 | 183.75 |
| C | 11.50 | -0.25 | 1.00 | 4.00 | 6.90 | 23.15 |
| Total | 465.75 | -85.25 | 42.50 | 35.00 | 39.65 | 497.65 |

Negative stock credits, removal work, and aggregate role redistribution remain
zero. The exact corrected bridge is
`465.75 - 85.25 + 42.50 + 35.00 + 39.65 = 497.65`.

## Corrected mechanisms

### Global borrowing and incorrect mixed-role allocation

The seven .NET source capabilities in B previously bound to all 137 represented
paths because aggregate structure facts had no locations. That included other
projects, tests, documentation, and build artifacts. A separate build capability
also borrowed the complete set. Its broad use marked every path explained,
concealing maintained-artifact fallback rather than proving its absence.

.NET aggregate facts now retain the exact files already parsed under their owning
project. Nested-project ownership, generated exclusions, measurements, and stable
fact IDs are unchanged. Positive capability growth without a bound represented
path is not charged to unrelated paths; `FB5211` warns without exposing private
lineage. Prefix-based growth binding is removed. Unexplained maintained paths
retain the ordinary fallback, with no new rate or minimum.

The seven B source budgets now bind to 2, 6, 8, 22, 9, 3, and 1 paths respectively.
One distinct added capability now retains its 32-hour marginal instead of receiving
an 8-hour modification budget triggered by unrelated modifications. Existing
source modification alternatives also shrink. Across the .NET source rule,
omitted growth falls from 32.75 to 0.75 and modification excess from 26.25 to 4.25.
Removing the unsupported global build budget eliminates another 8 hours of excess.
The newly exposed fallback is 7.75 hours. These changes explain B's net increase.

Decision: the global attribution/role multiplication was an unsupported heuristic,
with overlapping test/doc/build charges; corrected. The retained distinct added
source scope is legitimate under the existing rule, with uncalibrated magnitude.

### Test-only growth charged as residual production code

A's 64.75-hour polyglot source marginal bound to four changed files. All four have
native ecosystem test facts and separate test budgets. The aggregate source fact
contains both production and tests; the scanner labels these unconventional files
as source. Its source growth therefore had no represented production path after
recognizing the analyzer's test evidence. It cannot justify a second residual
production-construction budget on those tests.

Broad source binding now excludes located .NET, JavaScript, and ecosystem test
facts as well as common test classifications. Existing test budgets remain.
This excludes the 64.75-hour Change charge; it does not rescale stock, rewrite
frozen reports, or change seed rates. The stock model's underlying source/test
classification and aggregate measurement ambiguity remains a separate concern:
the unchanged saved stock operand is not certified free of that ambiguity.

Decision: unsupported production attribution overlapping independently valued test
scope; corrected. This exclusion deliberately increases omitted stock growth.

## Capability rules behind omitted growth and modification excess

Values are before category role redistribution. Each row reverses only native
presentation parts, groups by public seed rule, and includes stock-only rules.
The original and corrected per-capability sums reconcile to the native
`FB5210` aggregate; native category arithmetic also validates against the final
work-item ledger. No private capability identifiers are reproduced.

Omission is a positive magnitude subtracted in the bridge. Excess is the chosen
modification budget above retained growth; it can coexist with omission. Thus a
2-hour marginal replaced by a 2-hour modification budget records both terms,
without losing or adding 2 hours overall.

### Project A

| Rule | Stock | Omitted original / corrected | Excess original / corrected |
| --- | ---: | ---: | ---: |
| `application-entry-point` | 0.50 | 0.50 / 0.50 | 2.00 / 2.00 |
| `background-work` | 1.25 | 1.25 / 1.25 | 11.50 / 11.50 |
| `build-tooling` | 0.00 | 0.00 / 0.00 | 0.50 / 0.50 |
| `ci-infrastructure` | 0.00 | 0.00 / 0.00 | 0.50 / 0.50 |
| `documentation` | 3.75 | 3.75 / 3.75 | 5.50 / 5.50 |
| `external-integration` | 0.25 | 0.25 / 0.25 | 1.25 / 1.25 |
| `integration-tests` | 132.00 | 0.00 / 0.00 | 0.00 / 0.00 |
| `javascript-source-backbone` | 60.25 | 0.00 / 0.00 | 0.00 / 0.00 |
| `manual-validation` | 1.50 | 1.50 / 1.50 | 0.00 / 0.00 |
| `packaging-release` | 0.00 | 0.00 / 0.00 | 0.50 / 0.50 |
| `polyglot-source-backbone` | 66.50 | 0.00 / 64.75 | 0.00 / 0.00 |
| `project-setup` | 1.00 | 1.00 / 1.00 | 0.00 / 0.00 |
| `ui-surface` | 0.50 | 0.50 / 0.50 | 1.25 / 1.25 |
| `unit-tests` | 32.50 | 2.00 / 2.00 | 2.00 / 2.00 |
| `validation-surface` | 1.00 | 1.00 / 1.00 | 1.25 / 1.25 |

### Project B

| Rule | Stock | Omitted original / corrected | Excess original / corrected |
| --- | ---: | ---: | ---: |
| `application-entry-point` | 1.50 | 1.50 / 1.50 | 2.00 / 2.00 |
| `architecture-design` | 1.75 | 0.00 / 0.00 | 0.00 / 0.00 |
| `build-tooling` | 0.50 | 0.50 / 0.50 | 8.00 / 0.00 |
| `documentation` | 3.00 | 3.00 / 3.00 | 8.00 / 8.00 |
| `dotnet-source-backbone` | 90.50 | 32.75 / 0.75 | 26.25 / 4.25 |
| `end-to-end-tests` | 39.50 | 0.00 / 0.00 | 0.00 / 0.00 |
| `manual-validation` | 1.50 | 1.50 / 1.50 | 0.00 / 0.00 |
| `polyglot-source-backbone` | 5.75 | 0.00 / 0.00 | 0.00 / 0.00 |
| `project-setup` | 1.00 | 0.00 / 0.00 | 0.00 / 0.00 |
| `self-review` | 0.75 | 0.75 / 0.75 | 0.00 / 0.00 |
| `solution-coordination` | 0.25 | 0.25 / 0.25 | 0.00 / 0.00 |
| `specification-comprehension` | 0.25 | 0.25 / 0.25 | 0.00 / 0.00 |
| `unit-tests` | 7.00 | 0.00 / 0.00 | 1.00 / 1.00 |

### Project C

| Rule | Stock | Omitted original / corrected | Excess original / corrected |
| --- | ---: | ---: | ---: |
| `documentation` | 0.25 | 0.25 / 0.25 | 1.00 / 1.00 |
| `javascript-source-backbone` | 11.25 | 0.00 / 0.00 | 0.00 / 0.00 |

Retained source/test growth uses supported methods/functions/types or test-unit
growth, or distinct newly added capability evidence. Growth and modification are
alternatives, never summed. Entry points, background behavior, UI, validation,
integration, and documentation use bounded modification alternatives on existing
artifacts; their positive stock growth is not retained as growth. A's 2-hour
JavaScript test marginal has no qualifying test-unit growth and is replaced by an
equal 2-hour modification budget. B's 0.75-hour source marginal similarly lacks
qualifying unit growth and receives a 1.50-hour modification alternative.

Repository comprehension, QA, and review capabilities are explicitly excluded
from Change capability deltas. Existing setup/design/coordination are excluded in
implementation mode; newly added setup/design in B retain their native marginal.
Those are scope decisions, not missing commits. Whether their substitutes conserve
reasonable hours is unresolved.

Modification rules use one to four logical units per represented edit region,
status factors (modified 0.30, removed 0.75, added 1.00), category factors,
diminishing tiers, quarter-hour rounding, a 0.50-hour floor and 8-hour ceiling.
Those mechanics explain why modifications with unchanged functionality inventory
can be positive. They do **not** independently validate each floor or demonstrate
that specialized capabilities on the same body represent distinct coding tasks.

Decision: existing-behavior modification is legitimate scope absent from signed
net growth; the numerical alternatives remain unvalidated heuristics. In
particular A's background-work 11.50, entry-point 2.00, integration 1.25,
validation 1.25, and build/release alternatives require overlap adjudication.
Background and test capabilities share 18 charged paths across two groups;
integration and a test capability share one; build and release share one. Shared
paths alone do not prove identical work, but no disjoint logical-task proof is
available. These overlaps remain unresolved and their charges are not certified.
B's setup/design budgets share one descriptor; setup versus dependency design are
separate rule intentions, with their actual task separation unresolved.

## Maintained-artifact fallback

| Project | Category | Alpha.31 | Corrected | Corrected represented paths |
| --- | --- | ---: | ---: | ---: |
| A | Production | 15.25 | 15.25 | 73 |
| A | Unit testing | 7.00 | 7.00 | 32 |
| A | Integration testing | 0.50 | 0.50 | 1 |
| A | Build/tooling | 0.50 | 0.50 | 1 |
| B | Production | 0.00 | 1.00 | 1 |
| B | Build/tooling | 0.00 | 6.75 | 13 |
| C | Production | 4.00 | 4.00 | 7 |

These are native category/status groups, not repeated per-file minima. Their
charged evidence IDs have zero intersection with charged capability evidence in
each project. Every fallback path is represented; excluded/generated/exact-copy
paths cannot enter these groups. A includes JavaScript/Rust source and testing
support artifacts; C includes TypeScript artifacts. B exposes previously hidden
source and build artifacts when global borrowing is removed.

Decision: distinct maintained-artifact scope at the producer's path ledger level.
There is no observed duplicate **path** charge with a capability. That does not
prove feature-level independence: coarse capability marginals may still cover
supporting edits on other paths. The fallback rate, edit-region bands, and this
partial logical overlap remain unresolved. Removing fallback solely because a
repository's stock grew would lose existing-behavior changes and is unjustified.

## Change-level work and scope substitution

| Project | Comprehension | Validation/debugging | Review/integration | Total |
| --- | ---: | ---: | ---: | ---: |
| A | 2.75 | 6.00 | 8.00 | 16.75 |
| B | 2.00 | 6.00 | 8.00 | 16.00 |
| C | 0.75 | 2.75 | 3.40 | 6.90 |

Original and corrected values are identical. Comprehension is
`min(4, 0.5 + (distinct capability categories - 1) * 0.25)`. Review uses represented
path tiers 0.50/0.25/0.10 with an 8-hour ceiling. Validation uses tiers
0.50/0.20/0.05 with a 6-hour ceiling and quarter-hour rounding. These run once per
coherent final delta, independent of commit count, identity, or timestamps.

Repository-level comprehension, manual QA, and self-review deltas are not charged
again. Test priors intend test authoring; manual validation intends running,
debugging, and hardening. Residual source priors intend construction rather than
complete feature delivery. Those rule intentions establish separate named scope,
but no measured task decomposition proves coding, test authoring, debugging, and
integration numerically disjoint. Path-count caps are not calibration evidence.

Decision: plausible distinct supporting scope, with numerical size and overlap
unresolved. The 39.65 hours cannot be certified reasonable from these static
reports. It must not be removed through a blanket subtraction either.

## Category redistribution

A retains -0.55 production, -1.25 integration, -0.50 packaging; +1.80 unit tests
and +0.50 build. These conserve totals, but the specialized/test overlaps above
remain unresolved. C has no redistribution.

B originally moved -47.23 production, -1.75 architecture, -1.00 setup into +18.74
unit tests, +13.94 end-to-end tests, +14.45 docs, +2.85 build. Global borrowing
caused these source budgets to cite independently valued test/doc/build work.
The correction leaves only -1.75 architecture and -1.00 setup into +2.75 build;
production/test/doc redistribution is zero. Conserving a budget did not make its
original attribution correct.

## Verification and interpretation

Public in-memory regressions cover exact nested-project parser lineage, unchanged
seed reports, schema/semantic validity, unlocated growth refusing unrelated paths,
distinct additions unaffected by unrelated modifications, and analyzer test facts
overriding a conflicting source classification. Existing explicit mixed-role
fixtures retain exact conserved ranges and category sums. Public endpoint, fork,
merge, partial reversal, allocation, and checkpoint tests remain required.

The frozen native rerun passes all 46 selected identities unchanged; all three
original endpoint boundaries proven; daily category/range ledgers equal to direct
endpoint reports; exact expected allocations; October 1 ledger equality when
September 29-30 are added; old-producer receipts invalidated; and a warm repeat
with identical semantic digest and zero snapshot/inventory/blob requests/reads.
All 955 unit tests and 305 end-to-end tests pass. Formatting, locked restore,
build, package creation, schema checks, and file budgets also pass.
Private operational artifacts retain the evidence for these checks.

Calendar should present normalized selected-day Change work; Chart should present
signed whole-snapshot replacement-stock growth. They should disclose both model
identities and selection boundaries. Equality is not an invariant. This review
also does not certify the remaining 31.90-hour difference: specialized overlap,
stock source/test ambiguity, fallback logical coverage, and supporting-work priors
still need independent scope judgments or separately governed calibration.
Website integration/publication remains pending that product decision and a full
historical-calendar validation; this three-day check certifies only its inputs.
