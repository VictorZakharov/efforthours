# Change portfolio reconciliation

## Current boundary

`change-portfolio/0.2.4` composes canonical Change estimates selected as repeated
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
eh change portfolio --author-period-manifest <manifest.json> --preflight
eh change portfolio <repository> --author <alias> [--author <alias> ...]
  --since <instant> --until <instant>
```

The manifest form can also produce one time-bucketed, multi-contributor comparison
without rerunning or adding independently rounded reports:

```text
eh change portfolio --author-period-manifest <manifest.json> \
  --bucket calendar-month \
  --capacity-manifest <capacity.json> \
  --format markdown \
  --output <trend.md>
```

`--preflight` is a read-only selection-only mode. It validates the manifest and
pinned local objects, performs the exact repository-scoped identity/time
selection, and stops before diff construction, snapshot loading, static analysis,
reconciliation, or EHE estimation. Its versioned JSON or Markdown report exposes
public repository/contributor IDs, exact counts (or an explicit lower bound when
a resource stops measurement), projected snapshot requests, deterministic
selection and analysis chunk counts, ledger charge, and fixed checkpoint/output/
queue/concurrency bounds. It recommends one normal run, one checkpointed time-
bucketed summary run, no analysis for an empty selection, or a stop on a named resource.
It never recommends splitting an interval into independently reconciled reports.
For example:

```text
eh change portfolio --author-period-manifest manifest.json --preflight \
  --format markdown --no-rate --output scope.md
