# Calibration, review, and local-model admission

## Current status

EffortHours has versioned corpus, authoring, review, mutation, and evaluation
infrastructure. The repository estimator remains `experimental-uncalibrated`, and
no local ML training or inference dependency has been selected.

The public repository corpora contain 21 repository families and 2,262 teacher
targets. They share one disclosed host-AI teacher and have no completed independent
correction, so their maturity remains `teacher-estimate`. They are logical weak
supervision, not historical labor, literal ground truth, or empirical production
validation.

The separate `efforthours-public-readiness/0.1.0` sampling plan freezes 33
additional public MIT, Apache-2.0, and BSD-3-Clause source families across the
complete .NET, JavaScript/TypeScript, and mixed development/validation/test
matrix. It was frozen before candidate totals. Reproduction checkpoint `0.2.0`
verifies all 33 exact trees, emits strict-blind capability packets for all 15
development families, and records nine validation plus nine test families as
source-verified but `withheld-not-run` and `not-authored`. Development checkpoint
`0.3.0` freezes rubric-complete judgments for all 2,030 development capabilities,
including 162 explicit exclusions, before unlocking the matching candidate
reports. Validation and test remain unopened.

Against those 15 development records, `seed-rules/0.4.0` totals 40,076.50 expected
hours versus 40,564.00 reviewed hours. Repository-total expected WAPE is `0.2365`,
aggregate bias is `-0.0120`, 14 of 15 reviewed expected totals fall inside the
candidate intervals, and 13 of 15 reviewed ranges are fully covered. Work-item
lineage matches all 2,030 targets and 11,161 candidate work items. These are
development diagnostics from one teacher, not a fitted correction, blind
validation result, admission decision, or production-accuracy claim.

