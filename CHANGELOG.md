# Changelog

Significant user-visible EffortHours changes are recorded here. The project follows
Semantic Versioning once a package version has been released; prerelease versions
may still change public contracts with explicit documentation.

## Unreleased

### Added

- Rust analyzer `0.1.0` adds offline, digest-verified bounded analysis for Cargo
  packages and virtual workspaces, dependencies, features, build scripts, local
  edges, explicit and conventional targets, maintained `.rs` source, tests,
  benchmarks, and examples. It records token-backed declarations, public APIs,
  generics, lifetimes, async/concurrency, unsafe/error paths, FFI, and import-
  qualified semantics without invoking Cargo, rustc, build scripts, procedural
  macros, generators, tests, examples, or benchmarks, and emits no source excerpts.
- A standalone public Rust mutation suite adds 14 project-authored states and 62
  passing relations for formatting, exact-copy and conventional-exclusion
  behavior, workspace ownership, semantic/category directionality, FFI/build
  uncertainty, tests, and crate-namesake rejection.
- Scanner benchmark mode `rust-cargo-static` records a fresh-process million-line
  checkpoint with target metadata unchanged and no target execution, dependency
  installation, or network access.
- PHP analyzer `0.1.0` adds offline, digest-verified bounded analysis for Composer
  packages, dependencies, autoload mappings, scripts, binary entry points, literal
  repository-local path repositories, maintained `.php` source and tests, and PHP/
  Blade templates. It records token-backed declarations, public APIs, attributes,
  branches, exceptions, and import-qualified framework semantics without invoking
  PHP, Composer, autoloaders, package scripts, framework bootstraps, containers,
  routes, reflection, dependency resolution, or tests, and emits no source excerpts.
- A standalone public PHP mutation suite adds 14 project-authored states and 59
  passing relations for formatting, exact-copy and conventional-exclusion
  behavior, package ownership, semantic/category directionality, templates, tests,
  and framework-namesake rejection.
- Scanner benchmark mode `php-composer-static` records a fresh-process million-line
  checkpoint with target metadata unchanged and no target execution, dependency
  installation, or network access.
- Terraform/HCL analyzer `0.1.0` adds offline, digest-verified bounded analysis
  for maintained `.tf`, `.tfvars`, Terraform tests, CLI configuration, and
  relevant HCL. It records resources, data sources, modules, interfaces, locals,
  providers, backends, lifecycle/dependency/expression structure, tests,
  security-sensitive configuration, documentation, and delivery evidence while
  resolving repository-local module ownership only. It never runs Terraform,
  loads providers/schemas, fetches modules, contacts backends, evaluates plans,
  interpolation, or policy, or emits configured values/source excerpts.
- A standalone public Terraform mutation suite adds 14 project-authored states
  and 48 passing relations for formatting, exact-copy, state/cache/lock/generated
  exclusion, semantic/category directionality, repeated-resource marginality,
  local/external modules, and generic-HCL conservatism.
- Scanner benchmark mode `terraform-hcl-static` records a fresh-process million-
  line checkpoint with target metadata unchanged and no target execution,
  dependency installation, or network access.
- Scripting analyzer `0.1.0` adds offline, digest-verified token analysis for
  maintained POSIX-family Shell/Bash and PowerShell product scripts, reusable
  modules, tests, and build/CI/delivery/infrastructure automation. It records
  bounded command structure, file/network/process boundaries, security surfaces,
  validation, and dynamic uncertainty without starting a shell, resolving
  commands/modules, sourcing content, evaluating expansions, or emitting source
  values/excerpts.
- A standalone public scripting mutation suite adds 13 project-authored states
  and 46 passing relations for formatting, exact-copy/generated/copied-launcher
  invariance, category directionality/isolation, and remote-command namesake
  rejection.
- Scanner benchmark modes `shell-static` and `powershell-static` record fresh-
  process million-line checkpoints with target metadata unchanged and no target
  execution, dependency installation, or network access.
