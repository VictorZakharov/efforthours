# AGENTS.md

## Scope

These instructions apply to the entire EffortHours repository.

EffortHours is an offline-first .NET 10 CLI that estimates **Equivalent Human
Effort (EHE)**: the time one competent senior contractor, unfamiliar with the
business domain and not using AI, would need to recreate the current functional
and quality state from a clear specification. EHE is replacement effort, not
actual labor, a timesheet, authorship, productivity, compensation, or an invoice.

## Read by change area

Use `docs/README.md` as the index. Before changing behavior, read only the
applicable contracts, but read those contracts completely:

- product or estimation semantics: `docs/PRODUCT.md` and
  `docs/ESTIMATION_MODEL.md`;
- architecture or roadmap: `docs/PLAN.md`;
- reporting or pricing: `docs/REPORTING.md` or `docs/PRICING.md`;
- calibration, repository-model admission, or model review:
  `docs/CALIBRATION.md`, `docs/MODEL_ADMISSION.md`, and
  `docs/MODEL_REVIEWS.md` as relevant;
- Change EHE, portfolios, or admission: `docs/CHANGE_ESTIMATION.md`,
  `docs/CHANGE_PORTFOLIOS.md`, and `docs/CHANGE_MODEL_ADMISSION.md` as relevant;
- host review: `docs/HOST_REVIEW.md` and
  `docs/HOST_REVIEW_MEASUREMENT.md`;
- analyzer behavior: the applicable `docs/*_ANALYSIS.md` file;
- release or NuGet publication: `docs/RELEASING.md`; and
- C# responsibility or file-size changes: `docs/CODE_BUDGETS.md` and
  `eng/file-budgets.json`.

Do not silently contradict a documented decision. Update the governing contract
when semantics, schemas, assumptions, or unresolved decisions change.

## Repository invariants

- Estimate the current artifact and normalized final change, never historical
  activity or abandoned work. Git identity and time may select Change/portfolio
  inputs but must never multiply effort.
- Prefer functional and quality equivalence over line-for-line reproduction.
- Do not reward generated, vendored, duplicate, dead, or accidentally complex
  content. Represent only supported evidence for maintained customization,
  integration, configuration, validation, tests, documentation, and delivery.
- Keep represented effort separate from remediation/professionalization gaps and
  from pricing. A rate card must never change EHE.
- Preserve versioned contracts, stable evidence IDs, explicit uncertainty, and
  calculation lineage. Never overstate calibration, accuracy, or production
  readiness.
- Calibrate at the coarsest level that can be estimated reliably. Treat repository
  totals as primary, group immaterial uncertainty into a bounded residual, and use
  granular disagreement analysis only to diagnose a materially wrong total. Row
  count and repeated micro-judgments are not independent accuracy evidence.
- Keep ordinary analysis deterministic, offline, read-only, and safe for untrusted
  source trees. Do not execute target code or tools, install target dependencies,
  access the network, inspect Git history, follow links outside scope, or emit
  secrets/source excerpts by default.
- Put structured output on stdout and diagnostics on stderr; remain cancellable,
  memory-bounded, and cross-platform.
- Keep language-neutral contracts separate from ecosystem analyzers. Keep
  evidence, inference, estimated work, review adjustments, and pricing distinct;
  favor parser/compiler evidence over textual guesses.

## Change discipline

- Add proportionate tests for behavior changes. Unit repositories/caches stay in
  memory; physical files, Git, subprocesses, and installed-tool checks belong in
  end-to-end tests or explicit benchmarks.
- Do not make ordinary CI pass or fail on benchmark wall-clock or sampled-memory
  thresholds. Record those measurements in explicit benchmark checkpoints; gate
  CI on deterministic semantics, operation/reuse counts, safety, and bounded
  configuration instead.
- Validate serialized output against checked-in schemas. Follow
  `eng/file-budgets.json`; never raise a ratchet without architectural rationale.
- Treat committed material as public. Exclude credentials, private/proprietary
  evidence, machine-specific data, and unlicensed assets; preserve MIT metadata
  and the root `LICENSE`.
- Keep living contracts current. Put releases in `CHANGELOG.md`, measurements in
  their designated records, and completed work in Git history, not this file.
