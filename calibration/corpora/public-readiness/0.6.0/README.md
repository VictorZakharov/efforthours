# Logical-capability operational preflight

## Status

`efforthours-public-readiness/0.6.0` is a **development operational rejection**.
Exact candidate `logical-capability/0.1.0` is retired without a candidate-manifest
freeze, validation authorization, model admission, or change to the shipped
`seed-rules/0.4.0` estimator.

The checkpoint uses only the frozen 15-family development partition and its 2,030
reviewed capabilities. Validation and test source, candidate outputs, and labels
remain unopened. The single host-AI teacher status and lack of independent
correction are unchanged.

## Decision

The candidate passes four operational gates:

- each five-family ecosystem stratum meets its WAPE and bias limits;
- every development product-shape or size slice with at least three families
  stays within `0.05` WAPE of the seed;
- all 15 transformed reports preserve schema validity, stable work/evidence IDs,
  exact reconciliation, canonical saved-report round trips, and bounded reasons
  for 11,161 adjusted work items; and
- the digest-pinned saved-artifact projector is bounded and cancellable, rejects
  model tampering, and visibly retains the complete seed for unsupported strata.

The candidate fails the material-category gate. Across all 15 material categories,
pooled expected WAPE improves from `0.4331` to `0.2082`. However,
`SpecificationComprehensionAndDomainLearning` has candidate WAPE `0.3326`, seed
WAPE `0.6712`, and aggregate bias `-0.2505`. The category improves substantially
but exceeds the frozen absolute-bias limit of `0.20`; policy therefore rejects the
candidate.

Public-mutation, cross-platform determinism, latency, peak-working-set, installed
package, and scanner/fingerprint gates were not run after that failure. These seven
states remain explicitly non-passing. Spending the cross-platform measurement
budget cannot rescue a candidate that already failed a development gate.

## Artifacts and implementation

The machine-readable
[`0.6.0.candidate-operational-preflight.json`](../0.6.0.candidate-operational-preflight.json)
pins numerical preflight `repository-candidate-preflight/0.2.0` at digest
`sha256:220eb04d95b7da022867afb8cb935d99731a12cbc268afeb4a39fc565aabc64f`,
model digest
`sha256:cde32aaea9d31baef2d0a588b488e7e0b2078a0b2fdb485ae5765f05e37de479`,
and operational implementation commit
`c58f5eb45244aa3f8fd509d4c91c5059f1647440`. Its normalized content digest is
`sha256:50dc6d37ad707121d7137c5d031d60aa3eda7f9ac1ddcbeb2f58dbfc3ae3f47c`.

The maintainer-only projector accepts one seed estimate, its digest-matched saved
evidence, the model artifact, the expected model digest, and a declared primary
stratum. It reads no repository or Git history, starts no external process, and
makes no provider or network call. It writes the canonical estimate to stdout and
fallback diagnostics to stderr. No dependency, dataset, model, or license changed.

## Reproduction

After reproducing the ignored development outputs as described by checkpoint
`0.2.0`, build the repository-calibration tool at the pinned implementation commit
and run:

```powershell
dotnet tools/EffortHours.RepositoryCalibration/bin/Release/net10.0/EffortHours.RepositoryCalibration.dll `
  candidate-operational-preflight `
  --plan calibration/corpora/public-readiness/0.1.0.sampling-plan.json `
  --corpus calibration/corpora/public-readiness/0.3.0.development-corpus.json `
  --seed-evaluation calibration/corpora/public-readiness/0.3.0.development-evaluation.json `
  --outputs artifacts/calibration/public-readiness-0.2.0/outputs `
  --model calibration/corpora/public-readiness/0.5.0.logical-capability-model.json `
  --numerical-preflight calibration/corpora/public-readiness/0.5.0.candidate-preflight.json `
  --source-commit c58f5eb45244aa3f8fd509d4c91c5059f1647440 `
  --output calibration/corpora/public-readiness/0.6.0.candidate-operational-preflight.json
```

The command reproduces the seed evaluation, candidate reports, and operational
gate results from frozen development inputs. It has no validation or test option.

## Next boundary

Do not retune or continue measuring `logical-capability/0.1.0`. A successor that
addresses category-level bias must use a new candidate identity and repeat the
complete development numerical and operational preflight. Validation labels stay
unavailable until one exact successor passes every required gate and a finite
manifest plus selection rule are frozen; test labels remain sealed until the
one-time selection boundary.
