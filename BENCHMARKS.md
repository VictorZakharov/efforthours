# Fairbill benchmarks

Fairbill records reproducible engineering checkpoints rather than presenting a
single synthetic run as a universal performance guarantee.

## Common scanner v0.2 checkpoint

Measured on August 5, 2026 with:

- Fairbill common scanner `0.2.0`;
- .NET runtime `10.0.7` and .NET SDK `10.0.203`;
- Windows `10.0.26200`, x64;
- 24 logical processors exposed to the process; and
- a generated repository containing 10,000 small C# files with 100 physical lines
  per file, plus one SDK-style project file.

Command:

```text
dotnet benchmarks/Fairbill.ScannerBenchmarks/bin/Release/net10.0/Fairbill.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --warm-cache
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
uniform. Future benchmark work should add repeated cold/warm runs, peak working-set
measurement, mixed .NET/JavaScript fixtures, and curated redistributable real-world
repositories.

## Static .NET analyzer v0.3 checkpoint

Measured on August 5, 2026 with the same environment and generated repository as
the common-scanner checkpoint. This mode runs the common inventory followed by
static project parsing and a Roslyn syntax pass over every maintained C# file. It
does not evaluate MSBuild, restore or compile the fixture, or execute target code.

Command:

```text
dotnet benchmarks/Fairbill.ScannerBenchmarks/bin/Release/net10.0/Fairbill.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --dotnet
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
Future runs should add mixed syntax shapes and peak working-set measurement.

## Static JavaScript/TypeScript analyzer v0.4 checkpoint

Measured on August 5, 2026 with the same runtime, operating system, and hardware as
the earlier checkpoints. The generated repository contains one root package and
10,000 source files with 100 physical lines each. Files alternate between
JavaScript, which takes the Acornima AST path, and TypeScript, which takes the
bounded token path. The benchmark does not run Node, a package manager, a
transpiler, executable configuration, or target code.

Command:

```text
dotnet benchmarks/Fairbill.ScannerBenchmarks/bin/Release/net10.0/Fairbill.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --javascript
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
exercises substantial AST allocation. Curated real-world mixed repositories and
peak working-set measurements remain future benchmark work.
