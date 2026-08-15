# Change portfolio reconciliation

## Current boundary

`change-portfolio/0.2.1` composes canonical Change estimates selected as repeated
pull requests, a versioned multi-repository PR manifest, a bounded direct
author-period, or a versioned multi-repository/multi-head author-period manifest.
It remains experimental and has no empirical production validation.

The result is **repository-attributed Change EHE**: counterfactual replacement
effort represented by selected immutable changes after portfolio normalization.
It is not actual labor, a timesheet, proof of sole authorship, individual
productivity, a performance grade, or compensation advice.

Portfolio reconciliation changes no source Change prior, frozen report, label, or
model-admission decision. Every source item retains the model status and size/
ecosystem boundary of its canonical Change estimate.

## Selectors

One command accepts exactly one selector family:

```text
eh change portfolio <repository> --pr <number-or-url> --pr <number-or-url>
eh change portfolio --manifest <portfolio.json>
eh change portfolio --author-period-manifest <manifest.json>
eh change portfolio <repository> --author <alias> [--author <alias> ...]
  --since <instant> --until <instant>
```

Repeated PR selectors use the optional `gh pr view` boundary only to resolve a
number/URL and immutable base/head object IDs. Every object must already exist in
the local Git database.

A versioned manifest can select PRs from multiple local repositories and assigns
explicit caller repository and row IDs. Relative repository paths resolve from
the manifest directory and remain execution-only; they are never copied into the
report. Repository IDs and resolved Git roots map one-to-one so caller labels
cannot combine unrelated repositories or bypass same-repository overlap.

The separate `change-author-period-manifest` v1 contract supplies the input and
privacy boundary for the multi-repository, multi-head author-period selector:

```text
eh change portfolio --author-period-manifest <manifest.json> [options]
```

The executable exposes that spelling together with the complete loader and
repository-scoped union planner. Profile, output, and optional pricing remain
invocation-wide.

The manifest contains one shared interval, timezone, date field, merge policy,
and co-author policy; stable contributor IDs with execution-only aliases; and
stable repository/head IDs with execution-only local paths and pinned immutable
objects. Profile and optional pricing remain invocation-wide CLI options, so they
cannot vary by contributor, repository, or head. A representative public-safe
shape is:

```json
{
  "schemaVersion": "1.0.0",
  "selection": {
    "sinceInclusive": "2026-08-03T00:00:00-04:00",
    "untilExclusive": "2026-08-10T00:00:00-04:00",
    "timeZone": "America/Toronto",
    "dateField": "author",
    "mergePolicy": "exclude",
    "coauthorPolicy": "include",
    "intervalSemantics": "since-inclusive-until-exclusive"
  },
  "contributors": [
    {
      "id": "contributor-a",
      "aliases": ["Contributor A", "contributor-a@example.invalid"]
    }
  ],
  "repositories": [
    {
      "id": "repository-a",
      "repositoryPath": "repositories/repository-a",
      "heads": [
        {
          "id": "default",
          "objectId": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
        }
      ]
    }
  ]
}
```

Public IDs use only letters, digits, `.`, `_`, and `-`, start with a letter or
digit, and are limited to 128 characters. The v1 execution budgets are 32
repositories, 32 heads per repository and 128 heads overall, 64 contributors, 16
aliases per contributor and 128 aliases overall, and 128 selected commits. An
immutable head object may appear only once within one repository; equal object-ID
text in different repositories remains repository-scoped. The CLI reads at most
one MiB of strict UTF-8 manifest JSON.

Semantic validation rejects duplicate IDs, aliases assigned to several
contributors, empty alias/head sets, repeated repository-local head objects,
non-canonical text, invalid object IDs, unsupported versions, reversed intervals,
and over-budget input. The execution adapter must additionally resolve paths
relative to the manifest, enforce a one-to-one repository-ID/root mapping, verify
every pinned commit locally, and reject missing or unsafe inputs before analysis.
It must not fetch, execute target code, or write into a target repository.

