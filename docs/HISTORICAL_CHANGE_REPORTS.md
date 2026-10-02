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

Historical discovery fully paginates the owner's visible, scope-admitted default
branch history without a provider date cutoff, then applies the requested author
or committer timestamp locally. This avoids rejecting January author dates because
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
fields. Default branches and authored retained PR heads enter one repository-local
Git union and one jointly reconciled calculation. Other non-default branches,
PRs authored by another account, deleted PRs, force-pushed-away intermediate
commits, and work crossing the selected interval remain outside this boundary.
Engineering path admission is identical to the native today/named-period path.
Low-level manifest `--scope` admission remains a separate follow-up.

## Exact retained equivalence

Portfolio `change-portfolio/0.3.0` adds two conservative proofs without changing
the source Change estimator or priors:

- An exact represented patch repeated on disjoint pinned head sets is counted
  once. The earlier retained timestamp, then stable item ID, chooses the keeper.
  Reintroductions on a shared reachable head are not suppressed by this rule.
- A connected sequence of selected non-merge commits can prove the same complete
  represented endpoint delta as a standalone squash representation. Every touched
  path must connect through matching immutable states, and the canonical net
  path/base/head digest must equal the standalone patch digest. Selected ancestor
  relationships prevent treating a later reintroduction as a rewritten squash.

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

External work logs, discrepancy matching, optional aggregate allocation across
reported workdays, per-entry multiplier rounding, and downstream time-entry
mutation are not implemented here. They require a separate explicit allocation
contract that conserves the deduplicated aggregate and labels allocated values.
EHE remains experimental replacement effort, not historical labor, productivity,
individual credit, compensation, or a recovered timesheet.
