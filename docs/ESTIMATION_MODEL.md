# Estimation Model

## 1. Purpose

This document defines the semantics of an EffortHours estimate. It is the
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

The version 1 taxonomy is:

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

The versioned work-item contract also preserves:

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

Evidence is a versioned, language-neutral intermediate representation. Evidence
families include:

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
calibration, or independence. `HOST_REVIEW.md` defines the exact boundary.

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
ranges formal probability intervals. `CALIBRATION.md` and the
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
calibrated or production-ready. `CALIBRATION.md`, `MODEL_ADMISSION.md`, the
versioned rubrics, and the blind packets under `calibration/corpora` define the
implemented boundary. The frozen repository gate accepts decomposed host-AI
judgment as logical weak supervision but requires a larger family matrix, blind
validation, a sealed one-time test, and materially sharper ranges before any
candidate can be admitted. Independent replication remains optional
corroboration, and later production observations are a separate empirical track.

`efforthours-public-readiness/0.1.0` separately freezes the complete 33-family
implementation-profile source matrix before candidate totals. It records immutable
Git trees, license checksums, product shapes, source-only size bands, and inherited
partition ownership. It contains no source estimates or labels and therefore does
not expand the six-family weak-supervision total or authorize fitting. Development
labeling and blind holdout custody remain subsequent checkpoints.

Change EHE reuses this corpus and metric boundary with additional immutable
final-delta provenance. A Change calibration source digest is derived from the
base and head repository-evidence digests, while selector kind, object IDs, and
coverage tags remain non-valuing provenance. Every change from one repository
family stays in one partition. Empty targets explicitly represent a normalized
zero final delta without inventing effort; exact `0/0/0` targets preserve lineage
for reviewed false-positive exclusions. `change-ehe-work-item/1.1.0`,
`CALIBRATION.md`, `calibration/changes/README.md`, and
`CHANGE_MODEL_ADMISSION.md` define the current boundary. A disclosed host-AI
teacher may supply logical weak supervision when the estimate reconciles exactly
from evidence-backed tasks normally about one hour each. The maturity remains
`teacher-estimate`; logical admission does not imply human review, empirical
accuracy, calibrated probability intervals, or production readiness. Frozen
rubric-1.0.0 labels are not rewritten; the separate Stage A logical audit maps all
eligible parent targets into rubric-1.1.0 tasks while preserving their exact
expected totals and uncertainty provenance.

The current `change-seed/0.18.0` rules retain the 0.3.0 correction that keeps
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

Version 0.9.0 adds Go `.go` final-delta support and a token signature that ignores
ordinary formatting/comments while preserving compiler directives, cgo comments,
implicit-semicolon boundaries, identifiers, operators, and literals. Go repository
evidence routes API, CLI, data, integration, security, validation, background,
build, concurrency, and test deltas through existing rules. No fitted Change prior
was added. The admitted 0.6.0 Stage A records contain no Go, so this extension is
explicitly unadmitted.

Version 0.10.0 adds Java `.java` final-delta support and a token signature that
ignores ordinary formatting and non-documentation comments while preserving
Javadoc/Markdown documentation comments, literals, identifiers, operators,
delimiters, and Unicode-escape ambiguity. Java repository evidence routes API,
CLI, data, integration, security,
validation, background, build, concurrency, and test deltas through existing
rules. No fitted Change prior was added. The admitted 0.6.0 Stage A records contain
no Java, so this extension is explicitly unadmitted.

Version 0.11.0 adds Kotlin `.kt`/`.kts` final-delta support and a bounded token
signature that ignores ordinary formatting, optional semicolons/trailing commas,
and non-documentation comments while preserving KDoc, regular/raw strings,
characters, numbers, identifiers, backtick names, operators, delimiters, and
semantic newlines after jump expressions. Kotlin repository evidence routes
server/API, Android/UI, data, integration, security, validation, background,
coroutine/Flow, build, and test deltas through existing category rules. No fitted
Change prior was added. The admitted 0.6.0 Stage A records contain no Kotlin, so
this extension is explicitly unadmitted.

