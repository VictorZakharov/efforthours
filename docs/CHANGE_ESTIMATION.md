# Change and Contribution Estimation

## Status

The first Change Estimation MVP and its first non-Git selector follow-on are
implemented after Milestone 7B5. They include provider-neutral immutable
base/head analysis, two statically scanned directories, two saved repository
evidence bundles, one commit, one final revision range, and one GitHub pull
request through an optional `gh` adapter. The current
`change-seed/0.17.0` rules remain experimental and are not empirically calibrated,
production-ready, or separately model-admitted. They preserve the valuation
behavior of the Stage A logically admitted 0.6.0 baseline; the SQL, Python, Go,
Java, Kotlin, Shell, PowerShell, Terraform/HCL, PHP/Composer, Rust/Cargo,
Docker/Compose, and Jupyter paths were not present in that gate. Version 0.17.0
retains the 0.3.0 logical-marginality
correction and the 0.4.0 fail-closed boundary for explicitly delimited
customization inside otherwise generated files. It adds an expected-point gross-
to-final normalization diagnostic
for explicit multi-commit ranges, disjoint mixed-role category partitions,
roughly-one-hour logical work-item decomposition, unique-snapshot analysis reuse,
bounded component audits, SQL-aware formatting/category routing, Python
indentation-aware formatting/category routing, and Go-aware formatting/directive/
implicit-semicolon/category routing, Java token-aware formatting/category routing,
Kotlin token-aware formatting/category routing, Shell/PowerShell literal-aware
formatting/semantic role routing, and HCL-aware formatting/Terraform semantic
routing, PHP-aware formatting and analyzer-backed semantic/category routing, plus
Rust-aware formatting and analyzer-backed semantic/category routing, plus
Dockerfile, Compose, and `.dockerignore` formatting normalization with analyzer-
backed packaging/deployment routing, plus bounded Jupyter container/output
normalization and analyzer-backed semantic routing, without
changing any existing Change EHE prior or previously supported final-delta total.
The current source composes repository `seed-rules/0.4.0`.
The first calibration-infrastructure checkpoint, a preliminary 24-record synthetic
host-AI teacher corpus, a one-record real public pilot, a blind six-family
real-source expansion, and a released-alpha.3 public validation follow-on are
implemented. No independent correction exists, but disclosed, decomposed host-AI
teacher labels are now sufficient for Stage A logical admission. The visible expansion
diagnostics exposed repeated category-slice overcounting. A subject-neutral 0.3.0
correctness revision and separate development/validation diagnostics now exist;
the alpha.3 follow-on exercises that revision on a new family, the expansion test
comparison remains withheld, and no repository prior, threshold, or review
maturity changed.
The behavioral safeguard suite now covers cancellation and category-isolated
migration, integration, CI, container-delivery, and simplification mutations in
addition to the initial normalization and Git boundaries. The first Change
portfolio checkpoint adds repeated PRs, a versioned cross-repository PR manifest,
and bounded author-and-period selection. Its separate
`change-portfolio/0.1.0+change-seed/0.17.0+seed-rules/0.4.0` reconciler changes no
Change prior, frozen report, label, or admission decision and remains experimental.

## Purpose

Repository-wide recreation is only one billing context. EffortHours should eventually
estimate the Equivalent Human Effort embodied in a completed incremental change,
while retaining the same current-artifact, no-churn valuation principles.

The output is **Change EHE**: the conventional senior-contractor effort represented
by the final functional and quality delta. It is not elapsed time, historical labor,
or a reconstruction of what a contributor actually did.

## Selector roadmap

The implemented provider-neutral engine and Git adapter support:

- two local directory snapshots, each statically scanned and pinned to its
  content-derived repository source digest;
- two v1 serialized repository-evidence bundles with digest-checked file
  inventories;
- immutable base and head snapshots selected by local Git revisions;
- one commit compared with a selected parent;
- a revision range compared by its final base and head;
- one pull request, with GitHub available through an optional `gh` CLI adapter;
- repeated pull requests from one local repository;
- a v1 manifest selecting pull requests from multiple local repositories; and
- commits selected by exact author/co-author alias and an explicit time interval.

The engine accepts storage-independent snapshot factories, which keeps selector
adapters separate from valuation. Portfolio selection composes canonical immutable
Change reports and applies pricing only after repository-level normalization.

The initial CLI shape is:

