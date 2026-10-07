# Historical note refresh dry-run plan

`historical-note-refresh-plan/1.0.0` is an offline planning surface. It creates a
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
