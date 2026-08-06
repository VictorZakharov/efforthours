# Changelog

Significant user-visible Fairbill changes are recorded here. The project follows
Semantic Versioning once a package version has been released; prerelease versions
may still change public contracts with explicit documentation.

## Unreleased

### Added

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

### Known limitations

- `seed-rules/0.2.1` and `change-seed/0.1.0` are experimental and uncalibrated.
- No checked-in corpus has completed genuinely independent correction.
- Multiple pull requests and contributor-period portfolios are not implemented.
- TypeScript and TSX analysis is token-backed rather than compiler-backed.

No version in this file is a public release until a matching immutable Git tag and
package/release record exist.
