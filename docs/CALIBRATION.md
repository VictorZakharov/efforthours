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
gates were initially unrun. Operational checkpoint `0.6.0` later retired that
identity after specification-comprehension bias reached `-0.2505` against the
frozen `0.20` limit; seven measured gates were not run after rejection.

Successor preflight `0.7.0` gives the bounded correction a new
`logical-capability/0.2.0` identity and again passes all 16 numerical gates:
repository expected WAPE is `0.1137`, absolute aggregate bias is `0.0094`,
repository expected coverage is `0.8667`, and matched-target coverage/normalized
width are `0.8217/0.7406`. Operational checkpoint `0.8.0` passes all five
development-computable gates, including material-category agreement with
specification-comprehension bias `-0.1253`. Measured checkpoint `0.9.0` then
retires the exact candidate. Public mutation suite `0.8.0` passes `314/339`
assertions, and raw Windows candidate JSON differs from matching Linux/macOS
bytes by CRLF versus LF. All latency, peak-memory, installed-package, and
scanner/fingerprint gates pass. No candidate manifest is frozen, and
validation/test source, candidate outputs, and labels remain unopened.

Successor `logical-capability/0.3.0` has new model, estimator, feature-contract,
scorer, and implementation identities. Development checkpoint `1.0.0` passes all
16 numerical gates with repository expected WAPE `0.1009`, absolute aggregate
bias `0.0103`, repository coverage `0.8000`, and matched-target
coverage/normalized width `0.7601/0.7321`. Checkpoint `1.1.0` passes all five
development-computable operational gates and a standalone evaluation of the
unchanged public mutation suite at `339/339`. Measured checkpoint `1.2.0` passes
all seven remaining gates and all 12 total operational gates on Windows, Linux,
and macOS. It freezes the seed plus sole challenger, exact resource budgets, and
validation-selection rule. Checkpoint `1.3.0` freezes 2,747 strict-blind
validation targets before projecting the challenger. Expected WAPE improves from
seed `0.2279` to `0.0940`, but six frozen median/family-error, coverage, width,
and material-category gates fail. The candidate is retired without test
disclosure; no candidate is admitted or shipped, and `seed-rules/0.4.0` remains
the product estimator and fallback.

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
eh calibration diagnose <corpus.json> <estimate.json>...
  --partition <development|validation|test> [--output <path>]
eh calibration uncertainty-features <estimate.json> <evidence.json>
  [--output <path>]
eh calibration uncertainty-structure <estimate.json> <evidence.json>
  [--output <path>]
eh calibration uncertainty-graph <estimate.json> <evidence.json>
  [--output <path>]
eh calibration uncertainty-evaluate <development-corpus.json> <features.json>...
  [--output <path>]
eh calibration uncertainty-structure-evaluate <development-corpus.json>
  <structural-features.json>... [--output <path>]
eh calibration uncertainty-graph-evaluate <development-corpus.json>
  <graph-features.json>... [--output <path>]
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

### Residual diagnosis

`eh calibration diagnose` emits the versioned
`calibration-residual-diagnostic/1.0.0` report for an explicitly selected corpus
partition. It is a development/review tool, not a model fit or admission result.
Opened validation labels may be diagnosed after a frozen decision, but they cease
to be a fresh holdout for any successor informed by the result. A sealed test
partition remains off limits until its separate authorization boundary is met.

The diagnostic records candidate-minus-reviewed low, expected, and high deltas;
reviewed-expected interval miss direction and distance; candidate range symmetry;
and raw versus expected-normalized candidate-to-reviewed repository-width
correlation. It ranks repositories, categories, and matched-target or
unmatched-candidate components by absolute expected residual.
`materialContributor` marks the smallest largest-first set whose gross residual
share reaches the fixed `0.8` threshold; all remaining components are retained.

Category and component signed residuals reconcile to their repository total at
the diagnostic's `0.0001`-hour reconciliation precision. Incomplete mappings,
category mismatches, unmatched candidate work items, cancellation between positive
and negative residuals, reviewed size exceptions, and reviewed targets above the
ordinary eight-hour boundary remain explicit. Each component retains reviewed and
candidate evidence IDs and a compact ordered leaf projection. A leaf carries its
stable ID, scope, category, range, complexity, confidence, evidence count/digest,
and uncertainty count; the candidate report digest remains the authority for its
full explanation. This avoids copying an entire large candidate report while
still letting a reviewer expand a multi-thousand-hour aggregate into the normally
small work items that produced it.

