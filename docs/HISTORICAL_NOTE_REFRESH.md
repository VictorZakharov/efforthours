# Historical note refresh dry-run plan

`historical-note-refresh-plan/1.1.0` is an offline planning surface. It creates a
private, reviewable proposal and never writes a time entry. It neither authorizes
actual log edits nor certifies workdays or labor. EHE remains experimental and
uncalibrated replacement effort.

```text
eh change plan-refresh period.json --work-records records.json --entries refresh.json --output new-plan.json
eh change plan-refresh period.json --work-records records.json --entries refresh.json --fields both --entry-policy equal-matched-entries/1.0.0 --output new-plan.json
```

The command recomputes the existing digest-bound [workday review](WORKDAY_REVIEW.md)
from the original complete single-contributor joint whole-day comparison and work
records. It rejects incomplete discovery, tampered source digests, isolated or
independent-day sources and invalid inputs before emitting any plan. It does not
trust externally edited review values, rerun estimation, access Git/network or
overwrite input or existing output artifacts. `--output` must be a new path. Cancellation and
invalid input produce no plan; output is atomic UTF-8.

The separate `change-historical-refresh-manifest` binds the exact source semantic
and work-record input digests. Copy `workRecordInputDigest` from `review-days`.
Declare real `sinceInclusiveDate` / `untilExclusiveDate` boundaries, 1-4096 exact
public `recordId` values from that review, and a complete private `original` JSON
snapshot containing its string `description`. Every ID must be inside the explicit
range. Inputs are at most 1 MiB, each snapshot at most 65,536 characters and each
description at most 8,192 characters. Preserve original dates, hours, projects,
tasks, tickets, billing and provenance fields in that snapshot. These private
records intentionally contain caller-supplied text; never upload their plans as
public calibration data or substitute fixture records into real logs.

For example (replace both digest placeholders and every synthetic record with the
actual reviewed private snapshot before use):

```json
{
  "schemaVersion": "1.0.0",
  "sourceSemanticDigest": "sha256:<source verification.semanticDigest>",
  "workRecordInputDigest": "sha256:<review workRecordInputDigest>",
  "sinceInclusiveDate": "2026-01-19",
  "untilExclusiveDate": "2026-01-24",
  "entries": [{
    "recordId": "entry-1",
    "original": { "description": "Original task text", "loggedHours": 4, "project": "project-1", "task": "task-1", "ticket": "ticket-1", "billing": "original-state" },
    "notePermission": "allowed",
    "ehePermission": "unknown",
    "restriction": "none"
  }]
}
```

The placeholders are intentionally invalid digests. Original snapshot fields are
opaque context, not effort inputs; preserve the complete actual external record.

Each entry declares independent `notePermission` and `ehePermission`: `allowed`,
`denied` or `unknown`. `restriction` is `none`, `locked`, `invoiced` or `unknown`.
Missing permissions are invalid. Unknown permissions/restrictions block proposals;
locked and invoiced entries block both fields. These are observed caller-supplied
states, not a claim that EH contacted the external system. A downstream adapter
must expose actual edit failures and recheck permissions; note authorization does
not authorize analytics EHE edits, and EHE authorization does not authorize notes.

The output always has `dryRun: true` and `requiresEntryConfirmation: true`. It
contains the exact affected IDs/range, untouched original snapshots, their digests,
source/review lineage, evidence states, proposed descriptions and separate optional
EHE contributions with explicit permission/restriction/unresolved block reasons.
Only `notes` is requested by default; `--fields ehe|both` independently opts into
numeric proposals. Nothing changes dates, hours, projects, tasks, tickets, billing
or provenance. No actual write command exists in EH.

Notes preserve all existing description text outside a single exact managed block:
`[EffortHours historical annotation]` through
`[/EffortHours historical annotation]`. Refresh replaces that block in place or
appends it once. Repeating the same plan yields `unchanged`, not a duplicate.
Multiple/unbalanced markers or an annotation that would exceed the 8,192-character
description bound block the note proposal for external review. The
annotation states retained evidence, discrepancies and source lineage. Every new
annotation consumes the review's source `attributionCompleteness`, including event
availability, missing dates/baselines, original-workday status and intermediate-history
status, even without an external workday allocation. Older reviews without that
optional contract explicitly show unknown source completeness. The annotation always
keeps original workdays/intermediate history unresolved, and explicitly says zero
retained EHE is not zero labor. Unresolved dates/baselines produce discrepancy
annotations, never a true-zero-work explanation.

