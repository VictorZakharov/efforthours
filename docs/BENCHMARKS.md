# EffortHours benchmarks

EffortHours records reproducible engineering checkpoints rather than presenting a
single synthetic run as a universal performance guarantee.

## Common scanner v0.2 checkpoint

Measured on August 5, 2026 with:

- EffortHours common scanner `0.2.0`;
- .NET runtime `10.0.7` and .NET SDK `10.0.203`;
- Windows `10.0.26200`, x64;
- 24 logical processors exposed to the process; and
- a generated repository containing 10,000 small C# files with 100 physical lines
  per file, plus one SDK-style project file.

Command:

```text
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --warm-cache
```

Observed result:

| Measure | Result |
| --- | ---: |
| Requested source lines | 1,000,000 |
| Source files | 10,000 |
| Fixture generation | 1.454 s |
| Full repository scan | 4.275 s |
| Evidence serialization | 0.116 s |
| Full-scan throughput | 233,900 lines/s |
| Managed bytes allocated during full scan | 159.96 MiB |
| Evidence JSON size | 9.91 MiB |
| Evidence facts | 10,005 |
| Unchanged warm-cache scan | 1.646 s |
| Managed bytes allocated during warm-cache scan | 119.69 MiB |

Fixture generation is excluded from scan time. The full scan streamed every
included file, computed SHA-256, classified it, and built the in-memory evidence
document. The harness then populated the explicit external cache and timed an
unchanged warm-cache scan. The full and cached scans produced the same source
digest.

## Interpretation and limitations

This checkpoint demonstrates that the common scanner is well inside the original
"one million lines in a few minutes" objective for a many-file synthetic tree. It
does not establish a release threshold or predict performance for monorepos with
very large binaries, slow/network filesystems, deep ignore rules, unusual links,
or future compiler-based semantic analysis.

The run was not a controlled cold-cache benchmark, allocated bytes are cumulative
managed allocations rather than peak resident memory, and the generated files are
uniform. The August 9 checkpoint below adds fresh-process/cache-labeled runs,
sampled peak working set, mixed fixtures, and initial redistributable real-source
measurements; repeated controlled OS-cache runs remain future work.

## Static .NET analyzer v0.3 checkpoint

Measured on August 5, 2026 with the same environment and generated repository as
the common-scanner checkpoint. This mode runs the common inventory followed by
static project parsing and a Roslyn syntax pass over every maintained C# file. It
does not evaluate MSBuild, restore or compile the fixture, or execute target code.

Command:

```text
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --dotnet
```

Observed result:

| Measure | Result |
| --- | ---: |
| Requested source lines | 1,000,000 |
| Source files | 10,000 |
| Fixture generation | 1.461 s |
| Common scan plus static .NET analysis | 6.608 s |
| Evidence serialization | 0.107 s |
| Analysis throughput | 151,327 lines/s |
| Managed bytes allocated during analysis | 616.47 MiB |
| Evidence JSON size | 9.91 MiB |
| Evidence facts | 10,008 |

Fixture generation is excluded from analysis time. The benchmark is explicitly
invoked and disk-backed; it is not part of ordinary unit-test runs. Unit repository
and cache fixtures are memory-backed to avoid repeated test-tree I/O.

The generated source is deliberately syntax-simple, so this checkpoint establishes
scalability for many files rather than a universal semantic-complexity threshold.
The August 9 checkpoint below adds mixed shapes and sampled peak working set.

## Static JavaScript/TypeScript analyzer v0.4 checkpoint

Measured on August 5, 2026 with the same runtime, operating system, and hardware as
the earlier checkpoints. The generated repository contains one root package and
10,000 source files with 100 physical lines each. Files alternate between
JavaScript, which takes the Acornima AST path, and TypeScript, which takes the
bounded token path. The benchmark does not run Node, a package manager, a
transpiler, executable configuration, or target code.

Command:

```text
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --javascript
```

Observed result:

| Measure | Result |
| --- | ---: |
| Requested source lines | 1,000,000 |
| Source files | 10,000 |
| Fixture generation | 2.291 s |
| Common scan plus static JS/TS analysis | 13.303 s |
| Evidence serialization | 0.118 s |
| Analysis throughput | 75,169 lines/s |
| Cumulative managed bytes allocated during analysis | 1,715.82 MiB |
| Evidence JSON size | 10.00 MiB |
| Evidence facts | 10,010 |

Fixture generation is excluded from analysis time. This explicitly invoked,
disk-backed benchmark is not part of ordinary unit-test runs. The ordinary unit
suite uses only memory-backed repository and cache fixtures.

The allocation figure is cumulative allocation, not peak live memory. Half of the
files deliberately contain many exported JavaScript declarations, so this run
exercises substantial AST allocation. The newer checkpoint below adds sampled
peak working set plus mixed generated and curated real-source measurements.

## Peak working-set and mixed-repository v0.2.1 checkpoint

Measured on August 9, 2026 with:

- common scanner `0.2.1`, .NET analyzer `0.3.2`, and JavaScript analyzer `0.4.1`;
- .NET runtime `10.0.7` and .NET SDK `10.0.203`;
- Windows `10.0.26200`, x64;
- an AMD Ryzen 9 5900X with 12 physical cores and 24 logical processors; and
- 127.9 GiB installed memory.

This is a local developer workstation, not a memory-constrained runner. Each
full-scan row below starts a fresh process over a newly generated tree. Generation
immediately precedes analysis, so these are fresh-process full scans, not controlled
OS cold-cache measurements. The explicit warm-cache result populates EffortHours's
external scan cache and then measures an unchanged scan in the same process.

Commands:

```text
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --dotnet
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --javascript
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --mixed --warm-cache
```

The mixed fixture divides its 10,000 source files deterministically among C#,
JavaScript, TypeScript, C, and C++ and includes project, package, and CMake
manifests.

| Full-scan mode | Text lines | Scan | Lines/s | Managed allocation | Sampled peak working set | Evidence JSON |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Static .NET | 1,000,001 | 7.083 s | 141,179 | 619.32 MiB | 272.52 MiB | 9.94 MiB |
| Static JavaScript/TypeScript | 1,000,001 | 12.088 s | 82,728 | 1,723.34 MiB | 185.69 MiB | 10.03 MiB |
| Static mixed | 996,004 | 13.214 s | 75,375 | 1,368.67 MiB | 205.70 MiB | 9.99 MiB |

The mixed run's measured warm-cache pass took 6.197 seconds, cumulatively allocated
1,315.00 MiB, and reached a sampled 416.21 MiB process working set. That absolute
warm peak is not comparable to a fresh process: it follows the full scan and an
untimed cache-population pass, so its starting working set was already 340.62 MiB.
The cache avoids unchanged common-file inspection; ecosystem analyzers still parse
their retained source evidence.

Fresh-process mixed samples at 250,000, 500,000, and 1,000,000 requested lines
recorded sampled scan peaks of 98.61, 136.61, and 205.70 MiB respectively. The
corresponding cumulative managed allocations were 345.79, 686.34, and 1,368.67 MiB.
These points demonstrate bounded resident behavior for this fixture while also
showing why cumulative allocation is not a peak-memory proxy. They are too few and
too uniform to establish an asymptotic guarantee.

### Curated real-source and project-tree checks

The repository-input mode was also run over the exact, SHA-256-verified MIT release
archives already recorded in
[`calibration/corpora/public-expansion/SOURCES.md`](../calibration/corpora/public-expansion/SOURCES.md):
developit/mitt `3.0.1`, Tyrrrz/CliWrap `3.10.4`, and nanostores/nanostores `1.4.2`.
Their extracted trees were placed under one temporary parent to exercise a mixed
.NET, JavaScript, and TypeScript collection; no archive or extracted source was
committed.

| Dataset | Included files | Text lines | Full scan | Allocation | Sampled peak | Warm scan | Target unchanged |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Three verified MIT releases | 168 | 16,823 | 0.637 s | 26.17 MiB | 71.70 MiB | 0.147 s | yes |
| EffortHours development tree | 591 | 135,720 | 1.184 s | 74.62 MiB | 81.54 MiB | 0.561 s | yes |

The curated-release collection produced source digest
`sha256:09b545d80b5294faac3655e843236d29e41adab4f7ba747e45f76a7e0a08560a`.
The EffortHours reading includes the current project-authored mixed tree; ignored
Git/build/artifact entries remain part of the safety fingerprint but not analyzed
source totals. These are realistic safety and latency checks, not a representative
population or an estimator-accuracy benchmark.

### Safety and measurement boundary

The harness samples process resident working set every 10 milliseconds only during
the measured scan. It records cumulative managed allocation separately. Before and
after analysis it hashes the normalized path, attributes, length, and last-write
timestamp of every target-tree entry, including excluded `.git` and build entries,
without following reparse points. All runs above retained the same metadata digest.
This detects ordinary target writes, additions, and removals; it is not an
adversarial content-integrity proof.

The benchmark calls only the static scanner pipeline. It does not execute target
code, start target tools, install dependencies, or invoke network operations. Its
optional cache is outside caller-supplied repositories. A process-level E2E smoke
test covers mixed generation, memory fields, repository input, cache placement,
and byte-identical target contents. Ordinary unit tests remain memory-only and do
not run this disk-backed benchmark.

No regression threshold is frozen from this checkpoint. The measurements cover
one workstation, uniform large fixtures, three small public releases, and one
project tree; repeated cross-platform samples and larger realistic monorepos are
still required before a threshold can distinguish regressions from environment
variance.

## Static Python analyzer v0.1.0 checkpoint

Measured on August 12, 2026 with the same Windows/.NET/hardware environment as the
fresh-process checkpoint, common scanner `0.2.3`, and Python analyzer `0.1.0`.
The generated repository contains one root `pyproject.toml` and 10,000 `.py` files
with approximately 100 physical lines each. Each file contains simple assignments
plus one async function so the run exercises scanning, digest verification,
bounded tokenization, indentation structure, evidence construction, and JSON
serialization without invoking Python.

