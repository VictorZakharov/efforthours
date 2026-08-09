# Milestone 8 measurement checkpoint

## Status

Designed and implemented on August 9, 2026 for issue #27. This checkpoint adds a
provider-neutral way to record host-review sessions and compare compact review
with broader-source review. The first public diagnostic contains three repository
families and six sanitized measurements. It does not call a provider, apply an
adjustment to a canonical estimate, calibrate `seed-rules/0.2.1`, or choose a
default AI budget.

The measurement identities are:

- session contract: `host-review-measurement/1.0.0`; and
- comparison metrics: `host-review-comparison-metrics/1.0.0`.

## Objective

Measure whether the `host-review/1.0.0` packet and bounded queries reduce the
context, elapsed time, and monetary cost needed to reach a strong logical review,
and whether any savings retain useful agreement with a broader-source review.

The local estimate is the baseline. One reviewer session sees the compact packet
and only explicitly requested query results. A second session may inspect a
broader authorized source context. Both sessions decide every candidate in the
same packet with `affirm` or `replace`. The broader-source result is a comparison
reference, not historical labor, ground truth, or an independently calibrated
label.

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

`measure` never performs the AI review. It validates and records a review that the
surrounding caller already conducted. Missing provider tokens, elapsed time, or
cost are represented explicitly as unavailable rather than estimated or replaced
with zero.

`benchmark` groups measurements by opaque subject ID. Each subject requires
exactly one compact and one broader-source measurement over the same packet input
and candidate set. A report may contain several subjects and provides both
per-subject and aggregate agreement metrics.

## Measurement artifact

A measurement records:

- an opaque caller-supplied subject ID, never the packet's repository name;
- protocol, estimator, profile, and exact input digest;
- context mode and available provider/model/version identity;
- an opaque session ID plus source-order, reference-anchoring, and paired-review
  independence conditions;
- canonical packet, adjustment, and query-result digests;
- UTF-8 bytes, Unicode characters, and the explicitly approximate
  `ceiling(characters / 4)` token indicator for each observed payload;
- query kind, payload size, and whether the query returned an explicit source
  excerpt, without its selector, reason, path, or content;
- caller-reported provider input, output, and cached-input tokens when available;
- caller-reported elapsed milliseconds and monetary cost when available;
- whether numeric size for additional context was supplied, without that context
  itself;
- whether the observed-input byte/character/token accounting is complete;
- one complete normalized decision for every packet candidate; and
- baseline and reviewed category and repository totals derived from those
  decisions.

The four-character token indicator remains provider-neutral and is never labeled
as exact. Exact provider token telemetry retains its caller-supplied basis. Cost
requires one three-letter currency per measurement; EffortHours does not convert
currencies or infer prices from a model name.

The packet and query byte counts describe the exact decoded payload text supplied
to `measure`, re-encoded as UTF-8 without a byte-order mark. Canonical digests are
independent of insignificant JSON whitespace. Terminal newlines therefore affect
payload size but not identity.

## Complete decision coverage

The general adjustment protocol permits a partial ledger. A benchmark measurement
is intentionally stricter: it requires exactly one decision for every candidate in
the packet. An unchanged candidate must be explicitly affirmed. This distinguishes
reviewed agreement from an omitted or abandoned review.

For each decision:

- baseline category and range come from the packet;
- an affirmation keeps both unchanged; and
- a replacement supplies its reviewed category and range.

The measurement engine projects category and repository totals by subtracting the
candidate's exact original range and adding its reviewed range. This projection is
for experiment comparison only. It does not rewrite a canonical estimate, allocate
hours back to sliced work items, calculate pricing, or admit the adjustment into a
model.

## Comparison metrics

The broader-source measurement is the named reference for one experiment. Metrics
must call it a reference, not truth.

Agreement is reported at three levels:

1. candidate capability items;
2. complete represented-effort categories; and
3. repository totals.

For low, expected, and high points, each level reports:

- sample count;
- reference and candidate hours;
- total and mean absolute error;
- signed error;
- weighted absolute percentage error when the reference denominator is nonzero;
  and
- aggregate bias when the reference denominator is nonzero.

Interval diagnostics report reference-expected coverage, full-reference-range
coverage, and any-range-overlap rates. These are agreement diagnostics, not
probability calibration.

Each level compares both the unchanged local baseline and the compact review with
the broader-source reference. It also records:

- absolute expected-hour correction from baseline to compact review;
- absolute expected-hour correction from baseline to broader-source review;
- reduction in absolute expected error; and
- the reduction rate when baseline error is nonzero.

A negative reduction means compact review moved farther from the broader-source
reference. Category and total comparisons include the entire baseline, not only
the selected candidates; candidate-item comparisons cover the packet's bounded
review queue.

## Context, latency, token, and cost comparison

Every subject comparison reports compact and broader-source totals for measured
input bytes, characters, and approximate tokens plus a completeness flag. Input
accounting is complete only when the caller explicitly attests that the packet,
queries, and any additional input sizes cover the whole review input. A session
with prior source or reference context can be marked complete only when both bytes
and characters for that additional context were supplied. Input ratios are
emitted only when both sessions are complete and the broader-source denominator is
positive. Otherwise the known payload totals remain visible, the ratios stay
null, and an explicit diagnostic prevents unrecorded context from appearing free.

