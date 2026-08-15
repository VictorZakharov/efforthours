# Logical-capability v0.3 numerical preflight

## Status

`efforthours-public-readiness/1.0.0` is a **development numerical pass** for
`logical-capability/0.3.0`. It does not freeze a candidate manifest, authorize
validation, admit a model, or change the shipped `seed-rules/0.4.0` estimator.

The fit uses only the 15 development families and 2,030 reviewed capabilities
frozen at checkpoint `0.3.0`. All nine validation and nine test families remain
unestimated and unlabeled. Review maturity remains one disclosed host-AI teacher
with no independent correction.

## Candidate boundary

Version 0.3 replaces retired v0.2 with a new model, estimator, feature-contract,
scorer, and implementation identity. Its repository-neutral corrections are:

- normalize exact-content evidence and repeated source aggregates before fitting;
- retain bounded minimum logical points for meaningful API and integration intent;
- anchor small maintained-source capabilities to their seed-normalized budget;
- use the nearest same-kind residuals when an exact range group is empty;
- bound operation-only persistence evidence by distinct maintained facts rather
  than raw query/call volume; and
- retain scope-adjusted low floors of `0.5` hour for represented persistence and
  `1.0` hour for represented external integration.

The model uses no repository identity, source-path value, history, activity,
provider, source body, or repository-wide effort multiplier. Frozen v0.1 and v0.2
artifacts remain readable and retain their original projection bytes.

## Result

All 16 numerical development gates pass:

| Development metric | Seed | Retired v0.2 | v0.3 | Gate |
| --- | ---: | ---: | ---: | :---: |
| Repository expected WAPE | 0.2365 | 0.1137 | 0.1009 | pass |
| Relative WAPE improvement | - | 51.92% | 57.34% | pass |
| Absolute aggregate bias | 0.0120 | 0.0094 | 0.0103 | pass |
| Median repository absolute error | 391.00 h | 191.0279 h | 105.6833 h | pass |
| Families inside maximum error boundary | - | 100.00% | 100.00% | pass |
| Families inside ordinary error boundary | - | 93.33% | 93.33% | pass |
| Low/high WAPE | 0.3958 / 0.5384 | 0.1129 / 0.1494 | 0.0869 / 0.1353 | pass |
| Repository expected coverage | 93.33% | 86.67% | 80.00% | pass |
| Mean / P90 repository normalized width | - | 0.4405 / 0.6912 | 0.3662 / 0.5377 | pass |
| Mean width relative to seed | 1.0000 | 0.3805 | 0.2758 | pass |
| Mean width relative to reviewed ranges | - | 1.2056 | 0.8741 | pass |
| Matched-target expected coverage | 63.35% | 82.17% | 76.01% | pass |
| Matched-target mean normalized width | - | 0.7406 | 0.7321 | pass |
| Mapping / category mismatch | - | 1.00 / 0.00 | 1.00 / 0.00 | pass |

These are in-sample development diagnostics, not held-out accuracy or a
production claim.

## Artifacts

The machine-readable artifacts are:

- [`1.0.0.logical-capability-model.json`](../1.0.0.logical-capability-model.json),
  normalized digest
  `sha256:492e10b2f427a471a8edcf4f7e3f19d65b098e4822e25c552956c9ce992fa1ea`;
  and
- [`1.0.0.candidate-preflight.json`](../1.0.0.candidate-preflight.json),
  normalized digest
  `sha256:c5614f11513619b7293f454d58cda87efdc192e181ef3551270b1d3bc2a9ba97`.

Both artifacts pin implementation commit
`7e5451c807cef3ce22bedd1bc374ab519882b21c` and the exact development source,
estimate, and evidence digests.

## Reproduction

Build the pinned commit and reproduce the ignored development outputs from the
source-custody checkpoint, then run:

```powershell
dotnet tools/EffortHours.RepositoryCalibration/bin/Release/net10.0/EffortHours.RepositoryCalibration.dll `
  candidate-fit `
  --corpus calibration/corpora/public-readiness/0.3.0.development-corpus.json `
  --outputs artifacts/calibration/public-readiness-v03-repro/outputs `
  --source-commit 7e5451c807cef3ce22bedd1bc374ab519882b21c `
  --output artifacts/calibration/public-readiness-v03-repro/1.0.0.logical-capability-model.json

dotnet tools/EffortHours.RepositoryCalibration/bin/Release/net10.0/EffortHours.RepositoryCalibration.dll `
  candidate-numerical-preflight `
  --plan calibration/corpora/public-readiness/0.1.0.sampling-plan.json `
  --corpus calibration/corpora/public-readiness/0.3.0.development-corpus.json `
  --seed-evaluation calibration/corpora/public-readiness/0.3.0.development-evaluation.json `
  --outputs artifacts/calibration/public-readiness-v03-repro/outputs `
  --model artifacts/calibration/public-readiness-v03-repro/1.0.0.logical-capability-model.json `
  --source-commit 7e5451c807cef3ce22bedd1bc374ab519882b21c `
  --output artifacts/calibration/public-readiness-v03-repro/1.0.0.candidate-preflight.json
```

The commands accept no validation or test partition. Reproduced normalized bytes
must match the checked-in digests above.

## Next boundary

Checkpoint `1.1.0` runs the development-computable operational gates and public
mutation suite against this exact candidate. Candidate freeze and holdout access
remain blocked until the later measured preflight also passes.
