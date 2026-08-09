# EffortHours

EffortHours is an experimental .NET 10 command-line tool and set of reusable libraries
for estimating the human effort represented by a software repository.

Its primary metric is **Equivalent Human Effort (EHE)**: the counterfactual time a
competent senior contractor, unfamiliar with the business domain, would need to
recreate the repository's current functional and quality state from a clear
specification without using AI.

EffortHours is intended to make a defensible repository estimate inexpensive. It is
expected to run inside an AI-enabled development session, but static analysis and
local models should compress and handle most of the repository. The host AI should
need to reason about only compact evidence and unresolved semantic questions rather
than reading an entire large repository.

> **Experimental public-alpha candidate:** EffortHours is a working reference
> implementation, but its bundled seed estimators have not completed independent
> calibration. Outputs are counterfactual replacement-effort estimates, not actual
> labor records, invoices, or production-validated billing determinations.

## Status

EffortHours is being prepared for a quiet public source and NuGet preview. Milestones
1 through 6 and the Milestone 7A calibration foundation are complete.
Milestone 7B1 through 7B5 public-corpus, review, and mutation checkpoints are
implemented. A post-7B5 precision checkpoint corrects reviewed .NET persistence,
framework-neutral JavaScript UI, and benchmark-entry-point false positives without
changing seed priors. Actual independent correction and broader multi-observation
corpus coverage remain. The first experimental Change Estimation MVP is also
implemented for immutable base/head revisions, one commit, one range, and one
GitHub pull request. Its behavioral safeguard checkpoint adds cooperative
cancellation and category-isolated migration, integration, CI, container-delivery,
and simplification mutations. Its first calibration checkpoint adds final-delta
provenance, review and evaluation commands, a frozen rubric, and a 24-case matrix
with reproducible source reports, 121 preliminary teacher targets, and a blind
independent-review packet. No independent Change correction or accuracy claim is
complete.
The repository now contains:

- versioned JSON contracts and published schemas for evidence, work items,
  estimates, diagnostics, and rate cards;
- deterministic serialization and semantic/schema validation;
- a deterministic, read-only common scanner with nested `.gitignore` and
  `.efforthoursignore` handling;
- streamed SHA-256, size, and physical-line measurement without source excerpts;
- file, language, ecosystem, project/package, test, documentation, build, CI,
  container, infrastructure, coverage, and exclusion evidence;
- generated, vendored, minified, binary, build-output, link, and Git-metadata
  classification;
- an optional versioned incremental cache that must live outside the target tree;
- static `.sln`, `.slnx`, SDK-project, central-package, package-reference, and
  project-graph analysis without evaluating MSBuild;
- Roslyn syntax evidence for .NET entry points, APIs, data access, migrations,
  background work, integrations, security, validation, UI/Razor surfaces, source
  structure, and unit/component/integration/end-to-end tests;
- static npm-compatible package, npm/pnpm/Yarn/Bun convention, workspace,
  dependency graph, script-name, framework, and TypeScript JSONC configuration
  discovery;
- Acornima AST evidence for JavaScript/JSX plus deterministic bounded token-stream
  evidence for TypeScript/TSX and Vue/Svelte script blocks;
- JavaScript/TypeScript evidence for server routes, UI pages/components/state,
  maintained web assets, data access, integrations, security, validation,
  background work, and unit/component/integration/end-to-end tests;
- an injectable repository-storage boundary with memory-only unit fixtures;
- an installable `EffortHours.Tool` .NET global tool with the command `eh`;
- schema-versioned Change EHE evidence, reports, explanations, immutable Git-tree
  analysis, additive range reconciliation, and an optional identity-only `gh` PR
  adapter;
- Change calibration authoring, compilation, independent-review handoff, and
  evaluation paths that preserve immutable final-delta lineage and repository-held-out
  partitions without changing `change-seed/0.1.0`, plus a deterministic in-memory
  generator for 24 public synthetic source cases;
