# Milestone 5: Granular Seed Estimation Model

## Status

Implementation complete on August 5, 2026. Numerical calibration remains future
work; completion means that the transparent seed-model mechanics and lineage are
implemented and verified, not that the priors are production-ready.

This milestone replaces the project-level placeholder rules with an explicitly
experimental, deterministic estimator. It does not claim calibrated accuracy.
Its purpose is to create a defensible work-item decomposition that can be reviewed,
corrected, and later used for calibration and local ML.

## Objective

Convert versioned repository evidence into a repository-first ledger of small work
items whose hours can all be explained by:

- the evidence that caused the item to exist;
- the normalized quantity being valued;
- the applicable seed prior and complexity modifier;
- the selected estimation profile; and
- the uncertainty and exclusions attached to the item.

Every represented hour must belong to a named work item. There is no unexplained
repository-level multiplier. Work items normally contain 0.5 to 8 expected hours.

## Approved design decisions

1. Use a hybrid model consisting of a general implementation backbone plus
   specialized boundary work.
2. Store seed productivity priors in a checked-in, versioned JSON artifact.
3. Emit a conservative professionalization-gap ledger by default and exclude it
   from represented EHE and replacement cost.
4. Use EffortHours itself as the first realistic reviewed teacher estimate, while
   explicitly avoiding calibration to a single repository.

## Estimation pipeline

```text
repository evidence
        |
        v
evidence index and normalization
        |
        +--> exact-content deduplication
        +--> scope and role resolution
        +--> broad/fine evidence precedence
        +--> provenance and uncertainty drivers
        |
        v
category-specific capability units
        |
        v
versioned seed priors and complexity modifiers
        |
        v
bounded 0.5-to-8-hour represented work items
        |
        +--> profile-specific design items
        +--> separate professionalization-gap items
        |
        v
category totals, EHE total, and independent pricing
```

The capability unit is internal to the estimator. The published v1 work-item and
estimate contracts already contain the required output lineage, so this milestone
does not require a breaking schema change.

## Evidence normalization

### Scope and role

Project and package evidence establishes stable estimation scopes and their roles,
such as application, server, library, worker, CLI, web UI, and test. Source,
semantic, test, configuration, and dependency facts are associated with the most
specific matching scope.

SQL files use the deepest unambiguous containing .NET project or JavaScript package;
otherwise one standalone SQL scope is explicit. Equal-depth ownership conflicts
remain visible and are not guessed.

### Exact duplication

Common-scanner SHA-256 tags identify byte-identical maintained files. Identical
source bodies contribute implementation structure once, even when copied to more
than one path. All relevant paths and fact IDs remain available as traceability.

Separate project setup, deployment, configuration, and integration context may
still represent real work when the same body is used in multiple runnable scopes.
The initial seed model handles exact duplicates only. Near-duplicate and dead-code
detection remain explicit limitations rather than invented precision.

SQL semantic normalization selects one canonical maintained body per exact digest
and role family. Duplicate paths retain artifact/exclusion lineage but do not add
data, test, delivery, integration, review-width, or total effort.

### Broad and fine facts

Broad repository facts are fallbacks and context. They are not billed again when a
more precise ecosystem fact represents the same artifact. Examples include:

- repository test-suite evidence yielding to file- or project-level test facts;
- component-manifest evidence yielding to parsed project/package facts;
- aggregate build evidence supporting, rather than duplicating, project and
  configuration work; and
- coverage evidence modifying represented test work instead of becoming another
  copy of the tests.

Coverage provenance participates in precedence as well as normalization. A
digest-verified parsed LCOV or Cobertura report is `measured`; a configured
threshold remains `declared-assumed`. When both apply to one scope, the estimator
uses measured percentages only and retains the declaration as non-valued evidence
instead of averaging or double-counting them. This uses the existing
`coverage-achievement` rule and does not change its numerical prior.

Known inventory, language, file, exclusion, package-reference, and graph facts may
be supporting or deliberately non-valued evidence. Only unknown evidence kinds are
reported as unsupported by the estimator.

### Generated and external content

