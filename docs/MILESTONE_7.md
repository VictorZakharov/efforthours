# Milestone 7: Calibration and Local Models

## Status

Milestone 7A and the Milestone 7B1 through 7B5 public-corpus, review, and mutation
checkpoints were implemented on August 6, 2026. A post-7B5 analyzer-precision
checkpoint was implemented on August 8, 2026, and the first measured-coverage
checkpoint was implemented on August 9, 2026. Together they establish the
versioned review corpus, low-cost authoring and compilation boundaries,
deterministic offline evaluation, expanded licensed labels, exact-digest
subsequent review, cross-ecosystem relational model guardrails, and a documented
path from reviewed exclusions to general analyzer corrections. The seed estimator
remains `experimental-uncalibrated`; this milestone does not make its current hours
production-ready.

Both public corpora still have one host-AI teacher and no independent correction.
Milestone 7B is therefore in progress: broader licensed repository coverage,
semantic-clone, reachability, accessibility, and realistic multi-package
guardrails, and an actual independent review are required before numerical
admission thresholds or model training. The
presence of second-review tooling must not be confused with completion of that
review.

Local model training and inference are deferred until a diverse, licensed corpus
exists and a candidate model demonstrates an improvement on repository-held-out
data.

## Objective

Turn consistent logical reviews into measurable weak supervision without
confusing teacher judgment with historical labor or literal ground truth.

Milestone 7 must make it possible to:

- record evidence-backed, work-item-sized reviewed EHE labels;
- keep public and private calibration material separable;
- isolate development, validation, and test partitions by repository identity;
- compare any candidate `EstimateReport` with the reviewed labels offline;
- measure item, category, and repository-total error and interval behavior;
- diagnose unmatched or structurally changed work-item decompositions; and
- admit local ML only when it improves held-out results without losing lineage or
  deterministic rule guardrails.

## Implemented Milestone 7A scope

The first slice contains no ML runtime or training dependency. It adds:

1. a v1 calibration-corpus contract and JSON Schema;
2. a versioned teacher/reviewer rubric;
3. semantic validation, including repository-level split isolation;
4. an offline evaluator for existing canonical estimate reports;
5. versioned validation and evaluation output contracts;
6. `eh calibration validate` and `eh calibration evaluate`; and
7. memory-only unit tests plus focused disk-backed CLI tests.

The first slice deliberately does not tune `seed-rules/0.2.0`, publish an accuracy
claim, train a model, call a remote provider, or implement PR/commit estimation.

## Implemented Milestone 7B1 scope

The public-pilot slice adds no ML runtime or training dependency. It adds:

1. a v1 unreviewed authoring-packet contract and JSON Schema;
2. `eh calibration scaffold`, with visible reference values or `--blind`;
3. a v1 completed review-plan contract and JSON Schema;
4. `eh calibration compile`, which verifies exact source-estimate digests,
   pins its compiler version, requires every represented capability, and restores
   all work-item/evidence mappings deterministically;
5. explicit `--output` support for estimates and calibration artifacts;
6. a three-repository MIT-provenanced pilot with repository-level partitions
   frozen before tuning;
7. 99 teacher-estimate targets covering all 228 represented source work items; and
8. checked-in development, validation, and test reports for `seed-rules/0.2.0`.

The authoring packet is structurally separate from the corpus. Its status is always
`unreviewed`, its review fields begin null, and corpus validation cannot consume it.
Reference mode identifies seed values as suggestions; blind mode removes candidate
hours, category totals, and confidence. Completed review plans contain explicit
small-target ranges and rationale rather than an instruction to copy seed values.

The compiler groups only the source estimate's deterministic sliced work-item IDs.
It rejects changed estimate digests, unknown or missing capabilities, incomplete
coverage, mixed capability categories/scopes, and attempts to create more reviewed
targets than there are uniquely assignable source work items. Professionalization
gaps remain outside represented calibration targets.

The public pilot and baseline interpretation are documented in
`calibration/corpora/public-pilot/BASELINE.md`; exact source/license provenance is
in `calibration/corpora/public-pilot/SOURCES.md`. The fixed test partition must not
be used to tune the seed rules or candidate hyperparameters.

## Implemented Milestone 7B2 scope

The review/mutation slice still adds no ML runtime or training dependency. It adds:

1. a v1 explicitly unreviewed corpus-review packet and JSON Schema;
2. `eh calibration review-scaffold`, whose blind mode hides prior ranges,
   rationale, uncertainty decisions, and totals;
