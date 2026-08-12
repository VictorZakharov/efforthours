# Public synthetic mutation baselines

## Status

These are deterministic qualitative guardrail checkpoints, not reviewed
effort-label corpora, accuracy claims, or model-training data.

- Suite `0.1.0` records the Milestone 7B2 .NET-only checkpoint: 8 cases and 14
  assertions evaluated with `seed-rules/0.2.0`.
- Suite `0.2.0` records the Milestone 7B3 aggregate checkpoint: 30 cases and 84
  assertions evaluated with `seed-rules/0.2.1`.
- Suite `0.3.0` records the Milestone 7B4 behavior-and-delivery checkpoint: 48
  cases and 156 assertions evaluated with the same `seed-rules/0.2.1` model.
- Suite `0.4.0` records the first measured-coverage checkpoint: 51 cases and 170
  assertions evaluated with the same `seed-rules/0.2.1` catalog.
- Suite `0.5.0` records the frontend semantic-evidence checkpoint: 56 cases and
  192 assertions evaluated with `seed-rules/0.3.0`.
- Suite `0.6.0` records the static SQL checkpoint: 67 cases and 247 assertions
  evaluated with the unchanged `seed-rules/0.3.0` model.
- Suite `0.7.0` records the Milestone 7B6 precision checkpoint: 77 cases and 309
  assertions evaluated with the unchanged `seed-rules/0.3.0` model.
- Suite `0.8.0` records the Python expansion checkpoint: 88 cases and 339
  assertions, preserving 77 frozen `seed-rules/0.3.0` candidates and adding 11
  `seed-rules/0.4.0` candidates.
- Standalone suite `go-0.1.0` records the Go expansion checkpoint: 13 cases and 56
  assertions evaluated with unchanged `seed-rules/0.4.0`.
- Standalone suite `java-0.1.0` records the Java expansion checkpoint: 13 cases
  and 56 assertions evaluated with unchanged `seed-rules/0.4.0`.
- Standalone suite `kotlin-0.1.0` records the Kotlin/JVM expansion checkpoint: 14
  cases and 63 assertions evaluated with unchanged `seed-rules/0.4.0`.
- Standalone suite `scripting-0.1.0` records the Shell and PowerShell expansion
  checkpoint: 13 cases and 46 assertions evaluated with unchanged
  `seed-rules/0.4.0`.
- Standalone suite `terraform-0.1.0` records the Terraform/HCL expansion
  checkpoint: 14 cases and 48 assertions evaluated with unchanged
  `seed-rules/0.4.0`.
- Standalone suite `php-0.1.0` records the PHP/Composer expansion checkpoint: 14
  cases and 59 assertions evaluated with unchanged `seed-rules/0.4.0`.
- Standalone suite `rust-0.1.0` records the Rust/Cargo expansion checkpoint: 14
  cases and 62 assertions evaluated with unchanged `seed-rules/0.4.0`.
- Standalone suite `docker-0.1.0` records the Docker/Compose expansion checkpoint:
  13 cases and 38 assertions evaluated with unchanged `seed-rules/0.4.0`.

The August 8, 2026 analyzer-precision reevaluation with `.NET` analyzer `0.3.2`
and JavaScript analyzer `0.4.1` reproduced identical low, expected, and high
repository/category values for all 48 candidates. The checked-in 0.3.0 report is
therefore unchanged and all 156 relations remain green.

The fixtures are small synthetic repositories authored for EffortHours and distributed
under the repository's MIT License. They contain no copied project source, external
dataset, installed/vendored third-party code, private evidence, or Git history.
Some manifests, the CI fixture, and the Dockerfile contain inert public package,
action, or image identifiers solely to exercise static classification. EffortHours
does not install or redistribute that software and analyzed every fixture without
building, running, restoring, pulling images, or accessing the network.

`seed-rules/0.2.1` retains every 0.2.0 numerical prior. It corrects TypeScript file
ownership in the shared JavaScript/TypeScript estimation scope so byte-identical
TypeScript bodies and TypeScript test structure participate in normalization. The
catalog digest is
`sha256:57378795593acd2ff0a2f4361698193a11dca86da11493f072da6a9f9b344d4e`.

`seed-rules/0.3.0` retains every non-UI 0.2.1 numerical prior. It removes
physical asset lines from the UI rule and adds bounded semantic template
structure/binding, stylesheet structure, responsive, design-token, and
animation/theme drivers. Its digest is
`sha256:e8bce2f76c97564919ab6be41f1cfd6b222d531a4dbd08a8b22c7abe6b1eebdf`.
Re-evaluating all 51 suite-0.4.0 states produces identical low, expected, and high
repository/category values; their frozen reports remain unchanged.

Suite 0.3.0 changes no seed-rule prior or estimator behavior. Suite 0.4.0 adds
coverage analyzer `0.1.0` and measured-over-declared evidence precedence while
reusing the unchanged `coverage-achievement` prior. Both suites were designed from
product invariants, then evaluated. Their bounds are qualitative guardrails and
must not be treated as reviewed numeric labels.