```text
eh change <repository> --base <revision> --head <revision>
eh change <repository> --commit <revision> [--parent <revision>]
eh change <repository> --range <base>..<head>
eh change --base-path <directory> --head-path <directory>
eh change --base-evidence <repository-evidence.json>
  --head-evidence <repository-evidence.json>
eh change <repository> --pr <number-or-url> [--repo <owner/name>]
eh change portfolio <repository> --pr <pr> --pr <pr>
eh change portfolio --manifest <portfolio.json>
eh change portfolio <repository> --author <identity> --since <instant> --until <instant>
  [--date-field <author|committer>] [--timezone <iana-or-host-zone>]
  [--merge-policy <exclude|first-parent>] [--coauthors <include|exclude>]
```

The implemented forms share the repository-estimate profile, format, rate,
compact, and explicit-output options. Directory pairs and evidence pairs are
deliberately separate selector families; incomplete or mixed pairs fail before
analysis. Portfolio commands likewise require exactly one repeated-PR, manifest,
or author-period family. Local snapshot and Git-ref inputs do not depend on GitHub.
Pull-request resolution
uses `gh pr view` only when the caller explicitly selects `--pr`; `gh` must be
installed and authenticated. The adapter retains only the requested PR number or
URL and its immutable base/head object identities. It does not retain the PR body,
discussion, author, reviews, timestamps, or private diff.

Repeated PR selection accepts at most 128 rows. A manifest gives each row a stable
caller ID, repository ID, execution-only local repository path, PR selector, and
optional GitHub repository. Relative paths resolve from the manifest directory and
never enter report output. Caller repository IDs and resolved local Git roots must
map one-to-one, so labels cannot combine separate repositories or split one
repository around normalization. Each repository is normalized independently;
totals are then added without cross-repository deduplication.

Author-period selection reads at most 10,000 commits reachable from a pinned head
and emits at most 128 selected rows. Exact case-insensitive aliases match author
name, email, or `Name <email>`. The interval is start-inclusive and end-exclusive;
offset-free timestamps use the declared timezone, and skipped or ambiguous local
times fail unless the caller supplies an offset. Author versus committer time,
co-author trailer inclusion, and merge exclusion versus first-parent valuation are
explicit report policies. Git returns only valid `Co-authored-by` trailer values;
commit bodies are not returned to EffortHours or retained.

The first PR adapter analyzes objects already available in the selected local Git
object database. It does not fetch, check out, or modify the repository. When a PR
head or base object is absent locally, the command fails with an explicit
instruction to fetch that object before retrying. Automatic external object
materialization can be considered later, but it must not silently mutate the
target repository.

Directory selection runs the ordinary non-executing repository pipeline against
each caller-selected root with no implicit cache and writes nothing into either
tree. The admitted file inventory, per-file SHA-256 and byte length, and repository
source digest become the snapshot boundary. Content reads needed for modified-file
normalization are checked against the pinned hash; a changed selected body fails
visibly instead of being silently mixed into the report. Output records logical
directory selectors and content identities, not absolute host paths.

Evidence selection requires two schema-valid and semantically valid v1 repository
evidence documents. Each file fact must provide its canonical relative path, one
SHA-256 tag, one integral byte measurement, and a repository source digest that
matches the ordered inventory. Evidence bundles contain no source bodies. Exact
addition, removal, movement, duplication, classification, and repository-fact
deltas remain available, but a modified maintained body cannot be proven
formatting-only or partitioned into detailed edit regions. A path that otherwise
qualifies as represented remains represented with one conservative edit region and
a warning; it is never silently excluded.

## History boundary

Ordinary `scan` and repository `estimate` commands continue to ignore Git history.
Change mode is an explicit opt-in exception whose history access is limited to:

- resolving requested base, head, parent, PR, author, and time selectors;
- constructing final snapshots and normalized patches; and
- retaining stable provenance for the selected change.

Commit count, elapsed duration, review duration, author count, message length,
timestamp spacing, branch shape, and intermediate churn are never effort
multipliers.

## Valuation semantics

For a PR or revision range, the preferred estimate compares the final base and head
states. Rewriting the same feature ten times inside the range does not increase
Change EHE. The change analyzer should value additions, modifications, deletions,
tests, documentation, migrations, configuration, integration effects, and required
cross-cutting validation as a coherent net result.

A single non-merge commit is compared with its first parent by default. A root
commit uses Git's empty tree as its base and reports that choice. A merge commit
requires `--parent`; the selected object must be one of the commit's parents.
Commit messages, authors, timestamps, and branch names are neither read into the
change contract nor used as effort evidence.

