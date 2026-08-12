# Static Java analysis boundary

## Status

Java is a first-class, token-backed EffortHours ecosystem beginning with:

- common scanner `0.2.5`;
- Java analyzer `0.1.0`;
- unchanged repository estimator `seed-rules/0.4.0`; and
- Change estimator `change-seed/0.10.0+seed-rules/0.4.0`, composed in current
  source as `change-seed/0.12.0+seed-rules/0.4.0` after the additive Kotlin and
  scripting slices.

This boundary is experimental and uncalibrated. It adds static Java, Maven, and
Gradle evidence and reuses transparent analogous priors; it does not establish
numerical accuracy or production admission. The admitted Change Stage A slice
remains the pre-Java `change-seed/0.6.0` boundary documented in
`CHANGE_MODEL_ADMISSION.md`.

## Admitted files and project ownership

The analyzer admits maintained `.java` files selected by the common scanner. It
also reads scanner-admitted `pom.xml`, `build.gradle`, `build.gradle.kts`,
`settings.gradle`, and `settings.gradle.kts` files. Lockfiles, properties files,
and Maven/Gradle wrappers remain common build inventory; wrappers are never run.

Static Maven discovery records literal project coordinates, packaging,
dependencies, plugins, profiles, annotation-processor paths, and in-repository
reactor modules. XML DTD processing and external resolution are prohibited.
Static Gradle discovery records literal root names, includes, included builds,
`projectDir` mappings, project dependencies, external coordinates, plugins, and
annotation-processor configurations. Maven and Gradle projects can coexist; the
analyzer inventories both rather than guessing which build is authoritative.

Each source file belongs to the deepest containing discovered project. A
repository with maintained Java source and no build descriptor receives one
explicit repository-level fallback project. Local project-reference edges require
a literal reactor/include/project/dependency declaration or a package import that
matches another discovered project. Ambiguous project names do not create a
name-only edge. Paths resolving outside the repository are rejected and never
followed.

This is conservative build-model discovery, not an effective Maven model or a
Gradle evaluator. The analyzer does not resolve inheritance, BOMs, version
catalogs, plugins, repositories, source sets, variants, profiles, properties,
conditional logic, convention plugins, composite-build behavior, or dependency
graphs beyond the literal bounded evidence above. Dynamic/property-backed values
remain explicit unresolved measurements and diagnostics.

## Source structure

A bounded managed tokenizer recognizes Java identifiers, keywords, numeric
literals, strings, text blocks, character literals, comments, operators, and
delimiters. A conservative declaration pass records:

- packages, imports, and imports into a declared package owned by the same project;
- classes, records, interfaces, enums, constructors, methods, annotations, and
  public symbols;
- generic declarations, branch points, explicit exception paths, and
  synchronization/concurrency signals;
- module names plus `requires`, `exports`, `uses`, and `provides` directives; and
- static `main` entry points.

This is token-backed structural evidence, not a Java parser, compiler, type
checker, bytecode reader, call graph, control-flow engine, or module resolver.
Malformed delimiters, unterminated literals/comments, Unicode escape ambiguity,
and the token limit reduce confidence. Counts are bounded construction signals;
physical lines are not the principal effort driver.

## Import- and annotation-qualified semantic evidence

Framework evidence requires a recognized canonical import or fully qualified
name plus a compatible annotation, base type, or call. Local annotations and
classes that merely reuse framework names do not qualify. A static wildcard
import is used only when it has one unambiguous owner. Recognized bounded surfaces
include:

- Spring MVC and Jakarta REST API annotations;
- Spring Data, JDBC, JPA/Jakarta Persistence, Hibernate, MyBatis, jOOQ, Flyway,
  and Liquibase persistence/migration evidence;
- JDK HTTP, OkHttp, Retrofit, OpenFeign, gRPC, cloud SDK, Kafka, AMQP, and JMS
  integrations;
- Spring/Jakarta security, JWT, and JDK security usage;
- Spring scheduling/batch, Quartz, messaging listeners, executors, futures, and
  synchronization/background-work evidence;
- Jakarta Validation annotations and calls; and
- Picocli and JCommander command surfaces.

These facts establish represented static surfaces. They do not prove route
reachability, dependency injection, proxy/interceptor behavior, persistence
mapping correctness, transaction behavior, query validity, message delivery,
authentication/authorization correctness, reflection, service loading, native
integration, or runtime configuration.

## Tests

