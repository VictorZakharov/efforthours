# EffortHours calibration material

This directory is the public home for EffortHours calibration policy,
redistributable corpus manifests, and reviewed labels.

The frozen
[`repository-model-admission/1.0.0`](../docs/MODEL_ADMISSION.md) policy defines
the minimum family matrix, blind validation and sealed-test boundary, numerical
agreement and range-sharpness gates, candidate selection, resource limits, and
seed fallback. The existing labeled corpora do not yet open that gate.

The
[`efforthours-public-readiness/0.1.0`](corpora/public-readiness/README.md)
sampling checkpoint freezes 33 additional licensed source families across the
complete .NET, JavaScript/TypeScript, and mixed matrix before candidate totals.
The [`0.2.0 reproduction checkpoint`](corpora/public-readiness/0.2.0/README.md)
verifies every pinned tree, publishes strict-blind capability packets for all 15
development families, and leaves all 18 holdouts unanalyzed and unlabeled. The
[`0.3.0 development checkpoint`](corpora/public-readiness/0.3.0/README.md) freezes
rubric-complete judgments for all 2,030 development capabilities before candidate
evaluation. The [`0.4.0 candidate preflight`](corpora/public-readiness/0.4.0/README.md)
rejects the strongest bounded transparent design on development bias, per-family
consistency, and target coverage. It freezes no candidate and opens no holdout.
The [`0.5.0 logical-capability preflight`](corpora/public-readiness/0.5.0/README.md)
passes all 16 numerical development gates; 12 operational gates remain unrun and
non-passing, so it still freezes no manifest and opens no holdout.
The later [`2.0.0 manual-QA review freeze`](corpora/public-readiness/2.0.0/README.md)
projects all 955 eligible development responsibilities into 15 candidate-blind
packets under `manual-qa-work-item/1.0.0`. It freezes the review input before any
replacement QA judgment, compiler, candidate evaluation, or holdout access.
The public repository now contains 21 labeled repository families and 2,262
teacher targets in total, still without independent correction or a model-
admission result.

The current checkpoint contains the original
[`ehe-work-item/1.0.0`](rubrics/ehe-work-item/1.0.0.md) review rubric and the
[`ehe-work-item/1.1.0`](rubrics/ehe-work-item/1.1.0.md) revision for explicit
reviewed exclusions. The
[`efforthours-public-pilot/0.1.0`](corpora/public-pilot/BASELINE.md) and
[`efforthours-public-expansion/0.1.0`](corpora/public-expansion/BASELINE.md)
teacher-estimate corpora provide six public repository families. They remain
small, share one host-AI teacher, have not received independent correction, and do
not justify a production accuracy claim or distributable learned model. See the
optional [`independent-review handoff`](INDEPENDENT_REVIEW.md) for both blind
packets. Exact, evidence-backed small-task decomposition permits host-AI teacher
labels to support an explicitly logical gate without claiming higher review
maturity; empirical production observations remain separate.
The [`public-synthetic/0.7.0`](mutations/public-synthetic/BASELINE.md) mutation
suite adds cross-ecosystem relational guardrails for exclusions, behavior,
reachability, accessibility, dependency graphs, quality, delivery, and category
isolation but is not effort-label data. Its
[`dependency alert dispositions`](mutations/public-synthetic/DEPENDENCY_ALERTS.md)
record five vulnerable version declarations retained solely inside immutable,
non-executed static fixtures; they are not EffortHours runtime dependencies.
The [`changes`](changes) area adds the `change-ehe-work-item/1.0.0` rubric,
immutable final-delta review tooling, and a 24-case matrix frozen before labels.
It now contains a preliminary 121-target host-AI teacher corpus and blind handoff,
plus a one-record real public pilot and a blind six-family public expansion for
released `change-seed/0.2.0`. The expansion's visible development and validation
diagnostics exposed repeated category-slice overcounting. Separate
`change-seed/0.3.0` source-candidate diagnostics record a subject-neutral structural
correction without rewriting the frozen corpus or opening its test comparison. A
new released-alpha.3 public validation record compares a 7.00-hour candidate with
a 5.75-hour blind teacher estimate and records broad uncertainty, self-review
overstatement, and missing unit-test category separation. None of the Change
corpora has independent correction, and no result is an accuracy or
production-readiness claim.

## Local workflow

```text
eh estimate <repository> --no-rate --compact --output <estimate.json>
eh calibration scaffold <estimate.json> [--blind] --output <packet.json>
eh calibration compile <review-plan.json> <estimate.json>... --output <corpus.json>
eh calibration review-scaffold <corpus.json> [--blind] --output <packet.json>
eh calibration review-compile <plan.json> <corpus.json> --output <reviewed-corpus.json>
eh calibration validate <corpus.json>
eh calibration evaluate <corpus.json> <estimate.json>... --partition test
eh calibration mutations <suite.json> <estimate.json>... --output <report.json>
eh calibration change-scaffold <change-estimate.json> --repository-family <id> --case <id> --tag <tag>... [--blind] --output <packet.json>
eh calibration change-compile <review-plan.json> <change-estimate.json>... --output <corpus.json>
eh calibration change-evaluate <corpus.json> <change-estimate.json>... --partition development
```

The corpus stores reviewed labels and provenance. Candidate estimates stay in
ordinary canonical `EstimateReport` files. Keeping them separate allows the same
frozen labels to evaluate new rules or models without rewriting the corpus.
Change candidates remain canonical `ChangeEstimateReport` files and match by a
content-derived base/head final-delta digest plus profile and baseline.

`scaffold` output is explicitly `unreviewed` and cannot be consumed as a corpus.
Authoring version `0.2.0` presents one target per capability. Strict-blind mode
also removes candidate explanations, source work-item IDs/partition counts, and
professionalization-gap IDs. `compile` accepts only completed capability
decisions, requires every represented capability, verifies the exact
source-estimate digest, and restores source work-item/evidence lineage
deterministically.

`review-scaffold` creates a second-pass packet from an existing corpus. Blind mode
hides prior target ranges, rationale, uncertainty, and totals. `review-compile`
requires an explicit accept/replace decision for every target, verifies the exact
source-corpus digest, rejects reviewer identities already present in the record,
and preserves structural lineage while advancing review maturity. Generating a
packet does not itself make labels independently reviewed.

Compiler version `0.2.0` permits an explicit reviewed exclusion only as an exact
`0/0/0` range with rationale and `sizeException`. Ambiguous partially positive
zero ranges are invalid. Legacy `0.1.0` plans remain reproducible for positive
labels and intentionally cannot introduce zero exclusions.

`mutations` evaluates explicit lower/upper bounds on the difference between two
candidate estimates at a repository-total or category point. Mutation suites are
deterministic regression and model-admission guardrails; they must remain separate
from reviewed target labels. A failed relation emits its report and returns exit
code 5.

## Publication checklist

Before adding a record to this directory:

- confirm that the repository, exact revision/snapshot, fixtures, and derived
  labels may be redistributed;
- record the source reference, revision, license expression, data classification,
  and redistribution decision in the record;
- remove credentials, personal information, private source details, and source
  excerpts from rationales;
- keep every revision and both profiles of one repository identity in the same
  development, validation, or test partition; and
- obtain the review maturity and reviewer/model provenance required by the rubric.

Private client records may use the published schemas and evaluator locally. Do not
commit them here, place them in public fixtures, or include them in a distributable
model unless separate authorization and licensing make that publication valid.
