# Changelog

Significant user-visible EffortHours changes are recorded here. The project follows
Semantic Versioning once a package version has been released; prerelease versions
may still change public contracts with explicit documentation.

## Unreleased

### Changed

- Change estimation advances to experimental `change-seed/0.3.0`. Repeated
  repository work-item partitions for one existing or modified capability now
  share a bounded evidence-derived logical marginal budget instead of contributing
  their summed repository prior. Distinct added capabilities remain additive.
- Logical modification and fallback budgets use capped edit-region bands per path
  rather than growing linearly with diff fragmentation. A capability newly
  detected on a modified artifact receives a meaningful modification floor.
- Separate five-family development/validation diagnostics record the correction
  without rewriting frozen alpha.2 reports or consulting the withheld test
  comparison. The teacher-only results remain weak supervision, not calibration or
  an accuracy claim.

## 0.9.0-alpha.2 - 2026-08-10

### Changed

- Change estimation advances to experimental `change-seed/0.2.0`. Existing
  capability modifications now require changed normalized non-file evidence,
  repeated category/path evidence shares one diminishing marginal budget, and
  final-delta comprehension, validation, and review are emitted once.
- Modified artifacts use 30% of the corresponding new-artifact edit-region rates;
  scope membership alone no longer assigns specialized UI or other boundary work.

### Fixed

- Passing a Change estimate to repository calibration authoring now points to
  `eh calibration change-scaffold` instead of printing unrelated repository-schema
  failures.

## 0.9.0-alpha.1 - 2026-08-09

### Added

- The product identity is EffortHours, distributed as
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
- Sanitized `host-review-measurement/1.0.0` session records and
  `host-review-comparison-metrics/1.0.0` compact/broader-source benchmarks. The
  first three-repository public diagnostic reports payload and agreement evidence,
  explicitly withholds unavailable token/time/cost ratios, and selects no default
  review budget.
- Static, digest-verified LCOV and Cobertura coverage parsing with privacy-safe
  project/package scope mapping. Measured coverage is distinct from and takes
  precedence over a conflicting same-scope declared threshold; the public mutation
  baseline now has 51 cases and 170 passing relations.

### Changed

- Product, architecture, benchmark, calibration, and milestone records now live
  under an indexed `docs/` directory; standard GitHub community files remain at
  the repository root, and the release suite verifies relative Markdown links.
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
- Measured coverage formats other than LCOV and Cobertura are inventoried but not
  parsed, and checked-in reports can be stale because EffortHours does not rerun
  tests on the default path.

No version in this file is a public release until a matching immutable Git tag and
package/release record exist.
