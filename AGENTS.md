# AGENTS.md

## Scope

These instructions apply to the entire EffortHours repository.

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
7. `MILESTONE_8.md` when changing host-review packets, queries, adjustment
   validation, source disclosure, provider boundaries, or review budgets
8. `MODEL_REVIEWS.md` when changing priors, calibration labels, or review policy
9. `CHANGE_ESTIMATION.md` when changing diff, PR, commit, range, or contribution
   semantics
10. `MILESTONE_CHANGE_1.md` when changing the implemented Change EHE boundary
11. `MILESTONE_CHANGE_2.md`, `MILESTONE_CHANGE_3.md`, and
    `CHANGE_MODEL_ADMISSION.md` when changing Change
    calibration identity, labels, metrics, review maturity, or admission policy
12. `RELEASING.md` when changing package metadata, public-release automation,
    repository visibility procedure, or NuGet publication
13. `MILESTONE_8_MEASUREMENT.md` when changing host-review telemetry, comparison
    metrics, benchmark privacy, or model-budget admission

If an implementation request conflicts with those documents, surface the conflict
and update the relevant decision explicitly. Do not silently change estimation
semantics.

## Settled product constraints

- Target any common software repository eventually.
- Implement .NET and JavaScript/TypeScript analysis first.
- Implement the tool and reusable libraries on .NET 10.
- Ignore Git history, churn, contributors, and timestamps as effort signals.
- The explicit change-estimation command may use revisions, and future portfolio
  selectors may use author identity and time, only to select final changes.
  Ordinary repository estimates remain history-free, and selection metadata must
  never become an effort multiplier.
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

- Prefer one `eh` executable with composable subcommands and reusable
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
- Follow the enforced C# file budgets in `eng/file-budgets.json`. Start splitting
  responsibilities near 80% of a hard ceiling. Do not add or increase a ratchet
  override without recording an explicit architectural rationale.

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
- Keep ordinary unit tests storage-independent. `tests/EffortHours.Tests` must use
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
- Keep `CODE_BUDGETS.md` and the enforced manifest aligned when responsibilities
  move between files.

## Current project stage

Milestones 1 through 6, Milestone 7A, the Milestone 7B1 through 7B5 checkpoints,
the post-7B5 analyzer-precision checkpoint, and the scanner performance-and-safety
checkpoint are complete. The repository has a
working common scanner, static .NET
project/Roslyn analyzer, static JavaScript/TypeScript package and source analyzer,
mixed-repository evidence pipeline, published v1 schemas, optional external scan
cache, installable global-tool package, memory-only unit fixtures, automated
process-level CLI tests, reproducible million-line benchmarks, and a granular
evidence-to-work-item estimator. The JavaScript path uses Acornima ASTs; TypeScript
is explicitly token-backed. The bundled `seed-rules/0.2.1` model uses transparent
marginal priors, exact-content normalization, two explicit profiles, approximately
four-hour work-item partitions, confidence drivers, and a separate
professionalization-gap ledger. Version 0.2.1 keeps the 0.2.0 priors unchanged and
fixes TypeScript file ownership for duplicate and test normalization. It remains
explicitly uncalibrated and must not be described as production-ready. Milestone 6
adds schema-versioned compact projections, capability and evidence explanation,
saved-report reprojection, and the auditable
`us-senior-software-contractor/2026.1` default rate. Its EffortHours review projection
is 7.4% of compact full JSON in the recorded checkpoint.
Milestone 7A adds reviewed-label and evaluation contracts, the
`ehe-work-item/1.0.0` rubric, repository-isolated partitions, deterministic offline
metrics, and `calibration validate/evaluate`. Milestone 7B1 adds explicitly
unreviewed authoring packets, blind review, completed review-plan compilation,
explicit output paths, and the provenance-checked `efforthours-public-pilot/0.1.0`
corpus with frozen seed reports. The pilot has three MIT-licensed repositories, 99
teacher targets, one host-AI teacher, and no independent correction. Milestone 7B2
adds exact-digest subsequent-review packets/compilation, explicit accept/replace
decisions, reviewer-identity independence checks, versioned relational mutation
contracts, and an 8-case/14-assertion synthetic .NET guardrail baseline. A compact
blind handoff exists, but no independent reviewer has completed it. Milestone 7B3
expands the public synthetic baseline to 30 cases and 84 passing assertions across
.NET, parser-backed JavaScript, token-backed TypeScript, and mixed repositories,
including all three range points, generated customization, and category isolation.
Milestone 7B4 expands that unchanged seed baseline to 48 cases and 156 passing
assertions with bounded renamed near-copies, compiler-disabled C# syntax, data,
migrations, security, declared coverage levels, workspace reuse, CI, and container
delivery. Milestone 7B5 adds `efforthours-public-expansion/0.1.0`: three immutable
MIT-licensed releases, 133 lineage-complete teacher targets, frozen
`seed-rules/0.2.1` baselines, the `ehe-work-item/1.1.0` explicit-exclusion policy,
review compilers `0.2.0`, and a combined blind independent-review handoff. Across
both public corpora there are six repository families and 232 blind targets, still
with one host-AI teacher and no independent correction. `.NET` analyzer `0.3.2`
now qualifies ambiguous execute/query calls with persistence context; JavaScript
analyzer `0.4.1` requires UI-framework context for state/effect/form-only UI
evidence and excludes development benchmark hashbangs from product entry points.
The unchanged 48-case seed mutation baseline retains identical numeric estimates
and 156 passing relations. Frozen-corpus reevaluations disclose mapping changes and
are contamination diagnostics, not held-out accuracy evidence. General
semantic-clone and reachability analysis, multiple observations per
ecosystem/partition cell, and actual independent review are next; local ML has not
been selected or added.

