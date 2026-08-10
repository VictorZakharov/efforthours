# Seed-model review records

This file records reviewed repository-level anchors for EffortHours's transparent seed
model. A record is evidence for later calibration work; it is not itself a
calibration result, a benchmark of actual historical hours, or permission to tune
the model until one repository looks desirable.

Each record must identify the analyzed source revision, evidence digest, estimator
artifact, profiles, review method, and unresolved concerns. Repository-level
partition isolation is now defined by `MILESTONE_7.md` and enforced by the v1
calibration-corpus contract. Existing prose anchors do not become calibration
records automatically: they must be transcribed at work-item granularity, reviewed
under a versioned rubric, assigned to a repository-owned partition, and supplied
with the required source/license/distribution provenance. Revisions are retained
only for reproducibility and never become effort signals.

The first records that satisfy that boundary live in
`calibration/corpora/public-pilot/0.1.0.corpus.json` and
`calibration/corpora/public-expansion/0.1.0.corpus.json`; their source manifests and
frozen seed measurements are documented beside each corpus. They have
`teacher-estimate` maturity only and must not be conflated with the provisional
anchors below or described as independently validated. Blind second-review
handoffs and exact-digest compilers exist, but no completed independent plan; the
record maturity therefore remains unchanged.

Because EffortHours's own anchor informed the seed-model and calibration design, it is
a development diagnostic rather than an eligible held-out test record unless a
future independent snapshot and review policy explicitly establishes otherwise.

## 2026-08-10: `efforthours-change-public-real-expansion/0.1.0`

Status: **blind preliminary real-source host-AI teacher labels; not independently reviewed**

Six immutable MIT-licensed pull requests from six new repository families were
frozen across .NET, JavaScript, and TypeScript before candidate analysis. The
3/2/1 development/validation/test assignment, exact commits and trees, unchanged
license blobs, and forbidden-signal policy were committed first. Released
`EffortHours.Tool` `0.9.0-alpha.2` then wrote
`change-seed/0.2.0+seed-rules/0.2.1` reports without displaying numeric content.

One disclosed host-AI teacher used blind authoring packets, public final
specifications, immutable deltas, and bounded adjacent source to author 34 logical
targets. Contributor identity, activity, elapsed time, actual labor, commit count,
intermediate churn, candidate hours, and candidate category totals did not inform
the judgment. The teacher plan was committed before compilation or evaluation.

Development compares 19.00 teacher expected hours with 55.75 candidate hours
(WAPE 1.9605, bias +1.9342); validation compares 16.00 with 34.75 (WAPE and bias
1.1719). All target and candidate-item references match. Repeated partitions are
the dominant defect: Zod emits four security and four unit-test items totaling
16.00 expected hours per category, Axios emits four unit-test items totaling
14.50, and p-limit emits two production items totaling 5.25. Their corresponding
teacher targets are 1.25, 2.25, 3.50, and 0.50 hours. BenchmarkDotNet's 5.75 versus
6.00 total masks material category cancellation.

This is evidence for a general double-counting correction, not a fitted scale
factor or accuracy claim. No rule, prior, threshold, review maturity, or ML
dependency changed, and the ofetch test comparison remains unopened. The blind
34-target handoff pins corpus digest
`sha256:a60aed52d78368cad69fc39bb7fa399a255dbf237f7739bf78dfd55356c96c7c`.

## 2026-08-10: `efforthours-change-public-real-pilot/0.1.0`

Status: **preliminary real-source host-AI teacher label; not independently reviewed**

One immutable MIT-licensed GuardClauses pull request was selected from a repository
family already assigned to development. Released `EffortHours.Tool`
`0.9.0-alpha.2` analyzed exact local base/head objects with its remote disabled,
using `change-seed/0.2.0+seed-rules/0.2.1`. The public final specification,
normalized delta, and bounded adjacent source informed the teacher judgment;
contributor identity, activity, elapsed time, commit count, and intermediate churn
did not.

The released candidate range is `1.75/4.25/6.75` hours. One disclosed host-AI
teacher separately reasoned five targets totaling `2.25/4.00/6.25` hours, then
froze the plan before exact-digest compilation and evaluation. Expected absolute
error is 0.25 hour and expected WAPE is 0.0625. Candidate guidance was visible, so
this is weak supervision rather than blind review.

