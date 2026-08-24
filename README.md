# EffortHours

[![NuGet prerelease](https://img.shields.io/nuget/vpre/EffortHours.Tool?label=NuGet)](https://www.nuget.org/packages/EffortHours.Tool/)
[![CI](https://github.com/VictorZakharov/efforthours/actions/workflows/ci.yml/badge.svg)](https://github.com/VictorZakharov/efforthours/actions/workflows/ci.yml)
[![MIT License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**Evidence-first, offline effort estimates for software repositories and completed
code changes.**

EffortHours is a .NET 10 command-line tool that turns source, tests,
documentation, configuration, and delivery artifacts into traceable
**Equivalent Human Effort (EHE)**.

> How long would one competent senior contractor, unfamiliar with the business
> domain and not using AI, need to recreate this functional and quality state from
> a clear specification?

| EffortHours is | EffortHours is not |
| --- | --- |
| A replacement-effort estimate of the software that exists now | A reconstruction of hours historically worked |
| A low/expected/high range built from evidence-backed work items, with optional normalized X against reference capacity | A timesheet, actual-hours record, or proof of sole authorship |
| An offline static analysis with transparent rules | A build, runtime, security, accessibility, or quality audit |
| An effort model with optional pricing applied afterward | An invoice or compensation recommendation |

> [!WARNING]
> **Experimental public alpha.** The CLI and reporting pipeline work, but the
> repository estimator remains uncalibrated and Change EHE has only limited
> validation for smaller changes in established language analyzers. Neither is
> empirically production-validated. Review the evidence and ranges before using
> an estimate for a consequential decision.

[Product page](https://wellscoped.dev/products/efforthours) |
[NuGet](https://www.nuget.org/packages/EffortHours.Tool/) |
[Documentation](docs/README.md) |
[Changelog](CHANGELOG.md)

## Quick start

Install the explicit preview version:

```text
dotnet tool install --global EffortHours.Tool --version 0.10.0-alpha.17
eh version
```

Run an effort-only repository estimate:

```text
cd my-repository
eh estimate . --profile implementation --format markdown --no-rate
```

The report gives you:

- low, expected, and high EHE;
- work items grouped by implementation, UI, data, integrations, security, tests,
  documentation, delivery, validation, and review;
- stable evidence IDs and reasoning for every material item; and
- optional replacement-cost output applied only after hours are estimated.

For a compact review or machine-readable output:

```text
eh estimate . --view review --format markdown --no-rate
eh estimate . --format json --compact --output effort-hours.json --no-rate
eh explain . --item <capability-or-work-item-id> --format markdown
```

Update an existing preview installation with:

```text
dotnet tool update --global EffortHours.Tool --version 0.10.0-alpha.17
```

See the [`0.10.0-alpha.17` GitHub prerelease](https://github.com/VictorZakharov/efforthours/releases/tag/v0.10.0-alpha.17) for release notes and artifacts.

## Recent highlights

- **One command for today's work.** `eh change today` finds matching default-branch
  and user-authored open-PR work, prepares the needed repositories, and produces
  one daily EHE/capacity report without a caller-authored manifest.
- **A Codex companion.** `eh agent codex --install` installs versioned guidance so
  Codex uses the native EffortHours workflow immediately instead of scanning a
  workspace or rebuilding the calculation itself.
- **Clearer, faster GitHub-assisted runs.** Safe actionable failures identify
  authentication, permission, network, owner, response, and cache problems.
  Batched discovery and private metadata reuse reduce provider-process overhead
  while complete fallbacks preserve selection coverage.
- **Larger author portfolios.** Author-period workflows now accept up to 256
  repositories and 512 pinned heads under the same bounded execution model.

## Main workflows

### Estimate a repository

```text
eh estimate . --profile implementation --format markdown --no-rate
```

Repository EHE values the current functional and quality state. Ordinary
repository estimates ignore commits, authors, timestamps, churn, and abandoned
approaches.

### Estimate a completed change

```text
eh change . --base main --head feature/my-change --format markdown --no-rate
eh change . --commit HEAD --format markdown --no-rate
eh change . --range main..HEAD --format markdown --no-rate
eh change . --pr 123 --format markdown --no-rate
```

Change EHE values the normalized final delta between immutable base and head
states. The number of commits, contributors, or intermediate edits never
multiplies the result.

### Reconcile a change portfolio

```text
eh change portfolio . --pr 123 --pr 127 --format markdown --no-rate
eh change portfolio --manifest portfolio.json --format markdown --no-rate
eh change portfolio --author-period-manifest author-period.json --format markdown --no-rate
```

For an explicit GitHub-assisted today-to-date capacity comparison with no caller-
authored manifests or arithmetic:

```text
eh change today --owner my-organization --author "@me" \
  --timezone America/Toronto --include-open-prs --scope engineering \
  --capacity-hours 8 --format markdown --output today.md --no-rate
```

This command can run from any folder. It uses authenticated GitHub access to find
relevant work, downloads only the required immutable objects into a private
EffortHours cache, applies the native `engineering` scope, and writes one validated
JSON or concise Markdown report. A complete no-work day reports zero; a failed run
reports no partial EHE or X. Normalized X divides represented replacement effort by
supplied capacity. It does not infer actual hours or sole authorship; interpret it
within selection and shared-credit assumptions. Inspect the scope with
`eh change scope show engineering`.

### Use EffortHours from Codex

Install the packaged companion skill once, then verify it after updating EffortHours:

```text
eh agent codex --install
eh agent codex --check
```

When you ask Codex for an EffortHours or EHE estimate, the skill directs it to run
the highest-level native `eh` command, request needed GitHub/cache permission up
front, and treat the validated report as the calculation of record. Ordinary
estimates never modify Codex configuration. `eh agent codex` prints the packaged
skill without installing it.

Portfolio mode normalizes each repository independently, removes exact repeated
PR patches, unions pinned author-period heads without repeating shared commits,
keeps overlap/revert/shared-context adjustments visible, and allocates the final
EHE exactly. Manifest reports add contributor match-set and head-reachability
ledgers whose low/expected/high totals reconcile to the same portfolio total.
Shared groups are counted once, and requested contributors, repositories, and
heads with no unique match remain visible as zero rows. Manifest author aliases
and local paths remain execution-only. The result is repository-attributed change
effort; it does not reconstruct actual hours, personal labor shares, or sole
authorship.
Manifest execution overlaps at most two repository sessions and shares bounded
immutable inventories, parsed snapshot analyses, Git blob reads, and exact first-
parent evidence lineage across selected commits. The lineage fast path is limited
to unchanged scopes and proven same-size C# numeric-token edits; all other edits
retain full analysis. Deterministic reuse counts remain in report diagnostic
`FB5325`; live phase starts and wall-clock completion timings go to stderr and
never affect report bytes or EHE. Direct author-period runs expose seven active
phases; manifest runs add manifest validation and contributor/head allocation.

## What the model counts

| Principle | Treatment |
| --- | --- |
| Current artifact | Value the working system materially represented now |
| Functional equivalence | Recreate behavior and quality with sensible modern 2026-equivalent technology |
| Evidence before hours | Observe facts, infer capabilities, create work items, then estimate |
| Small logical tasks | Material totals decompose into named evidence-backed tasks, normally about 0.5 to 8 hours |
| Quality actually present | Represent tests, documentation, integrations, delivery, validation, and review at their observed depth |
| Mechanical volume | Do not reward generated, vendored, minified, duplicate, dead, or accidental complexity |
| History | Ignore churn, commit count, contributors, and timestamps as effort signals |
| Pricing | Estimate EHE first; apply a dated rate only afterward |

### Estimation profiles

| Profile | Assumption |
| --- | --- |
| `implementation` | Detailed requirements, acceptance criteria, designs, and contracts are supplied; technical implementation decisions remain included |
| `recreation` | A clear behavioral specification is supplied; more architecture, interface, data-model, and UX decisions must be recovered or made |

Neither profile includes open-ended product discovery.

### Optional pricing

The preview rate card is
`us-senior-software-contractor/2026.1`. Use effort-only output or supply your own
rate without changing EHE:

```text
eh estimate . --no-rate
eh estimate . --hourly-rate 175 --currency USD
eh rate info
```

Cost output is **Equivalent Replacement Cost**, not historical pay or a billing
determination.

## Supported analyzers

EffortHours combines 13 bounded static-analyzer families in one report, including
mixed repositories.

| Analyzer family | Maintained artifacts | Evidence represented |
| --- | --- | --- |
| **.NET** | C# source, projects, solutions, and Razor/UI assets | Project ownership, APIs, behavior, data and migrations, integrations, security, UI, tests, and parser-backed callable-structure diagnostics |
| **JavaScript/TypeScript + frontend** | JavaScript, JSX, TypeScript, TSX, HTML/templates, CSS-family styles, Vue/Svelte components, and static Angular metadata | Packages and workspaces, APIs, behavior, UI/template/style semantics, data, integrations, security, background work, tests, and parser-backed JS/JSX callable diagnostics with explicit TS/TSX coverage gaps |
| **SQL** | PostgreSQL, SQL Server, MySQL/MariaDB, and SQLite-oriented SQL | Schema, migrations, stored programs, queries, tests, deployment, and cross-database evidence |
| **Python + Jupyter** | `.py`, `.pyi`, package metadata, and bounded `.ipynb` notebooks | Package ownership, structure, APIs, qualified frameworks, tests, Markdown, data analysis, visualization, and integrations |
| **Go** | Modules, workspaces, `.go` source, and `_test.go` tests | Packages, local replacements, declarations, APIs, qualified semantics, concurrency, build directives, and tests |
| **Java** | `.java`, Maven reactors/POMs, and Gradle multi-project metadata | Packages/modules, types, methods, APIs, concurrency, Spring/Jakarta and integration semantics, and JUnit/TestNG tests |
| **Kotlin/JVM** | `.kt`, non-Gradle `.kts`, and shared Maven/Gradle JVM metadata | Declarations, APIs, coroutines/Flow, server, Android/Compose, data, integrations, security, background work, and tests |
| **Shell + PowerShell** | Maintained scripts, modules, and tests | Product logic plus build, CI, delivery, infrastructure, integration, security, validation, and invocation-context evidence |
| **Terraform/HCL** | Terraform configuration, tests, and relevant HCL | Resources, data sources, modules, interfaces, providers, backends, lifecycle/dependency structure, security, documentation, and delivery |
| **PHP/Composer** | PHP, Blade templates, Composer packages, and tests | Package ownership, dependencies, autoloading, entry points, declarations, APIs, qualified frameworks, templates, and tests |
| **Rust/Cargo** | Rust source, Cargo packages/workspaces, targets, tests, benchmarks, and examples | Declarations, APIs, generics/lifetimes, async/concurrency, unsafe/error paths, qualified semantics, FFI, and tests |
| **Docker/Compose** | Dockerfile variants, filename-qualified Compose files, and `.dockerignore` | Build stages and instructions, services, orchestration, literal local Dockerfile references, and ignore rules |
| **C/C++** | C99 through C23, C++11 through C++23, headers, modules, and static build metadata | Declarations, APIs, templates/concepts, concurrency, FFI, tests, qualified semantics, and CMake/Make/Meson/MSBuild ownership |

Unsupported languages still receive common inventory evidence, but not the same
semantic depth. Exact ecosystem boundaries are linked from the
[documentation index](docs/README.md).

### Performance and scale

EffortHours uses bounded parallel static analysis and remains responsive on large
repositories. Every recorded dedicated million-line analyzer checkpoint completed
in under 15 seconds on the project workstation. These are reproducible engineering
checkpoints, not universal latency guarantees. See the
[benchmark protocol and full results](docs/BENCHMARKS.md) for measurements,
machine details, safety checks, and limitations.

## How Change EHE works

Change analysis is deliberately about the final represented delta, not activity.

| Change signal | Treatment |
| --- | --- |
| Formatting-only edits, exact moves, lockfiles, build output, and exact copies | No body implementation effort |
| Conventional generated, vendored, or minified bodies | Excluded |
| Safely isolated `<custom-code>` regions in generated files | Bounded customization only |
| Added capabilities | Preserved as distinct additive work |
| Modified existing capabilities | One bounded, diminishing evidence-derived budget |
| Deletion | Never negative and never valued by deleted volume |
| Multi-commit range | Final base-to-head delta remains authoritative |
| Overlap and reverts | Separate structural diagnostics; never effort multipliers |

Language-aware normalizers preserve meaningful tokens, documentation, directives,
indentation, literals, ordered configuration, and other semantic structure while
allowing ordinary formatting to normalize to zero. Ambiguous or unsafe inputs fail
closed.

The current logical admission starts with one-to-several-day changes. Eligible
4-to-32-hour cases are decomposed into distinct evidence-backed tasks, normally
0.5 to 1.5 expected hours each. Larger deliverables and newer ecosystem paths
remain outside that admitted band.

Git selectors read immutable local objects without checking them out. PR mode uses
an installed, authenticated `gh` CLI only to resolve base/head identities; those
objects must already exist locally. Non-Git mode can compare two directories or
two digest-checked evidence bundles.

See the [Change EHE contract](docs/CHANGE_ESTIMATION.md) for exact selector,
normalization, reconciliation, rework-diagnostic, and portfolio semantics.

## Offline and safe by default

For normal `scan` and `estimate` commands, EffortHours:

- does not execute target code, build scripts, tests, compilers, or runtimes;
- does not install dependencies or invoke package managers;
- does not access the network;
- does not inspect Git history, contributors, churn, or timestamps;
- does not write into the target repository;
- does not follow links outside the selected scope; and
- does not send repository content to an AI provider.

Source trees are treated as untrusted input. Analysis is bounded and
deterministic. Structured data stays on stdout, diagnostics stay on stderr, and
ordinary reports avoid source excerpts. An optional external cache can speed
unchanged scans, but it is never an effort signal.

## Maturity and important limits

EffortHours is a working public alpha, not a production-calibrated estimator.
Repository EHE remains experimental. Change EHE has limited validation for
smaller completed changes; larger changes and newer analyzer ecosystems should be
treated as exploratory. Optional host-AI review can help inspect evidence, but it
does not silently alter estimates.

Keep these limits in mind:

- ranges are planning bounds, not formal confidence intervals;
- the fastest repository path assumes discovered tests pass and does not prove a
  checkout builds or runs;
- checked-in coverage can be stale because EffortHours does not regenerate it;
- exact portfolio normalization cannot recover every rebase, squash, semantic
  clone, or shared-human-credit ambiguity; and
- an estimate is not a security, accessibility, architecture, or code-quality
  audit.

Detailed boundaries and research status are in the [product charter](docs/PRODUCT.md),
[estimation model](docs/ESTIMATION_MODEL.md), and [calibration record](docs/CALIBRATION.md).

## Optional host-AI review

The local baseline is complete without AI. A surrounding AI session can use
`eh review packet` to inspect a compact, rate-free, provider-neutral uncertainty
packet and request bounded evidence or explicitly selected admitted-source detail.

EffortHours does not choose or call a provider, transmit material itself, or apply
proposed adjustments. The caller controls disclosure, privacy, retention, model
choice, and cost. See the [host-review protocol](docs/HOST_REVIEW.md).

## Command reference

<details>
<summary>Show common commands</summary>

```text
eh scan <repository> [--output evidence.json]
eh estimate <repository-or-evidence.json> [--profile implementation|recreation]
eh report <estimate.json> [--view review|category|scope|work-item]
eh explain <repository-or-evidence.json> --item <id>
eh change <repository> --base <revision> --head <revision>
eh change <repository> --commit <revision> [--parent <revision>]
eh change <repository> --range <base>..<head>
eh change <repository> --pr <number-or-url> [--repo <owner/name>]
eh change --base-path <before> --head-path <after>
eh change portfolio <repository> --pr <pr> --pr <pr>
eh change portfolio --manifest <portfolio.json>
eh change portfolio --author-period-manifest <manifest.json>
eh change today --owner <owner> --author "@me" --timezone <zone> --capacity-hours <hours>
eh change scope show engineering
eh change explain <change-estimate.json> --item <id>
eh agent codex [--install|--check]
eh review packet <repository> --compact
eh calibration uncertainty-features <estimate.json> <evidence.json> --compact
eh calibration uncertainty-structure <estimate.json> <evidence.json> --compact
eh calibration uncertainty-graph <estimate.json> <evidence.json> --compact
eh calibration uncertainty-evaluate <development-corpus.json> <features.json>... --compact
eh calibration uncertainty-structure-evaluate <development-corpus.json> <structural-features.json>... --compact
eh calibration uncertainty-graph-evaluate <development-corpus.json> <graph-features.json>... --compact
eh calibration uncertainty-support <population.json> <features.json>... --compact
eh calibration uncertainty-support-evaluate <development-corpus.json> <support-profile.json> <features.json>... --compact
eh model info
eh rate info
eh schema list
```

</details>

Run `eh --help` or a subcommand's help for the complete option surface.

## Documentation

| Topic | Reference |
| --- | --- |
| Product boundary | [Product charter](docs/PRODUCT.md) |
| Estimation semantics | [Estimation model](docs/ESTIMATION_MODEL.md) |
| Calibration and repository-model admission | [Calibration](docs/CALIBRATION.md) and [historical frozen v1 policy](docs/MODEL_ADMISSION.md) |
| Change and portfolio semantics | [Change EHE contract](docs/CHANGE_ESTIMATION.md) |
| GitHub today mode and Codex integration | [Author-period workflows](docs/AUTHOR_PERIOD_SCAFFOLDING.md) and [Codex companion](docs/CODEX_INTEGRATION.md) |
| Analyzer-specific boundaries | [Documentation index](docs/README.md) |
| Performance | [Benchmark protocol and results](docs/BENCHMARKS.md) |
| Versioned schemas | [Schemas](schemas/) |
| Release process | [Releasing](docs/RELEASING.md) |

## Build and contribute

The repository uses the .NET 10 SDK selected by `global.json`.

```text
dotnet restore EffortHours.slnx --configfile NuGet.Config --locked-mode
dotnet build EffortHours.slnx --no-restore --configuration Release
dotnet test tests/EffortHours.Tests/EffortHours.Tests.csproj --no-build --no-restore --configuration Release
dotnet test tests/EffortHours.EndToEndTests/EffortHours.EndToEndTests.csproj --no-build --no-restore --configuration Release
```

Read [CONTRIBUTING.md](CONTRIBUTING.md) before opening a change. Security reports
follow [SECURITY.md](SECURITY.md). EffortHours is licensed under the
[MIT License](LICENSE).