- Kotlin analyzer `0.1.0` adds offline, digest-verified analysis for maintained
  `.kt` and non-Gradle `.kts` files; shared Maven/Gradle JVM project ownership;
  package/type/function/public-API/nullability/coroutine structure; Kotlin tests;
  and import-qualified server, Android/Compose, data, integration, security,
  validation, and background-work evidence. It never invokes a JVM, Kotlin
  compiler, Gradle, Android tooling, KSP, kapt, or tests and emits no source
  excerpts.
- A standalone public Kotlin mutation suite adds 14 project-authored states and 63
  passing relations for formatting, exact-copy and generated invariance,
  semantic/category directionality, build/coroutine/Android evidence, and
  framework-namesake rejection.
- Scanner benchmark mode `kotlin-static` records a fresh-process million-line
  Kotlin checkpoint with target metadata unchanged and no target execution,
  dependency installation, or network access.
- Java analyzer `0.1.0` adds offline, digest-verified analysis for maintained
  `.java` files; static Maven POM/reactor and conservative Gradle multi-project
  discovery; package/module/type/method/public-API/concurrency structure; JUnit/
  TestNG tests; and import-qualified Spring/Jakarta, data, integration, security,
  messaging, scheduling, validation, and CLI evidence. It never invokes a JVM,
  Maven, Gradle, wrappers, compilers, annotation processors, or tests and emits no
  source excerpts.
- A standalone public Java mutation suite adds 13 project-authored states and 56
  passing relations for formatting, exact-copy and generated invariance,
  semantic/category directionality, static build/concurrency evidence, and
  framework-namesake rejection.
- Scanner benchmark mode `java-static` records a fresh-process million-line Java
  checkpoint with target metadata unchanged and no target execution, dependency
  installation, or network access.
- Go analyzer `0.1.0` adds offline, digest-verified analysis for `go.mod`,
  `go.work`, and maintained `.go` files; module/workspace/package ownership; local
  replacements and references; token-backed structure and concurrency; tests;
  and conservative import-qualified API, CLI, data, integration, security,
  validation, and background-work evidence. It never invokes the Go toolchain or
  emits source excerpts.
- A standalone public Go mutation suite adds 13 project-authored states and 56
  passing relations for formatting, exact-copy and generated invariance,
  semantic/category directionality, build/concurrency evidence, and framework-
  namesake rejection.
- Scanner benchmark mode `go-static` records a fresh-process million-line Go
  checkpoint with target metadata unchanged and no target execution, dependency
  installation, or network access.
- Python analyzer `0.1.0` adds offline, digest-verified analysis for `.py` and
  `.pyi` files; static package metadata; token- and indentation-backed source
  structure; tests; local package edges; and conservative import-qualified API,
  CLI, persistence, integration, security, validation, and background-work
  evidence. It never invokes Python, imports modules, installs dependencies,
  executes `setup.py`, or emits source excerpts.
- Public synthetic mutation suite `0.8.0` adds 11 Python states for formatting,
  exact-copy and generated invariance, semantic directionality, category
  isolation, and framework-namesake rejection. All 339 relations pass across 88
  cases; the earlier 77 candidate reports remain frozen.
- Language evidence now distinguishes semantically analyzed source from
  inventory-only maintained languages and reports the active analysis depth.

- Experimental Change portfolios add repeated local-repository PR selection, a
  versioned cross-repository PR manifest, and bounded author-period commit
  selection with explicit identity, timezone, date-field, merge, co-author, and
  interval policies. Reports separate isolated and normalized EHE, expose immutable
  base contexts and signed duplicate/overlap/revert/shared-context adjustments,
  and allocate normalized expected effort exactly without producing employee
  rankings or treating identity/time as effort signals.

- SQL analyzer `0.1.0` adds offline bounded evidence for `.sql` schemas,
  migrations, indexes, constraints, stored programs, queries, test fixtures,
  deployment scripts, and explicit cross-database boundaries. Dialect and parser
  confidence are separate; PostgreSQL, SQL Server, MySQL/MariaDB, and SQLite
  signals are conservative, and EffortHours never connects to or executes a
  database.
- Public synthetic mutation suite `0.6.0` adds 11 SQL states for formatting,
  exact-copy, generated-dump, unknown-syntax, semantic, test, delivery,
  cross-database, and bounded seed-volume behavior. All 247 relations pass across
  67 cases; the 56 earlier candidate reports remain frozen.
