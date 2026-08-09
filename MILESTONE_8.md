# Milestone 8: Provider-neutral host review

## Status

Initial protocol checkpoint implemented on August 9, 2026. Implementation and cost
measurement are tracked separately: issue #26 defines and implements the local
protocol; issue #27 must measure token use, elapsed time, monetary cost, packet
size, and estimate improvement before EffortHours chooses any model-facing budget
or automatic-review default.

This milestone does not calibrate `seed-rules/0.2.1`, select an AI provider, call a
remote model, or claim that an AI-reviewed estimate is production-ready.

## Objective

Let a surrounding AI session review only consequential uncertainty while the
ordinary local estimate remains deterministic, complete, offline, and useful on
its own.

The first checkpoint provides:

- a compact, versioned packet built from the existing review projection;
- stable input identity over the complete rate-free estimate and its evidence;
- bounded capability, evidence, scope, and selected-source follow-up queries;
- a provider-neutral adjustment ledger with evidence and rationale; and
- deterministic schema and semantic validation of that ledger.

The host session remains the orchestrator. EffortHours neither embeds a provider
SDK nor sends repository material anywhere.

## Semantic boundary

Host review is optional adjudication around an already completed local baseline.
It is not part of the definition of Equivalent Human Effort and it never represents
actual hours worked.

The local estimator runs first with no rate card. Its full estimate and repository
evidence remain the canonical inputs. The host packet is a lossy review projection;
pricing is intentionally absent so monetary values cannot anchor effort reasoning.
If no review occurs, the original local report remains the result.

An adjustment document is a proposed, traceable ledger. The first checkpoint
validates it but does not rewrite an estimate, redistribute capability totals, or
recalculate pricing. Applying reviewed adjustments requires a later explicit
admission policy and tests for aggregation, confidence, and lineage.

## CLI surface

The initial command shape is:

```text
eh review packet <repository-or-evidence.json>
  [--profile <implementation|recreation>]
  [--model <id> [--provider <id>] [--model-version <id>]]
  [--compact]
  [--output <path>]

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

eh review validate <packet.json> <adjustment.json>
  [--compact]
  [--output <path>]
```

`packet` and non-source `query` accept either a repository directory or saved
repository evidence. A selected-source query requires a repository directory. All
machine-readable payloads go to stdout unless `--output` is explicit; diagnostics
go to stderr.

The model options record identity made available by the surrounding session. A
model ID is required before provider or model-version metadata is accepted. No
provider or model is selected by default.

## Protocol identity

The first protocol identity is `host-review/1.0.0`. Each public document also uses
the ordinary v1 JSON contract version.

The packet `inputDigest` is SHA-256 over a canonical compact envelope containing:

1. the complete rate-free `EstimateReport`; and
2. the complete `RepositoryEvidence` used to produce it.

Re-serializing a valid document with different whitespace or property ordering
therefore does not change identity. Any material evidence, work-item, profile,
baseline, estimator, assumption, verification, or diagnostic change does.

Every query and adjustment must repeat the exact input digest. A query is rejected
before returning detail when the regenerated input does not match. Adjustment
validation also rejects a packet/ledger mismatch. When the packet records an
available reviewer model identity, the ledger must repeat it exactly. Repository
`sourceDigest` remains
present for artifact identity but is not sufficient for review identity because it
does not cover estimator output or profile.

## Review packet

The packet reuses the deterministic review queue from Milestone 6. It contains at
most the existing twelve material or uncertain capability groups and records:

- repository, profile, contractor baseline, estimator version, and estimator
  references;
- total and category EHE without prices;
- each candidate's baseline range and confidence;
- the reason the capability was selected for review;
- estimator reasoning, assumptions, exclusions, uncertainty, and evidence IDs;
- a compact union of referenced evidence facts and explicit omission counts;
- diagnostics and static verification state;
- available query kinds and structural safety ceilings; and
- caller responsibilities for provider selection, privacy, disclosure, retention,
  and authorization to transmit material.

No source excerpt is included in a packet. The packet may include at most 48
referenced evidence facts. This is an initial structural safety ceiling, not a token
or monetary budget. Omitted fact IDs remain visible on their candidates and can be
requested individually.

The packet explicitly states that the local baseline is complete without host
review and that the bundled estimator remains experimental and uncalibrated.

## Follow-up queries

Every query records its kind, selector, reason, profile, and expected input digest.
The result repeats that query and the verified digest.

The supported kinds are:

1. `capability`: return one compact capability with its work-item reasoning and up
   to 48 referenced evidence facts;
2. `evidence`: return one exact evidence fact by stable ID;
3. `scope`: return a deterministic page of capability summaries for one exact
   scope, with at most 20 entries per response; and
4. `selected-source`: return an explicit line window from one admitted source file.

Scope queries use zero-based `offset` and a positive `limit`; omitted counts are
reported so pagination is explicit. Capability and evidence queries do not accept
pagination. Source queries require a positive start line and line count, return no
more than 200 lines or 64 KiB of decoded text, and refuse files larger than 1 MiB.
These are untrusted-input safety ceilings, not model context budgets.

Selected-source access is intentionally stricter than ordinary path containment:

- the selector must be repository-relative and remain under the analyzed root;
- the path must identify a regular file already represented by a scanner `file:`
  evidence fact;
- the root, ancestors, and file must not be links or reparse points;
- the current content digest must match the fact recorded during the query's scan;
- binary or non-UTF-8 content is rejected; and
- output contains only the relative path and requested lines, never an absolute
  machine path.

This preserves the default no-excerpt behavior. The surrounding caller remains
responsible for deciding whether the explicit excerpt may be shown to its selected
provider.

## Adjustment ledger

The adjustment contract records:

- protocol and input identity;
- explicit model-identity availability plus any provider, model, and version the
  reviewer can disclose;
- one unique target capability per decision;
- `affirm` or `replace` intent;
- the exact original range to detect stale or fabricated baselines;
- a complete replacement category, range, confidence, assumptions, exclusions,
  and uncertainty list when replacing;
- stable supporting evidence IDs; and
- a non-empty review reason.

At least one supporting evidence ID is required for every decision. It must belong
to the target candidate. A selected-source decision cites the corresponding
scanner file fact, so source excerpts do not create a second untraceable evidence
namespace.

An `affirm` decision has no replacement payload. A `replace` decision must provide
one valid replacement payload. Validation does not imply that the replacement is
correct, independent, calibrated, or admitted into an official model.

## Determinism and safety

- Packet and non-source query output is byte-deterministic for identical inputs,
  configuration, estimator, and protocol version.
- Model identity is caller-supplied metadata; it does not change the local estimate
  or input digest.
- Queries never execute target code, inspect Git history, install dependencies, or
  access the network.
- Ordinary unit tests use in-memory repository abstractions. Physical filesystem
  and subprocess behavior remains in the end-to-end suite.
- Cancellation is checked during scan, query construction, and selected-source
  reading.
- Query reasons and review rationales are data, not trusted instructions; source
  trees remain untrusted input.

## Deferred work

Issue #27 owns representative measurement and comparison against full-source host
review. It must record packet/query sizes, token use where observable, elapsed time,
monetary cost, adjustment frequency, and improvement against reviewed references.

The following remain deferred until evidence justifies them:

- automatic provider calls or provider-specific adapters;
- automatic selection of a model, token budget, or review frequency;
- applying adjustment ledgers to canonical estimates;
- host review for Change EHE;
- multi-turn protocol state beyond digest-bound queries; and
- admission of AI adjustments into calibration or distributable model artifacts.

## Verification and issue #26 exit

- All packet, query-result, adjustment, and validation documents have checked-in v1
  JSON Schemas and semantic validation.
- The CLI produces deterministic packets and all four digest-bound query kinds.
- Source access passes containment, admission, link, size, encoding, and digest
  checks.
- Valid and invalid adjustment ledgers are reported without applying them.
- Unit coverage is storage-independent and end-to-end coverage checks exit codes,
  stdout/stderr separation, determinism, and explicit source disclosure.
- Public documentation states the offline/provider/privacy boundary and retains the
  uncalibrated status.

All criteria above are implemented. The ordinary unit suite exercises packet,
digest, query, source-admission, change-detection, schema, and adjustment behavior
entirely through in-memory repositories. The separate process-level suite exercises
all commands, stdout/stderr separation, deterministic packets, stale digests,
ignored-source refusal, and valid/invalid ledgers on temporary physical fixtures.
The complete solution build, unit suite, end-to-end suite, formatting check, and
package check are the release gate for this checkpoint. The August 9 checkpoint
passed formatting verification, a zero-warning Release build, 122 memory-only unit
tests, 37 process-level end-to-end tests, and creation of the
`EffortHours.Tool.0.9.0-alpha.1` package with the review assembly included.