The record proves that the public-source provenance and current-estimator workflow
operate on a real final delta. It is not actual labor, independent correction,
validation evidence, or held-out accuracy. No rule, prior, threshold, review
maturity, or ML dependency changed. The blind five-target handoff pins corpus
digest
`sha256:73966db241d7c272b11ad02e3ca87cf1433ef5213809a347648889452374d28a`.

## 2026-08-09: measured-coverage evidence admission

Status: **qualitative evidence correction; no prior calibration**

The common analyzer now parses checked-in LCOV and Cobertura reports into
digest-verified `measured` line, branch, and function evidence. Covered source
paths are matched privately to maintained production project/package scopes and
are not copied into emitted evidence, diagnostics, or estimates. Public fixtures
use only synthetic repository-relative paths. Changed, ambiguous, unmatched,
malformed, unsupported, unsafe, or oversized artifacts are not valued. EffortHours
still does not execute tests or prove that a report belongs to the analyzed source
snapshot.

When measured and `declared-assumed` percentages apply to the same scope, the
measured facts alone feed the existing `coverage-achievement` rule. The declaration
remains visible in repository evidence but is neither averaged nor double-counted.
No value in `models/seed-rules/0.2.1.json`, reviewed target, corpus partition, or
review maturity changed.

Synthetic suite `efforthours-public-synthetic-mutations/0.4.0` expands the prior
48-case/156-relation checkpoint to 51 cases and 170 passing relations. Measured 80%
and 100% cases move unit-test low/expected/high effort directionally while leaving
production unchanged; a measured 80% report plus a conflicting declared 100%
threshold remains identical to measured 80% alone at every repository-total and
unit-test range point. These are qualitative safeguards, not reviewed effort
labels or an accuracy result.

## 2026-08-08: analyzer precision from reviewed exclusions

Status: **qualitative analyzer correction; no prior calibration**

The seven exact-zero exclusions in `efforthours-public-expansion/0.1.0` exposed
three general classification defects. `.NET` analyzer `0.3.2` now qualifies
ambiguous execute/query calls with persistence context. JavaScript analyzer `0.4.1`
requires UI-framework context for state/effect/form-only UI evidence and excludes
test or benchmark hashbang scripts from product entry points. Positive database,
React-state, and CLI-entry-point boundaries remain covered by memory-only tests.

No `seed-rules/0.2.1` prior, normalization rule, reviewed target, or corpus
partition changed. All 156 public mutation assertions remain green and all 48
canonical mutation estimates retain their prior numerical totals and categories.

| Partition | Repository | Reviewed expected | Before | After | Repository WAPE / bias after | Target / candidate mapping after |
|---|---|---:|---:|---:|---:|---:|
| development | developit/mitt | 24.75 h | 31.50 h | 31.50 h | 0.2727 / +0.2727 | 14/14; 14/14 |
| validation | Tyrrrz/CliWrap | 191.50 h | 204.00 h | 192.25 h | 0.0039 / +0.0039 | 63/67; 63/63 |
| test | nanostores/nanostores | 170.50 h | 185.00 h | 169.75 h | 0.0044 / -0.0044 | 48/52; 48/48 |

The validation and test labels directly motivated these rule corrections. Their
after-values therefore cannot be used as held-out accuracy or calibration evidence,
and the test observation is now contaminated for this analyzer family. Four
CliWrap zero targets and three nanostores zero targets intentionally lose mappings
when their false evidence disappears. A positive nanostores manual-validation
target also loses its old source partition after the false UI work item is removed;
all 48 current candidate work items still map. The checked-in reevaluation reports
retain this structural mismatch instead of scoring eliminated targets as zero.

## 2026-08-06: `efforthours-change-public-synthetic/0.1.0`

Status: **preliminary host-AI teacher labels; not independently reviewed**

`change-ehe-work-item/1.0.0` now defines logical review of normalized immutable
final deltas. The existing corpus and independent-review machinery carries an
optional Change reference with base/head objects, evidence digests, a derived
final-delta digest, and non-valuing coverage tags. Scaffold, exact-provenance
compile, validation, blind second review, and held-out evaluation are executable
without changing `change-seed/0.1.0` or adding ML.

Twenty-four synthetic case identities across eight repository families were
assigned to development, validation, and test before numerical review. The
MIT-licensed `0.1.0.fixtures.json` suite now deterministically reproduces 24
effort-only source reports and 24 blind authoring packets with
`change-fixture-generator/0.1.0`; the suite digest is
`sha256:3e1788edea45616613baea8876de8f2336c3061135bb1c7f77dfc5707fba49a5`.
One disclosed host-AI teacher then authored category budgets from the final
synthetic behavior. Candidate totals had already been seen during invariant
verification, so the 121 compiled targets are weak supervision rather than a blind
label claim. Twenty-two targets are explicit `0/0/0` false-positive or duplicate
exclusions with retained lineage. No prior was changed.