The residual diagnostic does not change `seed-rules/0.4.0`, revive a rejected
candidate, fit uncertainty widths, or claim that teacher ranges are empirical
delivery distributions.

### Uncertainty feature contract

`repository-uncertainty-features/1.0.0` freezes the first label-independent
repository uncertainty vocabulary. It can be projected with:

```text
eh calibration uncertainty-features <estimate.json> <evidence.json> \
  --compact --output <features.json>
```

Both inputs must be saved schema-valid documents with the same immutable source
digest. The command is offline and reads no corpus or labels, performs no source
scan, fits no model, and changes no expected value or source range. Its report uses
`calibration-uncertainty-features.schema.json`, pins canonical estimate, evidence,
and feature-contract digests, and emits stable feature availability and evidence
IDs without summaries, source excerpts, or host paths.

The embedded `symmetric-planning-interval/1.0.0` policy fixes these semantics
before a successor fit:

- intended coverage is `0.80` for reviewed expected points on a held-out cohort;
- this is an operational weak-label metric, not a formal probability interval;
- low/high are symmetric around expected except for the zero-hour floor;
- directional contingencies remain separate from the primary interval;
- a material unresolved fact must strictly widen a comparable interval;
- weaker confidence, inferred provenance, parser evidence, or explicit ambiguity
  must not narrow a comparable interval; and
- a missing feature value is explicit and does not widen by itself.

Each vector also preserves its source category, expected size, current work-item
complexity, resolved ecosystem tags, parent, and correlation group as grouping
context. The width-constrained offline features
are source confidence, inferred supporting-fact share, worst parser-risk level,
explicit uncertainty-reason count, and material unresolved count. A missing
supporting evidence reference or an evidence-less represented work item is
material. An analyzer can also explicitly mark a fact with
`uncertainty:material-access-gap` or a namespaced suffix.

Non-execution/non-verification tags, dynamic and unsupported boundary counts,
resolved-fact count, aggregate branch density, and aggregate public-interface
concentration are diagnostic candidates. In particular, tags such as
`target-code:not-executed`, `terraform-execution:not-performed`, and
`runtime-correctness:not-verified` do not change interval width merely because
the product is offline-first. Dynamic and aggregate code-shape features must earn
held-out value rather than proxy repository size or reward accidental complexity.

At the v1 feature-contract freeze, per-callable complexity, size, and nesting
distributions; local fan-in, fan-out, and dependency cycles; analyzer-ambiguity
concentration; per-cell sample support; and OOD scoring were explicitly deferred.
They remain absent from that frozen work-item vector. Later support, structural,
and graph artifacts extend the pre-fit diagnostic evidence without rewriting v1
or silently adding a fitted feature. The source report's existing range and a
symmetry-compliance flag remain diagnostic only; none of these checkpoints changes
`seed-rules/0.4.0` or fits a successor.

### Structural uncertainty diagnostics

`repository-uncertainty-structural-features/1.0.0` separately freezes the first
label-independent callable-shape artifact. It can be projected with:

```text
eh calibration uncertainty-structure <estimate.json> <evidence.json> \
  --compact --output <structural-features.json>
```

The same digest-match, offline, privacy, and no-label rules apply as for
`uncertainty-features`. The report validates against
`calibration-uncertainty-structural-features.schema.json` and pins the canonical
contract digest
`sha256:a186c6e61ef7fcbd294ca4de27ed8504b313599e96246d8bcc7321eda04204ab`.

Analyzer evidence contract `callable-structural-metrics/1.0.0` fixes these local
project/package measurements before any reviewed residual is consulted:

- callable non-comment syntax-token count, bounded decision-complexity points,
  and control-nesting depth each emit nearest-rank p50, p90, and maximum;
- threshold shares use strictly more than `200` tokens, `10` complexity points,
  and `4` nesting levels;
- decision complexity starts at one and adds bounded control decisions and logical
  AND/OR expressions; nesting excludes logical expressions and nested callable
  bodies; and
- callable measurement coverage and source-file parser ambiguity remain separate
  so an unsupported parser path cannot masquerade as a low-complexity sample.

