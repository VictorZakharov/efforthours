# Declared replay range portfolio attribution

## Contract

`declared-replay-range/1.0.0` composes the bounded
[immutable replay reviewer](REWRITE_REPLAY_REVIEW.md) with the existing joint
portfolio. Add `repositories[].replayEvents` to an author-period manifest and run:

```text
eh change portfolio --author-period-manifest period.json --bucket calendar-day --no-rate --output daily.json
eh change portfolio --author-period-manifest period.json --bucket calendar-day --no-rate --checkpoint private-checkpoint --format markdown --output daily.md
```

This is experimental, uncalibrated replacement-effort allocation. It does not
measure integration labor or recover historical workdays. The caller retains
and reviews the counterfactual replay outside EH and supplies separate public
provenance IDs for that snapshot and the externally declared UTC event instant.
No target code, rebase, cherry-pick, work record or Git ref is executed or changed.
The ordinary manifest command is offline; restore missing immutable objects
separately. Paths and raw identity aliases remain execution-only.

Each event has this shape (replace every synthetic object ID):

```json
{
  "attributionPolicy": "declared-replay-range/1.0.0",
  "id": "feature-rewrite",
  "oldBaseObjectId": "1111111111111111111111111111111111111111",
  "originalObjectId": "2222222222222222222222222222222222222222",
  "newBaseObjectId": "3333333333333333333333333333333333333333",
  "replayObjectId": "4444444444444444444444444444444444444444",
  "rewrittenObjectId": "5555555555555555555555555555555555555555",
  "replayProvenanceId": "reviewed-counterfactual",
  "eventTimestamp": "2026-01-22T16:00:00Z",
  "eventProvenanceId": "external-event-record"
}
```

Pin heads reaching **every** original and retained range member, including older
support objects. Use the existing manifest selection/contributor/repository
fields. Event IDs are public IDs unique within one repository. Each repository
admits at most 32 disjoint events, each original/retained range at most 1,024
commits. Attribution requires complete linear non-merge first-parent ranges and
the same exact contributor match set across every member. Overlapping range
members, competing events, legacy-pair overlap, unavailable members and branched
ranges fail explicitly without an aggregate. A squash can be one retained commit;
a copied range can have different commit IDs with equivalent final artifacts.
The replay proof keeps its four-snapshot 16,384-files-per-snapshot bound.

## Conserved allocation

Each range member remains a canonical per-commit source row. Ordinary exact
patch/composition deduplication and joint category reconciliation run first.
Normalization uses every member's original selected timestamp and ordinary keeper
evidence, before event dates are applied. A directly equivalent duplicate can
transfer its keeper reference to the original date only after the budget is fixed,
and only when this preserves the disjoint-head guard.
The event receives `min(standalone novel expected EHE, available joint member
budget)`. The remainder stays on original members' original selected dates.
Distribution uses source expected weights and deterministic cent conservation;
suppressed duplicate/composition rows always stay zero. If a positive role budget
has no active supporting row, the calculation fails for review instead of
inventing a charge or silently claiming zero.

The independently estimated novel delta is replay -> retained head. Its five
canonical comparisons are non-additive and are embedded once per event in
`replayAllocations[].evidence.review`. Never add those comparisons to portfolio
EHE. The full jointly selected total and category ranges are conserved. This
policy is a structural projection of that budget, not causal category-by-category
conflict effort. Interval allocation uses the existing proportional ledger.

For example, a joint member budget of 100 hours and a standalone novel comparison
of 20 hours allocate 20 to the event and 80 to original dates. A standalone novel
comparison of 120 allocates at most 100: JSON and Markdown show both 120 and 100,
`allocationCapped: true`, and the available joint budget. No extra 20 hours are
created. Original and retained periods partition the same jointly selected range;
other selected changes can still affect membership-dependent joint normalization.

Out-of-window original/retained members remain `supportOnly` evidence and are
removed **after** joint reconciliation and allocation. Event-only and original-only
requests therefore retain the same complete immutable range, and omitted dates
cannot change its deduplication basis. The event instant is inclusive at `since`
and exclusive at `until`. Out-of-period event allocations are zero support while
the independent novel comparison remains visible. A proven zero novel artifact
delta is not a statement of zero human integration labor.

## Unresolved states and lineage

`replayObjectId` and `replayProvenanceId` are optional together; `eventTimestamp`
and `eventProvenanceId` are optional together. Missing replay or date keeps
ordinary retained-date selection/allocation and an explicit unresolved event
allocation (`null`, never certified zero). No Git committer timestamp supplies an
event date. Missing required objects or invalid replay evidence fail the portfolio
without complete aggregates. Exact nonconflicting path proof remains distinct
from `caller-declared-conflict-replay`; neither verifies historical causation.

`attribution.replay` binds each row's event ID, original/retained role, original
selected UTC timestamp and support flag. `replayAllocations` binds complete ordered
member IDs, declared endpoints, provenance, canonical review/source digests,
profile, budget, cap and event allocation. JSON, trend and findings Markdown use
one canonical result. Manifests and repository checkpoint identities bind events
but exclude local paths; warm checkpoints reuse the canonical review. Ordinary
reports without replay declarations retain portfolio 0.6.5 identity and serialized
fields. Reports using this policy carry portfolio 0.6.6. Source rules and priors
are unchanged. Independent-day normalization and isolated contributor series
cannot compose this joint range policy; use ordinary joint calendar-day buckets.

`--preflight` traverses bounded range membership and identity/reachability only;
it does not run replay proof or static comparisons. Canonical comparisons run
sequentially under existing source-analysis bounds when calculating. Their elapsed work
is recorded in static-analysis timing but is not included in the existing portfolio candidate snapshot/reuse
counters or projected candidate requests; those counters describe the canonical
per-commit pipeline. Review comparisons carry their own source digests.

Tests cover conserved budgets, explicit caps, zero novel deltas, deterministic
ordering, schema/semantic tampering, real conflicting/nonconflicting rebases,
squashed/copied endpoints, date partitions, missing date/objects, competing
mappings, zero duplicate charges and cold/warm checkpoint parity. These are
correctness checks, not field calibration or complete recovery of lost history.
