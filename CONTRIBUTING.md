# Contributing to Fairbill

Fairbill is being built in public for eventual open-source release. Contributions
should preserve the distinction between observed repository evidence, inferred
classification, estimated effort, and pricing.

## Before contributing

- Read `PRODUCT.md`, `ESTIMATION_MODEL.md`, `PLAN.md`, and `AGENTS.md`.
- Use the .NET 10 SDK selected by `global.json`.
- Do not use Git history, churn, author activity, or timestamps as effort signals.
- Do not commit client source, private calibration data, credentials, or fixtures
  without clear redistribution rights.
- Discuss changes to public schemas or estimation semantics before implementation.

## Development workflow

Run these commands from the repository root:

```text
dotnet restore Fairbill.slnx --configfile NuGet.Config --force-evaluate
dotnet format Fairbill.slnx --no-restore --verify-no-changes --severity info
dotnet build Fairbill.slnx --no-restore --configuration Release
dotnet test Fairbill.slnx --no-build --no-restore --configuration Release
dotnet pack src/Fairbill.Cli/Fairbill.Cli.csproj --configuration Release --no-build --no-restore --output artifacts/packages
```

Run the synthetic one-million-line scanner checkpoint with:

```text
dotnet benchmarks/Fairbill.ScannerBenchmarks/bin/Release/net10.0/Fairbill.ScannerBenchmarks.dll --files 10000 --lines-per-file 100 --warm-cache
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
