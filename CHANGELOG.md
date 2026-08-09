# Changelog

Significant user-visible EffortHours changes are recorded here. The project follows
Semantic Versioning once a package version has been released; prerelease versions
may still change public contracts with explicit documentation.

## Unreleased

### Added

- The pre-public product identity is now EffortHours, distributed as
  `EffortHours.Tool` with the `eh` command. Projects, namespaces, schema URNs,
  repository metadata, cache/ignore conventions, and calibration identities were
  renamed together for the `0.9.0-alpha.1` candidate.
- Public-alpha governance, contribution templates, cross-platform CI, and release
  instructions.
- A manually dispatched NuGet preview workflow with local installation checks and
  short-lived OIDC credentials.
- Repository, commit, range, and single-pull-request Equivalent Human Effort
  estimation for .NET and JavaScript/TypeScript repositories.
- Versioned evidence, estimate, Change, calibration, reporting, and rate-card
  contracts with checked-in schemas.
- Preliminary public repository and synthetic Change calibration corpora, mutation
  guardrails, and blind independent-review handoffs.
- Provider-neutral `host-review/1.0.0` packets, digest-bound capability/evidence/
  scope/selected-source queries, adjustment ledgers, and non-applying validation.
  The local baseline remains complete and offline; no provider is embedded or
  selected.

### Changed

- `.NET` analyzer `0.3.2` no longer treats generic process-command execute calls as
  persistence without data context.
- JavaScript analyzer `0.4.1` no longer treats framework-neutral state/effect calls
  as UI or development benchmark hashbangs as product entry points. Checked-in
  frozen-corpus reevaluations disclose the resulting target-mapping changes; the
  `seed-rules/0.2.1` priors remain unchanged and uncalibrated.
- The CLI now handles the first Ctrl+C cooperatively, emits its cancellation
  diagnostic only on stderr, and returns exit code 130; a second Ctrl+C retains
  immediate termination.
- Change-EHE safeguards now include category-isolated migration, integration, CI,
  container-delivery, and simplification mutations plus pre-start and in-flight
  cancellation, without changing `change-seed/0.1.0`.
- Scanner benchmarks now support mixed generated trees and caller-supplied
  repositories, sample peak working set, distinguish explicit warm-cache passes,
  and verify a before/after target-tree metadata digest.
- The documented performance checkpoint now includes million-line .NET,
  JavaScript/TypeScript, and mixed measurements plus three exact MIT releases and
  the EffortHours development tree; no regression threshold is claimed from the
  single-workstation results.

### Known limitations

- `seed-rules/0.2.1` and `change-seed/0.1.0` are experimental and uncalibrated.
- No checked-in corpus has completed genuinely independent correction.
- Multiple pull requests and contributor-period portfolios are not implemented.
- TypeScript and TSX analysis is token-backed rather than compiler-backed.

No version in this file is a public release until a matching immutable Git tag and
package/release record exist.