Version 0.12.0 adds Shell and PowerShell final-delta support with conservative
literal-aware signatures. Ordinary formatting and non-directive comments can
normalize to zero while shebangs, PowerShell `#requires`, identifiers, operators,
delimiters, and literal contents remain significant. Shell here-documents and
PowerShell here-strings fail closed. Analyzer-backed product/module, test, build,
CI, delivery, infrastructure, integration, security, and validation roles route
through existing category rules. No fitted Change prior was added. The admitted
0.6.0 Stage A records contain no Shell or PowerShell, so this extension is
explicitly unadmitted.

Version 0.13.0 adds Terraform/HCL final-delta support with a conservative
literal- and heredoc-aware signature. Horizontal layout and blank-line count can
normalize to zero while semantic newlines, comments, identifiers, operators,
literals, templates, delimiters, and heredoc bodies remain significant;
incomplete constructs fail closed. Analyzer-backed infrastructure, integration,
security, validation, test, documentation, build, and delivery facts route through
existing category rules. State, plan, cache, lock, generated, vendor, duplicate,
and formatting-only bodies retain zero implementation value. No fitted Change
prior was added. The admitted 0.6.0 Stage A records contain no Terraform or HCL,
so this extension is explicitly unadmitted.

Version 0.14.0 adds PHP `.php` final-delta support with a conservative token
signature. Ordinary formatting and non-documentation comments can normalize to
zero while PHPDoc, identifiers, variables, operators, delimiters, literals,
heredoc/nowdoc bodies, PHP tags, and inline template content remain significant;
incomplete constructs fail closed. Analyzer-backed API, UI, data, integration,
security, validation, background, build, and test facts route through existing
category rules. Vendor/cache/generated/lock/duplicate and formatting-only bodies
retain zero implementation value. No fitted Change prior was added. The admitted
0.6.0 Stage A records contain no PHP or Composer, so this extension is explicitly
unadmitted.

Version 0.15.0 adds Rust `.rs` final-delta support with a conservative token
signature. Ordinary formatting and non-documentation comments can normalize to
zero while Rustdoc, identifiers and raw identifiers, operators, delimiters,
strings and raw strings, character literals, numbers, lifetimes, attributes, and
compiler directives remain significant; incomplete constructs fail closed.
Analyzer-backed API, data, integration, security, validation, background,
concurrency, FFI, build, benchmark, and test facts route through existing category
rules. Target/vendor/generated/lock/duplicate and formatting-only bodies retain
zero implementation value. No fitted Change prior was added. The admitted 0.6.0
Stage A records contain no Rust or Cargo, so this extension is explicitly
unadmitted.

Version 0.16.0 adds final-delta support for strict Dockerfile variants,
filename-qualified Compose YAML, and `.dockerignore`. Dockerfile signatures can
ignore keyword case, ordinary comments, blank lines, and continuation layout;
Compose signatures can ignore comments, blank lines, indentation width, and
mapping-colon spacing; `.dockerignore` signatures can ignore ordinary comments
and surrounding layout. Directives, arguments, commands, YAML keys/values/
sequences/documents, and ignore patterns remain meaningful. Heredocs, tabs,
malformed flow syntax, and block scalars fail closed. Analyzer-backed Docker facts
route through the existing packaging/deployment rule, arbitrary YAML stays
outside the boundary, and no fitted Change prior was added. The admitted 0.6.0
Stage A records contain no Docker or Compose, so this extension is explicitly
unadmitted.

Version 0.17.0 adds Jupyter `.ipynb` final-delta support through a bounded
maintained-cell signature. JSON layout, source string/array representation,
outputs, execution counts, widgets, attachments, transient metadata, raw or
unsupported-language cells, magics, and shell escapes can normalize to zero.
Python token structure, Markdown, declared language, maintained cell tags, and
meaningful ordering remain significant; unsafe inputs fail closed. Analyzer-backed
Python, documentation, data, visualization, integration, and test facts route
through existing category rules. No fitted Change prior was added. The admitted
0.6.0 Stage A records contain no Jupyter changes, so this extension is explicitly
unadmitted.

