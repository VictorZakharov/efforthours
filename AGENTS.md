# AGENTS.md

## Scope

These instructions apply to the entire EffortHours repository.

## Mission

Build and maintain an offline-first .NET 10 CLI that estimates **Equivalent Human
Effort (EHE)**: the counterfactual time one competent senior contractor,
unfamiliar with the business domain and not using AI, would need to recreate a
repository's current functional and quality state from a clear specification.

EHE is replacement effort. It is not actual labor, a timesheet, an authorship
claim, a productivity score, or an invoice.

## Read before changing the repository

Read these documents completely before making changes:

1. `docs/PRODUCT.md`
2. `docs/ESTIMATION_MODEL.md`
3. `docs/PLAN.md`
4. `README.md`

Then read the documents that govern the area being changed:

- reporting or explanation: `docs/REPORTING.md`;
- pricing or default rates: `docs/PRICING.md`;
- repository calibration, labels, evaluation, mutation guardrails, or model
  admission: `docs/CALIBRATION.md` and `docs/MODEL_REVIEWS.md`;
- Change EHE selectors, normalization, ranges, or contributions:
  `docs/CHANGE_ESTIMATION.md`;
- Change portfolios or attribution safeguards: `docs/CHANGE_PORTFOLIOS.md`;
- Change labels, metrics, or admission: `docs/CHANGE_MODEL_ADMISSION.md` plus the
  applicable rubric under `calibration/rubrics/`;
- host-review packets, queries, adjustments, disclosure, or budgets:
  `docs/HOST_REVIEW.md` and `docs/HOST_REVIEW_MEASUREMENT.md`;
- analyzer behavior: the applicable `docs/*_ANALYSIS.md` boundary;
- packaging, release automation, visibility, or NuGet publication:
  `docs/RELEASING.md`; and
- C# responsibility moves or file-size changes: `docs/CODE_BUDGETS.md` and
  `eng/file-budgets.json`.

Use `docs/README.md` as the complete documentation index. If an implementation
request conflicts with a documented decision, surface the conflict and update the
decision explicitly. Never change estimation semantics silently.

## Current implementation baseline

- The primary distribution is the `EffortHours.Tool` .NET global tool and its
  installed command is `eh`.
- The common scanner and mixed-repository pipeline are implemented with versioned
  evidence, report, model, diagnostic, rate, review, calibration, and Change
  contracts.
- Static analyzer families cover .NET; JavaScript/TypeScript and frontend assets;
  SQL; Python and Jupyter; Go; Java; Kotlin/JVM; Shell and PowerShell;
  Terraform/HCL; PHP/Composer; Rust/Cargo; Docker/Compose; and C/C++.
- JavaScript and JSX use Acornima ASTs. TypeScript and TSX remain explicitly
  token-backed. The other ecosystem documents state their exact parser/token and
  uncertainty boundaries.
- Repository estimation uses transparent `seed-rules/0.4.0` priors. It remains
  experimental and numerically uncalibrated; do not describe it as production-
  ready.
- Current Change source reports use
  `change-seed/0.18.0+seed-rules/0.4.0`. Only the documented 0.6.0 Stage A subset
  has passed a model-authored logical gate for eligible 4-to-32-hour changes. That
  is not empirical production validation and does not admit later ecosystem
  extensions.
- `change-portfolio/0.1.0` provides experimental repeated-PR, manifest, and
  author-period reconciliation. Identity and time are selectors only.
- Calibration and review infrastructure is implemented, but no local ML model has
  been selected or added. Public labels remain disclosed weak supervision with
  the maturity recorded in their artifacts.
- Host-AI review is optional, provider-neutral, and non-applying. The local
  baseline remains complete without it, and no automatic review budget is
  selected.
- Reproducible fresh-process scanner benchmarks, memory-only unit fixtures,
  process-level CLI tests, optional external caching, and source-file budget gates
  are in place.

Refer to `CHANGELOG.md` for release history. Do not turn this file into a milestone
or per-release log.

## Non-negotiable product semantics

- Estimate the current artifact, not historical rework, abandoned approaches, or
  elapsed development time.
- Ignore commits, churn, contributors, timestamps, and branch activity as effort
  signals. Explicit Change and portfolio commands may read revisions, identity,
  and time only to select immutable final changes.
- Prefer functional and quality equivalence over line-for-line reproduction.
- Assume a sensible modern 2026-equivalent implementation while preserving
  meaningful compatibility, protocols, formats, and external constraints.
- Do not reward duplication, dead code, generated code, vendored code, accidental
  complexity, or mechanical volume.
- Value generated artifacts only through supported evidence for configuration,
  templates, integration, validation, or safely isolated maintained
  customization. Otherwise exclude the generated body and explain why.
- Reflect tests and documentation at the level represented. On the fast static
  path, label configured coverage `declared-assumed` and parsed supported reports
  `measured`; measured coverage takes precedence within the same scope.
- Keep reasonable manual validation and debugging separate from automated-test
  creation.
- Warn when a checkout was not verified as working. Do not add hypothetical repair
  work to represented EHE.
