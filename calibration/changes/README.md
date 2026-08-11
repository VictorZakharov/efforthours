# Change EHE calibration

This directory holds policy and redistributable teacher labels for EffortHours's
experimental Change estimators. The current source baseline is
`change-seed/0.6.0`; the frozen synthetic corpus retains its original
`change-seed/0.1.0` source-report provenance, the real pilot/expansion retain their
released `change-seed/0.2.0` reports, and the released-alpha.3 diagnostic exercises
`change-seed/0.3.0` directly. Change labels use the same corpus, optional
independent-review, validation, and metric contracts as repository labels, with an
additional immutable final-delta provenance record.

New packets use
[`change-ehe-work-item/1.1.0`](../rubrics/change-ehe-work-item/1.1.0.md); frozen
packets retain [`1.0.0`](../rubrics/change-ehe-work-item/1.0.0.md) and its explicit
byte-reproducible legacy authoring path. Current review plans use compiler 0.2.0;
compiler 0.1.0 remains accepted only with rubric 1.0.0 for frozen reproduction.
The checkpoint also contains a
[24-case matrix](public-synthetic/CASE_MATRIX.md) frozen before numerical review.
Its synthetic source suite now reproduces 24 canonical source reports and blind
authoring packets. A preliminary 24-record teacher corpus and blind independent-
review packet are checked in. No independent correction or empirical accuracy
claim is complete; decomposed host-AI teacher labels are nevertheless admitted as
Stage A logical weak supervision under the 1.1.0 policy.

The [`public-real`](public-real) pilot adds the first immutable public open-source
pull-request record for the released `change-seed/0.2.0` estimator. Alpha.2
reports 4.25 expected hours and the separately reasoned host-AI teacher reports
4.00. This single development record is a workflow and realism diagnostic only;
it changes no prior, threshold, review maturity, or production-readiness claim.

The [`public-real-expansion`](public-real-expansion) checkpoint adds six more
immutable MIT-licensed families across .NET, JavaScript, and TypeScript, split
3/2/1 across development, validation, and test. Its blind teacher plan was frozen
before candidate values were opened. Visible development and validation results
diagnose severe repeated-slice overcounting in tests and security work, while the
test comparison remains withheld. A separate
[`change-seed/0.3.0` diagnostic](public-real-expansion/diagnostics/change-seed-0.3.0)
records the subject-neutral structural correction on development and validation
only. Frozen reports and labels remain unchanged; no prior, threshold, review
maturity, or accuracy claim follows.

The [`public-real-alpha3`](public-real-alpha3) checkpoint adds a new .NET
validation family analyzed with released `EffortHours.Tool` `0.9.0-alpha.3`. Its
blind teacher expected value is 5.75 hours against 7.00 candidate hours. The
candidate covers the complete reviewed range but uses a substantially wider
interval, overstates self-review, and attaches the represented test path to the
production capability instead of emitting a separate unit-testing target. This is
one non-independent diagnostic; it changes no rule, prior, threshold, or maturity.

Current `change-seed/0.6.0+seed-rules/0.3.0` keeps the visible 0.3.0 repository
totals while splitting mixed maintained-file roles into disjoint categories and
decomposing larger candidate items into named roughly-one-hour phases. The
[`stage-a-logical-review`](stage-a-logical-review/README.md) audit preserves the
legacy labels while resolving all 28 eligible parent targets into 45 distinct
0.5-to-1.5-hour tasks. Five visible 4-to-32-hour public families pass the frozen
Stage A total, per-case, native-category, decomposition, interval, and safety gates
recorded in `docs/CHANGE_MODEL_ADMISSION.md`. The expansion test comparison remains
withheld. This admits experimental small-change logical use, not a general accuracy
rate or production readiness.

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
optional independent review. Empty target lists are reserved for exact-zero final
deltas. Positive 1.1.0 targets should be roughly 0.5 to 1.5 expected hours; any
target above two hours requires a concrete indivisibility exception.

The public synthetic source artifacts are generated without disk-backed target
snapshots:

```text
dotnet tools/EffortHours.ChangeCalibration/bin/Release/net10.0/EffortHours.ChangeCalibration.dll
  --suite calibration/changes/public-synthetic/0.1.0.fixtures.json
  --output calibration/changes/public-synthetic/0.1.0
```

See `public-synthetic/SOURCES.md` and `public-synthetic/REPRODUCING.md` for the
synthetic provenance and reproduction checks. See `public-real/SOURCES.md`,
`public-real-expansion/SOURCES.md`, and `public-real-alpha3/SOURCES.md` for the
real final-change records and their reproduction boundaries. All four blind
handoffs remain open.
