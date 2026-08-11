# Estimation Model

## 1. Purpose

This document defines the planned semantics of a EffortHours estimate. It is the
contract between repository analyzers, estimation engines, reports, calibration
data, and reviewers in a host AI session.

## 2. Estimate definition

For a repository `R`, profile `P`, baseline `B`, and rate card `M`, EffortHours
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

### 4.1 No history in repository estimates

Ordinary repository `scan` and `estimate` operations do not inspect Git commits,
churn, author count, timestamps, branches, or abandoned versions. An analyzer may
honor ignore files, but it must not derive repository EHE from development history.

Explicit Change EHE may resolve requested revisions, read immutable base/head
trees, and estimate selected commits independently for reconciliation. That access
selects final artifacts; commit count, identity, timing, messages, branch activity,
and intermediate churn never multiply effort. The normalized final base-to-head
delta remains authoritative. See `CHANGE_ESTIMATION.md`.

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
coverage level, EffortHours may assume that declared level is achieved; in particular,
a declared 100% level is treated as 100% coverage. Such a claim must be labeled
`declared-assumed`, not `measured`. Configuration that declares no coverage level
must not be translated into an invented percentage.

The current static analyzer parses checked-in LCOV and Cobertura reports without
executing tests. It verifies the report against the common-scanner content digest,
maps covered source paths to maintained production project or package scopes, and
emits `measured` evidence without copying source paths from the report. When a
measured report and a configured threshold describe the same scope, the measured
level is valued and the declaration remains non-valued evidence; the two values are
never averaged. A report can be stale or belong to another checkout, so that
limitation remains explicit even when its artifact digest is verified.

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
asterisk to the report. They do not cause EffortHours to value historical repair work,
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

The current implementation is `seed-rules/0.4.0`. Its checked-in JSON artifact is
validated against the published seed-model schema and embedded for deterministic
offline loading. Version 0.4.0 retains every 0.3.0 rule unchanged and adds the
language-neutral source backbone described in section 13. Version 0.3.0 retains
every non-UI numerical prior from 0.2.1 and replaces the UI rule's physical
asset-line driver with bounded semantic units. Version 0.2.1 remains the frozen
source estimator for the existing reviewed corpora and first four mutation
checkpoints. Before applying priors, the estimator resolves project/package scope
and role, separates production and test structure, gives fine semantic facts
precedence over broad aggregates,
and normalizes byte-identical maintained bodies.

Frontend semantic evidence is deterministic and formatting-insensitive. Static
Angular metadata requires a named `Component` import from `@angular/core`
(including a local alias) and accepts only literal strings and arrays; relative
external assets must resolve to digest-verified scanner-admitted files.
HTML/template and CSS-family scanners count bounded structural constructs rather
than raw text volume. They do not render, compile frameworks, execute
preprocessors, or establish runtime reachability. JavaScript analyzer `0.5.1`
records explicit static roles, labels, alternative text, live regions, and
keyboard/focus signals from maintained HTML and Angular templates as represented
accessibility work under the existing combined security/accessibility prior. This
is bounded implementation evidence with `accessibility-conformance:not-proven`;
it is not an accessibility audit or runtime conformance result.

SQL analyzer `0.1.0` uses a bounded token/statement stream rather than raw lines or
row volume. Recognized schema, migration, stored-program, query, seed, test,
delivery, and cross-database facts map to the unchanged `seed-rules/0.3.0` data,
testing, packaging, and integration drivers. Exact bodies are valued once; dumps
and conventional generated snapshots are excluded; seed rows collapse to bounded
intent. Parser confidence and dialect confidence are explicit uncertainty inputs,
not proof of validity for a selected database. No SQL-specific numerical prior was
fit, and the SQL path remains uncalibrated. `SQL_ANALYSIS.md` defines limitations.

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

