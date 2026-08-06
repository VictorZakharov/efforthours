# AGENTS.md

## Scope

These instructions apply to the entire Fairbill repository.

## Mission

Build an offline-first .NET 10 CLI that estimates **Equivalent Human Effort**: the
counterfactual time one competent senior contractor, unfamiliar with the business
domain and not using AI, would need to recreate a repository's current functional
and quality state from a clear specification.

This is replacement-effort estimation, not reconstruction of actual hours worked.

## Read before changing the repository

Read these root documents completely:

1. `PRODUCT.md`
2. `ESTIMATION_MODEL.md`
3. `PLAN.md`
4. `README.md`
5. `MILESTONE_5.md` when changing evidence-to-effort behavior
6. `MILESTONE_6.md` when changing reporting, explanation, or default pricing
7. `MODEL_REVIEWS.md` when changing priors, calibration labels, or review policy
8. `CHANGE_ESTIMATION.md` when changing future diff, PR, commit, or contribution
   semantics

If an implementation request conflicts with those documents, surface the conflict
and update the relevant decision explicitly. Do not silently change estimation
semantics.

## Settled product constraints

- Target any common software repository eventually.
- Implement .NET and JavaScript/TypeScript analysis first.
- Implement the tool and reusable libraries on .NET 10.
- Ignore Git history, churn, contributors, and timestamps as effort signals.
- A future explicit change-estimation command may use revisions, author identity,
  and time only to select final changes. Ordinary repository estimates remain
  history-free, and selection metadata must never become an effort multiplier.
- Estimate the current artifact, not historical rework or abandoned approaches.
- Prefer functional and quality equivalence over line-for-line reproduction.
- Recreate with sensible modern 2026-equivalent technology while preserving
  meaningful compatibility and external constraints.
- Do not reward duplication, dead code, generated code, vendored code, or accidental
  complexity.
- Value meaningful customization in generated artifacts when it can be distinguished
  from conventional generated output; otherwise exclude the generated body and
  explain the exclusion.
- Reflect tests and documentation at the level actually present.
- On the fastest default path, assume discovered tests pass. Label configured
  coverage as declared-and-assumed and measured coverage as measured.
- Include reasonable manual validation and debugging separately from automated-test
  creation.
- Estimate incomplete or broken repositories as the working system materially
  described, but prominently note that the checkout was not verified as working.
- Keep professionalization or remediation gaps separate from represented effort.
- Support `implementation` and `recreation` estimation profiles.
- Accept an optional specification input.
- Produce repository-level breakdowns before feature-oriented breakdowns.
- Decompose large totals into evidence-backed work items, normally about 0.5 to 8
  expected hours each.
- Keep effort independent from pricing. Apply a dated, configurable market rate
  only after hours are estimated.
- Make every material estimate traceable to evidence and reasoning.
- Require no embedded or remote AI provider for the local baseline estimate.
- Use local ML where it improves agreement with reviewed logical estimates.
- Design compact uncertainty packets and follow-up queries for the surrounding AI
  session; choose budgets only after measuring real implementations.
- Treat provider and privacy choices as the responsibility of the client running
  the AI session.

## Architecture and implementation guidance

- Prefer one `fairbill` executable with composable subcommands and reusable
  libraries.
- Keep language-neutral contracts separate from ecosystem analyzers.
- Treat repository evidence, work items, estimates, diagnostics, models, and rate
  cards as explicitly versioned contracts.
- Favor compiler/parser evidence over textual guesses where practical.
- Do not make lines of code the principal effort model.
- Separate observed evidence, inferred classification, estimated work, AI/human
  adjustments, and pricing in code and serialized output.
- Give facts stable IDs and preserve calculation lineage through reports.
- Keep output deterministic for identical inputs, configuration, and model versions.
- Put structured data on stdout and diagnostics on stderr.
- Make long-running analysis cancellable and bounded in memory.
- Do not execute target code, install its dependencies, access the network, or write
  into the target repository in default static-analysis mode.
- Treat source trees as untrusted input. Avoid following links outside the selected
  scope and avoid emitting secrets or source excerpts by default.
- Do not inspect Git history. Reading ignore configuration solely for scope handling
  is acceptable.
- Preserve cross-platform behavior across Windows, Linux, and macOS.

## Open-source requirements

- Treat every committed file, comment, fixture, test name, and generated artifact as
  material that may become public.
- Never commit credentials, private client code, proprietary repository evidence,
  machine-specific personal data, or copied examples without redistribution rights.
- Record the source, version, and license of third-party dependencies, datasets,
  benchmark repositories, model files, generated templates, and substantial copied
  assets.