- JSON and Markdown reports with evidence lineage, ranges, and optional pricing;
- synthetic fixtures, contract tests, and process-level CLI tests;
- evidence normalization that separates production/test structure, gives fine
  semantic facts precedence over broad inventory, and collapses exact duplicate
  maintained bodies;
- category-specific capability builders for implementation, UI, data, integrations,
  security, tests, documentation, delivery, validation, and review;
- explicit `implementation` and `recreation` profile work, deterministic confidence
  drivers, and a professionalization-gap ledger excluded from represented EHE;
- a schema-validated, checked-in, embedded `seed-rules/0.2.1` model that partitions
  large capabilities around a four-hour target, retains the 0.2.0 priors, and
  normalizes TypeScript duplicates and test structure through the shared
  JavaScript/TypeScript estimation scope;
- repository, category, scope, capability, and bounded review projections with
  compact JSON and readable Markdown;
- `explain` drill-down from stable work-item or capability IDs to evidence and
  calculation lineage;
- a schema-validated, checked-in 2026 US senior-contractor rate model with a
  $160/hour default, $125-$200 market reference, source provenance, formula, and
  caller override or opt-out;
- a versioned reviewed-label corpus, work-item rubric, repository-isolated
  development/validation/test partitions, and deterministic offline evaluator for
  item, category, total, bias, interval, and mapping-coverage metrics;
- schema-versioned unreviewed authoring packets, optional blind review, and a
  deterministic review-plan compiler that requires complete capability coverage;
- two MIT-source-provenanced three-repository public corpora with frozen
  partitions and checked-in `seed-rules/0.2.0` or `seed-rules/0.2.1` baseline
  reports. Their six repository families have one host-AI teacher and no
  independent correction, so they remain preliminary weak supervision;
- exact-digest subsequent-review packets and compilation with explicit
  accept/replace decisions, preserved lineage, maturity progression, and distinct
  reviewer identities;
- audited exact-zero reviewed exclusions, permitted only as `0/0/0` with rationale
  and a size exception under `ehe-work-item/1.1.0`;
- versioned relational mutation guardrails plus a 48-case, 156-assertion synthetic
  baseline spanning .NET, parser-backed JavaScript, token-backed TypeScript, and
  mixed repositories. It covers formatting, exact duplication, generated bodies,
  maintained generated customization, bounded renamed near-copies,
  compiler-disabled C# syntax, API/UI/data/security behavior, tests, declared
  coverage levels, documentation, integrations, workspace boundaries, CI,
  containers, all three range points, and category isolation.
- enforced source-file budgets with a 500-line default, a 400-line CLI ceiling,
  explicit legacy ratchets, and a thin command dispatcher.

`eh scan <folder>` now produces common, static .NET, and static
JavaScript/TypeScript evidence, including mixed-repository output.
`eh estimate <folder>` connects that evidence directly to the granular seed
pipeline. Every represented hour belongs to an evidence-backed work item, but the
priors remain experimental and uncalibrated. Seed-rule output must not be presented
as a production-ready or empirically validated estimate.

`eh estimate` now applies the bundled 2026 USD rate by default. Use
`--no-rate` for effort-only output or `--hourly-rate` for an explicit replacement.
Pricing never changes EHE. Use `--view review` for a bounded AI-review packet and
pass any reported capability ID to `eh explain` for its evidence lineage.

`eh change` estimates the final functional and quality delta for explicit
base/head revisions, a commit, a range, or one PR. It reads immutable local Git
objects without checking out, fetching, executing, or modifying the target. Range
reports reconcile isolated commits with the authoritative normalized final delta;
commit count and intermediate churn never multiply effort. PR mode uses optional
`gh` only to resolve immutable identities and requires those objects locally. The
`change-seed/0.1.0` model is uncalibrated and experimental.

The first Ctrl+C requests cooperative cancellation and returns exit code 130 after
writing a concise diagnostic to stderr; pressing Ctrl+C again retains immediate
termination. Partial structured output is not presented as a successful report.

