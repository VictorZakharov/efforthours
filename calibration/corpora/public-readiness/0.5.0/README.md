# Logical-capability development preflight

## Status

`efforthours-public-readiness/0.5.0` is a **development numerical pass with
operational preflight pending**. It does not freeze a candidate manifest,
authorize validation, select or admit a model, or change the shipped
`seed-rules/0.4.0` estimator.

The exact 15 development families and 2,030 reviewed capabilities from checkpoint
`0.3.0` were used. All nine validation and nine test families remain unestimated
and unlabeled. The records still have one disclosed host-AI teacher and no
independent correction.

## Candidate

`logical-capability/0.1.0` replaces seed point allocation with bounded logical
units derived from canonical evidence measurements, work-item kind, normalized
scope role, and logical size band. A transparent development-fitted table maps
those units to reviewed expected effort. Factors are clamped to `0.25` through
`3.00`; an unknown point or range group retains the complete seed capability.

Planning ranges use residual factors grouped by work-item kind and candidate size
band. They use development quantiles `0.15/0.80`, require three exact-group
samples, fall back to same-kind and then all positive development residuals, and
round interval boundaries outward. Stable work-item IDs, evidence IDs,
categories, and proportional part allocation remain traceable in each transformed
report.

The candidate has no repository identity, source-path value, history, popularity,
activity, provider, source-body feature, or opaque repository-wide multiplier. It
runs offline against a saved implementation-profile seed report and its
digest-matched canonical evidence bundle.

## Result

All 16 numerical development gates pass:

| Development metric | Seed | Candidate | Gate |
| --- | ---: | ---: | :---: |
| Repository expected WAPE | 0.2365 | 0.1141 | pass |
| Relative WAPE improvement | - | 51.75% | pass |
| Absolute aggregate bias | 0.0120 | 0.0108 | pass |
| Median repository absolute error | 391.00 h | 181.7779 h | pass |
| Families inside maximum error boundary | - | 100.00% | pass |
| Families inside ordinary error boundary | - | 93.33% | pass |
| Low/high WAPE | 0.3958 / 0.5384 | 0.1137 / 0.1492 | pass |
| Repository expected coverage | 93.33% | 86.67% | pass |
| Mean / P90 repository normalized width | - | 0.4403 / 0.6817 | pass |
| Mean width relative to seed | 1.0000 | 0.3811 | pass |
| Mean width relative to reviewed ranges | - | 1.2076 | pass |
| Matched-target expected coverage | 63.35% | 82.02% | pass |
| Matched-target mean normalized width | - | 0.7407 | pass |
| Mapping / category mismatch | - | 1.00 / 0.00 | pass |

These are in-sample development diagnostics from logical weak supervision, not
held-out accuracy evidence or a production claim. The intervals are empirical
planning ranges, not probability intervals.

Twelve required operational gates remain deliberately unevaluated: ecosystem,
category, and shape slices; public mutations; cross-platform determinism; schema
and explanation lineage; offline safety, out-of-distribution behavior, and
tamper handling; latency, memory, and package overhead; and scanner thresholds
plus target fingerprints. Every unrun gate is non-passing, so candidate freeze
and holdout access remain blocked.

## Artifacts

The machine-readable
[`0.5.0.logical-capability-model.json`](../0.5.0.logical-capability-model.json)
freezes the complete feature identity, fitted tables, development source digests,
fallbacks, and implementation commit. Its normalized content digest is
`sha256:cde32aaea9d31baef2d0a588b488e7e0b2078a0b2fdb485ae5765f05e37de479`.

The
[`0.5.0.candidate-preflight.json`](../0.5.0.candidate-preflight.json) records the
exact inputs, model digest, metrics, all 28 gate states, and the closed holdout
boundary. Its normalized content digest is
`sha256:220eb04d95b7da022867afb8cb935d99731a12cbc268afeb4a39fc565aabc64f`.

## Reproduction

Build implementation commit `e12765cb0312622a256de4d81798cd50884f9b49`, then
use the ignored, digest-verified development outputs retained by the `0.2.0`
reproducer:

```powershell
dotnet tools/EffortHours.RepositoryCalibration/bin/Release/net10.0/EffortHours.RepositoryCalibration.dll `
  candidate-fit `
  --corpus calibration/corpora/public-readiness/0.3.0.development-corpus.json `
  --outputs artifacts/calibration/public-readiness-0.2.0/outputs `
  --source-commit e12765cb0312622a256de4d81798cd50884f9b49 `
  --output calibration/corpora/public-readiness/0.5.0.logical-capability-model.json

dotnet tools/EffortHours.RepositoryCalibration/bin/Release/net10.0/EffortHours.RepositoryCalibration.dll `
  candidate-numerical-preflight `
  --plan calibration/corpora/public-readiness/0.1.0.sampling-plan.json `
  --corpus calibration/corpora/public-readiness/0.3.0.development-corpus.json `
  --seed-evaluation calibration/corpora/public-readiness/0.3.0.development-evaluation.json `
  --outputs artifacts/calibration/public-readiness-0.2.0/outputs `
  --model calibration/corpora/public-readiness/0.5.0.logical-capability-model.json `
  --source-commit e12765cb0312622a256de4d81798cd50884f9b49 `
  --output calibration/corpora/public-readiness/0.5.0.candidate-preflight.json
```

The second command reproduces both the seed evaluation and fitted model exactly
before evaluating the candidate. Neither command accepts a validation or test
partition.

## Next boundary

Run all 12 operational gates against this exact implementation and model artifact.
Only if every gate passes may a later checkpoint freeze the finite candidate
manifest and precommitted validation-selection rule. Validation labels remain
unavailable until that decision; test labels still require external sealed
body/digest custody before authoring.
