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

Current logical admission may use those honestly disclosed `teacher-estimate`
records when each total reconciles exactly from small evidence-backed tasks and a
frozen gate passes. Independent replication is optional corroboration, not a
prerequisite or an implied maturity upgrade. Empirical production observations are
separate evidence and never become effort multipliers.

Because EffortHours's own anchor informed the seed-model and calibration design, it is
a development diagnostic rather than an eligible held-out test record unless a
future independent snapshot and review policy explicitly establishes otherwise.

## 2026-08-11: Kotlin/JVM source-boundary review

Status: **qualitative analyzer and prior-reuse checkpoint; no numerical calibration or Change admission**

Kotlin analyzer `0.1.0` was reviewed as bounded token-backed static analysis with
shared conservative Maven/Gradle JVM ownership, not a Kotlin parser, compiler
front-end, type checker, bytecode reader, JVM, Android tool, or effective build-
model adapter. It decomposes maintained Kotlin evidence into JVM scopes, packages,
files, functions, methods, types, extensions, public symbols, generics, suspend/
async units, coroutines, Flow, nullability, and branches. The existing
`seed-rules/0.4.0` `polyglot-source-backbone` consumes those analogous units
without a model-artifact or numerical-prior change. Import-qualified server/API,
Android/UI, CLI, data, integration, security, validation, background, build, and
test facts reuse existing specialized rules. No fitted dataset, source-volume
regression, or external reviewer value was used.

Static Maven/Gradle parsing never invokes a JVM, compiler, build tool, wrapper,
dependency resolver, Android tooling, KSP, kapt, compiler plugin, or test runner,
and never follows a local project path outside repository scope. Semantic evidence
requires recognized canonical imports or fully qualified names; local framework
namesakes remain ordinary source structure. Gradle Kotlin DSL stays build
configuration, while other maintained `.kts` scripts are inventoried without
execution. Dynamic build values, source sets, variants, multiplatform
`expect`/`actual`, plugin-generated declarations, reflection, runtime DSLs, and
Android behavior retain explicit uncertainty. Ordinary output emits no source
excerpts.

The standalone public qualitative gate has 14 Kotlin source states and 63
relations. It covers formatting/comments, exact copies, generated output, Ktor
API, Android Compose UI, tests, Room data, OkHttp integration, security,
background work, static Gradle Kotlin DSL, coroutines/Flow, and namesake rejection.
All 63 assertions pass. Earlier aggregate, Go, and Java candidates remain
unchanged; the dedicated suite isolates the Kotlin boundary without presenting
version-only regeneration as accuracy evidence.

Change `0.11.0` adds Kotlin-aware normalization and semantic routing but no fitted
prior. The existing Stage A admission contains no Kotlin, Java, Go, Python, or SQL
cases and remains limited to the previously admitted `0.6.0` families. Absolute
Kotlin EHE accuracy, independent review, real-repository family coverage, and
decomposed public Kotlin Change labels remain unresolved.

## 2026-08-11: Java source-boundary review

Status: **qualitative analyzer and prior-reuse checkpoint; no numerical calibration or Change admission**

Java analyzer `0.1.0` was reviewed as bounded token-backed static analysis with a
conservative Maven/Gradle metadata projection, not a Java parser, compiler, type
checker, bytecode reader, JVM, or effective build-model adapter. It decomposes
maintained Java evidence into projects, packages, modules, files, methods, types,
public symbols, generics, asynchronous/concurrency units, and branches. The
existing `seed-rules/0.4.0` `polyglot-source-backbone` consumes those analogous
units without any model-artifact or numerical-prior change. Import- and annotation-
qualified API, CLI, data, integration, security, validation, background, build,
and test facts reuse existing specialized rules. No fitted dataset, logged time,
private repository, source-volume regression, or external reviewer value was used.

