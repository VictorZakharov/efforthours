# Milestone 7: Calibration and Local Models

## Status

Milestone 7A was implemented on August 6, 2026. It establishes the
versioned review corpus, deterministic offline evaluation, and acceptance gates
needed before Fairbill adopts any learned model. The seed estimator remains
`experimental-uncalibrated`; this milestone does not make its current hours
production-ready.

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
fairbill calibration validate <corpus.json> [--compact]

fairbill calibration evaluate <corpus.json> <estimate.json>...
  --partition <development|validation|test>
  [--compact]
```

`validate` checks both the JSON Schema and semantic invariants and emits a compact
corpus summary on success. Invalid input is reported on stderr with a nonzero exit
code.

`evaluate` requires an explicit partition so development results cannot be
mistaken for held-out results. It requires exactly one matching candidate for each
selected label record, validates every candidate estimate, and emits deterministic
JSON. Extra candidate reports are disclosed but do not affect metrics.

Neither command reads repository source, Git history, or the network. Physical
file access belongs only to the CLI boundary; the reusable validator and evaluator
operate on in-memory contract objects.

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

The deterministic seed rules are the baseline every learned candidate must beat.
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

### 7B: corpus and baseline measurement

- Build small synthetic archetypes and mutation families.
- Add diverse, redistributable real repositories with recorded licenses.
- Run consistent teacher reviews and independent correction.
- Freeze repository-level partitions before tuning.
- Publish seed-rule baseline measurements and disagreement notes.

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