The explicit author-period portfolio is a selector-layer exception: it reads
bounded author/committer identity, timestamps, parents, and valid co-author
trailers to choose immutable commits. The canonical Change estimate for each row
still receives only base/head content and never receives identity, timestamp,
message, activity, or commit-count multipliers.

Formatting, generated output, vendored content, exact duplication, lockfile noise,
and mechanical movement must not create implementation value. Deletion is not
negative hours: deliberate removal or simplification can represent bounded design,
implementation, migration, and validation work, but deleted volume alone is not an
effort signal.

The MVP compares immutable base and head trees directly. It derives added,
modified, removed, exact-move, excluded, and unchanged-context evidence without
checking out either tree. Formatting-only classification uses conservative,
literal-aware whitespace normalization for the initial .NET and JavaScript/
TypeScript source extensions plus bounded language-aware normalizers for SQL,
Python, Go, Java, Kotlin, Shell, PowerShell, Terraform/HCL, PHP, Rust, Docker, and
Jupyter artifacts. Shell and
PowerShell ordinary formatting and non-directive comments can normalize to zero while shebangs,
PowerShell `#requires`, literals, identifiers, operators, and delimiters remain
significant. Here-documents and here-strings fail closed. Unsupported or uncertain
rewrites remain represented and visible rather than being silently discarded.
Terraform/HCL comparison ignores horizontal layout and blank-line count while
preserving semantic newlines, comments, identifiers, operators, literals,
templates, delimiters, and heredoc bodies. Incomplete constructs fail closed.
PHP comparison ignores ordinary formatting and non-documentation comments while
preserving PHPDoc, identifiers, variables, operators, delimiters, literals,
heredoc/nowdoc bodies, PHP tags, and inline template content. Incomplete constructs
fail closed.
Rust comparison ignores ordinary formatting and non-documentation comments while
preserving Rustdoc, identifiers and raw identifiers, operators, delimiters,
literals and raw strings, lifetimes, attributes, and compiler directives.
Incomplete constructs fail closed.
Dockerfile comparison ignores instruction keyword case, ordinary comments, blank
lines, and continuation layout while preserving directives, arguments, stages,
and commands; heredocs fail closed. Filename-qualified Compose comparison ignores
comments, blank lines, indentation width, and mapping-colon spacing while
preserving keys, values, sequence structure, and document markers; tabs,
malformed flow syntax, and block scalars fail closed. `.dockerignore` comparison
ignores comments and surrounding layout while preserving ordered patterns and
negations.
Jupyter comparison projects bounded maintained cells. JSON layout, source
string/array representation, outputs, execution state, widgets, attachments,
transient metadata, raw/non-Python cells, magics, and shell escapes can normalize
to zero. Python tokens, Markdown, declared language, maintained cell tags, and
meaningful ordering remain significant; invalid or oversized content fails closed.
Exact blob moves are excluded from body implementation effort. Path-sensitive
integration work is included only when separate analyzer evidence supports it.

SQL schema, migration, stored-program, and query deltas use the data category;
test fixtures use integration/component testing; explicit deployment/install
scripts use packaging; and supported cross-database syntax uses integrations.
Generated dumps, exact copies, formatting-only SQL, and repeated seed rows do not
inflate body effort. This is bounded token/statement evidence, not database
execution, grammar validation, query planning, or a semantic schema diff.

Generated artifacts remain excluded by default. Change analysis may represent
only content inside exact, balanced, non-nested `<custom-code>` regions when source
bodies are available and no stronger exclusion applies. These are explicit
EffortHours opt-in markers; unrelated generator-specific protected-region syntax
is not inferred. The supported case-insensitive, whole-line marker pairs are:

- `// <custom-code>` and `// </custom-code>`;
- `/* <custom-code> */` and `/* </custom-code> */`;
- `# <custom-code>` and `# </custom-code>`; and
- `<!-- <custom-code> -->` and `<!-- </custom-code> -->`.

The marker style must match within each region. Inspection is limited to valid
UTF-8 text, eight mebibytes per blob, and 128 regions per artifact. Conventional
generated bytes remain excluded, and vendored, minified, binary, lockfile,
build-output, and exact-copy exclusions take precedence over marker detection.
Added, modified, or removed custom projections can contribute bounded
edit-region work; generated bytes outside the projection never do. An unchanged or
formatting-only projection remains zero. Unpaired, nested, mismatched, or otherwise
ambiguous markers fail closed with traceable path evidence and a review diagnostic.
Bodyless saved-evidence snapshots cannot prove this distinction and therefore keep
the complete generated body excluded.

