# Change EHE calibration

This directory holds policy and redistributable teacher labels for EffortHours's
experimental Change estimators. The current baseline is `change-seed/0.2.0`; the
frozen synthetic corpus retains its original `change-seed/0.1.0` source-report
provenance. Change labels use the same corpus, independent-review, validation, and
metric contracts as repository labels, with an additional immutable final-delta
provenance record.

The current checkpoint contains tooling, the
[`change-ehe-work-item/1.0.0`](../rubrics/change-ehe-work-item/1.0.0.md) rubric, and
a [24-case matrix](public-synthetic/CASE_MATRIX.md) frozen before numerical review.
Its synthetic source suite now reproduces 24 canonical source reports and blind
authoring packets. A preliminary 24-record teacher corpus and blind independent-
review packet are checked in, but no independent correction or accuracy claim is
complete.

The [`public-real`](public-real) pilot adds the first immutable public open-source
pull-request record for the current `change-seed/0.2.0` estimator. Released alpha.2
reports 4.25 expected hours and the separately reasoned host-AI teacher reports
4.00. This single development record is a workflow and realism diagnostic only;
it changes no prior, threshold, review maturity, or production-readiness claim.

The [`public-real-expansion`](public-real-expansion) checkpoint adds six more
immutable MIT-licensed families across .NET, JavaScript, and TypeScript, split
3/2/1 across development, validation, and test. Its blind teacher plan was frozen
before candidate values were opened. Visible development and validation results
diagnose severe repeated-slice overcounting in tests and security work, while the
test comparison remains withheld. The checkpoint changes no rule or prior.

## Workflow

```text
eh change <repository> --base <revision> --head <revision> --no-rate --output <change-estimate.json>

eh calibration change-scaffold <change-estimate.json>
  --repository-family <stable-id>
  --case <stable-id>
  --tag <coverage-tag>...
  [--blind]
  --output <packet.json>

eh calibration change-compile <review-plan.json> <change-estimate.json>...
  --output <corpus.json>

eh calibration validate <corpus.json>
eh calibration review-scaffold <corpus.json> --blind --output <second-pass.json>
eh calibration review-compile <completed-plan.json> <corpus.json> --output <reviewed.json>

eh calibration change-evaluate <reviewed.json> <change-estimate.json>...
  --partition <development|validation|test>
```

`change-scaffold` is always unreviewed. `change-compile` verifies the exact
final-delta identity, base/head object IDs, evidence digests, source estimate
digest, estimator, profile, and baseline before restoring work-item lineage.
Pricing is rejected at authoring time; generate effort-only reports with
`--no-rate`.

The repository-family ID owns partition isolation. Different commits, ranges,
profiles, or selector forms from one family must never cross development,
validation, and test partitions. Selector kind and coverage tags are provenance
and stratification only; they never multiply EHE.

Zero final deltas are represented by an empty target list and exact zero total.
This permits formatting, exact movement, conventional generation, and complete
reverts to enter held-out evaluation without invented work items.

A Change review-plan capability uses an exact `0/0/0` target plus a concrete
rationale and `sizeException` to reject a candidate false positive or duplicate.
The lineage-preserving exclusion remains visible in evaluation and in blind
independent review. Empty target lists are reserved for exact-zero final deltas.

The public synthetic source artifacts are generated without disk-backed target
snapshots:

```text
dotnet tools/EffortHours.ChangeCalibration/bin/Release/net10.0/EffortHours.ChangeCalibration.dll
  --suite calibration/changes/public-synthetic/0.1.0.fixtures.json
  --output calibration/changes/public-synthetic/0.1.0
```

See `public-synthetic/SOURCES.md` and `public-synthetic/REPRODUCING.md` for the
synthetic provenance and reproduction checks. See `public-real/SOURCES.md` and
`public-real-expansion/SOURCES.md` for the real final-change records and their
reproduction boundaries. All three blind handoffs remain open.