Version 0.18.0 adds C and C++ source/header/module final-delta support. It uses a
bounded managed token signature that can ignore ordinary formatting and
non-documentation comments while preserving documentation, preprocessor
directives/replacement tokens, identifiers, operators, delimiters, literals, raw
strings, attributes, declarations, and meaningful ordering. Unsafe or ambiguous
lexical/preprocessor structure fails closed. Analyzer-backed production, API, UI,
data, integration, security, validation, concurrency, FFI, test, build, and
delivery roles route through existing categories. It adds no fitted prior and
does not expand the 0.6.0 Stage A admission boundary.

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
`change-seed/0.18.1+seed-rules/0.4.0`; every frozen Change report retains its
original identity and numbers. `change-model-admission/0.2.0` admitted version
0.6.0 only for experimental 4-to-32-hour Stage A changes after model-authored
logical agreement and performance gates. Versions 0.7.0 through 0.18.1 preserve
those admitted rules but are not separately admitted, and SQL, Python, Go, Java,
Kotlin, Shell, PowerShell, Terraform, HCL, PHP, Composer, Rust, Cargo, Docker,
Compose, Jupyter, C, or C++ have no
reviewed Change labels.
Version 0.18.1 retains the 0.18.0 priors while adding the bounded changed-scope
projection and immutable Git-inventory reuse for large snapshots.
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

The first successor interval policy is frozen as
`symmetric-planning-interval/1.0.0`. It targets at least `0.80` held-out coverage
of reviewed expected points as an operational admission metric. This is agreement
with reviewed logical weak supervision, not an 80% probability statement. The
primary low/high interval must be centered arithmetically on expected EHE; only
the zero-hour floor may truncate its low side. Ordinary discovery, domain
learning, and implementation risk belong in expected EHE. Exceptional one-way
risks remain separately named contingencies or scenarios and do not make the
primary interval asymmetric.

A material fact that cannot be inspected must be an explicit assumption and
strict width-widening driver. Static analysis not executing target code, resolving
an environment, or contacting an external system is diagnostic-only unless that
limitation leaves such a material estimation fact unresolved. Within comparable
cells, lower confidence or greater ambiguity must not produce a narrower interval.
An unavailable feature value is explicit but does not automatically widen the
interval; the model must use an available material-gap or conservative-fallback
feature when widening is required.

`repository-uncertainty-features/1.0.0` freezes the label-independent offline
feature vocabulary before fitting. `eh calibration uncertainty-features` projects
it from a digest-matched saved estimate and repository-evidence document without
reading labels, scanning source, or changing hours. The projection also records
whether each source-model range already satisfies the successor symmetry policy;
`seed-rules/0.4.0` is unchanged by this diagnostic contract.

`uncertainty-feature-evaluation/1.0.0` measures those vectors only against a
development-only corpus. It uses leave-one-repository-out folds, an unconditional
nearest-rank 80th-percentile normalized-residual baseline, and fixed scalar-feature
buckets with explicit sparse fallback. It reports size, raw-residual, and
normalized-residual associations plus coverage and sharpness by feature and
category/ecosystem/size slice. It refuses validation/test records and does not
produce a deployable interval model or change expected EHE.

`uncertainty-support-profiler/1.0.0` separately derives label-independent sample
support and bucketed OOD distance from immutable feature reports. A versioned
development population supplies repository-family identity without reviewed
targets or hours. The complete family is held out, so another revision from the
same repository cannot inflate support or serve as a nearest reference. Support
falls back through fixed category/size/ecosystem/complexity cells, and OOD uses
four structural plus 11 frozen feature dimensions. These values remain population
diagnostics and do not alter expected EHE or interval width by themselves.
`uncertainty-support-evaluator/1.0.0` has now aggregated four frozen support/OOD
signals to the 2,030 development targets and measured them through repository-held-
out residual folds. Every signal worsened coverage and interval miss relative to
the unconditional baseline, and the bucket directions were non-monotonic or
opposite their width hypotheses. The current signals are therefore rejected as
direct width drivers; this is a measured non-selection, not permission to retune
the label-independent contract after observing labels.