The v1 schema does not need a new path-classification enum. A supported custom
projection is `represented`, retains the scanner's `classification:generated`
source tag, and adds
`normalization:generated-customization-represented`. Unchanged, formatting-only,
and ambiguous marker outcomes have parallel normalization tags. This makes the
generated-body exclusion and the represented customization auditable separately.

`change-seed/0.3.0` treats repository capabilities as context rather than charging
their full modification priors whenever one cited path changes. An existing
capability receives modification work only when its normalized non-file evidence
changes. Repository-level specification, validation, and review capabilities are
replaced by one bounded change-level item each; setup and architecture apply only
when a scope is added or removed. Repeated repository parts and changed paths
within one logical capability/category share a single diminishing marginal budget,
and modified-artifact rates remain 30% of the corresponding new-artifact fallback
rates. A specialized UI or other boundary
category therefore requires changed boundary evidence; a source file's location
inside such a scope is not sufficient.

Version 0.3.0 no longer uses the summed positive repository-capability difference
when an existing capability or modified artifact grows. Repository work-item
parts for one capability are context, not separate Change work. The changed paths
feed one logical budget: each path contributes one to four units through fixed,
capped edit-region bands, and the existing diminishing tiers cap one budget at
eight expected hours. The same logical units bound unmapped maintained-artifact
fallbacks. A newly detected capability on a modified artifact receives that
meaningful modification budget; a genuinely distinct capability added on a new
artifact retains its positive repository marginal. This is a structural
double-counting correction, not a fitted scale factor.

## Additivity and reconciliation

For clean, disjoint commits with no rework, isolated expected Change EHE should be
approximately additive. The documented initial tolerance is the greater of 10% or
one hour. This is a model guardrail, not a claim that commits record historical
labor.

A range report contains:

- the sum of each selected commit estimated against its own selected parent;
- the normalized EHE of the final base-to-head artifact delta;
- signed, named reconciliation adjustments for shared setup, overlap, reverts, and
  residual interactions when applicable; and
- one expected-hour allocation per selected commit whose exact sum equals the
  normalized expected total.

An explicit range with at least two enumerated commit components also contains a
gross-to-final normalization summary. Let `G` be gross isolated expected EHE and
`N` be authoritative normalized final-delta expected EHE:

- normalization hours are `max(0, G - N)`;
- gross-to-final normalization share is `max(0, G - N) / G`;
- rework-like hours are the lesser of normalization hours and the magnitudes of
  negative expected overlap plus revert adjustments;
- rework-like share uses the same `G` denominator; and
- other normalization is normalization hours minus bounded rework-like hours. It
  retains shared/repeated capability work and residual interaction rather than
  relabeling them as rework; and
- positive interaction hours sum positive residual-interaction adjustments, so a
  mixed-sign reconciliation remains visible rather than being netted out of its
  adjustment lineage.

Structured shares are fractions from zero through one, rounded to four decimal
places with midpoint rounding away from zero. When `G` is exactly zero, all three
shares are omitted and status is `not-applicable-zero-gross`; hours remain exact.
When positive `G` is below `N`, normalization hours and all three shares are zero,
and the positive interaction hours remain explicit. Only expected-point shares are
reported. Low and high remain dependent planning-hour bounds because converting
them into separate percentages would imply unsupported independence or interval
semantics.

The rework-like numerator is a bounded structural attribution, not a reconstruction
of historical rework. It includes only explicit overlap and revert adjustment
lineage. Shared setup, specification, review, validation, and unexplained residual
interaction stay outside it. The percentages diagnose reconciliation and never
multiply, reduce, or replace authoritative final-delta EHE.
Commit count, churn, timestamps, authors, logged-time records, and individual or
team identity do not enter either numerator or denominator.

The summary is not emitted for base/head, single-commit, directory, saved-evidence,
or current PR selections. PR mode currently pins only base/head identities and must
not invent intermediate work. A future PR form would need explicit opt-in immutable
commit enumeration before this diagnostic could be available.

A portfolio report uses a separate reconciliation boundary. Every row first
receives one canonical isolated Change estimate with no rate. Rows are then grouped
by repository and immutable base context. Disjoint connected components remain
additive. Within one repository, repeated PRs with the exact represented patch
identity are counted once, including cherry-picked equivalents; identical patches
in different repositories are never deduplicated. Non-shared category effort is
mapped through each source work item's cited path evidence, so overlap takes a
deterministic order-independent maximum only for the same path and category while
independent code, test, or documentation paths remain additive. Caller selector
order is not an application order. Opposing PR effects remain once with explicit
ordering uncertainty rather than being replayed arbitrarily.

