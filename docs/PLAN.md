# EffortHours engineering plan

This is the living architecture and roadmap. Release history belongs in
`CHANGELOG.md`; numerical review history belongs in `MODEL_REVIEWS.md`; benchmark
runs belong in the applicable benchmark record.

## Delivery strategy

EffortHours is built evidence-first:

1. observe bounded repository facts;
2. normalize exclusions, ownership, and duplication;
3. infer explicit capabilities;
4. construct small work items with transparent rules;
5. aggregate effort independently from pricing; and
6. use calibration or optional host review only where measured evidence justifies
   it.

The local deterministic path must remain useful without a calibrated model or a
remote AI provider. New sophistication is admitted only when it improves a frozen
evaluation boundary without weakening explanation, safety, or offline behavior.

## Current baseline

The repository already provides:

- one .NET 10 global tool, `EffortHours.Tool`, installed as `eh`;
- reusable versioned contracts and libraries for scanning, evidence, estimates,
  diagnostics, pricing, calibration, review, Change EHE, and reporting;
- safe common traversal, ignore handling, hashing, classification, exact-content
  normalization, optional external caching, and mixed-repository analysis;
- static analyzer families for .NET, JavaScript/TypeScript and frontend assets,
  SQL, Python/Jupyter, Go, Java/Kotlin, Shell/PowerShell, Terraform/HCL,
  PHP/Composer, Rust/Cargo, Docker/Compose, and C/C++;
- deterministic repository-first work-item construction under two profiles;
- JSON schemas, compact projections, Markdown, explanation queries, and a dated
  replaceable rate card;
- immutable Git and non-Git Change selectors, range reconciliation, portfolio
  normalization, and process-level cancellation;
- calibration authoring, blind review, exact-digest compilation, mutation
  guardrails, and offline evaluation;
- optional provider-neutral host-review packets, bounded queries, adjustment
  validation, and sanitized measurement; and
- memory-only unit fixtures, process-level CLI and Git tests, file-budget gates,
  public calibration artifacts, and reproducible fresh-process benchmarks.

This is an experimental public alpha. Repository `seed-rules/0.4.0` remains
uncalibrated. Current Change reports use
`change-seed/0.18.0+seed-rules/0.4.0`, while only the documented 0.6.0 Stage A
subset has limited logical admission. No local ML model and no automatic host-
review budget is admitted.

## Architecture

The implemented solution boundaries are:

```text
src/
  EffortHours.Cli/                  command parsing and process UX
  EffortHours.Contracts/            versioned public contracts and schemas
  EffortHours.Core/                 shared pipeline and diagnostics services
  EffortHours.Analysis/             language-neutral repository analysis
  EffortHours.Analyzers.DotNet/     .NET, C#, project, and Roslyn evidence
  EffortHours.Analyzers.JavaScript/ JS/TS, package, and frontend evidence
  EffortHours.Analyzers.Sql/        bounded SQL evidence
  EffortHours.Analyzers.Python/     Python package/source and Jupyter evidence
  EffortHours.Analyzers.Go/         Go module/workspace/source evidence
  EffortHours.Analyzers.Java/       Java, Kotlin, Maven, and Gradle evidence
  EffortHours.Analyzers.Scripting/  Shell and PowerShell evidence
  EffortHours.Analyzers.Terraform/  Terraform and HCL evidence
  EffortHours.Analyzers.Php/        PHP and Composer evidence
  EffortHours.Analyzers.Rust/       Rust and Cargo evidence
  EffortHours.Analyzers.Docker/     Dockerfile, Compose, and ignore evidence
  EffortHours.Analyzers.Cpp/        C/C++ source and native-build evidence
  EffortHours.Estimation/           rules, work items, and aggregation
  EffortHours.Change/               snapshot selection and final-delta EHE
  EffortHours.Calibration/          reviewed labels and offline evaluation
  EffortHours.Review/               host-review protocol and measurement
  EffortHours.Pricing/              versioned rate artifacts and projection
  EffortHours.Reporting/            JSON, Markdown, views, and explanation
tests/
  EffortHours.Tests/                storage-independent unit and contract tests
  EffortHours.TestFixtures/         synthetic and curated fixture builders
  EffortHours.EndToEndTests/        disk-backed CLI and safety boundaries
benchmarks/
  scanner, Change, and host-review measurements
schemas/
  published versioned JSON schemas
```

Language-neutral contracts must not depend on one analyzer's implementation.
Ecosystem analyzers should add bounded evidence to shared contracts rather than
forking the estimator. Split projects or files when a concrete responsibility
boundary exists, and follow the enforced ceilings in `eng/file-budgets.json`.

## Repository pipeline

```text
repository + optional specification
              |
              v
      safe scope and inventory
              |
              v
   ecosystem-specific analyzers
              |
              v
 versioned repository evidence
              |
              v
 normalization + capability inference
              |
              v
 work-item construction + local estimate
              |
              +----> optional digest-bound host review
              |
              v
 category aggregation + independent pricing
              |
              v
 canonical JSON, compact views, explanations, Markdown
```

The target repository is read-only. Caches and reports use explicit locations or
external storage. Structured output stays on stdout and diagnostics on stderr.

## Change pipeline