Static Maven/Gradle parsing never invokes a JVM, compiler, build tool, wrapper,
dependency resolver, annotation processor, or test runner, and never follows a
local project path outside repository scope. Semantic evidence requires recognized
canonical imports or fully qualified names; local framework namesakes remain
ordinary source structure. Dynamic build values, profiles, processors, module
resolution, reflection, generated types, and runtime behavior retain explicit
uncertainty. Ordinary output emits no source excerpts.

The standalone public qualitative gate has 13 Java source states and 56 relations.
It covers formatting/comments, exact copies, generated output, API, tests, data,
integration, security, background work, static build metadata, concurrency, and
namesake rejection. All 56 assertions pass. The prior aggregate and Go candidates
remain unchanged; the dedicated suite isolates the Java boundary without
presenting version-only regeneration as accuracy evidence.

Change `0.10.0` adds Java-aware normalization and semantic routing but no fitted
prior. The existing Stage A admission contains no Java, Go, Python, or SQL cases
and remains limited to the previously admitted `0.6.0` families. Absolute Java EHE
accuracy, independent review, real-repository family coverage, and decomposed
public Java Change labels remain unresolved.

## 2026-08-11: Go source-boundary review

Status: **qualitative analyzer and prior-reuse checkpoint; no numerical calibration or Change admission**

Go analyzer `0.1.0` was reviewed as bounded token-backed static analysis, not a Go
parser, type checker, compiler, or toolchain adapter. It decomposes maintained Go
evidence into modules, packages, files, functions, methods, types, interfaces,
exported symbols, generics, asynchronous/concurrency units, and branches. The
existing `seed-rules/0.4.0` `polyglot-source-backbone` consumes those analogous
units without any model-artifact or numerical-prior change. Import-qualified Go
API, CLI, data, integration, security, validation, background, build, and test
facts reuse existing specialized rules. No fitted dataset, logged time, private
repository, source-volume regression, or external reviewer value was used.

Static module/workspace parsing never invokes the Go toolchain or follows a local
replacement outside repository scope. Semantic evidence requires recognized
imports and qualified use; local framework namesakes remain ordinary source
structure. Build constraints, platform filenames, `go:embed`, `go:generate`, cgo,
and blank-import registration retain explicit uncertainty because the analyzer
does not resolve, expand, execute, compile, or prove them. Ordinary output emits no
source excerpts.

The standalone public qualitative gate has 13 Go source states and 56 relations.
It covers formatting/comments, exact copies, generated output, API, tests, data,
integration, security, background work, build semantics, concurrency, and namesake
rejection. All 56 assertions pass. The prior 88-state aggregate and every frozen
candidate remain unchanged. A dedicated suite isolates the new analyzer boundary
without presenting a version-only aggregate regeneration as accuracy evidence.

Change `0.9.0` adds Go-aware normalization and semantic routing but no fitted
prior. The existing Stage A admission contains no Go, Python, or SQL cases and
remains limited to the previously admitted `0.6.0` families. Absolute Go EHE
accuracy, independent review, real-repository family coverage, and decomposed
public Go Change labels remain unresolved.

## 2026-08-11: Python polyglot source-boundary review

Status: **qualitative analyzer and prior-extension checkpoint; no numerical calibration or Change admission**

Repository estimator `seed-rules/0.4.0` adds one generic
`polyglot-source-backbone` for the new token-backed Python analyzer. The review
first decomposed Python evidence into files, functions, methods, types, public
symbols, async units, and branch points, then mapped those units to the analogous
transparent JavaScript construction rates already published in `0.3.0`. The new
rule deliberately widens default uncertainty. No fitted dataset, logged time,
private repository, source-volume regression, or external reviewer value was used.
All existing .NET, JavaScript/TypeScript, frontend, SQL, and specialized rule
definitions remain byte-equivalent to their `0.3.0` counterparts.

Python analyzer `0.1.0` was reviewed as token/indentation-backed, not parser- or
compiler-backed. Semantic evidence requires import-qualified calls, decorators,
or base types. Negative local FastAPI/httpx/Celery namesakes remain ordinary
source structure. Static package metadata never invokes Python or `setup.py`, and
the analyzer emits no source excerpts. Jupyter is excluded pending its own output,
magic, embedded-data, and mixed-kernel safety boundary.

