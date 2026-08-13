# EffortHours product charter

## Product identity

The product is **EffortHours**, maintained under the **WellScoped** publishing
identity.

- NuGet package: `EffortHours.Tool`
- Installed command: `eh`
- Durable repository and file stem: `efforthours`
- Primary metric: Equivalent Human Effort (EHE)

Documentation uses the full product name for discovery and `eh` in command
examples. Installation guidance must identify `EffortHours.Tool` explicitly
because a two-letter executable can collide with unrelated tools.

## Problem

Repository-level software estimates are often opaque, volume-driven, expensive to
review, or incorrectly presented as reconstructed labor. Sending an entire large
repository to a remote model also creates cost, latency, privacy, and provenance
problems.

EffortHours compresses a repository into the facts and small logical work units
that matter for estimation. Its normal path is local, deterministic, explainable,
and complete without a remote AI provider.

## Core metric

**Equivalent Human Effort** is the estimated number of human-hours required for
one competent senior contractor to recreate the current functional and quality
state from a clear specification under the selected profile.

The modeled contractor:

- is technically competent and productive;
- knows the relevant implementation ecosystem but begins unfamiliar with the
  product's business domain;
- works alone, so EHE measures human-hours rather than team elapsed time;
- uses ordinary 2026 tools, documentation, search, generators, templates, and
  third-party libraries; and
- does not use AI while performing the modeled work.

The no-AI condition applies to the counterfactual worker. EffortHours may use
deterministic analysis, an admitted local model in the future, or an optional
surrounding host-AI review workflow without redefining EHE.

EHE is not actual labor, a timesheet, an invoice, an authorship claim, a
productivity score, or compensation advice.

## Recreation target

The target is functional and quality equivalence, not line-for-line source
reproduction. A competent recreation should preserve the meaningful behavior,
interfaces, integrations, data contracts, tests, documentation, infrastructure,
delivery surfaces, and observable quality represented by the artifact.

The estimate assumes a clean modern 2026-equivalent implementation. Duplicate or
dead code, generated or vendored bodies, accidental complexity, abandoned
approaches, and historical rework do not add EHE merely because they add volume.
Externally meaningful protocols, compatibility requirements, formats, framework
constraints, and behavior remain in scope.

An incomplete or broken checkout is estimated as the working system materially
described by its specification and repository, with a prominent warning that the
checkout was not verified as working. Hypothetical repair and professionalization
work stays outside represented EHE.

## Estimation profiles

| Profile | Supplied inputs and included decisions |
| --- | --- |
| `implementation` | Detailed requirements, acceptance criteria, designs, and contracts are supplied. Technical design and implementation decisions remain included. |
| `recreation` | A clear behavioral specification is supplied. More architecture, data-model, interface, and UX decisions must be recovered or made. |

Both profiles exclude open-ended stakeholder discovery. A specification input is
optional; when it is absent, EffortHours infers the represented product surface
and reports the resulting uncertainty.

## Product surfaces

### Repository EHE

Repository estimation values the current artifact. Ordinary `scan` and `estimate`
commands do not inspect Git history, contributors, churn, timestamps, or abandoned
versions. Evidence is repository-first and every material hour traces through a
small work item to observed facts and a versioned rule or model.

### Change EHE

Explicit Change estimation values the normalized final functional and quality
delta between immutable base and head states. Revisions, commits, ranges, and a
GitHub pull request may select snapshots; commit count, identity, timing, messages,
and intermediate churn never multiply effort. Non-Git directory and saved-evidence
pairs use the same final-delta boundary. `CHANGE_ESTIMATION.md` is the governing
contract.

### Change portfolios

Portfolio mode reconciles selected completed changes without summing exact repeats,
overlap, reversals, or shared context mechanically. Author identity and time may
select rows but never value them. Output is repository-attributed Change EHE, not
individual credit or performance. `CHANGE_PORTFOLIOS.md` defines the safeguards.

### Reporting and pricing

Canonical reports keep evidence, inferred capabilities, estimated work,
uncertainty, review adjustments, and pricing distinct. Compact views remain
projections of the same estimate and retain stable explanation paths.

