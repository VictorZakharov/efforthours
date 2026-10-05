# Large complete-context Change checkpoint

This explicit checkpoint reproduces the selection shape of a large author-period
query without using consumer source, private manifests or endpoint reports. It
uses one contributor, 32 overlapping frozen heads and a fixed UTC cutoff. Generated
MIT fixtures contain structural identifier edits, not just numeric-token edits.
The mixed shape changes C#, TypeScript and SQL on each selected commit.

- `csharp`: 10,001 files; default 128 commits, configurable through 10,000.
- `mixed`: 30,000 files (9,000 C#, 7,000 TypeScript, 7,000 SQL, 6,998 text assets,
  one project and one package descriptor).

Prepare once under ignored `artifacts`, then use precisely the same directory and
selected count for each independently installed public baseline and candidate:

```text
python benchmarks/large-change-context/run.py prepare --directory artifacts/large-mixed --shape mixed --commits 128
python benchmarks/large-change-context/run.py run --directory artifacts/large-mixed --cli artifacts/alpha29/eh.exe --label alpha29 --selected 128
python benchmarks/large-change-context/run.py run --directory artifacts/large-mixed --cli artifacts/alpha32/eh.exe --label alpha32 --selected 128
python benchmarks/large-change-context/run.py run --directory artifacts/large-mixed --cli src/EffortHours.Cli/bin/Release/net10.0/efforthours.dll --label candidate --selected 128
```

Use an isolated tool path for each `dotnet tool install EffortHours.Tool` version;
never change the user's global install. Each run uses `implementation`, compact
JSON, no rate, and a frozen author-period manifest. Preparation, installation and
fixture fingerprinting are outside the CLI wall timer. The harness checks all bare
repository bytes and modification metadata before and after analysis. It never
executes target code, restores target dependencies or accesses a provider.

Run sequentially without competing builds, tests or benchmark processes. Fresh
CLI processes retain warm operating-system disk caches; this is not a cold-disk
checkpoint. Records contain wall time, completion/timeout, selection counts,
progress-sampled memory, phase intervals and a report hash. Phase intervals overlap
and must not be added as exclusive wall/CPU time. Memory comes from CLI progress
samples; it is not a continuously sampled process peak and may miss endpoint peaks.
Timeouts retain the last progress and are not completed-run measurements.

Semantic report hashes exclude only operational diagnostic `FB5325`. Compare
alpha.32 and the candidate for semantic parity. Alpha.29 deliberately uses an older
normalization population, so it is the latency baseline and is not a general
numerical-equivalence oracle. These small generated bodies do not model all real
repository semantics or guarantee latency for an NDA repository. Ordinary CI gates
cold/warm evidence and estimate parity, operation/reuse counts, failure recovery,
read-only behavior, schemas and fixed bounds, never wall-time thresholds.

Measured checkpoints are recorded in [BENCHMARKS.md](../../docs/BENCHMARKS.md).
