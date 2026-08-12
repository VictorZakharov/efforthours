# EffortHours Product Charter

## Product identity

The product is **EffortHours**, maintained under the **WellScoped** publishing
identity. Its primary distribution and command identities are:

- NuGet package: `EffortHours.Tool`;
- installed command: `eh`;
- repository and durable file/URI stem: `efforthours`; and
- primary metric: Equivalent Human Effort (EHE).

The deliberately short `eh` command is the product's Canadian mnemonic as well as
an abbreviation of Equivalent Human. Documentation uses the full EffortHours name
for discovery and `eh` for executable examples. Because a two-letter executable
can collide with unrelated local tools, installation guidance must always identify
the `EffortHours.Tool` package explicitly.

## Problem

A capable AI can often form a reasonable software-effort estimate by inspecting a
repository and logically decomposing it. On a large repository, however, giving a
strong remote model enough source context can take a long time and cost hundreds
of dollars per estimate.

EffortHours should compress the repository into the facts and small work units that
matter for estimation. Its normal path must be fast, local, explainable, and much
less expensive than asking a model to read the complete source tree.

## Core metric

**Equivalent Human Effort (EHE)** is the estimated number of human-hours required
for one competent senior contractor to recreate the current repository from a
clear specification under the selected estimation profile.

The modeled contractor:

- is technically competent and productive;
- is familiar with the relevant software ecosystem;
- is initially unfamiliar with the product's business domain;
- works alone, so the result measures human-hours rather than team elapsed time;
- uses ordinary 2026 development tools, documentation, search, templates,
  generators, and third-party libraries; and
- does not use AI while performing the modeled work.

The last point applies to the counterfactual worker. EffortHours itself may use local
ML, and the AI session orchestrating EffortHours may use any tools available to it.

## Recreation target

The primary target is functional and quality equivalence, not line-for-line source
reproduction. The recreated system should provide the same meaningful behavior,
interfaces, integrations, tests, documentation, infrastructure, and observable
quality represented by the repository.

A clean and competent implementation is assumed. Duplicate code, dead code,
accidental complexity, abandoned approaches, and repeated historical rework must
not increase EHE merely because they increase repository size.

Meaningful constraints remain in scope. Compatibility requirements, protocols,
framework choices, data formats, and externally visible behavior cannot be erased
just because a different greenfield design might be easier.

Recreation assumes a sensible modern 2026-equivalent implementation. It is not a
legacy-restoration exercise. Compatibility behavior and externally meaningful
constraints remain, while obsolete implementation mechanics need not be reproduced
when current technology provides an equivalent result.

## Estimation profiles

EffortHours will support two profiles.

### Implementation profile

The contractor receives detailed requirements, acceptance criteria, UI designs,
API contracts, and other implementation inputs that exist for the product. The
estimate includes technical design and implementation decisions but excludes
product discovery and creation of supplied design artifacts.

### Recreation profile

The contractor receives a clear behavioral specification but must recover or make
more of the architecture, data-model, interface, and UX decisions needed to
recreate the artifact. It still excludes open-ended stakeholder discovery unless a
future profile explicitly adds it.

Both profiles should be reportable from the same repository evidence. A supplied
specification is optional; without one, EffortHours infers the product surface and
reports lower confidence where appropriate.

## Goals

- Estimate the current repository state without using Git history or inferred
  historical churn.
- Produce low, expected, and high hours with an explicit confidence assessment.
- Break effort down by category and small, inspectable work items.
- Attach repository evidence and reasoning to every material estimate.
- Reflect the actual level of automated tests and documentation present.
- Separate represented effort from a professionalization or remediation gap.
- Supply a reasonable, dated 2026 US senior-contractor rate and allow overrides.
- Work without network access or remote AI by default.
- Give AI agents compact output designed for inexpensive final adjudication.
- Mark coverage and other claims as measured, declared-and-assumed, or inferred.
- Scale from small repositories to large mixed-language repositories.
- Support .NET and JavaScript/TypeScript first, then become polyglot through
  analyzer extensions.
- Estimate the Equivalent Human Effort represented by a completed base-to-head
  change, one commit, a revision range, or one GitHub pull request without treating
  commit activity or elapsed history as effort evidence.