.NET analyzer `0.3.5` measures Roslyn C# method declarations with executable
bodies. JavaScript analyzer `0.5.2` measures Acornima JavaScript/JSX function and
arrow-function ASTs. TypeScript/TSX and parser fallbacks retain detected-callable
counts but emit no guessed distributions, producing explicit partial or
unavailable coverage.

Work-item projection takes the maximum shape/ambiguity value across contributing
local scopes and the minimum callable-coverage value. It does not sum repository
totals. All 14 fields are `diagnostic-only`: they do not reward complexity, change
expected EHE, widen an interval, alter a reviewed label, or fit a model. Local
fan-in/fan-out, dependency cycles, and interface concentration are frozen in the
separate graph contract below.

The label-independent evaluation boundary is separately frozen as
`uncertainty-structural-evaluation-policy/1.0.0`, digest
`sha256:2f3dc2417747ac8557744eb2d59c3b2e816158620eb26086f383d3809610f610`.
Before any public reviewed residual is loaded, it fixes target aggregation, expected
residual direction, and inclusive bucket boundaries for all 14 fields. Shape and
ambiguity use the maximum across a reviewed target's source work items; callable
coverage uses the minimum. The command is:

```text
eh calibration uncertainty-structure-evaluate <development-corpus.json> \
  <structural-features.json>... --compact --output <evaluation.json>
```

`uncertainty-structural-feature-evaluation/1.0.0` reuses the repository-held-out
nearest-rank q80 protocol below. It predeclares a pooled incremental gate requiring
at least one conditioned prediction, no coverage loss, no normalized-width growth,
and no interval-miss growth. Correlation direction, bucket-direction violations,
and repository coverage regressions remain separately visible. The evaluator
refuses validation/test records, fits no production model, and cannot alter labels,
seed estimates, or intervals. The subsequent 15-repository, 2,030-target checkpoint
rejects all 14 fields as direct width drivers: every variant narrows the baseline
but loses coverage and increases interval miss. Median decision complexity and
median nesting retain the cleanest expected ordering, so the measurements remain
useful diagnostics without admission.

### Graph uncertainty diagnostics

`repository-uncertainty-graph-features/1.0.0` freezes 14 label-independent
repository graph and interface-distribution fields for .NET and JavaScript. It can
be projected with:

```text
eh calibration uncertainty-graph <estimate.json> <evidence.json> \
  --compact --output <graph-features.json>
```

The projector applies the same immutable source-digest match, offline operation,
privacy boundary, and no-label rule as the other uncertainty projectors. The
report validates against `calibration-uncertainty-graph-features.schema.json` and
pins canonical contract digest
`sha256:3b41238130578b02e3c1b3426103cbbc5f1b6656efafe50fe53c336b94570200`.

The frozen graph population and measurements are:

- one node for every declared .NET project or JavaScript package, including
  zero-degree nodes;
- one directed edge for each resolved same-ecosystem local project/package
  reference, deduplicated by source and target; configuration references and
  unresolved or outside-scope targets are not edges;
- nearest-rank p50, p90, maximum, and the share strictly above `3` for local
  fan-in and fan-out;
- the share of nodes in a directed strongly connected component with more than
  one node or a self-edge, plus the largest cyclic component's share of all nodes;
- nearest-rank p50, p90, maximum, and the share strictly above `0.5` for local
  public-interface concentration; .NET uses public types plus public methods over
  all types plus methods, while JavaScript uses exports over functions, methods,
  classes, interfaces, type aliases, and enums, with each ratio capped at `1`; and
- explicit unavailable/not-applicable interface states, so missing or
  incompatible structure evidence cannot look like a private interface; any
  incompatible supported scope makes the repository interface distribution
  unavailable instead of silently using a partial sample.

Raw node, edge, and reference counts are audit context only. They are never
interval drivers or repository-size proxies. The report retains every node,
deduplicated edge and evidence ID, and each source work item's resolved node IDs so
a later target aggregation can be reviewed without reconstructing graph identity.
All 14 fields are `diagnostic-only`; no field changes expected EHE, source ranges,
labels, or `seed-rules/0.4.0`.