`eh calibration scaffold` creates an explicitly unreviewed packet from a
saved canonical estimate; `--blind` hides numeric seed guidance.
`eh calibration compile` turns completed capability decisions into a corpus only after
verifying exact estimate digests and full represented-capability coverage.
`eh calibration validate` checks a reviewed corpus and its provenance.
`eh calibration evaluate` compares canonical estimates with one explicit
repository-held-out partition. It evaluates effort only, never calls an AI or the
network, and does not make the still-uncalibrated seed priors production-ready.

`eh calibration review-scaffold` prepares a reference or blind second-pass
packet from an existing corpus. `review-compile` advances maturity only after a
distinct reviewer decides every target and the exact source-corpus digest matches.
Both checked-in public packets are still unreviewed; the combined handoff is in
[`calibration/INDEPENDENT_REVIEW.md`](calibration/INDEPENDENT_REVIEW.md).
`eh calibration mutations`
evaluates deterministic relational guardrails; failures emit a report and return
exit code 5. Mutation relations are not effort labels. The latest near-copy bounds
do not imply semantic clone detection, and the dead-code invariant is limited to
C# syntax excluded by the compiler preprocessor rather than general reachability.

Change reports use parallel `calibration change-scaffold`, `change-compile`, and
`change-evaluate` commands. They reuse ordinary corpus validation and blind
second-review commands while adding immutable base/head and final-delta provenance.
See [`calibration/changes`](calibration/changes); its source reports and blind
packets are reproducible, and its teacher labels are preliminary until a genuinely
distinct reviewer completes the frozen handoff.

EffortHours is licensed under the [MIT License](LICENSE). The public alpha is intended
to make the estimation model, evidence contracts, limitations, and working
reference implementation available for critique, reuse, forks, and independent
alternatives. Open-source availability does not turn preliminary teacher labels
into validated ground truth.

Current language-specific analysis covers:

- .NET and C# repositories
- JavaScript and TypeScript repositories
- Mixed repositories containing both ecosystems

The architecture should allow additional language analyzers later.

JavaScript and JSX structure counts are parser-backed. TypeScript and TSX use a
purpose-built, non-executing token analyzer in this release; evidence tags disclose
which path was used. EffortHours never imports JavaScript modules or runs package
scripts, package managers, transpilers, or executable configuration during the
default scan.

## Install the preview

The NuGet package identity is `EffortHours.Tool`, and the installed command is
`eh`. Once the public preview is listed on NuGet.org, install the pinned
prerelease with:

```text
dotnet tool install --global EffortHours.Tool --version 0.9.0-alpha.1
eh version
eh --help
```

Preview versions are intentionally opt-in. To replace an older preview, use
`dotnet tool update --global EffortHours.Tool --version <version>`. See
[RELEASING.md](RELEASING.md) for package verification, trusted-publishing, and
release procedures.

## Build from source

The .NET 10 SDK selected by `global.json` is required. Change commands also require
Git; `--pr` additionally requires an installed and authenticated `gh` CLI.

```text
dotnet restore EffortHours.slnx --configfile NuGet.Config --force-evaluate
dotnet build EffortHours.slnx --no-restore --configuration Release
dotnet test tests/EffortHours.Tests/EffortHours.Tests.csproj --no-build --no-restore --configuration Release
```

The normal unit suite uses only in-memory repository and cache fixtures. Run the
separate, disk-backed process tests when validating the CLI boundary or a release:

```text
dotnet test tests/EffortHours.EndToEndTests/EffortHours.EndToEndTests.csproj --no-build --no-restore --configuration Release
```

Scan a repository without executing it or reading its Git history:

```text
dotnet src/EffortHours.Cli/bin/Release/net10.0/efforthours.dll scan . --output ../efforthours.repository-evidence.json
```

Run the current evidence-to-estimate pipeline directly on a folder:

```text
dotnet src/EffortHours.Cli/bin/Release/net10.0/efforthours.dll estimate . --profile implementation --format markdown
```

