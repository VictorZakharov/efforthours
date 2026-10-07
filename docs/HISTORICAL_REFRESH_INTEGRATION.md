# Offline historical refresh integration

This is the external-consumer handoff for the existing versioned review, plan and
preflight contracts. EH remains an offline planner. EHE is experimental,
uncalibrated replacement effort, not actual labor, a timesheet or billing data.
An external timeinv writer, credentials and real entry mutations are separate
integration decisions. The [synthetic example](../examples/historical-refresh/README.md)
contains no credentials and its adapter writes only to an in-memory note store.

## Complete sequence

1. Produce and retain a complete `change-portfolio-comparison-report` JSON with
   one contributor and calendar-day buckets. Check its schema, semantic validation,
   verification digest and `status: complete` before continuing. Incomplete provider
   inventory, acquisition, selection or analysis must never become a ready update.
   No refresh step reacquires historical evidence or repairs incomplete discovery.
2. Export an explicit `change-work-record-manifest`: opaque public record IDs,
   dates, kind, exact repository IDs, optional logged hours, and the comparison's
   `verification.semanticDigest` as `sourceSemanticDigest`. Keep the private ID
   mapping and supporting work-record evidence outside the repository. Resolve
   mixed kinds and ambiguous project/repository/date matches before numeric work.
3. Run `review-days` on the same report and work records. For retained-date
   contributions explicitly select `equal-matched-entries/1.0.0`. For externally
   declared dates additionally supply the digest-bound workday manifest,
   `equal-declared-days/1.0.0` and `equal-declared-day-entries/1.0.0`.
   Review the allocation basis and unresolved attribution, not just process success.
4. Export selected complete entry snapshots to a
   `change-historical-refresh-manifest`. Preserve every original property, factual
   description, nested provenance and property order. `workRecordInputDigest` is
   the canonical digest returned by `review-days`; do not hash an ad hoc subset.
   Export independent live note/EHE permission observations and restriction state.
5. Run `plan-refresh` on those same inputs and snapshots, explicitly selecting
   `notes`, `ehe` or `both` and the same optional allocation policies. Preserve the
   original snapshot and review the exact selected dates, IDs and proposed fields.
   Notes-only selection may be narrower. Numeric selection requires all contributing
   entries on each affected retained date, or all contributing entries in a
   declared allocation period; a blocked subset cannot stand in for a conserved total.
6. Re-export fresh snapshots, permissions and restrictions for exactly the selected
   IDs and half-open date range. Run `check-refresh` with the saved plan and this
   `change-historical-refresh-current` manifest. Verify the receipt as described below.
7. Obtain separate confirmation of the specific plan digest, range, IDs and fields.
   An external adapter then needs a server-side atomic conditional update; it must
   still recheck current permission/restriction facts inside that operation.

The examples show executable commands. The governing arithmetic and source
requirements are [workday review](WORKDAY_REVIEW.md),
[workday allocation](WORKDAY_ALLOCATION.md) and
[refresh planning](HISTORICAL_NOTE_REFRESH.md).

## Process exit contract

For **`check-refresh` only**, the native process contract is:

| Exit | Meaning | Consumer action |
| ---: | --- | --- |
| 0 | A valid `ready-for-confirmation` receipt was emitted. | Validate the receipt and obtain specific confirmation; no write is authorized by this exit alone. |
| 3 | Expected blocked preflight; a valid `blocked` receipt was emitted. | Inspect structured blockers and re-export/replan; apply none of this selected batch. |
| 1 | Invalid/schema-inconsistent/oversized input, stale lineage/range, or expected I/O/output failure. | No valid new receipt; correct inputs/permissions and retry to a new output path. |
| 2 | Usage/argument error. | Correct invocation; no receipt. |
| 130 | Cancellation. | Treat as incomplete; discard any unverified output and retry. |
| 4 | Unexpected application failure. | Treat as failed; no usable receipt is promised. |

Usage, cancellation and unexpected application exits retain the general CLI
compatibility contract. Other commands retain their existing exit meanings;
`review-days`/`plan-refresh` success can contain unresolved or blocked proposals.
Alpha.41 returned 3 for both blocked preflight and invalid input; invalid/expected
operational failures now return 1. A blocked output file still exists intentionally.
An existing output is never overwritten: use a new path for every attempt and bind
that exact file to that attempt's exit and expected plan digest. Never infer readiness
from file existence, exit zero alone, or a human stderr message.

PowerShell launchers can normalize native nonzero exits to 1. The installed
alpha.41 binary returns 3 directly for a blocked fixture; both Windows PowerShell
and PowerShell 7 `-Command` wrappers reproduce exit 1 without explicit propagation.
Inside a wrapper/script, capture `$LASTEXITCODE` immediately, then `exit` that value:

```powershell
& eh change check-refresh plan.json --entries fresh.json --output new-check.json
$refreshExit = $LASTEXITCODE
exit $refreshExit
```

A calling PowerShell session can inspect `$LASTEXITCODE` without exiting itself.
Prefer a direct native subprocess with an argument array in automated adapters.

## Machine-readable acceptance and states

Validate `change-historical-refresh-check` schema **and** the EH semantic contract
(`ContractValidation.Validate(receipt)` for .NET consumers). Schema alone is not a
proof of lineage or recomputed policy. Require known policy
`historical-refresh-preflight/1.0.0`, `dryRun: true`,
`requiresEntryConfirmation: true`, `status: ready-for-confirmation`, and an empty
`unexpectedRecordIds`. Unknown versions/states fail closed.

