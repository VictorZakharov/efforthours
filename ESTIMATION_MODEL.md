# Estimation Model

## 1. Purpose

This document defines the planned semantics of a Fairbill estimate. It is the
contract between repository analyzers, estimation engines, reports, calibration
data, and reviewers in a host AI session.

## 2. Estimate definition

For a repository `R`, profile `P`, baseline `B`, and rate card `M`, Fairbill
produces:

- an evidence set describing the current state of `R`;
- a ledger of small work items needed to recreate that state under `P` and `B`;
- low, expected, and high Equivalent Human Effort totals; and
- Equivalent Replacement Cost calculated from effort and `M`.

The effort estimate and pricing calculation are separate. Changing the hourly rate
must never change the estimated hours.

## 3. Baseline assumptions

Unless a report overrides them, the baseline is:

- one competent senior contractor;
- familiar with the implementation ecosystem but not the business domain;
- a clear specification at the level defined by the selected profile;
- mainstream tools, reusable packages, and modern equivalent technology available
  in 2026;
- no AI used by the modeled contractor;
- no organizational waiting time or multi-person communication overhead;
- no knowledge of the repository's actual development history; and
- recreation of the current artifact, not completion of an imagined ideal product.

The report must serialize these assumptions rather than relying on undocumented
defaults.

## 4. Current-state valuation rules

### 4.1 No history

Git commits, churn, author count, timestamps, branches, and abandoned versions are
not estimation inputs. An analyzer may honor ignore files, but it must not derive
effort from repository history.

### 4.2 Clean competent recreation

Estimate the work needed to achieve equivalent results with a reasonable
implementation. Discount duplication, dead code, accidental complexity, and
unnecessary boilerplate. Preserve inherent domain complexity and externally
meaningful constraints.

### 4.3 Third-party and generated code

Do not estimate reimplementation of frameworks or dependencies. Estimate the work
to select, configure, integrate, adapt, validate, and document them.

Generated artifacts contribute only the effort represented by generator selection,
configuration, templates, customization, validation, and maintained hand-written
changes. Vendored and copied content should be identified and excluded or valued
according to the integration work it actually represents.

When practical, an analyzer should compare a generated artifact's shape with the
known generator, template, or conventional output and value only meaningful
customization. When that comparison is not reliable, excluding the generated body
with an explicit note is preferable to valuing it as hand-written production code.

### 4.4 Tests

Automated-test effort reflects the tests currently represented by the repository.
Unit, integration, contract, component, UI, and end-to-end tests must be identified
separately when evidence allows.

If a reliable coverage artifact exists, report its relevant line, branch, method,
or statement measures with provenance. On the default fast static path, discovered
tests are assumed to pass. When coverage configuration explicitly declares a
coverage level, Fairbill may assume that declared level is achieved; in particular,
a declared 100% level is treated as 100% coverage. Such a claim must be labeled
`declared-assumed`, not `measured`. Configuration that declares no coverage level
must not be translated into an invented percentage.

A repository with 80% measured coverage is valued at that observed level. A
repository with more extensive, meaningful coverage represents more testing effort.
Raw test counts must be moderated for generated tests, repetition, triviality, data
driven cases, and complexity.

Reasonable manual validation and debugging required to create working behavior are
included and remain distinct from automated-test creation. The amount should use
engineering judgment and the observed complexity rather than a fixed percentage.

### 4.5 Documentation

Documentation effort reflects the material currently present: onboarding,
architecture, API reference, tutorials, operational runbooks, examples, diagrams,
inline documentation, and other maintained guidance.

Missing documentation does not add represented effort. Reasonable missing work may
appear in the separate professionalization gap.

### 4.6 Incomplete and defective state

Estimate the system as fully working according to its supplied specification and
the behavior materially described by the current repository. TODOs, failing tests,
build failures, stubs, and obvious incomplete areas must add a visible warning or
asterisk to the report. They do not cause Fairbill to value historical repair work,
and the report must not falsely claim that the analyzed checkout was verified as
working. Compatibility quirks that form part of an external contract may remain in
scope; accidental defects do not add value.

## 5. Profiles

### 5.1 `implementation`

Assumes detailed requirements and available design inputs. Includes specification
comprehension, technical design, implementation, validation, and the repository's
represented supporting artifacts. Excludes discovery and creation of supplied
product or visual design.

### 5.2 `recreation`

Assumes a clear behavioral specification. Adds reasonable effort to infer or make
architecture, data, UX, interface, and other design decisions embodied in the
artifact. It does not add historical rework or open-ended stakeholder discovery.

Profile-specific work must appear as explicit ledger items rather than an opaque
multiplier whenever practical.

## 6. Effort categories

The initial taxonomy is:

1. Specification comprehension and bounded domain learning
2. Repository and solution setup
3. Architecture and technical design
4. Production implementation
5. UI implementation and represented UX decisions
6. Data modeling, persistence, and migrations
7. External integrations and protocols
8. Unit testing
9. Integration, contract, and component testing
10. End-to-end and UI testing
11. Manual validation, debugging, and hardening
12. Documentation
13. Build configuration and developer tooling
14. CI/CD and infrastructure as code
15. Security and accessibility work represented by the artifact
16. Packaging, deployment preparation, and release artifacts
17. Self-review and integration of the completed system

The taxonomy is versioned. Categories may be added or refined, but a schema change
must preserve the meaning of previous reports.

## 7. Work-item ledger

Large repository totals are derived from small work items. A work item should
normally represent 0.5 to 8 expected hours and contain at least:

```json
{
  "id": "stable-item-id",
  "category": "unit-testing",
  "title": "Invoice calculation tests",
  "scope": "Billing.Tests",
  "evidence": ["evidence:test-suite:invoice-calculator"],
  "quantity": 14,
  "complexity": "moderate",
  "hours": {
    "low": 3.0,
    "expected": 4.0,
    "high": 6.0
  },
  "confidence": 0.88,
  "reason": "Parameterized coverage of tax, discount, and rounding rules",
  "estimator": "rules-v1"
}
```

The final schema will additionally need:

- profile applicability;
- parent/child relationships;
- dependency and correlation groups;
- model and rule versions;
- assumptions and exclusions;
- uncertainty reasons;
- human or AI adjustments; and
- links back to source facts without embedding secrets or excessive source text.

Cross-cutting effort should become named work items. Blanket percentage overheads
are a fallback that must be visible and justified.

## 8. Repository evidence

Evidence is a versioned, language-neutral intermediate representation. Initial
evidence families should include:

- repository scope, files, languages, and sizes;
- project/package graph and architectural boundaries;
- build targets and application entry points;
- API endpoints, routes, commands, jobs, and event handlers;
- UI components, pages, navigation, and state-management structures;
- data entities, schemas, migrations, queries, and persistence mechanisms;
- external services, protocols, queues, and other integrations;
- authorization, authentication, validation, and security mechanisms;
- source complexity and duplication signals;
- generated, vendored, binary, minified, and unsupported content;
- automated tests by type, fixtures, mocks, and coverage artifacts;
- documentation by type and scope;
- build, CI/CD, containers, deployment, and infrastructure configuration; and
- analyzer warnings, gaps, and confidence.

Every fact needs a stable evidence ID, analyzer provenance, and location. Source
snippets should not be emitted by default.

## 9. Estimation layers

The planned estimator is hybrid.

### 9.1 Deterministic rules

Rules convert well-understood repository facts into work units, enforce exclusions,
apply transparent productivity priors, and provide guardrails against impossible
or perverse results.

### 9.2 Local ML

Local models may classify repository structures, recognize complexity archetypes,
predict category-level effort, estimate quantiles, and detect repositories or work
items outside the calibration distribution.

Structured/tabular models are the expected starting point. The runtime may use
ML.NET or ONNX; the runtime format should not unnecessarily constrain the training
toolchain.

### 9.3 Host AI adjudication

Fairbill is expected to be callable from an AI development session. The host AI can
consume compact, low-confidence work-item packets, invoke additional Fairbill
queries, inspect selected source when necessary, and refine semantic classification,
assumptions, ranges, or effort. Adjustments should be recorded with their reason and
available model identity.

The core CLI requires no embedded AI provider. Concrete time, token, and monetary
budgets will be chosen only after representative implementations are measured.
Users who run Fairbill through an AI session are responsible for that session's
provider, tool, disclosure, and privacy policies.

## 10. Calibration

Strong AI estimates can serve as teacher labels even when historical labor data is
unavailable. They are weak supervision rather than literal ground truth.

The calibration process should:

1. Analyze representative repositories into normalized evidence.
2. Decompose them into small work items.
3. Ask a consistent strong-AI rubric to estimate item categories and ranges.
4. Review and correct disputed or implausible labels.
5. Train local category and quantile models on the reviewed ledger.
6. Split training and evaluation by repository, never randomly by file.
7. Measure category error, total error, interval coverage, and calibration.
8. Retain the teacher rationale and evidence for diagnosis.
9. Add voluntary reviewed corrections over time.

Every distributable calibration record, benchmark repository, model, and derived
artifact needs recorded provenance and terms compatible with Fairbill's MIT
License. Private client evidence may be used only in its authorized environment and
must not be added to the public corpus by default.

The guiding premise is that a four-hour work item can be judged and calibrated more
accurately than a single four-hundred-hour total.

## 11. Uncertainty

The CLI should resolve structural facts and routine cases. Semantic ambiguity and
novel complexity may be deferred to the host AI session when useful.

Uncertainty output should distinguish:

- incomplete or conflicting evidence;
- unsupported languages or frameworks;
- semantic ambiguity;
- out-of-distribution inputs;
- profile assumptions;
- execution not performed;
- absent specification; and
- model uncertainty.

How item-level uncertainty is aggregated, including correlated uncertainty, remains
an implementation research item. Reports must not imply that ranges are formal
probability intervals until they have been calibrated as such.

## 12. Pricing

Pricing is a final, replaceable layer:

```text
Equivalent Replacement Cost = Equivalent Human Effort × hourly market rate
```

The default rate card will contain:

- a documented nationwide US senior independent-contractor bill rate, including
  the normal overhead represented by an independent-contractor rate;
- a source or methodology;
- an effective date and currency;
- a reasonable market range; and
- a schema version.

Callers can provide an exact override. Future regional profiles must not change the
underlying effort estimate.

## 13. Professionalization gap

The primary EHE estimate values the current artifact. A separate gap report may
estimate reasonable missing work such as:

- absent or weak automated tests;
- missing documentation;
- accessibility improvements;
- security hardening;
- operational readiness; and
- build or deployment repair.

Gap work must never be silently added to represented EHE.

## 14. Open decisions and measurements

The remaining questions require implementation evidence or external research:

1. The contractor-rate source and exact 2026 methodology.
2. Quantitative calibration and accuracy thresholds for held-out repositories.
3. Practical token, time, and monetary measurements for host-AI adjudication.
4. Exact benchmark hardware and memory limits for the one-million-line performance
   target.
5. Whether self-contained executables should supplement the primary .NET global
   tool package.