An evidence-only preflight across the 15 public development snapshots found 347
supported nodes and 657 deduplicated local edges. Of those nodes, 316 supplied a
usable public-interface measurement, 15 had no source-structure evidence, and 16
had no declaration denominator. One repository contained six cyclic nodes. Of
11,161 source work items, 10,048 mapped to at least one same-ecosystem graph node
and 1,113 remained explicitly unmapped. This verifies coverage and variation only:
no reviewed hours, residuals, or feature outcomes were consulted.

The separate label-independent evaluation boundary is frozen as
`uncertainty-graph-evaluation-policy/1.0.0`, digest
`sha256:f742039c129f02e6e423c31cf486f0c33e4b2e20b329da27294786cc9385c0db`.
Before any reviewed residual is loaded, it fixes these target semantics:

- union the unique node IDs mapped from all source work items in a reviewed target;
  repeated work items and overlapping mappings cannot multiply a node;
- retain every complete category-matched target and its full source range when
  some or all work items are unmapped; graph features use the mapped-node subset,
  and a target with no mapped nodes is explicitly not applicable;
- recompute fan-in/fan-out p50, p90, maximum, and high-degree shares across the
  selected nodes, using each node's repository-local degree;
- compute cyclic-node share across selected nodes and use the largest
  repository-relative cyclic-component share touched by those nodes;
- compute interface distributions across selected nodes with available
  measurements, excluding not-applicable nodes; any selected node with
  incompatible evidence makes all four target interface fields unavailable; and
- hypothesize, for all 14 diagnostics, that higher values correspond to higher
  absolute normalized residual. This is a predeclared direction to test, not a
  claim that graph complexity creates effort or uncertainty.

Count fields use fixed inclusive buckets `0`, `1`, `2-3`, `4-7`, and `8+`.
Ratio fields use `0`, `(0, 0.25]`, `(0.25, 0.50]`, `(0.50, 0.75]`, and
`(0.75, 1]`. The unlabeled node population fills all five fan-in, fan-out, and
interface bands: fan-in counts are `192/62/44/32/17`, fan-out counts are
`79/110/107/48/3`, and the 316 usable interface nodes are
`8/63/56/52/137` in bucket order. Only six cyclic nodes occur, so the frozen
sparse rule is material rather than cosmetic.

The command is:

```text
eh calibration uncertainty-graph-evaluate <development-corpus.json> \
  <graph-features.json>... --compact --output <evaluation.json>
```

`uncertainty-graph-feature-evaluation/1.0.0` uses leave-one-repository-out
nearest-rank q80 residual factors. A bucket is usable only with at least three
training observations from at least two repositories; otherwise that prediction
falls back to the unconditional held-out baseline. The predeclared pooled gate
requires at least one conditioned prediction, no coverage loss, no normalized-
width growth, and no interval-miss growth. Directional correlation, adjacent-
bucket violations, and repository coverage regressions remain separate
diagnostics. The evaluator rejects validation/test records, fits no production
model, and cannot change labels, seed estimates, or intervals. This checkpoint
froze and tested the machinery before public reviewed residuals were joined.

The subsequent 15-repository, 2,030-target development run rejects every graph
field as a direct interval-width driver. Twelve variants lose coverage and
increase interval miss. Both cycle variants reproduce the baseline exactly and
therefore satisfy the literal non-regression gate, but they improve nothing,
their residual correlations oppose the frozen direction, and positive-cycle
targets come from only one repository. They are recorded as non-selected no-op
passes. All 14 marginal correlations are negative against the all-higher
hypothesis; directions and buckets are not reversed after seeing labels. Exact
tables and reproduction metadata are in
`calibration/corpora/public-readiness/1.8.0/README.md`. Graph evidence remains
diagnostic-only and no estimate or interval changes.

### Development-only uncertainty feature measurement

`uncertainty-feature-evaluation/1.0.0` provides the pre-fit measurement path for
the frozen feature vectors:

```text
eh calibration uncertainty-evaluate <development-corpus.json> \
  <features.json>... --compact --output <evaluation.json>
```

The evaluator refuses any corpus containing a validation or test record. Every
development record must match exactly one feature report by immutable repository
source digest, profile, and baseline; every report must use the same canonical
feature-contract digest and interval policy. The result validates against
`calibration-uncertainty-evaluation.schema.json` and pins the corpus, source
estimate, feature report, contract, projector, and estimator identities.

