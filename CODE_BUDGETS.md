# Source-file budgets

EffortHours uses line budgets as an early architecture signal. They are not a style
score: a file approaching its budget is a prompt to separate responsibilities
while the code is still cheap to move.

The enforced manifest is [`eng/file-budgets.json`](eng/file-budgets.json).
`EffortHours.EndToEndTests` checks every C# file under `src`, `tests`, and `benchmarks`.
This check is intentionally disk-backed and therefore does not enter the
memory-only unit suite.

## Policy

- The default hard ceiling is 500 physical lines per C# file.
- CLI files have a stricter 400-line ceiling.
- `EffortHoursApplication.cs` should remain a thin dispatcher substantially below the
  CLI ceiling.
- Start refactoring near 80% of a hard ceiling; do not wait for the test to fail.
- Existing larger files have explicit ratchet budgets. Their entries record debt,
  not preferred sizes, and must not be copied to new files.
- Reducing an override should update the manifest in the same change. Increasing
  or adding an override requires an explicit architectural rationale in the
  relevant design or milestone document.
- Generated files may receive a narrowly documented exception when generation is
  reproducible and hand-maintained responsibilities are not being hidden.

The August 8, 2026 analyzer-precision change extracted `.NET` data classification
plus application and service-boundary classification from `CSharpFileAnalyzer.cs`
into focused analyzers. The main file moved below the 80% refactoring threshold for
the ordinary 500-line ceiling, so its former 750-line ratchet was removed; all new
classifiers also use the ordinary ceiling. This is a responsibility split, not a
new exception.

The August 9, 2026 benchmark checkpoint separated option parsing, fixture
generation and cleanup, target-tree fingerprinting, inventory projection, and
working-set sampling from benchmark orchestration. The largest scanner-benchmark
source file is 193 lines, and no ratchet override was added.

The August 9, 2026 host-review checkpoint placed packet construction, querying,
selected-source safety, rendering, and adjustment validation in a separate
`EffortHours.Review` library. CLI parsing is split by subcommand, and contract
validation is split into focused partial files after the budget test caught the
first oversized draft. No ratchet override was added.

The August 9, 2026 measurement checkpoint kept telemetry contracts, payload
measurement, session construction, comparison metrics, aggregation, rendering,
and CLI option parsing in focused files. Measurement semantic validation was split
again before reaching the ordinary ceiling. No ratchet override was added.

The August 9, 2026 measured-coverage checkpoint separates LCOV parsing, Cobertura
parsing, scope matching, evidence construction, and orchestration. Every new file
uses the ordinary 500-line ceiling, and the orchestrator was split below the 80%
refactoring threshold. Coverage capability construction moved out of the legacy
seed builder, allowing its ratchet to decrease from 1350 to 1300 lines. No override
was added or increased.

Line count is deliberately simple, deterministic, cross-platform, and difficult
to game accidentally. The useful outcome is earlier decomposition into cohesive
contracts, commands, analyzers, and renderers—not compressed formatting.