```text
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --python
```

Observed fresh-process result:

| Measure | Result |
| --- | ---: |
| Requested source lines | 1,000,000 |
| Analyzed text lines | 1,000,003 |
| Source/metadata files | 10,001 |
| Fixture generation | 1.502 s |
| Common scan plus static Python analysis | 13.354 s |
| Evidence serialization | 0.123 s |
| Analysis throughput | 74,883 lines/s |
| Cumulative managed allocation | 804.09 MiB |
| Sampled peak working set | 109.63 MiB |
| Evidence JSON size | 10.06 MiB |
| Evidence facts | 10,007 |

The before/after target metadata digest was identical. The analyzer did not
execute target code, invoke Python, install dependencies, or access the network.
Fixture generation and serialization are timed separately from analysis.

This is a many-small-files scaling checkpoint, not a representative distribution
of Python syntax or a cross-platform regression gate. The uniform fixture does
not exercise large literals, deeply nested indentation, framework-heavy modules,
namespace packages, dynamic import patterns, or the eight-MiB/250,000-token per-
file safeguards. Process-level E2E coverage runs a smaller Python shape in a fresh
process and verifies the same read-only/offline signals.

## Static Go analyzer v0.1.0 checkpoint

Measured on August 11, 2026 with the same Windows/.NET/hardware environment as the
fresh-process checkpoint, common scanner `0.2.4`, and Go analyzer `0.1.0`. The
generated repository contains one root `go.mod` and 10,000 `.go` files with
approximately 100 physical lines each. Each file contains bounded declarations
and control flow so the run exercises scanning, digest verification, managed
tokenization, structure, evidence construction, and JSON serialization without
invoking the Go toolchain.

```text
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --go
```

Observed fresh-process result:

| Measure | Result |
| --- | ---: |
| Requested source lines | 1,000,000 |
| Analyzed text lines | 1,000,003 |
| Source/metadata files | 10,001 |
| Fixture generation | 1.470 s |
| Common scan plus static Go analysis | 6.577 s |
| Evidence serialization | 0.111 s |
| Analysis throughput | 152,057 lines/s |
| Cumulative managed allocation | 736.15 MiB |
| Sampled peak working set | 119.95 MiB |
| Evidence JSON size | 10.27 MiB |
| Evidence facts | 10,107 |

The before/after target metadata digest was identical. The analyzer did not
execute target code, invoke the Go toolchain, install dependencies, or access the
network. Fixture generation and serialization are timed separately from analysis.

This is a many-small-files scaling checkpoint, not a representative distribution
of Go syntax or a cross-platform regression gate. The uniform fixture does not
exercise build-tag matrices, cgo, assembly, large raw literals, complex generic
constraints, framework-heavy modules, or the eight-MiB/250,000-token per-file
safeguards. Process-level E2E coverage runs a smaller Go shape in a fresh process
and verifies the same read-only/offline signals.

## Static Java analyzer v0.1.0 checkpoint

Measured on August 11, 2026 with the same Windows/.NET/hardware environment as the
fresh-process checkpoint, common scanner `0.2.5`, and Java analyzer `0.1.0`. The
generated repository contains one root `pom.xml` and 10,000 `.java` files with
approximately 100 physical lines each. Each source contains a package, a generic
class, bounded fields, and one method so the run exercises scanning, digest
verification, managed tokenization, structure, evidence construction, and JSON
serialization without invoking a JDK, Maven, or Gradle.

```text
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --java
```

Observed fresh-process result:

| Measure | Result |
| --- | ---: |
| Requested source lines | 1,000,000 |
| Analyzed text lines | 1,010,001 |
| Source/metadata files | 10,001 |
| Fixture generation | 1.461 s |
| Common scan plus static Java analysis | 13.954 s |
| Evidence serialization | 0.114 s |
| Analysis throughput | 72,379 lines/s |
| Cumulative managed allocation | 1,413.15 MiB |
| Sampled peak working set | 167.31 MiB |
| Evidence JSON size | 10.77 MiB |
| Evidence facts | 10,009 |

The before/after target metadata digest was identical. The analyzer did not
execute target code, invoke a JDK/Maven/Gradle, install dependencies, or access
the network. Fixture generation and serialization are timed separately from
analysis.

This is a many-small-files scaling checkpoint, not a representative distribution
of Java syntax or a cross-platform regression gate. The uniform fixture does not
exercise Maven reactors, Gradle multi-project builds, annotations, framework-heavy
applications, modules, text blocks, malformed syntax, or the eight-MiB/250,000-
token per-file safeguards. Process-level E2E coverage runs a smaller Java shape in
a fresh process and verifies the same read-only/offline signals.

## Static Kotlin/JVM analyzer v0.1.0 checkpoint

Measured on August 11, 2026 with the same Windows/.NET/hardware environment as the
fresh-process checkpoint, common scanner `0.2.6`, and Kotlin analyzer `0.1.0`. The
generated repository contains one root `build.gradle.kts` and 10,000 `.kt` files
with approximately 100 physical lines each. Each source contains a package, a
generic class, bounded properties, and one function so the run exercises scanning,
digest verification, managed tokenization, structure, evidence construction, and
JSON serialization without invoking a JDK, Kotlin compiler, Gradle, or Android
tooling.

```text
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --kotlin
```

Observed fresh-process result:

| Measure | Result |
| --- | ---: |
| Requested source lines | 1,000,000 |
| Analyzed text lines | 1,000,001 |
| Source/metadata files | 10,001 |
| Common scan plus static Kotlin analysis | 9.393 s |
| Analysis throughput | 106,459 lines/s |
| Cumulative managed allocation | 9,377.77 MiB |
| Sampled peak working set | 166.83 MiB |
| Evidence JSON size | 10.79 MiB |

The before/after target metadata digest was identical. The analyzer did not
execute target code, invoke a JDK/Kotlin compiler/Gradle/Android tool, run KSP or
kapt, install dependencies, or access the network. Fixture generation and
serialization are outside the reported analysis duration.

This is a many-small-files scaling checkpoint, not a representative distribution
of Kotlin syntax or a cross-platform regression gate. The uniform fixture does not
exercise mixed Java/Kotlin ownership, Maven builds, scripts, Android/Compose,
coroutines, compiler plugins, multiplatform source sets, malformed syntax, or the
eight-MiB/250,000-token per-file safeguards. Process-level E2E coverage runs a
smaller Kotlin shape in a fresh process and verifies the same read-only/offline
signals.

## Static Shell and PowerShell analyzer v0.1.0 checkpoints

Measured on August 11, 2026 with the same Windows/.NET/hardware environment as the
fresh-process checkpoint, common scanner `0.2.7`, and scripting analyzer `0.1.0`.
Each generated repository contains 10,000 maintained scripts with approximately
100 physical lines each. The Shell shape exercises functions, variable
assignments, conditionals, and literal built-ins; the PowerShell shape exercises
functions, parameters, assignments, conditionals, and cmdlets. Both runs include
scanning, digest verification, bounded managed tokenization, evidence
construction, and JSON serialization without starting a shell or resolving a
command/module.

```text
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --shell
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --powershell
```

Observed fresh-process results:

| Measure | Shell | PowerShell |
| --- | ---: | ---: |
| Requested source lines | 1,000,000 | 1,000,000 |
| Analyzed text lines | 990,000 | 990,000 |
| Source files | 10,000 | 10,000 |
| Fixture generation | 2.038 s | 1.813 s |
| Common scan plus static script analysis | 14.525 s | 13.689 s |
| Evidence serialization | 0.118 s | 0.145 s |
| Analysis throughput | 68,161 lines/s | 72,321 lines/s |
| Cumulative managed allocation | 622.53 MiB | 863.29 MiB |
| Sampled peak working set | 119.66 MiB | 130.76 MiB |
| Evidence JSON size | 10.08 MiB | 13.90 MiB |
| Evidence facts | 10,005 | 10,005 |

Each before/after target metadata digest was identical. Neither analyzer executed
target code, started a shell, installed dependencies, or accessed the network.
Fixture generation and serialization are timed separately from analysis.

These are many-small-files scaling checkpoints, not representative distributions
of shell dialects, platform commands, PowerShell types, module manifests, sourced
graphs, here-documents/here-strings, malformed syntax, or real automation
invocation graphs. They are not cross-platform regression gates. Process-level E2E
coverage runs smaller fresh-process Shell and PowerShell shapes and verifies the
same read-only/offline signals.

## Static Terraform/HCL analyzer v0.1.0 checkpoint

Measured on August 12, 2026 with the same Windows/.NET workstation, a fresh
process, common scanner `0.2.8`, and Terraform/HCL analyzer `0.1.0`.

```text
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --terraform
```

| Measure | Terraform/HCL |
| --- | ---: |
| Requested/analyzed lines | 1,000,000 |
| Included files | 10,000 |
| Included bytes | 18,000,000 |
| Fixture generation | 1.439 s |
| Full static scan | 8.239 s |
| Evidence serialization | 0.259 s |
| Full-scan throughput | 121,371 lines/s |
| Managed allocation | 3,867.00 MiB |
| Sampled peak working set | 303.72 MiB |
| Evidence JSON | 34.32 MiB |
| Evidence facts | 20,304 |

The fixture uses maintained `.tf` resource blocks with bounded attributes. Target
metadata retained the same digest before and after analysis. The benchmark did not
execute Terraform or target code, install dependencies/providers/modules, contact
a backend or network, or write into the target. This single synthetic workstation
measurement is a reproducible checkpoint, not a frozen cross-platform threshold
or a claim about native-parser/provider performance.

Process-level benchmark coverage also runs a smaller `--terraform` shape and
asserts static safety signals, positive line/memory measurements, and unchanged
target metadata.

## Static PHP/Composer analyzer v0.1.0 checkpoint

Measured on August 12, 2026 with the same Windows/.NET workstation, a fresh
process, common scanner `0.2.9`, and PHP analyzer `0.1.0`.

