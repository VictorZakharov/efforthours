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
dotnet tool install --global EffortHours.Tool --version 0.9.0-alpha.3
eh version
eh --help
```

## Estimate a repository

```text
eh estimate ./my-repository --profile implementation --format markdown
```

EffortHours statically analyzes .NET, JavaScript, TypeScript, Python, Go, Java,
Kotlin/JVM, Shell, PowerShell, Terraform/HCL, PHP/Composer, Rust/Cargo, HTML/CSS-family
frontends, SQL, and mixed repositories. Frontend support includes bounded template and stylesheet semantics
plus static Angular component metadata. SQL support includes bounded schema,
migration, stored-program, query, test, deployment, and cross-database evidence
for common PostgreSQL, SQL Server, MySQL/MariaDB, and SQLite syntax. It does not
render, compile frameworks, execute preprocessors, connect to a database, or
execute SQL.
Python support includes `.py`/`.pyi`, static package metadata, bounded token and
indentation structure, tests, and conservative import-qualified framework
evidence. It does not invoke Python, import modules, resolve environments, install
packages, execute `setup.py`, or parse notebooks.
Go support includes modules, workspaces, packages, local replacements, bounded
token structure, import-qualified semantic evidence, concurrency, build
directives, and `_test.go` tests. It does not invoke the Go toolchain, resolve
build constraints, expand embedded assets, run generators, compile cgo, or prove
runtime registration.
Java support includes bounded package/module/type/method structure, public APIs,
concurrency, JUnit/TestNG tests, import-qualified Spring/Jakarta and integration
evidence, static Maven reactors/POMs, and conservative Gradle multi-project
metadata. It does not invoke a JVM, Maven, Gradle, wrappers, compilers, annotation
processors, or tests, and it does not evaluate build DSL or prove runtime behavior.
Kotlin/JVM support includes maintained `.kt` and non-Gradle `.kts` source,
Kotlin declarations, coroutines/Flow, tests, import-qualified server,
Android/Compose, data, integration, security, and background evidence, and shared
static Maven/Gradle JVM ownership. It does not invoke a JVM, Kotlin compiler,
Gradle, Android tooling, KSP, kapt, compiler plugins, or tests.
Shell and PowerShell support includes maintained product scripts, reusable
modules, tests, and build/CI/delivery/infrastructure automation with bounded
token-backed structure and exact invocation-context evidence. It never starts a
shell, resolves commands or modules, sources files, evaluates expansions, or emits
source values or excerpts.
Terraform/HCL support includes bounded resources, data sources, modules, inputs,
outputs, locals, providers, backends, lifecycle/dependency/expression structure,
tests, security-sensitive configuration, documentation, and delivery evidence.
It resolves repository-local module ownership only and never runs Terraform,
fetches modules/providers, contacts backends, evaluates plans/interpolation, or
emits configured values or source excerpts.
PHP/Composer support includes static package ownership, dependencies, autoload
mappings, scripts, binary entry points, literal local path repositories, bounded
token-backed declarations and public APIs, import-qualified framework semantics,
tests, and PHP/Blade template structure. It never invokes PHP, Composer,
autoloaders, package scripts, framework bootstraps, containers, routes, reflection,
dependency resolution, or tests.
Rust/Cargo support includes static packages and workspaces, dependencies, features,
build scripts, local edges, targets, bounded token-backed declarations and public
APIs, async/concurrency, unsafe/error paths, import-qualified semantics, FFI,
tests, benchmarks, and examples. It never invokes Cargo, rustc, build scripts,
procedural macros, generators, tests, examples, or benchmarks, and it does not
resolve dependencies, active features, target triples, or generated bodies.
It reports evidence-backed work items across implementation, testing,
documentation, integration, delivery, validation, and review, then optionally
applies a dated contractor rate without changing the effort estimate.

## Estimate a final change

```text
eh change ./my-repository --commit <revision> --format markdown
eh change ./my-repository --range <base>..<head> --format markdown
eh change ./my-repository --pr <number> --format markdown
```

Change EHE estimates the normalized final functional and quality delta. Commit
activity, author identity, timestamps, and intermediate churn do not multiply
effort. Pull-request identity resolution optionally uses an installed `gh` CLI;
the selected Git objects must already exist locally. The current change rules
require changed capability evidence for existing-capability modifications and
consolidate repository work-item partitions for one capability into a bounded
logical budget while preserving distinct capabilities.

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

- `seed-rules/0.4.0` and `change-seed/0.15.0` remain experimental and uncalibrated.
- SQL uses bounded token/statement evidence mapped to existing priors; it is not a
  full grammar, schema diff engine, query optimizer, or database validator.
- Public calibration labels have not completed genuinely independent correction.
- TypeScript and TSX evidence is token-backed rather than compiler-backed.
- Python evidence is token/indentation-backed rather than compiler- or runtime-backed.
- Go evidence is token-backed rather than compiler- or toolchain-backed.
- Java evidence is token-backed rather than compiler-, bytecode-, or JVM-backed;
  Maven/Gradle evidence is a conservative static build-model projection.
- Kotlin evidence is token-backed rather than compiler-, bytecode-, or JVM-backed;
  Maven/Gradle evidence is a conservative shared JVM build-model projection, and
  Android resources, KSP/kapt output, and multiplatform binding are not resolved.
- Shell and PowerShell evidence is token-backed rather than interpreter-backed;
  sourced content, dynamic expansion, command/module resolution, and platform or
  runtime effects are not resolved.
- Terraform/HCL evidence is token-backed rather than native-parser/provider-
  backed; Terraform JSON, provider schemas, plans, policy validation,
  interpolation results, and runtime correctness are not evaluated.
- PHP evidence is token-backed rather than native-parser/runtime-backed; Composer
  dependency resolution, autoload execution, dynamic includes, framework
  compilation, container/route registration, reflection, and runtime behavior are
  not resolved.
- Rust evidence is token-backed rather than rustc-backed; Cargo dependency and
  feature resolution, macro expansion, build-script output, generated bindings,
  borrow checking, trait selection, and runtime behavior are not resolved.
- Host-review token use, cost, and estimate improvement have not yet been measured
  across representative repositories; no automatic review budget is selected.

The schemas, estimation decisions, calibration provenance, benchmarks, source,
issues, and contribution process are available in the
[EffortHours GitHub repository](https://github.com/VictorZakharov/efforthours).

EffortHours is distributed under the
[MIT License](https://github.com/VictorZakharov/efforthours/blob/main/LICENSE).