- JavaScript analyzer `0.5.0` adds bounded semantic HTML/template and CSS, SCSS,
  Sass, and Less evidence, plus conservative static Angular `@Component`
  metadata when a named `Component` import from `@angular/core` is present, with
  inline and repository-relative external asset ownership. It does not execute
  TypeScript/configuration, render, compile frameworks, run preprocessors, or
  emit source excerpts.
- Public synthetic mutation suite `0.5.0` adds standalone frontend formatting,
  exact-copy, semantic-behavior, and Angular component cases. All 192 relations
  pass across 56 cases, and all 51 prior cases retain identical numeric ranges.
- Change estimation can represent exact, balanced, EffortHours-specific
  `<custom-code>` regions inside otherwise generated UTF-8 files while continuing
  to exclude the generated body. Additions, modifications, and removals are
  supported for source-readable snapshots; malformed, oversized, and bodyless
  cases fail closed.

### Changed

- Common scanner `0.2.10` classifies Cargo manifests and maintained Rust source,
  tests, benchmarks, build scripts, and configuration while excluding conventional
  target/vendor/generated bodies, lock mechanics, and exact duplicates from
  semantic effort. The unchanged experimental `seed-rules/0.4.0` language-neutral
  backbone and existing specialized priors consume bounded Rust evidence; no
  fitted Rust rate or calibration claim was added.
- Change source identity advances to
  `change-seed/0.15.0+seed-rules/0.4.0` for conservative Rust-aware formatting and
  analyzer-backed semantic/category routing. The admitted Stage A boundary remains
  `change-seed/0.6.0`; Rust and Cargo are not included in that admission.
- Common scanner `0.2.9` classifies Composer manifests and maintained PHP source,
  templates, and tests while excluding conventional vendor/cache/generated bodies
  and exact duplicates from semantic effort. The unchanged experimental
  `seed-rules/0.4.0` language-neutral backbone and existing specialized priors
  consume bounded PHP evidence; no fitted PHP rate or calibration claim was added.
- Change source identity advances to
  `change-seed/0.14.0+seed-rules/0.4.0` for conservative PHP-aware formatting and
  analyzer-backed semantic/category routing. The admitted Stage A boundary remains
  `change-seed/0.6.0`; PHP and Composer are not included in that admission.
- Common scanner `0.2.8` classifies maintained Terraform/HCL source, values,
  tests, CLI configuration, and inventory-only Terraform JSON while excluding
  state, plans, `.terraform/` caches, lock mechanics, generated/vendor bodies,
  and exact duplicates from semantic effort. The unchanged experimental
  `seed-rules/0.4.0` maps bounded Terraform semantic units through existing
  infrastructure and quality priors; no fitted Terraform rate or calibration
  claim was added.
- Change source identity advances to
  `change-seed/0.13.0+seed-rules/0.4.0` for conservative HCL-aware formatting and
  Terraform semantic/category routing. The admitted Stage A boundary remains
  `change-seed/0.6.0`; Terraform and HCL are not included in that admission.
- Common scanner `0.2.7` classifies maintained Shell and PowerShell sources,
  modules, tests, build, CI, delivery, and infrastructure roles while excluding
  conventional completions and copied launchers/installers. The unchanged
  experimental `seed-rules/0.4.0` language-neutral backbone and existing
  specialized priors consume scripting evidence; no fitted scripting rate or
  calibration claim was added.
- Change source identity advances to
  `change-seed/0.12.0+seed-rules/0.4.0` for conservative Shell/PowerShell
  formatting and analyzer-backed semantic/category routing. The admitted Stage A
  boundary remains `change-seed/0.6.0`; Shell and PowerShell are not included in
  that admission.
- Common scanner `0.2.6` classifies Kotlin source, scripts, tests, and Gradle Kotlin
  DSL configuration. The unchanged experimental `seed-rules/0.4.0`
  `polyglot-source-backbone` and existing specialized priors consume Kotlin
  evidence; no fitted Kotlin-specific rate or calibration claim was added.