The public qualitative gate advances from 77 to 88 source states and from 309 to
339 relations. The 11 new Python states cover formatting/comments, exact copies,
generated output, API, tests, data, integration, security, background work, and
namesake rejection. All 339 assertions pass. The earlier 77 candidate reports
remain frozen at `seed-rules/0.3.0`; only the Python states use `0.4.0`. This mixed
candidate identity is explicit in the mutation report and prevents a model-version
rename from masquerading as reevaluation evidence.

Change `0.8.0` adds indentation-aware `.py`/`.pyi` normalization and category
routing but no fitted prior. The existing Stage A admission contains no Python or
SQL cases and remains limited to the previously admitted `0.6.0` non-SQL families.
Absolute Python EHE accuracy, independent review, real-repository family coverage,
and decomposed public Python Change labels remain unresolved.

## 2026-08-11: Milestone 7B6 analyzer precision and mutation guardrails

Status: **qualitative precision checkpoint; no numerical calibration or independent-review claim**

.NET analyzer `0.3.3` adds conservative intra-file reachability for explicit
private methods. It considers only methods with no bounded intra-file reference in
non-partial, unattributed types without a base list, and retains methods with
attributes or partial/external implementations. Reachable private helper chains,
overload ambiguity, and matching string names are also retained, while exclusions
emit explicit evidence. Recognized framework-derived and partial types fail closed.
This is not general liveness, reflection, dynamic-dispatch, or cross-file analysis.

JavaScript analyzer `0.5.1` records explicit static accessibility semantics from
maintained HTML and Angular templates, including roles, labels, alternative text,
live regions, focus, and keyboard handlers. It also records accessibility-focused
component/end-to-end test provenance for Testing Library and Axe-family tools.
Every accessibility fact states that conformance is not proven; no renderer,
framework compiler, test runner, audit, source excerpt, dependency install, or
network access is involved.

The unchanged `seed-rules/0.3.0` artifact maps accessibility to its existing
combined security/accessibility prior with distinct work-item reasoning. Public
mutation suite `0.7.0` adds 10 synthetic states and 62 relations for bounded
reachability, explicitly included conditional behavior, two specified non-exact
equivalent-purpose shapes, accessibility implementation/test depth, and
representative package/mixed dependency graphs. All 309 relations pass across 77
states; the earlier 67 candidate reports and 247 assertions remain frozen.

The two equivalent-purpose cases demonstrate non-explosive marginal movement for
those shapes only; they do not establish general semantic-clone detection. No
numerical prior, teacher label, corpus partition, review maturity, threshold, or ML
dependency changes. The policy accepts exact decomposed host-AI teacher judgment as
logical weak supervision while preserving `teacher-estimate` provenance.
Independent replication remains optional, and later production observations remain
a separate empirical validation track.

## 2026-08-11: static SQL evidence and Change normalization revision

Status: **transparent experimental analyzer extension; not calibrated, admitted, or independently reviewed**

Common scanner `0.2.2` and SQL analyzer `0.1.0` add bounded static `.sql` evidence
for schemas, migrations, constraints, indexes, stored programs, queries, test
fixtures, deployment scripts, and explicit cross-database boundaries. Parser and
dialect confidence remain distinct. Reads are scanner-admitted, digest checked,
UTF-8/size/token bounded, link safe, offline, and non-executing; output includes no
source excerpts. Unknown syntax remains visible without guessed units.

Repository `seed-rules/0.3.0` is unchanged. Supported SQL roles reuse its existing
data, integration, testing, and packaging priors, so this revision adds no fitted
SQL rate or new model artifact. Exact duplicate bodies contribute semantic value
once, dumps are excluded, and seed row volume is bounded to represented intent.