EffortHours is expected to be callable from an AI development session. The host AI can
consume compact, low-confidence work-item packets, invoke additional EffortHours
queries, inspect selected source when necessary, and refine semantic classification,
assumptions, ranges, or effort. Adjustments should be recorded with their reason and
available model identity.

The core CLI requires no embedded AI provider. Concrete time, token, and monetary
budgets will be chosen only after representative implementations are measured.
Users who run EffortHours through an AI session are responsible for that session's
provider, tool, disclosure, and privacy policies.

The first `host-review/1.0.0` checkpoint implements this as an optional protocol
around a complete rate-free local estimate. A compact packet binds the complete
estimate and evidence with a canonical SHA-256 input digest. Follow-up capability,
evidence, scope, and explicitly selected-source queries must repeat that digest.
The adjustment ledger records affirm/replace intent, the exact original range,
supporting evidence, rationale, and available model identity. Validation checks
identity and lineage but does not apply the ledger or establish correctness,
calibration, or independence. `MILESTONE_8.md` defines the exact boundary.

## 10. Calibration

Strong AI estimates can serve as teacher labels even when historical labor data is
unavailable. They are weak supervision rather than literal ground truth. A
disclosed host-AI teacher can satisfy a logical calibration or admission gate when
the total reconciles exactly from distinct evidence-backed tasks small enough to
audit, normally about 0.5 to 1.5 expected hours for the current one-to-several-day
band. Independent replication is useful optional corroboration, not a prerequisite
for that logical judgment. It does not change `teacher-estimate` maturity or imply
human review, empirical accuracy, or production validation.

The calibration process should:

1. Analyze representative repositories into normalized evidence.
2. Decompose them into small work items.
3. Ask a consistent strong-AI rubric to estimate item categories and ranges.
4. Audit and correct disputed, unsupported, or non-reconciling labels.
5. Train candidate local category and quantile models only after their applicable
   admission policy is frozen.
6. Split training and evaluation by repository, never randomly by file.
7. Measure category error, total error, interval coverage, and calibration.
8. Retain the teacher rationale and evidence for diagnosis.
9. Add voluntary reviewed corrections over time.

Every distributable calibration record, benchmark repository, model, and derived
artifact needs recorded provenance and terms compatible with EffortHours's MIT
License. Private client evidence may be used only in its authorized environment and
must not be added to the public corpus by default.

The guiding premise is that small logical work items can be judged and audited more
reliably than one unsupported large total. Production observations collected later
retain separate empirical provenance and never become effort multipliers.

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

Milestone 7B5 adds an explicit reviewed-exclusion representation. A source target
that is a false positive or wholly excluded by the rubric may use exactly
`0/0/0` low, expected, and high hours only when both its rationale and
`sizeException` explain the exclusion. A partially positive zero range is invalid,
and zero must not represent ordinary uncertainty, reuse, or a discount. Review
compiler versions `0.2.0` implement this rule while retaining deterministic
compatibility for positive `0.1.0` plans. The versioned
`ehe-work-item/1.1.0` rubric documents the policy without changing the earlier
positive-label semantics.

The same slice adds versioned mutation guardrails. A mutation suite compares a
subject and reference canonical estimate at one repository/category low, expected,
or high point and asserts inclusive bounds on `subject - reference`. These
relations test invariance, directionality, bounded marginality, and category
isolation. They are not effort labels and cannot be used as numerical training
targets. The public 0.7.0 suite covers 77 small .NET, parser-backed JavaScript,
token-backed TypeScript, token-backed SQL, standalone frontend, Angular, and
mixed-repository source states with 309 passing relations. It includes all three
range points, exact duplication, conventional
generated output, separately maintained generated customization, bounded renamed
near-copies, two specified non-exact equivalent-purpose shapes,
compiler-disabled and explicitly included C# syntax, bounded intra-file private
method reachability, API/UI/data/security/accessibility behavior, accessibility-
focused test depth, tests,
declared-and-assumed and measured coverage levels, measured-over-declared
precedence, documentation, integrations, workspace boundaries, frontend
formatting/duplication/semantic behavior, SQL formatting/dumps/unknown syntax/
semantic directionality/roles/seed-volume bounds, CI, containers, and category
isolation. Its representative four-package and mixed dependency graphs are small
synthetic boundaries, not large-repository benchmarks. It does not provide general
semantic-clone detection, liveness/reflection/dynamic-dispatch analysis, broad JSX
accessibility semantics, or multiple observations per ecosystem/partition cell;
those remain model-risk inputs for learned-model admission.