Reviewed targets are the measurement unit because a reviewed target may combine
several source work items. Counts sum across those items, ordinals take the worst
value, and ratios or rates use candidate-expected-hour weighting. A target is
unavailable if any contributing item lacks the feature; not-applicable items are
ignored when another contributing item is applicable. The target residual is the
absolute difference between candidate and reviewed expected EHE, normalized by
`max(candidate expected, 0.5 hours)` so exact or near-zero candidates remain
bounded without changing either estimate.

Each development repository is held out in turn. The baseline half-width is the
nearest-rank 80th percentile of normalized residuals from all other repositories.
For one feature at a time, the same percentile is calculated from a fixed,
label-independent value bucket only when at least three training targets from at
least two repositories support that bucket; otherwise the prediction falls back
to the repository-held-out baseline. Count and ordinal buckets are `0`, `1`,
`2-3`, `4-7`, and `8+`; ratios use quarter bands; rates use fixed bands through
`0.25`, `0.5`, `1`, `2`, `4`, and above `4`.

The report includes current-range and cross-validated coverage, normalized
sharpness, interval miss, feature availability, conditioned/fallback counts,
fixed-bucket monotonic violations, per-repository fold performance, and Spearman
association with candidate size, raw residual, and normalized residual. Category,
ecosystem, and expected-size slices remain explicit. These are development
weak-label diagnostics, not causal feature importance, an automatic retain/reject
decision, a fitted production model, a probability interval, or admission
evidence. `seed-rules/0.4.0` and all estimate hours remain unchanged.

The first complete public-development run is recorded in
[`calibration/corpora/public-readiness/1.4.0/README.md`](../calibration/corpora/public-readiness/1.4.0/README.md).
It matches all 2,030 targets across 15 repositories. The repository-held-out
symmetric baseline reaches `0.8463` reviewed-expected coverage, but none of the 11
available scalar features improves coverage, normalized width, and interval miss
together. No interval model is selected from that result.

### Label-independent support and OOD profiling

`uncertainty-support-profiler/1.0.0` measures population support without accepting
a review corpus or labels:

```text
eh calibration uncertainty-support <population.json> \
  <features.json>... --compact --output <support-profile.json>
```

The versioned population manifest maps immutable feature-report source digests to
stable repository-family and record IDs. It is development-only, pins one profile,
baseline, and feature contract, and contains no reviewed targets or hours. The
profiler excludes the complete repository family from every item's training
support and nearest-neighbor candidates, including another revision of that same
family.

Support selects the first cell containing at least three observations from at
least two other families: category/size/ecosystem/complexity, then
category/size/ecosystem, category/size, category, and global. Insufficient global
support stays explicit. Only resolved work-item ecosystem tags participate; a
repository-wide ecosystem inventory is not imputed onto generic work items.

`gower-bucket-distance/1.0.0` compares four structural dimensions and all 11
frozen scalar-feature dimensions at equal per-dimension weight. Scalar values use
the already frozen evaluation buckets. Matching non-available states have zero
distance, while availability mismatches have distance one. The report preserves
the nearest cross-repository identity, exact-profile support, component distances,
profile digest, fallback lineage, population digest, and bounded operation counts.
It remains a label-independent population diagnostic, not an interval feature,
accuracy result, fitted model, or probability claim.

The first complete run is recorded in
[`calibration/corpora/public-readiness/1.5.0/README.md`](../calibration/corpora/public-readiness/1.5.0/README.md).
All 11,161 work items have sufficient cross-family support; 8,322 use the exact
structural cell and 2,839 use a broader fallback. The mean nearest-neighbor OOD
score is `0.011802`. These values change no estimate.

### Support/OOD residual evaluation

`uncertainty-support-evaluator/1.0.0` joins the label-independent work-item profile
to reviewed development targets only after the support features are frozen:

```text
eh calibration uncertainty-support-evaluate <development-corpus.json> \
  <support-profile.json> <features.json>... --compact \
  --output <support-evaluation.json>
```

The target aggregation reports worst fallback depth, minimum selected-cell
repository count, expected-hour-weighted mean OOD, and maximum OOD. Each signal
uses fixed buckets and repository-held-out q80 conditioning with the same sparse
fallback as the scalar evaluator. The support profile itself remains label-free;
reviewed values are used only to score residual association, coverage, sharpness,
and miss. The command fits no production model and changes no estimate.