Retained-date numeric proposals require independent EHE permission and explicit
`equal-matched-entries/1.0.0`. The existing fixed expected-EHE/8 denominator,
midpoint-away-from-zero two-decimal rounding and equal integer-cent remainders
remain authoritative. Logged durations never supply weights. All matched entries
on an affected day must be selected together so their reviewed contributions
conserve the matched total; unresolved/excluded entries have no numeric proposal.
Multiple positive entries are retained when supported. Restrictions can block a
proposal, so an executor must not apply only the unblocked subset and describe it
as a complete refresh.

Before any downstream mutation, the user must separately confirm the exact range
and entries after reviewing this plan. A consumer must re-read original snapshots,
compare their digests, recheck independent permissions and locks/invoices, and
apply idempotently with a resumable all-or-explicitly-incomplete result. Changed
records, failed discovery, denied edits or uncertain attribution must never yield
partial misleading notes or zero-filled values. Real external adapter behavior is
not implemented or verified by these dry-run tests.

Memory-only tests cover description/snapshot preservation, managed-block
idempotence, Unicode, denied/unknown independent permissions, locks/invoices,
explicit dates/IDs, multiple matched contributions, digest/tamper rejection and
unresolved/DST evidence. Physical CLI tests verify schemas, private unchanged
inputs and protected output paths. These checks establish planning safety, not
historical completeness or empirical calibration.

## Explicit external dates for lost retained workdays

Add `--workdays <workdays.json> --workday-policy equal-declared-days/1.0.0` to
recompute the [declared-workday review](WORKDAY_REVIEW.md) from approved dated
implementation records. This can propose contributions on dates without retained
commits while preserving the original retained attribution and missing-history
warnings. Each date declaration must anchor an exact in-scope implementation
record; all implementation dates must be declared. It does not recover discarded
Git dates, certify external records, create missing effort or perform entry writes.

For numeric proposals explicitly use `--entry-policy
equal-declared-day-entries/1.0.0`; its fixed-eight, single period rounding and
canonical date/entry cent remainders conserve the complete matched multiplier.
Select every contributing entry in the entire declaration for EHE refresh. A
notes-only plan may select a subset. The annotation names external date evidence
and the exact declaration digest plus source event-date/baseline availability;
underlying event/workday uncertainty remains visible. Undeclared rows are labeled
as not declared for allocation. Empty retained effort supplies no fabricated zero contribution.

## Managed zero explanations and matching

New plans use `historical-note-refresh-plan/1.1.0`. The v1 schema also accepts
saved 1.0.0 plans; the exact known alpha.39 and alpha.40 annotation forms remain
available for semantic validation. Arbitrary edited legacy text is rejected. No source estimator, report value, allocation weight or entry multiplier
changes. New proposals include `priorAnnotationStatus` and, when a description
is available, `proposedNoteRecordDigest` over the complete original snapshot with
only `description` replaced. Numeric values are separate proposals with no inferred
external field mapping; this note digest never claims to include an applied EHE edit.

Within the exact managed delimiters, recognized legacy verdict lines such as
`No work occurred.`, `No work was done.` or `Zero actual labor.` are labeled
`zero-labor-claim`. A `no-retained-change` or `No retained change` annotation is
separately labeled `retained-no-change-annotation`. Unrecognized managed prose is
`other-managed-annotation`; malformed/multiple delimiters are ambiguous and block
refresh. This is a bounded explicit-verdict classifier, not general interpretation
of arbitrary historical prose. Text outside those delimiters is never classified
or removed. All original text remains in the private input snapshot for recovery.

An external implementation entry on a blank retained date receives explicit
unresolved daily-attribution wording; the annotation names unavailable source
evidence without inventing a squash/rebase cause. Review, coordination, testing
execution, debugging and integration labor with no retained artifact delta are not
measured by Change EHE. A supported declared event can resolve that event date while
original Git workdays and intermediate history remain unresolved. Numeric zeros
remain metric values only. Mixed entries or unresolved repository matching block
both note replacement and numeric proposals until matching is reviewed externally.
A known matched record with unresolved historical dates can still receive a notes-only
discrepancy annotation; it cannot receive a fabricated numeric contribution.

## Executable snapshot and permission preflight

After reviewing the plan, export **fresh complete snapshots** of the same exact
selected IDs from the external system, including freshly observed independent
permissions and locks/invoices. Use the `change-historical-refresh-current` schema
with the same refresh-manifest fields, source/record digests and explicit date
boundaries, then run:

```text
eh change check-refresh new-plan.json --entries current-refresh.json --output new-check.json
```