Execution preflights every repository and pinned commit before Change analysis.
Each repository's pinned heads are passed together into Git's identity-prefiltered
walk, which forms a repository-scoped reachable union and returns each commit
object once. The exact structured identity/time selector runs after that prefilter.
A separate topological walk propagates a compact head bitset through shared history
and stops once all selected commits have their complete head reachability. Its
frontier is bounded independently from the 10,000-record identity ledger, and the
walk has one-million-visited-commit and 100,000-frontier-entry hard stops.
Contributor, alias, repository, and head input order is canonicalized before
report construction.

The canonical semantic manifest digest sorts repositories, heads, contributors,
and aliases before hashing, so equivalent array reorderings retain one identity.
It binds identity aliases, stable IDs, immutable objects, and shared policy while
deliberately excluding local repository paths, so relocating the same object
databases does not change report identity. Only the digest crosses into the
report. A manifest-based report selection keeps the digest, shared policy,
contributor IDs, repository IDs, head IDs, and immutable object IDs. Per-item
attribution can retain contributor match kind and reachable head IDs; raw aliases
and local paths are excluded. The existing direct single-repository `--author`
report remains backward compatible and continues to retain its explicitly
supplied aliases.

Author-period selection is bounded to 10,000 Git-prefiltered identity candidates
per repository and 128 selected rows across the manifest. Git may traverse a
larger reachable graph without returning its unrelated identity records to
EffortHours. The exact selector then validates
structured author/co-author identities and the requested time policy. It provides:

- exact case-insensitive aliases matching author name, email, or
  `Name <email>`;
- inclusive `--since` and exclusive `--until` instants;
- an explicit timezone for offset-free values, with skipped or ambiguous daylight-
  saving times rejected;
- explicit author-versus-committer date selection;
- merge exclusion by default or explicit first-parent valuation;
- explicit inclusion or exclusion of valid `Co-authored-by` trailers; and
- a pinned reachable-history head.

Identity and time select rows only. They do not enter an effort rule or multiplier.
Git returns only structurally extracted co-author values; commit messages are not
retained in contracts or reports.

### Optional host-assisted scaffolding

Broad provider discovery remains outside the estimator. The accepted design in
[`AUTHOR_PERIOD_SCAFFOLDING.md`](AUTHOR_PERIOD_SCAFFOLDING.md) recommends an
optional companion adapter that emits the unchanged v1 manifest plus a separate,
local-only provenance sidecar. A caller must review and pin that manifest before a
distinct offline estimation invocation. Discovery may never fetch, expand scope
silently, translate provider accounts into Git aliases, or turn provider activity
into effort.

No such adapter is implemented or required. The local manifest and estimator remain
the complete provider-independent product boundary.

## Reconciliation policy

Every row is first estimated independently through the canonical Change engine
with pricing disabled. The portfolio reconciler then:

1. groups rows by caller-visible repository identity;
2. exposes every immutable base context used by those rows;
3. keeps disjoint connected components additive;
4. suppresses exact represented PR patch identities inside one repository,
   including cherry-picked equivalents, but never across repositories;
5. maps non-shared category hours through each work item's cited path evidence and
   normalizes the same path/category with an order-independent maximum while
   preserving independent code, test, and documentation paths;
6. follows selected author commits chronologically, excludes an exact object chain
   that returns a path to its initial state, and preserves later reintroduction;
7. represents shared specification, setup, design, validation, and review context
   once inside an overlapping connected component, not globally; and
8. applies an optional rate only after normalized EHE is complete.

Opposing PR effects are not replayed in caller order. They remain represented once
with ordering uncertainty. Interleaved author commits that do not form one exact
object chain remain represented with attribution uncertainty. This is
deterministic structural normalization, not a causal-credit model.

Every row retains its isolated low/expected/high estimate. Repository groups and
the portfolio expose normalized totals, named signed adjustments, and an expected-
hour allocation per row. Deterministic largest-remainder rounding makes allocations
sum exactly to normalized expected EHE. Allocations are audit values, not
reconstructed work hours.

Manifest author-period reports add two exclusive match-set ledgers:

- the contributor ledger groups an item by the exact set of requested contributor
  IDs that matched it; and
- each repository's head ledger groups an item by the exact set of pinned heads
  from which it is reachable.