| Partition | Records | Reviewed expected | Seed expected | WAPE / bias |
|---|---:|---:|---:|---:|
| development | 12 | 38.50 h | 41.25 h | 0.3701 / 0.0714 |
| validation | 6 | 26.00 h | 27.50 h | 0.0962 / 0.0577 |
| test | 6 | 41.75 h | not evaluated | deliberately withheld |

Development and validation metrics are diagnostic error scales only. The test
comparison was not run because no independently reviewed corpus, numerical
thresholds, or frozen release candidate exists. The blind 121-target handoff pins
source-corpus digest
`sha256:ecfdb867ed2ba4912c9550277fc050b5e5511d0e15a107c8a08c044f61793c10`.
Metric identity and candidate selection order are frozen, while numerical
thresholds remain blocked on realistic independently reviewed error scales.

## 2026-08-06: `efforthours-public-expansion/0.1.0`

Status: **preliminary host-AI teacher labels; not independently reviewed**

Three additional public repository families were frozen before numerical review
from immutable MIT-licensed release archives. No commit history, contributor data,
churn, timestamps, or actual labor records were inspected. A single host-AI teacher
reviewed blind target-level packets under `ehe-work-item/1.1.0`; repository totals
were visible during pipeline verification. The source shapes, archive hashes,
license hashes, source digests, and fixed partitions are recorded in
`calibration/corpora/public-expansion/SOURCES.md`.

| Partition | Repository release | Source estimate | Reviewed expected | Seed expected | WAPE / bias |
|---|---|---|---:|---:|---:|
| development | developit/mitt `3.0.1` | `seed-rules/0.2.1` | 24.75 h | 31.50 h | 0.2727 |
| validation | Tyrrrz/CliWrap `3.10.4` | `seed-rules/0.2.1` | 191.50 h | 204.00 h | 0.0653 |
| test | nanostores/nanostores `1.4.2` | `seed-rules/0.2.1` | 170.50 h | 185.00 h | 0.0850 |

The 133 reviewed targets total 386.75 expected hours against 420.50 seed hours.
Aggregate repository-observation WAPE and signed bias are both 0.0873 because all
three seed totals are higher. This is a three-observation diagnostic, not evidence
that the estimator has eight-percent generalization error.

Seven target labels are explicit `0/0/0` exclusions with retained source lineage:
CliWrap stream operations are process-pipe behavior rather than persistence,
nanostores effects represent framework-neutral state behavior rather than UI, and
a nanostores benchmark is not a product entry point. Review/compiler version 0.2.0
was introduced to represent these decisions honestly; it requires rationale and a
size exception and rejects partially positive zero ranges. The analyzers and
`seed-rules/0.2.1` priors were not changed from these development, validation, or
test observations.

The result broadens ecosystem and repository-shape coverage, but each partition
still has only one new observation and all six public families share one teacher.
No numerical fitting or admission threshold should use this expansion until a
genuinely distinct review is compiled and repository-held-out policies are frozen.

## 2026-08-06: `seed-rules/0.2.1` normalization revision

Status: **qualitative guardrail correction; no prior calibration**

The Milestone 7B3 TypeScript exact-copy mutation exposed an ecosystem-ownership
defect: the common scanner tags `.ts` files as `ecosystem:typescript`, while the
package estimator deliberately groups JavaScript and TypeScript under a shared
`javascript` scope. The duplicate and production/test normalizer therefore skipped
TypeScript file facts. Version 0.2.1 treats those tags as compatible.

The `models/seed-rules/0.2.1.json` numerical priors are identical to 0.2.0. No
teacher target, test-partition label, repository total, or preferred hour value was
used to choose the correction. The new artifact digest is
`sha256:57378795593acd2ff0a2f4361698193a11dca86da11493f072da6a9f9b344d4e`.
The public synthetic 0.2.0 suite passes all 84 qualitative assertions across 30
source states. Milestone 7B4 subsequently evaluates the same 0.2.1 artifact against
suite 0.3.0: 156 assertions across 48 states, including bounded renamed
near-copies, compiler-disabled syntax, data, security, declared coverage,
workspace boundaries, CI, and containers. No prior or estimator code changed for
that expansion. The frozen public-pilot corpus and its 0.2.0 source-estimate
provenance remain unchanged. `efforthours-public-expansion/0.1.0` subsequently
evaluates the same 0.2.1 artifact against three additional teacher-reviewed
release snapshots; those labels did not change the model and still lack
independent correction.

