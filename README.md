# Fairbill

Fairbill is an experimental .NET 10 command-line tool and set of reusable libraries
for estimating the human effort represented by a software repository.

Its primary metric is **Equivalent Human Effort (EHE)**: the counterfactual time a
competent senior contractor, unfamiliar with the business domain, would need to
recreate the repository's current functional and quality state from a clear
specification without using AI.

Fairbill is intended to make a defensible repository estimate inexpensive. It is
expected to run inside an AI-enabled development session, but static analysis and
local models should compress and handle most of the repository. The host AI should
need to reason about only compact evidence and unresolved semantic questions rather
than reading an entire large repository.

## Status

Milestones 1 through 6 are complete. The repository now contains:

- versioned JSON contracts and published schemas for evidence, work items,
  estimates, diagnostics, and rate cards;
- deterministic serialization and semantic/schema validation;
- a deterministic, read-only common scanner with nested `.gitignore` and
  `.fairbillignore` handling;
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
- an installable .NET global tool named `fairbill`;
- JSON and Markdown reports with evidence lineage, ranges, and optional pricing;
- synthetic fixtures, contract tests, and process-level CLI tests;
- evidence normalization that separates production/test structure, gives fine
  semantic facts precedence over broad inventory, and collapses exact duplicate
  maintained bodies;
- category-specific capability builders for implementation, UI, data, integrations,
  security, tests, documentation, delivery, validation, and review;
- explicit `implementation` and `recreation` profile work, deterministic confidence
  drivers, and a professionalization-gap ledger excluded from represented EHE; and
- a schema-validated, checked-in, embedded `seed-rules/0.2.0` model that partitions
  large capabilities around a four-hour target;
- repository, category, scope, capability, and bounded review projections with
  compact JSON and readable Markdown;
- `explain` drill-down from stable work-item or capability IDs to evidence and
  calculation lineage; and
- a schema-validated, checked-in 2026 US senior-contractor rate model with a
  $160/hour default, $125-$200 market reference, source provenance, formula, and
  caller override or opt-out.

`fairbill scan <folder>` now produces common, static .NET, and static
JavaScript/TypeScript evidence, including mixed-repository output.
`fairbill estimate <folder>` connects that evidence directly to the granular seed
pipeline. Every represented hour belongs to an evidence-backed work item, but the
priors remain experimental and uncalibrated. Seed-rule output must not be presented
as a production-ready or empirically validated estimate.

`fairbill estimate` now applies the bundled 2026 USD rate by default. Use
`--no-rate` for effort-only output or `--hourly-rate` for an explicit replacement.
Pricing never changes EHE. Use `--view review` for a bounded AI-review packet and
pass any reported capability ID to `fairbill explain` for its evidence lineage.

Fairbill is intended to be released as open-source software. Development should be
public-repository-ready from the beginning, even before the repository is published.
It is licensed under the [MIT License](LICENSE).

Current language-specific analysis covers:

- .NET and C# repositories
- JavaScript and TypeScript repositories
- Mixed repositories containing both ecosystems

The architecture should allow additional language analyzers later.

JavaScript and JSX structure counts are parser-backed. TypeScript and TSX use a
purpose-built, non-executing token analyzer in this release; evidence tags disclose
which path was used. Fairbill never imports JavaScript modules or runs package
scripts, package managers, transpilers, or executable configuration during the
default scan.

## Build and try the CLI

The .NET 10 SDK selected by `global.json` is required.

```text
dotnet restore Fairbill.slnx --configfile NuGet.Config --force-evaluate
dotnet build Fairbill.slnx --no-restore --configuration Release
dotnet test tests/Fairbill.Tests/Fairbill.Tests.csproj --no-build --no-restore --configuration Release
```

The normal unit suite uses only in-memory repository and cache fixtures. Run the
separate, disk-backed process tests when validating the CLI boundary or a release:

```text
dotnet test tests/Fairbill.EndToEndTests/Fairbill.EndToEndTests.csproj --no-build --no-restore --configuration Release
```

Scan a repository without executing it or reading its Git history:

```text
dotnet src/Fairbill.Cli/bin/Release/net10.0/fairbill.dll scan . --output ../fairbill.repository-evidence.json
```

Run the current evidence-to-estimate pipeline directly on a folder:

```text
dotnet src/Fairbill.Cli/bin/Release/net10.0/fairbill.dll estimate . --profile implementation --format markdown
```

The available CLI surface is:

```text
fairbill --help
fairbill version
fairbill scan <repository> [--output <path>] [--cache <external-path>] [--no-gitignore] [--no-fairbillignore]
fairbill schema list
fairbill schema show <name>
fairbill model info
fairbill model show
fairbill rate info
fairbill rate show
fairbill estimate <repository-or-evidence.json> [--profile <implementation|recreation>] [--view <full|repository|category|scope|work-item|review>] [--format <json|markdown>] [--compact] [--no-rate | --hourly-rate <amount> [--currency <code>]]
fairbill report <estimate.json> [--view <full|repository|category|scope|work-item|review>] [--format <json|markdown>] [--compact]
fairbill explain <repository-or-evidence.json> --item <work-item-or-capability-id> [--profile <implementation|recreation>] [--format <json|markdown>] [--compact]
```

The optional scan cache trusts file path, size, and last-write metadata for
invalidation. It is a performance optimization, not an effort signal or forensic
integrity mechanism. Omit it for a full content re-read.

## Performance checkpoint

The v0.2 scanner processed a synthetic one-million-line repository containing
10,000 C# files in 4.275 seconds, plus 0.116 seconds for JSON serialization, on the
documented development machine. An unchanged warm-cache scan took 1.646 seconds and
produced the same digest. This is a repeatable engineering checkpoint, not a claim
about every repository shape. The v0.3 static .NET path processed the same fixture,
including Roslyn syntax analysis, in 6.608 seconds plus 0.107 seconds for
serialization. See [BENCHMARKS.md](BENCHMARKS.md) for the methods, environment, and
limitations. The v0.4 static JavaScript/TypeScript path processed a mixed 10,000-file,
one-million-line fixture in 13.303 seconds plus 0.118 seconds for serialization.

The Milestone 6 Fairbill snapshot produced 240,464 bytes of compact canonical
estimate JSON. The bounded review projection used 17,694 bytes (7.4%), and review
Markdown used 8,763 bytes (3.6%). See
[REPORT_BENCHMARKS.md](REPORT_BENCHMARKS.md) for all views, small .NET,
JavaScript/TypeScript, and mixed fixtures, the measurement method, and the initial
usefulness review.

## Important distinction

EHE is not a claim about how many hours were actually worked. It is an estimate of
the conventional, non-AI replacement effort embodied in the current artifact.
Cost output is therefore an **Equivalent Replacement Cost**, not a timesheet.

This distinction allows Fairbill to support value and compensation discussions
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
- [REPORT_BENCHMARKS.md](REPORT_BENCHMARKS.md) records reporting size and usefulness
  measurements.
- [MODEL_REVIEWS.md](MODEL_REVIEWS.md) records provisional realism checks with
  source and model provenance; they are not calibration claims.
- [CHANGE_ESTIMATION.md](CHANGE_ESTIMATION.md) records the deferred PR, commit,
  range, and contribution-portfolio semantics and safeguards.
- [AGENTS.md](AGENTS.md) contains repository-wide instructions for coding agents.
- [CONTRIBUTING.md](CONTRIBUTING.md) contains the verified development workflow.
- [SECURITY.md](SECURITY.md) explains private vulnerability reporting expectations.
- [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) records dependency provenance.
- [BENCHMARKS.md](BENCHMARKS.md) records reproducible performance checkpoints.
- [`schemas/v1`](schemas/v1) contains the published v1 JSON schemas, including the
  seed-rule model schema.
- [`models/seed-rules`](models/seed-rules) contains the transparent bundled seed
  priors.
- [`rates/us-senior-contractor`](rates/us-senior-contractor) contains the auditable
  bundled contractor-rate derivation.