```text
explicit selector
  -> immutable base/head snapshots
  -> ordinary static evidence for each snapshot
  -> normalized final artifact delta
  -> Change work items
  -> optional component/range or portfolio reconciliation
  -> pricing after normalized EHE
```

The base-to-head final delta is authoritative. Selection metadata, component
count, and intermediate churn do not value effort. `CHANGE_ESTIMATION.md` and
`CHANGE_PORTFOLIOS.md` govern this pipeline.

## Current priorities

### 1. Broaden calibration evidence before changing priors

- Add redistributable repository families until relevant ecosystem and partition
  cells contain multiple observations.
- Keep all revisions and profiles from one repository family in one frozen
  development, validation, or test partition.
- Expand decomposed teacher review with honest model/input provenance. Optional
  independent review must remain explicit corroboration, not an implied maturity
  upgrade.
- Use the frozen `repository-model-admission/1.0.0` gates before fitting a
  correction or local model. Keep the new test labels sealed until one validation
  candidate and release decision are frozen.
- Retain mutation relations as qualitative invariance, directionality,
  marginality, and category-isolation guards—not numerical labels.

### 2. Extend Change validation deliberately

- Add immutable public changes inside the current 4-to-32-hour band, preserving
  exact small-task decomposition and the frozen Stage A decision order.
- Evaluate SQL, Python/Jupyter, Go, Java/Kotlin, Shell/PowerShell, Terraform/HCL,
  PHP/Composer, Rust/Cargo, Docker/Compose, and C/C++ changes before considering
  those extensions admitted.
- Introduce multi-day or multi-week bands only through new size-specific gates.
- Collect separately governed production observations for empirical validation.
  Never turn logged time into a multiplier or relabel it as counterfactual EHE.
- Measure portfolio reconciliation on public multi-change examples without
  weakening attribution and no-ranking safeguards.

### 3. Measure host review before selecting defaults

- Repeat the compact-versus-broader-source experiment blindly across multiple
  models or independent reviewers.
- Record complete context, exact provider token telemetry, elapsed time, and cost
  when available.
- Require useful item, category, and total behavior—not context reduction alone—
  before selecting any packet, query, token, cost, or automatic-review budget.
- Keep provider choice, disclosure, privacy, and retention in the surrounding
  client.

### 4. Improve analyzer precision where calibration shows material error

Candidate areas include general semantic-clone and liveness/reachability analysis,
reflection and dynamic registration boundaries, broader JSX accessibility
evidence, more measured-coverage formats, richer infrastructure semantics, and
larger monorepository ownership graphs.

Evaluate a compiler-grade TypeScript adapter only if reviewed results show that
the bounded token path creates material error. Do not adopt heavyweight parsers or
runtimes merely for architectural symmetry.

### 5. Strengthen scale and portability evidence

- Repeat peak-memory and read-only benchmark protocols on constrained Windows,
  Linux, and macOS hosts.
- Add larger redistributable monorepository shapes before freezing universal
  regression thresholds.
- Measure directory/evidence Change selectors and portfolio history bounds on
  realistic large trees.
- Keep caller-supplied target fingerprints and offline-safety checks in every
  performance claim.

### 6. Improve extensibility and reporting

- Publish stable analyzer extension contracts after current language-neutral
  boundaries prove sufficient across independent ecosystems.
- Continue polyglot expansion one bounded ecosystem at a time, driven by demand
  and accompanied by a public boundary, mutation slice, Change checks, and
  fresh-process benchmark.
- Add feature-oriented reporting only when it preserves repository-first lineage
  and exact reconciliation.
- Add regional rate cards without coupling geography to effort.

### 7. Admit local ML only on demonstrated value

Try transparent category corrections and simple statistical baselines before a
runtime model. A local model must improve repository-held-out agreement, preserve
guardrails and lineage, remain deterministic and offline, expose
out-of-distribution uncertainty, and justify its runtime, package size, licensing,
and maintenance cost.

## Test strategy

- Unit tests cover scope, exclusions, classification, normalization, work-item
  rules, aggregation, pricing, schemas, review, calibration, and Change semantics.
- Ordinary unit repositories and caches stay in memory. Physical fixture trees,
  Git repositories, subprocesses, and installed-tool checks stay in the end-to-end
  suite or explicit benchmarks.
- Contract tests validate every serialized schema and backward-compatibility rule.
- Mutation fixtures verify that meaningful behavior changes the intended estimate
  while formatting, generated output, duplication, excluded content, and history
  do not.
- Process-level tests cover exit codes, stdout/stderr separation, determinism,
  cancellation, explicit output paths, offline behavior, and unchanged targets.
- Calibration evaluation remains repository-isolated and reports item, category,
  total, bias, mapping, and interval behavior.
- Benchmarks record fixture generation separately from analysis, sample memory,
  fingerprint targets before and after, and disclose hardware and limitations.

## Delivery guardrails

A change is complete only when:

- applicable living contracts and schemas agree with the implementation;
- every material behavior has proportionate tests;
- ordinary unit tests remain storage-independent;
- deterministic and offline/read-only boundaries are preserved;
- public inputs and dependencies have compatible provenance;
- file budgets pass without an unexplained ratchet increase;
- the full diff contains no private source, credentials, machine-specific data,
  source excerpts, or unsupported accuracy claims; and
- release, tag, visibility, and package-publication actions remain separately
  authorized under `RELEASING.md`.