Generated, vendored, minified, bundled, binary, and build-output bodies contribute
no hand-written implementation hours. Maintained generator configuration,
templates, integration, validation, or customization can still produce explicit
work items when evidence supports them.

## Hybrid valuation model

### General implementation backbone

Source-structure facts represent internal logic that routine semantic classifiers
cannot name. The backbone uses types, functions, methods, public surface, branches,
async behavior, and similar parser/token measurements as cognitive construction
signals. It does not use physical lines as the principal effort model.

Backbone priors are intentionally lower than full feature-delivery rates because
specialized work is valued separately. Test-project structure is routed to test
work or excluded from the production backbone. Mixed JavaScript package structure
is adjusted using maintained production/test file evidence. Exact duplicate content
is normalized before the structural quantity is valued.

### Specialized boundary work

Semantic builders create explicit work for behavior whose difficulty is poorly
represented by syntax volume alone. Initial builders cover:

- repository, solution, workspace, project, and package setup;
- specification comprehension and bounded domain learning;
- architecture and technical design;
- API and command surfaces;
- UI pages, components, state, forms, markup, and styles;
- data models, persistence, queries, and migrations;
- external services, queues, and protocols;
- authentication, authorization, security, validation, and accessibility signals;
- background jobs, workers, functions, and message handlers;
- unit, component, integration, contract, end-to-end, and UI tests;
- documentation;
- build configuration and developer tooling;
- CI/CD, containers, infrastructure, packaging, and release artifacts;
- manual validation, debugging, and hardening; and
- self-review and integration of the completed system.

Specialized priors value the additional design, configuration, adaptation, edge
behavior, and validation represented by a boundary. They do not charge a second
full implementation price for methods already represented by the backbone.
Repeated calls to the same external technology increase bounded adaptation work;
they do not create a new integration selection effort for every call.

## Seed-rule artifact

The current catalog is `models/seed-rules/0.4.0.json`. It is checked in for public
review and embedded into `EffortHours.Estimation` so normal execution does not depend
on the current directory, external files, or network access. The 0.2.0 and 0.2.1
artifacts remain checked in because frozen reviewed corpora and baseline reports
record those source estimators.

Version 0.4.0 became effective on August 11, 2026. It retains all 30 version-0.3.0
rules unchanged and adds one transparent language-neutral source backbone, first
used by the token-backed Python analyzer and now reused unchanged by Go, Java,
Kotlin, and scripting analyzers `0.1.0`. Its analogous construction rates use
wider uncertainty and are not fitted calibration. The earlier 77 synthetic
candidates remain frozen at 0.3.0; the 11 Python, 13 standalone Go, 13 standalone
Java, 14 standalone Kotlin, and 13 standalone scripting states use 0.4.0.
Its artifact digest remains
`sha256:7cc0cd517ccf096470b98ef72993312263a4e60a2396967f5a07a1104a8c3a01`.

Version 0.3.0 became effective on August 10, 2026. It retains every non-UI
numerical prior from 0.2.1. Within `ui-surface`, it removes physical asset lines as
an effort driver and adds bounded semantic units for template structure and
bindings, stylesheet structure, responsive behavior, design tokens, and
animation/theme behavior. The new values are transparent preliminary priors, not
fitted calibration. The public mutation suite was expanded before acceptance, and
all 51 prior cases retain identical numeric ranges. Its artifact digest is
`sha256:e8bce2f76c97564919ab6be41f1cfd6b222d531a4dbd08a8b22c7abe6b1eebdf`.

Version 0.2.1 became effective on August 6, 2026. It copies all 0.2.0 numerical
priors unchanged and corrects TypeScript ownership in the shared JavaScript family
for duplicate and test normalization. Its frozen artifact digest is
`sha256:57378795593acd2ff0a2f4361698193a11dca86da11493f072da6a9f9b344d4e`.

The artifact records:

- schema, model, and semantic versions;
- experimental status and 2026 technology baseline;
- minimum and maximum expected item size;
- a four-hour target expected item size;
- rounding increment;
- complexity factors;
- provenance confidence defaults;
- category-specific setup costs;
- transparent marginal-hour tiers for observable drivers; and
- initial low/high range factors and confidence values.

