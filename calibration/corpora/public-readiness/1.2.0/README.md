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
and validation-selection-rule freeze. The measured result below is a complete
pass, so this checkpoint performs that separate freeze without changing any gate.

## Status

**All seven measured gates and all 12 total operational gates passed.** The exact
finite manifest is frozen, blind validation is authorized but remains unopened,
and test remains sealed. No candidate is admitted or shipped;
`seed-rules/0.4.0` remains the product estimator and required fallback.

## Measured result

GitHub Actions [run `31892555353`](https://github.com/VictorZakharov/efforthours/actions/runs/31892555353)
measured commit `40ad39c6dda9029c90aed60a37a2e93c0ecb1ce9`, attempt 1,
on resolved Ubuntu x64, macOS arm64, and Windows x64 environments using .NET
`10.0.11`.

| Gate | Observed result |
| --- | --- |
| Public mutations | `339/339` assertions; 66 candidate cases and 22 explicit seed-fallback cases |
| Canonical bytes | Raw seed and candidate bytes identical for all three shapes on all three platforms and within repeats |
| Median latency | All 9 platform/shape pairs passed; candidate medians ranged from `417.9008` to `631.8582 ms` |
| Slowest latency | All 9 pairs passed; candidate slowest runs ranged from `516.8569` to `676.6005 ms` |
| Peak working set | All 9 pairs passed; candidate peaks ranged from `52.4922` to `84.9453 MiB` |
| Installed package | `0.9871 MiB` increase, below the frozen `25 MiB` limit |
| Scanner and safety | All three 99,604-line scans passed with unchanged fingerprints and offline/non-executing/no-install signals |

The platform-specific timings are environment observations, not universal speed
claims or ordinary CI thresholds. The checked-in raw records preserve every
individual run and applicable limit.

## Candidate and validation freeze

[`1.2.0.candidate-manifest.json`](1.2.0.candidate-manifest.json) freezes:

- the seed baseline plus the sole challenger `logical-capability/0.3.0`;
- source commit, build inputs, complete feature and hyperparameter boundary,
  exact artifact digest, dependencies, license, runtime, explanation, and fallback;
- all 15 development records, all 18 excluded holdout families, and an empty
  contamination set;
- the unchanged latency, memory, package, scanner, determinism, and mutation
  budgets plus the exact measured resource record; and
- the validation eligibility rule, primary WAPE ordering, `0.01` tie boundary,
  ordered simplicity/sharpness/runtime/ID tie-breakers, and learned-model margin.

Validation is now `authorized-but-unopened`. Its labels and candidate outputs were
not generated for this checkpoint. Test labels remain unauthored, its body is not
present, and test access is forbidden until validation selects exactly one frozen
candidate.

## Artifacts

| Artifact | Normalized SHA-256 |
| --- | --- |
| `1.2.0.measured-operational-report.json` | `sha256:f3d25627394e8efb3faa3c51399e4da16312c31aa9aa3eeaa2f90d436957cabc` |
| `1.2.0.candidate-operational-preflight.json` | `sha256:01db86e772e6cc2582441d82404845fdc4b73d1e6b5a2e7d77169b3315448b17` |
| `0.8.0.candidate-mutation-report.json` | `sha256:59fb04a4e677795198a2b4c81d5aacf871eef0fdbd67c0bbc577f7992b5f79c0` |
| `platforms/linux.json` | `sha256:e99d99666b9a3eb6915078b56d3f5451518de7e6a646bc612f8bf459b614b3e3` |
| `platforms/macos.json` | `sha256:46fa9fc697b9d977d4fceeb9e8e31b8c6b85ff1054ff5450b2899f3c026586a3` |
| `platforms/windows.json` | `sha256:3bd4b0d9fd1bd58aa82cfd50a4b6f141da4533f06e5db3db534f870f94ac673b` |
| `1.2.0.candidate-manifest.json` | `sha256:206b3955d53af9902996b588e9255ab9396e7b7624731a6d6e09896ce5026f23` |

The generated aggregate still says `candidate-freeze-pending` because it is the
unaltered measurement output produced before this separately reviewed manifest.
The manifest records the subsequent freeze; neither artifact is rewritten to
manufacture a circular result.

## Next boundary

Issue #107 may author blind validation labels without candidate guidance, compile
their exact provenance, generate seed and challenger outputs once, and apply the
frozen selection rule. Test remains sealed throughout that step.