Author-period rows use their selected timestamps as the explicit chronological
policy. An exact selected object chain that returns a path to its starting state is
excluded from normalized final effects. A later reintroduction after a revert is
preserved. Interleaved rows that do not form one exact object chain remain
represented with visible attribution uncertainty. Shared specification, setup,
design, validation, and review context is represented once only inside an
overlapping connected component; disjoint work does not lose those categories.
Each repository exposes signed adjustments and exact expected-hour allocations,
and cross-repository totals add only after independent normalization.

The regression matrix includes sequential same-path mechanical splitting,
implementation/tests/documentation delivered in separate rows, and test or
documentation paths shared by otherwise independent PRs. Distinct-path work with
no structural overlap remains additive: the reconciler does not infer a shared
feature merely from path names, timestamps, or contributor identity.

The normalized final delta is authoritative. Intermediate commit activity never
multiplies it. Component estimates exist to audit composition and detect model
non-additivity. Repeated specification, setup, or review work is reconciled as
shared work. Overlapping edits, duplicate patches, and reverts are normalized
against the final artifact. A residual interaction adjustment is explicit rather
than hidden when the final coherent result is more or less than independently
estimated components.

Low and high values are reconciled independently and remain preliminary planning
bounds. Summing them does not assert independence or formal probability coverage.
Per-change allocations are attribution of repository-represented Change EHE, not
timesheet entries, productivity scores, or proof of sole authorship.

## Versioned MVP contracts

The language-neutral boundary separates:

1. a selector with pinned base and head identities;
2. observed path status and immutable object identity;
3. inferred normalization classification and exclusions;
4. evidence-backed work items and preliminary uncertainty;
5. signed reconciliation adjustments and exact expected allocations; and
6. optional pricing applied only after normalized EHE.

Stable IDs derive from immutable selection, path, capability, and rule inputs.
Reports emit paths and structural facts but no source excerpts. Unchanged context
is represented by counts and snapshot evidence rather than by repeating every
unchanged path.

The v1 public schemas are `change-evidence`, `change-estimate-report`,
`change-estimate-explanation`, `change-portfolio-manifest`, and
`change-portfolio-report`. The Change report schema adds an optional normalization
summary, so frozen v1 reports remain valid; explanation queries accept its stable
calculation ID and return exact adjustment lineage. Portfolio contracts separately
record selection policy, source estimator identity, immutable base contexts,
patch/evidence digests, isolated rows, repository-normalized categories, signed
adjustments, exact allocations, attribution uncertainty, verification, and
post-EHE pricing. They emit neither local repository paths nor source excerpts.

The current source Change estimator identity is
`change-seed/0.17.0+seed-rules/0.4.0`; the portfolio reconciler identity is
`change-portfolio/0.1.0+change-seed/0.17.0+seed-rules/0.4.0`. The earlier 0.6.0
Change identity alone passed the experimental Stage A logical gate, and that
record contains no SQL, Python, Go, Java, Kotlin, Shell, PowerShell, Terraform,
HCL, PHP, Composer, Rust, Cargo, Docker, Compose, or Jupyter. Portfolio aggregation
does not broaden that admission. Neither 0.17.0 nor portfolio 0.1.0 may be
described as empirically calibrated,
generally admitted, or production-ready. Frozen calibration source reports retain
the exact earlier estimator identity they were created from.

## Implemented CLI behavior

- Directory and evidence pairs work without Git or GitHub and use the existing
  base-head selection with explicit `directory` or `evidence` snapshot kinds.
- Directory inputs are bounded to scanner-admitted files and use content-derived
  source digests; saved evidence inventories are independently digest checked.
- Bodyless evidence modifications that otherwise qualify as represented retain one
  edit region and an explicit warning because formatting-only normalization is
  unavailable.
- Moving selectors are resolved to immutable object IDs before analysis.
- Git trees are streamed through `ls-tree` and bounded `cat-file --batch`; EffortHours
  does not create temporary checkouts or source trees.
- Root commits use Git's empty tree. Merge commits require an explicit parent.
- Ranges expose isolated commit estimates, normalized final effort, named signed
  adjustments, allocations that sum exactly to normalized expected hours, and the
  expected-point normalization diagnostic for explicit multi-commit ranges.
- Adjacent range components reuse repository analysis by immutable snapshot ID;
  `N` commits require `N + 1` repository estimates instead of `2N`.
