# EffortHours

Turn a software repository or completed code change into a traceable estimate of
the senior-contractor effort it represents.

EffortHours is an offline-first .NET 10 command-line tool. It statically analyzes
the code, tests, documentation, configuration, and delivery artifacts that exist
today, decomposes them into evidence-backed work items, and reports **Equivalent
Human Effort (EHE)**.

EHE answers this counterfactual question:

> How long would one competent senior contractor, unfamiliar with the business
> domain and not using AI, need to recreate this functional and quality state from
> a clear specification?

It does **not** claim how long anyone actually worked. It is not a timesheet,
productivity score, invoice, or reconstruction of repository history.

> **Experimental public alpha:** the CLI and reporting pipeline work. The bundled
> repository estimator is not numerically calibrated or admitted. The Change
> estimator has passed its first model-authored logical gate for changes estimated at 4 to 32
> hours on the previously admitted non-SQL families, but it is not empirically
> production-validated and the newer SQL, Python, Go, Java, Kotlin, Shell,
> PowerShell, Terraform/HCL, PHP/Composer, Rust/Cargo, Docker/Compose, and Jupyter paths are outside that
> gate.
> Review the evidence and ranges before using any estimate for a consequential
> decision.

## Install

EffortHours is published as the `EffortHours.Tool` .NET global tool. The installed
command is `eh`.

```text
dotnet tool install --global EffortHours.Tool --version 0.9.0-alpha.3
eh version
eh --help
```

Preview versions are opt-in. Update an existing installation with:

```text
dotnet tool update --global EffortHours.Tool --version 0.9.0-alpha.3
```

