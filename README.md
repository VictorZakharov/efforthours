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
| A low/expected/high range built from evidence-backed work items | A timesheet, productivity score, or authorship detector |
| An offline static analysis with transparent rules | A build, runtime, security, accessibility, or quality audit |
| An effort model with optional pricing applied afterward | An invoice or compensation recommendation |

> [!WARNING]
> **Experimental public alpha.** The CLI and reporting pipeline work, but the
> repository estimator remains uncalibrated. Change EHE has passed only its
> documented model-authored logical gate for 4-to-32-hour changes on the previously
> admitted language set. It is not empirically production-validated. Review the
> evidence and ranges before using an estimate for a consequential decision.

[Product page](https://wellscoped.dev/products/efforthours) |
[NuGet](https://www.nuget.org/packages/EffortHours.Tool/) |
[Documentation](docs/README.md) |
[Changelog](CHANGELOG.md)

## Quick start

Install the explicit preview version:

```text
dotnet tool install --global EffortHours.Tool --version 0.10.0-alpha.4
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
dotnet tool update --global EffortHours.Tool --version 0.10.0-alpha.4
```

See the
[`0.10.0-alpha.4` GitHub prerelease](https://github.com/VictorZakharov/efforthours/releases/tag/v0.10.0-alpha.4)
for release notes and artifacts.

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

Portfolio mode normalizes each repository independently, removes exact repeated
PR patches, unions pinned author-period heads without repeating shared commits,
keeps overlap/revert/shared-context adjustments visible, and allocates the final
EHE exactly. Manifest reports add contributor match-set and head-reachability
ledgers whose low/expected/high totals reconcile to the same portfolio total.
Shared groups are counted once, and requested contributors, repositories, and
heads with no unique match remain visible as zero rows. Manifest author aliases
and local paths remain execution-only. The result is repository-attributed change
effort, not individual productivity, personal labor shares, or sole authorship.
Manifest execution processes one repository at a time and shares bounded immutable
inventories, parsed snapshot analyses, and Git blob reads across its selected
commits. Deterministic reuse counts remain in report diagnostic `FB5325`; live
phase starts and wall-clock completion timings go to stderr and never affect report
bytes or EHE. Direct author-period runs expose seven active phases; manifest runs
add manifest validation and contributor/head allocation.

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
| **.NET** | C# source, projects, solutions, and Razor/UI assets | Project ownership, APIs, behavior, data and migrations, integrations, security, UI, and tests |
| **JavaScript/TypeScript + frontend** | JavaScript, JSX, TypeScript, TSX, HTML/templates, CSS-family styles, Vue/Svelte components, and static Angular metadata | Packages and workspaces, APIs, behavior, UI/template/style semantics, data, integrations, security, background work, and tests |
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

### One-million-line performance checkpoints

Every recorded dedicated analyzer shape completed in under 15 seconds on the
checkpoint workstation.

Measurements use fresh processes and generated many-small-file repositories on
Windows x64, an AMD Ryzen 9 5900X, 24 logical processors, and .NET 10. Fixture
generation and evidence serialization are excluded. These are reproducible
engineering checkpoints, not universal latency guarantees or representative
framework workloads.

| Language/analyzer shape | Analyzed text lines | Static analysis | Lines/s | Sampled peak |
| --- | ---: | ---: | ---: | ---: |
| .NET / C# | 1,000,001 | 7.083 s | 141,179 | 272.52 MiB |
| JavaScript / TypeScript | 1,000,001 | 12.088 s | 82,728 | 185.69 MiB |
| HTML/CSS/SCSS frontend assets | 1,000,001 | 8.187 s | 122,151 | 270.67 MiB |
| SQL | 1,000,000 | 8.421 s | 118,754 | 156.46 MiB |
| Python | 1,000,003 | 13.354 s | 74,883 | 109.63 MiB |
| Jupyter | 1,120,003 | 7.561 s | 148,131 | 159.33 MiB |
| Go | 1,000,003 | 6.577 s | 152,057 | 119.95 MiB |
| Java | 1,010,001 | 13.954 s | 72,379 | 167.31 MiB |
| Kotlin/JVM | 1,000,001 | 9.393 s | 106,459 | 166.83 MiB |
| Shell | 990,000 | 14.525 s | 68,161 | 119.66 MiB |
| PowerShell | 990,000 | 13.689 s | 72,321 | 130.76 MiB |
| Terraform/HCL | 1,000,000 | 8.239 s | 121,371 | 303.72 MiB |
| PHP/Composer | 1,000,001 | 7.920 s | 126,270 | 441.57 MiB |
| Rust/Cargo | 1,000,004 | 7.540 s | 132,627 | 139.88 MiB |
| Docker/Compose | 1,000,000 | 8.097 s | 123,502 | 203.00 MiB |
| C | 990,002 | 8.197 s | 120,781 | 127.65 MiB |
| C++ | 990,002 | 9.716 s | 101,891 | 128.84 MiB |
| Mixed .NET/JS/TS/C/C++ | 996,004 | 13.214 s | 75,375 | 205.70 MiB |

Read the [benchmark protocol and full results](docs/BENCHMARKS.md) for fixture
definitions, analyzer versions, allocation data, safety checks, and limitations.

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

## Model status

| Area | Current status |
| --- | --- |
| Repository EHE | Experimental and uncalibrated; `logical-capability/0.3.0` improves blind-validation expected WAPE from `0.2279` to `0.0940` but fails six frozen error, coverage, width, and material-category gates; it is retired without test disclosure, and `seed-rules/0.4.0` remains shipped |
| Change EHE, documented Stage A band | Model-authored logical gate passed for eligible 4-to-32-hour changes on the previously admitted language set |
| Newer analyzer ecosystems | Experimental and outside the existing Change admission boundary |
| Production validation | No empirical production-accuracy claim |
| Host-AI review | Optional, provider-neutral, and non-applying; no automatic review budget selected |

The logical gate uses evidence-backed task decomposition as weak supervision.
Independent replication can corroborate it, while later production observations
remain a separate evidence track.

Important limits:

- ranges are planning bounds, not formal confidence intervals;
- the fastest repository path assumes discovered tests pass and does not prove a
  checkout builds or runs;
- checked-in coverage can be stale because EffortHours does not regenerate it;
- exact portfolio normalization cannot recover every rebase, squash, semantic
  clone, or shared-human-credit ambiguity; and
- an estimate is not a security, accessibility, architecture, or code-quality
  audit.

The [product charter](docs/PRODUCT.md) and
[estimation model](docs/ESTIMATION_MODEL.md) define the precise semantics.

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
eh change explain <change-estimate.json> --item <id>
eh review packet <repository> --compact
eh calibration uncertainty-features <estimate.json> <evidence.json> --compact
eh calibration uncertainty-evaluate <development-corpus.json> <features.json>... --compact
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
| Calibration and repository-model admission | [Calibration](docs/CALIBRATION.md) and [frozen admission policy](docs/MODEL_ADMISSION.md) |
| Change and portfolio semantics | [Change EHE contract](docs/CHANGE_ESTIMATION.md) |
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
