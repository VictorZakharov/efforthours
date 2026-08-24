# Change and Contribution Estimation

## Status

EffortHours implements provider-neutral final-delta analysis for immutable local
Git revisions, commits, ranges, and one pull request; two statically scanned
directories; and two saved repository-evidence bundles. Portfolio reconciliation
supports repeated PRs, multi-repository manifests, and bounded author-period
selection under the separate `CHANGE_PORTFOLIOS.md` contract.

Current source reports use `change-seed/0.18.2+seed-rules/0.4.0`. The model remains
experimental and is not empirically production-validated. Only the documented
0.6.0 Stage A subset has passed a model-authored logical gate for eligible
4-to-32-hour changes; later ecosystem extensions preserve those admitted rules but
were not present in that evidence and are not separately admitted.

Version 0.18.2 retains the 0.18.1 priors and work-item rules while narrowing
large-Git context loading to changed-path neighborhoods, retaining enough parent
metadata for a complete default identity ledger, and permitting bounded
multi-repository portfolio execution described below.
The current rules retain bounded logical marginality, fail-closed generated-file
customization, language-aware formatting normalization, mixed-role category
partitioning, roughly-one-hour Change tasks, immutable snapshot reuse, bounded
range audits, final-delta reconciliation, and stable explanation lineage. Exact
versions and historical diagnostics remain in `CHANGELOG.md`,
`CHANGE_MODEL_ADMISSION.md`, and the immutable artifacts under
`calibration/changes/`.

## Purpose

Repository-wide recreation is only one estimation context. EffortHours also
estimates the Equivalent Human Effort embodied in a completed incremental change
while retaining the same current-artifact, no-churn valuation principles.

The output is **Change EHE**: the conventional senior-contractor effort represented
by the final functional and quality delta. It is not elapsed time, historical labor,
or a reconstruction of what a contributor actually did.

## Supported selectors

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
- commits selected by exact author/co-author alias and an explicit time interval; or
- a v1 author-period manifest spanning multiple local repositories and pinned
  heads; or
- an explicit GitHub-assisted today-to-date request that resolves to that same v1
  manifest before local estimation.

The engine accepts storage-independent snapshot factories, which keeps selector
adapters separate from valuation. Portfolio selection composes canonical immutable
Change reports and applies pricing only after repository-level normalization.

The CLI surface is:

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
eh change portfolio --author-period-manifest <manifest.json>
eh change today --owner <owner> --author "@me" --timezone <zone> \
  --scope engineering --capacity-hours <hours> [--include-open-prs]
eh change scope show engineering
eh change portfolio <repository> --author <identity> --since <instant> --until <instant>
  [--date-field <author|committer>] [--timezone <iana-or-host-zone>]
  [--merge-policy <exclude|first-parent>] [--coauthors <include|exclude>]
