# Fairbill Implementation Plan

## 1. Delivery strategy

Fairbill will be built evidence-first. The initial product should be useful as a
repository compressor for a human or AI estimator before its local effort model is
fully calibrated.

The sequence is therefore:

1. Define stable evidence and report contracts.
2. Build trustworthy repository and language analyzers.
3. Produce a granular work-item ledger with transparent seed rules.
4. Add reports and AI-agent-friendly interfaces.
5. Calibrate and distill strong-AI judgments into local models.
6. Optimize the host AI workflow only where it improves uncertain cases.

## 2. Proposed architecture

One .NET 10 global tool named `fairbill` will expose composable subcommands. Shared
libraries will make the analyzers and estimator reusable outside the CLI.

Proposed solution boundaries:

```text
src/
  Fairbill.Cli/                  command parsing and process UX
  Fairbill.Contracts/            versioned evidence/report contracts
  Fairbill.Core/                 scope, pipeline, diagnostics, shared services
  Fairbill.Analysis/             language-neutral repository analysis
  Fairbill.Analyzers.DotNet/     .NET/MSBuild/Roslyn evidence
  Fairbill.Analyzers.JavaScript/ JavaScript/TypeScript evidence
  Fairbill.Estimation/           rules, work items, aggregation
  Fairbill.ML/                   optional local inference and calibration contracts
  Fairbill.Reporting/            JSON and Markdown output
tests/
  unit and contract tests by production project
  Fairbill.TestFixtures/         synthetic and curated repository fixtures
  Fairbill.EndToEndTests/        packaged CLI behavior
benchmarks/
  analyzer and large-repository performance checks
schemas/
  published JSON schemas
```

These are planned boundaries, not a requirement to create every project on day one.
Projects should be split only when the boundary has concrete value.

## 3. Pipeline

```text
repository + optional specification
              |
              v
      scope and file inventory
              |
              v
  ecosystem-specific analyzers
              |
              v
 versioned repository evidence
              |
              v
 work-item construction and local estimation
              |
              +----> low-confidence packets ----> host AI session
              |
              v
 category aggregation + independent rate card
              |
              v
 JSON evidence, JSON estimate, and Markdown report
```

The target repository is read-only by default. Caches and generated reports should
use explicit output locations or a user cache rather than silently modifying the
analyzed project.

## 4. Planned CLI surface

The exact command names will be validated during the CLI milestone. The initial
shape is:

```text
fairbill scan <path> [--spec <path>] [--output <path>]
fairbill estimate <path-or-evidence> --profile <implementation|recreation>
fairbill report <estimate> --format <json|markdown>
fairbill explain <estimate> [--item <id>]
fairbill verify <path> [--build] [--test] [--coverage]
fairbill model info
fairbill rate show
```

Machine-readable output goes to standard output when no output path is given.
Diagnostics should go to standard error. Structured commands require stable exit
codes and a schema-version field.

An eventual agent mode may expose narrow queries over an evidence bundle so an AI
can request only relevant details without rereading source files.

`scan` and `estimate` use the fastest static path by default. They assume discovered
tests pass and may use an explicitly configured coverage level as
declared-and-assumed evidence. `verify` is an optional, slower path and is never
required for an ordinary estimate.

## 5. Milestones

Status as of August 5, 2026:

- Milestone 0 is complete: the initial product semantics, MIT License, packaging
  identity, and repository conventions are recorded.
- Milestone 1 is complete: the contracts, schemas, seed pipeline, reports, global
  tool package, and test harness are implemented and verified.
- Milestone 2 is complete: safe traversal, ignore handling, metadata-only file
  evidence, hashing, artifact/exclusion classification, optional external caching,
  CLI folder input, tests, and a one-million-line benchmark are implemented.
- Milestone 3 is complete: safe project/solution graph parsing and Roslyn syntax
  evidence cover representative web, worker, library, CLI, UI, data, integration,
  security, validation, and test shapes without MSBuild evaluation or execution.
- Milestone 4 is complete: static package/workspace/configuration discovery,
  Acornima JavaScript/JSX AST evidence, bounded TypeScript/TSX token evidence,
  framework behavior classification, mixed-repository support, memory-only unit
  fixtures, CLI tests, and a million-line benchmark are implemented.