3. a v1 completed corpus-review plan and JSON Schema;
4. `eh calibration review-compile`, which pins the exact source-corpus
   digest, requires every record and target, rejects reused reviewer identities,
   and preserves all structural lineage while advancing maturity;
5. v1 mutation-suite and mutation-report contracts;
6. `eh calibration mutations`, with deterministic repository/category
   difference bounds and a dedicated regression exit code;
7. a public synthetic .NET archetype with formatting, duplication, generated,
   API, test, documentation, and integration variants; and
8. a compact blind handoff packet for all 99 public-pilot targets.

Subsequent review decisions are explicit `accept` or `replace` actions. An accepted
target copies the prior reviewed target only after the second reviewer deliberately
checks it. A replacement supplies a complete range, rationale, uncertainty, and
size exception when needed. The compiler preserves target IDs, categories, scopes,
source work-item IDs, evidence IDs, source estimates, repository identities, and
partitions. It retains earlier reviewer provenance and appends distinct subsequent
reviewers. `reviewed` requires a reviewer; `adjudicated` requires an adjudicator and
combined reviewer provenance. Maturity cannot be downgraded, and an adjudicated
record cannot be advanced again.

The checked-in blind packet remains `unreviewed`; it contains no prior numeric or
rationale fields and is not a corpus. The agent that produced the teacher pass and
this implementation is not an independent reviewer. The public pilot therefore
remains at `teacher-estimate` maturity.

Mutation suites are relational guardrails, not reviewed labels. Each case selects
one canonical candidate by source digest, profile, and baseline. Each assertion
compares one low, expected, or high repository/category point and defines an
allowed minimum and/or maximum difference. Missing categories are zero, which
allows a test or documentation addition to be compared with an absent base
category. A failed assertion still emits a complete deterministic report and
returns exit code 5; malformed inputs use the ordinary invalid-input path.

## Implemented Milestone 7B3 scope

The cross-ecosystem mutation slice adds no schema, remote service, training
dependency, or numerical prior change. It adds:

1. parser-backed JavaScript formatting, exact-copy, generated, customized-generated,
   API, unit-test, documentation, and external-integration variants;
2. token-backed TypeScript formatting, exact-copy, generated, API, unit-test,
   documentation, and external-integration variants;
3. mixed .NET/JavaScript generated, .NET API, JavaScript UI, and TypeScript-test
   variants;
4. explicit low, expected, and high assertions for invariance and meaningful
   category movement;
5. nested synthetic test packages that prove test-only additions do not inflate
   production categories; and
6. `seed-rules/0.2.1`, a normalization correction that treats TypeScript file tags
   as members of the shared JavaScript/TypeScript estimation scope.

The aggregate public suite now contains 30 source states and 84 assertions. All
relations pass `seed-rules/0.2.1`. The 0.2.1 JSON catalog has exactly the same
numerical priors as 0.2.0; its version changes because the file-ownership correction
can change estimates for TypeScript repositories containing exact copies or tests
in a shared package scope. The frozen pilot labels and 0.2.0 candidate reports are
not rewritten.

## Implemented Milestone 7B4 scope

The behavior-and-delivery mutation slice adds no schema, ML dependency, remote
service, estimator code change, or numerical-prior change. It adds:

1. bounded marginality cases for renamed .NET, JavaScript, and TypeScript
   near-copies, without claiming general semantic clone detection;
2. a C# compiler-disabled block containing data and authorization syntax that must
   remain invisible to represented effort;
3. .NET, JavaScript, and TypeScript data/persistence boundaries plus an additional
   .NET schema migration;
4. .NET, JavaScript, and TypeScript authentication and hardening boundaries;
5. identical JavaScript code and tests with no coverage declaration, an 80%
   declared-and-assumed threshold, and a 100% declared-and-assumed threshold;
6. one- and two-package JavaScript workspaces where the second package reuses an
   exact source body but still adds setup and integration-review work;
7. isolated CI-workflow and container-definition delivery cases; and
8. a memory-only pipeline regression proving that compiler-disabled C# boundary
   syntax neither emits data/security evidence nor changes EHE.

The aggregate public suite now contains 48 source states and 156 assertions. All
relations pass the unchanged `seed-rules/0.2.1` artifact. These checks were chosen
from product semantics before evaluation and are still qualitative guardrails, not
reviewed effort labels. Near-copy bounds protect the current diminishing-marginal
behavior; the estimator still has no semantic clone detector. The dead-code claim
is limited to syntax excluded by the C# preprocessor and does not imply general
reachability or liveness analysis.

## Implemented Milestone 7B5 scope

The corpus-expansion and review-contract slice adds no ML dependency, remote
provider, estimator rule change, or numerical-prior change. It adds:

1. three additional MIT-licensed repository families selected and partitioned
   before numerical review from immutable release archives, without Git-history
   inspection;
2. `efforthours-public-expansion/0.1.0`, containing 133 teacher-estimate targets for
   developit/mitt, Tyrrrz/CliWrap, and nanostores/nanostores across fixed
   development, validation, and test partitions;
3. `ehe-work-item/1.1.0`, which preserves positive-label semantics and adds exact
   `0/0/0` reviewed exclusions with mandatory rationale and `sizeException`;
4. schema and semantic validation for exact-zero reviewed ranges, including
   rejection of ambiguous partially positive zero ranges;
5. `calibration-review-compiler/0.2.0` and
   `calibration-corpus-review-compiler/0.2.0`, with deterministic support for
   positive legacy `0.1.0` plans and deliberate rejection of legacy zero labels;
6. frozen development, validation, and test evaluations against the unchanged
   `seed-rules/0.2.1` candidate;
7. a blind 133-target subsequent-review packet and one combined handoff index for
   both public corpora; and
8. memory-only contract/compiler/evaluator tests plus a process-level CLI
   compilation check for explicit exclusions.

The expansion contains seven exact-zero targets. Four reject CliWrap process-pipe
I/O as data persistence; two reject framework-neutral nanostores effects as UI;
one rejects a nanostores development benchmark as a product entry point. These
labels measure classification disagreements without silently deleting lineage or
changing an analyzer from validation/test observations.

Reviewed expected effort is 24.75 hours for mitt, 191.50 hours for CliWrap, and
170.50 hours for nanostores, compared with 31.50, 204.00, and 185.00 seed hours.
Across the three repository observations, reviewed effort is 386.75 hours and seed
effort is 420.50 hours; WAPE and aggregate bias are both 0.0873. This favorable
small-sample result is descriptive weak supervision, not an accuracy claim. The
model remains unchanged, the validation and test records remain unavailable for
tuning, and no independent review is claimed.

## Implemented post-7B5 analyzer-precision checkpoint

This checkpoint corrects the three general analyzer defects represented by the
seven reviewed exclusions. It does not change a schema, effort prior, normalization
rule, reviewed label, corpus partition, or estimator artifact:

1. `.NET` analyzer `0.3.2` requires persistence context for ambiguous
   `Execute`, `ExecuteAsync`, `Query`, and `QueryAsync` calls, while retaining
   direct database primitives and unambiguous persistence operations;
2. JavaScript analyzer `0.4.1` requires UI/full-stack framework context before
   state, effect, or form calls alone imply a UI surface, while retaining
   structural component, page, and JSX evidence; and
3. JavaScript analyzer `0.4.1` excludes test and benchmark hashbang scripts from
   product entry-point evidence while retaining ordinary CLI hashbangs.

Four memory-only analyzer tests cover the negative and positive boundaries. The
existing `seed-rules/0.2.1` mutation estimates retain identical numerical totals
and category totals across all 48 source states, and all 156 relational assertions
remain green.

The exact public-expansion snapshots were also reevaluated without rewriting the
frozen corpus or its original baseline reports:

| Partition | Repository | Reviewed expected | Original candidate | Corrected-analyzer candidate | Target mapping | Candidate mapping |
|---|---|---:|---:|---:|---:|---:|
| development | developit/mitt | 24.75 h | 31.50 h | 31.50 h | 14/14 | 14/14 |
| validation | Tyrrrz/CliWrap | 191.50 h | 204.00 h | 192.25 h | 63/67 | 63/63 |
| test | nanostores/nanostores | 170.50 h | 185.00 h | 169.75 h | 48/52 | 48/48 |

The validation and test exclusions motivated these corrections, so their improved
repository totals are contamination diagnostics, not held-out accuracy evidence.
Four eliminated CliWrap persistence targets and three eliminated nanostores
entry-point/UI targets no longer map. One positive nanostores manual-validation
target also no longer maps because removal of the false UI boundary changes the
deterministic work-item partition. Every current candidate item maps. These mapping
changes are disclosed rather than treated as zero error.

## Implemented measured-coverage checkpoint

This checkpoint closes the first measured-versus-declared coverage boundary without
changing `seed-rules/0.2.1`, its JSON artifact, any numerical prior, a reviewed
label, or corpus partition:

1. a language-neutral analyzer parses checked-in LCOV and Cobertura reports with
   DTD processing disabled, a 128 MiB input bound, and no test execution;
2. report bytes are checked against the common-scanner SHA-256 before any
   measurement is admitted;