Suite 0.5.0 follows the same policy. Its five added states and 22 relations were
specified around frontend invariance, directionality, and category isolation, not
reviewed hour targets. Passing them does not calibrate the new UI rates.

Suite 0.6.0 adds 11 SQL states and 55 relations specified around formatting,
exact-copy, dump, unknown-syntax, seed-volume, semantic directionality, and role/
category isolation. It reuses existing data, integration, testing, and packaging
priors; passing does not calibrate SQL or add a reviewed label.

Suite 0.7.0 adds 10 states and 62 relations for bounded intra-file .NET
reachability, explicitly included conditional code, two specified non-exact
equivalent-purpose shapes, explicit static accessibility semantics,
accessibility-focused component-test depth, and representative JavaScript and
mixed dependency graphs. It reuses the existing source, UI, test, and combined
security/accessibility priors without fitting a rate. All 67 prior candidate
reports and all 247 prior assertions remain frozen and structurally unchanged.

Suite 0.8.0 adds 11 Python states and 30 relations specified around formatting,
exact-copy and generated invariance, semantic directionality, category isolation,
and framework-namesake rejection. It advances only the Python candidates to
`seed-rules/0.4.0`, whose digest is
`sha256:7cc0cd517ccf096470b98ef72993312263a4e60a2396967f5a07a1104a8c3a01`.
The earlier 77 candidates remain frozen. Passing this suite does not calibrate
Python or establish absolute-hour accuracy.

Standalone Go suite 0.1.0 adds 13 Go states and 56 relations specified around
formatting/comment, exact-copy, and generated invariance; semantic directionality;
category isolation; build/concurrency evidence; and framework-namesake rejection.
It reuses `seed-rules/0.4.0` without changing the model artifact or fitting a Go
rate. Passing this suite does not calibrate Go or establish absolute-hour accuracy.

Standalone Java suite 0.1.0 adds 13 Java states and 56 relations specified around
formatting/comment, exact-copy, and generated invariance; semantic directionality;
category isolation; build/concurrency evidence; and framework-namesake rejection.
It reuses `seed-rules/0.4.0` without changing the model artifact or fitting a Java
rate. Passing this suite does not calibrate Java or establish absolute-hour accuracy.

Standalone Kotlin suite 0.1.0 adds 14 Kotlin/JVM states and 63 relations specified
around formatting/comment, exact-copy, and generated invariance; semantic
directionality; category isolation; build, coroutine, and Android/Compose evidence;
and framework-namesake rejection. It reuses `seed-rules/0.4.0` without changing the
model artifact or fitting a Kotlin rate. Passing this suite does not calibrate
Kotlin or establish absolute-hour accuracy.

Standalone scripting suite 0.1.0 adds 13 Shell/PowerShell states and 46 relations
specified around formatting/comment, exact-copy, generated/completion and copied-
launcher invariance; test, integration, security, validation, build, CI, delivery,
and infrastructure directionality/category isolation; and local remote-command
namesake rejection. It reuses `seed-rules/0.4.0` without changing the model
artifact or fitting a scripting rate. Passing this suite does not calibrate Shell
or PowerShell or establish absolute-hour accuracy.

Standalone Rust suite 0.1.0 adds 14 Rust/Cargo states and 62 relations specified
around formatting/comment, exact-copy, and target/vendor/generated/lock exclusion;
workspace ownership; semantic directionality and category isolation; FFI/build
uncertainty; test/benchmark/example quality surfaces; and crate-namesake rejection.
It reuses `seed-rules/0.4.0` without changing the model artifact or fitting a Rust
rate. Passing this suite does not calibrate Rust or establish absolute-hour accuracy.

## What suite 0.4.0 measures

| Ecosystem family | Cases | Principal variants |
| --- | ---: | --- |
| .NET | 13 | Existing variants plus renamed near-copy, compiler-disabled boundaries, data context, migration, security |
| JavaScript | 22 | Existing variants plus renamed near-copy, data, security, declared/measured coverage levels, workspace boundaries, CI, container |
| TypeScript | 11 | Existing variants plus renamed near-copy, data, and security |
| Mixed | 5 | Base, generated JavaScript, .NET API, JavaScript UI, TypeScript tests |

All 170 assertions pass. The relations cover:

- zero-difference formatting, exact-copy, and conventional-generated invariants;
- low, expected, and high points rather than expected hours alone;
- positive production movement for API behavior and separately maintained
  customization beside generated output;
- positive UI, unit-test, documentation, and integration category movement; and
- bounded marginal rather than full-body treatment for small renamed near-copies;
- zero movement from compiler-disabled C# data and authorization syntax;
- positive data, migration, security, declared and measured coverage,
  workspace-boundary, CI, and container movement in the intended categories;
- measured-over-conflicting-declared precedence at every total and unit-test range
  point; and