- Prefer dependencies and assets compatible with the project's eventual
  MIT-licensed distribution. Surface uncertain or restrictive terms before
  adoption.
- Keep private calibration data separable from the public schemas, tooling, and
  distributable model artifacts.
- Design public extension contracts and serialized schemas conservatively because
  downstream users may depend on them.
- Add and maintain contribution, security, governance, and third-party-notice files
  as the project approaches publication.
- Set package license metadata to the SPDX expression `MIT` and keep the root
  `LICENSE` file intact unless the user explicitly changes the license.

## Testing expectations

- Add tests with every behavioral change.
- Keep ordinary unit tests storage-independent. `tests/Fairbill.Tests` must use
  in-memory repository/cache abstractions rather than temporary physical files;
  reserve physical filesystem and subprocess checks for the separate end-to-end
  suite and explicitly invoked benchmarks.
- Use small synthetic fixtures for precise analyzer behavior and curated fixtures
  for realistic integration behavior.
- Validate JSON output against checked-in versioned schemas.
- Use golden files only when their diffs remain reviewable and semantically useful.
- Test that formatting-only changes, generated content, duplication, and Git history
  do not improperly increase effort.
- Test that meaningful behavior, tests, documentation, integrations, and complexity
  do affect the appropriate categories.
- Keep calibration train/test splits isolated by repository.
- Add end-to-end tests for CLI exit codes, stdout/stderr separation, deterministic
  output, and offline safety.
- Benchmark toward approximately one million source lines in a few minutes on
  documented commodity hardware.

## Documentation and decision discipline

- Update the root documents when a change alters product semantics, schemas,
  milestones, assumptions, or unresolved decisions.
- Label experimental heuristics and models as experimental.
- Record the provenance and effective date of default rate cards and model files.
- Do not describe EHE as actual labor, a timesheet, or hours historically worked.
- Prefer explicit uncertainty over unsupported precision.

## Current project stage

Milestones 1 through 6 and Milestone 7A are complete. The repository has a working common scanner,
static .NET project/Roslyn analyzer, static JavaScript/TypeScript package and source
analyzer, mixed-repository evidence pipeline, published v1 schemas, optional
external scan cache, installable global-tool package, memory-only unit fixtures,
automated process-level CLI tests, reproducible million-line benchmarks, and a
granular evidence-to-work-item estimator. The JavaScript path uses Acornima ASTs;
TypeScript is explicitly token-backed. The bundled `seed-rules/0.2.0` model uses
transparent marginal priors, exact-content normalization, two explicit profiles,
approximately four-hour work-item partitions, confidence drivers, and a separate
professionalization-gap ledger. It remains explicitly uncalibrated and must not be
described as production-ready. Milestone 6 adds schema-versioned compact
projections, capability and evidence explanation, saved-report reprojection, and
the auditable `us-senior-software-contractor/2026.1` default rate. Its Fairbill
review projection is 7.4% of compact full JSON in the recorded checkpoint.
Milestone 7A adds versioned reviewed-label and evaluation contracts, an
`ehe-work-item/1.0.0` rubric, repository-isolated partitions, deterministic offline
item/category/total/bias/interval metrics, and `calibration validate/evaluate` CLI
commands. The seed model is still uncalibrated. A diverse licensed corpus and
baseline measurements are next; local ML has not been selected or added.

The following commands have been run successfully from the repository root:

```text
dotnet restore Fairbill.slnx --configfile NuGet.Config --force-evaluate
dotnet format Fairbill.slnx --no-restore --verify-no-changes --severity info
dotnet build Fairbill.slnx --no-restore --configuration Release
dotnet test tests/Fairbill.Tests/Fairbill.Tests.csproj --no-build --no-restore --configuration Release
dotnet test tests/Fairbill.EndToEndTests/Fairbill.EndToEndTests.csproj --no-build --no-restore --configuration Release
dotnet pack src/Fairbill.Cli/Fairbill.Cli.csproj --configuration Release --no-build --no-restore --output artifacts/packages
dotnet benchmarks/Fairbill.ScannerBenchmarks/bin/Release/net10.0/Fairbill.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --warm-cache
dotnet benchmarks/Fairbill.ScannerBenchmarks/bin/Release/net10.0/Fairbill.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --dotnet
dotnet benchmarks/Fairbill.ScannerBenchmarks/bin/Release/net10.0/Fairbill.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --javascript
```

The primary distribution is the `Fairbill.Tool` .NET global-tool package with the
command name `fairbill`. Self-contained executables may be added later if they
materially improve distribution.
