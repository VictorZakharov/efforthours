# Change and Contribution Estimation

## Status

The first Change Estimation MVP is implemented after Milestone 7B5. It includes
provider-neutral immutable base/head analysis, one commit, one final revision
range, and one GitHub pull request through an optional `gh` adapter. The current
`change-seed/0.2.0` rules are transparent but uncalibrated and remain experimental.
Version 0.2.0 corrects non-marginal modification stacking in the initial rules
without changing repository `seed-rules/0.2.1`.
The first calibration-infrastructure checkpoint, a preliminary 24-record synthetic
host-AI teacher corpus, and a one-record real public pull-request pilot are
implemented, but no independent correction exists.
The behavioral safeguard suite now covers cancellation and category-isolated
migration, integration, CI, container-delivery, and simplification mutations in
addition to the initial normalization and Git boundaries. Multiple pull requests
and author-and-period portfolios remain deferred.

## Purpose

Repository-wide recreation is only one billing context. EffortHours should eventually
estimate the Equivalent Human Effort embodied in a completed incremental change,
while retaining the same current-artifact, no-churn valuation principles.

The output is **Change EHE**: the conventional senior-contractor effort represented
by the final functional and quality delta. It is not elapsed time, historical labor,
or a reconstruction of what a contributor actually did.

## Selector roadmap

The implemented provider-neutral engine and Git adapter support:

- immutable base and head snapshots selected by local Git revisions;
- one commit compared with a selected parent;
- a revision range compared by its final base and head;
- one pull request, with GitHub available through an optional `gh` CLI adapter.

Provider-neutral directory/evidence-bundle selectors, multiple pull requests, and
commits selected by author identity and time interval remain roadmap work. The
engine itself accepts storage-independent snapshot factories, which keeps those
future selectors separate from valuation.

The initial CLI shape is:

```text
eh change <repository> --base <revision> --head <revision>
eh change <repository> --commit <revision> [--parent <revision>]
eh change <repository> --range <base>..<head>
eh change --author <identity> --since <instant> --until <instant>
  [--date-field <author|committer>] [--timezone <iana-zone>]
eh change <repository> --pr <number-or-url> [--repo <owner/name>]
```

The implemented forms share the repository-estimate profile, format, rate, compact,
and explicit-output options. The author form is illustrative and remains deferred.
Local snapshot and Git-ref inputs do not depend on GitHub. Pull-request resolution
uses `gh pr view` only when the caller explicitly selects `--pr`; `gh` must be
installed and authenticated. The adapter retains only the requested PR number or
URL and its immutable base/head object identities. It does not retain the PR body,
discussion, author, reviews, timestamps, or private diff.

The first PR adapter analyzes objects already available in the selected local Git
object database. It does not fetch, check out, or modify the repository. When a PR
head or base object is absent locally, the command fails with an explicit
instruction to fetch that object before retrying. Automatic external object
materialization can be considered later, but it must not silently mutate the
target repository.

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

Formatting, generated output, vendored content, exact duplication, lockfile noise,
and mechanical movement must not create implementation value. Deletion is not
negative hours: deliberate removal or simplification can represent bounded design,
implementation, migration, and validation work, but deleted volume alone is not an
effort signal.

The MVP compares immutable base and head trees directly. It derives added,
modified, removed, exact-move, excluded, and unchanged-context evidence without
checking out either tree. Formatting-only classification uses conservative,
literal-aware whitespace normalization for the initial .NET and JavaScript/
TypeScript source extensions; unsupported or uncertain rewrites remain represented
and visible rather than being silently discarded. Exact blob moves are excluded
from body implementation effort. Path-sensitive integration work is included only
when separate analyzer evidence supports it.

`change-seed/0.2.0` treats repository capabilities as context rather than charging
their full modification priors whenever one cited path changes. An existing
capability receives modification work only when its normalized non-file evidence
changes. Repository-level specification, validation, and review capabilities are
replaced by one bounded change-level item each; setup and architecture apply only
when a scope is added or removed. Repeated modified paths in one category share a
single diminishing marginal budget, and the edit-region rates for an existing
modified artifact are 30% of the corresponding new-artifact fallback rates. A
specialized UI or other boundary category therefore requires changed boundary
evidence; a source file's location inside such a scope is not sufficient.

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

The v1 public schemas are `change-evidence`, `change-estimate-report`, and
`change-estimate-explanation`. The current estimator identity is
`change-seed/0.2.0`; it composes the still-uncalibrated repository
`seed-rules/0.2.1` model and must not be described as production-ready. Frozen
calibration source reports retain the exact earlier estimator identity they were
created from.