- category isolation for test-only, documentation-only, UI-only, data-only,
  security-only, coverage-only, CI-only, container-only, and production-only
  variants.

Missing categories are evaluated as zero. This allows, for example, the unit-test
category in a test variant to be compared directly with an absent base category.
Nested synthetic test packages keep test structure in an explicit test scope, so
their production-category invariants are not artifacts of aggregate rounding.

## Seed expected checkpoints

### Go expansion additions

| Variant | Expected total | Intended category result |
| --- | ---: | --- |
| Go base | 8.00 h | 0.50 h production plus one explicit library release surface |
| Go formatting | 8.00 h | Total and production unchanged at every range point |
| Go exact copy | 8.00 h | Total and production unchanged at every range point |
| Go generated body | 8.00 h | Total and production unchanged at every range point |
| Go API | 11.50 h | Positive production/API movement |
| Go tests | 9.25 h | +1.25 h unit testing; production unchanged |
| Go data | 10.25 h | +1.00 h data/persistence |
| Go integration | 12.50 h | +3.25 h external integration |
| Go security | 13.50 h | +4.25 h security/accessibility |
| Go background work | 12.75 h | Positive background/production movement |
| Go build semantics | 8.75 h | +0.75 h build/tooling; production unchanged |
| Go concurrency | 8.75 h | Positive bounded production movement |
| Go framework namesakes | 9.50 h | No API, data, integration, security, or background category |

### Java expansion additions

| Variant | Expected total | Intended category result |
| --- | ---: | --- |
| Java base | 8.25 h | 0.75 h production plus one explicit library release surface |
| Java formatting | 8.25 h | Total and production unchanged at every range point |
| Java exact copy | 8.25 h | Total and production unchanged at every range point |
| Java generated body | 8.25 h | Total and production unchanged at every range point |
| Java API | 11.50 h | Positive production/API movement |
| Java tests | 9.50 h | +1.25 h unit testing; production unchanged |
| Java data | 10.25 h | +1.00 h data/persistence |
| Java integration | 12.50 h | +3.25 h external integration |
| Java security | 13.50 h | +4.25 h security/accessibility |
| Java background work | 12.75 h | Positive background/production movement |
| Java build semantics | 9.00 h | +0.75 h build/tooling; production unchanged |
| Java concurrency | 12.50 h | Positive bounded production movement |
| Java framework namesakes | 10.00 h | No API, data, integration, security, or background category |

### Kotlin/JVM expansion additions

| Variant | Expected total | Intended category result |
| --- | ---: | --- |
| Kotlin base | 8.25 h | 0.75 h production plus one explicit library release surface |
| Kotlin formatting | 8.25 h | Total and production unchanged at every range point |
| Kotlin exact copy | 8.25 h | Total and production unchanged at every range point |
| Kotlin generated body | 8.25 h | Total and production unchanged at every range point |
| Kotlin API | 11.75 h | Positive production/API movement |
| Kotlin Android/Compose | 11.25 h | Positive UI movement |
| Kotlin tests | 9.50 h | +1.25 h unit testing; production unchanged |
| Kotlin data | 11.00 h | Positive data/persistence movement |
| Kotlin integration | 12.75 h | Positive external-integration movement |
| Kotlin security | 14.25 h | Positive security/accessibility movement |
| Kotlin background work | 13.00 h | Positive background/production movement |
| Kotlin build semantics | 9.00 h | +0.75 h build/tooling; production unchanged |
| Kotlin coroutines | 13.00 h | Positive bounded production movement |
| Kotlin framework namesakes | 10.00 h | No API, UI, data, integration, security, or background category |

### Shell and PowerShell expansion additions

| Variant | Expected total | Intended category result |
| --- | ---: | --- |
| Scripting base | 13.50 h | Shell CLI plus PowerShell module production backbone |
| Scripting formatting | 13.50 h | Total and production unchanged at every range point |
| Scripting exact copy | 13.50 h | Total and production unchanged at every range point |
| Scripting generated/copied bodies | 13.50 h | Total and production unchanged at every range point |
| Scripting tests | 14.75 h | +1.25 h unit testing; production unchanged |
| Scripting integration | 17.75 h | +3.25 h external integration |
| Scripting security | 20.25 h | +5.75 h security/accessibility |
| Scripting validation | 15.50 h | +1.25 h existing validation/security rule |
| Scripting build | 14.25 h | +0.75 h build/tooling; production unchanged |
| Scripting CI | 15.50 h | +2.00 h CI/infrastructure; production unchanged |
| Scripting delivery | 14.75 h | +1.25 h packaging/delivery after scanner/semantic normalization; production unchanged |
| Scripting infrastructure | 15.25 h | +1.75 h CI/infrastructure; production unchanged |
| Scripting command namesakes | 14.00 h | No integration or security category |

### Rust and Cargo expansion additions

