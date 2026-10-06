# Retained workday evidence review

`retained-workday-review/1.0.0` compares explicit external work records with a
saved complete, jointly normalized, single-contributor calendar-day comparison.
It is an offline diagnostic projection, not an estimator, workday recovery,
causal credit model, or timesheet writer. EHE remains experimental and
uncalibrated. Original workdays stay unresolved even where retained attribution
is available. Integration labor with no novel retained artifact delta requires
separate external assessment; this view never invents replacement effort for it.

## Inputs and command

Save a whole-local-day native historical or explicit manifest comparison first.
The source must embed its complete portfolio and have valid recomputed portfolio
and semantic digests. Partial days, independent-day or isolated series, shared
multi-contributor selections, incomplete discovery and tampered source artifacts
are rejected before values are produced. The same guard governs `allocate-days`.

```text
eh change review-days period.json --work-records records.json --output review.json
eh change review-days period.json --work-records records.json --entry-policy equal-matched-entries/1.0.0 --format markdown
```

Supply a `change-work-record-manifest` with the exact source semantic digest:

```json
{
  "schemaVersion": "1.0.0",
  "sourceSemanticDigest": "sha256:<replace with the source verification.semanticDigest>",
  "records": [
    { "recordId": "entry-1", "date": "2026-01-19", "kind": "implementation", "repositoryIds": ["project"], "loggedHours": 4 },
    { "recordId": "entry-2", "date": "2026-01-20", "kind": "implementation", "repositoryIds": ["project"], "loggedHours": 12 },
    { "recordId": "entry-3", "date": "2026-01-21", "kind": "meeting", "loggedHours": 2 },
    { "recordId": "entry-4", "date": "2026-01-22", "kind": "pto" },
    { "recordId": "entry-5", "date": "2026-01-23", "kind": "mixed", "repositoryIds": ["project"] }
  ]
}
```

The placeholder is not valid input. Record and repository IDs are caller-approved
public IDs, not paths, provider names, tickets, notes or identities. Records have
unique IDs, real in-period dates and one explicit kind: `implementation`,
`meeting`, `pto` or `mixed`. Multiple records may share a date. Supply 1-4096
records in at most 1 MiB; each relationship list has at most 256 unique IDs.
Optional logged hours are 0-24 and bind the input digest as context only.

Implementation records must declare repository relationships. For this aggregate
review each must name exactly the source's entire repository set; a subset or
out-of-scope relationship yields `repository-scope-unresolved`, without assigning
the whole portfolio to one project. Relationships spanning several repositories
are explicit aggregate declarations, not cross-repository deduplication or a
recovered causal link. A project-specific review needs a separately selected,
complete source with the intended aggregate scope. Independent source totals must
not be joined to simulate a jointly reconciled portfolio.

Mixed-ticket or mixed implementation/non-implementation entries use `mixed` and
remain unresolved until their relationship is clarified externally. Meetings and
PTO remain visible, excluded records without EHE values. Neither their hours nor
absence of source commits proves zero labor. An implementation record cannot be
omitted merely to hide a discrepancy. Records outside the frozen period require
a new complete source; the command never shifts them to a retained commit date.

## States and values

Each day preserves source attributed expected EHE, retained evidence state,
public record IDs/kinds/relationships, and `unresolved-original-workday`.
It does not redistribute the source portfolio over blank dates.

| Review state | Meaning |
| --- | --- |
| `retained-attribution-available` | Positive retained source attribution and implementation records for exactly the declared aggregate scope; this does not verify original workdays. |
| `unresolved-workday` | Implementation records exist but source expected attribution is zero, including a complete empty retained selection. |
| `missing-work-record` | Positive retained attribution has no implementation record. |
| `repository-scope-unresolved` | An implementation relationship differs from the source aggregate scope. |
| `mixed-work-records-unresolved` | Mixed entries prevent unambiguous daily matching. |
| `no-implementation-record` | Neither positive expected attribution nor implementation records exist; no zero-labor claim follows. |

Mixed and scope discrepancies block values for the whole day so remaining entries
cannot silently absorb an unresolved share. Non-implementation records use
`excluded-non-implementation`. Native `no-retained-change`, `scope-excluded`,
`normalized-zero` and `reconciled-zero` states remain distinct. Offline manifest
reports without that richer native metadata expose only a conservative retained
selection/reconciliation classification; absent intermediate history is unknown.

By default every multiplier and contribution is unavailable. Only explicit
`--entry-policy equal-matched-entries/1.0.0` allocates matched positive retained
attribution. The daily multiplier is `round(sourceExpectedEHE / 8, 2)`, midpoint
away from zero, independent of logged durations and the source's optional capacity
choice. Divide its integer hundredths equally among the day's implementation
records; residual hundredths go to ascending public record IDs. Contributions are
two-decimal values and sum exactly to the rounded matched daily multiplier.
For example, 19.75 EHE yields 2.47, split across three entries as 0.83/0.82/0.82.
This is an explicit presentation policy; allocation weights never use logged time.
Low/high remain in the source artifact, not saved per-entry midpoint values.