Provider tokens, elapsed time, and cost are compared only when both sessions
reported compatible values. Cost ratios require the same currency. Missing or
incompatible telemetry stays null with an explicit diagnostic; it is never
silently treated as zero.

Additional broader context is caller-reported numeric metadata. For a full-tree
review it can describe the maintained text actually exposed to the reviewer. It
must not be the total repository size unless that complete material was genuinely
made available in the measured session.

## Privacy boundary

EffortHours does not copy the following repository-derived material into
measurement or benchmark artifacts:

- packet repository descriptor or source digest;
- prompt or chat transcript;
- query selector or reason;
- adjustment rationale or evidence IDs;
- source path, excerpt, or file content; or
- local filesystem path.

They retain opaque subject/session IDs, canonical digests, query kinds, numeric
telemetry, normalized ranges/categories, disclosed model identity, caller-supplied
telemetry bases, and caller-supplied condition notes. The privacy object states
both the non-copying boundary and that caller text is retained verbatim. The caller
must keep every ID, basis, and note non-sensitive and decide whether even derived
measurements may be disclosed. EffortHours does not inspect arbitrary caller text
and cannot make it anonymous automatically.

Public EffortHours benchmarks may use only redistributable source snapshots with
recorded provenance. Private measurements must remain outside the repository.

## Representative experiment policy

An initial checkpoint should exercise at least:

- one .NET repository;
- one JavaScript or TypeScript repository;
- one materially different repository shape or mixed ecosystem;
- small and nontrivial packet sizes; and
- at least one session that uses a follow-up query.

Compact review must be recorded before broader-source inspection when the same
reviewer performs both. Reports disclose shared-reviewer, order, anchoring, and
independence limitations. A single host, model, workstation, or repository per
shape is a diagnostic, not a representative population.

The public artifact stores only sanitized measurement and comparison JSON plus a
provenance/method document. Source archives and prompts are not committed.

## Public checkpoint outcome

The `public-expansion/0.1.0` checkpoint under `benchmarks/host-review` compares
three exact MIT release snapshots: one .NET library and two JavaScript/TypeScript
libraries. Canonical newline-free packet sizes range from 39,084 to 72,697 UTF-8
bytes. Each compact session uses one capability query, adding 2,865 to 5,280
bytes.

Across 30 candidate items, compact review reduced expected-hour mean absolute
error against the broader-source reference from 3.7417 to 1.2917 hours. Across 33
categories it reduced the same metric from 3.3106 to 1.1742 hours. Across three
repository totals it worsened mean absolute error from 4.9167 to 7.4167 hours.

This is not a blind result. The compact host session had broader source and
aggregate reference information available before deciding, and the frozen
broader-source targets came from one host-AI teacher without independent
correction. Exact provider tokens, elapsed time, monetary cost, the compact
session's full surrounding-context size, and the older teacher sessions'
additional source-context sizes were unavailable. Those fields and ratios remain
explicitly unavailable. The checkpoint therefore establishes the measurement path
and a mixed accuracy diagnostic, not context or cost savings.

## Budget admission

The benchmark report always records whether a default budget was selected. The
first implementation leaves it false.

A future default packet, query, token, time, cost, or automatic-review budget
requires all of the following:

- multiple repository families in each relevant ecosystem/shape;
- repeated measurements across more than one model or genuinely independent
  reviewer;
- exact provider telemetry for the provider-specific claim being made;
- material improvement at item, category, and total levels rather than context
  reduction alone;
- no systematic degradation of low-confidence or high-value cases; and
- a separately reviewed, versioned admission decision.

Structural safety ceilings in `host-review/1.0.0` remain security and bounded-input
limits. They are not model budgets and are not changed by this checkpoint.

## Testing and verification

Ordinary unit tests remain storage-independent. They construct packets, ledgers,
query results, payload strings, and measurements entirely in memory and cover:

- exact payload size and canonical digest behavior;
- complete-decision enforcement;
- category moves and total reconciliation;
- unavailable and reported provider telemetry;
- prompt/source/path omission from serialized measurements;
- item, category, and total agreement metrics;
- negative as well as positive review improvement;
- multi-subject aggregation and currency compatibility;
- deterministic JSON and schema validation; and
- cancellation where loops are nontrivial.

The process-level suite owns physical JSON files, stdout/stderr behavior, exit
codes, and explicit output paths. No test or benchmark calls a provider.

The August 9 implementation gate passed formatting verification, a zero-warning
Release build, 138 memory-only unit tests, 39 process-level end-to-end tests,
deterministic regeneration of the six public measurements and benchmark, and
creation plus isolated local-tool installation of
`EffortHours.Tool.0.9.0-alpha.1.nupkg`. The installed package validated the six
measurements with its embedded schemas and reproduced the committed benchmark
byte-for-byte after the generator's documented terminal-newline normalization.

## Exit condition

Issue #27 is complete only when:

- both public contracts and their schemas are implemented;
- the CLI records and compares deterministic sessions without provider access;
- representative public measurements compare local baseline, compact review, and
  broader-source review;
- item, category, total, payload, query, elapsed, token, and cost availability are
  reported honestly;
- no private source or prompts enter public artifacts; and
- any decision to retain or defer default budgets is justified from the recorded
  evidence.

The implementation and `public-expansion/0.1.0` diagnostic satisfy this checkpoint
boundary. The recorded evidence defers every default budget. Blind multi-model
runs with exact provider and context telemetry remain necessary before a savings
claim or automatic-review policy can be admitted.