The complete result is recorded in
[`calibration/corpora/public-readiness/1.6.0/README.md`](../calibration/corpora/public-readiness/1.6.0/README.md).
All four signals reduce pooled coverage by `0.0103-0.0118` and increase mean
interval miss by `0.0189-0.0413` hours relative to the unconditional held-out
baseline. Their fixed buckets are non-monotonic, and the observed residual
directions oppose the predeclared support/OOD hypotheses. They are rejected as
direct interval-width drivers for this corpus and retained only as diagnostics.

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

Current public repository records satisfy the complete development-label
boundary. Retired candidate `logical-capability/0.2.0` remains rejected by
measured checkpoint `0.9.0`. Successor `logical-capability/0.3.0` passes all 16
numerical gates, all five development-computable operational gates, and the
public mutation suite at `339/339`. Checkpoint `1.2.0` passes all seven measured
gates and freezes the finite candidate manifest and validation rule. Blind
validation checkpoint `1.3.0` then rejects the sole candidate after six required
gates fail despite a 58.75% expected-WAPE improvement. Test remains sealed and
admission is not attempted. The development and opened validation families are
diagnostic for future work; neither may be presented as a fresh held-out cohort
for a successor informed by these results. `seed-rules/0.4.0` remains the shipped
estimator and required fallback.

The public-readiness cohort satisfies the planned family, shape, and size
allocation, exact source reproduction, and complete development-label boundary.
All nine validation families now have frozen strict-blind teacher labels and a
completed selection result; all nine test families remain unlabeled and sealed.
This attempt ends without selecting a candidate, so test reveal is not authorized.
A successor requires a new identity and fresh blind validation boundary before it
can select exactly one candidate for a sealed one-time test. Source verification
must not be reported as labeled calibration evidence.

Transparent per-category corrections and simple statistical baselines come before
an ML runtime. ML.NET or ONNX is selected only from measured runtime, package,
license, training/export, and determinism needs. Any admitted model must retain an
offline seed fallback and out-of-distribution behavior.

Change model admission remains separate and follows the progressive size-band and
final-delta policy in `CHANGE_MODEL_ADMISSION.md`.

## Manual-QA coding-ratio candidate

Checkpoint `efforthours-public-readiness/1.9.0` freezes development-only
`manual-qa-coding-ratio/0.1.0`. It replaces seed manual-validation items with
traceable 30/40/50 percent items derived from eligible expected coding effort.
The rule is a disclosed maintainer-experience prior, not a production observation
or fitted result. Each QA item depends on one source coding item; design,
discovery, setup, documentation, QA, self-review, gaps, and pricing cannot enter
the basis, and source low/high values are not compounded.

An anonymized real-case diagnostic moves the expected estimate from `161.50` to
`218.10` hours against a separate `240.00`-hour assessment, reducing absolute
midpoint error by `72.1%`. Its inherited high bound worsens, so this is a focused
point correction rather than a solution to overall interval asymmetry.

The existing 15-family development labels allocate only `1,691.75` expected QA
hours over `34,625.75` eligible coding hours (`4.89%`); reproduced seed reports
allocate `4.50%`. Those weak labels were authored under the earlier semantics and
cannot independently validate the new prior. They remain immutable and require a
category-specific blind re-review before candidate evaluation. The shipped
`seed-rules/0.4.0`, opened validation cohort, and sealed test are unchanged.

## Next evidence required

- manual-QA development-label re-review under the explicit eligible-coding
  boundary, with candidate values hidden;
- a complete development evaluation and operational preflight for the exact
  `manual-qa-coding-ratio/0.1.0` policy;
- a new finite manifest and genuinely fresh blind validation boundary if the
  development candidate survives;
- sealed test labels with a precommitted digest and custody record only after a
  future candidate passes that new validation boundary;
- more exact small-task teacher decompositions with honest context provenance;
- optional independent replication where available;
- the already-frozen family matrix and held-out thresholds in
  `MODEL_ADMISSION.md`;
- separately governed empirical production observations that are never used as
  activity multipliers; and
- calibrated range semantics before any probability interpretation.

Detailed historical review results remain in `MODEL_REVIEWS.md` and the immutable
artifacts under `calibration/`.
