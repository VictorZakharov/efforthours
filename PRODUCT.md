# Fairbill Product Charter

## Problem

A capable AI can often form a reasonable software-effort estimate by inspecting a
repository and logically decomposing it. On a large repository, however, giving a
strong remote model enough source context can take a long time and cost hundreds
of dollars per estimate.

Fairbill should compress the repository into the facts and small work units that
matter for estimation. Its normal path must be fast, local, explainable, and much
less expensive than asking a model to read the complete source tree.

## Core metric

**Equivalent Human Effort (EHE)** is the estimated number of human-hours required
for one competent senior contractor to recreate the current repository from a
clear specification under the selected estimation profile.

The modeled contractor:

- is technically competent and productive;
- is familiar with the relevant software ecosystem;
- is initially unfamiliar with the product's business domain;
- works alone, so the result measures human-hours rather than team elapsed time;
- uses ordinary 2026 development tools, documentation, search, templates,
  generators, and third-party libraries; and
- does not use AI while performing the modeled work.

The last point applies to the counterfactual worker. Fairbill itself may use local
ML, and the AI session orchestrating Fairbill may use any tools available to it.

## Recreation target

The primary target is functional and quality equivalence, not line-for-line source
reproduction. The recreated system should provide the same meaningful behavior,
interfaces, integrations, tests, documentation, infrastructure, and observable
quality represented by the repository.

A clean and competent implementation is assumed. Duplicate code, dead code,
accidental complexity, abandoned approaches, and repeated historical rework must
not increase EHE merely because they increase repository size.

Meaningful constraints remain in scope. Compatibility requirements, protocols,
framework choices, data formats, and externally visible behavior cannot be erased
just because a different greenfield design might be easier.

Recreation assumes a sensible modern 2026-equivalent implementation. It is not a
legacy-restoration exercise. Compatibility behavior and externally meaningful
constraints remain, while obsolete implementation mechanics need not be reproduced
when current technology provides an equivalent result.

## Estimation profiles

Fairbill will support two profiles.

### Implementation profile

The contractor receives detailed requirements, acceptance criteria, UI designs,
API contracts, and other implementation inputs that exist for the product. The
estimate includes technical design and implementation decisions but excludes
product discovery and creation of supplied design artifacts.

### Recreation profile

The contractor receives a clear behavioral specification but must recover or make
more of the architecture, data-model, interface, and UX decisions needed to
recreate the artifact. It still excludes open-ended stakeholder discovery unless a
future profile explicitly adds it.

Both profiles should be reportable from the same repository evidence. A supplied
specification is optional; without one, Fairbill infers the product surface and
reports lower confidence where appropriate.

## Goals

- Estimate the current repository state without using Git history or inferred
  historical churn.
- Produce low, expected, and high hours with an explicit confidence assessment.
- Break effort down by category and small, inspectable work items.
- Attach repository evidence and reasoning to every material estimate.
- Reflect the actual level of automated tests and documentation present.
- Separate represented effort from a professionalization or remediation gap.
- Supply a reasonable, dated 2026 US senior-contractor rate and allow overrides.
- Work without network access or remote AI by default.
- Give AI agents compact output designed for inexpensive final adjudication.
- Mark coverage and other claims as measured, declared-and-assumed, or inferred.
- Scale from small repositories to large mixed-language repositories.
- Support .NET and JavaScript/TypeScript first, then become polyglot through
  analyzer extensions.
- Estimate the Equivalent Human Effort represented by a completed base-to-head
  change, one commit, a revision range, or one GitHub pull request without treating
  commit activity or elapsed history as effort evidence.
- Be suitable for public open-source development and redistribution.

## Non-goals

- Reconstructing actual hours worked.
- Treating commit counts, author activity, file timestamps, or code churn as labor
  evidence.
- Rewarding verbosity, generated code, copied code, or poor architecture.
- Predicting schedules for a multi-person team.
- Performing full project management, staffing, or delivery-date forecasting.
- Claiming that an estimate is an invoice or a record of historical labor.
- Requiring source code to be uploaded to an external service.
- Replacing a security audit, accessibility audit, legal review, or accounting
  opinion.

## Primary outputs

A completed estimate should contain:

- repository identity and analyzed scope;
- estimator, schema, model, and rate versions;
- estimation profile and assumptions;
- low, expected, and high EHE totals;
- equivalent replacement-cost totals;
- category breakdowns;
- a repository-first work-item ledger;
- evidence references for each work item;
- confidence and unresolved uncertainties;
- detected generated, vendored, excluded, or unsupported content;
- represented test and documentation effort;
- a separate professionalization gap; and
- warnings about incomplete, unbuildable, or out-of-distribution repositories.

Feature-oriented reporting is intentionally deferred until repository-level
analysis is trustworthy.

The experimental incremental-change mode estimates the functional and quality
delta for immutable base/head snapshots, one commit, a revision range, or one
GitHub pull request. The optional `gh` adapter resolves only the PR number or URL
and immutable base/head object IDs; analysis then remains local. Multiple-PR and
author-and-period portfolios are deferred. Commit count, activity, timestamps,
review duration, and discarded intermediate revisions remain excluded as effort
signals. The result is still EHE, not actual hours worked or a standalone measure
of an employee's performance. See `CHANGE_ESTIMATION.md`.

## Product principles

### Open-source from the start

Although publication may happen after the first useful build, the repository should
be maintained as if it were already public. Dependencies, model files, calibration
data, fixtures, and copied assets need clear provenance and redistribution-compatible
terms compatible with the MIT License. Client repositories, proprietary source,
credentials, and private estimation inputs must never become project fixtures or
committed calibration data.

### Evidence before inference

Objective facts must remain distinguishable from inferred work and estimated
hours. Reports must make that boundary visible.

### Small estimates compose better

Fairbill should prefer work items that normally represent roughly 0.5 to 8 hours.
A large item should be decomposed further or explicitly explain why it cannot be.

### Current state, not historical struggle

If a feature was rewritten ten times, Fairbill estimates a competent recreation of
the current result once.

### Artifact value is not artifact volume

Lines of code may be an input signal but never the principal value measure.
Behavior, complexity, quality, constraints, and supporting artifacts matter more.

### Offline first

The core CLI path should be deterministic and local. The host AI session can use
the evidence and any other tools it has, but Fairbill should not require an embedded
AI provider or full-source model ingestion.

### Honest uncertainty

Unknowns should widen ranges or be surfaced for review. They must not be silently
converted into false precision.