The scanner benchmark now measures fresh-process .NET, JavaScript/TypeScript, and
mixed million-line shapes, samples peak resident memory, labels explicit external
cache passes separately, and fingerprints caller-supplied target trees before and
after analysis. The recorded mixed full scan completes in 10.876 seconds with a
234.20 MiB sampled peak on the documented workstation; three exact MIT release
trees and the EffortHours tree also retain unchanged target metadata. The benchmark
is explicitly disk-backed, while ordinary unit tests remain memory-only. No
cross-platform regression threshold has been frozen from the single-machine data.

The first Change Estimation MVP adds provider-neutral immutable snapshot analysis,
local Git base/head, commit, and range selectors, one optional identity-only `gh`
PR selector, v1 change schemas, `change-seed/0.1.0` work items, final-delta
normalization, component reconciliation, saved-report explanation, and
process-level Git tests. Its completed safeguard matrix covers normalization,
meaningful code/tests/docs, migrations, integrations, CI, container delivery,
simplification, additivity, overlap, reverts, category isolation, all three range
points, and cooperative cancellation. The first Ctrl+C returns 130 after a
stderr-only diagnostic; a second retains immediate termination. It remains
experimental and uncalibrated. Multiple PRs, directory/evidence selectors, and
author-period portfolios remain deferred. The former large CLI application class
is split into focused partial modules, and `eng/file-budgets.json` enforces early
refactoring through the end-to-end suite.

The first Change calibration checkpoint adds `change-ehe-work-item/1.0.0`,
content-derived final-delta identity, backward-compatible Change provenance in the
existing corpus/review contracts, Change scaffold/compile/evaluate CLI paths, and
a 24-case eight-family matrix frozen before labels. Its in-memory fixture generator
reproduces 24 source reports and blind authoring packets, including exact-zero
formatting, move, generation, and revert cases. Change review plans may explicitly
reject duplicate or false-positive capabilities with lineage-preserving `0/0/0`
targets. The preliminary public synthetic corpus has
24 records, 121 teacher targets, 22 exact-zero exclusions, and one disclosed
host-AI teacher. Development and validation diagnostics are recorded; test
comparison remains withheld. No independent Change review is complete. The corpus
changes no estimator prior and adds no ML dependency;
`change-seed/0.1.0` remains experimental and uncalibrated.

The first Milestone 8 host-review checkpoint adds the provider-neutral
`host-review/1.0.0` protocol: rate-free compact packets, canonical estimate-plus-
evidence input digests, bounded capability/evidence/scope/selected-source queries,
explicit model-identity availability, evidence-backed affirm/replace ledgers, and
non-applying validation. Selected source is opt-in, restricted to scanner-admitted
files, link-safe, size/encoding bounded, and digest checked. The local baseline
remains complete without AI; EffortHours calls no provider and chooses no model.
The measurement checkpoint adds `host-review-measurement/1.0.0`,
`host-review-comparison-metrics/1.0.0`, sanitized `review measure`, paired
`review benchmark`, explicit incomplete-context accounting, and a reproducible
three-repository/six-session public diagnostic. Compact review improves the frozen
reference agreement at item/category levels but worsens repository-total
agreement. Exact provider token/time/cost and complete surrounding-context
telemetry for both paired sessions were unavailable, so no savings claim,
host-review budget, or automatic default is selected. Blind multi-model repetition
remains next.

The public-alpha candidate includes project-authored governance and conduct
policies, issue and pull-request templates, full-SHA-pinned Windows/Linux/macOS CI,
weekly dependency update configuration, a dedicated NuGet README, and a manually
dispatched `EffortHours.Tool` preview workflow. Package publication uses a protected
`nuget.org` GitHub environment and short-lived NuGet trusted-publishing/OIDC
credentials; long-lived publishing keys must not be committed or stored. The
repository remains private and no package is public until the user separately
authorizes those external actions. Follow `RELEASING.md` for the exact boundary.

The following commands have been run successfully from the repository root:

```text
dotnet restore EffortHours.slnx --configfile NuGet.Config --force-evaluate
dotnet format EffortHours.slnx --no-restore --verify-no-changes --severity info
dotnet build EffortHours.slnx --no-restore --configuration Release
dotnet test tests/EffortHours.Tests/EffortHours.Tests.csproj --no-build --no-restore --configuration Release
dotnet test tests/EffortHours.EndToEndTests/EffortHours.EndToEndTests.csproj --no-build --no-restore --configuration Release
dotnet pack src/EffortHours.Cli/EffortHours.Cli.csproj --configuration Release --no-build --no-restore --output artifacts/packages
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --warm-cache
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --dotnet
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --javascript
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --mixed --warm-cache
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --repository . --warm-cache
```

The primary distribution is the `EffortHours.Tool` .NET global-tool package with the
command name `eh`. Self-contained executables may be added later if they
materially improve distribution.
