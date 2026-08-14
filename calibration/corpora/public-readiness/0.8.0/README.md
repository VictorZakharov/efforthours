# Logical-capability v0.2 operational preflight

## Status

`efforthours-public-readiness/0.8.0` is a **development operational pass with seven
measured gates pending**. Exact candidate `logical-capability/0.2.0` is not frozen,
selected, admitted, or used by the product. Validation and test source, candidate
outputs, and labels remain unopened.

## Result

All five development-computable operational gates pass:

- ecosystem-stratum WAPE/bias passes independently for each five-family .NET,
  JavaScript/TypeScript, and mixed stratum;
- pooled material-category WAPE is `0.2075` versus seed `0.4331`, with no category
  regression or bias violation; specification-comprehension is `0.2752` WAPE and
  `-0.1253` bias;
- every qualifying product-shape and source-size slice stays within `0.05` WAPE of
  seed;
- all 15 reports validate, reconcile, round-trip, retain stable work/evidence IDs,
  and explain 11,161 adjusted items; and
- the bounded, cancellable, digest-pinned projector rejects model tampering and
  visibly retains the complete seed for unsupported strata.

Seven required measured gates remain `not-evaluated` and non-passing:

1. public mutation suites;
2. byte-identical Windows/Linux/macOS projection;
3. median latency overhead;
4. slowest latency overhead;
5. sampled peak working-set overhead;
6. installed-package increase; and
7. scanner thresholds plus unchanged target fingerprints.

No failure occurred in this checkpoint. The unrun states fail closed, so the
candidate manifest and validation authorization remain false.

## Artifact and implementation

The machine-readable
[`0.8.0.candidate-operational-preflight.json`](../0.8.0.candidate-operational-preflight.json)
pins numerical preflight `repository-candidate-preflight/0.2.0` at digest
`sha256:4a12b35e7088d54e8fee6240c5a478f007d6a714bdc1b089398591de1c52c333`,
model digest
`sha256:f53b5af09b5adf0d3efed5339e9309156f026b3378c6b69e979467b92524ae93`,
and implementation commit
`6962a3a49911ae230f2df13b5b05f8aded5c7e12`. Its normalized digest is
`sha256:609307d5a366b52c18118db2ef9f79e46d86565b5419aa767e0cbbf7f1fe8ec8`.

No dependency, dataset, license, policy threshold, validation/test artifact, or
shipped estimator changed.

## Reproduction

After reproducing the ignored development outputs and the `0.7.0` artifacts, run:

```powershell
dotnet tools/EffortHours.RepositoryCalibration/bin/Release/net10.0/EffortHours.RepositoryCalibration.dll `
  candidate-operational-preflight `
  --plan calibration/corpora/public-readiness/0.1.0.sampling-plan.json `
  --corpus calibration/corpora/public-readiness/0.3.0.development-corpus.json `
  --seed-evaluation calibration/corpora/public-readiness/0.3.0.development-evaluation.json `
  --outputs artifacts/calibration/public-readiness-0.2.0/outputs `
  --model calibration/corpora/public-readiness/0.7.0.logical-capability-model.json `
  --numerical-preflight calibration/corpora/public-readiness/0.7.0.candidate-preflight.json `
  --source-commit 6962a3a49911ae230f2df13b5b05f8aded5c7e12 `
  --output calibration/corpora/public-readiness/0.8.0.candidate-operational-preflight.json
```

The command has no validation or test option.

## Next boundary

Run the seven measured gates against this exact candidate, implementation, and
model artifact. Freeze a finite manifest and selection rule only if all seven pass;
otherwise retire v0.2 without opening holdouts.

## Subsequent result

Checkpoint [`0.9.0`](../0.9.0/README.md) ran all seven gates and retired v0.2
after public-mutation and raw cross-platform byte-determinism failures. This file
retains the state and decision boundary at the earlier 0.8.0 checkpoint.