Bind `planDigest` to the confirmed compact-serialized complete plan. Require exact
`plan.input`/`current` source and work-record digests, `sinceInclusiveDate` and
`untilExclusiveDate`, and exactly one row/current snapshot/proposal for every
selected `recordId`. Join by ID, never array position or fuzzy description matching.
Use the EH digest implementation: whitespace is normalized by compact serialization,
while property order, numeric serialization and Unicode escaping matter.

| State / field | Meaning and permitted handling |
| --- | --- |
| Plan `noteStatus` / `eheStatus: proposed` | Reviewable intent only; never an authorization. Read the separate `proposedDescription` / `proposedMultiplierContribution`. |
| Plan `noteStatus: unchanged` | The exact managed annotation already matches; preserve it. |
| Check `snapshotStatus: original-match` | Complete current snapshot matches the planned original, not just its description. |
| Check `snapshotStatus: planned-note-match` | Only the exact planned description differs. Other property/revision changes are conservative conflicts and require replanning. |
| Check field `ready-to-set` | This requested field passed offline preflight. Note text and numeric proposals remain separate mutations. |
| Check note `already-current` | No note mutation. This does not mean an external numeric field has the proposed value. |
| Field `not-requested` | No mutation, regardless of other permissions. |
| Top-level `blocked`, any `blocked-*`, missing/extra IDs | Apply none of the selected batch. Freshly allowed permissions cannot unblock a previously blocked plan; replan. |
| Review `unresolved-workday`, unavailable contribution (`null`) | Missing date attribution, not zero labor. A matched notes-only discrepancy annotation can be permitted; an invented numeric zero cannot. |
| Review `declared-workday-allocation` / allocation `allocated` | Explicit external distribution of one reconciled period total; not recovered history. Original workdays remain unresolved. |

For a note mutation require `noteStatus: ready-to-set`, nonnull checked current
snapshot digest, nonnull proposed text and `proposedNoteRecordDigest`. Only replace
`description`; preserve all other fields. For EHE require `eheStatus: ready-to-set`
and the exact nonnull decimal contribution from the matching proposal. Mapping
that value to a target analytics field requires an explicit consumer contract;
EH never guesses the field or infers whether it was already set. Inspect each field
independently even when the receipt as a whole is ready.

The managed note block uses exact delimiters
`[EffortHours historical annotation]` / `[/EffortHours historical annotation]`. Replace one complete
block in place; preserve all factual text outside it. Duplicated/unbalanced blocks
and descriptions whose proposed output exceeds 8,192 characters block note refresh.
Do not manually edit a plan to repair these states: fix the source record and replan.
Opaque original snapshots are bounded at 65,536 characters; current manifests at
1 MiB and plans at 16 MiB. Treat all snapshots/receipts as private data.

## Permissions, restrictions and uncertainty

`notePermission` and `ehePermission` are separate `allowed`, `denied` or `unknown`
observations. `restriction` is independently `none`, `locked`, `invoiced` or
`unknown`; anything except `none` blocks every requested field. Numeric editability
never implies note editability. EH cannot establish live permission from a synthetic
`allowed` value or certify an API's lock semantics. If a target allows analytics
edits on invoiced entries, that requires a new explicit integration/policy decision;
the current contract conservatively blocks them.

Attribution uncertainty is separate from all of these states. Retained-date and
externally allocated views are alternatives of the same total, never additive new
effort. Multipliers use expected EHE / fixed 8 hours, rounded to two decimals with
midpoint away from zero. Declared entry allocation rounds the complete period once,
then distributes cents over sorted dates and sorted implementation record IDs.
Meeting/PTO records receive no implementation contribution; logged durations and
optional capacity do not change these weights or the fixed denominator.

## Conditional application, interruption and recovery

An external conditional mutation must compare the **complete checked snapshot** or
its server revision, the exact target field and fresh permissions/restrictions in
one server-side operation. A changed ticket, project, hours, billing, opaque field,
managed note, revision, lock or invoice state must prevent overwriting the checked
record. Preserve billing and factual fields; avoid whole-record replacement with
an incomplete export. If the API cannot offer atomic conditional updates or enforce
current permissions/locks, this integration cannot promise race-free application.
A second read followed by an unconditional write does not repair that limitation.

Keep note and numeric write journals separate, keyed by record ID, confirmed plan
digest and exact field/value. Privately record before revision/snapshot, expected
target, conditional outcome and reread value. After timeout or interruption, first
reread: the write may have committed without its response. If the exact requested
field/value is already present, record that outcome without appending another note.
If anything else changed, re-export and replan; do not reuse stale numeric or note
snapshot conditions. Server revision increments may require replanning even after
an expected note write. Do not infer numeric idempotence from `planned-note-match`.

No cross-entry or note-plus-EHE transaction is promised by EH. If a partially
applied batch is interrupted, keep its partial outcome explicit and reconcile it
against the entire selected conserved period before proceeding. Do not claim a
complete refresh, redistribute blocked shares, blindly rollback someone else's
edits, or resume from file existence. Conditional idempotency tokens can supplement
these checks where the target API supports them; they do not replace permissions.
The example demonstrates a single atomic in-memory note operation, reread/retry,
and a later race being rejected. It intentionally leaves live transport, numeric
field mapping and multi-entry transactions to the external consumer.

## Verification boundary

The synthetic example is schema/semantic validated and reproduced by physical CLI
tests, with a separate in-memory conditional adapter exercise. Existing actual
conflicting multi-commit rebase, squash/cherry-pick, cross-period and DST fixtures,
partial provider resume, cancellation/budget/cache failure tests are retained.
See the [verification map](HISTORICAL_NOTE_REFRESH.md#verification-map-and-remaining-boundary)
and the recorded [annual/network measurements](HISTORICAL_NETWORK_BENCHMARK.md).
These are policy/coverage checks, not effort calibration or live timeinv validation.
