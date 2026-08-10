# EffortHours

EffortHours is an experimental .NET 10 command-line tool and set of reusable libraries
for estimating the human effort represented by a software repository.

Its primary metric is **Equivalent Human Effort (EHE)**: the counterfactual time a
competent senior contractor, unfamiliar with the business domain, would need to
recreate the repository's current functional and quality state from a clear
specification without using AI.

EffortHours is intended to make a defensible repository estimate inexpensive. It is
expected to run inside an AI-enabled development session, but static analysis and
local models should compress and handle most of the repository. The host AI should
need to reason about only compact evidence and unresolved semantic questions rather
than reading an entire large repository.

> **Experimental public alpha:** EffortHours is a working reference
> implementation, but its bundled seed estimators have not completed independent
> calibration. Outputs are counterfactual replacement-effort estimates, not actual
> labor records, invoices, or production-validated billing determinations.

## Status

[EffortHours 0.9.0-alpha.2](https://github.com/VictorZakharov/efforthours/releases/tag/v0.9.0-alpha.2)
is available as public source and as the
[`EffortHours.Tool` NuGet preview](https://www.nuget.org/packages/EffortHours.Tool/0.9.0-alpha.2).
Milestones 1 through 6 and the Milestone 7A calibration foundation are complete.
Milestone 7B1 through 7B5 public-corpus, review, and mutation checkpoints are
implemented. A post-7B5 precision checkpoint corrects reviewed .NET persistence,
framework-neutral JavaScript UI, and benchmark-entry-point false positives without
changing seed priors. Actual independent correction and broader multi-observation
corpus coverage remain. A subsequent measured-coverage checkpoint statically
parses digest-verified LCOV and Cobertura reports, maps them to maintained
production scopes without emitting reported source paths, and gives measured
values precedence over same-scope declarations. The first experimental Change
Estimation MVP is also
implemented for immutable base/head revisions, one commit, one range, and one
GitHub pull request. Its behavioral safeguard checkpoint adds cooperative
cancellation and category-isolated migration, integration, CI, container-delivery,
and simplification mutations. Its first calibration checkpoint adds final-delta
provenance, review and evaluation commands, a frozen rubric, and a 24-case matrix
with reproducible source reports, 121 preliminary teacher targets, and a blind
independent-review packet. Real-source follow-ons exercise released alpha.2 on one
pilot plus a blind six-family .NET/JavaScript/TypeScript expansion. The visible
expansion diagnostics expose repeated category-slice overcounting; its test
comparison remains withheld. No independent Change correction or accuracy claim
is complete.
The first Milestone 8 host-review and measurement checkpoints are implemented: a
surrounding AI session can consume a rate-free, digest-bound uncertainty packet,
request bounded capability, evidence, scope, or explicitly selected source detail,
and return a schema-validated adjustment ledger. EffortHours can record sanitized
session telemetry and compare compact/broader-source review pairs without calling
a provider or applying adjustments. The initial three-repository public diagnostic
improved item/category agreement but worsened total agreement; exact provider
tokens, time, and cost were unavailable, so no default review budget was selected.
The repository now contains:

- versioned JSON contracts and published schemas for evidence, work items,
  estimates, diagnostics, and rate cards;
- deterministic serialization and semantic/schema validation;
- a deterministic, read-only common scanner with nested `.gitignore` and
  `.efforthoursignore` handling;
- streamed SHA-256, size, and physical-line measurement without source excerpts;
- file, language, ecosystem, project/package, test, documentation, build, CI,
  container, infrastructure, coverage, and exclusion evidence;
- generated, vendored, minified, binary, build-output, link, and Git-metadata
  classification;
- an optional versioned incremental cache that must live outside the target tree;
- static `.sln`, `.slnx`, SDK-project, central-package, package-reference, and
  project-graph analysis without evaluating MSBuild;
- Roslyn syntax evidence for .NET entry points, APIs, data access, migrations,
  background work, integrations, security, validation, UI/Razor surfaces, source
  structure, and unit/component/integration/end-to-end tests;
- static npm-compatible package, npm/pnpm/Yarn/Bun convention, workspace,
  dependency graph, script-name, framework, and TypeScript JSONC configuration
  discovery;
- Acornima AST evidence for JavaScript/JSX plus deterministic bounded token-stream
  evidence for TypeScript/TSX and Vue/Svelte script blocks;
- JavaScript/TypeScript evidence for server routes, UI pages/components/state,
  maintained web assets, data access, integrations, security, validation,
  background work, and unit/component/integration/end-to-end tests;
- an injectable repository-storage boundary with memory-only unit fixtures;
- an installable `EffortHours.Tool` .NET global tool with the command `eh`;
- schema-versioned Change EHE evidence, reports, explanations, immutable Git-tree
  analysis, additive range reconciliation, and an optional identity-only `gh` PR
  adapter;
- Change calibration authoring, compilation, independent-review handoff, and
  evaluation paths that preserve immutable final-delta lineage and repository-held-out
  partitions, plus a deterministic in-memory generator for 24 public synthetic
  source cases whose frozen reports retain `change-seed/0.1.0` provenance and a
  one-record public pilot plus a six-record blind public expansion for current
  `change-seed/0.2.0`;
- current `change-seed/0.2.0` marginal-modification rules that require changed
  normalized capability evidence, correlate repeated category/path evidence, and
  emit final-delta comprehension, validation, and review once;
- JSON and Markdown reports with evidence lineage, ranges, and optional pricing;
- synthetic fixtures, contract tests, and process-level CLI tests;
- evidence normalization that separates production/test structure, gives fine
  semantic facts precedence over broad inventory, and collapses exact duplicate
  maintained bodies;
- category-specific capability builders for implementation, UI, data, integrations,
  security, tests, documentation, delivery, validation, and review;
- explicit `implementation` and `recreation` profile work, deterministic confidence
  drivers, and a professionalization-gap ledger excluded from represented EHE;
- a schema-validated, checked-in, embedded `seed-rules/0.2.1` model that partitions
  large capabilities around a four-hour target, retains the 0.2.0 priors, and
  normalizes TypeScript duplicates and test structure through the shared
  JavaScript/TypeScript estimation scope;
- repository, category, scope, capability, and bounded review projections with
  compact JSON and readable Markdown;
- `explain` drill-down from stable work-item or capability IDs to evidence and
  calculation lineage;
- provider-neutral `review packet`, digest-bound `review query`, non-applying
  `review validate`, sanitized `review measure`, and paired `review benchmark`
  commands with versioned packet, query-result, adjustment, validation,
  measurement, and comparison schemas;
- a schema-validated, checked-in 2026 US senior-contractor rate model with a
  $160/hour default, $125-$200 market reference, source provenance, formula, and
  caller override or opt-out;
- a versioned reviewed-label corpus, work-item rubric, repository-isolated
  development/validation/test partitions, and deterministic offline evaluator for
  item, category, total, bias, interval, and mapping-coverage metrics;
- schema-versioned unreviewed authoring packets, optional blind review, and a
  deterministic review-plan compiler that requires complete capability coverage;
- two MIT-source-provenanced three-repository public corpora with frozen
  partitions and checked-in `seed-rules/0.2.0` or `seed-rules/0.2.1` baseline
  reports. Their six repository families have one host-AI teacher and no
  independent correction, so they remain preliminary weak supervision;
- exact-digest subsequent-review packets and compilation with explicit
  accept/replace decisions, preserved lineage, maturity progression, and distinct
  reviewer identities;
- audited exact-zero reviewed exclusions, permitted only as `0/0/0` with rationale
  and a size exception under `ehe-work-item/1.1.0`;
- versioned relational mutation guardrails plus a 51-case, 170-assertion synthetic
  baseline spanning .NET, parser-backed JavaScript, token-backed TypeScript, and
  mixed repositories. It covers formatting, exact duplication, generated bodies,
  maintained generated customization, bounded renamed near-copies,
  compiler-disabled C# syntax, API/UI/data/security behavior, tests, declared
  and measured coverage levels, measured-over-declared precedence, documentation,
  integrations, workspace boundaries, CI, containers, all three range points, and
  category isolation;
- bounded, non-executing LCOV and Cobertura parsing with common-inventory digest
  checks, line/branch/function measurements, and privacy-safe project/package
  scope mapping;
- enforced source-file budgets with a 500-line default, a 400-line CLI ceiling,
  explicit legacy ratchets, and a thin command dispatcher.

`eh scan <folder>` now produces common, static .NET, and static
JavaScript/TypeScript evidence, including mixed-repository output and supported
checked-in measured-coverage artifacts.
`eh estimate <folder>` connects that evidence directly to the granular seed
pipeline. Every represented hour belongs to an evidence-backed work item, but the
priors remain experimental and uncalibrated. Seed-rule output must not be presented
as a production-ready or empirically validated estimate.

`eh estimate` now applies the bundled 2026 USD rate by default. Use
`--no-rate` for effort-only output or `--hourly-rate` for an explicit replacement.
Pricing never changes EHE. Use `--view review` for the compact local projection and
pass any reported capability ID to `eh explain` for its evidence lineage.

`eh review packet <folder>` creates the provider-neutral, rate-free handoff for a
surrounding AI session. Follow-up `review query` calls must repeat the packet's
input digest and can request one capability, one evidence fact, a bounded scope
page, or an explicit admitted-source line window. `review validate` checks a
proposed evidence-backed adjustment ledger but never applies it. `review measure`
records one completed review with caller-supplied telemetry; `review benchmark`
compares one compact and one broader-source session per opaque subject. No review
command chooses or calls a provider; provider, privacy, disclosure, and retention
decisions remain with the caller. See [Milestone 8](docs/MILESTONE_8.md) and its
[measurement checkpoint](docs/MILESTONE_8_MEASUREMENT.md).

`eh change` estimates the final functional and quality delta for explicit
base/head revisions, a commit, a range, or one PR. It reads immutable local Git
objects without checking out, fetching, executing, or modifying the target. Range
reports reconcile isolated commits with the authoritative normalized final delta;
commit count and intermediate churn never multiply effort. PR mode uses optional
`gh` only to resolve immutable identities and requires those objects locally. The
`change-seed/0.2.0` model is uncalibrated and experimental. It treats modification
work marginally and does not assign a specialized category merely because a
changed path belongs to a scope that already has that capability.

The blind six-family real-source diagnostic shows that repeated category
partitions can still multiply one logical Change work item, especially for tests
and security evidence. The result supports a general structural correction, not a
blanket ratio; no correction has been applied and the test comparison remains
withheld.

The first Ctrl+C requests cooperative cancellation and returns exit code 130 after
writing a concise diagnostic to stderr; pressing Ctrl+C again retains immediate
termination. Partial structured output is not presented as a successful report.

`eh calibration scaffold` creates an explicitly unreviewed packet from a
saved canonical estimate; `--blind` hides numeric seed guidance.
Change reports use the dedicated `calibration change-scaffold` command; passing one
to the repository scaffold returns an actionable redirect.
`eh calibration compile` turns completed capability decisions into a corpus only after
verifying exact estimate digests and full represented-capability coverage.
`eh calibration validate` checks a reviewed corpus and its provenance.
`eh calibration evaluate` compares canonical estimates with one explicit
repository-held-out partition. It evaluates effort only, never calls an AI or the
network, and does not make the still-uncalibrated seed priors production-ready.

`eh calibration review-scaffold` prepares a reference or blind second-pass
packet from an existing corpus. `review-compile` advances maturity only after a
distinct reviewer decides every target and the exact source-corpus digest matches.
Both checked-in public packets are still unreviewed; the combined handoff is in
[`calibration/INDEPENDENT_REVIEW.md`](calibration/INDEPENDENT_REVIEW.md).
`eh calibration mutations`
evaluates deterministic relational guardrails; failures emit a report and return
exit code 5. Mutation relations are not effort labels. The latest near-copy bounds
do not imply semantic clone detection, and the dead-code invariant is limited to
C# syntax excluded by the compiler preprocessor rather than general reachability.

Change reports use parallel `calibration change-scaffold`, `change-compile`, and
`change-evaluate` commands. They reuse ordinary corpus validation and blind
second-review commands while adding immutable base/head and final-delta provenance.
See [`calibration/changes`](calibration/changes); its source reports and blind
packets are reproducible, and its teacher labels are preliminary until a genuinely
distinct reviewer completes the frozen handoff.

EffortHours is licensed under the [MIT License](LICENSE). The public alpha is intended
to make the estimation model, evidence contracts, limitations, and working
reference implementation available for critique, reuse, forks, and independent
alternatives. Open-source availability does not turn preliminary teacher labels
into validated ground truth.

Current language-specific analysis covers:

- .NET and C# repositories
- JavaScript and TypeScript repositories
- Mixed repositories containing both ecosystems

The architecture should allow additional language analyzers later.

JavaScript and JSX structure counts are parser-backed. TypeScript and TSX use a
purpose-built, non-executing token analyzer in this release; evidence tags disclose
which path was used. EffortHours never imports JavaScript modules or runs package
scripts, package managers, transpilers, or executable configuration during the
default scan.

Measured coverage parsing currently supports checked-in LCOV and Cobertura only.
EffortHours does not generate or refresh those reports, so a stale or mismatched
artifact remains an explicit uncertainty even after its checked-in bytes are
digest-verified. OpenCover, JaCoCo, Istanbul JSON, and binary `.coverage` files are
inventoried but not interpreted as measured percentages in this checkpoint.

## Install the preview

The NuGet package identity is `EffortHours.Tool`, and the installed command is
`eh`. Install the pinned prerelease with:

```text
dotnet tool install --global EffortHours.Tool --version 0.9.0-alpha.2
eh version
eh --help
```

Preview versions are intentionally opt-in. To replace an older preview, use
`dotnet tool update --global EffortHours.Tool --version <version>`. See the
[release procedure](docs/RELEASING.md) for package verification,
trusted-publishing, and release steps.

## Build from source

The .NET 10 SDK selected by `global.json` is required. Change commands also require
Git; `--pr` additionally requires an installed and authenticated `gh` CLI.

```text
dotnet restore EffortHours.slnx --configfile NuGet.Config --force-evaluate
dotnet build EffortHours.slnx --no-restore --configuration Release
dotnet test tests/EffortHours.Tests/EffortHours.Tests.csproj --no-build --no-restore --configuration Release
```

The normal unit suite uses only in-memory repository and cache fixtures. Run the
separate, disk-backed process tests when validating the CLI boundary or a release:

```text
dotnet test tests/EffortHours.EndToEndTests/EffortHours.EndToEndTests.csproj --no-build --no-restore --configuration Release
```

Scan a repository without executing it or reading its Git history:

```text
dotnet src/EffortHours.Cli/bin/Release/net10.0/efforthours.dll scan . --output ../efforthours.repository-evidence.json
```

Run the current evidence-to-estimate pipeline directly on a folder:

```text
dotnet src/EffortHours.Cli/bin/Release/net10.0/efforthours.dll estimate . --profile implementation --format markdown
```

The available CLI surface is:

```text
eh --help
eh version
eh scan <repository> [--output <path>] [--cache <external-path>] [--no-gitignore] [--no-efforthoursignore]
eh schema list
eh schema show <name>
eh model info
eh model show
eh rate info
eh rate show
eh estimate <repository-or-evidence.json> [--profile <implementation|recreation>] [--view <full|repository|category|scope|work-item|review>] [--format <json|markdown>] [--compact] [--output <path>] [--no-rate | --hourly-rate <amount> [--currency <code>]]
eh report <estimate.json> [--view <full|repository|category|scope|work-item|review>] [--format <json|markdown>] [--compact]
eh explain <repository-or-evidence.json> --item <work-item-or-capability-id> [--profile <implementation|recreation>] [--format <json|markdown>] [--compact]
eh change <repository> --base <revision> --head <revision> [--profile <implementation|recreation>] [--format <json|markdown>] [--compact] [--output <path>] [--no-rate | --hourly-rate <amount> [--currency <code>]]
eh change <repository> --commit <revision> [--parent <revision>] [options]
eh change <repository> --range <base>..<head> [options]
eh change <repository> --pr <number-or-url> [--repo <owner/name>] [options]
eh change explain <change-estimate.json> --item <work-item-id> [--format <json|markdown>] [--compact]
eh calibration scaffold <estimate.json> [--blind] [--compact] [--output <path>]
eh calibration compile <review-plan.json> <estimate.json>... [--compact] [--output <path>]
eh calibration review-scaffold <corpus.json> [--blind] [--compact] [--output <path>]
eh calibration review-compile <plan.json> <corpus.json> [--compact] [--output <path>]
eh calibration mutations <suite.json> <estimate.json>... [--compact] [--output <path>]
eh calibration validate <corpus.json> [--compact] [--output <path>]
eh calibration evaluate <corpus.json> <estimate.json>... --partition <development|validation|test> [--compact] [--output <path>]
eh calibration change-scaffold <change-estimate.json> --repository-family <id> --case <id> --tag <tag>... [--blind] [--compact] [--output <path>]
eh calibration change-compile <review-plan.json> <change-estimate.json>... [--compact] [--output <path>]
eh calibration change-evaluate <corpus.json> <change-estimate.json>... --partition <development|validation|test> [--compact] [--output <path>]
```

The optional scan cache trusts file path, size, and last-write metadata for
invalidation. It is a performance optimization, not an effort signal or forensic
integrity mechanism. Omit it for a full content re-read.

## Performance checkpoint

The current benchmark harness records cumulative managed allocation, sampled peak
resident memory, fresh-process-versus-explicit-cache conditions, and a before/after
target-tree metadata digest. On the documented development machine, fresh-process
one-million-line scans took 7.083 seconds for static .NET, 12.088 seconds for static
JavaScript/TypeScript, and 10.876 seconds for a mixed C#/JavaScript/TypeScript tree;
sampled scan peaks were 272.52, 185.69, and 234.20 MiB respectively. The mixed
warm-cache pass took 4.581 seconds. A combined set of three exact, verified MIT
releases and the EffortHours development tree were also measured read-only. These
are reproducible engineering checkpoints, not universal performance guarantees or
release thresholds. See [scanner benchmarks](docs/BENCHMARKS.md) for commands,
provenance, hardware, cache semantics, scaling samples, and limitations.

The Milestone 6 EffortHours snapshot produced 240,464 bytes of compact canonical
estimate JSON. The bounded review projection used 17,694 bytes (7.4%), and review
Markdown used 8,763 bytes (3.6%). See
[reporting benchmarks](docs/REPORT_BENCHMARKS.md) for all views, small .NET,
JavaScript/TypeScript, and mixed fixtures, the measurement method, and the initial
usefulness review.

## Important distinction

EHE is not a claim about how many hours were actually worked. It is an estimate of
the conventional, non-AI replacement effort embodied in the current artifact.
Cost output is therefore an **Equivalent Replacement Cost**, not a timesheet.

This distinction allows EffortHours to support value and compensation discussions
without misrepresenting counterfactual hours as historical labor.

## Planned workflow

1. Inspect a repository without consulting its development history.
2. Extract objective, traceable evidence about its current state.
3. Decompose the work into small repository-level work items.
4. Estimate each item with transparent rules and local ML where useful.
5. Present ambiguous, low-confidence items to the host AI when useful.
6. Aggregate effort by category and apply a dated, configurable market rate.
7. Produce machine-readable evidence and a human-readable report.

## Documentation

The [documentation index](docs/README.md) separates current product and engineering
contracts from historical implementation checkpoints. Key starting points are the
[product charter](docs/PRODUCT.md), [estimation model](docs/ESTIMATION_MODEL.md),
[change-estimation contract](docs/CHANGE_ESTIMATION.md), and
[release procedure](docs/RELEASING.md).

Public schemas, transparent seed priors, auditable rate provenance, and review
corpora live under [`schemas/v1`](schemas/v1),
[`models/seed-rules`](models/seed-rules),
[`rates/us-senior-contractor`](rates/us-senior-contractor), and
[`calibration`](calibration), respectively. Contribution, conduct, governance,
security, changelog, and third-party-notice files remain at the repository root for
normal GitHub discovery.