```text
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --php
```

| Measure | PHP/Composer |
| --- | ---: |
| Requested/analyzed lines | 1,000,000 / 1,000,001 |
| Included files | 10,001 |
| Included bytes | 32,430,077 |
| Fixture generation | 1.832 s |
| Full static scan | 7.920 s |
| Evidence serialization | 0.109 s |
| Full-scan throughput | 126,270 lines/s |
| Managed allocation | 935.89 MiB |
| Sampled peak working set | 441.57 MiB |
| Evidence JSON | 9.94 MiB |
| Evidence facts | 10,008 |

The fixture uses one strict static `composer.json` and maintained `.php` source
with bounded declarations and qualified framework calls. Target metadata retained
the same digest before and after analysis. The benchmark did not execute PHP,
Composer, target code, package scripts, autoloaders, framework bootstraps, or tests;
install dependencies; access the network; or write into the target. This single
synthetic workstation measurement is a reproducible checkpoint, not a frozen
cross-platform threshold or a claim about native-parser/runtime performance.

Process-level benchmark coverage also runs a smaller `--php` shape and asserts
static safety signals, positive line/memory measurements, and unchanged target
metadata.

## Static Rust/Cargo analyzer v0.1.0 checkpoint

Measured on August 12, 2026 with the same Windows/.NET workstation, a fresh
process, common scanner `0.2.10`, and Rust analyzer `0.1.0`.

```text
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --rust
```

| Measure | Rust/Cargo |
| --- | ---: |
| Requested/analyzed lines | 1,000,000 / 1,000,004 |
| Included files | 10,001 |
| Included bytes | 27,300,076 |
| Fixture generation | 1.649 s |
| Full static scan | 7.540 s |
| Evidence serialization | 0.109 s |
| Full-scan throughput | 132,627 lines/s |
| Managed allocation | 1,164.95 MiB |
| Sampled peak working set | 139.88 MiB |
| Evidence JSON | 9.96 MiB |
| Evidence facts | 10,008 |

The fixture uses one static `Cargo.toml` and maintained `.rs` source with bounded
declarations, async/concurrency, and qualified crate calls. Target metadata
retained the same digest before and after analysis. The benchmark did not execute
Cargo, rustc, build scripts, procedural macros, generators, target code, tests,
examples, or benchmarks; install dependencies; access the network; or write into
the target. This single synthetic workstation measurement is a reproducible
checkpoint, not a frozen cross-platform threshold or a claim about rustc/Cargo
performance.

Process-level benchmark coverage also runs a smaller `--rust` shape and asserts
static safety signals, positive line/memory measurements, and unchanged target
metadata.

## Static Docker/Compose analyzer v0.1.0 checkpoint

Measured on August 12, 2026 with the same Windows/.NET workstation, a fresh
process, common scanner `0.2.11`, and Docker analyzer `0.1.0`.

```text
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --docker
```

| Measure | Docker/Compose |
| --- | ---: |
| Requested/analyzed lines | 1,000,000 |
| Included files | 10,000 |
| Included bytes | 44,349,500 |
| Fixture generation | 1.560 s |
| Full static scan | 8.097 s |
| Evidence serialization | 0.483 s |
| Full-scan throughput | 123,502 lines/s |
| Managed allocation | 1,683.01 MiB |
| Sampled peak working set | 203.00 MiB |
| Evidence JSON | 53.84 MiB |
| Evidence facts | 20,003 |

The fixture alternates maintained Dockerfile variants and filename-qualified
Compose YAML with bounded logical instructions and service structure. Target
metadata retained the same digest before and after analysis. The benchmark did
not invoke Docker, Compose, BuildKit, a shell, container runtime, or target code;
pull images; expand build contexts; load includes/environment files; resolve
interpolation/secrets; install dependencies; access the network; or write into
the target. This single synthetic workstation measurement is a reproducible
checkpoint, not a frozen cross-platform threshold or a claim about native Docker,
BuildKit, YAML, or Compose performance.

Process-level benchmark coverage also runs a smaller `--docker` shape and asserts
static safety signals, positive line/memory measurements, and unchanged target
metadata.

## Static Jupyter analyzer v0.2.0 checkpoint

Measured on August 12, 2026 with the same Windows/.NET workstation, a fresh
process, common scanner `0.2.12`, and Python analyzer `0.2.0`.

```text
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --jupyter
```

| Measure | Jupyter |
| --- | ---: |
| Requested/analyzed physical lines | 1,000,000 / 1,120,003 |
| Included files | 10,001 |
| Included bytes | 30,018,949 |
| Fixture generation | 1.684 s |
| Full static scan | 7.561 s |
| Evidence serialization | 0.231 s |
| Full-scan throughput | 148,131 lines/s |
| Managed allocation | 1,347.94 MiB |
| Sampled peak working set | 159.33 MiB |
| Evidence JSON | 62.71 MiB |
| Evidence facts | 40,007 |

The fixture uses 10,000 maintained Python notebooks with Markdown and bounded
code-cell arrays plus one static `pyproject.toml`. Target metadata retained the
same digest before and after analysis. The benchmark did not launch Jupyter or a
kernel, execute cells or target code, read outputs, install dependencies, access
the network, or write into the target. This single synthetic workstation
measurement is a reproducible checkpoint, not a frozen cross-platform threshold,
a realistic scientific workload, or a claim about notebook correctness.

Process-level benchmark coverage also runs a smaller `--jupyter` shape and asserts
static safety signals, positive line/memory measurements, and unchanged target
metadata.

## Static C and C++ analyzer v0.1.0 checkpoint

Measured on August 12, 2026 with the same Windows/.NET workstation, fresh
processes, common scanner `0.2.13`, and C/C++ analyzer `0.1.0`.

```text
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --c
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --cpp
```

| Measure | C | C++ |
| --- | ---: | ---: |
| Requested/analyzed lines | 1,000,000 / 990,002 | 1,000,000 / 990,002 |
| Included files | 10,001 | 10,001 |
| Included bytes | 20,118,974 | 20,070,084 |
| Fixture generation | 1.566 s | 1.587 s |
| Full static scan | 8.197 s | 9.716 s |
| Evidence serialization | 0.110 s | 0.135 s |
| Full-scan throughput | 120,781 lines/s | 101,891 lines/s |
| Managed allocation | 1,351.13 MiB | 1,357.17 MiB |
| Sampled peak working set | 127.65 MiB | 128.84 MiB |
| Evidence JSON | 9.79 MiB | 9.98 MiB |
| Evidence facts | 10,008 | 10,008 |

The fixtures use one static CMake descriptor and 10,000 maintained C or C++
source files with bounded declarations, branches, and qualified standard-library
structure. Target metadata retained the same digest before and after each
analysis. The benchmark invoked no compiler, preprocessor, linker, build system,
package manager, generator, native parser, tests, or target code; read no system
headers; installed no dependencies; accessed no network; and wrote nothing into
the target. These two single-workstation synthetic measurements are reproducible
checkpoints, not frozen cross-platform thresholds or claims about native compiler
or build performance.

Process-level benchmark coverage also runs smaller `--c` and `--cpp` shapes and
asserts static safety signals, positive line/memory measurements, and unchanged
target metadata.

## Static frontend-assets analyzer v0.5.1 checkpoint

Measured on August 13, 2026 with the same Windows/.NET workstation, a fresh
process, common scanner `0.2.13`, and JavaScript/frontend analyzer `0.5.1`.

```text
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --frontend
```

| Measure | HTML/CSS/SCSS frontend assets |
| --- | ---: |
| Requested/analyzed lines | 1,000,000 / 1,000,001 |
| Included files | 10,001 |
| Included bytes | 87,848,408 |
| Fixture generation | 1.566 s |
| Full static scan | 8.187 s |
| Evidence serialization | 0.451 s |
| Full-scan throughput | 122,151 lines/s |
| Managed allocation | 2,113.33 MiB |
| Sampled peak working set | 270.67 MiB |
| Evidence JSON | 55.65 MiB |
| Evidence facts | 23,344 |

The fixture uses one static `package.json` and 10,000 content-distinct maintained
files distributed deterministically across HTML, CSS, and SCSS. It exercises
digest verification, bounded template/style structure, accessibility-relevant
HTML, responsive-style inputs, and evidence construction without rendering a UI
or running a framework or preprocessor. Target metadata retained the same digest
before and after analysis.

This is a many-small-files scaling checkpoint, not a representative React,
Angular, Vue, Svelte, design-system, or browser workload and not a cross-platform
regression gate. Process-level coverage runs a smaller `--frontend` shape and
asserts positive measurements, offline safety signals, and unchanged target
metadata.

## Static SQL analyzer v0.1.0 checkpoint

Measured on August 13, 2026 with the same Windows/.NET workstation, a fresh
process, common scanner `0.2.13`, and SQL analyzer `0.1.0`.

```text
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --sql
```

| Measure | SQL schema migrations |
| --- | ---: |
| Requested/analyzed lines | 1,000,000 |
| Included files | 10,000 |
| Included bytes | 24,140,000 |
| Fixture generation | 1.404 s |
| Full static scan | 8.421 s |
| Evidence serialization | 0.682 s |
| Full-scan throughput | 118,754 lines/s |
| Managed allocation | 907.83 MiB |
| Sampled peak working set | 156.46 MiB |
| Evidence JSON | 77.34 MiB |
| Evidence facts | 30,004 |

The fixture uses 10,000 content-distinct `.sql` schema migrations, each with one
bounded table definition and deterministic columns. It exercises scanning, digest
verification, token/statement analysis, schema evidence, and serialization without
connecting to a database or executing SQL. Target metadata retained the same
digest before and after analysis.

This is a many-small-files scaling checkpoint, not a representative database,
dialect, migration history, stored-program, or query workload and not a
cross-platform regression gate. Process-level coverage runs a smaller `--sql`
shape and asserts positive measurements, offline safety signals, and unchanged
target metadata.