- Use ripgrep (`rg`) for searches; it is installed. If the restricted shell strips
  it from `PATH`, run plain `rg` in the approved shell. Do not paste a
  machine-specific executable path, add a repository wrapper, or treat the
  restricted-shell failure as evidence that `rg` is absent.
- Preserve unrelated user changes and avoid destructive Git operations unless
  explicitly requested.
- Use the validation sequence in `CONTRIBUTING.md`, scaled to risk. Agents may
  commit, push, and open/update PRs, but must never merge or enable auto-merge.

## Release preflight

- Before dispatching or retrying a NuGet publication, read `docs/RELEASING.md`
  completely and run
  `gh api repos/VictorZakharov/efforthours/environments/nuget.org/variables/NUGET_USER --jq .value`.
  Require the documented value `VictorZakharov`; stop before dispatch on a
  mismatch.
- `NUGET_USER` identifies the NuGet trusted-publishing policy creator. It is not
  the package/organization owner (`WellScoped`), and package metadata must never be
  used to infer or overwrite it.
- Every release PR must update `Version` and `PackageReleaseNotes` together in the
  CLI project. NuGet release notes must begin with the exact prerelease version,
  summarize the material user-visible changes, retain the experimental/
  uncalibrated boundary, and link to the tag-specific changelog. Inspect the
  packed `EffortHours.Tool.nuspec`; missing, mismatched, or stale release notes
  block merge and protected-environment approval.
- Publish only the exact verified prerelease tag and artifact. After approval,
  require a successful publish job, NuGet indexing, and a clean public-feed
  install before creating the matching GitHub prerelease.
- Never delete or move a pushed release tag after a failed workflow. If no package
  was published, record the skipped tag, fix the release path in a new PR,
  increment the prerelease version, and tag the corrected merge.

## Current model boundary

Repository EHE remains experimental and uncalibrated. The frozen 33-family public
readiness cohort has exact source reproduction, strict-blind packets for all 15
development families, and rubric-complete teacher records for the full development
partition. Its nine-family strict-blind validation is complete. Candidate
`logical-capability/0.3.0` improves repository expected WAPE from `0.2279` to
`0.0940` but fails six frozen gates covering median/family error, repository and
target coverage, target width, and individual material-category agreement. The
candidate is retired without test disclosure. Test remains sealed, no candidate
is admitted or shipped, and `seed-rules/0.4.0` remains the product estimator and
required fallback. The development-only uncertainty evaluator has measured the 11
frozen scalar features with repository-held-out folds; none yet beats the symmetric
baseline on coverage, normalized width, and interval miss together, so no interval
model is frozen. A separate label-independent support profiler now covers all
11,161 development work items with repository-family-held-out hierarchical support
and bucketed OOD distance. Its four predeclared target-level signals have now been
measured against all 2,030 development targets; each worsens coverage and interval
miss and has non-monotonic or contrary residual ordering. They remain diagnostic-
only, no interval model is frozen, and estimates do not change. A separate frozen
structural diagnostic contract now exposes local callable size, decision-
complexity, nesting, parser coverage, and analyzer ambiguity for parser-backed C#
and JavaScript/JSX. Its separate target aggregation, fixed buckets, expected
residual directions, and repository-held-out evaluation gate were frozen before
public labels were joined. All 14 fields have now been measured against the 2,030
development targets; every conditioned interval loses coverage and increases miss,
so all remain diagnostic-only and no interval model is frozen. The separate
`repository-uncertainty-graph-features/1.0.0` contract now freezes 14
.NET/JavaScript fan-in/fan-out, cycle, and public-interface diagnostics with
node/edge/work-item lineage. The separate
`uncertainty-graph-evaluation-policy/1.0.0` freezes unique-node target aggregation,
all-higher residual hypotheses, fixed buckets, explicit missing/interface states,
sparse baseline fallback, and a development-only repository-held-out gate without
reading public residuals. The subsequent 15-repository, 2,030-target development
run selects no graph field: 12 variants regress coverage and miss, and both cycle
variants are exact baseline no-ops with opposite-direction correlations and only
one repository of positive support. Every field remains diagnostic-only and
estimates remain unchanged.

