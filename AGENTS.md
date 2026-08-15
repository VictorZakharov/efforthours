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
- Use ripgrep (`rg`) for searches; it is installed. A restricted shell failure is
  a sandbox/PATH issue, not evidence that `rg` is absent.
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
- Publish only the exact verified prerelease tag and artifact. After approval,
  require a successful publish job, NuGet indexing, and a clean public-feed
  install before creating the matching GitHub prerelease.

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
model is frozen.
Change EHE has only the limited Stage A logical admission described in
`docs/CHANGE_MODEL_ADMISSION.md`; later ecosystem extensions remain experimental.
