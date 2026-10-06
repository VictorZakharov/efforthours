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
The 32-head per-repository, 512-head, ledger, queue, checkpoint, and output bounds
remain enforced. A complete report describes only currently available provider
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
Discrepancy matching, per-entry multiplier rounding, and downstream time-entry
mutation remain outside this tool's contract.
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
