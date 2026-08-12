# Source-file budgets

EffortHours uses line budgets as an early architecture signal. They are not a style
score: a file approaching its budget is a prompt to separate responsibilities
while the code is still cheap to move.

The enforced manifest is [`eng/file-budgets.json`](../eng/file-budgets.json).
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

The August 10, 2026 frontend semantic-evidence checkpoint moved maintained web-
asset orchestration out of the already ratcheted
`JavaScriptRepositoryAnalyzer.cs`. Angular import-context recognition, Angular
metadata parsing, HTML/template scanning, CSS-family scanning, and evidence/
ownership construction each live in a focused file under the ordinary 500-line
ceiling. The repository orchestrator decreased from 772 to 744 lines; no override
was added or increased.

The August 11, 2026 SQL checkpoint separates bounded text admission, tokenization,
statement measurement, dialect assessment, artifact-role classification,
project/package ownership, and evidence construction into focused files. The
semantic analyzer was split before the 80% threshold, and SQL Change formatting
normalization remains a separate component. No ratchet override was added or
increased.

The August 11, 2026 Milestone 7B6 checkpoint keeps bounded .NET reachability,
frontend accessibility scanning, accessibility capability construction, and
JavaScript test-call classification in focused files. Moving all test-call
classification out of `JavaScriptSyntaxAnalyzer.cs` reduces that legacy file from
800 to 760 lines and lowers its ratchet from 850 to 800. All new files use the
ordinary 500-line ceiling; no override was added or increased.

The August 11, 2026 Change portfolio checkpoint separates public contracts,
semantic validation, immutable identity, repository-group normalization, exact
allocation, adjustment construction, Git metadata selection, CLI parsing,
manifest loading, time parsing, CLI help, topology, and JSON/Markdown rendering.
Allocation, topology, option validation, and group-level contract validation were
split before their callers approached the ordinary ceiling. Every CLI file remains
below 400 lines, every other new file remains below 500, and no override was added
or increased.

The August 11, 2026 Python checkpoint separates safe digest-checked text admission,
bounded tokenization, indentation-aware structural analysis, static package
metadata, evidence construction, and repository orchestration. Python Change
formatting normalization is a separate focused component. Every new source and
test file remains under the ordinary 500-line ceiling, the Python CLI E2E file
remains under 400 lines, and no override was added or increased.

The August 11, 2026 Go checkpoint separates digest-checked text admission,
tokenization, syntax measurement, import qualification, module/workspace parsing,
evidence construction, platform selection markers, and repository orchestration.
Go Change formatting normalization is a separate focused component. Every new
source and test file remains below the ordinary 500-line ceiling, the Go CLI E2E
file remains below 400 lines, and no override was added or increased.

The August 11, 2026 Java checkpoint separates digest-checked text admission,
tokenization, syntax measurement, import and annotation qualification, Maven XML
parsing, Gradle literal projection, project ownership, evidence construction, and
repository orchestration. Java Change formatting normalization is a separate
focused component. Every new source and test file remains below the ordinary
500-line ceiling, the Java CLI E2E file remains below 400 lines, and no override
was added or increased.

The August 11, 2026 Kotlin checkpoint reuses the safe JVM text and build readers
while separating tokenization, token utilities, syntax measurement, import
qualification, semantic classification, evidence construction, repository
orchestration, and Kotlin Change formatting normalization. Every new source and
test file remains below the ordinary 500-line ceiling, the Kotlin CLI E2E file
remains below 400 lines, and no override was added or increased.

The August 11, 2026 Shell and PowerShell checkpoint separates common file and
automation-role classification, bounded text admission, invocation-context
matching, Shell/PowerShell tokenization and syntax measurement, evidence
construction, repository orchestration, and the two Change formatting
normalizers. Static binary/language catalogs moved out of `FileClassifier.cs` so
the new script classifications did not turn it into a mixed-responsibility file.
Every new source and test file remains below its ordinary ceiling, and no override
was added or increased.

The August 12, 2026 Terraform/HCL checkpoint separates common classification,
digest-checked text admission, bounded tokenization, structural parsing, semantic
measurement, module ownership/reference resolution, evidence construction,
repository orchestration, and Change formatting normalization. Fact construction
is split between core and category-specific partials. Every new source and test
file remains below its ordinary ceiling, and no override was added or increased.

Line count is deliberately simple, deterministic, cross-platform, and difficult
to game accidentally. The useful outcome is earlier decomposition into cohesive
contracts, commands, analyzers, and renderers—not compressed formatting.
