# Repository-candidate development preflight

## Status

`efforthours-public-readiness/0.4.0` is a **fail-closed development preflight**.
It does not freeze a candidate manifest, authorize validation, select a model, or
change the shipped `seed-rules/0.4.0` estimator.

The exact 15 development families and 2,030 reviewed capabilities from the `0.3.0`
checkpoint were used. Validation and test source were not analyzed, candidate
outputs were not generated for them, and their labels remain un-authored.

## Result

The strongest bounded transparent design tested in the preflight uses only stable
work-item kind and normalized scope role. It removes duplicated semantic work in
test, benchmark, and generated-fixture scopes, discounts supporting test or
benchmark implementation to 25%, and applies a fixed `0.77/1.00/1.26` range around
each remaining expected point. It has no repository identity, source path value,
history, popularity, activity, or repository-wide multiplier.

| Development metric | Seed | Preflight design | Gate |
| --- | ---: | ---: | :---: |
| Repository expected WAPE | 0.2365 | 0.1963 | pass |
| Relative WAPE improvement | — | 17.00% | pass |
| Absolute aggregate bias | 0.0120 | 0.0672 | **fail** |
| Median repository absolute error | 391.00 h | 224.75 h | pass |
| Families inside the ordinary error boundary | — | 73.33% | **fail** |
| Repository expected coverage | 93.33% | 80.00% | pass |
| Mean repository normalized width | — | 0.4808 | pass |
| P90 repository normalized width | — | 0.6320 | pass |
| Mean width relative to seed | 1.0000 | 0.3462 | pass |
| Mean width relative to reviewed ranges | — | 1.0971 | pass |
| Matched-target expected coverage | 63.35% | 26.11% | **fail** |
| Matched-target mean normalized width | — | 0.6691 | pass |

The point correction clears the headline WAPE threshold but worsens already-small
aggregate bias, leaves four of 15 families outside `max(8 h, 25%)`, and misses the
reviewed expected point for almost three quarters of matched capabilities. Wider
ranges can increase coverage only by violating the sharpness boundary. That is the
opposite of the desired `8-10` rather than `4-16` confidence improvement.

The machine-readable
[`0.4.0.candidate-preflight.json`](../0.4.0.candidate-preflight.json) records the
exact inputs, implementation commit, features, rules, hyperparameters, development
metrics, every computed gate, and every operational gate deliberately not run.
Unrun gates are non-passing. Its content digest is
`sha256:03284022beaa170df5a2e8edbb78ab145a9f0da66934d5ff02debfb316cd083d`.

## Other development-only designs

The bounded search also rejected category and capability ratio corrections,
capability-level linear corrections, a regularized repository baseline, and a
nearest-neighbor repository baseline. Their best repository expected WAPEs were
`0.2609`, `0.2589`, `0.2518`, `0.2268`, and `0.2062` respectively. The nearest-
neighbor result depended on one neighbor, improved seed by only 12.8%, and was not
stable enough to advance.

A robust capability allocation using kind, pre-adjustment size, confidence, and
complexity preserved the transparent repository totals and improved
repository-held-out capability WAPE from `0.5017` to `0.4202`; median capability
absolute error moved from `0.75` to `0.4848` hour. It still reached only about
52.4% target coverage at a 0.7085 target normalized width, while repository width
was already 0.5027. It therefore did not become a candidate artifact.

These are development diagnostics, not held-out accuracy evidence. No discarded
configuration is eligible for validation without a new, exact preflight identity.

## Reproduction

Build commit `ca3636293e92f120ac4d4d0f88a9b633ff96431e`, then use the ignored,
digest-verified development outputs retained by the `0.2.0` reproducer:

```powershell
dotnet tools/EffortHours.RepositoryCalibration/bin/Release/net10.0/EffortHours.RepositoryCalibration.dll `
  candidate-preflight `
  --plan calibration/corpora/public-readiness/0.1.0.sampling-plan.json `
  --corpus calibration/corpora/public-readiness/0.3.0.development-corpus.json `
  --seed-evaluation calibration/corpora/public-readiness/0.3.0.development-evaluation.json `
  --outputs artifacts/calibration/public-readiness-0.2.0/outputs `
  --source-commit ca3636293e92f120ac4d4d0f88a9b633ff96431e `
  --output calibration/corpora/public-readiness/0.4.0.candidate-preflight.json
```

The command first reproduces the checked-in seed evaluation exactly. It then
transforms development estimates in memory, preserves every stable work-item and
evidence mapping, and writes the deterministic preflight. It has no option for a
validation or test partition.

## Next boundary

Improve general semantic marginality and capability allocation using development
evidence only. A later design must pass a new development preflight—especially
bias, per-family consistency, target coverage, and sharpness—before an exact finite
candidate manifest can be frozen. Validation labels remain unavailable until then.