| Variant | Expected total | Intended category result |
| --- | ---: | --- |
| Rust base | 9.75 h | Bounded library package and production backbone |
| Rust formatting | 9.75 h | Total and production unchanged at every range point |
| Rust exact copy | 9.75 h | Total and production unchanged at every range point |
| Rust conventional exclusions | 10.00 h | Semantic production unchanged; lock inventory remains bounded build evidence |
| Rust workspace | 22.75 h | Positive nested package ownership and local-reference movement |
| Rust API | 12.50 h | Positive production/API movement |
| Rust data | 11.00 h | Positive data/persistence movement |
| Rust integration | 13.25 h | Positive external-integration movement |
| Rust security | 14.00 h | Positive security/accessibility movement |
| Rust background work | 13.00 h | Positive background/concurrency movement |
| Rust FFI/build | 16.50 h | Positive FFI integration, build/tooling, and delivery movement |
| Rust unit tests | 10.75 h | Positive unit-testing movement |
| Rust system quality | 17.75 h | Positive integration-test, benchmark, and example movement |
| Rust crate namesakes | 10.75 h | No API, data, integration, security, or background category |

### Frontend semantic additions

| Variant | Expected total | Intended category result |
| --- | ---: | --- |
| Frontend base | 10.25 h | 1.75 h UI reference |
| Frontend formatting | 10.25 h | Total and UI unchanged at every range point |
| Frontend exact stylesheet copy | 10.25 h | Total and UI unchanged at every range point |
| Frontend semantic behavior | 13.75 h | +3.50 h UI; production unchanged |
| Angular component and owned assets | 17.00 h | 7.25 h UI with static component ownership |

### Static SQL additions

| Variant | Expected total | Intended category result |
| --- | ---: | --- |
| SQL base | 9.75 h | 3.25 h data reference |
| SQL formatting | 9.75 h | Total and data unchanged at every range point |
| SQL exact copy | 9.75 h | Total and data unchanged at every range point |
| SQL generated dump | 9.75 h | Total and data unchanged at every range point |
| SQL unknown vendor statement | 9.75 h | Visible without guessed effort |
| SQL semantic schema/query | 14.50 h | +4.75 h data |
| SQL test fixture | 18.25 h | +8.50 h integration/component testing; data unchanged |
| SQL delivery script | 11.00 h | +1.25 h packaging/deployment; data unchanged |
| SQL cross-database query | 14.25 h | +3.25 h external integration |
| SQL seed, one vs twenty rows | 9.75 h | Total and data unchanged between row counts |

### Milestone 7B6 precision additions

| Variant | Expected total | Intended category result |
| --- | ---: | --- |
| Reachable .NET helper chain | 8.00 h | +0.75 h production over .NET base |
| Unreferenced private .NET integration | 7.25 h | Total, production, and integration remain at base |
| Explicitly included conditional .NET behavior | 7.75 h | +0.50 h production |
| Specified .NET equivalent-purpose shape | 8.25 h | +1.00 h bounded production marginality |
| Specified JavaScript equivalent-purpose shape | 7.00 h | +0.50 h bounded production marginality |
| Explicit frontend accessibility semantics | 14.50 h | 3.75 h security/accessibility; UI unchanged |
| Shallow component tests | 16.50 h | 2.50 h integration/component testing |
| Accessibility-focused component tests | 17.50 h | +1.00 h integration/component testing; UI/accessibility implementation unchanged |
| Representative four-package workspace | 38.75 h | Setup, architecture, production, UI, and unit tests increase |
| Representative mixed dependency graph | 40.75 h | Setup, architecture, production, UI, and component tests increase |

### JavaScript

| Variant | Expected total | Intended category result |
| --- | ---: | --- |
| Base | 6.50 h | 0.50 h production |
| Formatting | 6.50 h | Unchanged at every range point |
| Exact copy | 6.50 h | Unchanged at every range point |
| Generated body | 6.50 h | Unchanged at every range point |
| Generated customization | 7.00 h | +0.50 h production |
| API | 9.50 h | +1.75 h production |
| Unit tests | 9.25 h | +1.75 h unit testing; production unchanged |
| Documentation | 7.50 h | +1.00 h documentation; production unchanged |
| Integration | 11.50 h | +3.25 h external integration |

### TypeScript

| Variant | Expected total | Intended category result |
| --- | ---: | --- |
| Base | 7.75 h | 0.75 h production |
| Formatting | 7.75 h | Unchanged at every range point |
| Exact copy | 7.75 h | Unchanged at every range point |
| Generated body | 7.75 h | Unchanged at every range point |
| API | 10.75 h | +1.75 h production |
| Unit tests | 10.50 h | +1.75 h unit testing; production unchanged |
| Documentation | 8.75 h | +1.00 h documentation; production unchanged |
| Integration | 13.00 h | +3.25 h external integration |

### Mixed .NET and JavaScript/TypeScript

| Variant | Expected total | Intended category result |
| --- | ---: | --- |
| Base | 12.75 h | 2.00 h production |
| Generated JavaScript | 12.75 h | Unchanged at every range point |
| .NET API | 17.25 h | +2.00 h production; unit testing unchanged |
| JavaScript UI | 16.00 h | +1.75 h UI; unit testing unchanged |
| TypeScript tests | 15.50 h | +1.75 h unit testing; production unchanged |

