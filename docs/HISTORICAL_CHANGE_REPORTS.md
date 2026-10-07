# Retained historical Change EHE

One contributor can select an exact historical interval in one invocation:

```text
eh change period --owner example --author developer@example.invalid \
  --provider-login developer --since 2026-01-01 --until 2026-02-01 \
  --timezone America/Toronto --scope engineering --capacity-hours-per-day 8 \
  --breakdown day --format json --output historical.json --no-rate
```

Start is inclusive and end is exclusive. Offset-free instants use the named
timezone, including its existing ambiguous/skipped-time checks. The interval must
end no later than the frozen report instant. `--generated-at` freezes report
generation; it is no longer needed to select a past interval. Named periods can
opt into the same discovery with `--include-history-prs`. Explicit native ranges
enable it automatically. This is an explicitly provider-backed command; local
manifest and ordinary Change commands remain offline under their existing rules.

## Coverage

Historical discovery probes each visible, scope-admitted default branch once for
its immutable head, acquires missing history into the locked managed bare Git
cache, and applies exact identity and author/committer selection locally. It no
longer downloads the complete default history through paginated provider JSON.
The probe has no date or author cutoff, and the cached Git traversal has no
committer-date pruning. Inactive repositories can acquire source objects during
this selection phase; no static/snapshot analysis runs for them. The initial fetch
can be expensive; subsequent queries reuse the immutable local object store.
This phase accepts at most 256 scope-admitted repositories before acquisition;
larger inventories fail with a scope/explicit-manifest remedy rather than truncate.
A discovery attempt also bounds provider adapter requests at 2,048. This avoids rejecting January author dates because
the commit was rebased or committed in March. Historical discovery also paginates
authored open, closed, and merged PR inventories through the selected account
connection, with the existing complete per-repository fallback. PR creation,
update, merge, and closure dates never prune this inventory. Identity resolution
remains explicit and fail-closed; `--provider-login` selects the account without
expanding Git aliases.

