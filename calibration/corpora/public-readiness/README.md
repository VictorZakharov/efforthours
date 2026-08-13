# Public repository-admission sampling cohort

## Status

`efforthours-public-readiness/0.1.0` freezes the source cohort required by
`repository-model-admission/1.0.0` before any EffortHours candidate total is
generated or inspected for these snapshots.

The machine-readable
[`0.1.0.sampling-plan.json`](0.1.0.sampling-plan.json) contains 33 public
repository families: 11 .NET, 11 JavaScript/TypeScript, and 11 mixed
.NET/JavaScript/TypeScript families. It records immutable commit and Git-tree
identities, license blob and SHA-256 checksums, partition ownership, product-shape
tags, and a source-only size measurement. Source archives and source bodies are
not copied into EffortHours.

This is a **source-only sampling checkpoint**, not a calibration corpus. It has no
EffortHours evidence bundles, estimate reports, candidate totals, teacher hours,
reviewed targets, evaluation metrics, or model-admission result. All 33 rows have
an explicit `not-authored` label status. The six previously labeled repository
families remain the only public repository-EHE weak-supervision records.

## Frozen matrix

The implementation-profile matrix is:

| Primary stratum | Development | Blind validation | Sealed test | Total |
| --- | ---: | ---: | ---: | ---: |
| .NET | 5 | 3 | 3 | 11 |
| JavaScript/TypeScript | 5 | 3 | 3 | 11 |
| Mixed .NET/JavaScript/TypeScript | 5 | 3 | 3 | 11 |
| **Total** | **15** | **9** | **9** | **33** |

Every validation and test cell contains exactly one small, one medium, and one
large source tree. Across the whole matrix, each policy shape appears in at least
three families and in at least one validation or test family:

- library or SDK;
- CLI or desktop application;
- backend service;
- frontend or UI application;
- multi-package workspace or monorepository; and
- integration-heavy system.

The plan also records a descriptive product shape for every family. Shape tags are
sampling metadata only; they are not estimator features or effort multipliers.

## Source-size boundary

Size uses `repository-source-shape-file-count/1.0.0`, not lines of code,
candidate EHE, repository popularity, or compressed archive size. The metric
counts blobs in the complete recursive Git tree whose extensions belong to the
frozen .NET and JavaScript/TypeScript source/UI set, after excluding conventional
dependency, generated, coverage, and build-output paths.

| Band | Eligible files |
| --- | ---: |
| small | 0-249 |
| medium | 250-1,999 |
| large | 2,000 or more |

Eligible bytes are retained as a diagnostic only. They do not select the band and
never become an effort signal. The full extension and exclusion lists are part of
the frozen plan. A later change to the metric or thresholds requires a new plan
version and cannot reassign these families silently.

## Partition isolation

Repository family owns the partition across repository and Change calibration.
Three selected families already had real Change records, so this plan inherits
rather than overrides their assignments:

| Family | Inherited partition |
| --- | --- |
| `spectreconsole/spectre.console` | validation |
| `axios/axios` | validation |
| `colinhacks/zod` | development |

Every other family receives one new frozen assignment. Later revisions, forks,
and both estimation profiles from any listed family must remain in that same
partition. Existing repository and real-Change corpus assignments are checked by
the end-to-end artifact test.

## License and source provenance

The source metadata was checked through GitHub's repository, commit, recursive
tree, and license APIs on 2026-08-13. Every selected repository was active,
non-forked, and published under MIT, Apache-2.0, or BSD-3-Clause terms at the
pinned commit. Each row records:

- the public repository URL and default branch observed at freeze time;
- the exact 40-hex commit and Git-tree object IDs;
- confirmation that the recursive tree listing was complete;
- a commit-pinned codeload archive reference;
- the detected license expression and in-tree path;
- the exact license Git-blob object ID and content SHA-256; and
- the redistribution decision.

Git history, commit age, authorship, churn, contributor activity, stars, forks,
issues, and actual labor were not sampling or effort signals. Default branches are
recorded only to explain snapshot selection; the pinned commit and tree are the
immutable source identity.

## Label and holdout boundary

Future labels must follow `ehe-work-item/1.1.0` and remain explicit
`teacher-estimate` weak supervision until a genuinely distinct review advances
their maturity.

- Development labels may support diagnosis, feature design, or fitting after this
  source freeze.
- Validation labels must be authored without candidate values and remain
  unavailable until the complete finite candidate manifest is frozen.
- Test labels must be authored blind, sealed with a precommitted digest and
  custody record, and revealed only after validation selects one frozen candidate.

If a validation or test disagreement influences an analyzer, feature, prior,
threshold, or uncertainty width, that family becomes diagnostic-only for the
attempt and cannot satisfy a held-out gate. The public repository must never
contain an unsealed test label before the one-time decision.

## Next checkpoint

The next safe step is to reproduce each pinned snapshot, verify its tree and
license identity, generate source evidence and blind authoring packets without
displaying candidate values, and complete the development-label pass. Blind
validation and sealed-test custody must be implemented before those labels are
opened. No estimator fitting or candidate comparison is authorized by this
sampling checkpoint.