The common scanner recognizes conventional `*Test.java`, `*Tests.java`,
`*TestCase.java`, and `*IT.java` files plus test directories. The analyzer records
import-qualified JUnit and TestNG cases, parameterization, assertions, Mockito
usage, Spring test slices, and Testcontainers context. Explicit path or annotation
signals distinguish unit, integration, component, and end-to-end evidence
conservatively. API, data, integration, security, validation, and background facts
from test-classified files do not become production semantic evidence.

Discovered tests are assumed to pass on the fastest default path. EffortHours does
not invoke Maven, Gradle, Surefire, Failsafe, JUnit, TestNG, Testcontainers, or a
JVM; discover runtime-generated tests; measure coverage; or prove that tests
compile, execute, isolate dependencies, or reach the claimed behavior.

## Build, generation, and runtime uncertainty

Maven profiles, Gradle plugins, unresolved values, annotation processors, module
descriptors, and mixed build systems are visible as measurements, tags, or
diagnostics. Annotation-processor declarations are inventoried without running
processors or inventing generated types. Conventional generated, vendored, and
build-output Java bodies retain common exclusion precedence.

The analyzer does not execute a build script or wrapper; invoke a JVM, `javac`,
Maven, or Gradle; download dependencies or plugins; evaluate Groovy/Kotlin DSL;
select a toolchain, profile, source set, variant, or target JVM; expand resources;
run code generation; inspect class/JAR files; load modules; initialize classes;
use reflection; or execute target code.

## Safety, exclusions, and privacy

Every analyzer read is restricted to a scanner-admitted regular file inside the
selected root, then checked against the scanner-recorded byte length and SHA-256
digest. Source and build text must be valid UTF-8 and no larger than eight
mebibytes; each Java file is capped at 250,000 tokens. Changed, unsafe, oversized,
invalid, truncated, or malformed inputs fail closed or retain low confidence.

Vendored, conventional generated, minified, binary, build-output, and exact-copy
bodies retain common normalization precedence. Ordinary evidence and estimate
output contains paths, counts, classifications, declared coordinates/dependencies,
technology labels, and reasoning—not source excerpts, string values, comments,
credentials, build-script bodies, or generated output. Declared names and paths
remain repository metadata and should be handled according to the caller's
privacy requirements.

## Estimation and Change EHE

Java source structure consumes the existing `seed-rules/0.4.0`
`polyglot-source-backbone`. File/method/type/public-symbol/async/branch rates
transparently reuse analogous construction priors with wider uncertainty.
Specialized Java facts continue through the existing setup, architecture,
entry-point, API, data, integration, security, validation, background, testing,
build, manual-validation, and review rules. No Java-specific fitted rate, private
observation, dependency, or model artifact was added.

Change EHE `0.10.0` admits source-readable `.java` paths to a Java-aware token
comparison. Ordinary formatting and non-documentation comments can normalize to
zero. Javadoc/Markdown documentation comments, strings, text blocks,
character/numeric literals, identifiers, operators, delimiters, and Unicode-
escape ambiguity remain meaningful. Final
scanner/analyzer evidence routes Java changes through existing category
reconciliation. This experimental extension does not expand the admitted
4-to-32-hour Change band.

The standalone public Java mutation suite contains 13 project-authored MIT states
and 56 passing relations. It covers formatting/comments, exact duplication,
generated output, API, tests, data, integration, security, background work,
static build metadata, concurrency, and local framework namesakes. It is
qualitative invariance, directionality, isolation, and false-positive evidence—not
reviewed hour labels, held-out accuracy evidence, interval calibration, or
admission.

## Performance checkpoint

On the documented August 11, 2026 workstation, a fresh process analyzed a
generated 10,000-file, 1,010,001-line Java tree plus one root POM in 13.954
seconds. The sampled process peak was 167.31 MiB and cumulative managed allocation
was 1,413.15 MiB. Target metadata was unchanged. The analyzer did not execute
target code, invoke a JVM/Maven/Gradle, install dependencies, or access the
network.

This is a many-small-files scalability checkpoint, not a frozen cross-platform
regression threshold or a representative distribution of real Java/build syntax.

## Explicit non-goals

This boundary does not implement a complete Java grammar or effective build
model; compile, type-check, link, package, or execute; resolve Maven/Gradle
dependencies, plugins, inheritance, variants, profiles, or toolchains; run
annotation processors, generators, tests, benchmarks, or target programs; inspect
bytecode/JARs; prove reflection, dependency injection, proxies, module access,
runtime registration, reachability, thread safety, deadlock freedom, API/data/
security correctness, or production readiness. General semantic-clone and
generated-customization analysis remain separate safety decisions.