The public pilot and public expansion freeze six MIT-licensed repository families
across development, validation, and test partitions and measure
`seed-rules/0.2.0` or `seed-rules/0.2.1` against the source estimate each teacher
reviewed. Those labels share one host-AI teacher and have no independent
correction. They are preliminary weak supervision and do not make the seed model
calibrated or production-ready. `MILESTONE_7.md`, the versioned rubrics, and the
blind packets under `calibration/corpora` define the implemented policy. Their
decomposed host-AI judgments may support a future logical admission decision with
explicit gates; independent replication remains available as optional
corroboration, and later production observations are a separate empirical track.

Change EHE reuses this corpus and metric boundary with additional immutable
final-delta provenance. A Change calibration source digest is derived from the
base and head repository-evidence digests, while selector kind, object IDs, and
coverage tags remain non-valuing provenance. Every change from one repository
family stays in one partition. Empty targets explicitly represent a normalized
zero final delta without inventing effort; exact `0/0/0` targets preserve lineage
for reviewed false-positive exclusions. `change-ehe-work-item/1.1.0`,
`MILESTONE_CHANGE_2.md`, `MILESTONE_CHANGE_3.md`, and
`CHANGE_MODEL_ADMISSION.md` define the current checkpoints. A disclosed host-AI
teacher may supply logical weak supervision when the estimate reconciles exactly
from evidence-backed tasks normally about one hour each. The maturity remains
`teacher-estimate`; logical admission does not imply human review, empirical
accuracy, calibrated probability intervals, or production readiness. Frozen
rubric-1.0.0 labels are not rewritten; the separate Stage A logical audit maps all
eligible parent targets into rubric-1.1.0 tasks while preserving their exact
expected totals and uncertainty provenance.

The current `change-seed/0.8.0` rules retain the 0.3.0 correction that keeps
repository seed capabilities as context for a final delta but does not infer a
capability modification from path overlap alone. Existing capabilities require
changed normalized non-file evidence;
unchanged broad setup, architecture, UI, validation, and review context cannot each
charge an independent minimum for the same edit. Repository work-item partitions
for one existing or modified capability do not contribute their summed prior.
Instead, capped edit-region bands contribute one to four logical units per changed
path to one diminishing budget; distinct newly added capabilities remain additive.
Unmapped modified artifacts use the same bounded category-and-status units,
followed by one change-level comprehension, manual-validation, and self-review
item. This remains an experimental transparent correctness revision, not
calibrated-model admission.

Version 0.7.0 adds SQL-aware formatting normalization and carries scanner-derived
SQL role tags into Change path evidence. Meaningful schema/query deltas map to
data, test fixtures to integration/component testing, deployment scripts to
packaging, and explicit cross-database syntax to integrations. It adds no fitted
prior and preserves every non-SQL rule. The prior 0.6.0 Stage A admission did not
contain SQL records and therefore does not admit the SQL extension.

Version 0.8.0 adds Python `.py`/`.pyi` final-delta support and an indentation-aware
formatting signature. Comments and horizontal formatting can normalize to zero;
indent depth, identifiers, operators, literals, and docstrings remain meaningful.
Python repository evidence routes API, data, integration, security, validation,
background, and test deltas through the existing category rules. No fitted Change
prior was added. The admitted 0.6.0 Stage A records contain no Python, so this
extension is explicitly unadmitted.

