# Host-review measurement and budget admission

## Current status

The implemented measurement identities are:

- session contract: `host-review-measurement/1.0.0`; and
- comparison metrics: `host-review-comparison-metrics/1.0.0`.

They record reviews performed by a surrounding caller and compare compact packet
review with broader authorized source review. They do not call a provider, apply
an adjustment, calibrate the estimator, or select a default budget.

The first public diagnostic contains three repository families and six sanitized
measurements. It improved item/category agreement but worsened repository-total
agreement. Complete provider token, elapsed, cost, and paired-context telemetry
was unavailable, so no context-savings or automatic-review claim is admitted.

## Objective

Measure whether `host-review/1.0.0` packets and bounded queries reduce context,
time, tokens, or cost while retaining useful agreement with a broader-source
review.

The local estimate is the baseline. One session sees the compact packet and its
explicit queries; a second may inspect broader authorized source. Both decide
every packet candidate. The broader-source review is a comparison reference, not
ground truth, historical labor, or an independently calibrated label.

## CLI surface

```text
eh review measure <packet.json> <adjustment.json>
  --subject <opaque-id>
  --session <opaque-id>
  --context <compact|broader-source>
  [--query-result <query-result.json>]...
  [--elapsed-ms <integer>]
  [--provider-input-tokens <integer>]
  [--provider-output-tokens <integer>]
  [--provider-cached-input-tokens <integer>]
  [--token-basis <text>]
  [--cost <amount> --currency <code> --cost-basis <text>]
  [--additional-input-bytes <integer>]
  [--additional-input-characters <integer>]
  [--additional-input-basis <text>]
  [--input-complete]
  [--source-seen-before]
  [--reference-seen-before]
  [--independent-reviewer]
  [--note <text>]...
  [--compact]
  [--output <path>]

eh review benchmark <measurement.json>...
  [--compact]
  [--output <path>]
```

`measure` validates and records a review the caller has already conducted. Missing
tokens, elapsed time, cost, or context size remains explicitly unavailable rather
than being estimated or replaced with zero.

`benchmark` groups measurements by opaque subject. Each subject requires exactly
one compact and one broader-source measurement over the same packet and candidate
set.

## Measurement artifact

A session record contains:

- opaque caller-supplied subject and session IDs;
- protocol, estimator, profile, and exact input digest;
- context mode and available provider/model/version identity;
- source-order, reference-anchoring, and independence conditions;
- canonical packet, adjustment, and query-result digests;
- UTF-8 bytes, Unicode characters, and an explicitly approximate
  `ceiling(characters / 4)` indicator for observed payloads;
- query kind, size, and excerpt presence without selector, reason, path, or
  content;
- caller-reported provider tokens, elapsed milliseconds, and cost when available;
- numeric size and basis for additional context when supplied;
- whether complete review input was accounted for;
- one normalized decision for every packet candidate; and
- baseline/reviewed category and repository totals derived from those decisions.

The four-character indicator is never labeled as exact provider tokens. Cost
requires one three-letter currency per measurement; EffortHours does not convert
currencies or infer price from model identity.

Payload byte counts describe decoded JSON text re-encoded as UTF-8 without a byte-
order mark. Canonical identity ignores insignificant JSON formatting, while
payload size includes it.

## Complete decision coverage

The general adjustment protocol permits a partial ledger. A benchmark measurement
requires exactly one `affirm` or `replace` decision for every candidate. This
distinguishes reviewed agreement from omission or abandonment.

An affirmation retains the baseline category and range. A replacement supplies
the reviewed category and range. The measurement engine projects totals by
subtracting the original candidate range and adding the reviewed range.

This projection is for comparison only. It does not rewrite the canonical
estimate, allocate hours back to sliced work items, calculate pricing, or admit an
adjustment into a model.

## Agreement metrics

The broader-source session is named the reference, never truth. Low, expected, and
high agreement is reported at:

1. packet capability items;
2. complete represented-effort categories; and
3. repository totals.

Each level reports sample count, reference/candidate hours, total and mean absolute
error, signed error, WAPE when defined, and aggregate bias when defined. Interval
diagnostics report reference-expected coverage, full-reference-range coverage,
and any-range overlap.

Each level compares both the unchanged local baseline and compact review with the
broader-source reference. Reports also show absolute correction from baseline,
reduction in expected absolute error, and reduction rate where defined. A negative
reduction means compact review moved farther from the reference.

Categories and repository totals include the complete baseline; item comparison
covers the bounded packet queue. These are agreement diagnostics, not probability
calibration.

## Context, latency, tokens, and cost

Known packet/query sizes remain visible even when full context accounting is
incomplete. A session is complete only when the caller attests that all review
input is represented. Prior source or reference context also requires supplied
additional byte and character counts.

Input ratios are emitted only when both paired sessions are complete and the
broader-source denominator is positive. Otherwise ratios remain null with an
explicit diagnostic.

Provider tokens, elapsed time, and cost are compared only when both sessions
report compatible values. Cost ratios require the same currency. Missing or
incompatible telemetry remains null; it is never treated as zero.

Additional source size must describe material genuinely exposed during the
session, not total repository size by convenience.

## Privacy boundary

Measurement and benchmark artifacts do not copy:

- packet repository descriptors or source digests;
- prompts or chat transcripts;
- query selectors or reasons;
- adjustment rationales or evidence IDs;
- source paths, excerpts, or contents; or
- local filesystem paths.

They retain opaque IDs, canonical digests, query kinds, numeric telemetry,
normalized ranges/categories, disclosed model identity, telemetry bases, and
caller notes. Caller text is retained verbatim, so the caller must keep IDs,
bases, and notes non-sensitive and decide whether derived measurements may be
shared.

Public benchmarks may use only redistributable sources with recorded provenance.
Private measurements remain outside the repository.

## Public diagnostic

The reproducible `benchmarks/host-review/public-expansion/0.1.0` artifact compares
three exact MIT release snapshots. Packet sizes range from 39,084 to 72,697 UTF-8
bytes; each compact session uses one capability query.

Across 30 candidate items, compact review reduced expected-hour mean absolute
error against the broader-source reference from 3.7417 to 1.2917 hours. Across 33
categories, it reduced that measure from 3.3106 to 1.1742 hours. Across three
repository totals, it worsened mean absolute error from 4.9167 to 7.4167 hours.

The result was not blind: the compact host session had broader source and
aggregate reference information available, and the reference came from one host-
AI teacher without independent correction. Provider tokens, elapsed time, cost,
complete surrounding context, and older teacher-session source sizes were not
available. This validates the measurement path and records mixed accuracy; it does
not prove savings.

## Budget admission

A default packet, query, token, time, cost, or automatic-review budget requires:

- multiple repository families in every relevant ecosystem or shape;
- repeated measurements across multiple models or genuinely independent
  reviewers;
- exact telemetry for each provider-specific claim;
- material improvement at item, category, and total levels rather than context
  reduction alone;
- no systematic degradation in low-confidence or high-value cases; and
- a separately reviewed, versioned admission decision.

The structural ceilings in `HOST_REVIEW.md` remain safety bounds, not model
budgets. The current implementation leaves every automatic budget unselected.