- Change source identity advances to
  `change-seed/0.11.0+seed-rules/0.4.0` for Kotlin-aware formatting,
  documentation-comment/literal/operator preservation, semantic-newline handling,
  and semantic-category routing. The admitted Stage A boundary remains
  `change-seed/0.6.0`; Kotlin is not included in that admission.
- Common scanner `0.2.5` classifies Java sources/tests plus Maven and Gradle build
  artifacts. The unchanged experimental `seed-rules/0.4.0`
  `polyglot-source-backbone` and existing specialized priors consume Java evidence;
  no fitted Java-specific rate or calibration claim was added.
- Change source identity advances to
  `change-seed/0.10.0+seed-rules/0.4.0` for Java-aware formatting, documentation-
  comment/literal/operator preservation, and semantic-category routing. The
  admitted Stage A boundary remains `change-seed/0.6.0`; Java is not included in
  that admission.
- Common scanner `0.2.4` classifies Go modules, workspaces, lockfiles, and
  `_test.go` files. The unchanged experimental `seed-rules/0.4.0`
  `polyglot-source-backbone` and existing specialized priors now consume Go
  evidence; no fitted Go-specific rate or calibration claim was added.
- Change source identity advances to
  `change-seed/0.9.0+seed-rules/0.4.0` for Go-aware formatting, implicit-semicolon,
  compiler-directive, cgo, and semantic-category routing. The admitted Stage A
  boundary remains `change-seed/0.6.0`; Go is not included in that admission.
- The bundled repository model advances to experimental `seed-rules/0.4.0` with
  one language-neutral source backbone for Python. Its transparent rates reuse
  analogous `0.3.0` construction priors with wider uncertainty; all existing
  rules remain unchanged and no fitted calibration was added.
- Change source identity advances to
  `change-seed/0.8.0+seed-rules/0.4.0` for indentation-aware Python formatting
  normalization and evidence routing. The admitted Stage A boundary remains
  `change-seed/0.6.0`; neither SQL nor Python is included in that admission.
- Existing `seed-rules/0.3.0` data, integration, testing, and packaging priors now
  consume supported SQL evidence without a fitted SQL-specific rate. Repeated seed
  rows, exact copies, dumps, and formatting do not inflate any range point.
- Change source identity advances to
  `change-seed/0.7.0+seed-rules/0.3.0` for token-aware SQL formatting and semantic
  role routing. Existing Change priors and frozen reports are unchanged; SQL was
  not part of the earlier 0.6.0 Stage A admission.
- The bundled repository model advances to experimental `seed-rules/0.3.0`.
  UI asset line volume is no longer priced; bounded template structure/binding,
  stylesheet structure, responsive, design-token, animation, and theme units
  replace it. Every non-UI numerical prior remains unchanged. The composite
  source Change identity advances to `change-seed/0.4.0+seed-rules/0.3.0`
  without changing Change rules or frozen reports.
- The source Change estimator advances to experimental `change-seed/0.4.0` without
  changing repository priors, public schemas, calibration labels, or frozen
  reports. Represented, unchanged, formatting-only, and ambiguous customization
  outcomes carry explicit trace tags; represented and ambiguous paths add
  diagnostics.
- Change path normalization now preserves vendored, minified, binary, lockfile,
  build-output, and exact-copy precedence even when the repository scanner omits
  those files from emitted evidence.
- The root README now leads with installation, first-use workflows, interpretation,
  privacy, supported ecosystems, and limitations instead of milestone history.

## 0.9.0-alpha.3 - 2026-08-10

### Changed

- Change estimation advances to experimental `change-seed/0.3.0`. Repeated
  repository work-item partitions for one existing or modified capability now
  share a bounded evidence-derived logical marginal budget instead of contributing
  their summed repository prior. Distinct added capabilities remain additive.
- Logical modification and fallback budgets use capped edit-region bands per path
  rather than growing linearly with diff fragmentation. A capability newly
  detected on a modified artifact receives a meaningful modification floor.
- Separate five-family development/validation diagnostics record the correction
  without rewriting frozen alpha.2 reports or consulting the withheld test
  comparison. The teacher-only results remain weak supervision, not calibration or
  an accuracy claim.

## 0.9.0-alpha.2 - 2026-08-10

### Changed