### Milestone 7B4 additions

| Variant | Expected total | Intended category result |
| --- | ---: | --- |
| .NET renamed near-copy | 8.25 h | +1.00 h bounded production marginality |
| .NET compiler-disabled boundaries | 7.25 h | Total, data, and security unchanged |
| .NET data context | 13.25 h | +4.00 h data/persistence |
| .NET data context plus migration | 15.50 h | +1.25 h data/persistence over the context case |
| .NET security | 20.00 h | +9.00 h security/accessibility |
| JavaScript renamed near-copy | 6.75 h | +0.25 h bounded production marginality |
| JavaScript data | 9.25 h | +1.00 h data/persistence |
| JavaScript security | 16.75 h | +4.75 h security/accessibility |
| TypeScript renamed near-copy | 8.00 h | +0.25 h bounded production marginality |
| TypeScript data | 10.75 h | +1.00 h data/persistence |
| TypeScript security | 17.75 h | +4.75 h security/accessibility |
| Coverage base | 11.00 h | 1.75 h unit testing with no declared percentage |
| Declared-and-assumed 80% coverage | 12.25 h | +1.25 h unit testing over identical code/tests |
| Declared-and-assumed 100% coverage | 12.50 h | +0.25 h unit testing over the 80% case |
| One-package workspace | 12.75 h | Reference workspace boundary |
| Two-package workspace with exact body reuse | 18.00 h | +1.25 h setup and +1.00 h review; production unchanged |
| JavaScript CI workflow | 8.50 h | +2.00 h CI/CD; production unchanged |
| JavaScript container definition | 8.50 h | +2.00 h packaging/deployment; production unchanged |

### Measured-coverage additions

| Variant | Expected total | Intended category result |
| --- | ---: | --- |
| Measured 80% LCOV | 12.25 h | 3.00 h unit testing; production unchanged |
| Measured 100% LCOV | 12.50 h | +0.25 h unit testing over measured 80% |
| Measured 80% plus declared 100% | 12.25 h | Identical to measured 80% at every total and unit-test range point |

The LCOV files contain only synthetic repository-relative paths. Parser privacy is
separately tested with non-public absolute path text that must not appear in output.
LCOV and Cobertura inputs are bounded, parsed without execution, and checked against
their common-inventory SHA-256 before measurements are admitted.

The original .NET-only 0.1.0 table and its exact results remain represented by its
frozen suite, canonical estimates, and baseline report. Suites 0.2.0 through 0.7.0
retain all earlier assertions unchanged.

## Artifacts

- `0.1.0.suite.json` defines the frozen .NET-only suite.
- `baseline-seed-rules-0.2.0.json` is its original deterministic report.
- `estimates/*.estimate.json` contains its eight original 0.2.0 candidates.
- `0.2.0.suite.json` defines the 30-case aggregate suite.
- `baseline-seed-rules-0.2.1-suite-0.2.0.json` is the aggregate deterministic
  report.
- `0.3.0.suite.json` defines the 48-case expanded aggregate suite.
- `baseline-seed-rules-0.2.1-suite-0.3.0.json` is its deterministic report.
- `0.4.0.suite.json` defines the 51-case measured-coverage aggregate suite.
- `baseline-seed-rules-0.2.1-suite-0.4.0.json` is its deterministic report.
- `estimates/seed-rules-0.2.1/` contains the 51 frozen suite-0.4.0 candidates.
- `0.5.0.suite.json` defines the 56-case frontend semantic aggregate suite.
- `baseline-seed-rules-0.3.0-suite-0.5.0.json` is its deterministic report.
- `0.6.0.suite.json` defines the 67-case static SQL aggregate suite.
- `baseline-seed-rules-0.3.0-suite-0.6.0.json` is its deterministic report.
- `0.7.0.suite.json` defines the 77-case Milestone 7B6 aggregate suite.
- `baseline-seed-rules-0.3.0-suite-0.7.0.json` is its deterministic report.
- `estimates/seed-rules-0.3.0/` contains all 77 current aggregate candidates.
- `0.8.0.suite.json` extends that checkpoint to 88 cases with 11 synthetic Python
  states and 339 relations.
- `baseline-seed-rules-0.4.0-suite-0.8.0.json` records all 339 passing assertions.
  The earlier 77 candidates remain frozen at `seed-rules/0.3.0`; only the Python
  candidates under `estimates/seed-rules-0.4.0/` use the new model identity.
