# EffortHours

Turn a software repository or completed code change into a traceable estimate of
the senior-contractor effort it represents.

EffortHours is an offline-first .NET 10 command-line tool. It statically analyzes
the code, tests, documentation, configuration, and delivery artifacts that exist
today, decomposes them into evidence-backed work items, and reports **Equivalent
Human Effort (EHE)**.

EHE answers this counterfactual question:

> How long would one competent senior contractor, unfamiliar with the business
> domain and not using AI, need to recreate this functional and quality state from
> a clear specification?

It does **not** claim how long anyone actually worked. It is not a timesheet,
productivity score, invoice, or reconstruction of repository history.

> **Experimental public alpha:** the CLI and reporting pipeline work. The bundled
> repository estimator is not independently calibrated. The Change estimator has
> passed its first model-authored logical gate for changes estimated at 4 to 32
> hours, but it is not empirically production-validated. Review the evidence and
> ranges before using any estimate for a consequential decision.

## Install

EffortHours is published as the `EffortHours.Tool` .NET global tool. The installed
command is `eh`.

```text
dotnet tool install --global EffortHours.Tool --version 0.9.0-alpha.3
eh version
eh --help
```

Preview versions are opt-in. Update an existing installation with:

```text
dotnet tool update --global EffortHours.Tool --version 0.9.0-alpha.3
```