- The optional per-commit reconciliation audit is capped at 256 components by
  default. Larger ranges emit `FB5105` and retain the complete final base-to-head
  estimate while omitting the oversized component ledger.
- Component attribution uses nonnegative largest-remainder cents and sums exactly
  even for large component sets.
- PR mode invokes `gh` only to resolve number/URL and base/head object IDs, then
  requires both objects to exist locally.
- Portfolio mode accepts at most 128 repeated PRs or 128 schema-valid manifest
  rows; every source item is estimated without a rate before reconciliation.
- Manifest repository paths are execution-only. Reports retain caller IDs,
  immutable PR identities, and stable digests without host paths.
- Author-period mode scans at most 10,000 commits reachable from a pinned head,
  selects at most 128 exact alias matches, and records the inclusive/exclusive
  interval, timezone, date field, co-author policy, and merge policy.
- Portfolio JSON and Markdown show isolated and repository-normalized totals,
  base contexts, every selected row, exact expected allocations, signed
  adjustments, and unresolved attribution without source excerpts.
- JSON and Markdown output include optional pricing only after hours are estimated;
  saved JSON supports work-item and normalization-lineage explanation queries.
- Existing-capability modifications require changed normalized capability evidence,
  and repeated path evidence within each logical capability/category receives one
  marginal budget.
- Positive repository-capability partitions for one existing or modified logical
  capability collapse into one bounded evidence-derived Change budget; distinct
  newly added capabilities remain additive.
- Source-readable `.sql` paths use SQL-aware formatting normalization and
  scanner-derived role tags; bodyless evidence modifications retain the ordinary
  conservative fallback.
- Source-readable `.py` and `.pyi` paths use an indentation-aware token signature:
  ordinary comments and formatting can normalize to zero, while indentation
  depth, literals, identifiers, operators, and docstrings remain meaningful.
- Source-readable `.go` paths use a Go-aware token signature: ordinary comments
  and formatting can normalize to zero, while compiler directives, cgo comments,
  implicit-semicolon boundaries, literals, identifiers, and operators remain
  meaningful.
- Source-readable `.java` paths use a Java-aware token signature: ordinary
  formatting and non-documentation comments can normalize to zero, while Javadoc/
  Markdown documentation comments, literals, identifiers, operators, delimiters,
  and ambiguous Unicode escapes remain meaningful.
- Source-readable `.kt` and `.kts` paths use a Kotlin-aware token signature:
  ordinary formatting, optional semicolons/trailing commas, and non-documentation
  comments can normalize to zero, while KDoc, regular/raw strings, characters,
  numbers, identifiers, backtick names, operators, delimiters, and semantic
  newlines after jump expressions remain meaningful.
- Source-readable `.sh`, `.bash`, `.ksh`, `.bats`, `.ps1`, `.psm1`, and `.psd1`
  paths, plus `.command` or extensionless paths with matching shebangs, use
  conservative Shell/PowerShell signatures: ordinary formatting and non-directive
  comments can normalize to zero while directives, literals, identifiers,
  operators, and delimiters remain meaningful. Here-documents and here-strings
  fail closed.
- Analyzer-backed script roles route product/module, test, build, CI, delivery,
  infrastructure, integration, security, and validation deltas through existing
  categories without also charging generic product/CI work.
- Source-readable `.tf`, `.tfvars`, `.tfbackend`, and `.hcl` paths plus Terraform
  CLI configuration use a conservative HCL signature. Analyzer-backed Terraform
  facts keep infrastructure, integration, security, validation, tests,
  documentation, build, and delivery in their native categories; state, plan,
  cache, lock, generated, duplicate, and formatting-only bodies remain zero.
- Source-readable `.php` paths use a conservative PHP signature. Ordinary layout
  and non-documentation comments can normalize to zero while PHPDoc, literals,
  identifiers, variables, operators, delimiters, heredoc/nowdoc bodies, PHP tags,
  and inline template content remain meaningful; incomplete input fails closed.
- Analyzer-backed PHP facts route API, UI/template, data, integration, security,
  validation, background, build, and test deltas through existing categories.
  Vendor/cache/generated/lock/duplicate and formatting-only bodies remain zero.
- Source-readable `.rs` paths use a conservative Rust signature. Ordinary layout
  and non-documentation comments can normalize to zero while Rustdoc, identifiers
  and raw identifiers, operators, delimiters, strings and raw strings, character
  literals, numbers, lifetimes, attributes, and compiler directives remain
  meaningful; incomplete input fails closed.