- Change estimation advances to experimental `change-seed/0.2.0`. Existing
  capability modifications now require changed normalized non-file evidence,
  repeated category/path evidence shares one diminishing marginal budget, and
  final-delta comprehension, validation, and review are emitted once.
- Modified artifacts use 30% of the corresponding new-artifact edit-region rates;
  scope membership alone no longer assigns specialized UI or other boundary work.

### Fixed

- Passing a Change estimate to repository calibration authoring now points to
  `eh calibration change-scaffold` instead of printing unrelated repository-schema
  failures.

## 0.9.0-alpha.1 - 2026-08-09

### Added

- The product identity is EffortHours, distributed as
  `EffortHours.Tool` with the `eh` command. Projects, namespaces, schema URNs,
  repository metadata, cache/ignore conventions, and calibration identities were
  renamed together for the `0.9.0-alpha.1` candidate.
- Public-alpha governance, contribution templates, cross-platform CI, and release
  instructions.
- A manually dispatched NuGet preview workflow with local installation checks and
  short-lived OIDC credentials.
- Repository, commit, range, and single-pull-request Equivalent Human Effort
  estimation for .NET and JavaScript/TypeScript repositories.
- Versioned evidence, estimate, Change, calibration, reporting, and rate-card
  contracts with checked-in schemas.
- Preliminary public repository and synthetic Change calibration corpora, mutation
  guardrails, and blind independent-review handoffs.
- Provider-neutral `host-review/1.0.0` packets, digest-bound capability/evidence/
  scope/selected-source queries, adjustment ledgers, and non-applying validation.
  The local baseline remains complete and offline; no provider is embedded or
  selected.
- Sanitized `host-review-measurement/1.0.0` session records and
  `host-review-comparison-metrics/1.0.0` compact/broader-source benchmarks. The
  first three-repository public diagnostic reports payload and agreement evidence,
  explicitly withholds unavailable token/time/cost ratios, and selects no default
  review budget.
- Static, digest-verified LCOV and Cobertura coverage parsing with privacy-safe
  project/package scope mapping. Measured coverage is distinct from and takes
  precedence over a conflicting same-scope declared threshold; the public mutation
  baseline now has 51 cases and 170 passing relations.

### Changed

- Product, architecture, benchmark, calibration, and milestone records now live
  under an indexed `docs/` directory; standard GitHub community files remain at
  the repository root, and the release suite verifies relative Markdown links.
- `.NET` analyzer `0.3.2` no longer treats generic process-command execute calls as
  persistence without data context.
- JavaScript analyzer `0.4.1` no longer treats framework-neutral state/effect calls
  as UI or development benchmark hashbangs as product entry points. Checked-in
  frozen-corpus reevaluations disclose the resulting target-mapping changes; the
  `seed-rules/0.2.1` priors remain unchanged and uncalibrated.
- The CLI now handles the first Ctrl+C cooperatively, emits its cancellation
  diagnostic only on stderr, and returns exit code 130; a second Ctrl+C retains
  immediate termination.
- Change-EHE safeguards now include category-isolated migration, integration, CI,
  container-delivery, and simplification mutations plus pre-start and in-flight
  cancellation, without changing `change-seed/0.1.0`.
- Scanner benchmarks now support mixed generated trees and caller-supplied
  repositories, sample peak working set, distinguish explicit warm-cache passes,
  and verify a before/after target-tree metadata digest.
- The documented performance checkpoint now includes million-line .NET,
  JavaScript/TypeScript, and mixed measurements plus three exact MIT releases and
  the EffortHours development tree; no regression threshold is claimed from the
  single-workstation results.

### Known limitations

- `seed-rules/0.2.1` and `change-seed/0.1.0` are experimental and uncalibrated.
- No checked-in corpus has completed genuinely independent correction.
- Multiple pull requests and contributor-period portfolios are not implemented.
- TypeScript and TSX analysis is token-backed rather than compiler-backed.
- Measured coverage formats other than LCOV and Cobertura are inventoried but not
  parsed, and checked-in reports can be stale because EffortHours does not rerun
  tests on the default path.

No version in this file is a public release until a matching immutable Git tag and
package/release record exist.