See the [NuGet package](https://www.nuget.org/packages/EffortHours.Tool/) and the
[latest GitHub prerelease](https://github.com/VictorZakharov/efforthours/releases/tag/v0.9.0-alpha.3).

## Estimate a repository

Run this from the repository you want to estimate:

```text
eh estimate . --profile implementation --format markdown --no-rate
```

EffortHours reports a low, expected, and high EHE range, with work items grouped
into categories such as implementation, UI, data, integrations, security, tests,
documentation, delivery, validation, and review.

Use `--view review` for a compact result or JSON for automation:

```text
eh estimate . --view review --format markdown --no-rate
eh estimate . --format json --compact --output effort-hours.json --no-rate
```

To understand a material capability or work item, copy its ID from the report:

```text
eh explain . --item <capability-or-work-item-id> --format markdown
```

Every represented hour is linked back to observed evidence and a transparent rule.
EffortHours does not emit source excerpts in ordinary evidence or estimate output.

## Estimate a completed change

Change EHE values the final functional and quality delta between immutable base and
head states. Commit count, authors, timestamps, intermediate churn, and abandoned
approaches do not multiply the result.

```text
eh change . --base main --head feature/my-change --format markdown --no-rate
eh change . --commit HEAD --format markdown --no-rate
eh change . --range main..HEAD --format markdown --no-rate
eh change . --pr 123 --format markdown --no-rate
```

For an explicit range containing at least two commits, the report also compares
gross isolated commit EHE with authoritative normalized final-delta EHE. It shows
the gross-to-final normalization share and a narrower rework-like share containing
only explicit overlap/revert attribution. These are structural diagnostics—not
historical rework, actual hours, productivity scores, or multipliers. Copy the
normalization ID into `eh change explain <report.json> --item <id>` to inspect its
signed-adjustment lineage.

The current Change model is intended first for one-to-several-day changes. Its
calibration totals are built from distinct evidence-backed tasks normally about an
hour each, and its own larger work items are split into named phases capped at 1.5
expected hours rather than one unsupported aggregate guess. Larger deliverables
remain outside that admitted size band. For long ranges, EffortHours bounds the
optional per-commit audit while retaining the complete base-to-head estimate as
authoritative.

You can also compare two directories or two saved evidence bundles without Git:

```text
eh change --base-path ../before --head-path ../after --no-rate
eh change --base-evidence before.json --head-evidence after.json --no-rate
```

Git selectors read local immutable objects without checking them out. PR mode uses
an installed, authenticated `gh` CLI only to resolve the PR's immutable base/head
identities; those objects must already exist locally. The normal repository
estimate does not inspect Git history.

Formatting-only changes, exact moves, conventional generated output, vendored or
minified bodies, lockfiles, build output, and exact copies do not create body
implementation effort. Deletion is never negative or valued by deleted volume.
The current source build can retain bounded customization inside generated files
only when exact, EffortHours-specific `<custom-code>` regions isolate it safely;
the generated body still contributes zero. Generator-specific protected-region
syntax is not inferred automatically.

See the [Change EHE contract](docs/CHANGE_ESTIMATION.md) for selector,
normalization, reconciliation, and limitation details.

## Choose a profile

| Profile | Assumption |
| --- | --- |
| `implementation` | Detailed requirements, acceptance criteria, designs, and contracts are supplied. Technical design and implementation decisions remain included. |
| `recreation` | A clear behavioral specification is supplied, but more architecture, data-model, interface, and UX decisions must be recovered or made. |

Both profiles estimate a sensible modern 2026-equivalent implementation while
preserving meaningful compatibility and external constraints. Neither includes
open-ended product discovery.

## Pricing is separate from effort

EffortHours estimates hours first. Pricing is an optional, auditable projection
applied afterward and never changes EHE.

The published preview applies the dated
`us-senior-software-contractor/2026.1` rate by default. Use effort-only output or
provide your own rate:

```text
eh estimate . --no-rate
eh estimate . --hourly-rate 175 --currency USD
eh rate info
```

Cost output is **Equivalent Replacement Cost**, not historical pay or a billing
determination.

## What EffortHours analyzes

The current static analyzers support:

- .NET and C# projects, solutions, APIs, data access, migrations, integrations,
  security, Razor/UI surfaces, and tests;
- JavaScript, JSX, TypeScript, and TSX package/workspace structure, APIs, UI,
  data access, integrations, security, background work, and tests;
- React/Preact/Next-style JSX plus Vue and Svelte components;
- static Angular `@Component` metadata, including literal inline and relative
  external templates and styles when ownership is unambiguous;
- maintained HTML template structure, forms, bindings, and directives through a
  bounded tolerant scanner; and
- CSS, SCSS/Sass, and Less rules/selectors, responsive surfaces, design tokens,
  animation, and theme signals through bounded tolerant scanners;
- mixed .NET and JavaScript/TypeScript repositories;
- documentation, build configuration, CI, containers, infrastructure, package
  metadata, and checked-in LCOV or Cobertura coverage reports; and
- generated, vendored, minified, binary, duplicate, test, and documentation
  classification used to prevent mechanical volume from inflating effort.

JavaScript and JSX structure is parser-backed. TypeScript and TSX are explicitly
token-backed in this release. Angular metadata analysis requires a named
`Component` import from `@angular/core` (including a local alias), accepts only
static literals and arrays, and never evaluates TypeScript or executable
configuration; external assets must resolve to scanner-admitted files inside the
repository.
HTML and CSS-family analysis is tolerant and structural: it does not render a UI,
compile a framework, run a preprocessor, prove runtime behavior, or perform an
accessibility audit. Physical markup/style line count is retained as evidence but
is not an EHE driver.
Standalone SQL is inventoried but does not yet have semantic schema/query analysis;
that work is tracked in [issue #50](https://github.com/VictorZakharov/efforthours/issues/50).

## Offline and safe by default

For normal `scan` and `estimate` commands, EffortHours:

- does not execute target code or build scripts;
- does not install dependencies or invoke package managers;
- does not access the network;
- does not inspect Git history, contributors, churn, or timestamps;
- does not write into the target repository;
- does not follow links outside the selected scope; and
- does not send repository content to an AI provider.

Source trees are treated as untrusted input. Analysis is bounded and
deterministic, structured data stays on stdout, and diagnostics stay on stderr.
An optional external scan cache can speed repeated scans; it is never an effort
signal.

## Optional host-AI review

The local estimate is complete without AI. If a surrounding AI session is useful,
`eh review packet` produces a compact, rate-free, provider-neutral uncertainty
packet. Follow-up queries can request bounded evidence or explicitly admitted
source windows, and `review validate` checks an adjustment ledger without applying
it.

EffortHours does not choose or call a provider. The caller controls disclosure,
privacy, retention, model choice, and cost. No automatic review budget is selected
in this alpha. See the [host-review protocol](docs/MILESTONE_8.md).

## Important limitations

- The bundled estimators are experimental. The repository model remains
  uncalibrated; Change has only first-band logical calibration, not empirical
  production validation. Ranges are planning bounds, not formal confidence
  intervals.
- Static analysis assumes discovered tests pass on the fastest path and does not
  prove that a checkout builds or runs.
- Unsupported languages still receive common inventory evidence but not the same
  semantic depth as .NET, JavaScript/TypeScript, and the supported frontend forms.
- Checked-in coverage can be stale even when its bytes match the analyzed tree;
  EffortHours does not regenerate it.
- Multiple-PR and author/time portfolio estimation is not implemented.
- A repository estimate is not a security, accessibility, architecture, or code-
  quality audit.

The [product charter](docs/PRODUCT.md) and
[estimation model](docs/ESTIMATION_MODEL.md) define the precise semantics.

## Common commands

```text
eh scan <repository> [--output evidence.json]
eh estimate <repository-or-evidence.json> [--profile implementation|recreation]
eh report <estimate.json> [--view review|category|scope|work-item]
eh explain <repository-or-evidence.json> --item <id>
eh change <repository> --base <revision> --head <revision>
eh change <repository> --commit <revision> [--parent <revision>]
eh change <repository> --range <base>..<head>
eh change <repository> --pr <number-or-url> [--repo <owner/name>]
eh change explain <change-estimate.json> --item <id>
eh model info
eh rate info
eh schema list
```

Run `eh --help` or a subcommand's help for the complete option surface. Calibration
and review researchers can start from the [documentation index](docs/README.md).

## Build and contribute

The repository uses the .NET 10 SDK selected by `global.json`.

```text
dotnet restore EffortHours.slnx --configfile NuGet.Config --force-evaluate
dotnet build EffortHours.slnx --no-restore --configuration Release
dotnet test tests/EffortHours.Tests/EffortHours.Tests.csproj --no-build --no-restore --configuration Release
dotnet test tests/EffortHours.EndToEndTests/EffortHours.EndToEndTests.csproj --no-build --no-restore --configuration Release
```

Read [CONTRIBUTING.md](CONTRIBUTING.md) before opening a change. Security reports
should follow [SECURITY.md](SECURITY.md). Package maintainers should use the exact
[release procedure](docs/RELEASING.md).

EffortHours is licensed under the [MIT License](LICENSE).