- `go-0.1.0.suite.json` defines the standalone 13-case Go suite.
- `baseline-seed-rules-0.4.0-go-0.1.0.json` records all 56 passing Go assertions.
- `estimates/seed-rules-0.4.0/go-*.estimate.json` contains its candidates.
- `java-0.1.0.suite.json` defines the standalone 13-case Java suite.
- `baseline-seed-rules-0.4.0-java-0.1.0.json` records all 56 passing Java assertions.
- `estimates/seed-rules-0.4.0/java-*.estimate.json` contains its candidates.
- `kotlin-0.1.0.suite.json` defines the standalone 14-case Kotlin suite.
- `baseline-seed-rules-0.4.0-kotlin-0.1.0.json` records all 63 passing Kotlin assertions.
- `estimates/seed-rules-0.4.0/kotlin-*.estimate.json` contains its candidates.
- `scripting-0.1.0.suite.json` defines the standalone 13-case Shell/PowerShell
  suite.
- `baseline-seed-rules-0.4.0-scripting-0.1.0.json` records all 46 passing
  scripting assertions.
- `estimates/seed-rules-0.4.0/scripting-*.estimate.json` contains its candidates.
- `terraform-0.1.0.suite.json` defines the standalone 14-case Terraform/HCL suite.
- `baseline-seed-rules-0.4.0-terraform-0.1.0.json` records all 48 passing
  Terraform/HCL assertions.
- `estimates/seed-rules-0.4.0/terraform-*.estimate.json` contains its candidates.
- `php-0.1.0.suite.json` defines the standalone 14-case PHP/Composer suite.
- `baseline-seed-rules-0.4.0-php-0.1.0.json` records all 59 passing PHP assertions.
- `estimates/seed-rules-0.4.0/php-*.estimate.json` contains its candidates.
- `rust-0.1.0.suite.json` defines the standalone 14-case Rust/Cargo suite.
- `baseline-seed-rules-0.4.0-rust-0.1.0.json` records all 62 passing Rust assertions.
- `estimates/seed-rules-0.4.0/rust-*.estimate.json` contains its candidates.
- `fixtures/` contains every complete synthetic source state.

Every case has a distinct repository source digest. Assertions select canonical
estimates only by source digest, profile, and worker-baseline ID. File timestamps,
contributors, commit activity, and history are not inputs.

## Reproduce suite 0.4.0

From a Release build, regenerate each candidate with:

```text
eh estimate calibration/mutations/public-synthetic/fixtures/<fixture> \
  --profile implementation --no-rate \
  --output calibration/mutations/public-synthetic/estimates/seed-rules-0.2.1/<case>.estimate.json
```

For the original eight .NET cases, `<fixture>` is the case ID without the
`dotnet-` prefix. For every later case, fixture and case IDs are identical.

Then evaluate all versioned candidate paths:

```text
eh calibration mutations \
  calibration/mutations/public-synthetic/0.4.0.suite.json \
  calibration/mutations/public-synthetic/estimates/seed-rules-0.2.1/*.estimate.json \
  --output calibration/mutations/public-synthetic/baseline-seed-rules-0.2.1-suite-0.4.0.json
```

The wildcard is shell convenience, not part of EffortHours's argument semantics.
Callers on shells without wildcard expansion should list the estimate paths. Failed
assertions still produce the complete report and return process exit code `5`;
malformed inputs return the ordinary invalid-input code.

## Reproduce suite 0.5.0

Use the same estimate command for every case, changing the output directory to
`estimates/seed-rules-0.3.0`. The original eight .NET fixture aliases remain as
documented above; every other fixture matches its case ID. Then run:

```text
eh calibration mutations \
  calibration/mutations/public-synthetic/0.5.0.suite.json \
  calibration/mutations/public-synthetic/estimates/seed-rules-0.3.0/*.estimate.json \
  --output calibration/mutations/public-synthetic/baseline-seed-rules-0.3.0-suite-0.5.0.json
```

Callers whose shell does not expand wildcards must list the 56 paths explicitly.

## Reproduce suite 0.6.0

Retain the 56 frozen suite-0.5.0 candidates and generate the 11 `sql-*` fixtures
with the same estimate command into `estimates/seed-rules-0.3.0`. Then evaluate
all 67 explicitly listed candidate paths against `0.6.0.suite.json`, writing
`baseline-seed-rules-0.3.0-suite-0.6.0.json`. Shells that expand wildcards may use
the same command shape shown for suite 0.5.0.

## Reproduce suite 0.7.0

Retain all 67 frozen suite-0.6.0 candidates and generate only the 10 new fixture
IDs listed in the Milestone 7B6 table with the same estimate command into
`estimates/seed-rules-0.3.0`. Then evaluate all 77 explicitly listed candidate
paths against `0.7.0.suite.json`, writing
`baseline-seed-rules-0.3.0-suite-0.7.0.json`.

## Reproduce suite 0.8.0

Retain all 77 frozen suite-0.7.0 candidates. Generate the 11 `python-*` fixtures
with repository estimator `seed-rules/0.4.0` into
`estimates/seed-rules-0.4.0/`, then evaluate both candidate directories against
`0.8.0.suite.json`, writing
`baseline-seed-rules-0.4.0-suite-0.8.0.json`. The resulting report must disclose
both candidate estimator versions, 88 cases, 339 assertions, and zero failures.
Mixing versions here is deliberate: it preserves all earlier reports and isolates
the new Python boundary instead of presenting a version-only regeneration as new
accuracy evidence.