```

The implemented forms share the repository-estimate profile, format, rate,
compact, and explicit-output options. Directory pairs and evidence pairs are
deliberately separate selector families; incomplete or mixed pairs fail before
analysis. Portfolio commands likewise require exactly one repeated-PR, manifest,
direct-author-period, author-period-manifest, or today-to-date family. Local
snapshot and Git-ref inputs do not depend on GitHub. `change today` is a deliberate
orchestration exception that queries GitHub through `gh`, creates/reuses active
repositories in a private bare cache, applies the versioned engineering path
profile before immutable analysis, runs exact preflight, pins an in-memory v1
manifest, and then returns to the existing local estimator boundary.
Pull-request resolution uses `gh pr view` only when the caller explicitly selects
`--pr`; `gh` must be installed and authenticated. The adapter retains only the
requested PR number or URL, immutable provider base-tip/head object identities,
provider changed-file count, and the execution-only base ref/source needed by an
explicit acquisition. Local Git resolves the unique merge base, and that merge
base to head becomes the reviewed PR delta. It does not retain the PR body,
discussion, author, reviews, timestamps, or private diff.

Repeated PR selection accepts at most 128 rows. A manifest gives each row a stable
caller ID, repository ID, execution-only local repository path, PR selector, and
optional GitHub repository. Relative paths resolve from the manifest directory and
never enter report output. Caller repository IDs and resolved local Git roots must
map one-to-one, so labels cannot combine separate repositories or split one
repository around normalization. Each repository is normalized independently;
totals are then added without cross-repository deduplication.

Author-period selection asks Git to prefilter the reachable graph with fixed,
case-insensitive identity aliases, streams that metadata without a lifetime-sized
output buffer, and applies the exact timestamp and structured identity checks to
each record before retaining it. Exact candidates are accounted in logical 1,024-
row selection chunks and charged against a deterministic 128-MiB retained-ledger
budget per repository; lifetime matches outside the requested interval consume no
ledger bytes. The selector emits every retained match and has no ordinary calendar
or presentation-row cap. This preserves non-monotonic author-date correctness
without loading an unbounded identity ledger; Git's committer-date traversal
cutoffs are not applied to author-date selection. Counts of 100,000 candidates per
repository and 640,000 selected changes per manifest are final circuit breakers
after byte, cache, queue, checkpoint, and output bounds. A resource failure reports
a lower-bound in-window count, observed charge, exhausted resource, and privacy-
safe direct/co-author counts by requested contributor; it never returns a
truncated result. Successful selection records the same total, byte/chunk usage,
and per-contributor breakdown without raw aliases. Exact case-insensitive aliases
match author name, email, or `Name <email>`. The interval is start-inclusive and
end-exclusive;
offset-free timestamps use the declared timezone, and skipped or ambiguous local
times fail unless the caller supplies an offset. Author versus committer time,
co-author trailer inclusion, and merge exclusion versus first-parent valuation are
explicit report policies. Git returns only valid `Co-authored-by` trailer values;
commit bodies are not returned to EffortHours or retained.

The versioned `author-period-candidate-ledger-charge/1.0.0` policy charges 512
bytes per candidate entry, 32 bytes per retained string plus two bytes per
character, 32 bytes per retained identity/collection, and eight bytes per
collection reference. The charge covers commit/parent IDs and structured author,
committer, and co-author identities. It is deterministic conservative accounting,
not a claim about sampled CLR heap size.

The author-period manifest applies that exact selector across stable contributor
IDs, repository IDs, and pinned repository-local heads. Relative paths resolve
from the manifest directory. Before Change analysis starts, every path must resolve
to one readable local Git root, repository IDs and roots must map one-to-one, and
every pinned commit must exist locally. Author-period mode never fetches a missing
object and rejects the PR-only `--fetch-missing` option.
For each repository, all pinned heads enter one union query per required identity
filter, so shared ancestry and fully overlapping heads cannot duplicate a commit.
A bounded-memory topological pass then propagates head reachability until every
selected object is mapped. Reports retain the canonical manifest digest, shared
policy, stable contributor/repository/head IDs, immutable objects, exact match
kinds, and reachable head IDs; raw aliases and local paths remain execution-only.
Repositories and heads with no unique selected commit remain visible in selection
metadata. Matching several contributors or heads never multiplies the commit or
its EHE.

The PR adapter analyzes objects already available in the selected local Git object
database without fetching by default. When a PR head or provider base-tip object
is absent, the default command fails and names `--fetch-missing` as the explicit
opt-in. That option fetches only the provider base ref and selected PR head ref
from the provider repository as source-only refspecs with tags, recursive
submodules, and `FETCH_HEAD` writes disabled. HTTPS authentication uses the
already-authenticated `gh auth git-credential` helper through command-local Git
configuration; no global config is changed and no token enters arguments or
reports. It adds immutable objects to the local object database but does not update
any local or remote-tracking ref, index, or worktree; it does not check out or
execute target code. Existing local objects still short-circuit without network
access. Cancellation is propagated to Git and no report is emitted from a partial
acquisition. Once both exact resolved objects exist, local Git must resolve exactly
one merge base; no common ancestor or several criss-cross merge bases fail rather
than selecting an arbitrary boundary.

All immutable Git changes use a bounded changed-scope evidence projection. The
analyzer enumerates immutable changed
paths, root context artifacts, recognized static project/package/build context in
the changed path's directory or ancestors, and one deterministic representative
per supported source extension. It does not parse every unrelated nested project
or package descriptor merely because it exists in the same snapshot. Full base/head
inventories remain available for additions, removals, moves, exact duplicates,
unchanged-context counts, and a content-addressed source identity; unchanged
source bodies are not routinely parsed. Diagnostic `FB5205` records changed,
relevant-context, representative, available-context, and full-inventory counts.
Directory/evidence selectors retain full-snapshot analysis.

Within one Git repository session, at most 10,000 structurally shared immutable
inventories across 16 full-tree root lineages and 16 exact snapshot/scope analyses
are retained. First-parent links are remembered through the 100,000-candidate
emergency selection envelope. A known first-parent child with at most 1,024
changed paths and 16,000 path characters is derived from its cached parent plus
literal Git path deltas. Eligible non-merge deltas and changed-blob sizes are
loaded in two 64-MiB-output-bounded repository-level Git batches before row
analysis; roots, merges, custom snapshot providers, larger deltas, and unrelated
changes retain the exact per-change or complete-`ls-tree` fallback. Full-tree
enumeration loads path, mode, and immutable object identity without requesting
every blob length. The repository object-storage layout is inspected once per
session. Packed stores and stores below 1,024 loose objects use one recursive
`ls-tree`; larger loose stores with at least 128 disjoint shallow tree paths use
at most four deterministic recursive readers per tree and eight readers across
the process. Smaller frontiers and excessive path arguments retain the exact
single-reader fallback. Git tree reads use a separate bounded queue from managed
parsing and estimation, so object-store wait does not consume a CPU-work slot.
One repository-scoped
`cat-file --batch-check` reader resolves
lengths lazily and retains at most 16,384 entries; content reads retain their
separate 64-MiB bounded reader. Full virtual-directory indexes also start lazily.
These fixed retention limits let adjacent changes reuse evidence without making
cache memory unbounded.

Adjacent first-parent snapshots may also reuse a prior exact-scope repository
analysis without rescanning it. Reuse is exact when no path changed inside the
scope. When exactly one in-scope path changed, reuse is limited to a maintained,
unique C# body whose byte length is unchanged and whose cached, syntax-clean
Roslyn lineage proves that the only textual change stays inside one numeric
literal token. Numeric values are not analyzer evidence, and the fixed-size,
single-token proof preserves every source location and common/semantic metric;
only the file SHA-256, repository/scope identity, and matching diagnostic are
refreshed. Any structural edit, syntax error, length change, duplicate body,
generated/vendored/minified/binary classification, missing lineage, cache miss,
or unsupported filesystem uses the ordinary full analyzer. The invocation-local
lineage cache retains at most eight states and 16 MiB of decoded source text per
repository; syntax trees are additionally bounded by the same eight-entry limit.

Git inventory identity uses canonical SHA-256 Merkle nodes over path, mode, and
blob object identity. The digest is independent of delta application order and can
be updated with the structurally shared inventory rather than rehashing every
unchanged path. Blob identity already binds content and byte length, so lazily
resolved lengths do not alter the digest. Exact snapshot analysis is keyed by this
content identity plus the analysis-scope digest; two commits with the same
immutable tree and scope can reuse analysis, while different scopes remain
separate.

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

For a PR, the preferred estimate compares the unique merge base of the provider's
base tip and PR head with that head. Advancing the base branch therefore cannot
turn unrelated base-only work into PR modifications or removals. Open and merged
PRs retain this reviewed-head delta by default; estimating an integrated merge
result, including conflict resolution, would require a separate explicit mode.
The selection records the provider base tip, comparison-base policy, exact
comparison base/head, object-acquisition mode, provider changed-file count, raw
analyzed path count, represented path count, and `match`, `mismatch`, or
`provider-unavailable` status. A mismatch emits warning `FB5107`; it does not
silently replace the immutable local comparison or confuse represented paths with
all analyzed paths.
For a revision range, the preferred estimate compares the final base and head
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

The implemented C/C++ `change-seed/0.18.0` signature may ignore ordinary layout and non-documentation
comments while preserving documentation, preprocessing directives and replacement
tokens, identifiers, operators, delimiters, literals, raw strings, attributes,
declaration structure, and meaningful ordering. Malformed directives, ambiguous
line splicing, invalid literals, and unbalanced structure fail closed. Header
fan-out and translation-unit count never multiply a delta. Analyzer-backed
production, API, UI, data, integration, security, validation, concurrency, FFI,
test, build, and delivery evidence routes through existing categories.
`CPP_ANALYSIS.md` defines the complete implemented boundary.

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
or current PR selections. PR mode pins the provider base-tip/head identities and
their unique comparison merge base but must not invent intermediate work. A future
PR form would need explicit opt-in immutable commit enumeration before this
diagnostic could be available.

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
and cross-repository totals add only after independent normalization. Manifest
author-period reports additionally group every selected item once by its exact
requested-contributor match set and, independently within each repository, once by
its exact reachable-head set. Singleton and shared groups each expose normalized
low/expected/high totals. Shared groups retain the full repository-attributed item
without copying it into several contributor or head totals and without inventing
personal-credit percentages.

Every requested contributor has a singleton group, every requested repository has
a repository summary, and every pinned head has a singleton reachability group,
including deterministic zero rows when another part of the manifest supplies the
selected portfolio. Non-additive contributor/head summaries report direct-author,
co-author, reachable, unique, and shared counts and reference the additive groups.
The contributor and head ledgers are alternative decompositions of one
authoritative total and must never be added together.

Expected group totals are the exact sum of their item allocations. Low and high
are allocated inside each repository with the same deterministic expected-weight
and largest-remainder policy; an expected-zero/high-positive repository uses
isolated high only as a bound-allocation fallback. Each group records isolated and
normalized ranges, their signed difference, item IDs, repository-group lineage,
and influencing adjustment IDs. These are structural audit allocations, not
personal labor shares.

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
`change-estimate-explanation`, `change-portfolio-manifest`,
`change-author-period-manifest`, `change-portfolio-bucket-manifest`,
`change-portfolio-capacity-manifest`, `change-portfolio-preflight-report`,
`change-portfolio-report`, and `change-portfolio-comparison-report`. The Change report
schema adds an optional normalization summary, so frozen v1 reports remain valid;
explanation queries accept its stable calculation ID and return exact adjustment
lineage. Portfolio contracts separately record selection policy, source estimator
identity, immutable base contexts, patch/evidence digests, isolated rows,
repository-normalized categories, signed adjustments, exact allocations,
attribution uncertainty, contributor/repository/head aggregate ledgers,
verification, and post-EHE pricing. They emit neither local repository paths nor
source excerpts.

The current source Change estimator identity is
`change-seed/0.18.2+seed-rules/0.4.0`; the portfolio reconciler identity is
`change-portfolio/0.2.5+change-seed/0.18.2+seed-rules/0.4.0`. The earlier 0.6.0
Change identity alone passed the experimental Stage A logical gate, and that
record contains no SQL, Python, Go, Java, Kotlin, Shell, PowerShell, Terraform,
HCL, PHP, Composer, Rust, Cargo, Docker, Compose, Jupyter, C, or C++. Portfolio
aggregation does not broaden that admission. Neither 0.18.2 nor portfolio 0.2.5
may be described as empirically calibrated, generally admitted, or production-
ready. Frozen calibration source reports retain
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
- Full Git trees stream path, mode, and object identity through `ls-tree` without
  eagerly requesting every blob length. Packed and small loose-object stores use
  one recursive reader; large loose-object stores can use at most four
  deterministic readers per tree and eight readers process-wide through a queue
  separate from managed CPU work. Eligible portfolio first-parent deltas and
  changed-blob sizes use 64-MiB-output-bounded `diff-tree --stdin` and
  `cat-file --batch-check` repository batches; other admitted lengths are resolved
  lazily through one bounded repository metadata reader, and source bodies use a
  separate bounded `cat-file --batch` reader. Batch diff framing requests an
  explicit commit header even when a valid one-parent commit changes no paths, so
  an empty commit becomes a zero-path transition while a genuinely omitted
  requested object still fails closed. EffortHours does not create temporary
  checkouts or source trees.
- Root commits use Git's empty tree. Merge commits require an explicit parent.
- Ranges expose isolated commit estimates, normalized final effort, named signed
  adjustments, allocations that sum exactly to normalized expected hours, and the
  expected-point normalization diagnostic for explicit multi-commit ranges.
- Adjacent range or portfolio components reuse repository analysis by canonical
  immutable inventory digest and analysis-scope identity. An exact tree chain over
  one scope requires at most `N + 1` ordinary repository estimates instead of
  `2N`; equal trees reached through different commits also reuse analysis. The
  bounded C# lineage proof above may derive additional adjacent snapshots without
  another repository estimate. Differing scopes and all unproven edits remain
  independent canonical estimates.
- Portfolio 0.2.4 retains up to 16 exact snapshot/scope analyses and 8,192
  immutable file-analysis artifacts with deterministic key-ranked retention per
  active repository and permits at most two repository sessions to overlap.
  Within each repository, eligible deltas are primed in deterministic chunks of
  16; one ordered producer opens the next immutable snapshot pair while four
  consumers analyze earlier pairs. Common file inspection similarly enqueues
  discovered paths into a bounded producer/consumer pipeline; workers read and
  inspect earlier files while traversal discovers later work. A process-wide
  buffered-read budget prevents concurrent snapshots from retaining unbounded
  file content.
  Common inspection, .NET/JavaScript semantic parsing, and the thread-safe seed
  estimator share one process-wide CPU-work budget of at most 24 visible logical
  processors. Custom estimators remain serialized unless they explicitly declare
  thread safety. Canonical path and report order is restored before aggregation,
  and single-flight caches ensure concurrent requests for the same immutable
  inspection or snapshot analysis compute it once. Snapshot scope identity covers
  its canonical path set; cached evidence is rebound to each requesting row's
  output-only changed/context/representative counts before report construction.
  Author-period plans from one repository share
  one lazy Git object reader, one lazy Git metadata reader, a bounded 64-MiB/
  1-MiB-per-blob content cache, 10,000 structurally shared immutable indexed
  inventories across at most 16 full-tree roots, 16,384 retained object lengths,
  and 100,000 remembered first-parent links. Cached
  .NET/JavaScript file results and common inspections are keyed by immutable
  content, analyzer identity, and required path/context inputs; they do not broaden
  a row's canonical analysis scope. .NET project, solution, central-package, and
  derived project-fact context can additionally reuse one exact immutable artifact
  when every admitted descriptor object ID and the complete repository path-set
  identity match. Any path addition/removal or descriptor-content change selects a
  different key, preserving project-reference and solution-resolution semantics;
  a snapshot provider without both identities uses ordinary cold analysis. These
  entries share the existing 8,192-entry deterministic artifact bound rather than
  adding another unbounded cache. Repository evidence lineage is scheduled in
  deterministic first-parent order over already-opened structural inventories.
  Each row analyzes its base before its head, and optional parent-derived snapshot
  or C# evidence is consumed only when already complete; optional reuse never waits
  on an in-flight ancestor. When a selected row's base is an earlier queued row's
  immutable head, it waits for that row's completion so chronological reuse remains
  available without recursive in-flight waits. It does not traverse or admit
  unrelated history. The two-session
  ceiling is a deliberate bounded memory-for-latency tradeoff; cancellation
  disposes every repository context, and cache keys never merge equal-looking
  objects across repositories.
- The optional per-commit reconciliation audit is capped at 256 components by
  default. Larger ranges emit `FB5105` and retain the complete final base-to-head
  estimate while omitting the oversized component ledger.
- Component attribution uses nonnegative largest-remainder cents and sums exactly
  even for large component sets.
- PR mode invokes `gh` only to resolve bounded identity, base-ref, and changed-file
  metadata. It reuses local objects by default, optionally acquires only missing
  selected objects under `--fetch-missing`, and uses the provider base/head unique
  local merge base as the comparison base.
- Portfolio mode accepts at most 128 repeated PRs or 128 schema-valid manifest
  rows; every source item is estimated without a rate before reconciliation.
- Manifest repository paths are execution-only. Relative paths resolve from the
  manifest directory and may name readable siblings or descendants; there is no
  worktree-containment rule. Reports retain caller IDs, immutable identities, and
  stable digests without host paths.
- Author-period mode materializes exact in-window identity candidates from
  streamed Git-prefiltered metadata over pinned reachable graphs under a
  deterministic 128-MiB charged ledger per repository and logical 1,024-candidate
  selection accounting. Lifetime matches outside the requested interval consume no
  retained bytes. It applies no ordinary calendar-month or presentation-row
  ceiling; 100,000 repository candidates and 640,000 selected changes are final
  circuit breakers inside the complete 256-repository input envelope. `--preflight`
  measures this scope without constructing snapshots or estimating EHE and emits
  a versioned JSON/Markdown execution recommendation. It records the
  inclusive/exclusive interval, timezone, date field, co-author policy, and merge
  policy.
- Portfolio JSON and Markdown show isolated and repository-normalized totals,
  base contexts, every selected row, exact expected allocations, signed
  adjustments, and unresolved attribution without source excerpts.
- Manifest portfolios can produce one exact calendar/custom bucket decomposition
  across all requested contributors. Optional reference capacity supplies only a
  denominator. Versioned comparison JSON, final trend Markdown, and generic
  engineering-findings Markdown all derive from the same reconciled portfolio;
  shared contributor groups remain separate and count once.
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
  low/expected/high sum. Cent-rounded parts use a nonnegative bounded remainder,
  so a high part count cannot overdraw the total or invert the final range.
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
revert behavior while preserving range-point and category isolation. Portfolio
policy and its additional fixture boundary are documented in
`CHANGE_PORTFOLIOS.md`.

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
`CALIBRATION.md`, `calibration/changes/README.md`, and
`CHANGE_MODEL_ADMISSION.md`.

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
retain selection policy for auditability. The direct single-repository command
also retains its exact caller-supplied aliases for backward compatibility, so
callers must treat those reports according to their own privacy and retention
requirements. The separate multi-repository author-period manifest keeps aliases
and local paths execution-only: reports retain a canonical manifest digest,
caller-approved contributor/repository/head IDs, and immutable object IDs instead.
Commit messages and local repository paths are not retained.

Public fixtures must use synthetic identities and repositories. Private company
contribution data must never enter EffortHours's public calibration corpus by default.

## Deferred portfolio work

- General semantic patch equivalence across rebases, squashes, conflict
  resolutions, and non-exact clones.
- Shared-credit allocation beyond transparent repository attribution.
- More public reviewed multi-PR and author-period examples plus larger,
  cross-platform large-history and realistic monorepository measurements.

Performance rankings, grades, and compensation decisions are deliberately
unsupported, not future portfolio features. Portfolio aggregation remains
experimental and does not broaden the source Change model's admitted size or
ecosystem boundary.