A singleton group is emitted for every requested contributor and head even when
it is a zero row. A repository summary is likewise emitted for every requested
repository. Exact multi-contributor and multi-head sets become shared groups, so a
direct-author match for one contributor plus a co-author match for another remains
one item and one additive EHE row. The report does not copy the full value into
several contributor totals, infer a percentage split, or treat a singleton match
as proof of sole authorship.

Both ledgers expose low/expected/high normalized effort and reconcile independently
to the same repository and portfolio totals. Expected group effort sums the
existing exact per-item allocation. Low and high use the same expected weights and
deterministic largest-remainder arithmetic inside the repository; only an
expected-zero/high-positive bound uses isolated high as a fallback weight. Every
group retains item IDs, signed isolated-to-normalized delta, repository-group
identity, influencing adjustment IDs, and uncertainty. Contributor and head views
are alternative decompositions and must not be added together.

Non-additive summary rows expose direct-author/co-author counts, shared-match
counts, head reachability, uniquely reachable counts, and heads with no unique
selected commit. Zero rows are available when the overall manifest still selects
at least one commit; an entirely empty selection continues to return the existing
clear no-match error rather than inventing estimator metadata.

## Contracts and output

The v1 schema catalog includes:

- `change-author-period-manifest`;
- `change-portfolio-manifest`; and
- `change-portfolio-report`.

Contracts keep selection, immutable base contexts, source Change identity,
observed patch/evidence digests, isolated estimates, normalized categories,
attribution metadata, signed adjustments, exact allocations, aggregate match-set
ledgers, diagnostics, verification, and pricing separate.

The portfolio report schema adds optional manifest-safe author-period selection,
contributor-match, head-reachability, and aggregation members. Existing PR and
direct author-period documents omit them and retain their prior serialization;
saved `change-portfolio/0.1.0` manifest reports without aggregation remain valid.

JSON and Markdown output omit source excerpts and local repository paths. Markdown
shows every selected row, repository group, adjustment, uncertainty, and safety
warning in a visible ledger.

## Verification boundary

Storage-independent fixtures cover:

- disjoint additivity and selector-order independence;
- exact duplicate/cherry-pick suppression and zero duplicate allocation;
- overlapping paths and unresolved order;
- mechanically split sequential edits;
- implementation, tests, and documentation delivered in separate rows;
- shared test/documentation paths without erasing independent implementation;
- exact add/revert chains and later reintroduction;
- standalone deletion remaining represented;
- cross-repository patches remaining additive;
- exact allocation reconciliation and deterministic output;
- direct/co-author multi-match commits represented once in a shared contributor
  group;
- zero contributor/repository/head rows and no-unique-work head diagnostics;
- contributor-group and repository head-group low/expected/high reconciliation;
- author/committer date, interval, alias, co-authorship, merge, and interleaving
  policy; and
- manifest/report schema and customer-safety wording.

The process-level suite verifies an offline author-period command on a temporary
Git repository, immutable selected objects, deterministic stdout, exact
allocation, no source/host paths, and an unchanged worktree.

The first multi-repository author-period release freezes the following regression
matrix. The focused `change/1.3.0` process test covers the composed path; smaller
storage-independent tests retain precise policy and failure boundaries.

