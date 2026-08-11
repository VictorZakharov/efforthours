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
JavaScript, and TypeScript and includes both project and package manifests.

| Full-scan mode | Text lines | Scan | Lines/s | Managed allocation | Sampled peak working set | Evidence JSON |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Static .NET | 1,000,001 | 7.083 s | 141,179 | 619.32 MiB | 272.52 MiB | 9.94 MiB |
| Static JavaScript/TypeScript | 1,000,001 | 12.088 s | 82,728 | 1,723.34 MiB | 185.69 MiB | 10.03 MiB |
| Static mixed | 1,000,002 | 10.876 s | 91,947 | 1,361.23 MiB | 234.20 MiB | 10.01 MiB |

The mixed run's measured warm-cache pass took 4.581 seconds, cumulatively allocated
1,266.78 MiB, and reached a sampled 431.68 MiB process working set. That absolute
warm peak is not comparable to a fresh process: it follows the full scan and an
untimed cache-population pass, so its starting working set was already 384.33 MiB.
The cache avoids unchanged common-file inspection; ecosystem analyzers still parse
their retained source evidence.

Fresh-process mixed samples at 250,000, 500,000, and 1,000,000 requested lines
recorded sampled scan peaks of 121.95, 127.65, and 234.20 MiB respectively. The
corresponding cumulative managed allocations were 343.27, 682.66, and 1,361.23 MiB.
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

Measured on August 11, 2026 with the same Windows/.NET/hardware environment as the
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
