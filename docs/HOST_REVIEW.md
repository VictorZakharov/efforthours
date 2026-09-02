# Provider-neutral host review

## Current status

`host-review/1.0.0` is an implemented optional protocol around a complete local
repository estimate. It creates rate-free review packets, supports digest-bound
bounded queries, and validates evidence-backed adjustment ledgers.

EffortHours does not select or call an AI provider, transmit repository material,
apply reviewed adjustments, calibrate the seed model, or select an automatic
review budget. The local estimate remains complete without host review.

## Semantic boundary

Host review is optional adjudication of consequential uncertainty. It is not part
of the EHE definition and never represents actual hours worked.

The local estimator runs first without pricing. The full estimate and repository
evidence remain canonical. A host packet is a lossy projection for review;
monetary values are excluded so they cannot anchor effort judgment.

An adjustment document is a proposed traceable ledger. The current implementation
checks identity, evidence, and structure but does not rewrite an estimate,
redistribute capability totals, change confidence, or recalculate pricing.
Applying adjustments requires a separate explicit admission policy.

The surrounding client controls provider choice, authorization, disclosure,
privacy, retention, model identity, session context, and cost.

## CLI surface

```text
eh review packet <repository-or-evidence.json>
  [--profile <implementation|recreation>]
  [--model <id> [--provider <id>] [--model-version <id>]]
  [--compact]
  [--output <path>]

eh review packet --repo <owner/name>
  [--revision <revision>]
  [--fetch-missing]
  [packet options]

eh review query <repository-or-evidence.json>
  --input-digest <sha256:digest>
  [--profile <implementation|recreation>]
  (--capability <id> | --evidence <id> | --scope <scope> |
   --source <repository-relative-path>)
  --reason <text>
  [--offset <number>]
  [--limit <number>]
  [--start-line <number>]
  [--line-count <number>]
  [--compact]
  [--output <path>]

eh review query --repo <owner/name>
  [--revision <revision>]
  [--fetch-missing]
  --input-digest <sha256:digest>
  <selector and query options>

eh review validate <packet.json> <adjustment.json>
  [--compact]
  [--output <path>]
```

Packet and non-source query commands accept a repository directory, saved
repository evidence, or checkout-free immutable GitHub snapshot. Selected-source
queries require a repository directory or checkout-free snapshot so admitted
bytes remain available through the analyzed filesystem.
Machine-readable output uses stdout unless `--output` is explicit; diagnostics
use stderr.

Model/provider/version options record identity supplied by the surrounding
session. A model ID is required before provider or version metadata is accepted.
No identity is invented by default.

## Input identity

Each packet's `inputDigest` is SHA-256 over canonical compact JSON containing:

1. the complete rate-free `EstimateReport`; and
2. the complete `RepositoryEvidence` used to produce it.

Insignificant JSON whitespace and property order do not change identity. Any
material evidence, work item, profile, baseline, estimator, assumption,
verification, or diagnostic change does.

Every query and adjustment repeats the exact digest. Queries are rejected before
returning details when regenerated input does not match. Validation rejects a
packet/ledger mismatch. If a packet records a reviewer model identity, the ledger
must repeat it exactly.

Repository `sourceDigest` is still present for artifact identity but is not enough
for host-review identity because it does not cover estimator output or profile.

## Review packet

The packet reuses the deterministic bounded review projection described in
`REPORTING.md`. It includes at most 12 material or uncertain capability groups and
records:

- repository, profile, baseline, estimator, and estimator references;
- total and category EHE without prices;
- each candidate's local range, confidence, and selection reason;
- estimator reasoning, assumptions, exclusions, uncertainty, and evidence IDs;
- a compact union of referenced evidence facts and explicit omission counts;
- diagnostics and static verification state;
- supported query kinds and structural ceilings; and
- caller responsibilities for provider selection and disclosure.

Packets contain no source excerpt. At most 48 referenced evidence facts are
included. Omitted fact IDs stay visible and can be requested explicitly. The fact
and candidate caps are bounded-input safeguards, not token or monetary budgets.

Packets state that host review is optional and that the bundled repository
estimator remains experimental and uncalibrated.

## Follow-up queries

Every query records its kind, selector, reason, profile, and expected input digest.
The result repeats the verified query and digest.

| Kind | Result and bound |
| --- | --- |
| `capability` | One capability, matching work-item reasoning, and at most 48 referenced evidence facts |
| `evidence` | One exact fact by stable ID |
| `scope` | A deterministic page of at most 20 capabilities for one exact scope |
| `selected-source` | One explicitly requested admitted-source line window |

Scope pagination uses a zero-based offset and positive limit and reports omitted
counts. Capability and evidence queries do not paginate.

Selected-source queries require a positive start line and count and return no more
than 200 lines or 64 KiB of decoded text. Files larger than 1 MiB are refused.
These are untrusted-input safety ceilings, not context budgets.

Selected-source admission is fail-closed:

- the selector is repository-relative and remains under the analyzed root;
- it identifies a regular file already represented by a scanner `file:` fact;
- the root, ancestors, and file are not links or reparse points;
- current bytes match the digest recorded during the query's scan;
- binary and non-UTF-8 content is rejected; and
- output contains only the relative path and requested lines, never an absolute
  machine path.

This is an explicit exception to ordinary no-excerpt output. The caller decides
whether the admitted excerpt may be disclosed to its chosen provider.

## Adjustment ledger

An adjustment records:

- protocol and input identity;
- available provider/model/version identity;
- one unique target capability per decision;
- `affirm` or `replace` intent;
- the exact original range;
- a complete replacement category, range, confidence, assumptions, exclusions,
  and uncertainty list when replacing;
- stable supporting evidence IDs; and
- a non-empty reason.

Every decision requires supporting evidence that belongs to the target candidate.
A selected-source decision cites the scanner file fact; source excerpts do not
create a second evidence namespace.

`affirm` has no replacement payload. `replace` has exactly one valid replacement
payload. Successful validation proves identity and structural lineage only; it
does not prove correctness, independence, calibration, or model admission.

## Determinism, safety, and privacy

- Packet and non-source query output is byte-deterministic for identical inputs,
  configuration, estimator, and protocol version.
- Caller-supplied model identity does not change the local estimate or input
  digest.
- Review commands do not execute target code, inspect Git history, or install
  dependencies. By default they do not access the network. Explicit remote
  `--fetch-missing` may resolve one immutable revision and acquire its missing Git
  objects before the same local review pipeline runs; warm reruns are offline.
- Query reasons, source, and rationales are untrusted data, not instructions.
- Cancellation is checked through scan, query construction, and source reading.
- Ordinary unit tests use in-memory repositories; filesystem and process behavior
  stays in the end-to-end suite.
- No packet, ordinary query, measurement, or validation result silently authorizes
  transmission to a provider.

## Deferred decisions

The following require new evidence and an explicit policy:

- automatic provider calls or provider-specific adapters;
- automatic model, token, query, time, or cost budgets;
- automatic review frequency or candidate selection changes;
- applying adjustments to canonical estimates;
- host review for Change EHE;
- multi-turn protocol state beyond digest-bound queries; and
- admission of review adjustments into calibration or distributable models.

`HOST_REVIEW_MEASUREMENT.md` defines the measurement and budget-admission
boundary. The first public comparison produced mixed accuracy and incomplete
provider/context telemetry, so it supports no savings claim or default budget.