3. reported source paths are used only to map maintained production files to the
   most specific .NET project or JavaScript/TypeScript package and are never copied
   into emitted evidence or diagnostics;
4. line, branch, and function counts and percentages are emitted as `measured`
   evidence, while unmatched, ambiguous, changed, unsafe, malformed, and unsupported
   artifacts remain unvalued with diagnostics;
5. measured coverage supersedes configured `declared-assumed` percentages within
   the same scope instead of being averaged or double-counted; and
6. `public-synthetic/0.4.0` expands the unchanged seed-rule baseline to 51 cases and
   170 passing assertions, adding measured 80% and 100% directionality plus
   measured-over-conflicting-declared precedence across low, expected, and high
   points and category isolation.

The coverage artifact is still not proof that it was generated from the analyzed
source snapshot. That staleness risk remains an explicit uncertainty. OpenCover,
JaCoCo, Istanbul JSON, and other report formats are not parsed by this checkpoint.
Mutation relations remain qualitative guardrails, not calibration labels.

## Change real-source follow-on

`efforthours-change-public-real-pilot/0.1.0` adds the first calibration record from
an immutable public open-source final delta. Its GuardClauses repository family
remains in the pre-existing development partition. Released alpha.2 provides the
frozen `change-seed/0.2.0` candidate, while one disclosed host-AI teacher provides
five separately reasoned target ranges under `change-ehe-work-item/1.0.0`.

Candidate and teacher expected totals are 4.25 and 4.00 hours. That close one-case
result is descriptive weak supervision only: no independent correction,
validation observation, numerical threshold, model fit, prior change, or accuracy
claim follows from it. The public artifacts retain exact base/head, tree, license,
final-delta, source-estimate, and corpus provenance without copying source.

## Change logical-marginality correction

The blind six-family public-real expansion subsequently exposed repository
work-item partitions being summed as if they were separate Change capabilities.
The source estimator advances to `change-seed/0.3.0` under the pre-admission
structural-correctness exception. General memory-only regressions cover repeated
production, test, and security partitions, distinct added capabilities, and a
meaningful modification whose repository classification delta is only 0.25 hour.

One existing or modified capability now receives one bounded evidence-derived
budget. Capped edit-region bands contribute logical path units to diminishing
tiers; distinct capabilities added on new final paths retain their positive
marginal. No repository prior, label, partition, threshold, or dependency changes.

Separate visible-only diagnostics compare 20.75 candidate with 19.00 teacher
expected hours in development and 15.75 with 16.00 in validation. Category,
interval, and exact-item mapping behavior is mixed and fully disclosed. These
one-teacher diagnostics do not establish accuracy or review maturity, and the test
comparison remains withheld.

## Change Stage A logical-admission checkpoint

Change rubric `change-ehe-work-item/1.1.0` and admission policy
`change-model-admission/0.2.0` now distinguish logical weak-supervision admission
from empirical validation. A disclosed host-AI teacher can support the former when
every estimate is built from distinct evidence-backed targets normally about 0.5
to 1.5 expected hours and all totals reconcile exactly. Its maturity remains
`teacher-estimate`; it is neither human nor independent review.

The released corpora retain their exact rubric-1.0.0 provenance. The separate
`stage-a-logical-review/0.1.0` artifact transparently audits all 28 eligible parent
targets into 45 rubric-1.1.0 tasks without rewriting a label or uncertainty range.
Candidate values were visible during decomposition, so the artifact proves logical
granularity rather than creating a new blind estimate.

The current `change-seed/0.6.0+seed-rules/0.3.0` baseline passes the frozen first
band for five public 4-to-32-hour changes across .NET, JavaScript, and TypeScript.
Reviewed and candidate expected totals are 38.00 and 38.50 hours, expected WAPE is
0.0526, and aggregate bias is +0.0132. The category, decomposition, interval,
performance, and qualitative gates are recorded in `CHANGE_MODEL_ADMISSION.md`.
The held-out expansion test comparison was not used.

This Change-specific decision does not alter the repository-estimator admission
policy elsewhere in this milestone. It admits only an experimental logical
baseline for one-to-several-day changes. Larger size bands, empirical production
accuracy, formal interval calibration, and production readiness remain separate
future gates; independent review remains optional corroboration for Change and
does not change teacher provenance unless actually completed.

## Contract boundaries

### Reviewed corpus

A calibration corpus contains reviewed target records, not repository source or
candidate estimates. Each record identifies:

- a stable repository identity and analyzed source digest;
- one estimation profile and baseline;
- exactly one development, validation, or test partition;
- the source estimator and estimate digest originally reviewed;
- source/license/distribution provenance;
- teacher, reviewer, and adjudicator provenance;
- a consistent rubric reference; and
- evidence-backed target work units with low, expected, and high hours.

Repository identity, rather than source digest or file, owns the partition. Every
revision and both profiles of one repository must remain in the same partition.
This prevents near-identical revisions or profile variants from leaking across
training and evaluation.

Records contain no rate card or cost labels. Calibration predicts EHE; pricing
remains an independent projection.

### Target work units

Each reviewed target names one or more source work-item IDs from the estimate that
was reviewed and retains evidence IDs, category, scope, rationale, and uncertainty.
A target normally has 0.5 to 8 expected hours. A target outside that range requires
an explicit size-exception reason.

Under rubric 1.1.0, a reviewed false positive or wholly excluded source target may
instead use exactly zero low, expected, and high hours. The exclusion must carry a
concrete rationale and `sizeException`. Zero cannot stand for ordinary uncertainty,
reuse, lower value, or discounting, and a partially positive zero range is invalid.

Source work items may be regrouped during review, but one source work-item ID may
belong to only one target in a record. This preserves a diagnosable mapping from
the seed decomposition to reviewed judgment.

### Candidate estimates

Candidate estimates remain ordinary canonical v1 `EstimateReport` documents. The
evaluator matches them to labels by source digest, profile, and baseline ID. It
does not require the candidate estimator to be the same estimator that produced
the originally reviewed decomposition.

Repository-total and category metrics remain available when a later estimator
changes work-item IDs. Item metrics include only fully matched targets, and the
report discloses target and candidate-item match coverage rather than silently
treating unmatched work as zero.

## Review states and provenance

Reviews use three explicit maturity states:

- `teacher-estimate`: consistent weak supervision, commonly from a strong AI;
- `reviewed`: a second reviewer has checked and corrected material judgments; and
- `adjudicated`: documented disagreements have been resolved.

Reviewers are identified by a stable, publishable ID and a role. Host-AI entries
also record the available model identity and version. A completion date is
provenance only; elapsed time, contributor identity, commit activity, and other
history metadata are never effort signals.

Every corpus record declares its data classification, redistribution decision,
source reference, revision/snapshot reference, and license expression. Private
records can use the same schema and local tooling but must remain outside the
public corpus and distributable model artifacts unless separately authorized.

## CLI surface

```text
eh estimate <repository-or-evidence.json> --no-rate [--compact] [--output <path>]

eh calibration scaffold <estimate.json>
  [--blind]
  [--compact]
  [--output <path>]

eh calibration compile <review-plan.json> <estimate.json>...
  [--compact]
  [--output <path>]

eh calibration review-scaffold <corpus.json>
  [--blind]
  [--compact]
  [--output <path>]

eh calibration review-compile <plan.json> <corpus.json>
  [--compact]
  [--output <path>]

eh calibration validate <corpus.json> [--compact] [--output <path>]

eh calibration evaluate <corpus.json> <estimate.json>...
  --partition <development|validation|test>
  [--compact]
  [--output <path>]

eh calibration mutations <suite.json> <estimate.json>...
  [--compact]
  [--output <path>]
```

`validate` checks both the JSON Schema and semantic invariants and emits a compact
corpus summary on success. Invalid input is reported on stderr with a nonzero exit
code.

`evaluate` requires an explicit partition so development results cannot be
mistaken for held-out results. It requires exactly one matching candidate for each
selected label record, validates every candidate estimate, and emits deterministic
JSON. Extra candidate reports are disclosed but do not affect metrics.

The calibration commands do not read repository source, Git history, or the
network. Physical file access belongs only to the CLI boundary; the reusable
authoring, compiler, validator, corpus-review, and mutation evaluators operate on
in-memory contract objects. `estimate` may analyze a repository, but writes only
when the caller gives an explicit output path.

Mutation semantics are versioned as `calibration-mutation-metrics/1.0.0`. A result
passes when `subject - reference` is within every supplied inclusive difference
bound. Assertions must name different known cases, use at least one bound, and
identify a category exactly when category scope is selected. The report records
the actual values, difference, bounds, candidate digests and estimators, and
per-assertion pass/fail state without a generated timestamp.

## Metric set

Metric semantics are versioned as `calibration-metrics/1.0.0`.

For low, expected, and high hours, EffortHours reports:

- sample count;
- summed reviewed and candidate hours;
- mean and median absolute error in hours;
- root mean squared error in hours;
- mean signed error in hours, where positive means overestimation;
- weighted absolute percentage error, `sum(abs(error)) / sum(reviewed)`; and
- aggregate bias rate, `sum(error) / sum(reviewed)`.

