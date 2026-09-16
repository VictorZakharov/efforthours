# Frozen fixture dependency alert dispositions

## Decision

Effective August 14, 2026, the five alerts below are dismissed in GitHub as
`not_used`. This is a narrow exposure disposition, not a claim that the advisories
or version matches are false. The declared versions are vulnerable if installed
and executed in the affected configurations.

In this repository the declarations are project-authored text inputs for static
analyzer and mutation checks. EffortHours does not restore their dependencies,
create their lock files, run their package scripts or test runners, invoke the Go
toolchain, or ship dependency code from them. No affected third-party package body
is present in the fixture trees or the EffortHours build/runtime dependency graph.

Changing a listed manifest in place would change a frozen fixture tree and its
repository source digest. That would break the immutable relationship between the
fixture, suite case, saved estimate, and mutation report. If a future checkpoint
needs a patched declaration as analyzer evidence, it must add a new fixture/case
and suite version while retaining these historical artifacts.

## Alert inventory

| Alert and advisory | Dependency | Manifest and case | Declared and affected versions | Frozen report identity |
| --- | --- | --- | --- | --- |
| [#8](https://github.com/VictorZakharov/efforthours/security/dependabot/8), [GHSA-5xrq-8626-4rwp](https://github.com/advisories/GHSA-5xrq-8626-4rwp) / CVE-2026-47429 | `vitest` (direct development declaration) | `fixtures/frontend-accessibility-tests/package.json`; case `frontend-accessibility-tests` | Declared `4.0.0`; vulnerable `>= 4.0.0, < 4.1.0`; first patched `4.1.0` | Yes — `sha256:fc10583062a35d1e42cb1b5bcfb6ec749b99f4959ec048529faf04b7da4dd2ec` |
| [#9](https://github.com/VictorZakharov/efforthours/security/dependabot/9), [GHSA-5xrq-8626-4rwp](https://github.com/advisories/GHSA-5xrq-8626-4rwp) / CVE-2026-47429 | `vitest` (direct development declaration) | `fixtures/frontend-component-tests/package.json`; case `frontend-component-tests` | Declared `4.0.0`; vulnerable `>= 4.0.0, < 4.1.0`; first patched `4.1.0` | Yes — `sha256:8b00299eb5e3b7d1c4fe1b0824984b862e25d2b9c46c9e1e90d4d8275c4fa1a9` |
| [#10](https://github.com/VictorZakharov/efforthours/security/dependabot/10), [GHSA-5xrq-8626-4rwp](https://github.com/advisories/GHSA-5xrq-8626-4rwp) / CVE-2026-47429 | `vitest` (direct development declaration) | `fixtures/javascript-workspace-graph/packages/domain/package.json`; case `javascript-workspace-graph` | Declared `4.0.0`; vulnerable `>= 4.0.0, < 4.1.0`; first patched `4.1.0` | Yes — `sha256:ea5bc878f21fc63ba2a8ae7e6df830aa7856bed5094b088270f3407226b5311a` |
| [#11](https://github.com/VictorZakharov/efforthours/security/dependabot/11), [GHSA-5xrq-8626-4rwp](https://github.com/advisories/GHSA-5xrq-8626-4rwp) / CVE-2026-47429 | `vitest` (direct development declaration) | `fixtures/mixed-boundary-graph/web/packages/app/package.json`; case `mixed-boundary-graph` | Declared `4.0.0`; vulnerable `>= 4.0.0, < 4.1.0`; first patched `4.1.0` | Yes — `sha256:6cebdebdc779b09e46e2565981316501f478e4103d2f9f2d0e6e63cabe42c47e` |
| [#12](https://github.com/VictorZakharov/efforthours/security/dependabot/12), [GHSA-mh63-6h87-95cp](https://github.com/advisories/GHSA-mh63-6h87-95cp) / CVE-2025-30204 | `github.com/golang-jwt/jwt/v5` (direct runtime declaration in the synthetic module) | `fixtures/go-security/go.mod`; case `go-security` | Declared `v5.2.0`; vulnerable `>= 5.0.0-rc.1, < 5.2.2`; first patched `v5.2.2` | Yes — `sha256:8894a32546506d5d12ad7745e893ccf8c493d8a4eef0d768a0c1a42cd4414979` |

Paths in the table are relative to this directory. The four JavaScript cases are
digest-pinned in suites `0.7.0` and `0.8.0`, their `seed-rules/0.3.0` estimates,
and both corresponding baseline reports. The retired repository-candidate
measurement also pins the complete suite-0.8.0 mutation report. The Go case is
digest-pinned in `go-0.1.0.suite.json`, its `seed-rules/0.4.0` estimate, and the
standalone Go baseline report. None is an EffortHours runtime dependency.

## Guardrail

`CalibrationFixtureDependencyDispositionTests` enforces the exact five-entry
allowlist, the complete fixture-tree fingerprints, suite-to-estimate source
digests, and saved-report candidate identities without invoking npm, Vitest, Go,
or target code. A vulnerable declaration found anywhere else is not covered by
this disposition and must be removed, upgraded, or separately reviewed. A future
change to any listed fixture must create a versioned successor rather than update
the frozen case in place.

The GitHub dismissals use reason `not_used` and reference issue
[#103](https://github.com/VictorZakharov/efforthours/issues/103). Reopen an alert
if the fixture boundary changes, dependency code becomes present, or any workflow
starts restoring or executing these fixtures.

## September 16, 2026 review

The same `not_used` disposition applies to five additional alerts: a newer
Vitest advisory on the same four frozen manifests and an Angular advisory on one
additional frozen manifest. This brings the reviewed inventory to ten alerts
across six exact manifests, including the original Go declaration. These are
valid vulnerable-version matches, with no installed or executed affected package
code in this repository.

| Alert and advisory | Dependency | Manifest and case | Declared and affected versions | Frozen report identity |
| --- | --- | --- | --- | --- |
| [#13](https://github.com/VictorZakharov/efforthours/security/dependabot/13), [GHSA-82fw-gwwq-j7x9](https://github.com/vitest-dev/vitest/security/advisories/GHSA-82fw-gwwq-j7x9) | `vitest` | `fixtures/frontend-accessibility-tests/package.json`; case `frontend-accessibility-tests` | Declared `4.0.0`; vulnerable `>= 2.1.0, < 4.1.11`; first patched `4.1.11` | `sha256:fc10583062a35d1e42cb1b5bcfb6ec749b99f4959ec048529faf04b7da4dd2ec` |
| [#14](https://github.com/VictorZakharov/efforthours/security/dependabot/14), [GHSA-82fw-gwwq-j7x9](https://github.com/vitest-dev/vitest/security/advisories/GHSA-82fw-gwwq-j7x9) | `vitest` | `fixtures/javascript-workspace-graph/packages/domain/package.json`; case `javascript-workspace-graph` | Declared `4.0.0`; vulnerable `>= 2.1.0, < 4.1.11`; first patched `4.1.11` | `sha256:ea5bc878f21fc63ba2a8ae7e6df830aa7856bed5094b088270f3407226b5311a` |
| [#15](https://github.com/VictorZakharov/efforthours/security/dependabot/15), [GHSA-82fw-gwwq-j7x9](https://github.com/vitest-dev/vitest/security/advisories/GHSA-82fw-gwwq-j7x9) | `vitest` | `fixtures/mixed-boundary-graph/web/packages/app/package.json`; case `mixed-boundary-graph` | Declared `4.0.0`; vulnerable `>= 2.1.0, < 4.1.11`; first patched `4.1.11` | `sha256:6cebdebdc779b09e46e2565981316501f478e4103d2f9f2d0e6e63cabe42c47e` |
| [#16](https://github.com/VictorZakharov/efforthours/security/dependabot/16), [GHSA-82fw-gwwq-j7x9](https://github.com/vitest-dev/vitest/security/advisories/GHSA-82fw-gwwq-j7x9) | `vitest` | `fixtures/frontend-component-tests/package.json`; case `frontend-component-tests` | Declared `4.0.0`; vulnerable `>= 2.1.0, < 4.1.11`; first patched `4.1.11` | `sha256:8b00299eb5e3b7d1c4fe1b0824984b862e25d2b9c46c9e1e90d4d8275c4fa1a9` |
| [#17](https://github.com/VictorZakharov/efforthours/security/dependabot/17), [GHSA-hh8m-fm6v-7cvg](https://github.com/angular/angular/security/advisories/GHSA-hh8m-fm6v-7cvg) | `@angular/core` | `fixtures/frontend-angular/package.json`; case `frontend-angular` | Declared `21.0.0`; vulnerable `>= 21.0.0, < 21.2.20`; first patched `21.2.20` | `sha256:d30ffc9741750bf6fe887a2e4fe2697c5f548fa93c82ad2acc608900bb816038` |

The Angular case is already pinned in suites `0.5.0`, `0.6.0`, `0.7.0`, and
`0.8.0`, its `seed-rules/0.3.0` estimate, and all four baseline reports.
Its complete normalized fixture-tree fingerprint is
`sha256:c0138d8c3c88978e1c21753b37c6735dfa62c0f93b7c6ee3206ace2127943068`.
The companion advisory guardrail now verifies that lineage alongside the
unchanged original five-manifest guardrail.

The Vitest issue requires running the affected mock server; Angular's issue
requires executing affected compiled directive host bindings. The review checked
the exact manifests, fixture trees, root workflows, and project tooling: the
fixtures are static analyzer input, with no npm restore, test runner, Angular
build, dependency bodies, or shipped runtime path. The version strings remain
vulnerable for anyone who installs them elsewhere.

Dependabot PRs [#213](https://github.com/VictorZakharov/efforthours/pull/213),
[#214](https://github.com/VictorZakharov/efforthours/pull/214),
[#215](https://github.com/VictorZakharov/efforthours/pull/215), and
[#216](https://github.com/VictorZakharov/efforthours/pull/216) therefore receive
fixture dispositions rather than in-place upgrades. The three Vitest PRs fail
the exact frozen-declaration guardrail. Angular's initially green CI lacked an
equivalent tree check; adding that check closes the gap without rewriting history.

This does not disable Dependabot, add a package-wide ignore, or cover a future
advisory automatically. Review new alerts individually and reopen these
dispositions if dependency code is introduced or a fixture is installed, built,
executed, or shipped. Actual EffortHours build/test dependencies continue to
receive ordinary upgrades; Microsoft.NET.Test.Sdk advances to `18.10.0` with its
coverage and test-platform dependencies, retaining the stable Roslyn analyzer.
