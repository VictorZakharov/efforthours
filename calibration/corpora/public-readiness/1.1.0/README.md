# Logical-capability v0.3 development operational checkpoint

## Status

`efforthours-public-readiness/1.1.0` records a **five-of-five development
operational pass and a 339-of-339 public mutation pass** for exact candidate
`logical-capability/0.3.0`. The formal measured aggregate, cross-platform resource
measurements, package measurement, and scanner measurement remain pending under
issue #106. No candidate is frozen, selected, admitted, or shipped, and all
validation and test inputs remain unopened.

## Result

All five development-computable operational gates pass:

- ecosystem WAPE/bias passes independently for each five-family stratum: .NET
  `0.1252/0.1021`, JavaScript/TypeScript `0.1106/0.0568`, and mixed
  `0.0943/-0.0468`;
- pooled material-category WAPE is `0.1883` versus seed `0.4331`, with no
  category regression or bias violation;
- every qualifying product-shape and source-size slice remains within the frozen
  regression boundary;
- all 15 reports validate, reconcile, round-trip, retain stable IDs, and explain
  11,161 adjusted items; and
- the bounded, cancellable, digest-pinned projector preserves seed fallback for
  unsupported strata and rejects model tampering.

The standalone public mutation evaluation also passes all 339 assertions across
88 cases. The candidate is applied to the 66 .NET, JavaScript/TypeScript,
frontend, and mixed cases; the 22 out-of-policy SQL/Python cases retain the
explicit `seed-rules/0.4.0` fallback.

| Mutation checkpoint | Passed | Failed |
| --- | ---: | ---: |
| Retired v0.2 | 314 | 25 |
| v0.3 | 339 | 0 |

## Corrected failure groups and invariants

The seven v0.2 mutation failure groups are corrected through general invariants,
not fixture or repository identities:

1. exact duplication: exact-content evidence has one marginal contribution;
2. frontend duplication: repeated file-digest semantic facts contribute once;
3. meaningful API behavior: represented API intent retains a bounded logical
   minimum;
4. external integration: represented integration retains a bounded logical
   minimum and non-collapsing low bound;
5. generated customization and bounded semantic clones: small maintained-source
   capabilities retain a seed-normalized anchor;
6. multi-package review boundary: an empty exact point band expands from the
   nearest same-kind evidence; and
7. operation-only persistence volume: raw call/query counts cannot multiply a
   capability without structural data units; distinct maintained facts bound it.

Focused memory-only regressions cover each mechanism. The existing public suite
remains unchanged; this checkpoint changes only the candidate projection.

## Artifacts

The machine-readable artifacts are:

- [`1.1.0.candidate-operational-preflight.json`](../1.1.0.candidate-operational-preflight.json),
  normalized digest
  `sha256:eb091d5de07399a169dcef4bc9a08f9feb129563dd61d4101eed6c40cab7ddc8`;
  and
- [`0.8.0.candidate-mutation-report.json`](0.8.0.candidate-mutation-report.json),
  normalized digest
  `sha256:59fb04a4e677795198a2b4c81d5aacf871eef0fdbd67c0bbc577f7992b5f79c0`.

They bind model digest
`sha256:492e10b2f427a471a8edcf4f7e3f19d65b098e4822e25c552956c9ce992fa1ea`,
numerical-preflight digest
`sha256:c5614f11513619b7293f454d58cda87efdc192e181ef3551270b1d3bc2a9ba97`,
and implementation commit `7e5451c807cef3ce22bedd1bc374ab519882b21c`.

## Reproduction

After reproducing checkpoint `1.0.0`, run:

```powershell
dotnet tools/EffortHours.RepositoryCalibration/bin/Release/net10.0/EffortHours.RepositoryCalibration.dll `
  candidate-operational-preflight `
  --plan calibration/corpora/public-readiness/0.1.0.sampling-plan.json `
  --corpus calibration/corpora/public-readiness/0.3.0.development-corpus.json `
  --seed-evaluation calibration/corpora/public-readiness/0.3.0.development-evaluation.json `
  --outputs artifacts/calibration/public-readiness-v03-repro/outputs `
  --model calibration/corpora/public-readiness/1.0.0.logical-capability-model.json `
  --numerical-preflight calibration/corpora/public-readiness/1.0.0.candidate-preflight.json `
  --source-commit 7e5451c807cef3ce22bedd1bc374ab519882b21c `
  --output artifacts/calibration/public-readiness-v03-repro/1.1.0.candidate-operational-preflight.json
```

The mutation artifact is the deterministic `eh calibration mutations` evaluation
of suite `calibration/mutations/public-synthetic/0.8.0.suite.json` over the 88
digest-matched v0.3 candidate projections. The checked-in artifact test pins its
exact bytes, suite identity, estimator identities, case count, and 339/339 result.

## Next boundary

Issue #106 runs the remaining measured workflow on Windows, Linux, and macOS and
produces the formal measured aggregate. Candidate freeze, holdout access, model
admission, and a shipped-estimator change remain blocked until that boundary is
reviewed and passed.