Pricing is applied only after hours are estimated. The bundled dated market rate
is replaceable and can be disabled; it never changes EHE. See `REPORTING.md` and
`PRICING.md`.

### Optional host-AI review

The local estimate is complete without AI. A surrounding AI session may inspect a
compact provider-neutral packet, make digest-bound follow-up queries, and propose
an evidence-backed adjustment ledger. EffortHours does not select or call a
provider, and the current protocol validates but does not apply adjustments. The
caller owns disclosure, privacy, retention, model choice, and cost. See
`HOST_REVIEW.md`.

## Goals

- Estimate current repository and final-change state without historical activity
  signals.
- Produce low, expected, and high planning bounds with explicit confidence and
  uncertainty.
- Decompose material totals into evidence-backed work items, normally about 0.5
  to 8 expected hours each.
- Reflect the tests, documentation, security/accessibility implementation,
  integrations, operations, and delivery quality actually represented.
- Keep represented effort separate from professionalization or remediation gaps.
- Mark coverage and similar claims as measured, declared-and-assumed, inferred, or
  unverified as appropriate.
- Keep effort independent from a dated, configurable market-rate projection.
- Remain deterministic, offline, read-only, and safe by default.
- Scale across common mixed-language repositories through bounded analyzer
  extensions.
- Give human and AI reviewers compact output with stable drill-down lineage.
- Remain suitable for public MIT-licensed development and redistribution.

The current analyzer families and their exact static boundaries are listed in the
repository `README.md` and routed through `docs/README.md`. Analyzer support does
not imply building, running, rendering, compiling, or proving the target system.

## Non-goals

- Reconstructing actual hours worked or historical rework.
- Treating commit count, author activity, timestamps, or code churn as labor
  evidence.
- Rewarding verbosity, duplication, copied code, generated output, or poor
  architecture.
- Predicting a multi-person schedule or providing staffing and delivery dates.
- Serving as an invoice, employment record, performance grade, ranking, or
  compensation recommendation.
- Replacing security, accessibility, legal, accounting, architecture, or code-
  quality review.
- Requiring repository source to be uploaded to an external service.

## Primary outputs

A repository estimate contains:

- repository identity and analyzed scope;
- schema, analyzer, estimator, model, baseline, profile, and optional rate identity;
- low, expected, and high EHE totals;
- optional Equivalent Replacement Cost totals;
- category, scope, capability, and work-item breakdowns;
- stable evidence and calculation lineage;
- confidence, assumptions, exclusions, diagnostics, and unresolved uncertainty;
- generated, vendored, duplicate, unsupported, and otherwise excluded content;
- represented test and documentation effort; and
- a separate professionalization-gap ledger.

Change and portfolio reports additionally preserve immutable selection identity,
normalization evidence, reconciliation adjustments, allocations, and attribution
uncertainty without copying source excerpts or treating history as value.

## Product principles

### Evidence before inference

Observed facts, inferred classification, estimated work, review decisions, and
pricing stay distinguishable in code and output.

### Small estimates compose better

Large unsupported totals are decomposed into inspectable work. Logical calibration
uses separately reasoned evidence-backed tasks and preserves their provenance;
later empirical observations remain a different evidence track.

### Artifact value is not artifact volume

Lines and file counts can support classification, but meaningful behavior,
constraints, complexity, quality, and maintained supporting artifacts drive the
estimate.

### Offline first

The baseline CLI does not require a network, embedded provider, target build, or
dependency installation. Source trees are untrusted input and remain read-only.

### Honest uncertainty

Unknowns widen ranges or become explicit review questions. Planning bounds are not
formal probability intervals until a versioned calibration decision establishes
that interpretation.

### Open source from the start

Dependencies, datasets, models, fixtures, benchmark repositories, and copied
assets require recorded provenance and redistribution terms compatible with the
MIT-licensed project. Private client source, credentials, and private calibration
material remain outside the public repository.

## Model maturity

The repository estimator is experimental and uncalibrated. Change EHE has only the
limited logical admission described in `CHANGE_MODEL_ADMISSION.md`; it has no
empirical production-accuracy claim. No local ML model is currently admitted.
These limitations must remain visible anywhere estimates are presented.