How item-level uncertainty is aggregated, including correlated uncertainty,
remains an implementation research item. Reports must not imply that ranges are
formal probability intervals until separately governed empirical calibration
supports that interpretation.

## 12. Pricing

Pricing is a final, replaceable layer:

```text
Equivalent Replacement Cost = Equivalent Human Effort x hourly market rate
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
described in `PRICING.md`. Callers can provide an exact override or request
effort-only output. Future regional profiles must not change the underlying effort
estimate.

## 13. Polyglot source backbone and ecosystem analyzers

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

Go analyzer `0.1.0` reuses the same generic rule without changing its artifact or
rates. Its bounded managed tokenizer supplies files, functions, methods, types,
interfaces, exported symbols, generics, goroutines/async units, and branches;
specialized import-qualified facts continue through existing category rules.
Static `go.mod`/`go.work` ownership and explicit build/cgo/runtime-registration
uncertainty do not invoke the Go toolchain. `GO_ANALYSIS.md` defines the exact
boundary.

Java analyzer `0.1.0` also reuses the unchanged generic rule. Its bounded managed
tokenizer supplies files, methods, types, public symbols, generics, async/
concurrency units, and branches; specialized import- and annotation-qualified
facts continue through existing category rules. Static Maven/Gradle project
ownership and explicit dynamic-build/annotation-processor/runtime uncertainty do
not invoke a JVM or build tool. `JAVA_ANALYSIS.md` defines the exact boundary.

Kotlin analyzer `0.1.0` reuses that same generic rule inside its owning JVM scope.
Its bounded managed tokenizer supplies files, functions, methods, types, public
symbols, extensions, generics, suspend/async units, coroutines, Flow, nullability,
and branches; import-qualified server, Android/UI, persistence, integration,
security, validation, background, build, and test facts continue through existing
category rules. Static Maven/Gradle ownership and explicit compiler-plugin,
generation, Android, multiplatform, and runtime uncertainty do not invoke a JVM,
compiler, plugin, or build tool. `KOTLIN_ANALYSIS.md` defines the exact boundary.

Scripting analyzer `0.1.0` reuses that same generic rule separately for Shell and
PowerShell production/module scopes. Its bounded managed tokenizers supply files,
functions, methods/types, public symbols, parameters, branches, pipelines,
external commands, file/network/process/module operations, and dynamic-
uncertainty tags; path and exact invocation context separate tests, build, CI,
delivery, and infrastructure automation. Integration, security, validation,
testing, build, delivery, and infrastructure facts continue through existing
category rules. No shell, command/module resolver, sourced content, expansion, or
platform effect is executed. `SHELL_POWERSHELL_ANALYSIS.md` defines the exact
boundary.

Terraform/HCL analyzer `0.1.0` replaces raw Terraform file/line pricing with
bounded semantic infrastructure units. Distinct resource/data types, module and
interface boundaries, providers/backends, lifecycle/dependency structure, and
expression structure contribute transparent units; conventional repetitions use
diminishing bands and exact bodies are valued once. Those units feed the existing
`ci-infrastructure` rule, while integration, security, validation, test,
documentation, build, and delivery facts continue through existing specialized
rules. Generic non-Terraform HCL remains visible without guessed Terraform units.
No provider/schema/module/backend/plan/state/policy runtime is loaded or executed.
`TERRAFORM_HCL_ANALYSIS.md` defines the exact boundary.

PHP analyzer `0.1.0` reuses the unchanged generic source rule inside Composer
package scopes. Its bounded managed tokenizer supplies files, functions, methods,
types, public symbols, attributes, branches, exceptions, and explicit dynamic-
boundary signals; strict static Composer metadata supplies package/test ownership,
dependencies, autoload surfaces, scripts, binaries, and literal local edges.
Import-qualified API, UI/template, data, integration, security, validation,
background, build, and test facts continue through existing category rules.
Composer resolution, autoloading, framework compilation/container/route behavior,
reflection, and target execution do not occur. `PHP_COMPOSER_ANALYSIS.md` defines
the exact boundary.

Rust analyzer `0.1.0` reuses the unchanged generic source rule inside Cargo
package/workspace scopes. Its bounded managed tokenizer supplies files, functions,
methods, structs/enums/traits/unions, implementations, public symbols, generics,
lifetimes, async/await, branches, unsafe blocks, error paths, macros, attributes,
and extern/FFI boundaries; static Cargo metadata supplies package/test ownership,
dependencies, features, build scripts, targets, and literal local edges. Import-
qualified API, data, integration, security, validation, background/concurrency,
FFI, build, benchmark, and test facts continue through existing category rules.
Cargo resolution, feature/target selection, macro expansion, build scripts,
generated bindings, borrow checking, compilation, and target execution do not
occur. `RUST_CARGO_ANALYSIS.md` defines the exact boundary.

Docker analyzer `0.1.0` replaces raw Docker artifact file/line pricing with
bounded semantic container units. Dockerfile stages, build/runtime instructions,
mount and health boundaries; filename-qualified Compose services and orchestration
structure; literal local Compose-to-Dockerfile references; and `.dockerignore`
rules contribute transparent units with exact-body normalization and capped
dynamic uncertainty. Those units feed the unchanged `container-deployment` rule
and packaging/deployment category. Arbitrary YAML receives no Docker units, and
Docker/Compose/BuildKit/runtime execution, image inspection, context expansion,
interpolation, includes, secrets, and configured-value disclosure do not occur.
`DOCKER_ANALYSIS.md` defines the exact boundary.

The implemented C/C++ analyzer reuses the unchanged generic source rule. A bounded
managed lexer and conservative declaration parser supply C/C++ files, functions/
methods, types, public symbols, templates, async/concurrency units, and branches.
Static target/build ownership and qualified semantic facts reuse existing setup,
API, UI, data, integration, security,
validation, background/concurrency, FFI, test, build, delivery, manual-validation, and
review rules. Header bodies count once; include fan-out and template-instantiation
potential do not multiply effort. Conditional alternatives normalize identical
declarations once, retain distinct external surfaces, use a maximum envelope for
residual sibling-branch structure, and add only capped build/preprocessor
uncertainty. No C/C++ prior or parser dependency was added. `CPP_ANALYSIS.md`
defines the complete implemented boundary.

Public mutation suite `0.8.0` combines 77 unchanged `seed-rules/0.3.0` candidates
with 11 Python `seed-rules/0.4.0` candidates. All 339 relational assertions pass.
This protects directionality and invariance; it does not calibrate absolute hours.
The standalone Go suite adds 13 `seed-rules/0.4.0` candidates and passes 56/56
relations under the same qualitative-only interpretation.
The separate standalone Java suite likewise adds 13 `seed-rules/0.4.0` candidates
and passes 56/56 relations without altering the frozen aggregate or Go suites.
The standalone Kotlin suite adds 14 `seed-rules/0.4.0` candidates and passes 63/63
relations without altering any earlier aggregate or standalone suite.
The standalone scripting suite adds 13 `seed-rules/0.4.0` candidates and passes
46/46 relations without altering any earlier aggregate or standalone suite.
The standalone Terraform suite adds 14 `seed-rules/0.4.0` candidates and passes
48/48 relations without altering any earlier aggregate or standalone suite.
The standalone PHP suite adds 14 `seed-rules/0.4.0` candidates and passes 59/59
relations without altering any earlier aggregate or standalone suite.
The standalone Rust suite adds 14 `seed-rules/0.4.0` candidates and passes 62/62
relations without altering any earlier aggregate or standalone suite.
The standalone Docker suite adds 13 `seed-rules/0.4.0` candidates and passes
38/38 relations without altering any earlier aggregate or standalone suite.
The standalone C/C++ suite adds 21 `seed-rules/0.4.0` candidates and passes 71/71
relations without altering any earlier aggregate or standalone suite.

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