Development candidate `manual-qa-coding-ratio/0.1.0` now replaces seed manual-QA
items with dependency-linked 30/40/50 percent items over eligible expected coding
effort. It is an experience-based, unvalidated correction candidate only:
`seed-rules/0.4.0` remains shipped and existing labels are unchanged. Its
anonymized real-case midpoint improves from 161.50 to 218.10 hours against a
240.00-hour assessment, reducing absolute point error by 72.1%, while its inherited
high bound worsens. The 15 candidate-blind packets and blank 955-decision compiler
remain immutable optional diagnostic infrastructure; exhaustive micro-labeling was
stopped before any completed plan or corpus was published because completeness did
not establish reliable numerical judgment. `repository-total-materiality/1.0.0`
now makes credible repository totals primary and drills down only on material
misses. Its six-case follow-up cohort and source-backed assessments are frozen
before estimator output under `repository-total-assessment-cohort/1.0.0` and
`repository-total-source-review/1.1.0`. Expected assessments span 155 to 1,600
hours, with symmetric case-specific relative half-widths from 22.6% to 33.3%; the
two private cases remain anonymous and their source evidence stays outside the
repository. Total-first seed/candidate comparison is next. Any admission attempt
still needs a new policy/candidate identity and fresh validation boundary;
interval research remains separate.

Change EHE has only the limited Stage A logical admission described in
`docs/CHANGE_MODEL_ADMISSION.md`; later ecosystem extensions remain experimental.
Current source reports use `change-seed/0.18.2+seed-rules/0.4.0`, and current
portfolio reports use `change-portfolio/0.2.3`. Author-period manifests keep a
10,000-candidate exact in-window identity ledger per repository, accept up to 64
repositories, stream lifetime identity-prefiltered metadata so out-of-window
matches do not consume the ledger, impose no presentation-row cap, and run at most
two repository sessions concurrently under fixed per-repository cache bounds,
including 8,192 immutable analyzer-versioned file artifacts with deterministic
key-ranked retention, 10,000 structurally shared inventories across 16 full-tree
roots, and lazy object-length metadata. Eligible non-merge first-parent deltas and
changed-blob sizes are batched once per repository before row analysis with a
64-MiB output cap and exact per-row fallback. Delta chunks, immutable snapshot
pairs, and common file inspections now use bounded producer/consumer pipelines;
common workers read earlier files while traversal discovers later paths, under a
process-wide buffered-read bound. Common/semantic file work and thread-safe seed
estimation share a 24-logical-processor ceiling with deterministic single-flight
reuse. The CLI and Change benchmark request server GC while all repository,
cache, queue, and read-buffer bounds remain fixed. Protocol `change/1.11.0`
retains exact changed/context/representative scope and the storage-aware
scheduler: packed and small loose stores use one recursive tree traversal; large
loose stores use at most four shards per tree and
eight Git readers process-wide, independently of the at-most-24 managed CPU work
items. It additionally reuses exact first-parent evidence for unchanged scopes
and proven same-size C# numeric-token edits under an eight-state/16-MiB decoded-
text cache; every unproven case uses full analysis. On the prepared 512-change/
1,024-snapshot eight-worker checkpoint, median wall time falls from 11.399 to
6.279 seconds (`1.82x`), allocation falls 49.8%, sampled peak working set falls
19.5%, and estimate semantics are unchanged. This is a narrow work-elimination
result, not general field latency or core scaling. Issue #182 remains open. The
public alpha.6 control remains 3.01x faster end to end and 8.17x faster in
snapshot/diff work with identical output. The defects tracked by #157 and the
private A/B/A+B retest tracked by #176 are complete.

Time-bucketed author-period comparison uses versioned bucket, capacity, and
comparison-report contracts. Calendar-month, calendar-week, and exact custom
partitions are alternative views of one jointly reconciled portfolio; optional
capacity is only a denominator. The default joint contributor series are additive
but membership-dependent; optional isolated series are membership-stable
canonical sums that can overlap and are explicitly non-additive. JSON, final trend
Markdown, and generic engineering-findings Markdown share the same semantic
result, keep shared-credit groups separate, exclude paths/aliases, and exclude
operational timings/resources from the semantic digest. Repository evidence
checkpoints are digest-bound and selectively invalidated. Failed shards retain
resumable completed evidence and emit nonzero incomplete artifacts with root
failure/last-progress context but no aggregate EHE or trend.
