# Immutable rewrite replay review

## Policy and purpose

`immutable-replay-review/1.0.0` adds an offline, bounded review for multi-commit,
squashed and cherry-picked feature ranges. It accepts immutable pre/post endpoints
and old/new upstream bases. It does not infer a historical workday, measure labor,
change source priors, or replace joint portfolio reconciliation. EHE remains
experimental and uncalibrated replacement effort.

```text
eh change review-rewrite replay-manifest.json --scope engineering --format markdown
eh change review-rewrite replay-manifest.json --compact --output new-review.json
```

The output file must be new. The command reads existing local Git objects, never
fetches missing history or executes target code, and does not modify Git refs,
source files, work records or timesheets. Relative repository paths resolve from
the manifest directory. Public repository/provenance IDs must not contain private
identities. Optional `scopeRepository` selects execution-only engineering overrides.

A manifest validates against `change-rewrite-review-manifest`; output validates
against `change-rewrite-review-report`. Discover them with `eh schema list` and
`eh schema show`. Replace these synthetic IDs with actual full immutable objects:

```json
{
  "schemaVersion": "1.0.0",
  "policy": "immutable-replay-review/1.0.0",
  "repositoryId": "project",
  "repositoryPath": ".",
  "oldBaseObjectId": "1111111111111111111111111111111111111111",
  "originalObjectId": "2222222222222222222222222222222222222222",
  "newBaseObjectId": "3333333333333333333333333333333333333333",
  "replayObjectId": "4444444444444444444444444444444444444444",
  "rewrittenObjectId": "5555555555555555555555555555555555555555",
  "replayProvenanceId": "reviewed-counterfactual-replay",
  "eventTimestamp": "2026-01-22T16:00:00Z",
  "eventProvenanceId": "external-rewrite-event-record",
  "sinceInclusive": "2026-01-19T00:00:00Z",
  "untilExclusive": "2026-01-24T00:00:00Z"
}
```

## Replay proof and separate comparisons

The replay object declares the original implementation replayed onto the new
upstream **before the novel retained change**. The caller prepares and reviews
that counterfactual outside this read-only command, retains it as an immutable
commit, and supplies a public provenance ID. The tool does not automatically
execute a rebase or invent a counterfactual conflict resolution.

The original head must descend from the old base; the new base must descend from
the old base; replay and retained heads must descend from the new base. Each
feature/replay range admits at most 1,024 commits without truncation. Empty replay
ranges are permitted when the original feature is already in upstream. Each of
the four replay-proof snapshots admits at most 16,384 files; an oversized proof
fails explicitly. Ordinary source-estimation safety/cache bounds still apply.

For each path, the proof compares exact immutable object IDs and Git modes,
including additions, deletions and links. An original-untouched path must retain
the new upstream state. A path changed only by the original, or changed identically
by original and upstream, must retain the original state. Violations reject the
replay; callers cannot hide unrelated novel files in that snapshot. Paths changed
differently by original and upstream remain declared conflict paths. Their replay
is caller-reviewed evidence, not independently verified historical causation.

| Role | Canonical immutable comparison | Interpretation |
| --- | --- | --- |
| `original-implementation` | old base -> original head | Original normalized feature |
| `inherited-upstream` | old base -> new base | Upstream comparison, never an event surcharge |
| `retained-feature` | new base -> retained head | The final feature relative to new upstream |
| `replayed-implementation` | new base -> declared replay | Counterfactual replay under the declaration |
| `novel-retained-delta` | declared replay -> retained head | Independently estimated normalized retained delta |

Every row records full immutable endpoints, profile, source estimator identity,
effort range, canonical source-report digest and represented-path count. The
review input digest binds the complete declaration, profile and engineering scope;
execution-only repository paths do not enter output. No paths, raw aliases or
source excerpts are emitted.

**These rows are non-additive.** Only `retained-feature` describes the final feature
as a whole. Do not sum the five estimates, add the novel row to a portfolio total,
or interpret effort differences as labor. Novel effort comes from its own canonical
delta, never the per-representation portfolio remainder. An unchanged replay
has a zero novel artifact delta without certifying zero integration labor.

## Event dates and unavailable evidence

`replayConfidence` distinguishes exact nonconflicting path verification from
`caller-declared-conflict-replay`. The latter keeps its conflict-path count and
provenance visible. Even exact path proof does not verify historical causation.

Original author, rewritten committer and explicitly declared event timestamps
remain separate. No changed SHA or committer date supplies an event. With replay
and a supported external UTC event instant inside the requested period,
`eventAttributedNovelEffort` equals only the novel comparison. Outside that period
it is zero support for this event; the full novel comparison remains visible.
Changing periods never re-estimates or moves the full feature to the event.

Without a replay snapshot, three available comparisons remain and event effort
is unavailable (`unresolved-replay-evidence`). Without an event date, the five
comparisons remain but attribution is unavailable (`unresolved-event-date`). If a
required declared object is absent, output is `unresolved-object-evidence`, names
the missing immutable IDs, contains no comparison/effort/date measurements, and
returns exit 3. Restore required objects separately; no failed review certifies a
zero-work day. Invalid ancestry or replay content produces an error without a
review artifact.

The existing [single-commit portfolio allocation policy](REWRITE_EVENT_ATTRIBUTION.md)
remains unchanged. This separate review supports range/squash/copy endpoint
comparisons without introducing a second additive daily ledger. Existing joint
portfolio equivalence still deduplicates repeated representations, and
[workday review](WORKDAY_REVIEW.md) / [declared allocation](WORKDAY_ALLOCATION.md)
remain the explicit workflows for lost workday discrepancies and conserved
fixed-eight-hour projections. Integration labor without a novel retained artifact
is an external assessment, outside counterfactual Change EHE.

## Portfolio integration

[Declared replay range attribution](REPLAY_RANGE_ATTRIBUTION.md) now optionally
composes this canonical review with a manifest portfolio. It preserves the joint
budget, exposes an independent novel estimate and any allocation cap, and keeps
missing date/replay evidence unresolved. The standalone review remains non-additive.

## Verification

Memory-only tests verify exact, upstream, conflict and deletion paths, reject
hidden novel content and lost original deltas, and exercise cancellation and
inventory bounds. Physical fixtures perform real conflicting and nonconflicting
two-commit rebases, retain all original/replay/result objects, and verify distinct
author/committer/event dates, standalone feature parity, pure replay, independent
novel resolution, missing objects/dates, period boundaries and identical squashed/cherry-picked
endpoint effort. Existing portfolio conflicting-rebase/cherry-pick fixtures retain
joint aggregate conservation. These are engineering correctness checks, not
empirical calibration of conflict effort or recovery of discarded history.
