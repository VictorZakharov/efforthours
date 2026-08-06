# Milestone 7: Calibration and Local Models

## Status

Milestone 7A and the Milestone 7B1 through 7B3 public-pilot, review, and mutation
checkpoints were implemented on August 6, 2026. They establish the versioned review
corpus, low-cost authoring and compilation boundaries, deterministic offline
evaluation, initial licensed labels, exact-digest subsequent review, and
cross-ecosystem relational model guardrails needed before Fairbill adopts any
learned model. The seed estimator remains `experimental-uncalibrated`; this
milestone does not make its current hours production-ready.

The pilot still has one host-AI teacher and no independent correction. Milestone
7B is therefore in progress: broader licensed repository coverage, more complex
mutation families, and an actual independent review are required before numerical
admission thresholds or model training. The presence of second-review tooling must
not be confused with completion of that review.

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
6. `fairbill calibration validate` and `fairbill calibration evaluate`; and
7. memory-only unit tests plus focused disk-backed CLI tests.

The first slice deliberately does not tune `seed-rules/0.2.0`, publish an accuracy
claim, train a model, call a remote provider, or implement PR/commit estimation.

## Implemented Milestone 7B1 scope

The public-pilot slice adds no ML runtime or training dependency. It adds:

1. a v1 unreviewed authoring-packet contract and JSON Schema;
2. `fairbill calibration scaffold`, with visible reference values or `--blind`;
3. a v1 completed review-plan contract and JSON Schema;
4. `fairbill calibration compile`, which verifies exact source-estimate digests,
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
2. `fairbill calibration review-scaffold`, whose blind mode hides prior ranges,
   rationale, uncertainty decisions, and totals;
3. a v1 completed corpus-review plan and JSON Schema;
4. `fairbill calibration review-compile`, which pins the exact source-corpus
   digest, requires every record and target, rejects reused reviewer identities,
   and preserves all structural lineage while advancing maturity;
5. v1 mutation-suite and mutation-report contracts;
6. `fairbill calibration mutations`, with deterministic repository/category
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
fairbill estimate <repository-or-evidence.json> --no-rate [--compact] [--output <path>]

fairbill calibration scaffold <estimate.json>
  [--blind]
  [--compact]
  [--output <path>]

fairbill calibration compile <review-plan.json> <estimate.json>...
  [--compact]
  [--output <path>]

fairbill calibration review-scaffold <corpus.json>
  [--blind]
  [--compact]
  [--output <path>]

fairbill calibration review-compile <plan.json> <corpus.json>
  [--compact]
  [--output <path>]

fairbill calibration validate <corpus.json> [--compact] [--output <path>]

fairbill calibration evaluate <corpus.json> <estimate.json>...
  --partition <development|validation|test>
  [--compact]
  [--output <path>]

fairbill calibration mutations <suite.json> <estimate.json>...
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

For low, expected, and high hours, Fairbill reports:

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
  estimate loading is tested only through `Fairbill.EndToEndTests`.

## Baseline and model-admission gates

The current deterministic `seed-rules/0.2.1` rules are the baseline every learned
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
- pass the existing formatting, duplication, generated-content, and history
  invariants; and
- preserve evidence, rule/model version, and adjustment lineage in the final work
  items.

Numerical thresholds will be frozen only after the initial corpus exposes realistic
error scales. The test partition must not be used to tune those thresholds or
model hyperparameters.

## Later Milestone 7 slices

### Remaining 7B: corpus and baseline measurement

- Expand synthetic mutations to near-duplicates, dead-code shapes, data,
  persistence, security, coverage levels, more realistic multi-package boundaries,
  and additional delivery categories.
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
end-to-end tests. The `Fairbill.Tool` `0.7.0-alpha.1` package includes the reusable
`Fairbill.Calibration` assembly; the v1 calibration schemas remain embedded in
`Fairbill.Contracts`. A source audit confirmed that the unit suite added no
physical filesystem access.

## Milestone 7B1 completion evidence

The public-pilot checkpoint passes a zero-warning Release build, 75 memory-only
unit tests, and 17 disk-backed CLI end-to-end tests before the final release gate.
The `Fairbill.Tool` version advances to `0.7.0-alpha.2`. Fifteen v1 schemas are
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
The `Fairbill.Tool` version advances to `0.7.0-alpha.3`. Nineteen v1 schemas are
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
`sha256:216ee9e2289290c43bb843a51cacd9b8cb8d5da0d9da50f90ff77cf0ed11d5c0`.
All prior hours and rationales are absent. No completed second-review plan is
checked in, so the source corpus correctly remains `teacher-estimate`.

## Milestone 7B3 completion evidence

The cross-ecosystem mutation checkpoint passes a zero-warning Release build, 83
memory-only unit tests, and 19 disk-backed CLI end-to-end tests before the final
release gate. The `Fairbill.Tool` version advances to `0.7.0-alpha.4`; the schema
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
