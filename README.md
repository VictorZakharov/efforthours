# Fairbill

Fairbill is an experimental .NET 10 command-line tool and set of reusable libraries
for estimating the human effort represented by a software repository.

Its primary metric is **Equivalent Human Effort (EHE)**: the counterfactual time a
competent senior contractor, unfamiliar with the business domain, would need to
recreate the repository's current functional and quality state from a clear
specification without using AI.

Fairbill is intended to make a defensible repository estimate inexpensive. It is
expected to run inside an AI-enabled development session, but static analysis and
local models should compress and handle most of the repository. The host AI should
need to reason about only compact evidence and unresolved semantic questions rather
than reading an entire large repository.

## Status

Milestone 1 is complete. The repository now contains:

- versioned JSON contracts and published schemas for evidence, work items,
  estimates, diagnostics, and rate cards;
- deterministic serialization and semantic/schema validation;
- an installable .NET global tool named `fairbill`;
- JSON and Markdown reports with evidence lineage, ranges, and optional pricing;
- synthetic fixtures, contract tests, and process-level CLI tests; and
- an intentionally uncalibrated seed estimator that exercises the complete
  evidence-to-report pipeline.

The common repository scanner is the next milestone. At present, `fairbill
estimate` accepts a prepared repository-evidence JSON document; it does not yet
inspect an arbitrary source folder. Seed-rule output is scaffolding and must not be
presented as a production estimate.

Fairbill is intended to be released as open-source software. Development should be
public-repository-ready from the beginning, even before the repository is published.
It is licensed under the [MIT License](LICENSE).

Initial language support will target:

- .NET and C# repositories
- JavaScript and TypeScript repositories
- Mixed repositories containing both ecosystems

The architecture should allow additional language analyzers later.

## Build and try the CLI

The .NET 10 SDK selected by `global.json` is required.

```text
dotnet restore Fairbill.slnx --configfile NuGet.Config --force-evaluate
dotnet build Fairbill.slnx --no-restore --configuration Release
dotnet test Fairbill.slnx --no-build --no-restore --configuration Release
```

Run the current evidence-driven estimate pipeline:

```text
dotnet src/Fairbill.Cli/bin/Release/net10.0/fairbill.dll estimate tests/fixtures/evidence/minimal.repository-evidence.json --profile implementation --format json
```

The available CLI surface is:

```text
fairbill --help
fairbill version
fairbill schema list
fairbill schema show <name>
fairbill estimate <evidence.json> --profile <implementation|recreation> --format <json|markdown> [--hourly-rate <amount>] [--currency <code>]
```

## Important distinction

EHE is not a claim about how many hours were actually worked. It is an estimate of
the conventional, non-AI replacement effort embodied in the current artifact.
Cost output is therefore an **Equivalent Replacement Cost**, not a timesheet.

This distinction allows Fairbill to support value and compensation discussions
without misrepresenting counterfactual hours as historical labor.

## Planned workflow

1. Inspect a repository without consulting its development history.
2. Extract objective, traceable evidence about its current state.
3. Decompose the work into small repository-level work items.
4. Estimate each item with transparent rules and local ML where useful.
5. Present ambiguous, low-confidence items to the host AI when useful.
6. Aggregate effort by category and apply a dated, configurable market rate.
7. Produce machine-readable evidence and a human-readable report.

## Project documents

- [PRODUCT.md](PRODUCT.md) defines the product, metric, scope, and principles.
- [ESTIMATION_MODEL.md](ESTIMATION_MODEL.md) specifies how evidence becomes effort
  and cost.
- [PLAN.md](PLAN.md) describes the proposed architecture and delivery roadmap.
- [AGENTS.md](AGENTS.md) contains repository-wide instructions for coding agents.
- [CONTRIBUTING.md](CONTRIBUTING.md) contains the verified development workflow.
- [SECURITY.md](SECURITY.md) explains private vulnerability reporting expectations.
- [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) records dependency provenance.
- [`schemas/v1`](schemas/v1) contains the published v1 JSON schemas.