- Milestone 5 is complete: evidence normalization, exact-content deduplication,
  category capability builders, a versioned embedded seed-rule catalog, explicit
  profile work, approximately four-hour work-item partitioning, deterministic
  uncertainty drivers, manual validation, self-review, and a separate conservative
  professionalization gap are implemented. Milestone 6 is next. The seed estimator
  remains explicitly uncalibrated and is not a production estimate.

### Milestone 0: Product and contract decisions

- Approve the product charter and estimation semantics.
- Resolve the open questions in `ESTIMATION_MODEL.md`.
- Record the MIT License and choose the packaging identity.
- Establish coding, test, and documentation conventions.

Exit condition: ambiguous product rules are either decided or explicitly versioned
as experiments.

### Milestone 1: Contracts and skeleton

- Create the .NET 10 solution and minimal CLI.
- Add public-repository hygiene, including the MIT License, contribution guidance,
  security reporting guidance, and dependency provenance.
- Define versioned JSON contracts and schemas for evidence, work items, estimates,
  diagnostics, and rate cards.
- Add deterministic serialization and schema tests.
- Add fixture infrastructure and end-to-end CLI test harness.

Exit condition: a synthetic evidence bundle can be validated, estimated with stub
rules, and rendered without accessing a repository.

### Milestone 2: Common repository scanner

- Implement safe scope traversal, ignore handling, path normalization, file typing,
  content hashing, and binary/minified/vendor/generated detection.
- Detect ecosystems, projects, packages, configuration, documentation, CI/CD,
  containers, infrastructure, and coverage artifacts.
- Never inspect Git history for effort signals.
- Add incremental analysis and cache contracts without changing the target tree.

Exit condition: mixed fixture repositories produce stable, traceable inventories
with known exclusions and no network or code execution.

### Milestone 3: .NET analyzer

- Discover solutions, projects, target frameworks, packages, and project graphs.
- Analyze source structure, entry points, APIs, data access, migrations, background
  work, integrations, authentication/authorization, tests, and documentation.
- Use compiler-quality semantic information where available without requiring a
  successful build.
- Classify unit, integration, component, and end-to-end test projects and artifacts.

Exit condition: representative ASP.NET, worker, library, CLI, and test fixtures
produce reviewed evidence with stable IDs.

### Milestone 4: JavaScript and TypeScript analyzer

- Discover workspaces, packages, scripts, frameworks, dependency graphs, and build
  configurations.
- Analyze server routes, UI applications, components/pages, state, data access,
  integrations, tests, and documentation.
- Identify common generated, bundled, minified, lockfile, and vendored content.
- Support mixed .NET plus JavaScript/TypeScript solutions.

Exit condition: representative Node, React-family, frontend, backend, library, and
  test fixtures produce reviewed evidence equivalent in quality to .NET analysis.

Implementation note: Acornima 1.6.2 supplies standards-oriented JavaScript and JSX
ASTs. It does not parse TypeScript grammar, so the initial TS/TSX path uses a
bounded deterministic token analyzer and labels that provenance explicitly. No
Node process, package manager, transpiler, target dependency, or executable config
is loaded. A compiler-grade TypeScript adapter remains a future precision option
if calibration shows the token evidence is insufficient.

### Milestone 5: Seed estimation model

- Define category-specific work-unit builders.
- Create transparent baseline productivity priors and complexity modifiers.
- Decompose expected work to approximately 0.5-to-8-hour items.
- Implement both estimation profiles.
- Separate represented effort from the professionalization gap.
- Include reasonable manual validation and debugging as explicit work items.
- Produce preliminary ranges, confidence, and explicit assumptions.

Exit condition: every hour in an estimate traces to named work items and evidence;
there is no unexplained repository-level total multiplier.

Implementation note: `MILESTONE_5.md` records the detailed design. The checked-in
`models/seed-rules/0.2.0.json` artifact is schema-validated and embedded into the
estimation assembly for deterministic offline loading. Broad inventory facts yield
to fine semantic facts, exact byte-identical maintained bodies are normalized,
general source structure supplies residual implementation work, and specialized
builders value behavior-specific boundaries. Large capabilities are deterministically
partitioned around a four-hour target while remaining within the 0.5-to-8-hour
expected range. `MODEL_REVIEWS.md` records the first provenance-bound, provisional
self-review anchor. The model is transparent but not calibrated.

### Milestone 6: Reporting and agent usability

- Produce compact JSON and readable Markdown reports.
- Add repository, category, project/module, and work-item views.
- Add `explain` output for evidence and calculation lineage.
- Add configurable, dated rate cards and cost output.
- Measure token size and usefulness of evidence bundles for AI consumers.