PR commit counts and pinned retained heads must match the returned inventory.
Incomplete pagination, unavailable objects, changed heads, provider errors, and
resource limits produce nonzero incomplete artifacts without aggregate EHE. The
[GitHub PR-commits endpoint](https://docs.github.com/en/rest/pulls/pulls#list-commits-on-a-pull-request)
limits its inventory to 250 commits; count mismatch
fails rather than certifying a truncated result. Provider responses retain the
existing 16-MiB character bound and four-call concurrency ceiling. Acquisition
uses the existing locked managed bare cache and immutable source-only fetches.
The global 512-head envelope now also applies within one repository. Acquisition
retains 32-ref/32-tip fetch batches, and reachability retains a 128-MiB membership
ledger plus the existing traversal/frontier bounds. Candidate ledger, queue,
checkpoint and output bounds remain enforced. A complete report describes only currently available provider
objects, not complete original development history.

Closed/merged candidate and selected-head counts are separate optional discovery
fields. Coincident default/PR heads with the exact same immutable object are coalesced
once, preferring the default ID and otherwise stable head-ID order. This preserves
the complete reachable graph and avoids invalid duplicate-object manifests; PR
inventory counts remain separate from selected distinct-head counts.
Default branches and authored retained PR heads enter one repository-local
Git union and one jointly reconciled calculation. Other non-default branches,
PRs authored by another account, deleted PRs, force-pushed-away intermediate
commits, and work crossing the selected interval remain outside this boundary.
Engineering path admission is identical to the native today/named-period path.
Explicit `--author-period-manifest --scope engineering` now applies the same
versioned profile and repository overrides without requiring provider discovery.

## Exact retained equivalence

Portfolio `change-portfolio/0.5.0` adds two conservative proofs without changing
the source Change estimator or priors:

- An exact represented patch repeated on disjoint pinned head sets is counted
  once. The earlier retained timestamp, then stable item ID, chooses the keeper.
  Reintroductions on a shared reachable head are not suppressed by this rule.
- A connected sequence of selected non-merge commits can prove the same complete
  represented endpoint delta as a standalone squash representation. Every touched
  path must connect through matching immutable states, and the canonical net
  path/base/head digest must equal the standalone patch digest. Selected ancestor
  relationships and disjoint head reachability prevent treating a later
  reintroduction as a rewritten squash, even when a revert is not selected.
  Without head-reachability metadata, the standalone and earliest component must
  share the exact base commit in addition to the endpoint proof.

The component chain remains represented; the standalone equivalent receives zero
normalized allocation and retains its isolated estimate plus
`exact-retained-composition/1.0.0` lineage with ordered component item IDs and the
matching patch digest. Exact repeated patches retain `duplicateOfItemId`.
Adjustments include every contributing representation. These optional v1 fields
preserve older reports. Repository boundaries remain strict: identical changes
in separate repositories are not deduplicated.

Proofs retain at most 64 distinct head sets per patch and examine at most 256
members and 16,384 paths per composition, with one million composition steps per
repository. Unproven or over-bound cases stay in ordinary structural overlap
reconciliation with its uncertainty; they are never discarded by a similarity
guess. Missing selected intermediate commits, conflicting states, conflict
resolutions, partial squash matches, semantic clones, and altered rewrites have
no general equivalence proof. New follow-up work is retained. Functional and
quality changes introduced while resolving a conflict contribute represented
replacement effort under the normal estimator and therefore affect EHE/capacity
ratios. Conflict size, time spent, or the occurrence of a conflict never adds an
activity surcharge. Real Git rebase fixtures cover small altered resolutions,
additional resolution behavior, and a subsequent independent follow-up.

Historical contributor series use **joint** allocation; `--normalization isolated`
is rejected for this path because summing canonical representations can repeat
the same work. Existing named-period/team isolated semantics remain unchanged.
Full-range joint normalization is authoritative; separate monthly/day runs need
not allocate identically. Capacity stays independent at the caller's reference
hours per calendar day, including blank dates.

## Daily evidence

Complete historical daily reports expose one `nativePeriod.dailyEvidence` cell
per bucket:

| State | Meaning |
| --- | --- |
| `measured-retained-change` | Selected retained changes receive positive joint allocation on their declared timestamp date. |
| `no-retained-change` | No qualifying retained commit was selected for the date; original daily history and labor are unknown. |
| `scope-excluded` | Selected commits contain no admitted engineering delta paths. |
| `normalized-zero` | Admitted paths are formatting-only, generated, or otherwise unrepresented. |
| `reconciled-zero` | Selected represented changes receive zero after overlap, exact equivalence, or revert normalization. |

Measured means retained timestamp attribution, not recovered original workdays.
A blank date is never certified as a no-labor day. January 20-23 are not filled
merely because a retained January 19 commit represents several days of work.
Failed calculations have no daily evidence cells or zero-value aggregate.
Checkpoint/cache observations stay in execution metadata rather than semantic
identity, so exact cold/warm runs preserve the same result digest.

[WORKDAY_ALLOCATION.md](WORKDAY_ALLOCATION.md) now defines a separate explicit
aggregate projection across externally declared workdays. It conserves the
complete deduplicated EHE and reference capacity and labels every result allocated.
[WORKDAY_REVIEW.md](WORKDAY_REVIEW.md) now adds explicit record discrepancies and
optional conserved two-decimal entry contributions with a fixed eight-hour
reference. Downstream time-entry mutation remains outside this tool's contract.
EHE remains experimental replacement effort, not historical labor, productivity,
individual credit, compensation, or a recovered timesheet.

Portfolio 0.6.0 additionally re-estimates a proven complete selected endpoint as
specified in `CHANGE_PORTFOLIOS.md`. A single retained branch can pass that proof
while a combined conflicting-head selection fails it. Such an ambiguous combined
selection retains conservative structural maxima and explicit uncertainty; equality
with the single-branch endpoint is not promised. Exact rewrite/composition
suppression remains intact, and no unproven conflict resolution is discarded.


Failed provider setup now preserves observed query/page/process counts, startup
and elapsed time, metadata-cache state, and repository request observations keyed
only by opaque digests. A repository observation identifies phase, request/page
counts and complete/incomplete response state; it exposes no owner, repository
name, aliases, path or source. Partial observations do not make discovery complete,
and no failure publishes aggregate EHE or daily zero cells. Operational observations
remain outside the semantic digest.

Explicit immutable rewrite-event pairing and resolution-only period attribution
are governed by [Rewrite event attribution](REWRITE_EVENT_ATTRIBUTION.md).
Native retained-history reports emit `FB5341`: a complete current inventory cannot
recover discarded pre-rewrite objects or actual resolution dates. A caller can
provide frozen older objects and an explicit event through the offline manifest;
missing objects produce incomplete evidence, not invented historical commits.


Selected non-viewer GitHub logins can require provider-linked email aliases before
exact local Git selection. Identity bootstrap uses the selected account's
`author`-filtered immutable-head metadata without date pruning; that filter learns
aliases only and never prunes the local author/coauthor change selection. A private
`historical-identity-cache/1.0.0` entry in each managed repository is bound to viewer,
selected login and immutable head, expires after 24 hours, charges at most 16 KiB,
and retains at most 16 aliases under the existing alias bound. It caches identity
associations, not EHE or source excerpts. A changed head/account forces a fresh
bounded association lookup. Warm exact-head queries retain the one default-head
probe and reuse local history without repeating this association inventory. All
repository associations finish before local selection; every repository uses the
same completed alias set regardless of task scheduling.


The alias lookup uses GitHub's documented
[immutable SHA and author parameters](https://docs.github.com/en/rest/commits/commits#list-commits).
It reads explicit 100-row pages, charges their cumulative response characters to
16 MiB, and admits each page through the process-wide request ceiling. A full
100-row final page requires an additional empty page to prove completion. An
interrupted or budget-exhausted association inventory is not cached as complete.

## Bounded acquisition and explicit coverage restriction

Native provider reports accept repeated `--repository <owner/name>` restrictions.
Every repository must be unique, belong to `--owner`, and appear in the complete
accessible inventory. An absent requested repository fails rather than producing
a zero. Restrictions are applied before per-repository head queries, identity
bootstrap, Git acquisition, and estimation. Reports record policy
`explicit-repositories/1.0.0`, the canonical restriction digest, requested count,
and excluded accessible-repository count. They certify only this restricted
coverage, not the whole owner. Raw owner/repository names remain execution-only.

```text
eh change period --owner example --repository example/project --author selected --provider-login selected --since 2026-01-19 --until 2026-01-24 --breakdown day --timezone America/Toronto --scope engineering --capacity-hours-per-day 8 --discovery-timeout-seconds 900 --max-acquired-mib 4096 --no-rate --output period.json
```

This is the explicit bounded fallback when broad historical coverage is too
expensive. PR creation/merge dates and committer-date cutoffs are still unsafe
pruning rules for older author dates. Without an explicit restriction all
scope-admitted default histories remain required; no relevance guess silently
omits them. There is no promise of shallow or blob-filtered acquisition: exact
local selection and later canonical analysis may require ancestor/source objects.

Historical default acquisition now overlaps at most two repositories. Native
discovery plus acquisition has a default 900-second deadline (configurable 1 to
86,400 seconds) and a default 4,096-MiB observed object-store growth budget
(configurable 1 to 16,384 MiB). `--discovery-timeout-seconds` and
`--max-acquired-mib` are operational controls, never effort modifiers. The
`native-acquisition-budget/1.0.0` summary records effective limits, visited caches,
cache-hit head requests, acquired objects, and observed growth, including histories
that ultimately select no in-window work. Existing selected-head discovery counts
remain separate. Immutable warm cache reuse does not charge pre-existing objects.

Cache growth is measured under the per-repository lock, including incoming Git
pack files, at five-second progress ticks and completion. It is an observed-store
guard, not a hard wire-byte cap; in-flight writes can overshoot between ticks.
No cache deletion or destructive rollback is attempted. Stderr shows an opaque
repository ID, acquisition reason, head count, running/completed/cache-reuse state,
and observed growth while fetching. Native Markdown also states limits and
restricted coverage. Provider request/page/process counts, phase timings, and
failure summaries remain in JSON; operational metadata does not affect the
semantic EHE digest.

Deadline, growth-budget, authentication, object, and pagination failures produce
nonzero incomplete reports without aggregate EHE. The original fetch failure is
preserved when sibling cancellation follows it. User cancellation remains exit
130. Child fetches are canceled and drained before cache locks are released;
completed immutable objects remain reusable. Existing provider/selection/cache/
checkpoint/output bounds still apply. Synthetic cold/warm and cancellation tests
verify these rules; a field latency claim still needs the same-input consumer run.

For lost workdays, [explicit external workday allocation](WORKDAY_ALLOCATION.md)
provides a separate, labeled, conserved opt-in projection. It does not recover
missing Git history or turn retained timestamps into proof of actual work dates.

## Historical PR request plan and metadata reuse

The native retained-history path refreshes a complete live PR inventory with head
object, upstream-base object and commit count. For one resolved contributor with
explicit repositories, a bounded count probe chooses the smaller complete authored
account or included-repository traversal. Counts choose a request plan, never
selection membership; the selected traversal must prove complete live coverage.
It discards
repositories outside the already restricted, admitted owner scope before expensive
PR reads. Uncached PR evidence is requested in repository-local GraphQL batches
of at most 12 PRs, 100 commits per PR and two parents per commit, with at most four
concurrent batch/fallback readers process-wide, including per-repository inventory
fallbacks. Each batch response is parsed once. No PR creation, update, closure,
merge, author or committer cutoff
prunes this inventory. Exact identity/date matching is reapplied to every complete
metadata result; local immutable Git remains authoritative for estimation.

Only independently complete aliases are accepted from a batch. GraphQL errors,
missing fields, incomplete commit/parent connections or unavailable batch support
send the affected PR through complete REST detail/commit pagination. A changed
head, upstream base or count fails closed rather than silently changing the frozen
selection. The existing 250-commit complete REST inventory limit remains explicit.
An incomplete account connection uses the complete per-repository inventory and
the same batched evidence reader. Individual REST calls remain necessary only
for incomplete or unsupported batch evidence.
The per-repository fallback admits at most 1,000 authored candidates, not a
truncated sample. Head, response, request, deadline and acquisition bounds remain.

Private `github-pull-commit-metadata/1.0.0` sidecars retain only parsed identities,
author/committer dates, parents and coauthors, without full commit messages or
source excerpts. Reuse requires the same live viewer, repository, PR, head, base
and count, supported protocol, content digest and freshness within 24 hours.
Live PR inventory is always refreshed; advancing the base invalidates reuse even
when the PR head and commit count stay unchanged. Entries use atomic replacement,
charge at most 64 KiB each, and retain at most 1,000 entries (64 MiB total) across
the configured provider-cache root. Oversized evidence is valid but is not cached;
invalid, expired or unavailable cache entries refresh from the provider. Completed
evidence survives a sibling failure. Cache reuse never stores or changes EHE.

Optional `providerDiagnostics.historicalPullRequests` exposes candidate, cache-hit,
batch, complete-REST fallback, completed, selected and pending PR counts. New v1
observations additionally carry `inventoryStrategy`, `inventoryComplete`,
`metadataComplete`, inventory/header/metadata query counts, header batch/fallback
counts, successful `cacheWriteCount` and `resumeState`. All new fields are optional
for older v1 reports. Complete discovery requires complete inventory and metadata;
an interrupted inventory does not claim that its observed prefix is the candidate
universe. `completedCount` means complete metadata plus exact selection, rather than
merely a successful adapter response. Header requests cannot inflate commit batch
counts. Queued work checks cancellation before sending or accounting a request;
requests rejected by the attempt ceiling increment neither query nor
batch/fallback-request counters. `cacheWriteCount` acknowledges atomic writes, not indefinite retention:
24-hour freshness, eviction, oversized entries and unavailable storage still apply.
Selected
is the count of matching PR representations before identical-head coalescing,
not a selected-change count. The native manifest/planner retains the actual distinct
heads and selected-change counts. `lastRequest` identifies a fixed operation,
subphase, opaque repository digest when available, completed/incomplete state,
returned page count and one adapter-request duration. It retains the latest
incomplete request when present, otherwise the latest completed request; completed
siblings cannot obscure failure context while cancellation drains them. No aliases, PR numbers or source
paths are serialized. All these optional v1 fields are operational only.

Repository observation `elapsedMilliseconds` retains its prior meaning: a sum of
adapter-request elapsed times, including overlapping calls. New observations label
it `elapsedKind: cumulative-request` and add `wallElapsedMilliseconds`, measured
from that repository/subphase's first request start to last request completion.
Execution phase timings sum measured work, including overlapping calls and
repository shards. New rows label `elapsedKind: cumulative-work`; older rows
retain the same numeric meaning without that optional label. End-to-end and
per-repository elapsed fields are wall-clock observations. Cumulative startup is
separate.
Adapter requests include child startup, provider transport and response handling;
GitHub transport time is not independently measured. Query count counts adapter
invocations, page count counts returned pages, and process/startup counts record
completed child invocations with a returned process receipt, so interrupted-run
process/startup totals can be lower than actual launches. An interrupted
paginated child has no trustworthy partial-page count. Cumulative durations may
exceed wall time and must not be displayed as total runtime.

Deadline failures now use the interrupted provider subphase, while an acquisition
failure still uses managed-cache acquisition. Historical phases now distinguish
`historical-pr-discovery` (the parent workflow), `historical-pr-inventory`,
`historical-pr-headers`, `historical-pr-metadata` and `historical-pr-selection`
(including cache reads/writes). `lastRequest` distinguishes `pull-inventory-probe`,
`pull-inventory`, `pull-header-batch` and `pull-metadata-batch`; a complete last
request can coexist with interrupted cache/selection work. Request observations
retain up to four phase rows per admitted repository (1,024 overall), replacing the
older three-row 768 limit solely to represent these additional fixed diagnostics.
No adapter, head, cache or acquisition limit increases.

A historical PR timeout emits `resume-same-scope-or-use-pinned-manifest` when
completed metadata was reused or saved, including across multiple repositories.
Without reusable metadata, a single-repository PR timeout emits
`inspect-pr-discovery-or-use-pinned-manifest`, always with zero automatic retries.
Resume the exact repository, author, interval and cache/checkpoint scope: completed
immutable objects and still-valid sidecars are reusable, unfinished/stale metadata
is fetched, and complete live inventory is refreshed. There is no cached inventory
cursor/membership authority, automatic retry or date-based pruning. A complete
pinned manifest is a separate explicit coverage declaration. No timeout emits an EHE
aggregate, and provider/fetch children are canceled and drained before returning.

The synthetic 258-PR request checkpoint and explicit latency simulation are
recorded in [HISTORICAL_PR_DISCOVERY_BENCHMARK.md](HISTORICAL_PR_DISCOVERY_BENCHMARK.md).
A native retained-chain/squash Git fixture verifies discovery without manual head
input, one-time exact reconciliation and offline engineering parity. Existing
actual conflicting-rebase and declared event-only/original-only tests remain the
causal attribution boundary. Native discovery cannot reconstruct missing pre-rewrite
objects, mapping intent, event dates or discarded workdays. Use the existing
explicit rewrite-event and workday-allocation policies for those declarations;
plain author-date reports do not infer a later workday from a changed commit ID.
The NDA field reproduction still requires a same-input consumer retest.

Single-repository acquisition deadline/byte failures now use
`inspect-acquisition-or-use-pinned-manifest` with zero automatic retries. The
byte-budget message identifies the already restricted scope and permits only
inspection, an explicit larger bound or a complete pinned manifest; it does not
ask the caller to narrow that one repository again. Failure tests retain exact
provider receipt counts, active acquisition phase, no aggregates and completed
immutable objects for a subsequent reuse. The request checkpoint additionally
covers cold/warm annual PR discovery across 16 admitted repositories, separately
from the original restricted five-day case. Its omitted acquisition/estimation
phases and simulated latency remain explicit.

## Explicit scoped historical inventory

For a single resolved contributor and explicit repository restrictions, a cheap
GraphQL census compares the authored account's page count with the sum of admitted
repository page counts. Census groups contain at most 12 repositories and share
the four-reader gate. If all totals are available and consistent and the account
has strictly fewer pages, traverse its authored connection; otherwise traverse the
admitted repository connections. Both use explicit 100-row pages, unchanged totals,
unique identities/cursors and terminal-page proofs. Count probes, live inventories,
header and metadata requests share the unchanged 2,048-attempt request ceiling.
Out-of-scope account PRs are discarded before expensive reads; scoped pages discard
unrelated and null/deleted authors before retained candidate admission. The
1,000-candidate limit applies to matching authored PRs, not the repository's whole
population. Complete `totalCount`, unchanged totals, unique PR numbers and cursors,
and terminal-page checks remain mandatory. No creation/update/merge timestamp
prunes authored commits retained on an immutable head.

An unavailable/inconsistent account connection falls back to included repository
connections. Only an unavailable/inconsistent repository connection falls back to
that repository's explicit REST pages of 100 rows, sorted by creation ascending, with `gh --jq` projecting only number, state, author
login and immutable head. It never uses `--paginate --slurp` for this inventory.
An exactly full last page requires an additional empty response; repeated numbers,
malformed pages and changed frozen heads fail without partial selection. No
partial inventory is admitted or cached as complete. An unavailable repository
does not discard completed sibling inventories. Ordinary today selection remains
unchanged; historical account traversal now streams explicit pages and applies the
1,000-authored-candidate bound per included repository, rather than rejecting an
otherwise bounded account merely because it has more than 1,000 out-of-scope PRs.

Each historical inventory page is limited to 1,048,576 response characters at the
pipe reader, before JSON buffering. Other provider responses retain the existing
16-Mi-character ceiling, now also enforced while reading. Four readers remain the
process-wide maximum. Streaming stderr retains at most 65,536 characters while
continuing to drain. Consumer failure or cancellation kills and drains the process
tree and observes all readers before returning; no child is intentionally orphaned.
Each repository has a 16-MiB deterministic inventory ledger: 64 bytes per observed
PR number plus 512 bytes and twice the repository-identity length per retained
authored candidate. This charge is a retention proxy, not measured heap usage.
Account traversal separately charges a 16-MiB global observed-identity ledger
(64 bytes plus twice the identity length per row), in addition to each included
repository's authored ledger. Pages are discarded after minimal parsing; whole
JSON page collections are not retained or reserialized. The shared 2,048-request attempt ceiling bounds page and
metadata work. Existing head, deadline, cache, object-store and output bounds stay
in force; no bound permits truncating a complete selection.

Scoped pages retain live head/base/count for exact metadata reuse. REST candidates
refresh those same fields in at-most-12-PR header batches; independently complete
aliases remain usable when another header alias fails, and only unsupported aliases
use minimal REST detail. Each repository-local pipeline slot resolves its headers,
reads its cache, fetches only its misses, and atomically saves/selects each complete
PR before moving on. There is no all-header or all-cache barrier. The same four
slots cover this complete pipeline and REST fallbacks, so cancellation can retain
useful progress even while later headers are unavailable. This keeps the existing
viewer,
repository, PR, head, base, count, freshness and digest cache identity. Completed
commit evidence remains reusable after a later failure; live inventory is always
refreshed and incomplete inventories are never stored as completeness authority.

Adapter response-character, inventory-ledger-byte, authored-candidate and request
failures name the violated limit and safely observed amount. `lastRequest` retains
the responsible operation and output-bound outcome where observed. Acquired-store
bytes and EH deadlines remain separate resource failures. Guidance is to inspect
and resume the same frozen scope/checkpoint after correction, explicitly change a
resource bound where supported, or use a **complete** pinned manifest. A smaller
date interval does not preserve historical coverage. A manual manifest certifies
only its supplied repository/head scope, never organization or annual completeness.

Scoped inventory shares the four-reader process-wide historical gate. A root
request failure cancels and drains siblings before returning its original error.
Repository request observations use opaque scope digests. The fallback reason
`scoped-connection-unavailable` is an optional v1 enum extension. A selected-head
budget failure reports the active `historical-pr-selection` phase, observed distinct
head count and `inspect-head-scope-or-use-pinned-manifest`, with zero retries;
it does not misdiagnose a GitHub service failure or silently omit heads.

Actual cold/warm and annual-bound observations on the explicitly selected public
repositories are recorded in [HISTORICAL_NETWORK_BENCHMARK.md](HISTORICAL_NETWORK_BENCHMARK.md).
A separate [immutable replay review](REWRITE_REPLAY_REVIEW.md) now compares
multi-commit/squashed original, upstream, declared replay and retained endpoints.
Its event result remains conditional on external replay/date provenance and is
non-additive; plain native discovery still cannot recover discarded workdays.

## Safe provider failure detail

New optional `providerDiagnostics.lastRequest` fields identify `api` (REST or
GraphQL), `outcome`, returned `exitCode`, observed `httpStatus` and `timeoutOwner`.
Outcomes distinguish process exits, explicit GraphQL API errors, HTTP failures,
transport failure/timeouts,
malformed JSON, adapter output bounds, executable startup failure and cancellation.
A transport timeout reported by `gh` belongs to `provider-transport`; EH discovery
expiry belongs to `discovery-deadline`. Caller cancellation and sibling failure
are separate owners. No per-process timeout is inferred from duration alone.
Observed HTTP 5xx failures use `github-provider-service-unavailable` and the
zero-automatic-retry action `retry-after-provider-recovery`, rather than blaming
CLI health. Unknown exit details remain `process-exit`, not an invented API or
timeout cause. Successful process receipts include exit zero; accepted empty
repository/fallback receipts retain the actual nonzero exit and HTTP status; interrupted processes have no
fabricated exit receipt. The failure message includes these fixed safe fields.
No raw stderr, URL, query, owner, PR number, identity or source is copied.

Accepted capability fallbacks complete their adapter observation as `fallback`,
so the final successful REST request can become the latest observation. A root
failed request cannot be replaced by cancelled or successfully drained siblings.
Malformed JSON is classified before it could be admitted as a successful page;
known unsupported capability responses retain complete REST fallback. Existing
response, request, deadline and acquisition limits remain fixed. Optional fields
preserve older v1 reports and stay outside the semantic digest. Incomplete reports
still return nonzero without aggregate EHE or daily-zero cells.

The reported alpha.37 incident lasted about 58.83 seconds under a 90-second EH
deadline, with a roughly 45.27-second final adapter request. Available consumer
artifacts do not retain its raw provider exit/status receipt, so these durations
alone do not identify an EH deadline, GitHub transport timeout or API failure.
Later successful health probes cannot resolve that historical cause. Fresh public
revalidation is recorded separately from the incomplete incident in
[HISTORICAL_NETWORK_BENCHMARK.md](HISTORICAL_NETWORK_BENCHMARK.md).

## Attribution and historical note planning

Comparison reports now carry optional derived `attributionCompleteness` separately
from execution `status`. Consequential source warnings are promoted to report-level
`diagnostics`; FB5340/FB5341/FB5344/FB5345 also appear in Markdown. A complete retained
calculation can have unresolved declared events and unknown intermediate history.
Blank buckets never verify zero integration labor or original workdays. See
[REPORTING.md](REPORTING.md) and the offline, permission-aware dry-run contract in
[HISTORICAL_NOTE_REFRESH.md](HISTORICAL_NOTE_REFRESH.md).