WAPE is used instead of ordinary MAPE because valid category observations may be
zero. Ratio metrics are omitted when summed reviewed effort is zero.

Interval diagnostics report:

- how often the candidate low/high range contains the reviewed expected point;
- how often it fully contains the reviewed low/high range; and
- mean candidate and reviewed interval widths.

These are agreement measurements against reviewed weak labels. They do not turn
the current low/high planning bounds into formal probability intervals.

Metrics are emitted at three levels:

1. repository-profile totals;
2. active repository-category observations, where either side is nonzero; and
3. fully matched reviewed target work units.

Per-record diagnostics retain candidate digests, match counts, unmatched target
IDs, unmatched candidate work-item IDs, and category mismatches for investigation.

## Determinism and safety

- Evaluation contains no generated timestamp.
- Candidate digests derive from canonical compact contract JSON.
- Records, categories, estimator versions, and diagnostic IDs use ordinal stable
  ordering.
- Decimal results are rounded to four places using midpoint-away-from-zero.
- The evaluator does not execute target code, load target dependencies, inspect
  Git history, access the network, or write to a repository.
- Unit tests construct every corpus and estimate in memory. Disk-backed corpus and
  estimate loading is tested only through `EffortHours.EndToEndTests`.

## Baseline and model-admission gates

The current deterministic `seed-rules/0.3.0` rules are the baseline every learned
candidate must beat.
A local model is considered only after the corpus contains multiple
redistributable repository families and a frozen repository-level test partition.

A candidate must, at minimum:

- improve held-out expected-hour WAPE at repository-total and category levels;
- avoid a material regression in median absolute error or signed bias;
- improve or preserve interval coverage without achieving it through unjustifiably
  wide ranges;
- retain acceptable results across .NET, JavaScript/TypeScript, and mixed
  repositories rather than only the aggregate;
- pass the existing formatting, duplication, generated-content, near-copy,
  compiler-disabled-code, data, security, declared/measured-coverage precedence,
  workspace, delivery, and history guardrails; and
- preserve evidence, rule/model version, and adjustment lineage in the final work
  items.

Numerical thresholds will be frozen only after the initial corpus exposes realistic
error scales. The test partition must not be used to tune those thresholds or
model hyperparameters.

## Later Milestone 7 slices

### Remaining 7B: corpus and baseline measurement

- Expand synthetic mutations from the initial bounded near-copy and
  compiler-disabled cases to semantic clones, unreachable/unreferenced behavior,
  accessibility-specific evidence, additional measured-coverage formats, larger
  multi-package dependency graphs, richer infrastructure, and additional delivery
  categories.
- Add diverse, redistributable real repositories with recorded licenses.
- Expand the consistent teacher reviews and complete the prepared independent
  correction handoff with a genuinely distinct reviewer.
- Preserve the frozen repository-level partitions before tuning.
- Extend the published seed-rule baseline measurements and disagreement notes.

### 7C: statistical and local-model candidates

- Try transparent per-category corrections and simple statistical baselines first.
- Evaluate structured/tabular ML only when it can improve on those baselines.
- Select ML.NET or ONNX based on measured runtime, package size, licensing, and
  training/export needs rather than adopting a runtime in advance.
- Add out-of-distribution detection and deterministic inference packaging.

### 7D: calibrated uncertainty and release review

- Calibrate range behavior on held-out data.
- Test ecosystem and repository-shape slices for hidden regressions.
- Document model provenance, training corpus versions, limitations, and effective
  date.
- Retain the seed-only offline fallback.

## Milestone 7A exit criteria

Milestone 7A is complete when:

- corpus, validation-summary, and evaluation-report schemas are published and
  round-trip deterministically;
- semantic validation rejects repository split leakage, duplicate mappings,
  missing lineage, invalid ranges, and unqualified oversized targets;
- evaluation reports item, category, total, bias, and interval metrics with
  disclosed match coverage;
- the CLI evaluates an explicit partition entirely offline;
- all ordinary unit tests remain storage-independent;
- focused end-to-end tests cover file loading, stdout/stderr separation, and exit
  codes; and
- documentation still describes the seed model as uncalibrated.

## Milestone 7A completion evidence

The implemented checkpoint passed locked restore, formatting verification, a
zero-warning Release build, 71 memory-only unit tests, and 15 disk-backed CLI
end-to-end tests. The `EffortHours.Tool` `0.7.0-alpha.1` package includes the reusable
`EffortHours.Calibration` assembly; the v1 calibration schemas remain embedded in
`EffortHours.Contracts`. A source audit confirmed that the unit suite added no
physical filesystem access.