Version 0.4.0 also distinguishes only exact, balanced, EffortHours-specific
`<custom-code>` regions inside otherwise generated files; it does not infer other
generator-specific protected-region syntax. Only the extracted maintained
projection can be represented; conventional generated bytes remain zero.
Unchanged and formatting-only projections remain zero, malformed or unavailable
regions fail closed, and vendored/minified/binary/lockfile/build-output/duplicate
exclusions take precedence. This is a Change evidence-normalization boundary, not
a calibration result.

Version 0.5.0 adds no effort rule or fitted parameter. For explicit multi-commit
ranges it reports expected-point gross-to-final normalization and bounds a separate
rework-like numerator to negative overlap/revert adjustment attribution. Shared or
repeated work and residual interaction remain separate. Zero-gross shares are not
applicable, positive net interaction is preserved, and no low/high percentage is
derived from dependent planning bounds.

Version 0.6.0 adds no fitted numerical prior. It partitions the existing budget of
a mixed-role capability across disjoint production, test, documentation, build,
and delivery evidence without changing its low/expected/high total. Candidate
items above 1.5 expected hours are partitioned into distinct named logical phases
of roughly one hour without changing category totals. Repository analysis is
cached once per immutable snapshot within a Change estimate, optional
per-commit range audits default to 256 components, and exact largest-remainder
allocation keeps every component nonnegative while reconciling to the normalized
total.

The current composite source identity is
`change-seed/0.8.0+seed-rules/0.4.0`; every frozen Change report retains its
original identity and numbers. `change-model-admission/0.2.0` admitted version
0.6.0 only for experimental 4-to-32-hour Stage A changes after model-authored
logical agreement and performance gates. Versions 0.7.0 and 0.8.0 preserve those
non-SQL rules but are not separately admitted, and SQL and Python have no reviewed
Change labels.
Larger size bands and empirical production accuracy remain separate decisions.

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

## 13. Polyglot source backbone and Python

`seed-rules/0.4.0` introduces language-neutral analyzed package and fine-test
contracts plus `polyglot-source-backbone`. The new source rule consumes files,
functions, methods, types, public symbols, async units, and branch points. Its
marginal rates transparently reuse analogous `0.3.0` JavaScript construction rates
with wider uncertainty; they are not fitted calibration. Every existing .NET,
JavaScript/TypeScript, frontend, SQL, and specialized rule remains numerically
unchanged.

Python analyzer `0.1.0` supplies the first evidence to this generic rule. Its
bounded managed tokenizer and indentation pass are explicitly token-backed.
Package ownership comes from static metadata, and framework semantics require
matching import-qualified calls, decorators, or base types. Inventory-only
maintained languages remain visible and emit diagnostics instead of receiving a
guessed source prior. `PYTHON_ANALYSIS.md` defines exact supported inputs,
exclusions, uncertainty, safety, and non-goals.

Public mutation suite `0.8.0` combines 77 unchanged `seed-rules/0.3.0` candidates
with 11 Python `seed-rules/0.4.0` candidates. All 339 relational assertions pass.
This protects directionality and invariance; it does not calibrate absolute hours.

## 14. Professionalization gap

The primary EHE estimate values the current artifact. A separate gap report may
estimate reasonable missing work such as:

- absent or weak automated tests;
- missing documentation;
- accessibility improvements;
- security hardening;
- operational readiness; and
- build or deployment repair.

Gap work must never be silently added to represented EHE.

## 15. Open decisions and measurements

The remaining questions require implementation evidence or external research:

1. Quantitative calibration and accuracy thresholds for held-out repositories.
2. Practical token, time, and monetary measurements for host-AI adjudication.
3. Exact benchmark hardware and memory limits for the one-million-line performance
   target.
4. Whether self-contained executables should supplement the primary .NET global
   tool package.