## Change EHE scale-and-safety v1.0.0 checkpoint

Measured on August 10, 2026 with:

- Change estimator `change-seed/0.6.0+seed-rules/0.3.0`;
- benchmark protocol `change/1.0.0`;
- .NET runtime `10.0.7` and .NET SDK `10.0.203`;
- Windows `10.0.26200`, x64, with 24 logical processors; and
- fresh generated local Git repositories with automatic maintenance disabled.

The dedicated harness has two shapes. `large-tree` compares two immutable Git
snapshots containing 10,000 C# files of approximately 100 physical lines each.
`long-range` creates 32 base files and changes one bounded file through 128 commits,
so an untruncated audit analyzes 129 unique snapshots.

```text
dotnet benchmarks/EffortHours.ChangeBenchmarks/bin/Release/net10.0/EffortHours.ChangeBenchmarks.dll
  --tree --files 10000 --lines-per-file 100
  --max-seconds 30 --max-peak-mib 512

dotnet benchmarks/EffortHours.ChangeBenchmarks/bin/Release/net10.0/EffortHours.ChangeBenchmarks.dll
  --range --files 32 --lines-per-file 20 --commits 128
  --max-seconds 45 --max-peak-mib 192
```

Three fresh processes were measured for each shape:

| Shape | Repository-estimator calls | Analysis seconds min / median / max | Sampled peak MiB min / median / max | Cumulative allocation MiB min / median / max |
| --- | ---: | ---: | ---: | ---: |
| Million-line base/head | 2 | 16.975 / 17.181 / 17.187 | 357.16 / 357.54 / 357.80 | 3,642.24 / 3,642.43 / 3,642.61 |
| 128-commit audit | 129 | 26.916 / 27.003 / 27.024 | 109.20 / 113.67 / 114.02 | 600.00 / 600.07 / 600.21 |

The first ceiling is therefore 30 seconds and 512 MiB for the exact million-line
shape on this workstation. The second is 45 seconds and 192 MiB for the exact
128-commit shape. Supplying either ceiling makes the harness return exit code 3
when it is exceeded. These are local regression gates with deliberate run-to-run
margin, not cross-platform product guarantees.

### Unique-snapshot reuse and bounded audits

Range analysis caches repository evidence and repository estimates by immutable
snapshot object ID for the duration of one Change estimate. An adjacent `N`-commit
range therefore invokes the repository estimator `N + 1` times instead of `2N`.
Content-delta comparison still opens the exact immutable base/head pair for each
component; only redundant repository analysis is reused.

The optional per-commit reconciliation audit defaults to 256 components and has a
public hard ceiling of 1,024. The planner asks Git for at most `limit + 1` commit
IDs. When the limit is exceeded, diagnostic `FB5105` records the omission and the
complete final base-to-head Change estimate remains authoritative. A boundary run
with 257 commits and the default cap planned one final-delta component, performed
two repository-estimator calls, completed measured analysis in 0.860 seconds with
a 75.07 MiB sampled peak, and retained exact nonnegative allocation semantics.

### Read-only and measurement boundary

Fixture generation, Git commits, and before/after integrity hashing are outside the
analysis timer. The measured interval includes selector planning, immutable Git
snapshot access, repository analysis, Change evidence/work-item construction, and
reconciliation. Sampled peak working set covers the benchmark process every 10
milliseconds; cumulative managed allocation is reported separately. Short-lived
Git child-process memory is not included in that sampled process peak.

Before and after every run, the harness hashes normalized path, length,
last-write time, and complete bytes for both the worktree and the full `.git`
state. Every recorded run was unchanged. The benchmark does not execute target
code, install dependencies, or access the network. Process-level smoke tests cover
both shapes, threshold output, the 256-component omission path at a smaller test
limit, unique-snapshot counts, and exact read-only digests. Ordinary unit tests
remain memory-only.

## Change author-period nested-monorepository v1.1.0 checkpoint

Measured on August 14, 2026 with benchmark protocol `change/1.1.0`, .NET runtime
`10.0.7`, Windows `10.0.26200` x64, 24 visible logical processors, and Change
estimator `change-seed/0.18.1+seed-rules/0.4.0`.

The `author-period` fixture contains 29,225 nested C# source files, one project
file, eight synthetic fan-out files, an eight-branch octopus merge, and eight
consecutive qualifying non-merge commits by the selected identity. Each selected
commit modifies the same maintained path, so the fixture exercises exact
chronological snapshot reuse while retaining a merge-heavy reachable graph. The
head contains 29,234 files and 29,328 virtual directories.

```text
dotnet benchmarks/EffortHours.ChangeBenchmarks/bin/Release/net10.0/EffortHours.ChangeBenchmarks.dll
  --author-period --files 29225 --lines-per-file 8 --commits 8
  --max-seconds 30 --max-peak-mib 256
```

One fresh Release process produced:

| Selected changes | Repository-estimator calls | Analysis seconds | Sampled peak MiB | Cumulative allocation MiB | Result |
| ---: | ---: | ---: | ---: | ---: | --- |
| 8 | 9 | 12.681 | 130.40 | 301.68 | report completed; thresholds passed |

Fixture creation and complete before/after hashing are outside the 12.681-second
timer. They dominate total harness wall time because the benchmark deliberately
creates and deletes tens of thousands of physical directories and loose Git
objects. The measured interval includes author selection, immutable inventory
loading/derivation, changed-scope static analysis, Change estimation, and portfolio
reconciliation. The worktree and complete `.git` digest were unchanged; no target
code, dependency installation, or network access occurred.

### Before/after interpretation

The former virtual-filesystem traversal tested every known directory and file for
every visited directory. On this exact head shape, that is an estimated
`29,328 * (29,328 + 29,234) = 1,717,506,336` entry comparisons per snapshot, or
`15,457,557,024` across nine unique snapshots. This is a deterministic operation-
count estimate from the removed algorithm, not an hours-long wall-time rerun.

An anonymized field report on a different 29,225-file monorepository supplied the
external lower-bound context: three isolated author-period commands produced no
report after approximately ten minutes, and each process reached 1.10-1.24 GiB
working set. The synthetic after result is at least 47 times shorter than that
reported time floor and roughly 8-10 times smaller than the reported per-process
memory range. Because the repositories, processor visibility, and concurrency
differ, these ratios establish order of magnitude only; they are not a controlled
same-input speedup claim.

The local regression ceilings for this exact fixture are 30 seconds and 256 MiB.
They are not universal or cross-platform guarantees. Short-lived Git child memory
is not included in the sampled process peak, and concurrent independent processes
against one object database remain a separate measurement boundary.

## Change portfolio repository-reuse v1.2.0 checkpoint

Measured on August 14, 2026 with benchmark protocol `change/1.2.0`, the same .NET,
Windows, x64, 24-logical-processor environment, and unchanged
`change-seed/0.18.1+seed-rules/0.4.0` source estimator. This focused mechanism
check uses 1,025 requested nested source files, merge fan-out, and three qualifying
chronological commits. It is deliberately smaller than the v1.1.0 scale fixture so
the equivalent independent baseline can be measured rather than extrapolated.

```text
dotnet benchmarks/EffortHours.ChangeBenchmarks/bin/Release/net10.0/EffortHours.ChangeBenchmarks.dll
  --author-period --files 1025 --lines-per-file 8 --commits 3
  --compare-independent --max-seconds 30 --max-peak-mib 256
```

One fresh Release process measured the combined candidate batch first and then the
three equivalent isolated estimates against the same local object database:

| Measure | Combined | Equivalent isolated sum |
| --- | ---: | ---: |
| Candidate-analysis wall time | 1.189 s | 1.639 s |
| Repository-estimator calls | 4 | 6 |
| Git object readers | 1 | 6 by standalone snapshot ownership |
| Output semantics | baseline | byte-equivalent per-change reports |

Immutable commit planning was completed before both timed analysis paths. The
combined estimate was 1.38 times faster in this run. Its repository session
served two of six immutable-inventory requests and two of six exact snapshot-
analysis requests from cache, derived three inventories incrementally after one
full load, and served 17 of 22 blob requests from its bounded object cache. The
whole measured comparison interval was 3.671 seconds, allocated 41.68 MiB of
managed memory cumulatively, and sampled a 104.59-MiB peak working set. Worktree
and complete `.git` fingerprints were unchanged; target execution, dependency
installation, and network access were not performed.

The independent baseline runs second and can benefit from operating-system/Git
warmth, making the comparison conservative for the combined path. It does not
model several repositories or concurrent processes. The full multi-repository,
multi-head correctness/performance matrix and controlled concurrency measurements
are recorded below. Any universal threshold remains a separate future decision.

Ordinary CI uses an eight-file smoke form of this fixture and no wall-clock or
sampled-memory threshold. It gates deterministic report equivalence,
analysis/reuse counts, cache bounds, nested-tree behavior, and read-only/offline
safety. The in-memory unit suite covers the 1,024-file changed-scope boundary. The
1,025-file disk-backed comparison and its machine-dependent performance numbers
remain explicitly invoked benchmark checkpoints such as the command above.

## Multi-repository author-period matrix v1.3.0 checkpoint

Measured on August 14, 2026 with benchmark protocol `change/1.3.0`, .NET runtime
`10.0.7`, Windows `10.0.26200` x64, and unchanged estimator
`change-seed/0.18.1+seed-rules/0.4.0`. The machine is the documented AMD Ryzen 9
5900X workstation with 24 visible logical processors and 127.9 GiB installed
memory.

The public-safe synthetic fixture contains two local repositories cloned from one
shared history, 1,025 requested nested C# files per repository, four merge-fanout
branches, four pinned heads per repository, three contributor selectors, and
three qualifying commits per repository. The repositories intentionally contain
the same selected object ID, then diverge through default and open heads. One
contributor matches that shared object directly, a second matches its co-author
trailer, and a third selects nothing. The resulting portfolio has six unique
repository-scoped changes; the equivalent single-repository/single-head/
contributor workflow makes 24 invocations, eight of which are empty, and observes
20 repeated selected rows before external deduplication.