The available CLI surface is:

```text
eh --help
eh version
eh scan <repository> [--output <path>] [--cache <external-path>] [--no-gitignore] [--no-efforthoursignore]
eh schema list
eh schema show <name>
eh model info
eh model show
eh rate info
eh rate show
eh estimate <repository-or-evidence.json> [--profile <implementation|recreation>] [--view <full|repository|category|scope|work-item|review>] [--format <json|markdown>] [--compact] [--output <path>] [--no-rate | --hourly-rate <amount> [--currency <code>]]
eh report <estimate.json> [--view <full|repository|category|scope|work-item|review>] [--format <json|markdown>] [--compact]
eh explain <repository-or-evidence.json> --item <work-item-or-capability-id> [--profile <implementation|recreation>] [--format <json|markdown>] [--compact]
eh change <repository> --base <revision> --head <revision> [--profile <implementation|recreation>] [--format <json|markdown>] [--compact] [--output <path>] [--no-rate | --hourly-rate <amount> [--currency <code>]]
eh change <repository> --commit <revision> [--parent <revision>] [options]
eh change <repository> --range <base>..<head> [options]
eh change <repository> --pr <number-or-url> [--repo <owner/name>] [options]
eh change explain <change-estimate.json> --item <work-item-id> [--format <json|markdown>] [--compact]
eh calibration scaffold <estimate.json> [--blind] [--compact] [--output <path>]
eh calibration compile <review-plan.json> <estimate.json>... [--compact] [--output <path>]
eh calibration review-scaffold <corpus.json> [--blind] [--compact] [--output <path>]
eh calibration review-compile <plan.json> <corpus.json> [--compact] [--output <path>]
eh calibration mutations <suite.json> <estimate.json>... [--compact] [--output <path>]
eh calibration validate <corpus.json> [--compact] [--output <path>]
eh calibration evaluate <corpus.json> <estimate.json>... --partition <development|validation|test> [--compact] [--output <path>]
eh calibration change-scaffold <change-estimate.json> --repository-family <id> --case <id> --tag <tag>... [--blind] [--compact] [--output <path>]
eh calibration change-compile <review-plan.json> <change-estimate.json>... [--compact] [--output <path>]
eh calibration change-evaluate <corpus.json> <change-estimate.json>... --partition <development|validation|test> [--compact] [--output <path>]
```

The optional scan cache trusts file path, size, and last-write metadata for
invalidation. It is a performance optimization, not an effort signal or forensic
integrity mechanism. Omit it for a full content re-read.

## Performance checkpoint

The current benchmark harness records cumulative managed allocation, sampled peak
resident memory, fresh-process-versus-explicit-cache conditions, and a before/after
target-tree metadata digest. On the documented development machine, fresh-process
one-million-line scans took 7.083 seconds for static .NET, 12.088 seconds for static
JavaScript/TypeScript, and 10.876 seconds for a mixed C#/JavaScript/TypeScript tree;
sampled scan peaks were 272.52, 185.69, and 234.20 MiB respectively. The mixed
warm-cache pass took 4.581 seconds. A combined set of three exact, verified MIT
releases and the EffortHours development tree were also measured read-only. These
are reproducible engineering checkpoints, not universal performance guarantees or
release thresholds. See [BENCHMARKS.md](BENCHMARKS.md) for commands, provenance,
hardware, cache semantics, scaling samples, and limitations.

The Milestone 6 EffortHours snapshot produced 240,464 bytes of compact canonical
estimate JSON. The bounded review projection used 17,694 bytes (7.4%), and review
Markdown used 8,763 bytes (3.6%). See
[REPORT_BENCHMARKS.md](REPORT_BENCHMARKS.md) for all views, small .NET,
JavaScript/TypeScript, and mixed fixtures, the measurement method, and the initial
usefulness review.

## Important distinction

EHE is not a claim about how many hours were actually worked. It is an estimate of
the conventional, non-AI replacement effort embodied in the current artifact.
Cost output is therefore an **Equivalent Replacement Cost**, not a timesheet.