Exit condition: a strong AI can review an estimate from the compressed output
without reading the full repository in ordinary cases.

### Milestone 7: Calibration and local ML

- Establish the structured teacher-estimate rubric.
- Create a reviewed calibration corpus at work-item granularity.
- Train and evaluate category and quantile models with repository-level holdouts.
- Add out-of-distribution and low-confidence detection.
- Package deterministic local inference through ML.NET or ONNX.
- Retain rule guardrails and explanation lineage around every prediction.

Exit condition: the local model improves held-out agreement and calibration over
the seed rules without making reports opaque.

### Milestone 8: Host AI integration and measurement

- Define compact uncertainty packets and follow-up query commands for an AI agent.
- Record available AI model identity, input digest, adjustment, and rationale.
- Benchmark token use, elapsed time, cost, and estimate improvement on representative
  repositories before choosing defaults or limits.
- Keep provider and privacy choices in the surrounding AI session rather than
  embedding them into the core estimator.
- Ensure the same repository can always receive a local baseline estimate.

Exit condition: host AI review materially improves low-confidence cases while using
only a small, measurable fraction of the context and cost required for full-source
review.

### Milestone 9: Expansion

- Add feature-oriented reporting.
- Publish analyzer extension contracts.
- Add languages and ecosystems based on demand.
- Add regional rate cards without coupling geography to effort.
- Explore local semantic models where deterministic analysis is insufficient.

## 6. Test strategy

Fairbill's credibility depends on analyzer and calculation tests as much as product
features.

- Unit tests cover scope, exclusions, classification, work-item rules, aggregation,
  and pricing.
- Unit repository fixtures and scan caches are memory-backed; ordinary unit-test
  runs do not create, modify, enumerate, or delete physical fixture trees.
- Contract tests validate every serialized schema and backward-compatibility rule.
- Golden fixture tests compare reviewed evidence and reports for small repositories.
- Mutation-style fixture variants verify that meaningful changes alter estimates
  while formatting, generated output, duplication, and history do not.
- End-to-end tests invoke the packaged CLI on .NET, JavaScript, TypeScript, and mixed
  repositories.
- Performance tests cover large trees, incremental scans, and bounded memory.
- Calibration tests split by repository and measure item, category, total, and
  interval behavior.
- Safety tests confirm static mode performs no builds, package installs, network
  requests, or writes into the target repository.

## 7. Initial acceptance criteria

The first useful release should:

- run cross-platform on the .NET 10 runtime;
- analyze .NET and JavaScript/TypeScript repositories without remote services;
- ignore development history;
- distinguish maintained source from generated, vendored, binary, and minified
  content;
- inventory tests and consume existing coverage artifacts without inventing
  coverage, while clearly labeling configured coverage as declared-and-assumed;
- inventory documentation, build, CI/CD, and infrastructure artifacts;
- emit versioned evidence and estimate JSON;
- produce low, expected, and high EHE by category;
- support implementation and recreation profiles;
- attach evidence and reasoning to material work items;
- separately report professionalization gaps;
- apply a dated, configurable rate without altering hours;
- reproduce identical results for identical inputs, configuration, and model
  versions; and
- complete without AI, network access, or repository history; and
- use only dependencies, fixtures, model artifacts, and distributed data that can
  legally accompany the chosen open-source distribution, with recorded provenance.

The initial common-scanner checkpoint analyzes one million synthetic C# source
lines across 10,000 files in 4.275 seconds and serializes the evidence in another
0.116 seconds on the environment recorded in `BENCHMARKS.md`; an unchanged
warm-cache scan takes 1.646 seconds with the same evidence digest. Representative
mixed and real-world corpora are still required before establishing release
thresholds.

Numerical accuracy and performance thresholds will be added after a representative
benchmark corpus exists.

## 8. Immediate next steps

1. Build Milestone 6 compact repository, category, scope, and explain views without
   losing work-item calculation lineage.
2. Define reviewed repository-level calibration labels and isolate train/test
   splits by repository.
3. Measure evidence-to-estimate accuracy by category and validate deterministic
   uncertainty drivers before considering local ML.
4. Extend performance and safety measurements to curated, redistributable mixed
   repository shapes and peak-memory measurements.
5. Evaluate a compiler-grade TypeScript adapter only if reviewed calibration shows
   material error from the bounded token evidence.