```text
dotnet benchmarks/EffortHours.ChangeBenchmarks/bin/Release/net10.0/EffortHours.ChangeBenchmarks.dll
  --author-period-manifest --files 1025 --lines-per-file 8 --commits 3
  --compare-independent
```

One fresh Release process measured the combined path first, the equivalent
isolated paths second against warm local object databases, and a reordered
combined manifest third. The combined interval includes manifest planning,
analysis, reconciliation, and serialization. The isolated interval includes each
head/contributor selection and analysis plus unique-report collection; exact
manual reconciliation is verified afterward, so this slightly favors the
baseline:

| Measure | Before: equivalent isolated invocations | After: one combined manifest | Change |
| --- | ---: | ---: | ---: |
| Invocation count | 24 | 1 | 95.8% fewer |
| Measured core wall time | 12.360 s | 2.878 s | 4.30x faster in this run |
| Parent-process CPU | 5.875 s | 3.500 s | 40.4% lower |
| Snapshot analyses | 36 | 8 | 77.8% fewer |
| Git object readers | 16 | 2 | 87.5% fewer |
| Selected rows before/after deduplication | 20 | 6 | six identical repository-scoped changes |

The complete measured comparison, including the reordered determinism run but
excluding fixture construction and before/after hashing, took 17.338 seconds,
allocated 150.72 MiB cumulatively, and sampled a 114.93-MiB process peak. The two
representative default heads contained 2,060 files and 2,256 virtual directories
in aggregate. The combined path served four of 12 snapshot-analysis requests and
four of 12 inventory requests from cache, loaded two full inventories, derived six
incrementally, and served 34 of 44 blob requests from its bounded caches.

The pre/post target fingerprints were:

| Scope | Files | Bytes | SHA-256 composite digest |
| --- | ---: | ---: | --- |
| Two worktrees | 2,060 | 467,907 | `ba21145f299555bfa643467c34317ea37f146c688f3a41bf3de2fd764964012e` |
| Two complete `.git` states | 4,476 | 856,946 | `ddf724db040a2c16b13d7c4e82061895570d3afcba91e2d039aa25387485c117` |

The combined report was byte-identical after repository, head, contributor, and
alias reordering and matched both every isolated per-change report and an exact
manual disjoint reconciliation at low, expected, and high range points. Shared
objects remained repository-scoped, overlapping heads and zero contributors were
preserved, local paths and raw aliases were absent, and both worktree and complete
`.git` fingerprints were unchanged. Target execution, dependency installation,
and network access were not performed.

The 4.30x timing and sampled memory are one-machine observations, not CI gates or
universal guarantees. The isolated path runs second and therefore receives the
warmer operating-system and Git caches. Parent-process CPU and sampled working set
exclude short-lived Git children. Ordinary CI uses an eight-file form of this
fixture and gates only deterministic semantics, exact equivalence, reuse counts,
privacy, cache bounds, and read-only/offline safety. Cross-platform repetition and
larger public repository shapes remain separate measurements.

## Author-period concurrency and public-tree v1.4.0 checkpoint

Measured on August 14, 2026 with benchmark protocol `change/1.4.0`, .NET runtime
`10.0.7`, Windows `10.0.26200` x64, and unchanged estimator
`change-seed/0.18.1+seed-rules/0.4.0`. The machine is the same AMD Ryzen 9 5900X
workstation with 24 visible logical processors and 127.9 GiB installed memory.

The controlled fixture uses the existing `Squidex/squidex` public-readiness
development snapshot. It is an MIT-licensed mixed .NET/JavaScript/TypeScript
multi-package monorepository pinned at commit
`0ecfe2fc6807a59f0cf67fdcb41bfa037b4fd60e` and Git tree
`a0bd64d3ace748fb13438fea5018a5ed5fc94ee1`. The cached source archive measured
SHA-256 `0e3dc1a69a9d5b0904749ad95261240fc5de9b9cc380c5618bc7dc2d3f3aae7d`.
These identities already belong to the frozen public-readiness provenance ledger;
the benchmark adds no calibration label and opens no validation or test holdout.

The harness copies the caller-supplied snapshot into a generated local Git
repository, skips links rather than following them, adds an isolated synthetic
benchmark path, creates eight merge-fanout branches, and selects three consecutive
non-merge commits. The source snapshot has 4,779 files and 36,609,446 bytes; the
generated head has 4,784 files and 689 virtual directories.

```text
dotnet benchmarks/EffortHours.ChangeBenchmarks/bin/Release/net10.0/EffortHours.ChangeBenchmarks.dll
  --author-period --source-tree <pinned-snapshot> --lines-per-file 8 --commits 3
  --process-matrix
```

One invocation ran fresh-process groups of one, two, and three independent
estimators against the same local object database. A start gate synchronized each
group. The groups ran in that fixed order, so later groups may benefit from warmer
operating-system and Git caches. Each worker measured its own analyzer interval
and peak working set:

| Concurrent processes | Group wall time | Slowest worker | Total worker CPU | Maximum per-worker peak | Peak vs. isolated |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 3.184 s | 3.040 s | 2.203 s | 145.32 MiB | 1.00x |
| 2 | 3.265 s | 3.108 s | 3.813 s | 145.86 MiB | 1.00x |
| 3 | 3.516 s | 3.362 s | 8.593 s | 146.13 MiB | 1.01x |

All six workers selected the same three immutable changes, produced the same exact
EHE and byte-equivalent deterministic report, and left the caller source,
generated worktree, and complete shared `.git` state unchanged. Three concurrent
workers increased the slowest-worker interval by 10.6% and maximum per-worker peak
by less than 1% in this observation. The group completed three reports in 1.10x
the isolated group wall time, so shared-object access neither serialized the runs
nor unexpectedly multiplied each process's memory on this fixture.

### Incident before/after context

The anonymized field observation remains a lower bound from a different private
repository: no report after approximately 600 seconds and 1.10-1.24 GiB working
set per process. A same-input rerun is unavailable, so it would be misleading to
claim a controlled speedup. The two public-safe after checkpoints establish the
order of magnitude without an hours-long reproduction:

| Evidence | Tree shape | Selected changes | Analyzer wall time | Peak working set | Result |
| --- | ---: | ---: | ---: | ---: | --- |
| Field lower bound before the traversal fix | approximately 29,225 files | narrow recent interval | more than 600 s | 1.10-1.24 GiB per process | no report |
| v1.1 synthetic after the traversal fix | 29,234 head files | 8 | 12.681 s | 130.40 MiB | report produced |
| v1.4 pinned public tree, isolated | 4,784 head files | 3 | 3.040 s | 145.32 MiB | report produced |

The v1.1 row remains the like-sized synthetic scale check; v1.4 supplies the
previously missing real-tree and controlled concurrency evidence. Neither row is a
same-input comparison with the private field workload.

### CI and safety boundary

Ordinary CI uses an eight-file form of the 1/2/3 matrix. It verifies exactly six
workers, byte-equivalent reports, exact selected-change/EHE preservation, one
shared object database, and unchanged/offline targets. A separate tiny fixture
verifies read-only caller-source copying. CI does not assert elapsed time, CPU,
working set, ratios, or a faster-than relation; `--process-matrix` rejects
`--max-seconds` and `--max-peak-mib` so machine-dependent observations cannot
become accidental gates.

The benchmark does not execute target code, install dependencies, or access the
network. Fixture generation and complete pre/post fingerprinting remain outside
worker intervals. Link targets are not followed, caller source paths are never
printed, `--keep` discloses only the generated repository path, and the public
source tree is unchanged.

## Change author-period closed-month v1.5.0 checkpoint

> Historical comparison correction: the v1.5 "equivalent independent" path
> expanded repository, head, and contributor combinations instead of running one
> manifest per contributor over the identical repository/head scope. Its `0.372`
> ratio measures the benefit over that older operator workflow, not combined-
> contributor reuse. Do not use it as the isolated-manifest speedup baseline. The
> corrected comparison and retained v1.5 history are documented in v1.6 below.

Measured on August 17, 2026 with benchmark protocol `change/1.5.0`, .NET runtime
`10.0.7`, Windows `10.0.26200` x64, 24 visible logical processors, source estimator
`change-seed/0.18.2+seed-rules/0.4.0`, and portfolio reconciler
`change-portfolio/0.2.2`. The machine is the same AMD Ryzen 9 5900X workstation
used by the earlier Change checkpoints.

The generated fixture has two local repositories, eight pinned heads, two matching
contributors plus one deterministic zero row, and 65 qualifying commits per
repository. Each base tree requests 1,025 source files, one root project, and 512
nested project-context files distributed between the changed path's ancestor and
unrelated directories. The two measured default heads contain 3,084 files
and 3,280 virtual directories in aggregate. The closed interval is one calendar
month, and the combined report selects 130 repository-scoped changes.

```text
dotnet benchmarks/EffortHours.ChangeBenchmarks/bin/Release/net10.0/EffortHours.ChangeBenchmarks.dll
  --author-period-manifest --files 1025 --context-projects 512
  --lines-per-file 8 --commits 65 --compare-independent
```

One invocation measured the combined portfolio, the equivalent contributor/
repository/head estimates, and a reordered determinism run against the same local
objects:

| Measure | Combined manifest | Equivalent independent estimates |
| --- | ---: | ---: |
| Estimate wall time | 8.908 s | 23.934 s |
| Combined/independent wall ratio | 0.372 | 1.000 |
| Snapshot analyses | 134 | 160 |
| Git object readers | 2 | 16 |
| Selected unique changes | 130 | 130 |
| Semantic result | baseline | exact contributor totals and unique changes matched |

