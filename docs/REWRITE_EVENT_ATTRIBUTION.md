# Declared rewrite event attribution

## Boundary

`declared-rewrite-event/1.0.0` is an explicit allocation policy for immutable,
single-commit, first-parent rewrite pairs. It is not automatic recovery of a
rebase event, actual labor measurement, a conflict surcharge, or a new source
estimator. EHE remains experimental and uncalibrated replacement effort.

An author-period manifest repository may contain `rewriteEvents`, with 1-32
disjoint pairings. Each pairing contains:

```json
{
  "attributionPolicy": "declared-rewrite-event/1.0.0",
  "originalObjectId": "<full immutable before commit>",
  "rewrittenObjectId": "<full immutable after commit>",
  "oldBaseObjectId": "<actual first parent of before commit>",
  "newBaseObjectId": "<actual first parent of after commit>",
  "eventTimestamp": "2026-01-22T12:00:00Z"
}
```

For local repository locators, optional `scopeRepository: "owner/repository"`
selects the same engineering repository overrides as native discovery. It is
execution-only, digest-bound and excluded from report text. A provider locator
already supplies this identity; conflicting explicit identities are rejected.

Both commits must be reachable from the manifest's pinned heads, match requested
identity/coauthor policy, and have exactly the declared single parent. Source Git
stays read-only. Chained/competing pairings and merge rewrites are rejected, and
missing evidence fails rather than certifying zero. The input pairing is a caller
assertion, not independent proof of semantic equivalence or a historical workday.
An unavailable older commit must be restored outside ordinary analysis and pinned
explicitly; the tool never invents it or reads a mutable reflog as authoritative.

## Selection and allocation

Each pair is admitted when either the original selected timestamp or declared
novel event instant is in the requested interval. Both immutable rows enter the
same bounded ledger, including out-of-window support; their complete original
metadata remains the source of identity and snapshot planning. In-window selection
still uses exact ordinary identity matching. Support rows receive an in-window
accounting anchor only to carry evidence through the versioned portfolio contract;
`supportOnly` distinguishes that anchor from the preserved original timestamps.
They contribute no EHE to the requested period.

Canonical Change reports compare original against old upstream and rewritten
against new upstream. Thus inherited upstream changes are not charged. Existing
joint overlap, exact replay and composition reconciliation establishes the pair's
allocated budget; comparing the two feature heads directly is not the novel budget.
When both pair rows remain active, the original row retains up to its canonical expected effort from that budget,
and the rewritten row receives only the remainder. Existing exact suppression
retains zero for suppressed rows. No hour is added to the joint total.

For example, a reconciled 2.75-hour pair with a 2.00-hour original receives 2.00
on the original date and 0.75 at the declared resolution event. An event-only
period carries both evidence rows but allocates only 0.75. An unchanged 2.00-hour
replay allocates zero at the event. Separately selecting the two disjoint periods
preserves that same pair's full-period expected total. General portfolio membership
can still change joint allocations; this is not a membership-stable attribution
or a claim that arbitrary subintervals or additional contributors commute.

When a support row lies outside the period, its allocation is removed after joint
reconciliation. Category ranges are projected proportionally using the same
allocation interpretation as comparison series, with a deterministic two-decimal
residual adjustment. They are allocation bounds, not independent measured
resolution-effort confidence intervals. Full-period ranges/categories remain
unchanged. Rates and capacity denominators do not affect this calculation.

Use joint normalization and ordinary `calendar-day` buckets. Independent-day
normalization and isolated contributor series are rejected for declared pairs:
they cannot retain the shared baseline proof across dates. The CLI does not mutate
external time entries. Consumer work-log allocation, two-decimal multiplier
rounding, and fixed-eight-hour reference choices remain separate adapter contracts;
the daily EHE ledger must be conserved and must not be multiplied by logged labor.

## Evidence and unresolved dates

Each output item carries `attribution.rewrite` with the complete immutable pair,
original author timestamp, rewritten committer timestamp, original/rewritten role,
`supportOnly`, `basis: caller-declared-immutable-pair`, and
`confidence: declared-not-verified-workday`. `treatment` distinguishes original
contribution, retained resolution contribution, no retained increment, and
zero-cost evidence support. These fields and event instants bind manifest and
repository checkpoint digests. Aliases and repository locators remain excluded.

`eventTimestamp` is optional. Without it, attribution remains on the original
selected timestamp and `FB5340` explicitly reports unresolved event attribution.
A rewritten committer date alone is never promoted to a verified workday. Even an
empty requested day retains that warning when a supplied pair has no event date.
Native retained-history reports emit `FB5341` for discoverability coverage: a
complete inventory of currently reachable objects does not certify original daily
history or recover discarded intermediate versions. A measured no-increment replay,
a no-selected-retained-change day, and a failed/missing-evidence report are distinct.

