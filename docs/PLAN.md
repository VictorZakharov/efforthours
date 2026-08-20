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
`change-seed/0.18.2+seed-rules/0.4.0`, while only the documented 0.6.0 Stage A
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
  -> bounded static evidence for each snapshot (full or changed-scope Git projection)
  -> normalized final artifact delta
  -> Change work items
  -> optional component/range or portfolio reconciliation
  -> pricing after normalized EHE
```

The base-to-head final delta is authoritative. Selection metadata, component
count, and intermediate churn do not value effort. `CHANGE_ESTIMATION.md` and
`CHANGE_PORTFOLIOS.md` govern this pipeline.

## Current priorities

### 1. Correct manual QA and preserve the sealed-test boundary

- Keep `manual-qa-coding-ratio/0.1.0` development-only. It replaces seed QA with
  dependency-linked 30/40/50 percent items over eligible expected coding effort;
  it does not multiply total repository effort, compound source ranges, or admit
  design, discovery, setup, docs, review, gaps, or pricing into the coding basis.
- Treat the anonymized `149.00`-hour coding diagnostic as useful development
  evidence: expected total improves from `161.50` to `218.10` hours against a
  separate `240.00`-hour assessment. Do not hide that the inherited high bound
  worsens or claim that one case calibrates the prior.
- Keep the frozen `2.0.0` packets and `2.1.0` blank-plan/compiler boundary
  immutable as optional diagnostic infrastructure. A local complete-plan preflight
  was not published because hundreds of rows still repeated weak category-shaped
  judgments; exhaustive micro-labeling is no longer a prerequisite.
- The current teacher labels allocate `4.89%` of eligible coding to QA and encode
  the old missing-QA assumption. Do not use them as independent validation of the
  new semantics.
- Keep the six corrected candidate-blind aggregate assessments in checkpoints
  `2.2.0` and `2.3.0` immutable. Their expected values span `155` to `1,600` hours
  and their case-specific symmetric relative half-widths span `22.6%` to `33.3%`;
  no cohort estimator output has been opened. Next, compare shipped-seed and exact-candidate
  repository totals before components and stop when the total is credible. When a
  total materially misses, use largest-first residual diagnosis only until the
  decision is explained; do not re-review correct or immaterial components.
- Record advance, revise, or reject for the exact manual-QA candidate. A survivor
  requires a new admission-policy and candidate identity plus a fresh blind
  validation boundary; the current test partition remains sealed.

- Treat `logical-capability/0.3.0` and its `1.3.0` validation rejection as final.
  Its repository expected WAPE improves from `0.2279` to `0.0940`, but six frozen
  gates fail across median/family error, repository and target coverage, target
  width, and material-category agreement. Do not relax a threshold, alter its
  uncertainty after seeing validation, or reuse its identity.
- Keep `seed-rules/0.4.0` as the shipped estimator and mandatory fallback. The
  rejected challenger never reaches the one-time test, and no admission or
  production-readiness claim follows from its aggregate improvement.
- Keep the current test source bodies, labels, seed estimates, challenger outputs,
  and metrics sealed. A rejected validation candidate does not justify test
  disclosure.
- Treat the opened validation cohort as diagnostic for future design, not as a
  fresh held-out selection set. Any successor that uses these findings needs a
  new candidate identity, new finite manifest, and fresh blind validation
  boundary before another selection.
- Use `calibration diagnose` to localize the opened cohort's point and interval
  failures before proposing a successor. Its stable category/component residuals,
  80% largest-first material set, compact leaf expansion, symmetry checks, and
  raw/normalized width correlations must reconcile without changing the rejected
  candidate or opening test. Issue #137 owns the uncertainty-model follow-through.
- Keep `repository-uncertainty-features/1.0.0` and
  `symmetric-planning-interval/1.0.0` frozen as the pre-label successor boundary.
  The offline projector exposes current confidence, provenance, parser, explicit-
  uncertainty, and material-access signals while marking unavailable function
  distributions, coupling/cycles, sample support, and out-of-distribution evidence
  as deferred within that frozen vector. `uncertainty-feature-evaluation/1.0.0`
  now supplies a deterministic
  development-only, leave-one-repository-out baseline and fixed-bucket incremental
  measurement path without fitting a production model. The first 15-repository,
  2,030-target checkpoint finds no scalar feature that improves coverage,
  normalized width, and interval miss together. The label-independent
  `uncertainty-support-profiler/1.0.0` now computes repository-family-held-out
  hierarchical support and bucketed OOD distance for all 11,161 development work
  items. `uncertainty-support-evaluator/1.0.0` aggregates four predeclared signals
  to all 2,030 targets and rejects each as a direct width driver: all reduce
  coverage, increase miss, and show non-monotonic or contrary residual ordering.
  Retain them as diagnostics. The separate label-independent
  `repository-uncertainty-structural-features/1.0.0` contract now freezes local
  callable size, bounded decision-complexity, nesting, measurement-coverage, and
  parser-ambiguity diagnostics from .NET `0.3.5` and JavaScript `0.5.2`, without
  changing the original vector, labels, seed rules, or estimates. Its separate
  `uncertainty-structural-evaluation-policy/1.0.0` now freezes target aggregation,
  expected residual direction, fixed buckets, and a repository-held-out pooled
  gate before public labels are joined. Its 15-repository, 2,030-target run rejects
  all 14 direct drivers: every conditioned interval loses coverage and increases
  miss, although median decision complexity and nesting retain useful diagnostic
  ordering. The separate label-independent
  `repository-uncertainty-graph-features/1.0.0` contract now freezes 14 .NET and
  JavaScript local fan-in/fan-out, cycle-concentration, and public-interface
  distribution diagnostics with node/edge/work-item lineage. Its evidence-only
  preflight consulted no labels or residuals, and all fields remain diagnostic.
  Its separate `uncertainty-graph-evaluation-policy/1.0.0` now freezes unique-node
  target aggregation, all-higher residual hypotheses, fixed buckets, explicit
  unmapped/interface availability behavior, sparse fallback, and the repository-
  held-out pooled gate before public residuals are joined. Its 15-repository,
  2,030-target run selects no graph field: 12 regress coverage and miss, while
  both cycle variants are exact baseline no-ops with insufficient cross-repository
  positive support. All 14 correlations oppose the predeclared direction. Next
  freeze a small, mechanistically justified correlated-combination manifest before
  calculating it; do not reverse failed directions or mine arbitrary interactions.
  Any selected successor still needs a new candidate identity and fresh blind
  validation boundary.
- Preserve repository-family partition isolation and honest teacher/model/input
  provenance. Optional independent review remains explicit corroboration, not an
  implied maturity upgrade.
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
- The host-independent multi-repository, multi-head author-period selector shares
  bounded repository-scoped Git readers, blobs, immutable inventories, and exact-
  scope snapshot analyses and emits phase/reuse diagnostics. Its public-safe
  correctness/privacy matrix and controlled combined-versus-isolated checkpoint
  are frozen in `CHANGE_PORTFOLIOS.md` and `BENCHMARKS.md`. Preserve its repository-
  scoped commit union,
  exact contributor/repository/head aggregation and zero rows, non-multiplying
  shared groups, privacy-safe report boundary, and rule that identity and time
  never become value.
- Direct and manifest author-period commands now announce live phase starts and
  summarize non-semantic timings on stderr. The `change/1.4.0` checkpoint adds a
  controlled 1/2/3-process shared-object-database matrix plus a pinned MIT public
  monorepository shape; all workers preserve exact reports and per-worker memory
  stays effectively flat in the recorded run. CI gates equivalence, read-only
  safety, and bounded coordination rather than machine-dependent performance.
- Closed-month author-period portfolios retain every exact match inside the
  existing per-repository identity-ledger boundary instead of imposing a
  presentation-row cap. Large-tree analysis loads only changed-neighborhood
  context, batches eligible first-parent deltas and changed-blob sizes once per
  repository, retains structurally shared inventories across 16 root lineages,
  resolves full-tree blob lengths lazily, and reuses immutable file and equal-tree
  analysis through a canonical Merkle inventory identity. Reconciliation uses
  linear component construction, and at most two repository sessions overlap as
  a bounded memory-for-latency tradeoff. Preserve
  the 1,700-change regression, progress/cancellation diagnostics, sibling-path
  behavior, and the non-gating `change/1.6.0` checkpoint. The current checkpoint
  compares one combined manifest with the same contributors in isolated manifests;
  the earlier repository/head explosion is retained only as historical evidence
  and is not a speedup baseline. Preserve bounded immutable file-analysis reuse,
  non-regex ignore matching, actionable privacy-safe Git ownership diagnostics,
  and explicit membership-dependent normalized contributor allocation. The
  non-gating `change/1.7.0` alpha.6-versus-final 31,010-file/256-change control is
  3.01x faster end to end and 8.17x faster in aggregate snapshot/diff work with
  identical results; it is not evidence of a 10x field improvement. The
  calculation, correctness, reuse, diagnostic, safety, and boundedness work in
  #157 is complete, including alpha.8 empty-commit handling. Issue #176 owns the
  unchanged private A/B/A+B retest. Do not claim a 10x field improvement unless
  the same 219.33-second manifest completes in approximately 21.9 seconds or less;
  if it misses, use the recorded phase and reuse counters to identify the remaining
  bottleneck honestly.
  Protocol `change/1.10.0` retains the 1.9.0 work elimination and adds
  storage-aware full-tree scheduling. Packed and small loose stores use one
  recursive traversal; large loose stores use at most four shards per tree and
  eight Git readers process-wide. Git I/O and managed CPU work have separate
  bounded queues so object-store wait can overlap parsing and estimation. The
  repeated loose-object checkpoint improves 12-worker wall time from 3.305 to
  2.907 seconds and tree-read elapsed from 0.697 to 0.368 seconds with identical
  semantics. One to eight active tree readers reaches 3.28x, but the requested
  12-worker path reaches only 3.25x. Because that approximately three-second
  fixture is too short for a whole-command core claim, a second prepared fixture
  now exercises 512 selected changes and 1,024 snapshot analyses. With server GC,
  its repeated median improves from 16.744 seconds at one admitted worker to
  11.399 at eight (`1.47x`) and then plateaus at 11.540 at twelve. Against the
  workstation-GC 12-worker median, server GC is 33.6% faster while peak working
  set is 23.9% higher; semantics are identical. Global CPU-work wait is already
  small, and widening four row consumers per repository to six regresses both
  time and memory. Issue #182 therefore remains open for a different decomposition
  of allocation-heavy semantic and repository work; do not claim general
  logarithmic core scaling. The private A/B/A+B regression owned by #176 was
  completed before that issue closed; it is no longer pending.
- The optional host-assisted scaffolding boundary is now frozen in
  `AUTHOR_PERIOD_SCAFFOLDING.md`: a separate companion adapter may eventually emit
  a reviewed v1 manifest and local-only provenance, but the estimator stays
  provider-independent and offline. Defer implementation until a concrete workflow
  justifies the authentication, privacy, and maintenance surface.

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
- Add still larger redistributable monorepository shapes before freezing universal
  regression thresholds.
- Extend the recorded nested author-period checkpoint to directory/evidence
  selectors, other ecosystems, and additional constrained-host concurrency runs.
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