The combined path served 126 of 260 snapshot-analysis requests and 126 of 260
immutable-inventory requests from bounded caches. It loaded four full inventories,
derived 130 incrementally, and served 930 of 1,064 blob requests from cache. The
complete estimate comparison, including independent and reordered runs, took
40.503 seconds, allocated 1,117.64 MiB cumulatively, and sampled a 142.68-MiB
process peak from a 59.07-MiB start. Cumulative allocation is churn over the full
suite, not simultaneously retained memory.

The two repository sessions overlap. Aggregate snapshot/diff phase time was
14.283 seconds and static-analysis time was 1.126 seconds; phase totals may exceed
combined wall time because concurrent repository work is added. Bounded
two-repository execution deliberately raised the earlier sequential development
observation's approximately 132-MiB process peak by about 11 MiB while roughly
halving its 18.526-second combined interval. That intermediate observation is
context, not a CI threshold or a separately shipped benchmark identity.

### Field before/after context

The anonymized field workload and generated fixture are not the same repository,
so the table establishes order of magnitude rather than a controlled speedup:

| Evidence | Selected work | Wall time | Working set | Result |
| --- | ---: | ---: | ---: | --- |
| Field contributor A before | more than 128 exact matches | 1.507 s | not material | rejected; no report |
| Field contributor B before | closed month | more than 300 s | 640.5 MiB sampled | terminated; no report |
| v1.5 generated after | 130 exact matches | 8.908 s combined | 142.68 MiB suite peak | report completed |

The deterministic suite separately constructs, reconciles, serializes, and
schema-validates 1,701 report rows so that a high-commit month cannot regress to a
presentation cap. The public calculation remains bounded by 10,000 identity
candidates per repository and the 32-repository manifest envelope; this is not a
calendar-month limit.

Every semantic invariant passed: independent contributor totals, exact manual
reconciliation, reordered report bytes, repository-scoped object identity,
overlapping-head deduplication, zero rows, and the privacy boundary. Worktrees and
complete Git states were unchanged; target execution, dependency installation,
and network access were not performed. A manifest inside one worktree also opens a
readable sibling repository in the process suite. An unreadable sibling in a
restricted caller environment is therefore an operating-system/process-access
failure, not an EffortHours containment rule.

No wall-time, ratio, allocation, or working-set value gates ordinary CI. CI checks
the 1,701-row contract, bounded two-repository coordination, linear-space
component construction, relevant-context selection, exact reuse counts,
progress/cancellation privacy, sibling-path acceptance, determinism, and
unchanged/offline targets.

## Change author-period hardening v1.6.0 checkpoint

Measured on August 17, 2026 with benchmark protocol `change/1.6.0`, .NET runtime
`10.0.7`, Windows `10.0.26200` x64, 24 visible logical processors, common scanner
`0.2.14`, source estimator `change-seed/0.18.2+seed-rules/0.4.0`, and portfolio
reconciler `change-portfolio/0.2.3`. The workstation is the same AMD Ryzen 9 5900X
host used by the earlier Change checkpoints.

The corrected fixture uses two repositories, eight pinned heads, two exclusive
matching contributors plus one zero row, and 65 qualifying commits per repository.
Each base requests 1,025 nested C# files and 512 distinct nested project-context
files; 80 projects per repository are relevant to the changed component. The two
representative heads contain 3,084 files and 3,122 virtual directories in
aggregate. The closed-month report selects 130 repository-scoped changes.

```text
dotnet benchmarks/EffortHours.ChangeBenchmarks/bin/Release/net10.0/EffortHours.ChangeBenchmarks.dll
  --author-period-manifest --files 1025 --context-projects 512
  --lines-per-file 8 --commits 65 --compare-independent
```

The timing order is explicit: an initial combined run warms the managed and local
Git paths and supplies the canonical report; the two isolated contributor
manifests are measured next; then a semantically equivalent reordered combined
manifest is measured. This compares both measured paths after the same initial
warm-up.
The initial combined time is retained separately because a new CLI process does
not receive that warm-up. Timing remains observational and non-gating.

### Intermediate repository-batch before/after

Two consecutive development checkpoints used the same deterministic fixture and
timing protocol. "Before" already includes indexed inventories, immutable
file-analysis caching, and bounded file concurrency; "after" additionally batches
eligible first-parent raw diffs and changed-blob sizes once per repository.
These columns compare two intermediate branch states. They do not compare the
published alpha.6 package with the final branch, do not represent the private
field workload, and do not establish a 10x end-to-end improvement.

| Measure | Before repository batching | After repository batching | Change |
| --- | ---: | ---: | ---: |
| Initial combined run | 11.066 s | 6.045 s | 45.4% lower |
| Warm controlled combined run | 9.473 s | 4.087 s | 56.9% lower |
| Two isolated contributor manifests | 10.405 s | 5.204 s | 50.0% lower |
| Combined / isolated wall ratio | 0.910 | 0.785 | combined is 21.5% faster after |
| Aggregate snapshot/diff phase | 14.561 s | 2.773 s | 81.0% lower |
| Complete comparison suite | 31.016 s | 15.413 s | 50.3% lower |
| Cumulative managed allocation | 6,714.17 MiB | 6,644.84 MiB | 1.0% lower |
| Sampled suite peak working set | 191.04 MiB | 201.36 MiB | 5.4% higher |

The suite peak spans the warm-up, both isolated manifests, and reordered combined
run; it is not the live retained size of one cache. The approximately 10-MiB peak
increase is the accepted bounded memory-for-latency tradeoff on this fixture, not
permission for unbounded retention. Cumulative allocation is churn across all
four estimates and is not simultaneous memory.

The after run derived all 130 eligible incremental inventories from repository-
level batches. The combined invocation used two Git object readers versus four
across isolated manifests, computed 426 unique immutable analysis artifacts versus
592 (28.0% fewer), and missed the blob cache 294 times versus 458 (35.8% fewer).
It requested the same 260 snapshot analyses and retained the same 126 exact hits;
the speedup therefore comes from less Git process churn and less immutable file
work, not from weakening a row's exact analysis-scope key.

Every semantic and safety invariant passed: isolated contributor rows and exact
manual reconciliation matched, reordered report bytes were identical, shared
object text remained repository-scoped, overlapping heads and the zero contributor
were preserved, report output omitted local paths and aliases, both worktrees and
complete Git states were unchanged, and target execution, dependency installation,
and network access were not performed.

### Other alpha.6 regressions

Repository-controlled ignore rules no longer construct runtime regular
expressions. A bounded deterministic glob matcher covers supported ignore syntax,
rejects oversized or invalid rules with a digest-only diagnostic, and passes a
50-repetition concurrent stress test. This removes the observed symbolic-regex
CLR crash path rather than attempting to catch a native access violation after it
occurs.

Manifest repository validation now distinguishes a privacy-safe Git
`safe.directory` ownership rejection, non-repository input, and unsupported
repository format instead of reducing every case to "not readable." Contributor
tables and diagnostics now label normalized values as joint portfolio allocations:
exclusive commit matches do not imply allocation invariance when another
contributor changes repository-level reconciliation. Stable isolated row sums and
the exact signed allocation adjustments remain visible.

### Field boundary and CI policy

The anonymized alpha.6 field run selected 255 changes from approximately 41,793
reachable commits and a dominant roughly 29,000-file/663-MiB tree. It took
219.33 seconds and sampled 693.45 MiB for the combined manifest. That private
workload is materially larger and structurally different from this generated
fixture, so applying either the intermediate 56.9% reduction or the representative
3.01x result to predict a field result would be unsupported.

The first same-input alpha.7 field retest established one controlled result but
could not complete the comparison matrix:

| Run | Alpha.6 | Alpha.7 | Result |
| --- | ---: | ---: | --- |
| Contributor A wall time | 143.033 s | 29.189 s | 79.6% lower; 4.9x faster |
| Contributor A expected EHE | 686.17 h | 686.17 h | exact semantic match |
| Contributor A observed peak | 658.38 MiB | 1,432.46 MiB | 2.18x; field review remains required |
| Contributor B | 60.233 s success | batch-diff failure | blocked by a selected empty commit |
| Combined A+B | 219.330 s success | batch-diff failure | blocked by the same empty commit |

Alpha.8 preserves selected empty one-parent commits as valid zero-path
transitions, closing that deterministic correctness regression. The original
closed-month engineering work is otherwise covered by the 1,701-row calculation
regression, exact combined/isolated and reorder semantics, bounded reuse and
repository concurrency, the 50-repetition ignore-rule stress case, actionable
Git ownership diagnostics, cancellation diagnostics, and unchanged/offline target
checks. GitHub issue #157 therefore closes as an engineering implementation
record. Issue #176 owns the unchanged private alpha.8 A, B, and A+B field retest;
the private manifests remain outside the repository.

Closing the engineering issue does not establish the desired 10x field result.
That claim still requires approximately 21.9 seconds or less against the
219.33-second combined baseline. The field retest must report phase times,
batched-inventory coverage, unique artifact/blob counts, byte traffic, combined
versus isolated wall ratio, and peak working set. A miss is a measurement to
diagnose, not permission to reinterpret the generated fixture as field evidence.

Ordinary CI does not assert wall time, memory, or a faster-than ratio. It gates the
bounded batch parser, exact combined/isolated semantics, deterministic operation
and reuse counts, canonical reorder invariance, privacy, cancellation, and
unchanged/offline targets. Explicit benchmark checkpoints retain timing and memory
so performance can be reviewed without turning normal machine variance into a
release failure.

## Change author-period structural reuse v1.7.0 checkpoint

Measured on August 17, 2026 with .NET runtime `10.0.7`, Windows `10.0.26200` x64,
and 24 visible logical processors, this same-input checkpoint compares the
published alpha.6 executable with the final hardening branch. The deterministic
local manifest contains two repositories, eight pinned heads, two exclusive
contributors, 31,010 aggregate tree files, 256 selected changes, and 512
snapshot-analysis requests. Every generated commit changes 14 distinct small C#
blobs, creating 3,254 unique blob objects and enough interleaved snapshots to
exceed the former 16-inventory retention limit. Both executables ran sequentially
against the same immutable Git objects on the same workstation.

