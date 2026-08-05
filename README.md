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

Milestones 1 and 2 are complete. The repository now contains:

- versioned JSON contracts and published schemas for evidence, work items,
  estimates, diagnostics, and rate cards;
- deterministic serialization and semantic/schema validation;
- a deterministic, read-only common scanner with nested `.gitignore` and
  `.fairbillignore` handling;
- streamed SHA-256, size, and physical-line measurement without source excerpts;
- file, language, ecosystem, project/package, test, documentation, build, CI,
  container, infrastructure, coverage, and exclusion evidence;
- generated, vendored, minified, binary, build-output, link, and Git-metadata
  classification;
- an optional versioned incremental cache that must live outside the target tree;
- an installable .NET global tool named `fairbill`;
- JSON and Markdown reports with evidence lineage, ranges, and optional pricing;
- synthetic fixtures, contract tests, and process-level CLI tests; and
- an intentionally uncalibrated seed estimator that exercises the complete
  evidence-to-report pipeline.

`fairbill scan <folder>` now produces repository evidence, and `fairbill estimate
<folder>` connects that evidence directly to the seed pipeline. The next milestone
is semantic .NET analysis. JavaScript/TypeScript semantic analysis follows it.
Seed-rule output remains scaffolding and must not be presented as a production
estimate.

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

Scan a repository without executing it or reading its Git history:

```text
dotnet src/Fairbill.Cli/bin/Release/net10.0/fairbill.dll scan . --output ../fairbill.repository-evidence.json
```

Run the current evidence-to-estimate pipeline directly on a folder:

```text
dotnet src/Fairbill.Cli/bin/Release/net10.0/fairbill.dll estimate . --profile implementation --format markdown
```

The available CLI surface is:

```text
fairbill --help
fairbill version
fairbill scan <repository> [--output <path>] [--cache <external-path>] [--no-gitignore] [--no-fairbillignore]
fairbill schema list
fairbill schema show <name>
fairbill estimate <repository-or-evidence.json> --profile <implementation|recreation> --format <json|markdown> [--hourly-rate <amount>] [--currency <code>]
```

The optional scan cache trusts file path, size, and last-write metadata for
invalidation. It is a performance optimization, not an effort signal or forensic
integrity mechanism. Omit it for a full content re-read.

## Performance checkpoint

The v0.2 scanner processed a synthetic one-million-line repository containing
10,000 C# files in 4.275 seconds, plus 0.116 seconds for JSON serialization, on the
documented development machine. An unchanged warm-cache scan took 1.646 seconds and
produced the same digest. This is a repeatable engineering checkpoint, not a claim
about every repository shape. See [BENCHMARKS.md](BENCHMARKS.md) for the method,
environment, and limitations.

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
- [BENCHMARKS.md](BENCHMARKS.md) records reproducible performance checkpoints.
- [`schemas/v1`](schemas/v1) contains the published v1 JSON schemas.