## Reproduce standalone Go suite 0.1.0

Generate each of the 13 `go-*` fixtures with repository estimator
`seed-rules/0.4.0` into `estimates/seed-rules-0.4.0/`. Then evaluate the 13 exact
candidate paths against `go-0.1.0.suite.json`, writing
`baseline-seed-rules-0.4.0-go-0.1.0.json`. The result must disclose one candidate
estimator version, 13 cases, 56 assertions, and zero failures. The suite is
standalone so the frozen aggregate and its mixed estimator identities remain
untouched.

## Reproduce standalone Java suite 0.1.0

Generate each of the 13 `java-*` fixtures with repository estimator
`seed-rules/0.4.0` into `estimates/seed-rules-0.4.0/`. Then evaluate the 13 exact
candidate paths against `java-0.1.0.suite.json`, writing
`baseline-seed-rules-0.4.0-java-0.1.0.json`. The result must disclose one candidate
estimator version, 13 cases, 56 assertions, and zero failures. The suite is
standalone so the frozen aggregate and its mixed estimator identities remain
untouched.

## Reproduce standalone Kotlin suite 0.1.0

Generate each of the 14 `kotlin-*` fixtures with repository estimator
`seed-rules/0.4.0` into `estimates/seed-rules-0.4.0/`. Then evaluate the 14 exact
candidate paths against `kotlin-0.1.0.suite.json`, writing
`baseline-seed-rules-0.4.0-kotlin-0.1.0.json`. The result must disclose one
candidate estimator version, 14 cases, 63 assertions, and zero failures. The suite
is standalone so the frozen aggregate and its mixed estimator identities remain
untouched.

## Reproduce standalone scripting suite 0.1.0

Generate each of the 13 `scripting-*` fixtures with repository estimator
`seed-rules/0.4.0` into `estimates/seed-rules-0.4.0/`. Then evaluate the 13 exact
candidate paths against `scripting-0.1.0.suite.json`, writing
`baseline-seed-rules-0.4.0-scripting-0.1.0.json`. The result must disclose one
candidate estimator version, 13 cases, 46 assertions, and zero failures. The suite
is standalone so the frozen aggregate and all earlier standalone suites remain
untouched.

## Reproduce standalone Terraform/HCL suite 0.1.0

Generate each of the 14 `terraform-*` fixtures with repository estimator
`seed-rules/0.4.0` into `estimates/seed-rules-0.4.0/`. Then evaluate the 14 exact
candidate paths against `terraform-0.1.0.suite.json`, writing
`baseline-seed-rules-0.4.0-terraform-0.1.0.json`. The result must disclose one
candidate estimator version, 14 cases, 48 assertions, and zero failures. The suite
is standalone so the frozen aggregate and all earlier standalone suites remain
untouched.

## Reproduce standalone PHP/Composer suite 0.1.0

Generate each of the 14 `php-*` fixtures with repository estimator
`seed-rules/0.4.0` into `estimates/seed-rules-0.4.0/`. Then evaluate the 14 exact
candidate paths against `php-0.1.0.suite.json`, writing
`baseline-seed-rules-0.4.0-php-0.1.0.json`. The result must disclose one candidate
estimator version, 14 cases, 59 assertions, and zero failures. The suite is
standalone so the frozen aggregate and all earlier standalone suites remain
untouched.

The `php-excluded` case includes `composer.lock` to verify that lock contents never
create PHP production semantics. The existing language-neutral lock inventory may
still add one bounded build/configuration unit; the suite bounds that movement
explicitly instead of changing the shared prior or regenerating frozen reports.

## Reproduce standalone Rust/Cargo suite 0.1.0

Generate each of the 14 `rust-*` fixtures with repository estimator
`seed-rules/0.4.0` into `estimates/seed-rules-0.4.0/`. Then evaluate the 14 exact
candidate paths against `rust-0.1.0.suite.json`, writing
`baseline-seed-rules-0.4.0-rust-0.1.0.json`. The result must disclose one candidate
estimator version, 14 cases, 62 assertions, and zero failures. The suite is
standalone so the frozen aggregate and all earlier standalone suites remain
untouched.

The `rust-excluded` case includes `Cargo.lock` to verify that lock contents never
create Rust production semantics. The existing language-neutral lock inventory may
still add one bounded build/configuration unit; the suite bounds that movement
explicitly instead of changing the shared prior or regenerating frozen reports.

## Reproduce standalone Docker/Compose suite 0.1.0

Generate each of the 13 `docker-*` fixtures with repository estimator
`seed-rules/0.4.0` into `estimates/seed-rules-0.4.0/`. Then evaluate the 13 exact
candidate paths against `docker-0.1.0.suite.json`, writing
`baseline-seed-rules-0.4.0-docker-0.1.0.json`. The result must disclose one
candidate estimator version, 13 cases, 38 assertions, and zero failures. The suite
is standalone so the frozen aggregate and all earlier standalone suites remain
untouched.