- Be suitable for public open-source development and redistribution.

The implemented JavaScript/frontend boundary includes parser-backed JavaScript/
JSX, token-backed TypeScript/TSX, explicit JSX/Vue/Svelte structure, conservative
static Angular component metadata, bounded HTML/template semantics, and bounded
CSS/SCSS/Sass/Less semantics. Angular components require a named `Component`
import from `@angular/core` (a local alias is accepted), and static metadata values
must be literals or arrays; external assets must resolve inside the scanner-
admitted repository scope. This evidence does not claim rendering, framework
compilation, preprocessor execution, runtime reachability, visual correctness, or
accessibility conformance.

The implemented SQL boundary admits scanner-owned `.sql` files to a bounded,
comment/string/quoted-identifier-aware token analyzer. It records schema,
migration, stored-program, query, test, delivery, and explicit cross-database
evidence with separate parser and dialect confidence for common PostgreSQL, SQL
Server, MySQL/MariaDB, and SQLite syntax. It does not choose or connect to a
database, execute SQL, prove semantic validity or performance, or value dump/row/
timestamp volume. Supported semantics map to existing category priors and remain
experimental; [SQL_ANALYSIS.md](SQL_ANALYSIS.md) defines the exact boundary.

The first implemented polyglot expansion is Python 3. Scanner-admitted `.py` and
`.pyi` files receive bounded managed tokenization and indentation-aware structure,
static package ownership, local import edges, test evidence, and conservative
import-qualified framework evidence. Static metadata includes `pyproject.toml`,
`setup.cfg`, literal-only `setup.py`, requirements, Pipfile, and common Poetry,
PDM, and uv surfaces. The analyzer never invokes Python, imports modules, resolves
an environment, installs dependencies, or executes `setup.py`; notebooks remain a
separate safety boundary. `PYTHON_ANALYSIS.md` defines the exact scope. The
language-neutral package/test contracts and source-backbone routing are intended
to keep later ecosystem extensions additive rather than one-off estimator forks.

The second polyglot expansion is Go. Scanner-admitted `go.mod`, `go.work`, and
`.go` files receive static module/workspace/package ownership, local replacement
and internal-reference edges, bounded token structure, `_test.go` evidence, and
conservative import-qualified framework semantics. The analyzer records build
constraints, platform filenames, `go:embed`, `go:generate`, cgo, and blank-import
registration as explicit static evidence or uncertainty; it never invokes the Go
toolchain, resolves target selection, expands assets, runs generators, compiles
native code, loads plugins, or proves runtime registration. `GO_ANALYSIS.md`
defines the exact scope. Go reuses the existing language-neutral source backbone
and analogous specialized priors without a fitted Go-specific rate.

The third polyglot expansion is Java. Scanner-admitted `.java` files receive
bounded token-backed package/module/type/method/public-API/concurrency structure,
test evidence, and conservative import- and annotation-qualified framework
semantics. Scanner-admitted Maven POM/reactor and Gradle multi-project descriptors
receive bounded static ownership and local-edge analysis with unresolved dynamic
values disclosed explicitly. The analyzer never invokes a JVM, compiler, Maven,
Gradle, wrapper, annotation processor, test runner, dependency resolver, or target
code; `JAVA_ANALYSIS.md` defines the exact scope. Java reuses the unchanged
language-neutral source backbone and analogous specialized priors without a fitted
Java-specific rate.

The fourth polyglot expansion is Kotlin/JVM. Scanner-admitted maintained `.kt`
and non-Gradle `.kts` files receive bounded token-backed package/type/function/
extension/public-API/nullability/coroutine/Flow structure, test evidence, and
conservative import-qualified server, Android/Compose, persistence, integration,
security, validation, and background semantics. Kotlin reuses static Maven/Gradle
JVM module ownership and avoids duplicate project/build evidence in mixed
Java/Kotlin modules; Gradle Kotlin DSL remains build configuration rather than
product script effort. The analyzer never invokes a JVM, Kotlin compiler, Maven,
Gradle, Android tooling, KSP, kapt, compiler plugins, test runner, dependency
resolver, or target code; `KOTLIN_ANALYSIS.md` defines the exact scope. Kotlin
reuses the unchanged language-neutral source backbone and analogous specialized
priors without a fitted Kotlin-specific rate.