Unresolved and excluded records have no contribution, including no fabricated
zero. A tiny positive matched EHE can round to zero under this explicit policy;
its retained evidence and unresolved original-workday state remain visible, so it
still cannot be described as genuine zero work. The report uses `unresolved` when
any discrepancy exists, otherwise `reviewed-retained-attribution`; neither state
certifies a timesheet or actual workdays.

## Safety, compatibility and verification

The new v1 schemas are separate from existing workday declarations/allocation.
Input order does not change review output or canonical input digest. Changing
logged context changes only that digest, not matching, weights or values. Source
semantic/portfolio and work-record digests retain audit lineage. All output is
schema- and semantically validated. Diagnostics use stderr; JSON or Markdown
uses stdout or a new output file. Existing output/input files are protected and
new outputs are written atomically. No Git, network, estimator rerun or external
entry writes occur. Invalid or incomplete sources exit nonzero without a review
artifact or saved zero explanation; a successful unresolved review exits zero
because the diagnostic itself completed, so consumers must inspect the states.

Keep source and work-record artifacts private where necessary. Consumers preserve
logged hours, notes, tasks, projects and billing status and must not overwrite them
from an unresolved or incomplete result. The explicit conserved date projection
in [WORKDAY_ALLOCATION.md](WORKDAY_ALLOCATION.md) remains a separate opt-in;
review discrepancies do not automatically authorize spreading a squash aggregate.

Memory-only tests verify discrepancy states, missing/mixed/scope relationships,
fixed-eight matching with twelve-hour source capacity, logged-hour invariance,
canonical two-decimal remainders, order, DST, complete empty selections, digest
rejection and incomplete/tampered inputs. Native physical CLI tests verify schemas,
Markdown, stdout separation, privacy and unchanged/protected input/output files.
These are deterministic policy checks, not empirical workday calibration.

## Safe historical refresh planning

The offline [historical note refresh plan](HISTORICAL_NOTE_REFRESH.md) preserves
original entry snapshots, creates one idempotent managed annotation, and keeps
note/EHE permissions and locked/invoiced states separate. It never edits entries.
Retained reviews expose `attributionCompleteness`; missing declared dates/baselines
block retained-date entry allocation with `unresolved-event-attribution`. Execution completion
and retained zero values do not certify original workdays or zero labor.

## Explicit declared-workday review

`declared-workday-review/1.0.0` connects the existing saved-source date allocation
to work-record review. Opt in with `--workdays <workdays.json> --workday-policy
equal-declared-days/1.0.0`. The command recomputes both retained review and
allocation from the original complete source; saved allocation/review values are
never trusted as inputs. A declaration's record ID must identify an implementation
record on that exact local date with the source's entire repository scope. Every
implementation date must be declared. Mixed records, missing anchors, incorrect
dates/scopes, partial declarations and incomplete sources fail before a projection.
Meeting/PTO records stay excluded. Existing retained review remains the default.

The review embeds a canonical `workdayResolution` declaration/allocation receipt
with exact source, portfolio, record and declaration digests. Each day preserves
its original source expected EHE and retained evidence, separately exposing
`allocatedExpectedHours` and `workdayEvidenceBasis: external-work-record` or
`not-declared`. Original Git workdays and intermediate history remain unresolved;
source event-date/baseline warnings are retained. A declared date is externally
supplied evidence, never a recovered timestamp or causal event proof.

Optional `--entry-policy equal-declared-day-entries/1.0.0` rounds the complete
period's expected EHE/8 once to two decimals, midpoint away from zero, then divides
integer hundredths equally across sorted declared dates and each date's sorted
implementation IDs. Thus all matched entry values sum to that single rounded
period multiplier. Logged hours and capacity never weight these values. This is
a separate policy from rounding retained daily values independently. For 98.75
expected EHE over five declared dates, the period multiplier is 12.34; the daily
shares are 2.47/2.47/2.47/2.47/2.46, not five independently rounded 2.47 values.
The existing category/range allocation still conserves every source hour exactly.

Declared positive-period rows use `declared-workday-allocation`; an empty retained
period uses `declared-workday-no-retained-effort` and supplies no entry values.
Undeclared rows use `no-declared-workday`. No zero cell establishes zero labor.
Without the explicit new entry policy all multiplier fields remain unavailable.
The retained entry policy cannot be combined with a declared-date projection.

`plan-refresh` accepts the same declaration/policy options and recomputes this
review. Note proposals label external dates and declaration lineage. Numeric
refresh must select every contributing entry across the declared period so a
partial range cannot present itself as a conserved refresh. Notes-only subsets
remain allowed. Independent permissions, locks/invoices, untouched snapshots,
idempotent annotations and separate confirmation before any real write still apply.

For example, use the same complete source and declaration for both commands:

```text
eh change review-days period.json --work-records records.json --workdays workdays.json --workday-policy equal-declared-days/1.0.0 --entry-policy equal-declared-day-entries/1.0.0 --output declared-review.json
eh change plan-refresh period.json --work-records records.json --workdays workdays.json --workday-policy equal-declared-days/1.0.0 --entry-policy equal-declared-day-entries/1.0.0 --entries refresh-manifest.json --fields both --output refresh-plan.json
```

Use `--fields notes` for a notes-only subset. These commands read saved artifacts
and create new review/plan files; applying a plan to real records is separate.