This distinction allows EffortHours to support value and compensation discussions
without misrepresenting counterfactual hours as historical labor.

## Planned workflow

1. Inspect a repository without consulting its development history.
2. Extract objective, traceable evidence about its current state.
3. Decompose the work into small repository-level work items.
4. Estimate each item with transparent rules and local ML where useful.
5. Present ambiguous, low-confidence items to the host AI when useful.
6. Aggregate effort by category and apply a dated, configurable market rate.
7. Produce machine-readable evidence and a human-readable report.

## Project documents

- [PRODUCT.md](PRODUCT.md) defines the product, metric, scope, and principles.
- [ESTIMATION_MODEL.md](ESTIMATION_MODEL.md) specifies how evidence becomes effort
  and cost.
- [PLAN.md](PLAN.md) describes the proposed architecture and delivery roadmap.
- [MILESTONE_5.md](MILESTONE_5.md) records the granular seed-estimator design and
  its current limitations.
- [MILESTONE_6.md](MILESTONE_6.md) records reporting, explanation, and default-rate
  decisions.
- [MILESTONE_7.md](MILESTONE_7.md) defines reviewed labels, repository-held-out
  evaluation, metrics, and the admission gates for later local models.
- [REPORT_BENCHMARKS.md](REPORT_BENCHMARKS.md) records reporting size and usefulness
  measurements.
- [MODEL_REVIEWS.md](MODEL_REVIEWS.md) records provisional realism checks with
  source and model provenance; they are not calibration claims.
- [CHANGE_ESTIMATION.md](CHANGE_ESTIMATION.md) records implemented PR, commit, and
  range semantics plus deferred contribution-portfolio safeguards.
- [MILESTONE_CHANGE_1.md](MILESTONE_CHANGE_1.md) records the first Change EHE
  implementation, verification, and limitations.
- [MILESTONE_CHANGE_2.md](MILESTONE_CHANGE_2.md) records Change calibration
  identity, review, evaluation, and the remaining independent-label boundary.
- [MILESTONE_CHANGE_3.md](MILESTONE_CHANGE_3.md) records the reproducible 24-case
  source suite, first-pass teacher corpus, diagnostics, and withheld test boundary.
- [CHANGE_MODEL_ADMISSION.md](CHANGE_MODEL_ADMISSION.md) freezes Change candidate
  metric identities and decision order before any local-ML fitting.
- [CODE_BUDGETS.md](CODE_BUDGETS.md) defines enforced early-refactoring thresholds
  and legacy ratchets.
- [AGENTS.md](AGENTS.md) contains repository-wide instructions for coding agents.
- [CONTRIBUTING.md](CONTRIBUTING.md) contains the verified development workflow.
- [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) defines participation and enforcement
  expectations.
- [GOVERNANCE.md](GOVERNANCE.md) records decision authority, contribution policy,
  calibration independence, and succession.
- [RELEASING.md](RELEASING.md) defines the public-source and NuGet preview checklist.
- [CHANGELOG.md](CHANGELOG.md) records user-visible release changes and known
  limitations.
- [SECURITY.md](SECURITY.md) explains private vulnerability reporting expectations.
- [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) records dependency provenance.
- [BENCHMARKS.md](BENCHMARKS.md) records reproducible performance checkpoints.
- [`schemas/v1`](schemas/v1) contains the published v1 JSON schemas, including the
  seed-rule model schema.
- [`models/seed-rules`](models/seed-rules) contains the transparent bundled seed
  priors.
- [`rates/us-senior-contractor`](rates/us-senior-contractor) contains the auditable
  bundled contractor-rate derivation.
- [`calibration`](calibration) contains the public review rubrics, two repository
  corpora, independent-review handoff, mutation fixtures, frozen seed baselines,
  the Change case matrix, reproducible source packets, and preliminary Change
  teacher corpus, and publication guidance; no private calibration data belongs
  there.