## 2026-08-05: EffortHours at `f84a58a`

Status: **provisional logical-review anchor; not calibration data**

This is the first realism check for the Milestone 5 seed estimator. The deterministic
output was reviewed as a decomposition of the visible repository by an AI coding
agent using senior-engineer judgment. No Git history, time records, invoice data,
or independent maintainer estimate was used. No prior was changed to make this
result land on a preferred total.

### Provenance

| Field | Value |
| --- | --- |
| Source commit | `f84a58af9f7be61b9920e62c69658a0273e513b9` |
| Repository evidence digest | `sha256:f7481a93397ef7124582663d3d9485d74c9cdec2c5ab74826a522a40d1bae856` |
| Estimator | `seed-rules/0.2.0` |
| Model status | `experimental-uncalibrated` |
| Model digest | `sha256:ecc81cb04d5f3bcdc3dcd80a260ee3560f1f142185f1e12c1366211bdbf0011a` |
| Worker baseline | One competent senior contractor, technically familiar, business-domain unfamiliar, no AI |
| Technology baseline | Modern 2026 implementation |
| Rate card | None; pricing is deliberately omitted until the dated Milestone 6 rate card exists |
| EffortHours verification mode | Static assumed-working; target code was not executed by the analyzer |
| Separate development check | Release build passed; 46 memory-only unit tests and 7 end-to-end tests passed |

### Observed repository shape

| Measurement | Value |
| --- | ---: |
| Included files | 118 |
| Text lines | 19,555 |
| Maintained source files | 55 |
| Maintained source lines | 11,866 |
| Test files | 16 |
| Test lines | 2,693 |
| Generated files | 2 |
| C# types | 140 |
| C# methods | 477 |
| C# branch points | 947 |

### Profile totals

| Profile | Low EHE | Expected EHE | High EHE | Represented items |
| --- | ---: | ---: | ---: | ---: |
| Implementation | 229.00 h | 418.75 h | 737.00 h | 135 |
| Recreation | 233.50 h | 432.25 h | 766.25 h | 144 |

The recreation delta is 13.50 expected hours, all in explicit architecture and
recreation-design work. The source state, tests, documentation, and other represented
work remain identical between profiles.

### Expected category breakdown

| Category | Implementation | Recreation |
| --- | ---: | ---: |
| Specification comprehension and domain learning | 5.75 h | 5.75 h |
| Repository and solution setup | 16.25 h | 16.25 h |
| Architecture and technical design | 16.50 h | 30.00 h |
| Production implementation | 295.75 h | 295.75 h |
| Unit testing | 21.00 h | 21.00 h |
| End-to-end and UI testing | 25.00 h | 25.00 h |
| Manual validation, debugging, and hardening | 7.00 h | 7.00 h |
| Documentation | 13.75 h | 13.75 h |
| Build configuration and developer tooling | 6.75 h | 6.75 h |
| Self-review and system integration | 11.00 h | 11.00 h |

The separate professionalization ledger contains one basic CI-workflow gap at
1.50/3.75/8.00 hours. It is correctly excluded from both profile totals and from
future replacement-cost calculations.

### Review judgment

The expected implementation total is a plausible preliminary order of magnitude:
about 10.5 forty-hour contractor weeks for a multi-project CLI containing safe
repository traversal, two static ecosystem analyzers, versioned contracts and
schemas, reporting, packaging, a granular estimator, tests, benchmarks, and project
documentation. The 229-hour low case represents unusually smooth delivery with
strong ecosystem familiarity and reuse of conventional patterns. The 737-hour high
case reasonably covers ambiguity and the risk of getting parsers, filesystem safety,
and deterministic contracts right.

The decomposition is more useful than the total, but it is not yet persuasive
enough for billing. Production implementation contributes 70.6% of expected effort
and is driven heavily by the general source backbone. That concentration must be
tested against dissimilar repositories so source volume does not overwhelm feature
boundaries or reward verbosity. The testing split is directionally credible, though
coverage evidence and test depth need stronger fixtures. Documentation and manual
validation may be understated for a polished public release. The narrow recreation
premium also needs comparison with repositories whose architecture is difficult to
infer from the finished artifact.

This anchor should next receive an independent work-item review. It must remain out
of any held-out evaluation set if its corrections are used to change priors.