See the [NuGet package](https://www.nuget.org/packages/EffortHours.Tool/) and the
[latest GitHub prerelease](https://github.com/VictorZakharov/efforthours/releases/tag/v0.9.0-alpha.3).

## Estimate a repository

Run this from the repository you want to estimate:

```text
eh estimate . --profile implementation --format markdown --no-rate
```

EffortHours reports a low, expected, and high EHE range, with work items grouped
into categories such as implementation, UI, data, integrations, security, tests,
documentation, delivery, validation, and review.

Use `--view review` for a compact result or JSON for automation:

```text
eh estimate . --view review --format markdown --no-rate
eh estimate . --format json --compact --output effort-hours.json --no-rate
```

To understand a material capability or work item, copy its ID from the report:

```text
eh explain . --item <capability-or-work-item-id> --format markdown
```

Every represented hour is linked back to observed evidence and a transparent rule.
EffortHours does not emit source excerpts in ordinary evidence or estimate output.

## Estimate a completed change

Change EHE values the final functional and quality delta between immutable base and
head states. Commit count, authors, timestamps, intermediate churn, and abandoned
approaches do not multiply the result.

```text
eh change . --base main --head feature/my-change --format markdown --no-rate
eh change . --commit HEAD --format markdown --no-rate
eh change . --range main..HEAD --format markdown --no-rate
eh change . --pr 123 --format markdown --no-rate
```

Combine completed changes without summing overlapping work twice:

```text
eh change portfolio . --pr 123 --pr 127 --format markdown --no-rate
eh change portfolio --manifest portfolio.json --format markdown --no-rate
eh change portfolio . --author "Contributor <contributor@example.com>" --since 2026-07-01 --until 2026-08-01 --timezone America/Toronto --format markdown --no-rate
```

Portfolio reports show the isolated sum, repository-normalized EHE, one row per
selected change, named duplicate/overlap/revert/shared-context adjustments, and
allocations that sum exactly to normalized expected hours. Repeated PRs are
order-independent; author periods use explicit chronological commit selection.
Cross-repository manifests normalize each repository independently and then add
the results. Identity and time select immutable changes only and never multiply
effort.

This output is repository-attributed Change EHE, not actual labor, sole-authorship
proof, individual productivity, a performance grade, or compensation advice.
Co-authored, interleaved, merge, and overlapping changes retain visible attribution
uncertainty. Portfolio aggregation does not widen the experimental Change model's
existing per-item admission boundary.

For an explicit range containing at least two commits, the report also compares
gross isolated commit EHE with authoritative normalized final-delta EHE. It shows
the gross-to-final normalization share and a narrower rework-like share containing
only explicit overlap/revert attribution. These are structural diagnostics—not
historical rework, actual hours, productivity scores, or multipliers. Copy the
normalization ID into `eh change explain <report.json> --item <id>` to inspect its
signed-adjustment lineage.

The current Change model is intended first for one-to-several-day changes. Its
calibration totals are built from distinct evidence-backed tasks normally about an
hour each, and its own larger work items are split into named phases capped at 1.5
expected hours rather than one unsupported aggregate guess. Larger deliverables
remain outside that admitted size band. For long ranges, EffortHours bounds the
optional per-commit audit while retaining the complete base-to-head estimate as
authoritative.

You can also compare two directories or two saved evidence bundles without Git:

```text
eh change --base-path ../before --head-path ../after --no-rate
eh change --base-evidence before.json --head-evidence after.json --no-rate
```

Git selectors read local immutable objects without checking them out. PR mode uses
an installed, authenticated `gh` CLI only to resolve the PR's immutable base/head
identities; those objects must already exist locally. The normal repository
estimate does not inspect Git history.

Formatting-only changes, exact moves, conventional generated output, vendored or
minified bodies, lockfiles, build output, and exact copies do not create body
implementation effort. Deletion is never negative or valued by deleted volume.
For `.sql`, formatting comparison is token-aware while preserving literal,
quoted-identifier, and comment content. For `.py` and `.pyi`, it is token- and
indentation-aware: formatting and ordinary comments can normalize to zero, while
dedentation, literals, and docstrings remain meaningful. For `.go`, ordinary
formatting and comments can normalize to zero while compiler directives, implicit
semicolon boundaries, identifiers, operators, and literals remain meaningful.
For `.java`, ordinary formatting and non-documentation comments can normalize to
zero while Javadoc/Markdown documentation comments, literals, identifiers,
operators, delimiters, and ambiguous Unicode escapes remain meaningful.
For `.kt` and `.kts`, ordinary formatting, optional semicolons/trailing commas,
and non-documentation comments can normalize to zero while KDoc, literals,
backtick identifiers, operators, delimiters, and semantic newlines after jump
expressions remain meaningful.
For Shell and PowerShell, ordinary formatting and non-directive comments can
normalize to zero while shebangs, PowerShell `#requires`, literals, identifiers,
operators, and delimiters remain meaningful. Here-documents and here-strings fail
closed so uncertain content changes remain represented.
For Terraform and HCL, horizontal layout and blank-line count can normalize to
zero while semantic newlines, comments, identifiers, operators, literals,
templates, delimiters, and heredoc bodies remain meaningful. Incomplete HCL fails
closed.
For PHP, ordinary formatting and non-documentation comments can normalize to zero
while PHPDoc, literals, identifiers, operators, delimiters, heredoc/nowdoc bodies,
and inline template content remain meaningful. Incomplete PHP fails closed.
For Rust, ordinary formatting and non-documentation comments can normalize to zero
while Rustdoc, raw identifiers, literals, lifetimes, attributes, operators, and
delimiters remain meaningful. Incomplete Rust fails closed.
For Dockerfiles, instruction keyword case, ordinary comments, blank lines, and
continuation layout can normalize to zero while directives, arguments, stages,
and commands remain meaningful; heredocs fail closed. For filename-qualified
Compose YAML, comments, blank lines, indentation width, and mapping-colon spacing
can normalize to zero while keys, values, sequences, and document markers remain
meaningful; tabs, malformed flow syntax, and block scalars fail closed.
`.dockerignore` comments and surrounding layout can normalize to zero while
ordered patterns and negations remain meaningful.
For `.ipynb`, JSON layout, source string/array representation, outputs, execution
state, widgets, attachments, raw/non-Python cells, magics, and shell escapes can
normalize to zero. Maintained Python tokens, Markdown, declared language, cell
tags, and meaningful cell ordering remain significant; unsafe inputs fail closed.
The current source build can retain bounded customization inside generated files
only when exact, EffortHours-specific `<custom-code>` regions isolate it safely;
the generated body still contributes zero. Generator-specific protected-region
syntax is not inferred automatically.

See the [Change EHE contract](docs/CHANGE_ESTIMATION.md) for selector,
normalization, reconciliation, and limitation details.

## Choose a profile

| Profile | Assumption |
| --- | --- |
| `implementation` | Detailed requirements, acceptance criteria, designs, and contracts are supplied. Technical design and implementation decisions remain included. |
| `recreation` | A clear behavioral specification is supplied, but more architecture, data-model, interface, and UX decisions must be recovered or made. |

Both profiles estimate a sensible modern 2026-equivalent implementation while
preserving meaningful compatibility and external constraints. Neither includes
open-ended product discovery.

## Pricing is separate from effort

EffortHours estimates hours first. Pricing is an optional, auditable projection
applied afterward and never changes EHE.

The published preview applies the dated
`us-senior-software-contractor/2026.1` rate by default. Use effort-only output or
provide your own rate:

```text
eh estimate . --no-rate
eh estimate . --hourly-rate 175 --currency USD
eh rate info
```

Cost output is **Equivalent Replacement Cost**, not historical pay or a billing
determination.

## What EffortHours analyzes

The current static analyzers support:

- .NET and C# projects, solutions, APIs, data access, migrations, integrations,
  security, Razor/UI surfaces, and tests;
- JavaScript, JSX, TypeScript, and TSX package/workspace structure, APIs, UI,
  data access, integrations, security, background work, and tests;
- React/Preact/Next-style JSX plus Vue and Svelte components;
- static Angular `@Component` metadata, including literal inline and relative
  external templates and styles when ownership is unambiguous;
- maintained HTML template structure, forms, bindings, and directives through a
  bounded tolerant scanner;
- CSS, SCSS/Sass, and Less rules/selectors, responsive surfaces, design tokens,
  animation, and theme signals through bounded tolerant scanners;
- standalone or project/package-owned `.sql` schemas, migrations, indexes,
  constraints, stored programs, queries, test fixtures, deployment scripts, and
  explicit cross-database boundaries through bounded static token analysis;
- Python 3 `.py` and `.pyi` package/module/import structure, functions, classes,
  async and branching structure, tests, and conservative import-qualified API,
  CLI, data, integration, security, validation, and background-work evidence;
- static Python package metadata from `pyproject.toml`, `setup.cfg`, literal-only
  `setup.py`, requirements, Pipfile, and common Poetry/PDM/uv surfaces without
  invoking Python or resolving an environment;
- maintained Jupyter `.ipynb` Python code cells and Markdown narrative through a
  digest-verified bounded JSON projection, including qualified data-analysis,
  visualization, integration, and test evidence while excluding outputs,
  execution state, widgets, attachments, magics, shell escapes, and unsupported
  language cells;
- Go modules, workspaces, packages, local replacements, internal references,
  commands, libraries, exported APIs, generics, concurrency, build constraints,
  embedded-asset declarations, and `_test.go` structure through bounded static
  token analysis;
- conservative import-qualified Go API, CLI, data, integration, security,
  validation, background-work, synchronization, and test evidence;
- Java packages, modules, classes, records, interfaces, enums, methods,
  annotations, generics, exceptions, concurrency, public APIs, and JUnit/TestNG
  tests through bounded static token analysis;
- static Maven POM/reactor and conservative Gradle multi-project metadata,
  including local edges and explicit dynamic-build uncertainty, without invoking
  a JVM, Maven, Gradle, wrappers, compilers, or annotation processors;
- import- and annotation-qualified Spring/Jakarta API, persistence, security,
  messaging, scheduling, validation, CLI, and integration evidence;
- Kotlin/JVM `.kt` and maintained `.kts` package, class/object/data/sealed type,
  function, extension, nullability, coroutine, Flow, public API, and test
  structure through bounded static token analysis;
- import-qualified Ktor/Spring server, Android/Compose, Room/Exposed/JPA,
  integration, security, validation, scheduling, coroutine, Flow, and test
  evidence, with Gradle Kotlin DSL kept separate from maintained scripts;
- maintained POSIX-family Shell/Bash and PowerShell product scripts, modules,
  tests, build/CI/delivery/infrastructure automation, command structure, file/
  network/process boundaries, security surfaces, and error handling through
  bounded static token and invocation-context analysis;
- maintained Terraform/HCL resources, data sources, modules, variables, outputs,
  locals, providers, backends, lifecycle/dynamic/dependency and expression
  structure, tests, security-sensitive configuration, interface documentation,
  and delivery configuration through bounded static token analysis;
- repository-local Terraform module ownership plus unresolved registry, Git,
  HTTP, object-storage, and dynamic module boundaries without fetching them;
- Composer packages, dependencies, autoload namespaces and roots, scripts, binary
  entry points, literal repository-local path repositories, and maintained PHP
  package ownership without invoking PHP or Composer;
- PHP namespaces, imports, declarations, public APIs, attributes, branches,
  exceptions, tests, conservative import-qualified framework semantics, and
  bounded PHP/Blade template structure through static token analysis;
- Cargo packages and virtual workspaces, dependencies, features, build scripts,
  conventional and explicit targets, literal repository-local edges, and
  maintained Rust ownership without invoking Cargo or rustc;
- Rust modules, uses, structs, enums, traits, implementations, functions, public
  APIs, generics, lifetimes, async/concurrency, unsafe and error paths, tests,
  benchmarks, examples, FFI boundaries, and import-qualified semantic evidence
  through bounded static token analysis;
- Dockerfile logical instructions, stages, build/runtime boundaries, multi-stage
  copies, health/user/volume/port configuration, and BuildKit mount uncertainty;
- filename-qualified Docker Compose services, builds, commands, ports,
  environment boundaries, volumes, networks, dependencies, health checks,
  profiles, secrets/configs, deploy/security structure, and literal local
  Compose-to-Dockerfile references, plus bounded `.dockerignore` rules;
- mixed .NET, JavaScript/TypeScript, Python/Jupyter, Go, Java/Kotlin, Shell/PowerShell,
  Terraform/HCL, PHP/Composer, Rust/Cargo, Docker/Compose, and SQL repositories;
- documentation, build configuration, CI, containers, infrastructure, package
  metadata, and checked-in LCOV or Cobertura coverage reports; and
- generated, vendored, minified, binary, duplicate, test, and documentation
  classification used to prevent mechanical volume from inflating effort.

JavaScript and JSX structure is parser-backed. TypeScript and TSX are explicitly
token-backed in this release. Angular metadata analysis requires a named
`Component` import from `@angular/core` (including a local alias), accepts only
static literals and arrays, and never evaluates TypeScript or executable
configuration; external assets must resolve to scanner-admitted files inside the
repository.
HTML and CSS-family analysis is tolerant and structural: it does not render a UI,
compile a framework, run a preprocessor, or prove runtime behavior. Explicit static
roles, labels, alternative text, live regions, and keyboard/focus signals in HTML
and Angular templates contribute bounded accessibility evidence, clearly labeled
as not proving conformance. This is not an accessibility audit. Physical
markup/style line count is retained as evidence but is not an EHE driver.
SQL analysis recognizes conservative PostgreSQL, SQL Server, MySQL/MariaDB, and
SQLite syntax signals without choosing or connecting to a database. It does not
compile SQL, prove name/type correctness, execute migrations, inspect query plans,
or infer effort from rows, dumps, timestamps, or migration versions. See the
[static SQL boundary](docs/SQL_ANALYSIS.md).
Python analysis uses a bounded managed tokenizer and indentation-aware structural
pass. Framework evidence requires matching import context; local namesakes do not
qualify. It does not execute `setup.py`, import modules, install dependencies,
type-check, or discover runtime routes. See the
[static Python boundary](docs/PYTHON_ANALYSIS.md).
Jupyter analysis never starts a kernel or reads output payloads. It admits only
bounded Python cells through the Python tokenizer, represents Markdown separately,
and treats execution state, data provenance, reproducibility, output correctness,
runtime dependencies, mixed languages, and scientific validity as unverified. See
the [static Jupyter boundary](docs/JUPYTER_ANALYSIS.md).
Go analysis statically reads scanner-admitted `go.mod`, `go.work`, and `.go`
files. It does not invoke the Go toolchain, resolve build constraints, expand
`go:embed` patterns, run `go:generate`, compile cgo, prove reflection or runtime
registration, or emit source excerpts. See the
[static Go boundary](docs/GO_ANALYSIS.md).
Java analysis statically reads scanner-admitted Maven/Gradle descriptors and
maintained `.java` files. It does not evaluate build DSL, resolve dependencies,
compile source, run annotation processors/tests, inspect bytecode, or prove
reflection/runtime behavior. See the
[static Java boundary](docs/JAVA_ANALYSIS.md).
Kotlin analysis reuses static JVM module ownership while reading maintained `.kt`
and non-Gradle `.kts` files. It does not evaluate Gradle Kotlin DSL, resolve
dependencies or source sets, compile source, run KSP/kapt/compiler plugins/tests,
inspect bytecode, or prove Android, multiplatform, reflection, or runtime DSL
behavior. See the [static Kotlin/JVM boundary](docs/KOTLIN_ANALYSIS.md).
Shell and PowerShell analysis reads only scanner-admitted maintained scripts and
bounded manifest/automation invocation context. It does not start a shell, resolve
commands or modules, source files, evaluate expansions, observe platform effects,
or emit source values or excerpts. Dynamic behavior remains explicit uncertainty.
See the [static Shell and PowerShell boundary](docs/SHELL_POWERSHELL_ANALYSIS.md).
Terraform/HCL analysis reads only scanner-admitted, digest-matched static text. It
does not run Terraform or related tools, fetch modules/providers, contact
backends, load provider schemas, read state/plan semantics, evaluate interpolation
or policy, or prove runtime correctness. State, plans, caches, lock mechanics,
generated/vendor bodies, exact duplicates, and raw Terraform line volume do not
inflate semantic effort. See the
[static Terraform and HCL boundary](docs/TERRAFORM_HCL_ANALYSIS.md).
PHP/Composer analysis reads only scanner-admitted, digest-matched static text and
strict JSON. It does not run PHP, Composer, autoloaders, package scripts, framework
bootstraps, containers, routes, reflection, dependency resolution, or tests.
Dynamic includes, magic methods, runtime registration, and linked frontend assets
remain explicit uncertainty. See the
[static PHP and Composer boundary](docs/PHP_COMPOSER_ANALYSIS.md).
Rust/Cargo analysis reads only scanner-admitted, digest-matched static text. It
does not run Cargo, rustc, build scripts, procedural macros, generators, tests,
examples, or benchmarks; resolve dependencies, features, or target triples; or
infer macro-expanded and generated bodies. See the
[static Rust and Cargo boundary](docs/RUST_CARGO_ANALYSIS.md).
Docker/Compose analysis reads only scanner-admitted, digest-matched Dockerfile
variants, filename-qualified Compose YAML, and `.dockerignore`. It does not invoke
Docker, Compose, BuildKit, a shell, or target code; pull or inspect images; expand
build contexts; load includes/environment files; resolve interpolation or
secrets; or treat arbitrary YAML as Compose. See the
[static Docker and Compose boundary](docs/DOCKER_ANALYSIS.md).

## Offline and safe by default

For normal `scan` and `estimate` commands, EffortHours:

- does not execute target code or build scripts;
- does not install dependencies or invoke package managers;
- does not access the network;
- does not inspect Git history, contributors, churn, or timestamps;
- does not write into the target repository;
- does not follow links outside the selected scope; and
- does not send repository content to an AI provider.

Source trees are treated as untrusted input. Analysis is bounded and
deterministic, structured data stays on stdout, and diagnostics stay on stderr.
An optional external scan cache can speed repeated scans; it is never an effort
signal.

## Optional host-AI review

The local estimate is complete without AI. If a surrounding AI session is useful,
`eh review packet` produces a compact, rate-free, provider-neutral uncertainty
packet. Follow-up queries can request bounded evidence or explicitly admitted
source windows, and `review validate` checks an adjustment ledger without applying
it.

EffortHours does not choose or call a provider. The caller controls disclosure,
privacy, retention, model choice, and cost. No automatic review budget is selected
in this alpha. See the [host-review protocol](docs/MILESTONE_8.md).

## Important limitations

- The bundled estimators are experimental. The repository model remains
  uncalibrated; Change has only first-band logical calibration, not empirical
  production validation. Ranges are planning bounds, not formal confidence
  intervals.
- Static analysis assumes discovered tests pass on the fastest path and does not
  prove that a checkout builds or runs.
- Unsupported languages still receive common inventory evidence but not the same
  semantic depth as .NET, JavaScript/TypeScript, Python/Jupyter, Go, Java/Kotlin,
  Shell/PowerShell, Terraform/HCL, PHP/Composer, Rust/Cargo, Docker/Compose, SQL, and the supported
  frontend forms.
- Checked-in coverage can be stale even when its bytes match the analyzed tree;
  EffortHours does not regenerate it.
- Change portfolio reconciliation is experimental. Exact patch/object-chain
  normalization does not recover general rebases, squashes, semantic clones, or
  shared human credit.
- A repository estimate is not a security, accessibility, architecture, or code-
  quality audit.

The [product charter](docs/PRODUCT.md) and
[estimation model](docs/ESTIMATION_MODEL.md) define the precise semantics.

## Common commands

```text
eh scan <repository> [--output evidence.json]
eh estimate <repository-or-evidence.json> [--profile implementation|recreation]
eh report <estimate.json> [--view review|category|scope|work-item]
eh explain <repository-or-evidence.json> --item <id>
eh change <repository> --base <revision> --head <revision>
eh change <repository> --commit <revision> [--parent <revision>]
eh change <repository> --range <base>..<head>
eh change <repository> --pr <number-or-url> [--repo <owner/name>]
eh change portfolio <repository> --pr <pr> --pr <pr>
eh change portfolio --manifest <portfolio.json>
eh change portfolio <repository> --author <alias> --since <instant> --until <instant>
eh change explain <change-estimate.json> --item <id>
eh model info
eh rate info
eh schema list
```

Run `eh --help` or a subcommand's help for the complete option surface. Calibration
and review researchers can start from the [documentation index](docs/README.md).

## Build and contribute

The repository uses the .NET 10 SDK selected by `global.json`.

```text
dotnet restore EffortHours.slnx --configfile NuGet.Config --force-evaluate
dotnet build EffortHours.slnx --no-restore --configuration Release
dotnet test tests/EffortHours.Tests/EffortHours.Tests.csproj --no-build --no-restore --configuration Release
dotnet test tests/EffortHours.EndToEndTests/EffortHours.EndToEndTests.csproj --no-build --no-restore --configuration Release
```

Read [CONTRIBUTING.md](CONTRIBUTING.md) before opening a change. Security reports
should follow [SECURITY.md](SECURITY.md). Package maintainers should use the exact
[release procedure](docs/RELEASING.md).

EffortHours is licensed under the [MIT License](LICENSE).
