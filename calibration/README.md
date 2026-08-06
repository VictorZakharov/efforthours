# Fairbill calibration material

This directory is the public home for Fairbill calibration policy,
redistributable corpus manifests, and reviewed labels.

The current checkpoint contains the original
[`ehe-work-item/1.0.0`](rubrics/ehe-work-item/1.0.0.md) review rubric and the
[`ehe-work-item/1.1.0`](rubrics/ehe-work-item/1.1.0.md) revision for explicit
reviewed exclusions. The
[`fairbill-public-pilot/0.1.0`](corpora/public-pilot/BASELINE.md) and
[`fairbill-public-expansion/0.1.0`](corpora/public-expansion/BASELINE.md)
teacher-estimate corpora provide six public repository families. They remain
small, share one host-AI teacher, have not received independent correction, and do
not justify a production accuracy claim or distributable learned model. See the
[`independent-review handoff`](INDEPENDENT_REVIEW.md) for both blind packets.
The [`public-synthetic/0.3.0`](mutations/public-synthetic/BASELINE.md) mutation
suite adds cross-ecosystem relational guardrails for exclusions, behavior,
quality, delivery, and category isolation but is not effort-label data.

## Local workflow

```text
fairbill estimate <repository> --no-rate --compact --output <estimate.json>
fairbill calibration scaffold <estimate.json> [--blind] --output <packet.json>
fairbill calibration compile <review-plan.json> <estimate.json>... --output <corpus.json>
fairbill calibration review-scaffold <corpus.json> [--blind] --output <packet.json>
fairbill calibration review-compile <plan.json> <corpus.json> --output <reviewed-corpus.json>
fairbill calibration validate <corpus.json>
fairbill calibration evaluate <corpus.json> <estimate.json>... --partition test
fairbill calibration mutations <suite.json> <estimate.json>... --output <report.json>
```

The corpus stores reviewed labels and provenance. Candidate estimates stay in
ordinary canonical `EstimateReport` files. Keeping them separate allows the same
frozen labels to evaluate new rules or models without rewriting the corpus.

`scaffold` output is explicitly `unreviewed` and cannot be consumed as a corpus.
`compile` accepts only completed capability decisions, requires every represented
capability, verifies the exact source-estimate digest, and restores source
work-item/evidence lineage deterministically.

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