Rule evaluation is deterministic. Inputs, selected rule ID/version, normalized
quantity, complexity, and reasoning are preserved in the resulting work item.

The catalog is a product model, not a user rate card. Changing a contractor rate
never changes its effort output. Future calibration can replace catalog values or
add local inference without changing evidence semantics.

## Profiles

Both profiles share implementation, validation, tests, documentation, and other
represented artifact work.

The `implementation` profile includes bounded specification comprehension and the
technical design needed to implement supplied requirements and design inputs.

The `recreation` profile adds named work items for recovering or making architecture,
data, API, interface, and UX decisions embodied in the artifact. It does not use an
opaque percentage uplift and does not add stakeholder discovery or historical
rework.

## Work-item construction

A capability may be larger than eight hours. The deterministic slicer partitions
it into stable parts whose expected effort is between 0.5 and 8 hours. Each part
retains the same rule, scope, evidence, reasoning, assumptions, exclusions,
correlation group, and profile applicability. Quantity and effort are apportioned
without changing the capability total.

Hours use the catalog's rounding increment. Stable item IDs derive from the rule,
scope, capability identity, and part number rather than enumeration order.

## Complexity and uncertainty

Complexity is an item-level modifier derived from explicit tags and bounded
structural signals. The catalog records the factor for routine, moderate, high, and
exceptional work. There is no repository-wide complexity multiplier.

Confidence is an explanation score, not a calibrated probability. Initial drivers
include:

- observed or measured versus inferred evidence;
- parser-backed versus token-backed or fallback syntax analysis;
- unresolved project/configuration references;
- exact-deduplication approximations for aggregate structure;
- missing specification input;
- analyzer diagnostics and truncated locations; and
- unsupported ecosystems or semantic ambiguity.

Uncertainty widens item ranges through documented rule behavior and produces
specific uncertainty reasons. Correlated items share a named correlation group.
Summed low/high values are preliminary planning bounds and must not be described as
formal probability intervals before calibration.

## Manual validation and self-review

Manual validation is generated from the actual runnable and boundary surfaces, not
as a fixed percentage of implementation. API, UI, data, integration, security, and
background capabilities contribute bounded validation sessions.

Self-review and system integration are explicit items derived from runnable scopes,
project/package boundaries, and reference edges. They remain visible instead of
being hidden in a general overhead factor.

## Professionalization gap

Gap work is generated conservatively from strong absence evidence. Initial cases
include:

- a maintained production scope with no detected automated tests;
- a runnable API or UI scope without representative integration/end-to-end tests;
- no maintained onboarding documentation; and
- no detected CI configuration for a multi-project or runnable repository.

Absence does not imply that every best practice is required. The seed model will
not invent security, accessibility, operations, or deployment gaps without enough
context. Gap items are serialized separately and never included in represented EHE,
category totals, or replacement cost.

Terraform/HCL analyzer `0.1.0` replaces the common scanner's coarse Terraform
file/line contribution with bounded semantic infrastructure units while preserving
the unchanged `seed-rules/0.4.0` artifact. Distinct types and boundaries receive
more weight than conventional repetition; exact bodies are valued once. Existing
integration, security, validation, test, documentation, build, and delivery rules
consume separate facts. Generic HCL, lock/state/plan/cache/generated/vendor bodies,
and raw Terraform line volume do not receive guessed Terraform effort. This is
transparent prior reuse, not fitted calibration.

PHP analyzer `0.1.0` replaces coarse PHP file inventory with bounded source,
Composer package, and template semantics while preserving the unchanged
`seed-rules/0.4.0` artifact. The language-neutral source backbone consumes files,
declarations, public symbols, branches, and exception structure; existing API, UI,
data, integration, security, validation, background, build, test, and delivery
rules consume separate qualified facts. Vendor/cache/generated/lock/duplicate
bodies and raw PHP line volume do not receive guessed PHP effort. This is
transparent prior reuse, not fitted calibration.