- Keep professionalization or remediation gaps separate from represented effort
  and pricing.
- Support both `implementation` and `recreation` profiles, plus an optional
  specification input.
- Build repository-first, evidence-backed work items, normally about 0.5 to 8
  expected hours each. Change admission may impose a smaller decomposition
  boundary.
- Estimate hours before pricing. A rate card or override must never change EHE.
- Preserve stable evidence IDs, explicit uncertainty, and calculation lineage for
  every material estimate.

## Architecture and implementation rules

- Prefer one `eh` executable with composable commands and reusable libraries.
- Keep language-neutral contracts and pipeline behavior separate from ecosystem
  analyzers.
- Separate observed evidence, inferred classification, estimated work, review
  adjustments, and pricing in both code and serialized output.
- Favor compiler or parser evidence over textual guesses when practical. Lines of
  code may be supporting evidence but never the principal effort model.
- Keep identical inputs deterministic across scans, estimates, projections, and
  explanations for the same configuration and versions.
- Put structured data on stdout and diagnostics on stderr.
- Make long-running analysis cancellable and bounded in memory.
- Treat source trees as untrusted input. Do not follow links outside selected
  scope or emit secrets, configured values, absolute target paths, or source
  excerpts by default.
- In ordinary static mode, do not execute target code, build scripts, tests,
  compilers, package managers, or generators; do not install dependencies, access
  the network, inspect Git history, or write into the target repository.
- Preserve Windows, Linux, and macOS behavior.
- Follow `eng/file-budgets.json`. Start splitting responsibilities near 80% of a
  ceiling, and never add or raise a ratchet override without an explicit
  architectural rationale.

## Testing expectations

- Add tests for every behavioral change.
- Keep `tests/EffortHours.Tests` storage-independent. Use in-memory repository and
  cache abstractions; reserve physical files, Git repositories, subprocesses, and
  installed-tool checks for `tests/EffortHours.EndToEndTests` or explicitly
  invoked benchmarks.
- Use small synthetic fixtures for precise behavior and curated, redistributable
  fixtures for realistic integration coverage.
- Validate serialized JSON against checked-in versioned schemas.
- Use golden files only when their diffs are reviewable and semantically useful.
- Guard formatting, generation, duplication, excluded content, and history from
  inflating effort. Also test that meaningful behavior, tests, documentation,
  integrations, security, delivery, and complexity affect the intended category.
- Keep calibration partitions isolated by repository family.
- Cover CLI exit codes, stdout/stderr separation, deterministic output,
  cancellation, and offline/read-only safety at the process boundary.
- Keep performance claims tied to reproducible benchmark commands, recorded
  hardware, and explicit limitations.

Use the normal validation sequence from the repository root:

```text
dotnet restore EffortHours.slnx --configfile NuGet.Config --locked-mode
dotnet format EffortHours.slnx --no-restore --verify-no-changes --severity info
dotnet build EffortHours.slnx --no-restore --configuration Release
dotnet test tests/EffortHours.Tests/EffortHours.Tests.csproj --no-build --no-restore --configuration Release
dotnet test tests/EffortHours.EndToEndTests/EffortHours.EndToEndTests.csproj --no-build --no-restore --configuration Release
```

Run narrower tests first when appropriate, but complete validation should remain
proportional to the risk and affected contracts.

## Tooling and worktree discipline

- Use ripgrep (`rg`) for text and file searches. It is installed in the
  development environment. If a restricted shell cannot resolve it, treat that as
  a sandbox or `PATH` visibility issue and use the approved installation rather
  than concluding that ripgrep is absent.
- Preserve unrelated user changes in a dirty worktree. Do not stage, rewrite, or
  discard them.
- Prefer non-interactive Git commands. Never use destructive resets or checkouts
  unless the user explicitly requests them.
- Use focused topic branches. Keep generated packages, temporary worktrees, and
  other local artifacts out of commits.

## Open-source and documentation requirements

- Treat every committed file, comment, fixture, test name, and generated artifact
  as public material.
- Never commit credentials, private client code, proprietary evidence,
  machine-specific personal data, or copied material without redistribution
  rights.
- Record the source, version, and license of dependencies, datasets, benchmark
  repositories, models, templates, and substantial copied assets. Prefer terms
  compatible with the MIT-licensed distribution.
- Keep private calibration data separate from public schemas, tooling, and model
  artifacts.
- Preserve conservative compatibility for public contracts and schemas.
- Keep package license metadata at SPDX `MIT` and preserve the root `LICENSE`
  unless the user explicitly changes the license.
- Update living contracts when semantics, schemas, assumptions, or unresolved
  decisions change. Put release history in `CHANGELOG.md`, measurements in their
  benchmark/model-review records, and completed work in Git history—not in
  `AGENTS.md` or the living roadmap.
- Label experimental heuristics and models honestly. Prefer explicit uncertainty
  over unsupported precision.

## Pull-request handoff

Agents may create commits, push topic branches, and open or update pull requests.
Never merge a pull request or enable auto-merge; the maintainer performs the final
review and merge.
