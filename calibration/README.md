# Fairbill calibration material

This directory is the public home for Fairbill calibration policy and, later,
redistributable corpus manifests and labels.

The current checkpoint contains the versioned
[`ehe-work-item/1.0.0`](rubrics/ehe-work-item/1.0.0.md) review rubric. It does not
yet contain a public calibration corpus or a distributable learned model.

## Local workflow

```text
fairbill calibration validate <corpus.json>
fairbill calibration evaluate <corpus.json> <estimate.json>... --partition test
```

The corpus stores reviewed labels and provenance. Candidate estimates stay in
ordinary canonical `EstimateReport` files. Keeping them separate allows the same
frozen labels to evaluate new rules or models without rewriting the corpus.

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