## Verification

Synthetic constructed graphs and actual conflicting `git rebase` fixtures test
pure replay, retained resolution, old/new base exclusion, unchanged full-period
categories/ranges, event-only and original-only allocation, unresolved dates,
invalid-base incomplete artifacts, schema validation and privacy. Empty explicit
manifests are tested through calendar-day, independent-day and plain portfolio
output. Real repository calibration and automatic event capture remain outside
this policy's evidence boundary.

## Using the existing pair policy

A plain two-head manifest does not declare a rewrite event. Supply
`repositories[].rewriteEvents[]` with original/rewritten object IDs, their actual
old/new first parents, and an optional externally declared event instant. Run the
manifest with joint `--bucket calendar-day` reporting; `eh examples rebase` prints
the workflow. Keep both support objects available even for an event-only period.
The pairing shape appears above; a complete synthetic manifest follows. A changed SHA or later committer
date cannot replace these inputs. Real conflicting-rebase and disjoint-period
fixtures remain the verification baseline.

For broader lost-workday declarations, use the separate
[workday allocation contract](WORKDAY_ALLOCATION.md). It deliberately preserves
original event/date evidence in its source artifact rather than declaring novel
resolution from ordinary row allocations.

```json
{
  "schemaVersion": "1.0.0",
  "selection": {
    "sinceInclusive": "2026-01-19T00:00:00Z",
    "untilExclusive": "2026-01-24T00:00:00Z",
    "timeZone": "UTC",
    "dateField": "author",
    "mergePolicy": "exclude",
    "coauthorPolicy": "include",
    "intervalSemantics": "since-inclusive-until-exclusive"
  },
  "contributors": [{ "id": "developer", "aliases": ["developer@example.invalid"] }],
  "repositories": [{
    "id": "project",
    "repositoryPath": ".",
    "heads": [
      { "id": "original", "objectId": "1111111111111111111111111111111111111111" },
      { "id": "rewritten", "objectId": "2222222222222222222222222222222222222222" }
    ],
    "rewriteEvents": [{
      "attributionPolicy": "declared-rewrite-event/1.0.0",
      "originalObjectId": "1111111111111111111111111111111111111111",
      "rewrittenObjectId": "2222222222222222222222222222222222222222",
      "oldBaseObjectId": "3333333333333333333333333333333333333333",
      "newBaseObjectId": "4444444444444444444444444444444444444444",
      "eventTimestamp": "2026-01-22T12:00:00Z"
    }]
  }]
}
```

Replace every synthetic ID with the actual immutable object and choose the local
repository path relative to the manifest. Those older objects must still exist.
The ordinary manifest command is offline; provider locators and missing objects
require its explicit `--fetch-missing` authorization. Use a full-period run first,
then an event-only period with the same pair and original support heads. No-event
inputs keep `FB5340` unresolved rather than inferring a workday from commit dates.

## Allocation basis and repeated representations

New portfolio 0.6.5 reports add optional `allocationBasis` with value
`joint-retained-budget-remainder` and diagnostic `FB5342`. The retained remainder
is a structural allocation from the joint budget after preserving the original
baseline, not a causal conflict-resolution breakdown. The legacy
`retained-resolution-contribution` treatment name retains its declared-policy
meaning; it must not be read as independently measured resolution work. Existing
v1 reports without the new basis remain valid. Integration labor with no novel
retained delta needs a separate external definition and never creates Change EHE
merely because a conflict occurred.

A proven exact repeat on disjoint reachable heads now prefers a declared pair
member over an unpaired representation before the ordinary retained timestamp
and stable-ID ordering. This preserves the explicitly declared date policy when
a cherry-pick repeats the rewritten feature. Original pair roles still take
precedence over pure replay. Shared-head reintroductions remain represented;
matching content alone never overrides that reachability guard. The portfolio
identity advances so generated allocation/digest lineage records this policy.
Source-only repository evidence can remain reusable; reconciliation runs again.
Priors and ordinary unpaired equivalence rules are unchanged. Real conflicting rebase fixtures now
include actual cherry-picks of both pair members, full/event-only conservation,
zero duplicate allocation and separated author/committer/event instants.

This still supports single-commit pairs only. General multi-commit causal replay,
altered squash equivalence and automatic event recovery remain outside the proof.
Use [work-record review](WORKDAY_REVIEW.md) to expose unresolved workday evidence
before opting into the separate declared-date projection.
