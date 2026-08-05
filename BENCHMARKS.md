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