## Milestone 7B1 completion evidence

The public-pilot checkpoint passes a zero-warning Release build, 75 memory-only
unit tests, and 17 disk-backed CLI end-to-end tests before the final release gate.
The `EffortHours.Tool` version advances to `0.7.0-alpha.2`. Fifteen v1 schemas are
embedded, including authoring-packet and review-plan contracts. Unit tests build
packets, plans, corpora, and compiler/evaluator results entirely in memory; only
the separate end-to-end suite exercises explicit file output and process loading.

The frozen corpus validates as three records and three repository families, one in
each partition. Its 99 reviewed targets retain all 228 source work-item references.
Combined teacher expected effort is 401.50 hours versus 805.75 seed hours; the
three-repository observation WAPE and aggregate bias are both 1.0068. These values
are a diagnostic baseline against preliminary weak labels, not an accuracy claim.

## Milestone 7B2 completion evidence

The review/mutation checkpoint passes a zero-warning Release build, 81 memory-only
unit tests, and 19 disk-backed CLI end-to-end tests before the final release gate.
The `EffortHours.Tool` version advances to `0.7.0-alpha.3`. Nineteen v1 schemas are
embedded. Unit tests construct every packet, plan, corpus, mutation suite,
candidate, and report in memory; only CLI tests and explicit public-artifact
generation use physical files.

The public synthetic baseline contains 8 canonical .NET cases and 14 assertions.
All seed assertions pass: formatting, exact excluded duplication, and conventional
generated output leave both expected total and production EHE unchanged; API,
tests, documentation, and integration variants increase the intended totals or
categories; test- and documentation-only variants leave production unchanged.
This established the first qualitative invariant/directionality guardrails, not
numerical accuracy. JavaScript/TypeScript and mixed mutation coverage was deferred
to 7B3.

The public-pilot blind packet covers all 3 records and 99 targets and pins source
corpus digest
`sha256:43b73a6e7ecc743612037349e07cd93c43fc258c926dac326734f304f4a75222`.
All prior hours and rationales are absent. No completed second-review plan is
checked in, so the source corpus correctly remains `teacher-estimate`.

## Milestone 7B3 completion evidence

The cross-ecosystem mutation checkpoint passes a zero-warning Release build, 83
memory-only unit tests, and 19 disk-backed CLI end-to-end tests before the final
release gate. The `EffortHours.Tool` version advances to `0.7.0-alpha.4`; the schema
count remains 19 because this slice reuses the 7B2 mutation contracts. A dedicated
memory-only regression proves TypeScript exact-copy normalization, and the existing
process-level mutation test now exercises low, expected, and high points.

The aggregate `public-synthetic/0.2.0` suite contains 30 canonical cases: 8 .NET,
9 JavaScript, 8 TypeScript, and 5 mixed. All 84 assertions pass
`seed-rules/0.2.1`. The baseline covers formatting and exact-copy invariance,
conventional generated exclusion, separately maintained generated customization,
API and UI directionality, represented unit tests and documentation, external
integrations, missing-category zero behavior, and production/test category
isolation. These remain qualitative relations, not reviewed numeric effort labels.

## Milestone 7B4 completion evidence

The behavior-and-delivery guardrail checkpoint passes a zero-warning Release
build, 84 memory-only unit tests, and 19 disk-backed CLI end-to-end tests before
the final release gate. The `EffortHours.Tool` version advances to
`0.7.0-alpha.5`; the schema count and active estimator remain unchanged.

The aggregate `public-synthetic/0.3.0` suite contains 48 canonical cases and 156
passing assertions. It retains every earlier relation and adds bounded renamed
near-copies, compiler-disabled C# boundaries, data and persistence across the
initial ecosystems, a .NET migration, security across the initial ecosystems,
declared-and-assumed 80% and 100% coverage, exact body reuse across a second
workspace package, CI, and container delivery. The checked baseline uses the same
`seed-rules/0.2.1` artifact and changes no numerical prior. A new in-memory pipeline
test independently proves the compiler-disabled exclusion without reading or
writing a physical fixture tree.

## Milestone 7B5 completion evidence

The corpus-expansion checkpoint passes a zero-warning Release build, 87
memory-only unit tests, and 19 disk-backed CLI end-to-end tests before the final
release gate. The `EffortHours.Tool` version advances to `0.7.0-alpha.6`; the schema
count remains 19, with four backward-compatible v1 effort-range schemas broadened
to serialize exact-zero reviewed exclusions.

