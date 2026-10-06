# Explicit declared workday allocation

`equal-declared-days/1.0.0` is an optional presentation allocation over an already
complete, jointly deduplicated Change portfolio. The maintainer explicitly opted
into this separate policy. It is not an estimator, recovered history, actual
labor, productivity, personal credit, or a timesheet. EHE remains experimental
and uncalibrated; source estimator identities and numerical priors are unchanged.

## Inputs and command

First save a complete single-contributor author-period comparison with ordinary
`calendar-day` buckets, joint normalization, and whole local calendar days. Native
`eh change period --breakdown day` JSON also qualifies. Incomplete discovery,
independent-day reconciliation, isolated contributor series, partial days, and
multi-contributor/shared-credit inputs are rejected. These limits avoid assigning
joint credit to an individual or treating independent daily totals as a
period-wide deduplicated total.

Copy its exact `verification.semanticDigest` into an external declaration file:

```json
{
  "schemaVersion": "1.0.0",
  "sourceSemanticDigest": "sha256:<64 lowercase hexadecimal characters from the source report>",
  "workdays": [
    { "recordId": "record-1", "date": "2026-01-19", "loggedHours": 4 },
    { "recordId": "record-2", "date": "2026-01-20", "loggedHours": 12 },
    { "recordId": "record-3", "date": "2026-01-21" },
    { "recordId": "record-4", "date": "2026-01-22" },
    { "recordId": "record-5", "date": "2026-01-23" }
  ]
}
```

The digest placeholder must be replaced; it is not a valid manifest value.
Record IDs are caller-approved public IDs. Do not include notes, identities,
tickets, paths, or source excerpts. Declare 1 through 512 unique dates and unique
record IDs. Dates must be real `yyyy-MM-dd` local dates inside the complete source
period. Optional logged hours range from zero to 24 and are context only.
Contradictory or duplicate records must be resolved externally; the tool does not
silently combine them. Dates outside the source period require a new complete
source calculation rather than pulling effort across a cutoff.

```text
eh change allocate-days period.json --workdays workdays.json --policy equal-declared-days/1.0.0 --output allocated.json
eh change allocate-days period.json --workdays workdays.json --policy equal-declared-days/1.0.0 --format markdown
```

The command reads saved artifacts only, performs no Git/provider access or
estimator rerun, and never mutates its inputs or external time entries. Output
paths must be new files; existing outputs are also protected. Source JSON is
bounded by the existing 512-MiB comparison envelope and declarations by 1 MiB.
Both inputs and JSON output are checked against their public schemas. Semantic
validation and recomputation of the complete source and portfolio digests reject
stale or tampered bindings. Failures exit nonzero without an allocated aggregate.

## Conserved arithmetic and evidence states

Each source category's low, expected, and high cent-hour values are divided
uniformly across the sorted declared dates. Integer-cent largest remainders go
to earlier dates. Category allocations sum into each day; every category and
all three source totals are conserved exactly. Undeclared source dates remain
in the output with zero *allocated* EHE. That zero is not proof of zero labor.
Changing declaration order does not change the output; changing logged durations
does not change allocations, ratios, or capacity.

For example, 98.75 expected EHE over five declared dates allocates 19.75 expected
hours per date. An existing eight-hour daily reference remains eight on every
source date, including undeclared dates. Five source dates retain 40 reference
hours overall. Neither four nor twelve logged hours replaces that denominator.
Each allocated ratio is allocated EHE divided by the preserved reference
capacity, rounded to six decimal places, midpoint away from zero. Absent capacity
remains absent. DST days use the source timezone and whole local boundaries,
including 23- and 25-hour days; elapsed clock duration does not change weights.
An unavailable source timezone fails as an input error without allocation.
Allocated Markdown reports name the source timezone used for local dates.

Every output day carries `status: allocated` and
`originalWorkdayStatus: unresolved-original-workday`. Its optional `recordId`
identifies the external declaration; it does not certify that Git recovered the
date. `sourceAttributedEffort` preserves the original source bucket allocation
separately from `allocatedEffort`. The source may contain retained timestamps or
explicit declared rewrite events; this policy does not reinterpret their proof.
Keep the original source report and declaration file alongside the projection
for audit. Source, portfolio, and canonically ordered declaration digests retain lineage.
Logged-hour context binds the declaration digest but is never an effort signal.

Lost intermediate commits remain lost. A squash retained on one author date and
external declarations on several other dates can coexist: original workdays stay
unresolved while the user-selected allocation is visible. There is no automatic
redistribution, fitted multiplier, or conflict-resolution bonus. For supported
immutable old/new pairs, use [declared rewrite event attribution](REWRITE_EVENT_ATTRIBUTION.md)
first to distinguish replay from novel retained resolution. This coarser workday
allocation is a separate opt-in view and does not replace that causal evidence.

## Verification boundary

Memory-only cases check exact category/range conservation, deterministic cent
remainders, declaration order, wrong digests, malformed/out-of-period dates,
zero complete portfolios, and DST. Physical CLI cases check native historical
sources, four/twelve-hour context with fixed-eight reference capacity, schema
validation, stdout separation, unchanged input files, and protected output paths.
This is deterministic allocation verification, not empirical workday calibration.

## External work-record discrepancy review

[WORKDAY_REVIEW.md](WORKDAY_REVIEW.md) defines the separate offline `eh change
review-days` diagnostic. Blank retained dates, missing implementation records,
mixed entries and mismatched repository relationships stay unresolved. Optional
explicit entry allocations conserve the rounded daily expected EHE/8 multiplier;
logged durations never set weights, and no external entries are overwritten.