Development preflight `0.4.0` tested a bounded transparent scope-marginality
design and failed closed. It improved repository expected WAPE to `0.1963` and
mean normalized width to `0.4808`, but failed aggregate bias (`0.0672` versus the
seed's `0.0120`), ordinary per-family consistency (`0.7333`), and matched-target
expected coverage (`0.2611`). No candidate manifest was frozen, and no validation
or test labels or candidate outputs were opened.

Development preflight `0.5.0` freezes the next exact candidate,
`logical-capability/0.1.0`. Its transparent evidence-unit and fitted-table design
passes all 16 numerical gates: repository expected WAPE is `0.1141`, absolute
aggregate bias is `0.0108`, repository expected coverage is `0.8667`, and
matched-target coverage/normalized width are `0.8202/0.7407`. All 12 operational
gates remain deliberately unrun and non-passing. The finite candidate manifest is
therefore not frozen, and validation/test labels and candidate outputs remain
unopened.

Change EHE has a separate limited Stage A logical-admission decision. Its exact
size, ecosystem, metric, and performance boundary is defined in
`CHANGE_MODEL_ADMISSION.md`; it does not calibrate repository EHE or later Change
ecosystem extensions.

## Objective

Calibration turns consistent logical review into measurable weak supervision
without obscuring provenance or confusing the labels with actual work history.
The system must:

- record evidence-backed target work units;
- keep public and private material separable;
- isolate development, validation, and test partitions by repository family;
- compare canonical candidate estimates with frozen labels offline;
- report item, category, repository-total, mapping, bias, and interval behavior;
- preserve disagreement and unmatched decomposition rather than treating it as
  zero; and
- admit a correction or local model only when it improves frozen held-out results
  without weakening deterministic guardrails and explanation lineage.

## Corpus boundary

A calibration corpus contains reviewed target records, not repository source or
candidate estimate bodies. Each record identifies:

- a stable repository family and analyzed source digest;
- one estimation profile and contractor baseline;
- exactly one `development`, `validation`, or `test` partition;
- the source estimator and canonical estimate digest originally reviewed;
- source, license, and redistribution provenance;
- teacher, reviewer, and adjudicator provenance;
- one consistent rubric reference; and
- target work units with categories, scopes, evidence, rationale, uncertainty,
  and low/expected/high hours.

Repository family owns the partition. Every revision and both profiles from the
same family stay in that partition, preventing near-identical snapshots or profile
variants from leaking across training and evaluation.

Calibration records contain no rate card or cost label. Pricing is not a model
target or evaluation metric.

Private records may use the public schema and local tooling, but they must remain
outside public corpora and distributable model artifacts unless separately
authorized.

## Target work units

A target names one or more source work-item IDs from the estimate that was
reviewed. One source work item may belong to only one target in a record.

Targets normally contain 0.5 to 8 expected hours. Larger targets require an
explicit size exception. A stricter model-admission policy may require smaller
decomposition; current Change Stage A review normally uses 0.5-to-1.5-hour tasks.

Under rubric `ehe-work-item/1.1.0` and its Change equivalent, a false-positive or
wholly excluded source target may use exactly `0/0/0` only when both its rationale
and `sizeException` explain the exclusion. A partially positive zero range is
invalid. Zero cannot mean ordinary uncertainty, reuse, lower value, or a discount.

## Candidate boundary

Candidates remain ordinary canonical v1 estimate reports. Repository evaluation
matches source digest, profile, and baseline; Change evaluation additionally pins
immutable final-delta provenance.

The candidate estimator need not be the version that produced the reviewed source
decomposition. Repository-total and category metrics remain meaningful after work-
item IDs change. Item metrics include only fully matched targets, and reports
disclose target and candidate mapping coverage plus unmatched IDs and category
disagreements.

## Authoring and review workflow

### First review

`calibration scaffold` projects a canonical estimate into an explicitly
`unreviewed` packet. Authoring version `0.2.0` groups candidate partitions into one
capability summary. Reference mode exposes candidate values only as suggestions;
strict-blind mode removes candidate hours, totals, confidence, explanations,
source work-item IDs and partition counts, plus professionalization-gap IDs.

A separately authored completed plan provides a decision for every represented
capability. `calibration compile` verifies the exact source-estimate digest,
compiler identity, complete capability coverage, unique work-item assignment, and
evidence lineage before producing a corpus. Scaffolds are never valid corpora by
themselves.

### Subsequent review

`calibration review-scaffold` creates an explicitly unreviewed packet from an
existing corpus. Blind mode removes prior ranges, rationale, uncertainty
decisions, and totals.

`calibration review-compile` pins the exact source-corpus digest, requires an
`accept` or `replace` decision for every record and target, rejects reviewer IDs
already present in the source provenance, preserves structural lineage, and
prevents maturity downgrades.

An accepted target is copied only after deliberate review. A replacement supplies
a complete range, rationale, uncertainty, and size exception where needed.

## Review maturity and provenance

The explicit states are:

- `teacher-estimate`: consistent weak supervision, commonly from a strong AI;
- `reviewed`: a distinct reviewer has checked and corrected material judgments;
  and
- `adjudicated`: documented disagreements have been resolved by an adjudicator.

A disclosed host-AI teacher may support an explicitly labeled logical gate when
the applicable policy requires exact reconciliation from small evidence-backed
tasks and every frozen gate passes. This does not upgrade maturity, imply human or
independent review, establish empirical accuracy, or calibrate probability
intervals.

Reviewer IDs must be stable and publishable. Host-AI provenance records the
available provider/model/version identity and whether candidate values or source
context were visible. Dates are provenance only; elapsed time, identity, commits,
and activity never become EHE signals.

## Mutation guardrails

Mutation suites are qualitative model guardrails, not reviewed effort labels and
not numerical training targets. Each assertion compares one low, expected, or
high repository/category point and defines an inclusive bound on:

```text
subject estimate - reference estimate
```

They protect invariance, directionality, bounded marginality, and category
isolation for formatting, generated and excluded content, exact duplication,
specified near-equivalent shapes, reachability boundaries, tests, documentation,
coverage, data, security/accessibility, integrations, delivery, and supported
ecosystem behavior.

Missing categories are treated as zero for comparison. A failed assertion still
produces a complete deterministic report and uses the dedicated regression exit
code. Mutation results cannot establish absolute-hour accuracy.

The current public mutation artifacts and their exact counts live under
`calibration/mutations/public-synthetic/`; do not duplicate those changing totals
in living guidance.

## CLI surface

```text
eh calibration scaffold <estimate.json> [--blind] [--output <path>]
eh calibration compile <review-plan.json> <estimate.json>... [--output <path>]
eh calibration review-scaffold <corpus.json> [--blind] [--output <path>]
eh calibration review-compile <plan.json> <corpus.json> [--output <path>]
eh calibration validate <corpus.json> [--output <path>]
eh calibration evaluate <corpus.json> <estimate.json>...
  --partition <development|validation|test> [--output <path>]
eh calibration mutations <suite.json> <estimate.json>... [--output <path>]

eh calibration change-scaffold <change-estimate.json> [--blind]
eh calibration change-compile <review-plan.json> <change-estimate.json>...
eh calibration change-evaluate <corpus.json> <change-estimate.json>...
  --partition <development|validation|test>
```

Commands validate JSON Schema and semantic invariants. Evaluation requires an
explicit partition, exactly one matching candidate for each selected label, and
deterministic output. Extra candidates are disclosed but do not affect metrics.

Calibration commands operate on supplied documents without target source, Git
history, provider calls, or network access. Reusable compilers and evaluators use
in-memory contract objects; physical loading belongs to the CLI boundary.

## Evaluation metrics

Metric semantics are versioned as `calibration-metrics/1.0.0`.

For low, expected, and high hours, evaluation reports:

- sample count and summed reviewed/candidate hours;
- mean and median absolute error;
- root mean squared error;
- signed error, where positive means candidate overestimation;
- weighted absolute percentage error,
  `sum(abs(candidate - reviewed)) / sum(reviewed)`; and
- aggregate bias, `sum(candidate - reviewed) / sum(reviewed)`.

WAPE is used instead of ordinary MAPE because valid category observations may be
zero. Ratio metrics are omitted when the reviewed denominator is zero.

Interval diagnostics report reviewed-expected coverage, full reviewed-range
coverage, and mean candidate/reviewed width. They measure agreement with weak
labels and do not make planning bounds formal probability intervals.

Metrics are emitted for repository-profile totals, active categories, and fully
matched targets. Per-record diagnostics retain candidate digests, mapping counts,
unmatched IDs, and category mismatches.

## Determinism and safety

- Evaluation has no generated timestamp.
- Candidate and corpus identities use canonical compact JSON digests.
- Records, categories, versions, and diagnostics use ordinal stable ordering.
- Decimal metrics round to four places, midpoint away from zero.
- Calibration does not execute target code, resolve dependencies, inspect Git
  history, access the network, or write into target repositories.
- Ordinary calibration tests construct corpora, plans, estimates, and reports in
  memory. Disk-backed loading remains in the end-to-end suite.
- Public corpora require recorded provenance and redistributable source terms.

## Model-admission policy

The exact repository gate is frozen as
`repository-model-admission/1.0.0` in `MODEL_ADMISSION.md`. It requires at least
33 repository families across explicit .NET, JavaScript/TypeScript, and mixed
development/validation/sealed-test cells; a finite predeclared candidate set;
point, category, mapping, bias, and range-sharpness thresholds; and complete
mutation, explanation, safety, determinism, latency, memory, and package gates.

Coverage alone cannot admit an unhelpfully broad range. A candidate must retain
reviewed-point coverage while materially reducing normalized width relative to
the seed baseline. Ranges remain empirical planning bounds rather than formal
probability intervals.

Current public repository records satisfy the complete development-label boundary
and numerical development-preflight boundary, but not the operational preflight,
candidate-freeze, blind-validation, or sealed-test boundary. The 15 development
families may be compared diagnostically, but no validation/test comparison or
admission decision is authorized. `seed-rules/0.4.0` remains the shipped estimator
and required fallback.

The public-readiness cohort satisfies the planned family, shape, and size
allocation, exact source reproduction, and complete development-label boundary.
All nine validation and nine test families remain unlabeled. Admission still
requires a finite candidate freeze, blind validation, sealed one-time test reveal,
and every numerical and operational gate. Source verification must not be reported
as labeled calibration evidence.

Transparent per-category corrections and simple statistical baselines come before
an ML runtime. ML.NET or ONNX is selected only from measured runtime, package,
license, training/export, and determinism needs. Any admitted model must retain an
offline seed fallback and out-of-distribution behavior.

Change model admission remains separate and follows the progressive size-band and
final-delta policy in `CHANGE_MODEL_ADMISSION.md`.

## Next evidence required

- all 12 operational preflight gates for the exact `logical-capability/0.1.0`
  implementation and model artifact, without retuning its numerical design;
- only if they pass, a finite candidate manifest with exact model/configuration identities,
  resource measurements, and a frozen selection rule;
- blind validation labels that remain unavailable until candidate freeze;
- sealed test labels with a precommitted digest and custody record;
- more exact small-task teacher decompositions with honest context provenance;
- optional independent replication where available;
- the already-frozen family matrix and held-out thresholds in
  `MODEL_ADMISSION.md`;
- separately governed empirical production observations that are never used as
  activity multipliers; and
- calibrated range semantics before any probability interpretation.

Detailed historical review results remain in `MODEL_REVIEWS.md` and the immutable
artifacts under `calibration/`.