| Measure | Published alpha.6 | Final branch | Change |
| --- | ---: | ---: | ---: |
| End-to-end wall time | 31.531 s | 10.490 s | 66.7% lower; 3.01x faster |
| Aggregate snapshot/diff phase | 49.681 s | 6.082 s | 87.8% lower; 8.17x faster |
| Git blob requests | 54,816 | 33,696 | 38.5% lower |
| Full inventories built | 4 | 2 | 50.0% lower |
| Inventory evictions | 228 | 0 | eliminated |
| Observed peak working set | 208.39 MiB | 237.18 MiB | 13.8% higher |

Both reports contain the same 256 stable item IDs and the same
`9.36 / 16.46 / 31.96` low/expected/high EHE. The final run retained 258 unique
inventory objects as structurally shared states across one full-tree root lineage
per repository. Its two lazy metadata readers served 42,660 length requests over
190 unique objects with 42,470 cache hits and no eviction. The approximately
29-MiB peak increase is an accepted bounded memory-for-latency tradeoff on this
fixture, not a general memory allowance.

This fixture is useful for exact regression and scaling checks, but its source
bodies are deliberately tiny and its reachable history is shallow. It does not
reproduce the private field workload's roughly 663 MiB tree or 41,793 reachable
commits. The defensible same-fixture result is therefore 3.01x end to end, not
10x. The snapshot/diff subsystem itself is now in the expected order of magnitude,
but issue #176 must rerun the same private manifest and measure no more than
approximately 21.9 seconds against alpha.6's 219.33-second result before a 10x
claim is made. Protocol v1.7.0 also records the lazy object-metadata reader counters
and canonical structurally shared inventory behavior. These wall-time and sampled-
memory observations remain explicit benchmark evidence, never ordinary CI gates.

## Change author-period pipelining and CPU scaling v1.8.0 checkpoint

Measured on August 18, 2026 with .NET runtime `10.0.7`, Windows
`10.0.26200` x64, a 12-core/24-logical-processor Ryzen 9 5900X, workstation GC,
and no competing `eh` process. Fixture construction completed before measurement;
the measured command reopened only the prepared immutable objects. The CPU-heavy
fixture contains two repositories, eight pinned heads, three requested contributor
rows, 12 selected changes, 14 unique snapshot analyses, 1,290 aggregate head
files, 1,000 lines per synthetic C# file, 2,338 unique immutable file-analysis
artifacts, and 41,271,446 Git bytes read.

The benchmark-only worker setting controls the shared admission budget for common
file inspection, semantic file analysis, and thread-safe repository estimation.
It does not constrain all process work or child Git processes, so the one-worker
row is an admitted-work baseline, not a claim that the whole process used exactly
one core.

| Measure | 1 worker | 12 workers | 24 workers |
| --- | ---: | ---: | ---: |
| Combined wall time | 5.515 s | 4.102 s | 3.830 s |
| Combined analysis wall time | 4.475 s | 3.089 s | 2.820 s |
| Managed process CPU time | 8.672 s | 19.906 s | 22.828 s |
| Managed average processor equivalents | 1.572 | 4.853 | 5.961 |
| Average active admitted work | 0.703 | 6.893 | 10.158 |
| Maximum active admitted work | 1 | 12 | 24 |
| Managed allocation | 1,529.11 MiB | 1,552.18 MiB | 1,562.06 MiB |
| Sampled peak working set | 207.94 MiB | 227.68 MiB | 234.11 MiB |

Twelve workers reduce whole-command wall time by 25.6% (`1.34x`) and analysis
wall time by 31.0% (`1.45x`) versus one admitted worker. Twenty-four workers
reduce them by 30.6% (`1.44x`) and 37.0% (`1.59x`). This is not near-linear
physical-core scaling. The gate reaches its configured maximum, proving that work
exists and is admitted concurrently, but fixed Git/history work and
allocation-heavy Roslyn/estimator processing make each concurrent operation more
expensive. Aggregate lease occupancy is wall time summed across leases, not CPU
time, and must not be presented as processor consumption.

Indexing evidence facts by kind and memoizing file-to-scope ownership reduced the
one-worker repository-estimation lease from 1.145 seconds to 0.426 seconds on the
same fixture (62.8% lower). Filtering irrelevant retained Roslyn identifiers and
counting only data-relevant properties further reduces unnecessary syntax
retention without changing evidence. These are algorithmic wins, but parser and
repository aggregation remain the dominant scaling boundary.

A separate prepared 2,048-file/1,000-line .NET scanner diagnostic excludes its
0.314-second fixture construction from measurement. Moving small-file reads from
the traversal producer into its bounded workers lowers one controlled 12-worker
scan from 1.552 to 1.415 seconds (8.8%) and raises average managed processor use
from 3.605 to 3.909. Increasing one scan beyond four read workers produced no
further wall-time benefit, so the local cap remains four while a separate
process-wide read budget bounds content buffered across concurrent snapshots.
This isolates a real serial producer cost without presenting a single observation
as a universal speedup.

A separate prepared 128-change fixture checks phase overlap. Moving from one
repository-wide delta barrier to deterministic 16-row delta chunks feeding four
consumers changed wall time from 6.104 to 5.969 seconds (2.2% lower), while
aggregate diff construction rose from 0.830 to 1.445 seconds because more bounded
Git batches were opened. This small-diff fixture therefore establishes the
producer/consumer behavior and exact output, not a broad speedup claim; the field
retest must decide whether overlap repays batch startup on large deltas.

The prepared repository-scale diagnostic contains 31,034 aggregate files and six
selected changes. It completes in 4.178 seconds, samples 140.16 MiB, and consumes
3.812 managed CPU-seconds, or 0.912 average managed processors. Only 186 admitted
CPU work items exist because changed-scope analysis correctly avoids parsing the
full tree. Its low CPU use is consequently a real full-inventory/Git/aggregation
scalability signal, not evidence that more parser workers would help.

Protocol v1.8.0 records per-kind admission counts, occupied and wait time, managed
CPU and GC observations, and Git-reader CPU/occupied/wait time. CI continues to
gate deterministic semantics, producer/consumer overlap, single-flight reuse,
bounded configuration, privacy, and unchanged targets only. Wall time, CPU,
allocation, and sampled memory remain non-gating checkpoints. Near-linear core
scaling is explicitly unresolved.

## Change author-period work-elimination and tree scaling v1.9.0 checkpoint

Measured on August 19, 2026 on the same Windows, .NET 10.0.7, Ryzen 9 5900X,
workstation-GC host as v1.8.0, with no competing `eh` process. Both fixtures were
constructed before measurement and reopened from their immutable descriptors.
The command shape was:

```text
dotnet run --project benchmarks/EffortHours.ChangeBenchmarks \
  --configuration Release --no-build -- \
  --author-period-manifest --prepared-fixture <descriptor> \
  --combined-only --file-analysis-workers <1|2|4|6|8|12>
```

Protocol v1.9.0 adds full-inventory read/projection timings and SHA-256 digests for
the complete report and its estimate-bearing semantic projection. Report bytes may
legitimately differ when the explicit worker configuration changes its execution
diagnostic; the estimate semantic digest stayed exactly
`04ce6c51699f446ac61feffc1fdc9a43d4dd36073c454cacec680792e6a85e5f`
throughout the six-point curve.

The implementation applies the exact changed/context/representative analysis
scope to every immutable Git change instead of analyzing complete small snapshots,
retains the complete common scanned-file fact in the immutable artifact cache,
avoids clean-C# diagnostic enumeration and duplicate callable traversal, and
buffers the repository-scoped Git object stream. This algorithmically removes the
former CPU-heavy slice rather than attempting to distribute all of it across 12
cores.

| Measure | v1.8.0, 1 worker | v1.9.0, 1 worker | Change |
| --- | ---: | ---: | ---: |
| Combined wall time | 5.515 s | 2.215 s | 59.8% lower |
| Combined analysis wall time | 4.475 s | 1.185 s | 73.5% lower |
| Managed process CPU time | 8.672 s | 2.219 s | 74.4% lower |
| Managed allocation | 1,529.11 MiB | 251.96 MiB | 83.5% lower |
| Sampled peak working set | 207.94 MiB | 111.97 MiB | 46.2% lower |

| Measure | v1.8.0, 12 workers | v1.9.0, 12 workers | Change |
| --- | ---: | ---: | ---: |
| Combined wall time | 4.102 s | 2.266 s | 44.8% lower |
| Combined analysis wall time | 3.089 s | 1.215 s | 60.7% lower |
| Managed process CPU time | 19.906 s | 3.812 s | 80.9% lower |
| Managed allocation | 1,552.18 MiB | 256.37 MiB | 83.5% lower |
| Sampled peak working set | 227.68 MiB | 121.44 MiB | 46.7% lower |

The final prepared CPU-heavy curve is:

| Workers | Wall | CPU | Avg managed processors | Max active admitted | Allocation | Gen 0/1/2 | Snapshot/diff phase | Static-analysis phase | Peak working set |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 2.215 s | 2.219 s | 1.002 | 1 | 251.96 MiB | 16/9/2 | 0.713 s | 7.243 s | 111.97 MiB |
| 2 | 2.157 s | 2.516 s | 1.166 | 2 | 253.13 MiB | 17/10/2 | 0.979 s | 5.996 s | 119.29 MiB |
| 4 | 2.164 s | 2.859 s | 1.321 | 4 | 253.94 MiB | 17/10/2 | 1.017 s | 6.024 s | 121.23 MiB |
| 6 | 2.191 s | 3.875 s | 1.769 | 6 | 254.66 MiB | 17/10/2 | 1.080 s | 6.082 s | 120.34 MiB |
| 8 | 2.168 s | 3.297 s | 1.520 | 8 | 255.36 MiB | 17/10/2 | 1.090 s | 6.112 s | 121.29 MiB |
| 12 | 2.266 s | 3.812 s | 1.682 | 10 | 256.37 MiB | 17/10/2 | 1.090 s | 6.512 s | 121.44 MiB |

