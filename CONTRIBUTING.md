# Contributing to EffortHours

EffortHours is an MIT-licensed open-source project. Contributions should preserve
the distinction between observed repository evidence, inferred classification,
estimated effort, and pricing.

## Before contributing

- Read `docs/PRODUCT.md`, `docs/ESTIMATION_MODEL.md`, `docs/PLAN.md`, and
  `AGENTS.md`. Read `docs/CHANGE_ESTIMATION.md` before changing explicit
  revision/PR behavior. Read `docs/CALIBRATION.md` and
  `docs/MODEL_ADMISSION.md` before changing calibration or repository-model
  admission.
- Use the .NET 10 SDK selected by `global.json`.
- Do not use Git history, churn, author activity, or timestamps as effort signals.
- Do not commit client source, private calibration data, credentials, or fixtures
  without clear redistribution rights.
- Discuss changes to public schemas or estimation semantics before implementation.

## Development workflow

Run these commands from the repository root:

```text
dotnet restore EffortHours.slnx --configfile NuGet.Config --locked-mode
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

Pull-request CI runs formatting once on Linux and runs build/unit and end-to-end
matrices independently on Windows, Linux, and macOS. End-to-end jobs rebuild their
project graph on each operating system instead of transferring compiled output.
Package compilation runs in parallel, but the final preview artifact is promoted
only after every formatting, quality, and end-to-end gate succeeds.

An exact pull-request diff check short-circuits those expensive jobs when every
changed path ends in `.md`. Required single jobs report successful conditional
skips, while the OS-matrix checks run only a no-op step, so every protected check
name resolves without checkout, .NET setup, restore, compilation, tests, or
packaging. The linear-history check still runs. After merge, a push to `main`
reuses those required checks when GitHub identifies exactly one matching merged
PR and the merge tree is identical to its PR-head second parent. Direct pushes,
manual dispatches, non-merge commits, unverified merges, and merges that combine
parallel first-parent changes run the complete matrix. A later `main` push does
not cancel that fallback validation. Tagged publication then requires the
successful aggregate package gate on whichever commit supplied the final tree and
does not repeat formatting, unit, or end-to-end validation.

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

Keep each pull-request branch rebased and free of merge commits; rebase onto the
current `main` instead of merging `main` into the branch. Accepted pull requests
land on `main` through GitHub's merge-commit action. Direct pushes, squash merges,
and rebase merges into `main` are disabled.

Keep changes focused and explain:

- which evidence or estimation behavior changes;
- how the result remains traceable;
- which tests demonstrate the behavior;
- whether serialized contracts change; and
- the provenance and license of any new dependency, model, dataset, or fixture.

By contributing, you agree that your contribution is provided under the repository's
MIT License.