The fifth polyglot expansion is maintained Shell and PowerShell. Scanner-admitted
POSIX-family shell/Bash and PowerShell scripts receive bounded token-backed
function, parameter, branch, loop, pipeline, error-handling, command, file,
network, process, module, test, and credential-surface evidence. Common path facts
and bounded exact references from manifests and automation files separate product
commands and reusable modules from test, build, CI, delivery, and infrastructure
roles. The analyzer never starts a shell, resolves commands or modules, sources
files, evaluates expansions, accesses the network, observes platform effects, or
emits source values or excerpts; `SHELL_POWERSHELL_ANALYSIS.md` defines the exact
scope. Shell and PowerShell reuse the unchanged language-neutral source backbone
and analogous specialized priors without a fitted script-specific rate.

The sixth polyglot expansion is Terraform and relevant HCL. Scanner-admitted
maintained `.tf`, `.tfvars`, Terraform tests, CLI configuration, and HCL receive
bounded comment/string/heredoc-aware token and structural analysis. Terraform
evidence separates resources, data sources, modules, inputs/outputs/locals,
providers/backends, lifecycle/dependency/expression structure, integrations,
security-sensitive configuration, validation, tests, documentation, and delivery.
Only literal repository-local module ownership is resolved; external or dynamic
sources remain classified unresolved boundaries. The analyzer never invokes
Terraform or related tools, loads schemas, fetches providers/modules, contacts a
backend, reads plan/state semantics, evaluates interpolation/policy, or emits
configured values/source excerpts; `TERRAFORM_HCL_ANALYSIS.md` defines the exact
scope. Terraform reuses unchanged existing priors without a fitted ecosystem-
specific rate.

The seventh polyglot expansion is PHP and Composer. Scanner-admitted maintained
`.php` files, including Blade templates and tests, receive bounded token-backed
namespace/import/declaration/public-API/control-flow structure, template evidence,
and conservative import-qualified framework semantics. Strict static
`composer.json` analysis supplies package ownership, dependencies, autoload
mappings, scripts, binary entry points, and literal repository-local path edges.
The analyzer never invokes PHP, Composer, autoloaders, package scripts, framework
bootstraps, containers, routes, reflection, dependency resolution, tests, or target
code; `PHP_COMPOSER_ANALYSIS.md` defines the exact scope. PHP reuses the unchanged
language-neutral source backbone and analogous specialized priors without a fitted
PHP-specific rate.

The eighth polyglot expansion is Rust and Cargo. Scanner-admitted maintained `.rs`
files, tests, benchmarks, examples, and build scripts receive bounded token-backed
module/use/declaration/public-API/generic/lifetime/async/unsafe/error structure,
test evidence, FFI boundaries, and conservative import-qualified semantics.
Static `Cargo.toml` analysis supplies package and workspace ownership,
dependencies, features, build scripts, conventional and explicit targets, and
literal repository-local edges. The analyzer never invokes Cargo, rustc, rustdoc,
rustfmt, Clippy, build scripts, procedural macros, generators, tests, examples,
benchmarks, or target code; `RUST_CARGO_ANALYSIS.md` defines the exact scope. Rust
reuses the unchanged language-neutral source backbone and analogous specialized
priors without a fitted Rust-specific rate.

The ninth ecosystem expansion is Docker build and local orchestration
configuration. Scanner-admitted Dockerfile variants receive bounded logical-
instruction, stage, build, runtime, health, mount, and unresolved-boundary
analysis. Filename-qualified Compose YAML receives bounded service, build, image,
command, port, environment, storage, network, dependency, health, profile,
secret/config, deploy, security, extension, include, and dynamic-YAML structure;
literal repository-contained builds can reference admitted Dockerfiles.
`.dockerignore` receives bounded rule inventory. Arbitrary YAML is not Compose.
The analyzer never invokes Docker, Compose, BuildKit, a shell, container runtime,
or target code; pulls images; expands build contexts; loads includes/environment
files; resolves interpolation/secrets; or emits configured values/source excerpts.
`DOCKER_ANALYSIS.md` defines the exact scope. Docker reuses the unchanged existing
container prior without a fitted ecosystem-specific rate.

