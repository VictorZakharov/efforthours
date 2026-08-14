# Logical-capability v0.2 measured operational rejection

## Status

`efforthours-public-readiness/0.9.0` **retires exact candidate
`logical-capability/0.2.0` after two of seven measured operational gates fail**.
No candidate manifest or selection rule is frozen, no repository model is
admitted or shipped, and all validation and test source, outputs, and labels
remain unopened.

This is a development-only rejection under
`repository-model-admission/1.0.0`. It is not held-out accuracy evidence,
empirical production validation, or permission to weaken a public guardrail.

## Measured result

The exact candidate passed five gates:

- all 27 median-latency, slowest-latency, and sampled peak-working-set
  platform/shape comparisons stayed within their frozen paired limits;
- the conservatively staged installed candidate added `0.9106 MiB`, below the
  `25 MiB` limit; and
- all three 99,604-line mixed static scanner runs retained unchanged target
  fingerprints plus offline, non-executing, and no-install signals. No frozen
  cross-platform scanner latency or memory threshold applies.

It failed two gates:

1. Public aggregate mutation suite `0.8.0` passed `314/339` assertions across 88
   cases. The candidate applied to 66 .NET, JavaScript/TypeScript, frontend, and
   mixed cases; 22 SQL/Python cases retained the named `seed-rules/0.4.0`
   fallback. The 25 failures cover exact duplication (2), frontend exact
   duplication (6), meaningful API behavior (6), external integration (4),
   generated customization (3), bounded semantic clone behavior (3), and one
   multi-package review boundary.
2. Five repeated fresh processes for each small, medium, and large saved-evidence
   shape were stable within each of Windows, Linux, and macOS, and evidence
   digests matched across them. Raw seed and candidate JSON bytes differed
   between Windows and the Unix runners, so the frozen byte-identical gate fails.
   LF-normalized seed and candidate digests match for every shape, isolating the
   difference to CRLF versus LF serialization. The policy requires raw byte
   identity, so this diagnostic does not convert the failure into a pass.

The final decision is `rejected-measured-operational-preflight`. Candidate
manifest freeze and validation authorization remain `false`.

## Artifacts and provenance

The machine-readable artifacts are:

- [`0.9.0.measured-operational-report.json`](../0.9.0.measured-operational-report.json),
  normalized digest
  `sha256:47c9d916ac8a728a549086f8118d40ed785a797fb975f7570d39e46ae1163275`;
- [`0.9.0.candidate-operational-preflight.json`](../0.9.0.candidate-operational-preflight.json),
  normalized digest
  `sha256:a958aed00f2a0836d813590ec7eb09fd6d88ba6d7159219131c4c977a285e841`;
  and
- [`0.8.0.candidate-mutation-report.json`](0.8.0.candidate-mutation-report.json),
  normalized digest
  `sha256:9943f4826ddcdb7536d2759db61e1b1d8d5e55dbb64425020b4e5f3aafe3e383`.

They retain exact model digest
`sha256:f53b5af09b5adf0d3efed5339e9309156f026b3378c6b69e979467b92524ae93`,
prior operational-preflight digest
`sha256:609307d5a366b52c18118db2ef9f79e46d86565b5419aa767e0cbbf7f1fe8ec8`,
and public-suite digest
`sha256:ab7d3ad79a33cba14d3837211433f94965cd98d7def64e08434a4820f77386c7`.
GitHub Actions run `31758839188` measured implementation commit
`f783fa2df25054e7969d53805e77e0e5d66b92c5` on GitHub-hosted Windows, Linux,
and macOS runners with five paired fresh seed/candidate processes per shape.
Complete environments, individual runs, resource values, digests, limitations,
and gate rationales are embedded in the measured report.

## Reproduction

Workflow `.github/workflows/repository-candidate-measurement.yml` builds the exact
source revision, runs the three platform jobs, stages the installed package only
on Linux, reproduces the public mutation report, and aggregates the result. The
aggregate command returns policy exit `3` for this rejection; the workflow then
verifies the explicit rejection statuses and preserves the artifacts as a
successful measurement run.

The workflow has no validation or test input and does not execute any measured
target repository.

## Next boundary

Retire `logical-capability/0.2.0`. Address cross-platform canonical output and the
25 public mutation failures using development-visible evidence only. Any changed
challenger requires a new candidate, model, estimator, and implementation
identity plus the complete numerical and operational preflight. Validation and
test must remain unopened until a later finite candidate manifest and selection
rule are frozen after every development gate passes.