- Analyzer-backed Rust facts route API, data, integration, security, validation,
  background/concurrency, FFI, build, benchmark, and test deltas through existing
  categories. Target/vendor/generated/lock/duplicate and formatting-only bodies
  remain zero.
- Dockerfile variants, filename-qualified Compose YAML, and `.dockerignore` use
  conservative artifact-specific signatures. Analyzer-backed container facts
  route final-delta build/runtime/orchestration structure through the existing
  packaging, deployment, and release-artifact category without adding a Docker
  prior. Heredocs, block scalars, tabs, and malformed dynamic structure fail
  closed; generic YAML is not treated as Compose.
- When one logical capability cites explicit production, test, documentation,
  build, or delivery roles, its existing low/expected/high budget is partitioned
  across disjoint category evidence rather than duplicated or left in one category.
- Any candidate work item above 1.5 expected hours is partitioned into distinct,
  named logical phases of roughly one hour while preserving its category and exact
  low/expected/high sum.
- Change comprehension, manual validation, and self-review are emitted once for
  the coherent final delta instead of being inherited repeatedly from repository
  scopes.
- The CLI and model are deterministic for the same snapshot identities, options,
  and versions.
- The first Ctrl+C requests cooperative cancellation through snapshot selection,
  analysis, and output; it emits a stderr-only diagnostic with exit code 130. A
  second Ctrl+C retains immediate operating-system termination.

The implementation is covered by memory-only unit snapshots plus separate
process-level Git and non-Git directory/evidence tests. The mutation matrix
includes formatting, movement,
generation, lockfiles, exact duplication, code, tests, documentation, migration,
integration, CI, container delivery, simplification, additivity, overlap, and
revert behavior while preserving range-point and category isolation. See
`MILESTONE_CHANGE_1.md` for the original boundary and
`MILESTONE_CHANGE_PORTFOLIOS.md` for the portfolio policy and fixture matrix.

## Calibration boundary

New Change calibration uses `change-ehe-work-item/1.1.0` and the existing v1 corpus,
blind-review, validation, and evaluation contracts with an optional immutable
Change reference. Repository records omit that member and retain their canonical
serialization. A Change record pins case ID, selector kind, base/head object IDs,
base/head evidence digests, a content-derived final-delta digest, and non-valuing
coverage tags. Repository family owns the development/validation/test partition.
Frozen 1.0.0 packets and corpora retain their original rubric and are reproduced
through an explicit legacy authoring path.

`calibration change-scaffold` produces unreviewed effort-only packets;
`change-compile` verifies the exact report and final-delta provenance;
`change-evaluate` applies the same versioned WAPE, bias, absolute-error, interval,
and mapping metrics as repository evaluation. A disclosed host-AI teacher may
supply logical labels without a second reviewer when model/input provenance and
evidence-backed decomposition are complete. Ordinary `review-scaffold` and
`review-compile` remain available for optional independent correction without
dropping Change lineage or changing the original maturity claim.
A zero normalized delta has an empty reviewed target list rather than an invented
zero-hour task. A reviewer rejects a false-positive or duplicate candidate
capability with a lineage-preserving exact `0/0/0` target, concrete rationale, and
size exception. That exclusion remains visible to evaluation and independent
review.

The first 24 synthetic case identities and partitions are frozen under
`calibration/changes/public-synthetic` before numerical labels. Their 24 canonical
source reports and blind authoring packets are reproducible from the checked-in
MIT synthetic suite without physical target-repository snapshots. The first-pass
teacher corpus has 121 targets and an exact-digest blind handoff; it is disclosed
weak supervision, not an empirical accuracy claim. Metric identity, Stage A gates,
and candidate decision order are frozen in `CHANGE_MODEL_ADMISSION.md`.

Rubric 1.1.0 builds totals from roughly 0.5-to-1.5-hour tasks. Targets above two
hours require a concrete indivisibility exception. Calibration starts with changes
of roughly 4 to 32 expected hours and advances to larger deliverables only after
the current band passes. Host-AI input size, token use, wall time, and cost are
recorded when available because review expense grows with source scope; none is an
EHE multiplier.

Frozen rubric-1.0.0 labels are not rewritten. The versioned Stage A logical audit
decomposes all eligible parent targets under the stricter 1.1.0 boundary while
preserving exact teacher totals and original uncertainty provenance.