Public mutation suite `0.6.0` has 67 source states and 247 passing relations. Its
11 SQL additions test all three range points for formatting/copy/dump/unknown
invariance, semantic directionality, role/category isolation, cross-database
integration, and seed-volume bounds. The earlier 56 candidate reports remain
frozen. The relations establish qualitative non-perverse movement only; they are
not hour labels, independent review, held-out accuracy, or calibration.

Change source identity advances to
`change-seed/0.7.0+seed-rules/0.3.0` for SQL-aware formatting and category routing.
Every non-SQL Change rule and frozen report remains unchanged. The admitted 0.6.0
Stage A set contains no SQL records, so its gate cannot be generalized to SQL.

## 2026-08-10: `change-seed/0.6.0` Stage A logical admission

Status: **experimental 4-to-32-hour logical baseline; not empirically calibrated or production-ready**

`change-model-admission/0.2.0` and rubric `change-ehe-work-item/1.1.0` permit a
disclosed host-AI teacher to supply logical weak supervision when each total is an
exact sum of small, distinct, evidence-backed tasks. Targets normally carry 0.5
to 1.5 expected hours; more than two hours requires a concrete indivisibility
exception. The label remains `teacher-estimate` and is never represented as human
or independent review. Production observations, if collected later, are a
separate empirical comparison rather than an effort signal or multiplier.

The current baseline passes the frozen Stage A gates on five eligible public
repository families spanning .NET, JavaScript, and TypeScript. Teacher and
candidate totals are 38.00 and 38.50 expected hours, WAPE is 0.0526, aggregate
bias is +0.0132, and every per-case error bound passes. Their native-category WAPE
is 0.3963 with +0.0132 bias, teacher-expected interval coverage is 1.0000, and the
mean-width ratio is 1.3624. A transparent rubric-1.1.0 audit covers all 28 frozen
parent targets with 45 distinct 0.5-to-1.5-hour teacher tasks; all 41 current
candidate work items meet the same ceiling. The sub-day p-limit record remains a
semantic guardrail, and the expansion test comparison remains withheld.

Version 0.6.0 does not fit or alter a numerical prior. It partitions an unchanged
mixed-role capability budget across disjoint native-category evidence, decomposes
larger items into named roughly one-hour phases, caches repository analysis once
per immutable snapshot, caps the optional commit audit, and replaces cent rounding
with exact nonnegative largest-remainder allocation.
The documented million-line base/head and 128-commit benchmarks pass local
30-second/512-MiB and 45-second/192-MiB gates in three fresh processes; a
257-commit boundary retains the complete final delta while explicitly omitting the
bounded optional audit.

This decision admits only one-to-several-day experimental use. A larger band is a
new decision after the first band remains stable and model context/token/time/cost
telemetry is recorded when available. It does not establish a general error rate,
formal interval calibration, historical truth, or production accuracy.

## 2026-08-10: `change-seed/0.5.0` range-normalization diagnostic

Status: **transparent reporting revision; no valuation or calibration change**

Explicit multi-commit ranges already exposed gross isolated component EHE,
authoritative normalized final-delta EHE, named signed adjustments, and exact
allocations. Version 0.5.0 adds an auditable expected-point projection of that
unchanged ledger. Gross-to-final normalization is `max(0, gross - normalized)`
over gross isolated expected EHE. The narrower rework-like numerator is capped at
that normalization amount and includes only negative overlap/revert adjustment
attribution; shared/repeated work and residual interaction remain separate.

Zero gross effort produces a not-applicable status and no percentage. A normalized
total above gross produces zero normalization and retains positive interaction.
Structured shares round to four decimal places away from zero. Low/high planning
bounds remain hours and are not converted to percentages. Base/head, commit,
directory, evidence, and current base/head-only PR forms do not receive an invented
intermediate-history diagnostic.

The v1 report adds an optional summary with a content-stable explanation ID and
exact adjustment-ID lineage. Existing frozen reports remain valid and are not
rewritten. Memory-only tests cover exact 10-to-5 normalization, clean additivity,
overlap, complete/partial reverts, shared work, zero gross, positive interaction,
rounding, and selector eligibility. A process-level Git range covers a complete
revert and saved-report explanation.