Rust analyzer `0.1.0` replaces coarse Rust file inventory with bounded source and
Cargo package/workspace semantics while preserving the unchanged
`seed-rules/0.4.0` artifact. The language-neutral source backbone consumes files,
declarations, public symbols, branches, async/error/unsafe structure, and FFI;
existing API, data, integration, security, validation, background/concurrency,
build, benchmark, test, and delivery rules consume separate qualified facts.
Target/vendor/generated/lock/duplicate bodies and raw Rust line volume do not
receive guessed Rust effort. This is transparent prior reuse, not fitted
calibration.

Docker analyzer `0.1.0` replaces coarse container file/line volume for admitted
Docker artifacts with bounded semantic container units while preserving the
unchanged `seed-rules/0.4.0` artifact. The existing container-deployment rule
consumes Dockerfile stage/build/runtime structure, filename-qualified Compose
service/orchestration structure, literal local build references, and
`.dockerignore` rules. Exact duplicate bodies are valued once; generic YAML,
configured values, image contents, build-context volume, and raw Docker line
volume receive no guessed Docker effort. This is transparent prior reuse, not
fitted calibration.

Python analyzer `0.2.0` adds a bounded Jupyter maintained-cell projection while
preserving unchanged `seed-rules/0.4.0`. Python code-cell structure enters the
language-neutral source backbone only after output/transient state, unsupported
syntax, exact duplicate cells, and duplicate notebook projections are excluded.
Markdown, data analysis, visualization, integrations, and tests remain separate
facts consumed by existing rules. Physical notebook JSON, outputs, execution
counts, widget/attachment payloads, checkpoints, and generated bodies receive no
guessed effort. This is transparent prior reuse, not fitted calibration or a
scientific-validity claim.

The C/C++ analyzer `0.1.0` reuses the same unchanged `seed-rules/0.4.0`
language-neutral backbone. Maintained C/C++ declarations and structure are
normalized by exact body, source/header
ownership, and preprocessor alternative groups before valuation. Header include
fan-out, macro-expansion potential, and template-instantiation potential are not
quantities. Qualified API, UI, data, integration, security, validation,
concurrency, FFI, test, build, and delivery evidence uses existing rules;
preprocessor/build variability is capped uncertainty rather than a source-volume
multiplier. No C/C++-specific prior was added; `CPP_ANALYSIS.md` defines the
implemented token-backed boundary.

## Testing requirements

Ordinary unit tests remain entirely memory-backed. Milestone 5 tests must cover:

- deterministic output and stable IDs;
- schema and semantic validity;
- complete evidence lineage for represented items;
- the 0.5-to-8 expected-hour invariant;
- exact duplicate content not increasing implementation effort;
- generated, vendored, minified, and binary bodies not increasing effort;
- broad/fine evidence precedence;
- .NET, JavaScript/TypeScript, SQL, and mixed evidence;
- tests, declared-assumed coverage, measured coverage, and measured-over-declared
  precedence at the represented level;
- explicit recreation-profile additions;
- manual validation and self-review as named items;
- professionalization gaps remaining outside totals and pricing;
- rate changes affecting cost but never effort; and
- unknown evidence producing diagnostics without invented effort.

Disk-backed CLI tests remain in `EffortHours.EndToEndTests`. No physical fixture tree,
temporary file, model file, or cache is read or written by `EffortHours.Tests`.

## First review anchors

The initial priors will be reviewed against:

1. small synthetic, memory-backed archetypes with obvious bounded work;
2. mutation variants proving that meaningful behavior changes the correct category
   while formatting and exact duplication do not; and
3. a repository-level logical estimate of EffortHours itself.

EffortHours is only an initial realism check. Seed values must not be tuned to make one
self-estimate look desirable, and the model remains uncalibrated until a diverse,
licensed, repository-separated corpus exists.

## Exit criteria

Milestone 5 is complete when:

- all represented hours trace to evidence-backed work items;
- no repository-level total multiplier exists;
- both profiles produce explicit, reviewable differences;
- expected work-item size is normally 0.5 to 8 hours;
- manual validation, self-review, and gap work are visible and separate;
- exact duplication and excluded bodies do not create implementation value;
- JSON and Markdown reports remain deterministic and schema-valid;
- unit tests remain filesystem-free; and
- the CLI and package continue to work offline without executing target code.