`calibration/changes/public-real` adds the first current-estimator public final
delta: one MIT-licensed pull request in an already-development repository family.
Released alpha.2 reports 4.25 expected hours and a separately reasoned host-AI
teacher reports 4.00 across five targets. This one-case diagnostic changes no
prior, threshold, review maturity, or production-readiness claim.

`calibration/changes/public-real-expansion` adds six new MIT-licensed repository
families across .NET, JavaScript, and TypeScript. Its blind teacher plan was
committed before candidate values were opened. Development and validation compare
35.00 teacher expected hours with 90.50 alpha.2 hours and diagnose repeated
test/security/production slices rather than a stable aggregate multiplier. The
one test-family candidate comparison remains withheld.

Separate `change-seed/0.3.0` diagnostics preserve every frozen alpha.2 artifact and
compare only the three development and two validation records. Development moves
to 20.75 candidate versus 19.00 teacher expected hours (WAPE 0.1447, bias
+0.0921); validation moves to 15.75 versus 16.00 (WAPE 0.0156, bias -0.0156).
Consolidated item identities reduce exact mapping, some categories undershoot, and
candidate high totals remain above reviewed high totals. These one-teacher results
diagnose the correction; they do not establish calibration or accuracy. See
`MILESTONE_CHANGE_2.md` and `MILESTONE_CHANGE_3.md`.

`calibration/changes/public-real-alpha3` adds one new MIT-licensed .NET validation
family selected before candidate analysis. Released alpha.3 reports 7.00 expected
hours and a blind host-AI teacher reports 5.75 across four targets, for 1.25 hours
absolute error, 0.2174 WAPE, and +0.2174 bias. The candidate covers the full
teacher range but uses a twice-as-wide interval, overstates self-review, and maps
the added test path into production instead of a distinct unit-testing target. This
non-independent validation diagnostic changes no model or admission decision.

## Author-and-period portfolios

Author and time values are selectors, not labor evidence. Identity aliases, author
versus committer timestamp, timezone, interval inclusivity, merge handling, and
co-authorship are explicit in the implemented report. Start is inclusive, end is
exclusive, offset-free values use the selected timezone, merges are excluded by
default, and co-author trailers are included by default. First-parent merge
valuation and co-author exclusion require explicit options.

The selected portfolio requires more normalization than a simple range because
other contributors' commits may be interleaved. EffortHours estimates each selected
commit against its immutable parent, orders selected rows by the chosen timestamp,
and follows only exact object-state chains. Exact net-zero chains are removed;
overlap that is not one exact chain is retained once with attribution uncertainty.
Repeated author patches are not pre-deduplicated because a later identical patch
can be a meaningful reintroduction after a revert.

Per-change rows and their isolated estimates remain visible, but raw commit
estimates are not the portfolio total. Exact normalized expected allocations sum
to the repository-attributed total. Merge/rebase equivalence, pair-programming
shares, tests or documentation committed by another person, and work moved across
the interval boundary remain unresolved and are never inferred as personal credit.

## Use in performance reviews

An author-period report may be useful as one source of evidence in a performance
conversation. Its name and disclaimers must make clear that it measures
**repository-attributed change EHE**, not the person's productivity, value, actual
hours, or total contribution.

Repositories do not capture many valuable activities: requirements work,
architecture discussions, code review, mentoring, incident response, debugging,
coordination, research, pair work, and work credited through another person's
commit. Conversely, commit authorship does not prove sole responsibility for the
change. EffortHours must not generate employee rankings, performance grades, or
compensation recommendations from this signal alone.

## Privacy and safety

The default offline engine invokes local Git only for an explicitly requested
change operation. The optional `gh` adapter may access network data and credentials;
the CLI announces that boundary and does not retain PR bodies, discussions,
reviews, activity, or private diff bodies. Author-period reports intentionally
retain the exact caller-supplied aliases and selection policy for auditability, so
callers must treat reports containing real identities according to their own
privacy and retention requirements. Commit messages and local repository paths are
not retained.

Public fixtures must use synthetic identities and repositories. Private company
contribution data must never enter EffortHours's public calibration corpus by default.

## Deferred portfolio work

- General semantic patch equivalence across rebases, squashes, conflict
  resolutions, and non-exact clones.
- Shared-credit allocation beyond transparent repository attribution.
- More public reviewed multi-PR and author-period examples plus measured
  large-history and realistic monorepository performance.

Performance rankings, grades, and compensation decisions are deliberately
unsupported, not future portfolio features. Portfolio aggregation remains
experimental and does not broaden the source Change model's admitted size or
ecosystem boundary.