| Case | Frozen coverage |
| --- | --- |
| Fully overlapping heads | `AuthorPeriodManifestBenchmarkFreezesRegressionAndReuseMatrix` |
| Shared ancestry plus unique commits | `OneTopologicalWalkMapsSharedAndUniqueCommitsToEveryHead` and the process matrix |
| Default/open-head overlap | `AuthorPeriodManifestBenchmarkFreezesRegressionAndReuseMatrix` |
| Equal object-ID text in two repositories | `PortfolioCandidateBatchScopesEqualObjectsByRepositoryAndRunsOneAtATime` and the process matrix |
| Direct-author plus co-author multi-match | `OneCommitRetainsEveryContributorMatchWithoutMultiplication` and the process matrix |
| Zero contributor/repository/head rows | `MatchSetGroupsKeepSharedEffortOnceAndPreserveZeroRows` and the process matrix |
| Offset and daylight-saving boundaries | `OffsetFreeDaylightSavingGapsAndAmbiguitiesRequireExplicitOffsets` |
| Merge inclusion/exclusion parity | `CommitterDateMergeAndCoauthorPoliciesAreExplicit` |
| Repository/head/contributor/alias reorder invariance | `DigestIsInvariantToContributorRepositoryHeadAndAliasOrder` and the process matrix |
| Exact disjoint manual baseline at all range points | `MatchSetGroupsKeepSharedEffortOnceAndPreserveZeroRows` and the process matrix |
| No local paths, raw aliases, or source excerpts | `ReportSelectionContainsStableIdsAndObjectsButNoPathsOrAliases` and the process matrix |
| Missing object, cancellation, input limits, and safety caps | `AuthorPeriodManifestPreflightFailuresDoNotLeakPathsOrAliases`, `PortfolioCandidateCancellationDisposesRepositorySessionBeforeOpeningSnapshots`, `ManifestContractIsVersionedBoundedAndRejectsDuplicateIds`, and `CandidateLedgerRetainsTheTenThousandRecordSafetyBoundary` |

Timing and sampled-memory fields from the process matrix are observations. CI does
not pass or fail on them; it gates the semantic, privacy, reuse, boundedness, and
unchanged-target assertions above. The exact measurement protocol and controlled
before/after table live in `BENCHMARKS.md`.

Large Git portfolios additionally use the bounded changed-scope and immutable-
inventory reuse rules in `CHANGE_ESTIMATION.md`. Identity and time still select
rows only; neither the candidate count nor the size of the reachable graph enters
an effort rule.

`change-portfolio/0.2.1` adds one invocation-scoped execution context per local
repository. Candidate plans are grouped by canonical repository root and processed
one repository at a time, so cross-repository and per-repository analysis
concurrency are both explicitly one in this checkpoint. The repository context
owns one lazy `git cat-file --batch` reader, a 64-MiB blob cache that admits no
single blob above 1 MiB, 16 immutable snapshot inventories, 1,024 remembered first
parents, and a 16-entry snapshot-analysis LRU. Inventory derivation retains the
existing 1,024-changed-path and 16,000-path-character fallback boundaries. Each
context is disposed after its repository, including cancellation and failures.

Snapshot analysis remains keyed by repository, immutable object, and exact
analysis-scope digest. A broader portfolio scope is never substituted merely to
increase cache hits, so a row remains byte-equivalent to its independent canonical
Change estimate. Exact same-scope snapshots can be analyzed once even when other
workstreams intervene; different scopes remain separate where correctness
requires. Shared blob reads and immutable inventories still benefit those rows.

Report diagnostic `FB5325` records deterministic, privacy-safe request/hit counts
and retention bounds without paths, aliases, source, or timings. Manifest runs also
write nine wall-clock phases to stderr: manifest validation, head validation,
history union, selection, snapshot/diff construction, static analysis,
reconciliation, allocation, and rendering. Durations are operational telemetry,
not report-contract fields, deterministic identity, or effort inputs.

## Safety and limitations

- Ordinary `scan` and repository `estimate` remain history-free. Only an explicit
  author-period selector reads bounded identity, timestamp, parent, and co-author
  metadata.
- Exact patch and object-chain normalization do not provide general semantic-
  clone, rebase, squash, or conflict-resolution equivalence.
- Mechanically split work on distinct paths with no structural overlap stays
  additive. Filenames, timestamps, and contributor identity do not invent shared
  feature identity.
- Pair work, reviews, requirements, design, mentoring, incidents, debugging,
  coordination, and work committed under another identity cannot be recovered as
  individual credit from repository history.
- Cross-repository totals are additive after independent per-repository
  normalization. Similar-looking work in different repositories is never silently
  deduplicated.
- Portfolio aggregation does not widen Change model admission.
- Ranking, grading, performance-review scoring, and compensation workflows are
  deliberately unsupported.

Future evidence may justify reviewed public portfolio examples, broader semantic
equivalence, cross-platform/concurrent monorepository measurements, and universal
regression thresholds. Any extension must preserve the selector-only identity
boundary and explicit uncertainty.