No repository or Change prior, threshold, work-item amount, normalized total,
label, dependency, metric, partition, or review maturity changes. These percentages
are structural diagnostics, not historical labor/rework, productivity scoring,
calibration evidence, an effort multiplier, or model admission.

## 2026-08-10: `seed-rules/0.3.0` frontend semantic revision

Status: **transparent evidence/prior correction; not calibrated or independently reviewed**

The prior UI rule priced maintained HTML and CSS-family physical lines while the
analyzer exposed only file and line aggregates. That made formatting a possible
effort signal and could not distinguish a simple asset from represented forms,
bindings, responsive rules, design tokens, animation, or theme behavior. The
correction was specified as a subject-neutral invariant before evaluation: raw
line layout must not move EHE, exact copies must normalize, and additional
represented frontend behavior must move the UI category.

JavaScript analyzer `0.5.0` adds bounded tolerant HTML/template and CSS-family
structure plus static Angular `@Component` metadata. A named `Component` import
from `@angular/core` is required (a local alias is accepted), and metadata values
must be literal strings or arrays. External references resolve only relative to
the component and only to common-scanner-admitted, digest-verified assets;
ambiguous shared assets retain generic ownership. Generated, vendored, minified,
test, documentation, binary, and ignored build-output assets remain excluded. No
source excerpt is emitted. The implementation does not render, compile a
framework, execute TypeScript/configuration or preprocessors, prove runtime
behavior, or perform an accessibility audit.

`seed-rules/0.3.0` removes the `asset-lines` UI driver and adds bounded template
structure/binding, stylesheet structure, responsive, design-token, and
animation/theme drivers. Every non-UI numerical prior remains identical to 0.2.1.
The new UI rates are transparent preliminary priors, not fitted values. The model
digest is
`sha256:e8bce2f76c97564919ab6be41f1cfd6b222d531a4dbd08a8b22c7abe6b1eebdf`.

Public mutation suite `0.5.0` contains 56 cases and 192 passing relations. Its five
new frontend states cover all three range points for formatting and exact-copy
invariance, semantic template/style directionality, Angular UI directionality,
and production-category isolation. Re-evaluating the previous 51 cases under the
new analyzer/model produces identical numeric ranges. All earlier mutation
reports, reviewed corpora, labels, and estimator provenance remain frozen.

The current composite Change identity advances mechanically to
`change-seed/0.4.0+seed-rules/0.3.0`; no Change rule, threshold, label, diagnostic
comparison, or frozen Change report changes. This record is a correctness and
guardrail disclosure, not calibration, accuracy evidence, or model admission.

## 2026-08-10: `change-seed/0.4.0` generated-customization boundary

Status: **transparent structural normalization revision; not calibrated or independently reviewed**

The previous Change path classifier excluded every generated file body, including
maintained customization that a generator explicitly preserves inside the file.
The settled product rule allows that effort only when it can be distinguished
safely. The correction uses exact, balanced, non-nested, EffortHours-specific
`<custom-code>` markers with same-style closing markers, valid UTF-8, an eight-
mebibyte blob limit, and a 128-region limit. It does not infer unrelated generator-
specific protected-region syntax. Only the extracted custom projection is
compared. Generated bytes outside it never contribute, and stronger vendored/
minified/binary/lockfile/build-output/exact-copy exclusions still win.

Unchanged and formatting-only projections remain zero. Added, modified, or removed
custom projections can contribute bounded edit-region work. Missing source,
malformed markers, mixed marker styles, excessive regions, and oversized blobs
fail closed with traceable reasons; ambiguous markers also emit a review warning.
The v1 path contract remains unchanged: a supported projection is represented
while retaining the scanner's generated tag and adding an explicit normalization
tag.

The estimator identity advances to
`change-seed/0.4.0+seed-rules/0.2.1`. Repository priors, work-item rates, labels,
thresholds, dependencies, and frozen reports do not change. Memory-only semantic
regressions and one process-level Git regression were added before the identity
advance was documented.