## Implemented CLI behavior

- Moving selectors are resolved to immutable object IDs before analysis.
- Git trees are streamed through `ls-tree` and bounded `cat-file --batch`; EffortHours
  does not create temporary checkouts or source trees.
- Root commits use Git's empty tree. Merge commits require an explicit parent.
- Ranges expose isolated commit estimates, normalized final effort, named signed
  adjustments, and allocations that sum exactly to normalized expected hours.
- PR mode invokes `gh` only to resolve number/URL and base/head object IDs, then
  requires both objects to exist locally.
- JSON and Markdown output include optional pricing only after hours are estimated;
  saved JSON supports work-item explanation queries.
- Existing-capability modifications require changed normalized capability evidence,
  and correlated category/path evidence receives one marginal budget.
- Change comprehension, manual validation, and self-review are emitted once for
  the coherent final delta instead of being inherited repeatedly from repository
  scopes.
- The CLI and model are deterministic for the same objects, options, and versions.
- The first Ctrl+C requests cooperative cancellation through snapshot selection,
  analysis, and output; it emits a stderr-only diagnostic with exit code 130. A
  second Ctrl+C retains immediate operating-system termination.

The implementation is covered by memory-only unit snapshots plus separate
process-level Git tests. The mutation matrix includes formatting, movement,
generation, lockfiles, exact duplication, code, tests, documentation, migration,
integration, CI, container delivery, simplification, additivity, overlap, and
revert behavior while preserving range-point and category isolation. See
`MILESTONE_CHANGE_1.md` for the delivered boundaries and remaining limitations.

## Calibration boundary

Change calibration uses `change-ehe-work-item/1.0.0` and the existing v1 corpus,
blind-review, validation, and evaluation contracts with an optional immutable
Change reference. Repository records omit that member and retain their canonical
serialization. A Change record pins case ID, selector kind, base/head object IDs,
base/head evidence digests, a content-derived final-delta digest, and non-valuing
coverage tags. Repository family owns the development/validation/test partition.

`calibration change-scaffold` produces unreviewed effort-only packets;
`change-compile` verifies the exact report and final-delta provenance;
`change-evaluate` applies the same versioned WAPE, bias, absolute-error, interval,
and mapping metrics as repository evaluation. Ordinary `review-scaffold` and
`review-compile` provide independent correction without dropping Change lineage.
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
weak supervision, not an accuracy claim. Metric identity and
candidate decision order are frozen in `CHANGE_MODEL_ADMISSION.md`; numerical
thresholds, model fitting, and accuracy claims remain blocked on realistic,
independently reviewed error measurements.

`calibration/changes/public-real` adds the first current-estimator public final
delta: one MIT-licensed pull request in an already-development repository family.
Released alpha.2 reports 4.25 expected hours and a separately reasoned host-AI
teacher reports 4.00 across five targets. This one-case diagnostic changes no
prior, threshold, review maturity, or production-readiness claim. See
`MILESTONE_CHANGE_2.md` and `MILESTONE_CHANGE_3.md`.

## Author-and-period portfolios

Author and time values are selectors, not labor evidence. Identity aliases, author
versus committer timestamp, timezone, interval inclusivity, merge handling, and
co-authorship must be explicit in the report.

The selected portfolio requires more normalization than a simple range because
other contributors' commits may be interleaved. Before this mode is considered
credible, prototypes must address:

- exact duplicate and cherry-picked patches;
- reverts and net-zero change sequences;
- overlapping edits across selected commits;
- merge commits and rebases;
- co-authored and pair-programmed changes;
- changes whose tests or documentation land in another contributor's commit; and
- work moved across the interval boundary.

EffortHours may expose both per-change rows and a normalized portfolio total, but it
must not sum raw commit estimates when doing so would double count the same final
capability. Any unresolved attribution ambiguity widens ranges and remains visible.

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

The default offline engine should invoke local Git only for an explicitly requested
change operation. The optional `gh` adapter may access network data and credentials;
the CLI must announce that boundary and avoid persisting PR bodies, author emails,
private diffs, or repository evidence unless the caller requests an output path.

Public fixtures must use synthetic identities and repositories. Private company
contribution data must never enter EffortHours's public calibration corpus by default.

## Deferred portfolio work

- Multiple-PR selection and cross-PR normalization.
- Author-and-period identity, timezone, co-author, merge, and interval semantics.
- Shared-credit policy beyond transparent repository attribution.
- Performance-review workflows, rankings, grades, or compensation decisions.

Portfolio aggregation and shared-credit behavior still need reviewed examples
before performance-review use is described as anything beyond experimental.
