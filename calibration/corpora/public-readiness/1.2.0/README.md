# Logical-capability v0.3 measured operational preflight

## Frozen measurement boundary

This checkpoint runs the seven measured gates from
`repository-model-admission/1.0.0` against one exact development-only candidate.
The inputs below are frozen before measurement:

| Input | Frozen identity |
| --- | --- |
| Candidate | `logical-capability/0.3.0` |
| Model | `repository-logical-capability-model/0.3.0` |
| Estimator | `candidate-logical-capability/0.3.0+seed-rules/0.4.0` |
| Feature contract | `logical-capability-features/1.3.0` |
| Candidate implementation | `7e5451c807cef3ce22bedd1bc374ab519882b21c` |
| Model artifact | `1.0.0.logical-capability-model.json`; `sha256:492e10b2f427a471a8edcf4f7e3f19d65b098e4822e25c552956c9ce992fa1ea` |
| Numerical preflight | `1.0.0.candidate-preflight.json`; `sha256:c5614f11513619b7293f454d58cda87efdc192e181ef3551270b1d3bc2a9ba97` |
| Development operational preflight | `1.1.0.candidate-operational-preflight.json`; `sha256:eb091d5de07399a169dcef4bc9a08f9feb129563dd61d4101eed6c40cab7ddc8` |
| Public mutation suite | `efforthours-public-synthetic-mutations/0.8.0`; `sha256:ab7d3ad79a33cba14d3837211433f94965cd98d7def64e08434a4820f77386c7` |
| Seed fallback | `seed-rules/0.4.0` |
| Policy | `repository-model-admission/1.0.0` |

The measurement runner rejects any different digest, identity, predecessor
reference, suite, or holdout state. No validation or test input is accepted.

## Frozen execution

Workflow `.github/workflows/repository-candidate-measurement.yml` runs five paired
fresh seed/candidate processes for each deterministic small, medium, and large
saved-evidence shape on GitHub-hosted Windows, Linux, and macOS runners. It also:

- reproduces all 88 public mutation cases and 339 assertions on Linux;
- stages separate seed and candidate tool installations and measures their size;
- runs the public-safe mixed scanner benchmark on every platform; and
- aggregates raw platform records without treating normalized bytes as a pass.

The workflow records its exact dispatch commit, run ID, attempt, environments,
individual runs, output digests, scanner results, and package sizes. Generated
artifacts contain no private repository data or machine-specific local paths.

## Unchanged gates

| Gate | Frozen requirement |
| --- | --- |
| Public mutations | `339/339` applicable assertions pass |
| Canonical bytes | Raw candidate bytes are identical across all three platforms and within repeated runs |
| Median latency | Each platform/shape is at most `max(seed × 1.15, seed + 250 ms)` |
| Slowest latency | Each platform/shape is at most `max(seed × 1.25, seed + 500 ms)` |
| Peak working set | Each platform/shape is at most `max(seed × 1.15, seed + 64 MiB)` |
| Installed package | Candidate increase is at most `25 MiB` |
| Scanner and safety | Applicable scanner thresholds, unchanged target fingerprints, offline operation, no dependency installation, and no target execution all pass |

Machine timing and memory observations are deliberately confined to this manual
measurement workflow; they are not ordinary runner-speed CI gates.

## Decision rule

Any failed gate retires this exact candidate and keeps validation/test closed.
Only a complete pass permits a separate finite candidate-manifest, resource-budget,
and validation-selection-rule freeze. Until that freeze is checked in, candidate
manifest and validation authorization remain `false`.

## Status

**Inputs frozen; measurement not yet run.** Results, workflow provenance, exact
artifact digests, and the pass-or-retire decision will be added without changing
the boundary above.
