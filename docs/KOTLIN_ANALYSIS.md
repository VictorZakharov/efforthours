# Static Kotlin/JVM analysis boundary

## Status

Kotlin/JVM is a first-class, token-backed EffortHours ecosystem beginning with:

- common scanner `0.2.6`;
- Kotlin analyzer `0.1.0`;
- unchanged repository estimator `seed-rules/0.4.0`; and
- Change estimator `change-seed/0.11.0+seed-rules/0.4.0`.

Later additive ecosystem extensions advance the current composite source identity to
`change-seed/0.18.1+seed-rules/0.4.0` without changing Kotlin normalization or
valuation behavior.

This boundary is experimental and uncalibrated. It adds static Kotlin/JVM,
Android, Maven, and Gradle evidence and reuses transparent analogous priors; it
does not establish numerical accuracy or production admission. The admitted
Change Stage A slice remains the pre-Kotlin `change-seed/0.6.0` boundary in
`CHANGE_MODEL_ADMISSION.md`.

## Admitted files, scripts, and JVM ownership

The analyzer admits maintained `.kt` and `.kts` files selected by the common
scanner. Exact `build.gradle.kts`, `settings.gradle.kts`, and other
`*.gradle.kts` paths are build configuration, not executable product source.
Other scanner-admitted `.kts` files are maintained Kotlin scripts and potential
entry surfaces, but they are never executed. Conventional generated, vendored,
minified, binary, and build-output files retain common exclusion precedence.

Kotlin reuses the Java analyzer assembly's bounded Maven and Gradle project
reader. It statically reads scanner-admitted `pom.xml`, `build.gradle`,
`build.gradle.kts`, `settings.gradle`, and `settings.gradle.kts` files. A
Kotlin-only repository owns its JVM project and build facts. In a mixed
Java/Kotlin project, Kotlin source reuses an already discovered Java/JVM scope,
build fact, dependencies, and local-project edges instead of charging for the
same module setup twice. A source-only Kotlin repository receives one explicit
repository-level fallback JVM scope.

Static Maven discovery records literal coordinates, packaging, dependencies,
plugins, profiles, processor declarations, and in-repository reactor modules.
Static Gradle discovery records literal root names, includes, included builds,
`projectDir` mappings, project dependencies, external coordinates, and plugins.
Each source file belongs to the deepest containing discovered project. Kotlin
package imports may establish a local project edge only when they match a package
owned by another discovered project and no shared JVM build edge already
represents that relationship.

This is conservative project discovery, not an effective Maven model or Gradle
evaluator. Dynamic or property-backed values remain explicit uncertainty. The
analyzer does not resolve version catalogs, convention plugins, source sets,
variants, profiles, properties, composite builds, or dependency graphs beyond
bounded literal evidence.

## Source structure

A bounded managed tokenizer recognizes Kotlin identifiers and backtick names,
keywords, numeric literals, regular and triple-quoted strings, character
literals, nested comments, operators, delimiters, annotations, and a leading
script hashbang. A conservative declaration pass records:

- packages, imports, aliases, wildcards, and imports into a package owned by the
  same JVM project;
- classes, objects, data classes, sealed types, interfaces, enums, type aliases,
  functions, methods, extension functions, annotations, generic declarations,
  and default-public symbols;
- `suspend` functions, coroutine and Flow usage, branch points, exception paths,
  and nullability operators; and
- top-level `main` declarations and maintained `.kts` script entry surfaces.

This is token-backed structural evidence, not a Kotlin parser, FIR/front-end,
compiler, type checker, control-flow engine, call graph, or JVM bytecode reader.
Malformed delimiters, unterminated literals/comments, and the 250,000-token cap
reduce confidence. Counts are bounded construction signals; physical lines are
not the principal effort driver.

## Import-qualified server, Android, and library semantics

Framework evidence requires a recognized canonical import or fully qualified
name plus a compatible annotation, base type, property type, or call. Local
annotations, classes, and functions that merely reuse framework names do not
qualify. Aliased imports retain their canonical owner. A wildcard contributes a
symbol only when its recognized ownership is unambiguous.

Recognized bounded surfaces include:

- Ktor server routing, Spring MVC, and Jakarta REST APIs;
- Android activities, fragments, lifecycle components, services, receivers,
  workers, Compose functions, and common Compose UI calls;
- Spring Data, JPA/Jakarta Persistence, JDBC, Exposed, Room, Flyway, and
  Liquibase persistence or migration evidence;
- Ktor Client, JDK HTTP, OkHttp, Retrofit, gRPC, cloud SDK, Kafka, AMQP, and JMS
  integrations;
- Spring/Jakarta/Android security and JWT usage;
- Spring scheduling/batch, Quartz, Android WorkManager, messaging, coroutines,
  and Flow background behavior;
- Jakarta Validation rules; and
- Clikt and Picocli command surfaces.

These facts establish represented static surfaces. They do not prove route or
navigation reachability, dependency injection, proxy/interceptor behavior,
Compose recomposition, lifecycle correctness, persistence mappings, query
validity, transaction behavior, message delivery, authentication or authorization
correctness, coroutine cancellation/structured-concurrency correctness, or
runtime configuration.

