# Logical-capability v0.2 numerical preflight

## Status

`efforthours-public-readiness/0.7.0` is a **development numerical pass** for
`logical-capability/0.2.0`. It does not freeze a candidate manifest, authorize
validation, admit a model, or change the shipped `seed-rules/0.4.0` estimator.

The exact 15 development families and 2,030 reviewed capabilities from checkpoint
`0.3.0` were used. All nine validation and nine test families remain unestimated
and unlabeled. Review maturity remains one disclosed host-AI teacher with no
independent correction.

## Diagnosis and candidate

The rejected v0.1 model clipped two specification-comprehension point groups at
its general `3.00` factor ceiling. The `s` group contained 4.75 logical hours
against 56 reviewed hours, and the `xl` group contained 55.25 logical hours
against 244 reviewed hours. Clipping those two groups accounted for the complete
120-hour aggregate category deficit.

Version 0.2 retains the same evidence-unit scorer, scope normalization, size bands,
development families, factor fitting, range quantiles, and seed fallback. It adds
one explicit bounded configuration: specification-comprehension groups may use a
maximum factor of `4.00`; every other work-item kind remains capped at `3.00`.
The model uses no repository identity, source-path value, history, activity,
provider, source body, or repository-wide multiplier. Frozen v0.1 artifacts remain
accepted by the projector under their original identities.

## Result

All 16 numerical development gates pass:

| Development metric | Seed | Candidate | Gate |
| --- | ---: | ---: | :---: |
| Repository expected WAPE | 0.2365 | 0.1137 | pass |
| Relative WAPE improvement | - | 51.92% | pass |
| Absolute aggregate bias | 0.0120 | 0.0094 | pass |
| Median repository absolute error | 391.00 h | 191.0279 h | pass |
| Families inside maximum error boundary | - | 100.00% | pass |
| Families inside ordinary error boundary | - | 93.33% | pass |
| Low/high WAPE | 0.3958 / 0.5384 | 0.1129 / 0.1494 | pass |
| Repository expected coverage | 93.33% | 86.67% | pass |
| Mean / P90 repository normalized width | - | 0.4405 / 0.6912 | pass |
| Mean width relative to seed | 1.0000 | 0.3805 | pass |
| Mean width relative to reviewed ranges | - | 1.2056 | pass |
| Matched-target expected coverage | 63.35% | 82.17% | pass |
| Matched-target mean normalized width | - | 0.7406 | pass |
| Mapping / category mismatch | - | 1.00 / 0.00 | pass |

The specification category moves from 359.00 to 419.00 candidate expected hours
against 479.00 reviewed hours. Its aggregate bias improves from `-0.2505` to
`-0.1253`, with WAPE `0.2752`. These are in-sample development diagnostics, not
held-out accuracy or a production claim.

## Artifacts

The machine-readable
[`0.7.0.logical-capability-model.json`](../0.7.0.logical-capability-model.json)
has normalized digest
`sha256:f53b5af09b5adf0d3efed5339e9309156f026b3378c6b69e979467b92524ae93`.

The
[`0.7.0.candidate-preflight.json`](../0.7.0.candidate-preflight.json) records the
exact inputs, model digest, metrics, all 28 gate states, and closed holdout
boundary. Its normalized digest is
`sha256:4a12b35e7088d54e8fee6240c5a478f007d6a714bdc1b089398591de1c52c333`.

Both artifacts pin implementation commit
`6962a3a49911ae230f2df13b5b05f8aded5c7e12`.

## Reproduction

Build the pinned implementation commit, then use the ignored, digest-verified
development outputs retained by checkpoint `0.2.0`:

```powershell
dotnet tools/EffortHours.RepositoryCalibration/bin/Release/net10.0/EffortHours.RepositoryCalibration.dll `
  candidate-fit `
  --corpus calibration/corpora/public-readiness/0.3.0.development-corpus.json `
  --outputs artifacts/calibration/public-readiness-0.2.0/outputs `
  --source-commit 6962a3a49911ae230f2df13b5b05f8aded5c7e12 `
  --output calibration/corpora/public-readiness/0.7.0.logical-capability-model.json

dotnet tools/EffortHours.RepositoryCalibration/bin/Release/net10.0/EffortHours.RepositoryCalibration.dll `
  candidate-numerical-preflight `
  --plan calibration/corpora/public-readiness/0.1.0.sampling-plan.json `
  --corpus calibration/corpora/public-readiness/0.3.0.development-corpus.json `
  --seed-evaluation calibration/corpora/public-readiness/0.3.0.development-evaluation.json `
  --outputs artifacts/calibration/public-readiness-0.2.0/outputs `
  --model calibration/corpora/public-readiness/0.7.0.logical-capability-model.json `
  --source-commit 6962a3a49911ae230f2df13b5b05f8aded5c7e12 `
  --output calibration/corpora/public-readiness/0.7.0.candidate-preflight.json
```

Neither command accepts a validation or test partition.

## Next boundary

Checkpoint `0.8.0` runs the development-computable operational gates against this
exact model. Candidate freeze and holdout access remain blocked until all measured
operational gates pass as well.
