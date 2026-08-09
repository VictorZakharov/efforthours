# Reproducing public synthetic Change artifacts

Build the solution, then run from the repository root:

```text
dotnet tools/EffortHours.ChangeCalibration/bin/Release/net10.0/EffortHours.ChangeCalibration.dll
  --suite calibration/changes/public-synthetic/0.1.0.fixtures.json
  --output calibration/changes/public-synthetic/0.1.0
```

The command writes 24 effort-only reports, 24 blind authoring packets, and one
index. Target snapshots stay in memory; only the requested generated-artifact
directory is written. Every report and packet is semantically and schema validated
before writing, and a process-level test runs a small suite twice to verify
byte-identical output.

For the checked-in suite, a second run must leave every generated file unchanged.
Review `git diff -- calibration/changes/public-synthetic` after regeneration. The
index must report 12 development, 6 validation, and 6 test cases across 8 repository
families. The formatting-only, exact-move, conventional-generated, and complete-
revert reports must each have exact `0/0/0` total effort and no work items.

The generator overwrites only named current artifacts and does not delete stale
files. Generate into an empty directory when comparing a different suite version.

The preliminary teacher review plan is reproducible from the separately reviewed
category policy and pinned source index:

```text
dotnet tools/EffortHours.ChangeCalibration/bin/Release/net10.0/EffortHours.ChangeCalibration.dll
  --teacher-policy calibration/changes/public-synthetic/0.1.0.teacher-policy.json
  --index calibration/changes/public-synthetic/0.1.0/index.json
  --output calibration/changes/public-synthetic/0.1.0.teacher-review-plan.json
```

Compile that plan with `eh calibration change-compile` and all 24 indexed
reports, then scaffold the blind second pass with
`eh calibration review-scaffold --blind`. Only development and validation teacher diagnostics are checked in. Do
not evaluate the test partition before independent review, numerical gates, and a
release candidate are frozen under the
[Change model-admission policy](../../../docs/CHANGE_MODEL_ADMISSION.md).