No checked-in Change source case contains the supported marker, and the only
generated-path source report remains a conventional exact-zero synthetic case.
Consequently existing 0.3.0 development/validation numbers are invariant under the
new normalization boundary. They were not regenerated under a new identity, and
the withheld test comparison was not opened. This record is not calibration or an
accuracy claim.

## 2026-08-10: `change-seed/0.3.0` logical-marginality correction

Status: **transparent structural correctness revision; not calibrated or independently reviewed**

The frozen public-real expansion diagnosed repository capability parts being
summed into a full positive Change prior. The correction was expressed without
repository-specific identifiers: repeated production, test, and security
partitions for the same logical capability/path evidence must share one budget;
distinct added capabilities remain additive; and a capability detected on a
materially modified artifact cannot collapse to a 0.25-hour classification delta.
These in-memory regressions were fixed before new candidate reports were generated.

`change-seed/0.3.0` replaces the summed repository-capability delta for existing or
modified artifacts with one evidence-derived budget. Each changed path contributes
one to four logical units through capped edit-region bands, which feed the existing
diminishing tiers with an eight-hour cap. Unmapped maintained-artifact fallbacks
use the same units. Distinct capabilities added through new artifacts retain their
positive repository marginal. Repository `seed-rules/0.2.1`, labels, thresholds,
and dependencies are unchanged.

Five new reports and development/validation evaluations are stored separately
under `calibration/changes/public-real-expansion/diagnostics/change-seed-0.3.0`.
Every frozen alpha.2 report, review plan, corpus, and evaluation remains unchanged.

| Partition | Reviewed expected | Alpha.2 expected / WAPE / bias | 0.3.0 expected / WAPE / bias |
|---|---:|---:|---:|
| development | 19.00 h | 55.75 h / 1.9605 / +1.9342 | 20.75 h / 0.1447 / +0.0921 |
| validation | 16.00 h | 34.75 h / 1.1719 / +1.1719 | 15.75 h / 0.0156 / -0.0156 |

The structural symptom is removed, but effects remain mixed. Development unit
testing moves from 21.50 to 2.75 hours against 4.25 reviewed and security from
16.00 to 2.00 against 1.25; validation unit testing moves from 19.75 to 4.50
against 4.75. Production moves to 5.00 against 5.50 in development and 3.25
against 4.25 in validation. Unchanged change-level validation/review work retains
its prior disagreement, and candidate high totals remain 39.25 versus 30.25
reviewed in development and 30.00 versus 26.00 in validation.

Consolidated work items reduce exact mapping: development target/source-reference/
candidate-item matches are 14/17, 14/24, and 14/17; validation matches are 10/12,
10/16, and 10/12. Repository and category metrics include every item, while item
metrics disclose this lower coverage.

These one-teacher development/validation measurements are a diagnostic of the
correction, not an accuracy result or admission decision. No test-family candidate
report or evaluation was generated for 0.3.0; the held-out comparison remains
unopened.

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

Development and validation metrics are diagnostic error scales only. At that
checkpoint the test comparison was not run because no independently reviewed
corpus, numerical thresholds, or frozen release candidate existed. The blind
121-target handoff pins
source-corpus digest
`sha256:ecfdb867ed2ba4912c9550277fc050b5e5511d0e15a107c8a08c044f61793c10`.
Metric identity and candidate selection order are frozen. Milestone 7B6 supersedes
independence as a prerequisite, but any future threshold still requires a frozen
logical-admission policy, adequate repository diversity, and an unopened eligible
comparison.

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
No numerical fitting or admission threshold should use this expansion until the
repository-held-out policy, decomposed logical-label gate, and adequate diversity
requirements are frozen. A genuinely distinct review remains optional
corroboration rather than a prerequisite.

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

This anchor may receive an optional independent work-item replication. It must
remain out of any held-out evaluation set if its corrections are used to change
priors.