## Reproduce standalone Jupyter suite 0.1.0

Generate each of the 14 `jupyter-*` fixtures with repository estimator
`seed-rules/0.4.0` into `estimates/seed-rules-0.4.0/`. Then evaluate the 14 exact
candidate paths against `jupyter-0.1.0.suite.json`, writing
`baseline-seed-rules-0.4.0-jupyter-0.1.0.json`. The result must disclose one
candidate estimator version, 14 cases, 52 assertions, and zero failures. The suite
is standalone so the frozen aggregate and all earlier standalone suites remain
untouched.

## Limitations and next expansion

The suite uses deliberately small archetypes. Near-copy and the two new
equivalent-purpose assertions bound only their specified shapes; EffortHours does
not perform general semantic-clone detection. .NET reachability excludes only
explicit private methods with no bounded intra-file reference in a non-partial,
unattributed type without a base list; attributed, partial, and external methods
are retained, as are conservative string-name matches. It is not a
general liveness, reflection, dynamic-dispatch, or cross-file analysis. Measured
coverage supports LCOV and Cobertura only, and a digest-verified checked-in report
can still be stale or belong to a different source snapshot. Security and explicit
static accessibility evidence do not replace audits or prove runtime conformance.
The new dependency graphs are representative small synthetic boundaries, not
large-repository benchmarks. Additional coverage formats, richer infrastructure,
general reachability/clones, broader JSX accessibility semantics, and multiple
observations per ecosystem/partition cell remain future guardrails. The TypeScript
path remains token-backed.

Frontend scanners are tolerant and bounded. They do not render, execute
TypeScript/configuration, compile Angular or another framework, run CSS
preprocessors, establish runtime reachability, or perform accessibility auditing.
Explicit roles, labels, alternative text, live regions, and keyboard/focus signals
are represented only as static implementation evidence and carry an explicit
`accessibility-conformance:not-proven` tag.

The SQL scanner is also tolerant and bounded. It does not connect to a database,
execute or validate SQL, bind object names/types, calculate query plans, prove
migration order/reversibility, or exhaustively parse vendor procedural languages.
Its dialect labels are confidence signals rather than server selection.

The Java scanner is token-backed rather than a compiler or full build-system
model. It does not resolve types, annotation processors, Maven interpolation,
Gradle execution, dependency graphs outside scanner-admitted literals, generated
sources, reflection, or runtime reachability. Build and semantic labels are static
confidence signals, not proof that a Java build compiles or runs.

The Kotlin scanner is likewise token-backed. It does not resolve Kotlin/JVM types,
compiler plugins, Gradle execution, KSP/kapt generation, Android resources,
multiplatform expect/actual bindings, reflection, or runtime reachability. Build,
platform, and semantic labels are static confidence signals, not proof that a
Kotlin project compiles or runs.

The Shell and PowerShell scanner is token-backed rather than interpreter-backed.
It does not start a shell, resolve commands/modules, follow sourced content,
evaluate expansions, prove quoting/pipeline semantics, or observe filesystem,
process, network, permission, or platform effects. Static role and semantic labels
are confidence signals, not proof that a script runs correctly or portably.

The PHP scanner is token-backed rather than native-parser/runtime-backed, and its
Composer reader is not a dependency resolver. It does not execute autoloaders,
scripts, plugins, framework bootstraps, containers, routes, reflection, dynamic
includes, tests, or generated caches. Static package, template, and semantic labels
are confidence signals, not proof that a PHP application installs or runs.

The Rust scanner is token-backed rather than rustc-backed, and its Cargo reader is
not a dependency, feature, or build-plan resolver. It does not expand macros, run
build scripts or generators, resolve target-specific configuration, borrow-check,
compile, link, execute tests/examples/benchmarks, or inspect generated bindings.
Static package, source, and semantic labels are confidence signals, not proof that
a Rust workspace builds or runs.

The Dockerfile reader is not Docker's parser or BuildKit frontend, and the Compose
reader is not a full YAML parser, Compose schema validator, interpolation engine,
or runtime planner. It does not pull images, expand build contexts, load includes
or environment files, resolve secrets, invoke Docker/Compose/BuildKit, or prove
build/runtime correctness. Static container labels are confidence signals, not
proof that a stack builds or runs.

The Jupyter reader is not a notebook runtime, kernel, environment resolver,
executor, data-provenance system, output validator, or scientific-review tool. It
does not execute cells or magics, load outputs/attachments/widgets, resolve
runtime dependencies, verify reproducibility, or establish output/scientific
correctness. Static maintained-cell evidence and the passing qualitative suite do
not prove that a notebook runs or that its results are valid.

Passing these relations prevents known perverse movements. It does not establish
that any absolute hour or delta is numerically correct, and it does not make the
seed estimator calibrated or production-ready.
