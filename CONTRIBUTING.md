# Contributing to EffortHours

EffortHours is an MIT-licensed open-source project. Contributions should preserve
the distinction between observed repository evidence, inferred classification,
estimated effort, and pricing.

## Before contributing

- Read `docs/PRODUCT.md`, `docs/ESTIMATION_MODEL.md`, `docs/PLAN.md`, and
  `AGENTS.md`. Read `docs/CHANGE_ESTIMATION.md` before changing explicit
  revision/PR behavior.
- Use the .NET 10 SDK selected by `global.json`.
- Do not use Git history, churn, author activity, or timestamps as effort signals.
- Do not commit client source, private calibration data, credentials, or fixtures
  without clear redistribution rights.
- Discuss changes to public schemas or estimation semantics before implementation.

## Development workflow

Run these commands from the repository root:

```text
dotnet restore EffortHours.slnx --configfile NuGet.Config --force-evaluate
dotnet format EffortHours.slnx --no-restore --verify-no-changes --severity info
dotnet build EffortHours.slnx --no-restore --configuration Release
dotnet test tests/EffortHours.Tests/EffortHours.Tests.csproj --no-build --no-restore --configuration Release
dotnet test tests/EffortHours.EndToEndTests/EffortHours.EndToEndTests.csproj --no-build --no-restore --configuration Release
dotnet pack src/EffortHours.Cli/EffortHours.Cli.csproj --configuration Release --no-build --no-restore --output artifacts/packages
```

The first test command is the frequent, storage-independent loop: all repository
and cache fixtures are in memory. The end-to-end project intentionally exercises
the physical CLI/process boundary and is primarily a release check. It also
enforces the source-file ratchets in `eng/file-budgets.json`; follow
`docs/CODE_BUDGETS.md` and split responsibilities near 80% of a ceiling.

Run the synthetic one-million-line scanner checkpoint with:

```text
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --warm-cache
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --dotnet
dotnet benchmarks/EffortHours.ScannerBenchmarks/bin/Release/net10.0/EffortHours.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --javascript
```

All behavioral changes require tests, and schema changes require contract and
compatibility tests. Keep generated build output under the ignored `artifacts`
directory.

## Pull requests

Keep changes focused and explain:

- which evidence or estimation behavior changes;
- how the result remains traceable;
- which tests demonstrate the behavior;
- whether serialized contracts change; and
- the provenance and license of any new dependency, model, dataset, or fixture.

By contributing, you agree that your contribution is provided under the repository's
MIT License.
