# Candidate-blind repository-total cohort freeze

## Status

**The six-case cohort and aggregate assessment rules are frozen before source
review and before any EffortHours output is opened for these sources.** This
checkpoint contains no reviewed hours, seed estimate, manual-QA candidate
estimate, comparison, candidate decision, validation access, or test access.

The shipped `seed-rules/0.4.0` estimator and development-only
`manual-qa-coding-ratio/0.1.0` candidate remain unchanged.

## Frozen boundary

Artifact `repository-total-assessment-cohort/1.0.0` applies development strategy
`repository-total-materiality/1.0.0` to six materially different repository
shapes:

- four immutable public MIT sources: a native desktop utility, a cross-platform
  desktop application, and two browser games with distinct TypeScript/JavaScript
  structures; and
- two locally reviewed private sources represented publicly only as
  `private-product-site-a` and `private-desktop-utility-a`.

The exact public repository identities and object IDs are frozen in
[`repository-total-cohort.json`](repository-total-cohort.json). Exact private
repository identities, paths, source object IDs, and source evidence remain in an
ignored local ledger and must never enter a commit, issue, pull request, test,
fixture, log, or generated artifact.

## Assessment contract

Each completed assessment must:

- use the `implementation` profile and the EHE counterfactual rather than actual
  labor;
- inspect the pinned current artifact without using Git history, authors,
  timestamps, commit counts, or historical rework as effort signals;
- state one credible repository low/expected/high range, assumptions, reviewer
  provenance, and confidence limitations;
- use four to seven source-backed material work areas plus exactly one bounded
  residual for immaterial or poorly estimable work;
- reconcile every range point exactly from work areas and residual to the
  repository total;
- estimate represented functional and quality state, including reasonable manual
  validation, while keeping remediation gaps and pricing separate; and
- become `diagnostic-only` when the reviewed range is too broad to distinguish a
  useful candidate result.

Existing EffortHours reports, seed totals, candidate totals, and comparisons for
all six cases remain hidden until every assessment is frozen in a later commit.
Knowing the candidate's published semantics does not authorize deriving a review
answer from its ratio or preferred result; the review must reason from observable
source-backed recreation activities.

## Later comparison boundary

The later comparison will open seed and exact candidate outputs only after all six
assessments are immutable. It compares repository totals first. An in-range
candidate stops review for that case; a material miss triggers only the
largest-first diagnosis needed to explain the decision under
`repository-total-materiality/1.0.0`. Test remains sealed, and no result in this
development cohort can admit a model without a new policy identity and fresh
holdout boundary.