```

Preflight does not predict wall time: repository tree, diff, and analyzer shape
can still dominate after selection. Its recommendation is a deterministic scope,
reviewability, and declared-resource decision rather than a machine-specific ETA.

`--bucket calendar-month` and `--bucket calendar-week` derive a gap-free partition
in the manifest timezone. `--bucket-manifest` instead accepts a versioned caller-
supplied partition whose first and last instants must equal the overall manifest
interval and whose buckets may neither overlap nor leave gaps. Every selected
commit follows the existing since-inclusive/until-exclusive timestamp policy and
is assigned to exactly one bucket. Buckets are an alternative decomposition of
the one jointly reconciled portfolio; they are not independent estimates and
never multiply EHE.

Derived calendar-month bucket IDs are `yyyy-MM`, for example `2026-07`. Derived
calendar-week IDs are `week-yyyy-MM-dd`, using the Monday start date in the
manifest timezone. Custom bucket IDs are copied exactly from the bucket manifest.
Capacity input must use those exact IDs. A mismatch identifies the missing and
unexpected public contributor/bucket cells rather than reporting only a generic
matrix error.

An optional `change-portfolio-capacity-manifest` supplies exactly one positive
reference-capacity value for every requested contributor/bucket cell plus a
caller-stated calendar policy. Capacity is a denominator only. It does not change
EHE and is not attendance, actual labor, productivity, authorship, compensation,
or a schedule prediction. The portfolio denominator is the exact sum of the
contributor denominators. Full-period ratios divide full-period EHE by full-period
capacity; they never average bucket ratios.

Comparison output requires an explicit `--output` path. JSON uses the versioned
`change-portfolio-comparison-report` contract. Markdown can select `--report-view
trend` for a publishable multi-period report or `--report-view findings` for a
generic, anonymized engineering run report. Both views come from the same
structured calculation. `--generated-at` permits a frozen generation instant for
reproducible artifacts.

`--normalization joint|isolated` chooses the contributor comparison view without
changing the source portfolio. `joint` is the default: its mutually exclusive
contributor-match-set series reconcile additively to the jointly normalized
portfolio, but allocations can change when manifest membership changes.
`isolated` instead emits one membership-stable canonical series for every
requested contributor. A shared commit appears in every contributor series that
matched it, so isolated contributor series can overlap, are deliberately
non-additive, and must never be summed into the authoritative jointly reconciled
portfolio total.

Comparison mode enables atomic repository-evidence checkpoints by default at
`<output>.eh-checkpoint`; `--checkpoint` selects another execution-only directory
and `--no-checkpoint` disables persistence. Each entry binds one repository's
pinned heads, the shared selection/contributor policy, profile, and estimator
identity. A rerun reuses an exact hit without replanning or reanalysis. Changing
one pinned head invalidates only that repository's evidence; bucket/capacity/view
changes reuse immutable repository evidence and deterministically recalculate the
cheap presentation cells.

Checkpoint protocol `repository-evidence-checkpoint/1.1.0` additionally binds the
privacy-safe measured repository scope. Each file is limited to 512 MiB, and
comparison execution records exact checkpoint bytes read and written. Versioned
comparison resources also record candidates, deterministic ledger charge,
selected changes, projected snapshot requests, selection/analysis chunks, and
actual snapshot-analysis requests, observed peak working set, declared queue/
concurrency bounds, and exact rendered bytes against the fixed 512-MiB output
bound. These operational fields do not change EHE or bucket semantics.

Because the default checkpoint name is derived from the exact output path,
changing the output filename also selects a different checkpoint directory. Use
an explicit stable `--checkpoint` path when several output filenames should reuse
the same immutable repository evidence.

Repository shards fail independently. Completed evidence is retained for resume,
the first substantive exception is sanitized and recorded with repository, phase,
optional bucket, digest, and true last-progress timestamp, and remaining
repositories continue. Any failure produces a nonzero exit plus an explicitly
`incomplete` JSON or Markdown artifact at the requested path. Incomplete artifacts
contain no source portfolio, additive series, aggregate EHE, or trend, so a failed
cell can never masquerade as zero or a complete comparison.

Repeated PR selectors use the optional `gh pr view` boundary only to resolve a
number/URL, immutable provider base-tip/head object IDs, provider changed-file
count, and execution-only acquisition coordinates. Objects must already exist in
the local Git database by default, where Git resolves the unique merge base as the
reviewed PR comparison base. Invocation-wide `--fetch-missing` explicitly permits
missing objects for repeated PRs or the PR manifest to be acquired with the same
source-only, no-ref-update boundary as single-PR mode. It is invalid for either
author-period selector.

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
digit, and are limited to 128 characters. The v1 execution budgets are 64
repositories, 32 heads per repository and 128 heads overall, 64 contributors, 16
aliases per contributor, and 128 aliases overall. Each repository has a
deterministic 128-MiB charged exact-candidate ledger and reports its scope in
logical 1,024-candidate chunks. Git may stream more lifetime identity-prefiltered metadata,
but out-of-window matches do not consume the ledger. A 100,000-candidate
per-repository count and the report contract's 640,000 selected-change envelope
remain final circuit breakers after the byte, cache, queue, checkpoint, and output
bounds; neither is an ordinary calendar or presentation limit. An
immutable head object may appear only once within one repository; equal object-ID
text in different repositories remains repository-scoped. The CLI reads at most
one MiB of strict UTF-8 manifest JSON.

Charge policy `author-period-candidate-ledger-charge/1.0.0` uses a conservative
512-byte entry charge, 32 bytes per retained string plus two bytes per character,
32 bytes per retained identity/collection, and eight bytes per collection
reference. It accounts deterministically for commit IDs, parent IDs, author and
committer identities, and parsed co-author identities. This is reproducible
retained-state accounting, not sampled CLR heap usage; the process remains
separately bounded by the fixed caches, queues, and concurrency ceilings.

The comparison report describes each repository session as an internal evidence
shard under `repository-evidence-shards/1.0.0`, but those shards remain one logical
portfolio. All candidates are globally composed before one reconciliation and
bucket allocation. Callers must not partition contributors or heads, join output
files, or add rounded totals. Inputs above 64 repositories remain outside the v1
envelope rather than silently changing semantic boundaries.

Semantic validation rejects duplicate IDs, aliases assigned to several
contributors, empty alias/head sets, repeated repository-local head objects,
non-canonical text, invalid object IDs, unsupported versions, reversed intervals,
and over-budget input. The execution adapter must additionally resolve paths
relative to the manifest, enforce a one-to-one repository-ID/root mapping, verify
every pinned commit locally, and reject missing or unsafe inputs before analysis.
It must not fetch, execute target code, or write into a target repository.

Canonical non-comparison manifest execution preflights every repository and pinned
commit before Change analysis. Comparison mode instead validates one internal
repository shard at a time so an earlier success can be checkpointed and a later
failure isolated without discarding completed evidence. In both modes, each
repository's pinned heads are passed together into Git's identity-prefiltered walk,
which forms a repository-scoped reachable union and returns each commit object
once. EffortHours streams those records, applies the exact structured identity and
selected timestamp policy record by record, and retains only in-window matches.
A separate topological walk propagates a compact head bitset through shared history
and stops once all selected commits have their complete head reachability. Its
frontier is bounded independently from the charged identity ledger, and the
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

Author-period selection streams exact in-window identity candidates into a
deterministically charged 128-MiB ledger per repository and records deterministic
logical 1,024-candidate chunk counts. Git may traverse a larger reachable graph and stream more
lifetime identity-prefiltered records without their consuming retained bytes.
Every exact match inside the requested interval is retained, including a
high-commit closed month; calculation is not split or rejected merely to shorten
the detailed ledger. A budget failure reports a lower-bound in-window count, the
observed charge, the exhausted byte or emergency-count resource, and privacy-safe
direct/co-author counts by requested contributor. It never emits raw aliases or a
truncated result. Successful selection reports the retained total, byte charge,
chunk count, and the same public contributor breakdown.
The exact selector provides:

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

The default joint contributor values are allocations from the jointly reconciled
repository portfolio, not membership-invariant personal estimates. Adding an
otherwise exclusive contributor can change those normalized allocations when the
new rows change path/category overlap or shared repository context. A zero shared-
contributor-group count means only that no commit matched several requested
identities; it does not mean repository reconciliation was independent. Each
group's `isolatedEffort` remains the stable sum of its canonical row estimates,
while `normalizedEffort`, `reconciliationDelta`, and `adjustmentIds` expose the
context-dependent allocation explicitly.

Comparison mode can project those canonical row estimates with `--normalization
isolated`. It emits one `contributor-isolated` series per requested contributor,
including deterministic zero rows. Its total and buckets sum `isolatedEffort` for
every canonical item that matched that contributor. This makes a contributor's
series invariant when unrelated contributors are added, but a shared item appears
in every matching contributor series. These series therefore do not reconcile to
the portfolio and have `additiveToPortfolio: false`. Joint repository
reconciliation remains the only portfolio total in both modes.

Non-additive summary rows expose direct-author/co-author counts, shared-match
counts, head reachability, uniquely reachable counts, and heads with no unique
selected commit. Zero rows are available when the overall manifest still selects
at least one commit; an entirely empty selection continues to return the existing
clear no-match error rather than inventing estimator metadata.

## Contracts and output

The v1 schema catalog includes:

- `change-author-period-manifest`;
- `change-portfolio-bucket-manifest`;
- `change-portfolio-capacity-manifest`;
- `change-portfolio-comparison-report`;
- `change-portfolio-manifest`;
- `change-portfolio-preflight-report`; and
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

Preflight JSON and Markdown are selection-only operational reports and contain no
EHE. Their 2,000-row threshold is review guidance only: above it, the recommended
time-bucketed Markdown summary may omit per-change detail rows, but the underlying
source portfolio still calculates and globally reconciles every selected change.
The full JSON contract remains available when downstream processing requires the
detailed ledger, subject to the declared rendered-output bound.

The comparison contract embeds the canonical source portfolio and adds canonical
bucket definitions, the selected contributor-normalization view, the one
authoritative portfolio series, optional capacity ratios, and trend statistics.
Portfolio validation uses bounded canonical item-ID indexes, so item-lineage
checks remain linear in row count rather than rescanning a large source ledger.
In joint mode, a requested contributor's additive series contains only its
exclusive exact-match group; multi-contributor matches stay in separate shared
series and are counted once. In isolated mode, each requested contributor gets a
membership-stable, non-additive canonical series and shared items may appear in
several series. The renderer never invents personal percentages. Trend statistics
use expected capacity ratios, OLS over bucket ordinal, and a fixed three-bucket
capacity-weighted rolling ratio.
Ratios are rounded deterministically to six decimal places and trend coefficients
to their documented fixed precision after exact EHE/capacity aggregation.

The trend Markdown includes the immutable input snapshot, partial-period notes,
portfolio and per-contributor Mermaid lines plus a labeled numeric fallback,
overall and contributor tables, a comparison matrix, shared-credit semantics,
calculation validation, and interpretation limits. Those limits explicitly state
that coverage includes only manifest repositories and objects reachable from the
pinned local heads; omitted repositories and unavailable work are invisible. The
findings Markdown includes version/environment boundaries, repository
outcomes, preserved structured failures when available, phase/progress and
resource observations, reuse/data-volume counters, a sanitized command shape,
checkpoint dispositions, repository wall-time baselines, confirmed invariants,
and data-handling notes. It reports only structured facts and does not guess a
cause the analyzer did not establish.

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

The process-level suite verifies offline direct and manifest author-period
commands on temporary Git repositories, immutable selected objects, deterministic
stdout, exact allocation, live phase progress, no source/host paths, and unchanged
worktrees. Separate physical PR acquisition coverage freezes default no-fetch
failure, narrow explicit acquisition, exact-object verification, and unchanged
refs, `FETCH_HEAD`, index, and worktree state.

The first multi-repository author-period release freezes the following regression
matrix. The focused `change/1.3.0` process test covers the composed path; smaller
storage-independent tests retain precise policy and failure boundaries.

| Case | Frozen coverage |
| --- | --- |
| Fully overlapping heads | `AuthorPeriodManifestBenchmarkFreezesRegressionAndReuseMatrix` |
| Shared ancestry plus unique commits | `OneTopologicalWalkMapsSharedAndUniqueCommitsToEveryHead` and the process matrix |
| Default/open-head overlap | `AuthorPeriodManifestBenchmarkFreezesRegressionAndReuseMatrix` |
| Equal object-ID text in two repositories | `PortfolioCandidateBatchScopesEqualObjectsAndBoundsRepositoryConcurrency` and the process matrix |
| Direct-author plus co-author multi-match | `OneCommitRetainsEveryContributorMatchWithoutMultiplication` and the process matrix |
| Zero contributor/repository/head rows | `MatchSetGroupsKeepSharedEffortOnceAndPreserveZeroRows` and the process matrix |
| Offset and daylight-saving boundaries | `OffsetFreeDaylightSavingGapsAndAmbiguitiesRequireExplicitOffsets` |
| Merge inclusion/exclusion parity | `CommitterDateMergeAndCoauthorPoliciesAreExplicit` |
| Repository/head/contributor/alias reorder invariance | `DigestIsInvariantToContributorRepositoryHeadAndAliasOrder` and the process matrix |
| Exact disjoint manual baseline at all range points | `MatchSetGroupsKeepSharedEffortOnceAndPreserveZeroRows` and the process matrix |
| No local paths, raw aliases, or source excerpts | `ReportSelectionContainsStableIdsAndObjectsButNoPathsOrAliases` and the process matrix |
| High-commit closed month | `DefaultBudgetRetainsMoreThanTenThousandExactCandidatesInChunks`, `MoreThanTenThousandChangesReconcileAndRenderOneDeterministicSummary`, and the explicit v1.5.0 benchmark mode |
| Lifetime identity matches outside interval do not consume the ledger | `OutOfWindowLifetimeMatchesDoNotConsumeTheCandidateLimit` |
| Exact resource failure and privacy-safe contributor breakdown | `EmergencyCircuitBreakerReportsObservedCountAndPublicContributorBreakdown` and `CandidateLedgerStopsAtTheDeclaredByteBudgetWithoutTruncation` |
| Agent-readable selection scope without EHE analysis | `ManifestPreflightMeasuresSelectionWithoutSnapshotAnalysis` and `AuthorManifestPreflightRecommendsOneCheckpointedSummaryWithoutAnalyzing` |
| Stable isolated contributor series and non-additive shared matches | `IsolatedContributorSeriesStayStableAcrossManifestMembership` |
| Sibling repository paths | `AuthorPeriodManifestAcceptsSiblingRepositoryFromManifestInsideWorktree` |
| Missing object, cancellation, input limits, and safety caps | `AuthorPeriodManifestPreflightFailuresDoNotLeakPathsOrAliases`, `PortfolioCandidateCancellationDisposesRepositorySessionBeforeOpeningSnapshots`, `CancelledManifestCommandEmitsPrivacySafeLastPhaseProgress`, `ManifestContractIsVersionedBoundedAndRejectsDuplicateIds`, and `CandidateLedgerStopsAtTheDeclaredByteBudgetWithoutTruncation` |

Timing and sampled-memory fields from the process matrix are observations. CI does
not pass or fail on them; it gates the semantic, privacy, reuse, boundedness, and
unchanged-target assertions above. The exact measurement protocol and controlled
before/after table live in `BENCHMARKS.md`.

Git portfolios additionally use the bounded changed-scope and immutable-
inventory reuse rules in `CHANGE_ESTIMATION.md`. Identity and time still select
rows only; neither the candidate count nor the size of the reachable graph enters
an effort rule.

`change-portfolio/0.2.4` keeps one invocation-scoped execution context per local
repository. Candidate plans are grouped by canonical repository root and
scheduled with at most two repository sessions active at once. Within each
repository, a deterministic 16-row delta-prime chunk feeds one ordered snapshot
producer and four row consumers, allowing the next immutable snapshot pair to be
opened while earlier pairs are analyzed. Common scanning also enqueues discovered
paths while bounded workers read and inspect earlier files; a process-wide read
budget bounds buffered content across snapshots. All repository sessions share
separate process-wide budgets: at most eight Git tree readers for object-store I/O
and at most 24 visible logical processors for common file inspection,
.NET/JavaScript semantic parsing, and thread-safe seed estimation; unmarked custom
estimators remain serialized. The two work classes may overlap through the bounded
snapshot pipeline, so an I/O wait does not reserve a managed CPU slot. Results are
restored to canonical path order before aggregation, and immutable file/snapshot
requests are single-flight. The repository context owns one lazy
`git cat-file --batch` content reader, one lazy `git cat-file --batch-check`
metadata reader, a 64-MiB blob cache that admits no single blob above 1 MiB,
16,384 retained object lengths, 10,000 structurally shared immutable snapshot
inventories across at most 16 full-tree root lineages, 10,000 remembered first
parents, a 16-entry snapshot-analysis LRU, and an 8,192-entry immutable file-
analysis artifact cache with deterministic key-ranked retention. The artifact
cache retains only analyzer-versioned, content-addressed common scanned-file
facts and .NET/JavaScript per-file results; source text, keys, and local paths
never enter a report. Its entry bound permits an intentional
memory-for-latency tradeoff without making memory unbounded. A separate C#
first-parent lineage cache retains at most eight states and 16 MiB of decoded
source text per repository. It can reuse exact evidence when a scope is unchanged
or when one unique maintained C# body has a syntax-clean, same-size numeric-token
edit; every structural or ambiguous case uses full analysis. Inventory derivation
retains the existing 1,024-changed-path and 16,000-path-character fallback
boundaries. Before row analysis, eligible non-merge first-parent deltas and changed
blob sizes are read with one `diff-tree --stdin` and one `cat-file --batch-check`
process per repository rather than two Git processes per selected change. The
diff batch always emits one explicit commit frame, including a zero-path frame for
a valid empty commit; absence of that frame remains a missing-object failure. Each
batch output is capped at 64 MiB; exceeding it uses the existing row fallback.
Roots, merges, custom snapshot providers, oversized deltas, and missing cached
parents retain the exact existing fallback. Full-tree enumeration asks `ls-tree`
only for path, mode, and immutable object identity; unchanged blob lengths are
resolved only when admitted analysis requests them. The object-storage layout is
read once per repository session. Packed stores and stores with fewer than 1,024
loose objects use one exact recursive reader. Larger loose stores with at least
128 disjoint shallow tree paths are deterministically partitioned across at most
four recursive readers per tree and eight readers process-wide; smaller or
command-line-oversized shapes retain the exact single-reader fallback. Cached
inventories retain
their persistent content index, canonical Merkle source digest, object-ID set,
and already-read first-parent diff so repeated scopes and Change evidence do not
rebuild complete tree maps. Each context is disposed after its repository,
including cancellation and failures.
Optional parent-derived snapshot and C# evidence is reused only after that parent
analysis has completed; optional reuse never waits on another in-flight lineage.
Within each row the base analysis precedes the head analysis, retaining direct
base-to-head reuse while preventing overlapping row lineages from forming a
circular wait. A selected row whose base is an earlier queued row's immutable head
waits for that earlier row to complete, preserving chronological lineage reuse
without waiting on recursively discovered in-flight ancestors. A producer or
consumer failure cancels further preparation, drains unclaimed snapshot pairs,
and reports the first substantive failure rather than a secondary cancellation or
channel-closure exception.
The two-session maximum deliberately spends bounded additional memory to overlap
independent Git/tree work and reduce wall time; it does not make caches or
repository concurrency unbounded.

The CLI and Change benchmark executables request .NET server garbage collection.
Closed-month portfolios retain many immutable analysis artifacts and allocate
heavily enough that parallel collection materially reduces elapsed time. This is
a bounded memory-for-latency choice, not permission to expand the repository,
cache, read-buffer, or queue limits above. It does not change report semantics.

This scheduling contract removes avoidable phase barriers; it does not promise
near-linear core scaling. Protocol `change/1.11.0` retains the storage-aware
heterogeneous scheduler and diagnostics from 1.10.0 and adds the bounded exact
evidence-lineage optimization above. On the 1.10.0
prepared loose-object fixture, tree-read elapsed improves from 0.697 to 0.368
seconds and whole-command wall time from 3.305 to 2.907 seconds at 12 requested
workers, with an unchanged semantic digest. One to eight active tree readers
improves that isolated path by 3.28x; the requested 12-worker result is only 3.25x
and whole-command one-to-twelve speedup is 1.10x. That approximately three-second
fixture diagnoses tree scheduling but is too short to establish whole-command
core scaling.

The longer prepared checkpoint contains two repositories, eight heads, 512
selected changes, 1,024 snapshot requests, and 210,147,148 unique blob bytes.
Across three fresh-process measurements per point, server-GC median wall time is
16.744 seconds at one admitted worker, 12.799 at two, 11.943 at four, 11.464 at
six, 11.399 at eight, and 11.540 at twelve. The best measured speedup is therefore
1.47x at eight workers, followed by a plateau. At twelve workers, enabling server
GC lowers the workstation-GC median from 17.374 to 11.540 seconds while median
sampled peak working set rises from 608.92 to 754.63 MiB. Every run retains the
same estimate-semantic digest. A controlled attempt to widen the fixed four row
consumers per repository to six made both time and memory worse, so the fixed
bound remains four. General logarithmic core scaling is unresolved. Reaching a
configured maximum proves only admission; performance claims use repeated CPU and
wall observations, and CI never gates on those timings.

On that same longer fixture at eight workers, three fresh 1.11.0 processes have a
6.279-second median versus the 11.399-second 1.10.0 median (`1.82x` faster).
Full repository-estimate invocations fall from 516 to 13 and immutable analysis-
artifact requests from 42,828 to 1,079. Managed allocation falls 49.8% and sampled
peak working set falls 19.5%. All new runs have identical report bytes, and the
estimate-semantic digest is unchanged across protocols. The fixture deliberately
changes one same-size numeric literal per commit; these numbers establish that
specific lineage optimization, not general repository shapes, field latency, or
core scaling. Issue #182 therefore remains open.

Snapshot analysis is keyed by repository, canonical immutable-inventory digest,
and exact analysis-scope path-set digest. Reused evidence is rebound to the
requesting row's changed/context/representative and full-inventory counts before
the Change report is built, so metadata-distinct rows can reuse the same scan
without reusing one another's diagnostics. Inventory identity is a versioned
SHA-256 Merkle tree over path, mode, and blob object identity, independent of delta
application order. A broader portfolio scope is never substituted merely to
increase cache hits, so a row remains byte-equivalent to its independent canonical
Change estimate. Equal trees with the same scope can be analyzed once even when
reached through different commits or intervening workstreams; different scopes
remain separate where correctness requires. Shared blob reads and structurally
shared inventories still benefit those rows.

Report diagnostic `FB5325` records deterministic, privacy-safe request/hit,
unique-key, revisit-miss, byte, eviction, retention, and batched-inventory counts
for snapshot, inventory, file-analysis-artifact, Git-blob, and object-metadata
reuse without paths, aliases, source, or timings. Direct and
manifest author-period runs announce each active phase on stderr before it begins,
emit processed/total and cache counters every 16 estimated rows, then write
elapsed phase summaries after successful output. Cancellation emits the last
phase, elapsed time, processed/remaining units, cache counters, current working
set, and highest observed working set without paths or aliases. Direct runs report
head validation, history union, selection, snapshot/diff construction, static
analysis, reconciliation, and rendering. Manifest runs additionally report
manifest validation and contributor/head allocation. Durations and sampled memory
are operational telemetry, not effort inputs or ordinary CI gates. The canonical
`change-portfolio-report` continues to exclude them. A comparison report
deliberately includes them in its execution section so a saved findings report can
identify a stalled phase, while its `verification.semanticDigest` excludes all
timings and resource samples. Aggregate phase time can exceed wall time when two
repository sessions overlap.

Manifest paths resolve against the manifest directory and may point to any local
Git repository the process can read, including a sibling. Validation opens the
specified repository rather than imposing containment beneath another worktree.
Unknown unreadable-path failures remain sanitized, so an operating-system,
sandbox, or process-permission denial does not disclose the rejected local path or
raw Git stderr. Known Git categories retain a privacy-safe actionable cause. In
particular, dubious ownership reports identify Git's `safe.directory` rejection
without copying the path; automation can admit only the intended repositories for
one process with `GIT_CONFIG_COUNT`, `GIT_CONFIG_KEY_n=safe.directory`, and matching
`GIT_CONFIG_VALUE_n` variables instead of changing global Git configuration.

Author-date selection intentionally does not pass a date cutoff to Git history
traversal. Git revision date pruning uses the commit/committer timestamp, while an
author timestamp may differ arbitrarily and need not be monotonic through the
graph. Applying that cutoff to `--date-field author` could silently omit valid
commits. EffortHours instead streams Git's identity-prefiltered metadata and
applies the exact selected timestamp, timezone, merge, and co-author rules locally
before retaining the bounded in-window ledger. A separate committer-only traversal
shortcut remains deferred until measurements justify the extra policy path.

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
equivalence, cross-platform concurrency repetition, larger public monorepository
shapes, and universal regression thresholds. Any extension must preserve the
selector-only identity boundary and explicit uncertainty.