Aggregate phase timings overlap across repository sessions and rows and therefore
can exceed wall time. The curve is intentionally reported rather than summarized
as a speedup: after the work elimination, two workers are only 2.6% faster than
one and 12 workers are 2.3% slower. There is no remaining CPU-heavy slice on this
fixture for which an 8.4x 12-worker claim would be meaningful.

The separate repository-scale fixture contains 31,034 aggregate files and six
selected changes. Shallow traversal deterministically partitions a frontier of at
least 256 disjoint trees across at most 12 recursive `ls-tree` readers. A
16,000-character argument bound and exact single-reader fallback preserve bounded,
cross-platform behavior.

| Measure | 1 worker | 12 workers | Change |
| --- | ---: | ---: | ---: |
| Combined wall time | 4.431 s | 3.442 s | 22.3% lower (`1.29x`) |
| Combined analysis wall time | 3.392 s | 2.379 s | 29.9% lower |
| Aggregate full-inventory read time | 4.414 s | 2.585 s | 41.4% lower (`1.71x`) |
| Managed process CPU time | 4.031 s | 4.578 s | 13.6% higher |
| Managed average processor equivalents | 0.910 | 1.330 | 46.2% higher |
| Managed allocation | 223.89 MiB | 233.59 MiB | 4.3% higher |
| Sampled peak working set | 140.48 MiB | 147.99 MiB | 5.3% higher |

Both rows produced semantic digest
`4984e68b05fdacf5b06145c6a160667b9486a4ac0e99ee14c953e913b63c56d2`.
The bounded memory increase is accepted for the measured latency reduction; it is
not evidence that other tree shapes scale proportionally. CI covers exact sharded
tree reconstruction, deterministic partitioning, bounded command construction,
semantic equivalence, cache reuse, privacy, and read-only behavior. Timing,
allocation, CPU, GC, and sampled-memory values remain non-gating. Issue #176 still
owns the unchanged private A/B/A+B retest; no 10x field claim is made here.

## Storage-aware heterogeneous tree scheduling v1.10.0 checkpoint

Measured on August 20, 2026 on the same Windows, .NET 10.0.7, Ryzen 9 5900X,
workstation-GC host, with no competing `eh` process. Fixture construction was
completed before measurement. The loose fixture contains 4,000 requested source
files and 128 requested context projects per repository, two repositories, eight
pinned heads, six selected changes, and 8,326 loose Git objects per repository.
Each point is the median of three fresh processes in forward/reverse/forward
order. CI does not gate on any timing or sampled-memory value.

Protocol v1.10.0 reads `git count-objects -v` once per repository session. Packed
stores and stores below 1,024 loose objects use one recursive `ls-tree`; larger
loose stores with at least 128 shallow frontier trees use at most four shards per
tree. At most eight Git readers run process-wide. Git I/O has a separate bounded
queue from common/semantic parsing and estimation, allowing heterogeneous work to
overlap without treating object-store wait as managed CPU occupancy. New
diagnostics record command count, elapsed/occupied/wait time, maximum command
time, best-effort child-process CPU where the host retains it, output bytes, and
maximum active readers.

The same loose fixture before and after the final scheduling branch is:

| Measure at 12 requested workers | Before | v1.10.0 | Change |
| --- | ---: | ---: | ---: |
| Combined wall time | 3.305 s | 2.907 s | 12.0% lower |
| Combined analysis wall time | 2.322 s | 1.945 s | 16.2% lower |
| Snapshot/diff phase | 3.369 s | 2.628 s | 22.0% lower |
| Git tree-read elapsed | 0.697 s | 0.368 s | 47.2% lower |
| Managed allocation | 166.69 MiB | 167.75 MiB | 0.6% higher |
| Sampled peak working set | 120.93 MiB | 120.84 MiB | effectively unchanged |

The final requested-worker curve is:

| Requested workers | Wall | Snapshot/diff | Tree elapsed | Tree speedup | Max active tree readers | Child Git CPU | Allocation | Peak working set |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 3.200 s | 3.597 s | 1.196 s | 1.00x | 1 | 0.000 s | 164.12 MiB | 115.38 MiB |
| 2 | 3.291 s | 3.363 s | 0.742 s | 1.61x | 2 | 0.062 s | 165.49 MiB | 116.80 MiB |
| 4 | 3.212 s | 3.017 s | 0.494 s | 2.42x | 4 | 0.141 s | 166.36 MiB | 115.64 MiB |
| 6 | 3.042 s | 2.886 s | 0.498 s | 2.40x | 6 | 0.109 s | 167.13 MiB | 120.79 MiB |
| 8 | 2.933 s | 2.626 s | 0.365 s | 3.28x | 8 | 0.109 s | 167.65 MiB | 120.41 MiB |
| 12 | 2.907 s | 2.628 s | 0.368 s | 3.25x | 8 | 0.156 s | 167.75 MiB | 120.84 MiB |

All 18 runs produced semantic digest
`9c61a3e4b2541cbdbe66427fa0fc38e1fa8b89dff2f7efcf74fd46f9f43ddf33`.
Tree scaling meets the logarithmic floor at eight active readers (`3.28x` versus
`log2(9) = 3.17`) but not at 12 requested workers (`3.25x` versus
`log2(13) = 3.70`), and whole-command one-to-twelve speedup is only `1.10x`.
Issue #182 remains open; these results must not be presented as general
logarithmic or near-linear core scaling.

The preceding approximately three-second fixture is sufficient to isolate tree
scheduling, but not to assess whole-command scaling on a 12-core host. A second
fixture was therefore prepared completely before measurement. Preparation took
114.419 seconds and is excluded from every result. It contains two repositories,
eight pinned heads, three requested contributor rows, 1,025 files and 512 context
projects per repository, 80 active context projects per repository, 10,000 lines
per source file, and 256 qualifying commits per repository. The combined report
selects 512 changes, requests 1,024 snapshot analyses, and observes 210,147,148
unique blob bytes. This is an allocation-heavy closed-month-style workload rather
than a claim that source lines or bytes multiply EHE.

The CLI and benchmark now request .NET server GC. The following curve reports the
median of three fresh processes per point in forward/reverse/forward order. The
prepared descriptor and immutable object stores were reused, no competing `eh`
process ran, and timing, CPU, GC, allocation, and sampled-memory values remain
non-gating.

| Requested workers | Wall | Speedup | Managed CPU | Avg managed processors | Max active admitted | GC pause | Cumulative allocation | Peak working set |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 16.744 s | 1.000x | 53.094 s | 3.182 | 1 | 1.804 s | 16,658.80 MiB | 682.51 MiB |
| 2 | 12.799 s | 1.308x | 60.047 s | 4.562 | 2 | 0.990 s | 16,645.12 MiB | 728.95 MiB |
| 4 | 11.943 s | 1.402x | 75.438 s | 6.360 | 4 | 1.290 s | 16,634.39 MiB | 728.96 MiB |
| 6 | 11.464 s | 1.461x | 88.484 s | 7.776 | 6 | 1.129 s | 16,649.70 MiB | 753.96 MiB |
| 8 | 11.399 s | 1.469x | 92.688 s | 8.178 | 8 | 1.072 s | 16,629.78 MiB | 769.97 MiB |
| 12 | 11.540 s | 1.451x | 94.141 s | 8.238 | 12 | 1.112 s | 16,642.03 MiB | 754.63 MiB |

An admitted-worker setting of one does not limit the collector itself to one
processor; that is why the server-GC one-worker row averages more than one managed
processor. Useful application throughput rises through six to eight workers and
then plateaus. The final 12-worker server-GC result is 1.45x faster than the
server-GC one-worker row, below the predeclared `log2(13)` floor.

For a direct collector comparison, the same one- and 12-worker endpoints were
also measured three times with workstation GC:

| Measure | Workstation GC | Server GC | Change |
| --- | ---: | ---: | ---: |
| 1-worker wall | 20.529 s | 16.744 s | 18.4% lower |
| 1-worker peak working set | 496.55 MiB | 682.51 MiB | 37.5% higher |
| 12-worker wall | 17.374 s | 11.540 s | 33.6% lower |
| 12-worker peak working set | 608.92 MiB | 754.63 MiB | 23.9% higher |

The best final row, eight workers with server GC, is 44.5% lower (`1.80x`) than
the workstation-GC one-worker baseline. The additional resident memory is an
accepted measured tradeoff for this workload, not a relaxation of cache or
buffer bounds. All 18 server-GC curve runs and all six workstation-GC endpoint
runs produced estimate-semantic digest
`337b1c99e213c10f9389103055efe2e4f5195ddc27ed97eb99447566e809289a`.

One diagnostic eight-worker sample reports 51.461 CPU-work occupied seconds and
only 1.177 wait seconds, with 45.755 occupied seconds in semantic file analysis;
Git tree-read wait is zero and tree-read elapsed is 0.322 seconds. Widening the
fixed per-repository row consumers from four to six raised one 12-worker sample
from 12.131 to 12.898 seconds and peak working set from 769.48 to 829.92 MiB, so
that experiment was rejected. The remaining limit is allocation-heavy semantic
and repository work after tree discovery, not starvation at the global admission
gate. Issue #182 remains open for a different decomposition rather than a larger
copy of the same row fan-out.

The same 31,034-file fixture was then normally packed with `git gc` before a
separate measurement. Storage-aware selection used two direct readers rather than
12 discovery/shard commands. Median tree elapsed was 0.135 seconds at one worker
and 0.078 seconds at 12, while whole-command medians were 2.038 and 2.159 seconds.
The semantic digest remained
`4984e68b05fdacf5b06145c6a160667b9486a4ac0e99ee14c953e913b63c56d2`.
The packed tree path is already immaterial; sharding it would add work rather than
create useful core scaling.
