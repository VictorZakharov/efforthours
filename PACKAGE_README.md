# EffortHours CLI

EffortHours is an experimental .NET 10 command-line tool for estimating **Equivalent
Human Effort (EHE)**: the counterfactual time one competent senior contractor,
unfamiliar with the business domain and not using AI, would need to recreate a
software repository's current functional and quality state from a clear
specification.

> This is a public alpha. The bundled estimators are transparent but uncalibrated.
> EffortHours output is not actual labor history, a timesheet, an invoice, or an
> empirically validated billing determination.

## Install

```text
dotnet tool install --global EffortHours.Tool --version 0.10.0-alpha.12
eh version
eh --help
```

## Estimate a repository

```text
eh estimate ./my-repository --profile implementation --format markdown
```

## Supported analyzers

EffortHours combines 13 analyzer families in one report, including mixed
repositories. Each family uses bounded static evidence; the table shows the
maintained artifacts and the kind of work it can represent.

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

Evidence becomes traceable work items across implementation, UI, data,
integrations, security, testing, documentation, delivery, validation, and review.
A dated contractor rate can be applied afterward without changing the effort
estimate.

See the
[per-language million-line performance checkpoints](https://github.com/VictorZakharov/efforthours#one-million-line-performance-checkpoints)
and their
[full benchmark protocol](https://github.com/VictorZakharov/efforthours/blob/main/docs/BENCHMARKS.md).

### Shared static-analysis boundary

- EffortHours reads scanner-admitted, digest-verified files inside the selected
  repository scope. It does not follow repository links outside that scope.
- It does not run target code, runtimes, compilers, tests, build systems, package
  managers, deployment tools, or generators, and it does not install or fetch
  target dependencies.
- It does not prove compilation or runtime behavior, evaluate dynamic
  configuration, or treat mechanical source volume as effort. Generated,
  vendored, minified, binary, and exact-duplicate content is conservatively
  excluded or bounded.
- Ordinary reports avoid source excerpts, configured values, secrets, and embedded
  notebook payloads.

### Ecosystem-specific boundaries

- **.NET:** C# structure is Roslyn-backed, but EffortHours does not build projects,
  restore packages, execute source generators, run tests, or prove runtime
  registration.
- **JavaScript/TypeScript + frontend:** JavaScript and JSX are parser-backed;
  TypeScript and TSX are token-backed. React/Preact/Next-style JSX, Vue, Svelte,
  maintained HTML, CSS/SCSS/Sass/Less, and import-qualified static Angular
  component metadata receive bounded evidence. EffortHours does not render a UI,
  execute a preprocessor, compile a framework, or evaluate executable
  configuration; Angular assets must be unambiguous scanner-admitted relative
  files.
- **SQL:** Analysis is token/statement-backed rather than a full grammar, schema
  diff engine, query optimizer, or database validator. It does not connect to a
  database, execute SQL, or prove dialect validity or performance.
- **Python + Jupyter:** Python evidence is token/indentation-backed. EffortHours
  does not invoke Python, import modules, resolve environments, install packages,
  or execute `setup.py`. It analyzes only unambiguous Python notebook cells and
  never launches Jupyter or a kernel; outputs, execution counts, widget state,
  attachments, embedded payloads, magics, shell escapes, and unsupported-language
  cells do not add EHE. Execution state, data provenance, reproducibility, output
  correctness, runtime dependencies, and scientific validity are not verified.
- **Go:** EffortHours does not invoke the Go toolchain, resolve build constraints,
  expand embedded assets, run generators, compile cgo, or prove runtime
  registration.
- **Java:** Evidence is token-backed. EffortHours does not invoke a JVM, compiler,
  Maven, Gradle, wrappers, annotation processors, or tests; evaluate build DSL; or
  prove bytecode or runtime behavior.
- **Kotlin/JVM:** Evidence is token-backed. EffortHours does not invoke a JVM,
  Kotlin compiler, Gradle, Android tooling, KSP, kapt, compiler plugins, or tests.
  Android resources, generated output, and multiplatform binding are not resolved.
- **Shell + PowerShell:** EffortHours never starts a shell, resolves commands or
  modules, sources files, evaluates expansions, or proves platform/runtime
  effects.
- **Terraform/HCL:** Evidence is token-backed and local module ownership is
  resolved only inside the repository. EffortHours does not run Terraform, fetch
  modules/providers, contact backends, load provider schemas, evaluate state,
  plans, interpolation, or policy, or prove runtime correctness. Terraform JSON is
  not analyzed as HCL.
- **PHP/Composer:** Evidence is token-backed. EffortHours does not invoke PHP,
  Composer, autoloaders, package scripts, framework bootstraps, containers,
  routes, reflection, dependency resolution, or tests; dynamic includes and
  runtime behavior are not resolved.
- **Rust/Cargo:** Evidence is token-backed. EffortHours does not invoke Cargo,
  rustc, build scripts, procedural macros, generators, tests, examples, or
  benchmarks. It does not resolve dependencies, active features, target triples,
  macro expansion, generated bindings, borrow checking, trait selection, or
  runtime behavior.
- **Docker/Compose:** Analysis is bounded structure, not general YAML or complete
  Docker/BuildKit/Compose parsing. EffortHours does not invoke container tooling,
  a shell, or target code; pull or inspect images; expand build contexts; load
  includes or environment files; resolve interpolation or secrets; validate a
  Compose schema; or prove deployment behavior. Arbitrary YAML is not treated as
  Compose.
- **C/C++:** Evidence is token-backed rather than compiler-, preprocessor-, or
  native-parser-backed. EffortHours does not invoke a compiler, preprocessor,
  linker, build system, generator, package manager, or tests; expand headers or
  macros; resolve active configurations, system headers, types, links, or template
  instantiations; or prove compilation, ABI behavior, memory safety, or runtime
  correctness.

## Estimate a final change

```text
eh change ./my-repository --commit <revision> --format markdown
eh change ./my-repository --range <base>..<head> --format markdown
eh change ./my-repository --pr <number> --format markdown
eh change portfolio --author-period-manifest <manifest.json> --format markdown --no-rate
```

Change EHE estimates the normalized final functional and quality delta. Commit
activity, author identity, timestamps, and intermediate churn do not multiply
effort. Pull-request identity resolution optionally uses an installed `gh` CLI;
the selected Git objects must already exist locally. The current change rules
require changed capability evidence for existing-capability modifications and
consolidate repository work-item partitions for one capability into a bounded
logical budget while preserving distinct capabilities.

The author-period manifest can union pinned heads across local repositories and
contributors without counting shared commits or shared-credit groups repeatedly.
Aliases and local paths remain execution-only. One invocation processes
repositories sequentially and reuses bounded immutable inventories, snapshot
analysis, and Git blobs; privacy-safe reuse counters stay in the report while
non-semantic phase timings are written to stderr.

## Review consequential uncertainty

```text
eh review packet ./my-repository --compact
eh review query ./my-repository --input-digest <packet-digest> --capability <id> --reason <reason>
eh review validate review-packet.json proposed-adjustments.json
eh review measure review-packet.json proposed-adjustments.json --subject <opaque-id> --session <opaque-id> --context compact
eh review benchmark compact.measurement.json broader-source.measurement.json
```

The provider-neutral review packet is rate-free and contains no source excerpts.
A surrounding AI session can request bounded capability, evidence, scope, or
explicitly selected admitted-source detail. EffortHours does not call a provider,
transmit repository material, or apply proposed adjustments. The caller controls
provider, privacy, disclosure, and retention choices.

Optional measurement commands sanitize completed-session telemetry and compare
compact review with a broader-source reference. They record only telemetry the
caller supplies, do not infer missing provider tokens, time, or cost, and do not
select an automatic review budget. Caller-supplied IDs, telemetry bases, and notes
are retained verbatim and must be non-sensitive.

## Offline and safety boundary

Default repository analysis does not execute target code, install target
dependencies, fetch from the network, or inspect Git history. Source trees are
treated as untrusted input, and reports avoid source excerpts by default.

## Current limitations

- `seed-rules/0.4.0` and `change-seed/0.18.1` remain experimental and uncalibrated.
- Public calibration labels have not completed genuinely independent correction.
- Host-review token use, cost, and estimate improvement have not yet been measured
  across representative repositories; no automatic review budget is selected.

The schemas, estimation decisions, calibration provenance, benchmarks, source,
issues, and contribution process are available in the
[EffortHours GitHub repository](https://github.com/VictorZakharov/efforthours).

EffortHours is distributed under the
[MIT License](https://github.com/VictorZakharov/efforthours/blob/main/LICENSE).