`historical-refresh-preflight/1.0.0` checks validated plans and current manifests
offline. It never contacts timeinv, reads credentials, edits entries or authorizes a
write. Plans are bounded at 16 MiB and current inputs at 1 MiB. Outputs use the new
`change-historical-refresh-check` schema and preserve the complete private plan and
current observations, exact plan digest, selected IDs, before/current/planned-note
digests and independent field blockers. An entry whose snapshot is byte-equivalent
under compact JSON serialization is `original-match`. If **only** the exact planned
description has changed it is `planned-note-match`, and the note is `already-current`.
Any other field change is `concurrent-edit`; requested fields are blocked. Snapshot
property reordering is conservatively a change; adapters should preserve export order.
An empty current entry list records that every selected entry disappeared.
Missing IDs and extra selections block the whole check rather than blessing a subset.
Changed range/lineage or tampered plans are invalid and produce no receipt.

A denied/unknown permission or locked/invoiced/unknown restriction blocks its
requested field. Previously blocked proposals remain blocked even when a fresh
observation says allowed: create and review a new plan. A blocked receipt exits 3;
invalid input or expected operational failure exits 1 without a valid new receipt.
Usage errors remain 2 and cancellation 130. Preserve native exits in shell wrappers;
see the [complete integration contract](HISTORICAL_REFRESH_INTEGRATION.md) and
[synthetic offline adapter](../examples/historical-refresh/README.md).
No unblocked subset may be applied as a complete conserved refresh. Successful
checks exit zero with `ready-for-confirmation`, `dryRun: true` and
`requiresEntryConfirmation: true`. Already-current notes require no note mutation.
Numeric `ready-to-set` only permits reviewing the separate contribution proposal;
it does not claim an external analytics field already equals that value.

The user must confirm the **specific checked plan**, dates, IDs and fields before
an external adapter writes anything. The check is a point-in-time observation,
not a lock: immediately before writing, the adapter must use the current snapshot
or server revision in an atomic compare-and-set, recheck permissions, and refuse a
concurrent change. A read-then-unconditional-write is unsafe even after a successful
check. Preserve the original snapshot, plan/check digests and actual write receipts
privately for audit/recovery; do not log tokens or credentials. Actual timeinv
application, billing-system permissions and recovery transactions are outside EH
and remain unimplemented. Tests verify the offline planning/preflight policy only.

## Verification map and remaining boundary

| Requirement | Executed regression boundary |
| --- | --- |
| Real conflicting two-commit rebase, preserved original/upstream/replay/result and separate provenance | `ReplayRangeReviewSeparatesActualRebaseNovelDeltaAndPreservesSquashedResults` runs actual `git rebase` and conflict resolution; immutable comparison endpoints are asserted. |
| Cross-period conservation, squash/copy parity, missing dates/baselines, complete event with later unresolved work record | That fixture invokes `VerifyDailyReplayPortfolioAsync`, now also checking review/refresh/preflight for a blank later workday while event attribution is available. |
| DST and fixed-eight conserved multi-entry allocation | Existing `ChangeWorkdayAllocationTests`, `ChangeWorkdayReviewTests` and `ChangeDeclaredWorkdayReviewTests`; declarations do not infer dates or use logged durations as weights. |
| Partial interruption and exact resume | `GitHubHistoricalResumeTests` plus physical `HistoricalResumeBenchmarkCliTests` preserve 204 completed receipts and fetch only 236 remaining out of 440 candidates. See [resume checkpoint](HISTORICAL_PR_DISCOVERY_BENCHMARK.md). |
| Provider deadline/drain, malformed/bounded responses, object growth, changed cache identity/freshness | `ChangeHistoricalProviderTimeoutTests`, `GitHubProviderRequestFailureTests`, physical provider process/acquisition tests and `GitHubPullMetadataCacheTests`; failure artifacts omit aggregates. |
| Managed zero verdicts, exact preservation/idempotence, matching, independent permissions, snapshots and concurrent edits | `ChangeHistoricalAnnotationTests`, `ChangeHistoricalRefreshPreflightTests` and the physical CLI path in `ChangeWorkdayReviewCliTests`. |
| Annual/multiple-repository cold/warm network observations with explicit scope | [Public network checkpoint](HISTORICAL_NETWORK_BENCHMARK.md); no organization completeness or NDA field latency claim. |

These are policy/correctness checks. General semantic replay equivalence, recovery
of discarded Git workdays, automatic PR/ticket matching, certification of external
records, actual timeinv writes and empirical effort calibration remain unsupported.
No new estimate is fabricated to eliminate those evidence limitations.
