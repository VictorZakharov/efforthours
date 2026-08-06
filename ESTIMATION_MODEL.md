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

The estimator is hybrid.

### 9.1 Deterministic rules

Rules convert well-understood repository facts into work units, enforce exclusions,
apply transparent productivity priors, and provide guardrails against impossible
or perverse results.

The current implementation is `seed-rules/0.2.1`. Its checked-in JSON artifact is
validated against the published seed-model schema and embedded for deterministic
offline loading. Version 0.2.1 retains every numerical prior from 0.2.0 and fixes
TypeScript file ownership in the shared JavaScript/TypeScript estimation scope so
exact duplicates and test structure are normalized consistently. Before applying
priors, the estimator resolves project/package scope and role, separates production
and test structure, gives fine semantic facts precedence over broad aggregates,
and normalizes byte-identical maintained bodies.

The seed model combines a lower-rate general implementation backbone with explicit
specialized work for API, UI, data, integration, security, validation, background,
test, documentation, delivery, manual-validation, and review boundaries. Marginal
tiers reduce the value of repetition while retaining first-time setup work.
Capabilities are partitioned around four expected hours and must remain within the
normal 0.5-to-8-hour expected range. Profile differences are explicit work items;
professionalization gaps are separate. These mechanics are implemented and tested,
but their numerical priors remain uncalibrated.

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

Milestone 7A implements the first calibration boundary. A versioned corpus stores
reviewed target work units separately from candidate `EstimateReport` documents.
All revisions and both profiles of a stable repository identity must stay in one
development, validation, or test partition. The offline evaluator requires an
explicit partition and measures low/expected/high error, weighted absolute
percentage error, signed bias, reviewed-point and reviewed-range coverage, and
work-item mapping coverage at item, category, and repository-total levels. Pricing
is not a calibration label or metric.

Metric semantics are versioned as `calibration-metrics/1.0.0`. WAPE is defined as
`sum(abs(candidate - reviewed)) / sum(reviewed)` and is used instead of ordinary
MAPE because category observations may legitimately be zero. Interval results are
agreement diagnostics against weak reviewed labels; they do not yet make low/high
ranges formal probability intervals. `MILESTONE_7.md` and the
published schemas define the full metric boundary.

Milestone 7B1 adds a low-cost authoring boundary. A canonical estimate can be
projected into a schema-versioned packet whose status is always `unreviewed` and
which cannot be consumed as a corpus. Candidate values are visibly reference-only;
blind mode removes candidate hours, category totals, and confidence. A separate
completed review plan records explicit capability-level target ranges and
rationale. Compilation requires the exact source-estimate digest, a decision for
every represented capability, and complete source-work-item/evidence lineage before
it can emit a valid corpus. This prevents mechanically copied scaffold output from
silently becoming a reviewed label.

Milestone 7B2 adds a separate subsequent-review boundary around an existing
corpus. A corpus-review packet is always unreviewed; blind mode removes prior
ranges, rationale, uncertainty decisions, and totals. Compilation pins the exact
canonical source-corpus digest, requires an accept/replace decision for every
record and target, rejects reviewer identities already present in source
provenance, preserves all structural lineage, and prevents maturity downgrades.
This tooling does not itself constitute independent correction: maturity advances
only when a genuinely distinct reviewer or adjudicator completes a plan.

The same slice adds versioned mutation guardrails. A mutation suite compares a
subject and reference canonical estimate at one repository/category low, expected,
or high point and asserts inclusive bounds on `subject - reference`. These
relations test invariance, directionality, and category isolation. They are not
effort labels and cannot be used as numerical training targets. The public 0.2.0
suite covers 30 small .NET, parser-backed JavaScript, token-backed TypeScript, and
mixed-repository source states with 84 passing relations. It includes all three
range points, exact duplication, conventional generated output, separately
maintained generated customization, API/UI behavior, tests, documentation,
integrations, and category isolation. Near-duplicates and more complex behavior
families remain required before learned-model admission.

The initial public pilot freezes three MIT-licensed repository families across
development, validation, and test partitions and measures `seed-rules/0.2.0`.
Those labels have one host-AI teacher and no independent correction. They are
preliminary weak supervision and do not make the seed model calibrated or
production-ready. `MILESTONE_7.md` and the
`ehe-work-item/1.0.0` rubric define the complete implemented policy and the gates
for admitting a learned model.

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

The bundled `us-senior-software-contractor/2026.1` rate card contains:

- a documented nationwide US senior independent-contractor bill rate, including
  the normal overhead represented by an independent-contractor rate;
- a source or methodology;
- an effective date and currency;
- a reasonable market range; and
- a schema version.

The nationwide default is $160 USD/hour, with a disclosed $125-$200 market
reference. It starts from May 2025 BLS OEWS Software Developer median, 75th, and
90th-percentile wages, applies the March 2026 BLS ECEC professional-occupation
total-compensation-to-wage ratio, divides by an explicit 75% billable-utilization
assumption, and rounds each point to the nearest $5/hour. OEWS excludes
self-employed workers, so the observations are transparently treated as employee
wage anchors rather than contractor-rate measurements.

The complete inputs, series IDs, source release dates, formula, assumptions, and
public-domain provenance are stored under `rates/us-senior-contractor/` and
described in `MILESTONE_6.md`. Callers can provide an exact override or request
effort-only output. Future regional profiles must not change the underlying effort
estimate.

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

1. Quantitative calibration and accuracy thresholds for held-out repositories.
2. Practical token, time, and monetary measurements for host-AI adjudication.
3. Exact benchmark hardware and memory limits for the one-million-line performance
   target.
4. Whether self-contained executables should supplement the primary .NET global
   tool package.