## Tests

The common scanner recognizes conventional `*Test`, `*Tests`, `*TestCase`, and
`*IT` names for both `.kt` and `.kts`, plus test and Android-test directories. The
analyzer records import-qualified `kotlin.test`, JUnit, and Kotest cases,
parameterization, assertions, MockK/Mockito usage, Testcontainers context, and
unit, integration, component, Android, or end-to-end path signals. Semantic facts
from test-classified files do not become production API, UI, data, integration,
security, validation, or background evidence.

Discovered tests are assumed to pass on the fastest default path. EffortHours does
not invoke Gradle, Maven, a JVM, test runners, Android tooling, emulators, devices,
or Testcontainers; discover runtime-generated tests; measure coverage; or prove
that tests compile, execute, isolate dependencies, or reach claimed behavior.

## Build, compiler plugins, multiplatform, and generation

Gradle Kotlin DSL files, Maven profiles, plugins, unresolved values, KSP, kapt,
annotation processors, serialization, all-open/no-arg style compiler plugins,
Android plugins, and multiplatform indicators are visible only as static
measurements, tags, or diagnostics. Plugin and generator declarations never cause
EffortHours to invent generated types or behavior.

The analyzer does not execute Gradle Kotlin DSL or a wrapper; invoke a JVM,
`kotlinc`, Maven, Gradle, the Android Gradle Plugin, KSP, kapt, tests, or compiler
plugins; download dependencies; select variants, targets, source sets, toolchains,
or a target JVM; expand resources; inspect class/JAR files; use reflection; or
execute target code. `expect`/`actual`, platform source-set, and Kotlin
Multiplatform relationships remain visible source structure, not resolved target
behavior.

## Safety, exclusions, and privacy

Every analyzer read is restricted to a scanner-admitted regular file inside the
selected root, then checked against the scanner-recorded byte length and SHA-256
digest. Source and build text must be valid UTF-8 and no larger than eight
mebibytes. Changed, unsafe, oversized, invalid, truncated, or malformed inputs
fail closed or retain low confidence.

Ordinary evidence and estimate output contains paths, counts, classifications,
declared coordinates/dependencies, technology labels, and reasoning—not source
excerpts, literals, comments, credentials, build-script bodies, or generated
output. Declared names and paths remain repository metadata and should be handled
according to the caller's privacy requirements.

## Estimation and Change EHE

Kotlin source structure consumes the existing `seed-rules/0.4.0`
`polyglot-source-backbone` inside its owning JVM scope. File/function/method/type/
public-symbol/async/branch rates transparently reuse analogous construction priors
with wider uncertainty. Specialized facts continue through existing setup,
architecture, entry-point, API, UI, data, integration, security, validation,
background, testing, build, manual-validation, and review rules. No
Kotlin-specific fitted rate, private observation, dependency, or model artifact
was added.

Change EHE `0.11.0` admits source-readable `.kt` and `.kts` paths to a Kotlin-aware
token comparison. Ordinary formatting, optional semicolons/trailing commas, and
non-documentation comments can normalize to zero. KDoc/Markdown documentation,
regular and raw strings, character/numeric literals, identifiers, backtick names,
operators, delimiters, and semantic newlines after jump expressions remain
meaningful. Final scanner/analyzer evidence routes Kotlin changes through existing
category reconciliation. This extension does not expand the admitted 4-to-32-hour
Change band.

The standalone public Kotlin mutation suite contains 14 project-authored MIT
states and 63 passing relations. It covers formatting/comments, exact
duplication, generated output, Ktor API, Android Compose, tests, Room data, OkHttp
integration, security, background work, static Gradle Kotlin DSL, coroutines and
Flow, and local framework namesakes. It is qualitative invariance,
directionality, isolation, and false-positive evidence—not reviewed hour labels,
held-out accuracy evidence, interval calibration, or admission.

## Performance checkpoint

On the documented August 11, 2026 workstation, a fresh process analyzed a
generated 10,000-file, 1,000,001-line Kotlin/JVM tree plus one root
`build.gradle.kts` in 9.393 seconds. The sampled process peak was 166.83 MiB and
cumulative managed allocation was 9,377.77 MiB. Target metadata was unchanged.
The analyzer did not execute target code, invoke a JVM/Gradle/compiler/plugin,
install dependencies, or access the network.

This is a many-small-files scalability checkpoint, not a frozen cross-platform
regression threshold or a representative distribution of real Kotlin, Android,
Gradle, plugin, or multiplatform syntax.

## Explicit non-goals

This boundary does not implement the complete Kotlin grammar, compiler front-end,
effective Gradle/Maven model, Android resource/manifest model, or multiplatform
resolver; compile, link, package, install, or execute; resolve dependencies,
plugins, variants, profiles, source sets, toolchains, or generated declarations;
run KSP, kapt, compiler plugins, tests, emulators, devices, benchmarks, or target
programs; prove reflection, dependency injection, delegated-property behavior,
runtime DSL semantics, reachability, coroutine safety, lifecycle behavior,
accessibility, data/API/security correctness, or production readiness. General
semantic-clone and generated-customization analysis remain separate safety
decisions.
