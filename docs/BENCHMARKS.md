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