The `efforthours-public-expansion/0.1.0` corpus has three immutable MIT-licensed
release snapshots, 133 lineage-complete teacher targets, frozen partitions, and
three checked-in `seed-rules/0.2.1` evaluation reports. Its blind packet pins
canonical source-corpus digest
`sha256:a411961985414eb65228991abdbe8165e5fc8abdeea92934902aacac7f405070`.
Together with the earlier pilot, EffortHours now publishes six repository families
and 232 blind review targets. No completed second-review plan is checked in, so all
public labels correctly remain `teacher-estimate`.

## Post-7B5 analyzer-precision completion evidence

The checkpoint passes a zero-warning Release build, 110 memory-only unit tests,
the complete disk-backed CLI end-to-end suite, and formatting verification. The
analyzer regression fixture uses `InMemoryRepository` exclusively. The unchanged
`seed-rules/0.2.1` mutation baseline still passes all 156 assertions, with no
numeric change to any of its 48 canonical estimates.

The C# file analyzer was split while the new data rule was introduced: data,
application-boundary, and service-boundary classification now have focused files.
The orchestrator is 354 lines, its former 750-line ratchet is removed, and every
new analyzer uses the ordinary 500-line ceiling.

Three deterministic reevaluation reports are checked in beside the frozen
public-expansion baseline. They preserve the corpus, reviewed targets, source
digests, and candidate estimator identity; their filenames identify analyzer
versions `0.3.2` and `0.4.1`, while candidate digests pin the exact outputs. Their
mapping loss and observation contamination are explicit; they do not advance
review maturity or justify model admission.

## Measured-coverage completion evidence

The checkpoint passes formatting verification, a zero-warning Release build, 146
memory-only unit tests, and 40 disk-backed CLI end-to-end tests. Coverage unit
fixtures use `InMemoryRepository` exclusively; they cover LCOV, Cobertura,
measured-over-declared precedence, deterministic output, schema validity, digest
changes, bounded reads, unmatched-source isolation, privacy, and DTD rejection.
The process-level test confirms stdout/stderr separation and the CLI process
boundary on a physical fixture, where disk access belongs.

The reproducible `public-synthetic/0.4.0` report contains 51 canonical candidates
and 170 passing assertions under the unchanged `seed-rules/0.2.1` catalog. The
three new candidate reports and their source digests are checked in beside complete
MIT-authored fixtures. No schema, package version, model artifact, numerical prior,
reviewed target, or review maturity changed.

## Frontend semantic-evidence completion

The August 10, 2026 checkpoint advances JavaScript analyzer `0.4.1` to `0.5.0`
and repository model `seed-rules/0.2.1` to `seed-rules/0.3.0`. The analyzer adds:

- conservative static Angular `@Component` metadata after a named import from
  `@angular/core`, including aliases, with literal strings/arrays, inline
  templates/styles, and repository-relative external asset references;
- unambiguous component/package ownership when an admitted asset has one owner,
  with shared assets retained as generic evidence;
- bounded HTML/template elements, forms, controls, bindings, directives, and
  custom-element evidence; and
- bounded CSS, SCSS, Sass, and Less rule/selector, responsive, token, animation,
  and theme evidence.

All reads reuse the common scanner's admitted path, size, encoding, and SHA-256
boundary. Generated, vendored, minified, test, documentation, binary, and ignored
build-output assets do not become semantic UI facts. Output retains locations and
counts, never source excerpts. The scanners do not execute TypeScript or
configuration, render, compile frameworks, run preprocessors, establish runtime
reachability, or perform an accessibility audit.

`seed-rules/0.3.0` removes `asset-lines` from the UI rule and replaces it with six
bounded semantic drivers: template structure, template bindings, stylesheet
structure, responsive surfaces, design-token units, and animation/theme surfaces.
Every non-UI numerical prior is identical to 0.2.1. The new UI values are
transparent preliminary priors, not label-fitted calibration. The artifact digest
is `sha256:e8bce2f76c97564919ab6be41f1cfd6b222d531a4dbd08a8b22c7abe6b1eebdf`.

Public suite `0.5.0` adds five synthetic frontend states and 22 relations. Its 56
canonical candidates pass all 192 low/expected/high and category relations under
`seed-rules/0.3.0`. Formatting and an exact stylesheet copy remain invariant;
meaningful template/style semantics and static Angular component ownership raise
UI EHE. Re-evaluating the prior 51 states produces identical numeric ranges, so
the earlier 0.1.0 through 0.4.0 reports remain frozen. This checkpoint changes no
reviewed label, partition, review maturity, threshold, or ML admission decision.