## Non-goals

- Reconstructing actual hours worked.
- Treating commit counts, author activity, file timestamps, or code churn as labor
  evidence.
- Rewarding verbosity, generated code, copied code, or poor architecture.
- Predicting schedules for a multi-person team.
- Performing full project management, staffing, or delivery-date forecasting.
- Claiming that an estimate is an invoice or a record of historical labor.
- Requiring source code to be uploaded to an external service.
- Replacing a security audit, accessibility audit, legal review, or accounting
  opinion.

## Primary outputs

A completed estimate should contain:

- repository identity and analyzed scope;
- estimator, schema, model, and rate versions;
- estimation profile and assumptions;
- low, expected, and high EHE totals;
- equivalent replacement-cost totals;
- category breakdowns;
- a repository-first work-item ledger;
- evidence references for each work item;
- confidence and unresolved uncertainties;
- detected generated, vendored, excluded, or unsupported content;
- represented test and documentation effort;
- a separate professionalization gap; and
- warnings about incomplete, unbuildable, or out-of-distribution repositories.

Feature-oriented reporting is intentionally deferred until repository-level
analysis is trustworthy.

The experimental incremental-change mode estimates the functional and quality
delta for two statically scanned directories, two saved repository-evidence
bundles, immutable Git base/head snapshots, one commit, a revision range, or one
GitHub pull request. The optional `gh` adapter resolves only the PR number or URL
and immutable base/head object IDs; analysis then remains local. Evidence-only
modified maintained paths that otherwise qualify as represented are retained
conservatively because saved evidence has no source bodies. The explicit portfolio
command can combine repeated PRs, schema-valid multi-repository PR manifests, or
bounded commits selected by exact author/co-author alias and time interval. It
normalizes each repository independently, exposes isolated rows and exact
allocations, and labels the result repository-attributed Change EHE. Commit count,
activity, timestamps, review duration, identity, and discarded intermediate
revisions remain excluded as effort signals; portfolio identity/time values select
rows only. The result is still EHE, not actual hours worked, proof of sole
authorship, or a standalone measure of an employee's performance. Change
calibration advances through explicit size
bands, beginning with 4-to-32-hour final deltas decomposed into small,
evidence-backed tasks. Model-authored logical labels and later production
observations retain separate provenance; neither logged time nor AI review cost is
an effort multiplier. See `CHANGE_ESTIMATION.md`.

## Product principles

### Open-source from the start

The repository is maintained as public open source. Dependencies, model files,
calibration data, fixtures, and copied assets need clear provenance and
redistribution terms compatible with the MIT License. Client
repositories, proprietary source, credentials, and private estimation inputs must
never become project fixtures or committed calibration data.

### Evidence before inference

Objective facts must remain distinguishable from inferred work and estimated
hours. Reports must make that boundary visible.

### Small estimates compose better

EffortHours should prefer work items that normally represent roughly 0.5 to 8 hours.
A large item should be decomposed further or explicitly explain why it cannot be.
For a logical calibration or admission gate, a disclosed host-AI teacher's total
must reconcile exactly from distinct evidence-backed tasks small enough to audit;
the current one-to-several-day band normally uses 0.5-to-1.5-hour tasks.
Independent replication is optional corroboration and does not silently upgrade
`teacher-estimate` maturity. Later production observations retain separate
empirical provenance and never become effort multipliers.

### Current state, not historical struggle

If a feature was rewritten ten times, EffortHours estimates a competent recreation of
the current result once.

### Artifact value is not artifact volume

Lines of code may be an input signal but never the principal value measure.
Behavior, complexity, quality, constraints, and supporting artifacts matter more.

### Offline first

The core CLI path should be deterministic and local. The host AI session can use
the evidence and any other tools it has, but EffortHours should not require an embedded
AI provider or full-source model ingestion.

### Honest uncertainty

Unknowns should widen ranges or be surfaced for review. They must not be silently
converted into false precision.
