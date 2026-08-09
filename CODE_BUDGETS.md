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

Line count is deliberately simple, deterministic, cross-platform, and difficult
to game accidentally. The useful outcome is earlier decomposition into cohesive
contracts, commands, analyzers, and renderers—not compressed formatting.
