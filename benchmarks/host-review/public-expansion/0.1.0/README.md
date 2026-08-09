# Public host-review measurement checkpoint 0.1.0

## Status

This is a diagnostic Milestone 8 checkpoint, not evidence that compact host-AI
review is production-ready or cheaper. It exercises the public measurement
contracts on three redistributable repositories and deliberately selects no
default packet, query, token, time, or cost budget.

The compact decisions were authored in a host session where broader source and
aggregate teacher-review results had already been available. They are therefore
anchored and not a blind context-isolation experiment. The broader-source
references are the frozen single-host-AI teacher targets from
`efforthours-public-expansion/0.1.0`; they have no independent correction.

## Public sources

| Opaque subject | Public snapshot | Ecosystem |
| --- | --- | --- |
| `public-sample-a` | [developit/mitt 3.0.1](https://github.com/developit/mitt/tree/3.0.1) | JavaScript/TypeScript |
| `public-sample-b` | [Tyrrrz/CliWrap 3.10.4](https://github.com/Tyrrrz/CliWrap/tree/3.10.4) | .NET |
| `public-sample-c` | [nanostores/nanostores 1.4.2](https://github.com/nanostores/nanostores/tree/1.4.2) | JavaScript/TypeScript |

All three snapshots are MIT-licensed. Exact revisions, archive hashes, license
hashes, and provenance are recorded in
[`SOURCES.md`](../../../../calibration/corpora/public-expansion/SOURCES.md) and the
frozen [`0.1.0.corpus.json`](../../../../calibration/corpora/public-expansion/0.1.0.corpus.json).
Source archives and extracted trees are not redistributed here.

## Method

Each source snapshot was analyzed statically with the implementation profile and
`seed-rules/0.2.1`. The resulting `host-review/1.0.0` packet was not bound to a
reviewer identity, allowing the compact and older broader-source reviews to retain
their actual recorded identities.

The compact review used the packet plus one bounded capability query without a
source excerpt. It made one explicit `affirm` or `replace` decision for every
packet candidate. The broader-source ledger was reconstructed deterministically
by summing frozen teacher targets whose lineage identifies each candidate or one
of its partitions. Both ledgers were validated before measurement.

`host-review-measurement/1.0.0` strips repository identity, source digest, query
selectors and reasons, paths, source text, evidence IDs, and adjustment rationale.
The six committed measurement files contain opaque identities, canonical digests,
numeric telemetry, normalized decisions, model identity, and the generic
caller-supplied audit fields described below. The benchmark uses
`host-review-comparison-metrics/1.0.0`.

Caller-supplied IDs, telemetry bases, and condition notes are retained verbatim by
the contract. This checkpoint uses only generic non-identifying values, and the
committed JSON was checked for the public repository names, URLs, and source-path
tokens before inclusion.

## Payload observations

| Subject | Packet bytes | Approx. packet tokens | Query bytes | Known compact input bytes | Complete compact accounting | Complete source accounting |
| --- | ---: | ---: | ---: | ---: | --- | --- |
| `public-sample-a` | 39,084 | 9,771 | 3,569 | 42,653 | no | no |
| `public-sample-b` | 72,697 | 18,175 | 2,865 | 75,562 | no | no |
| `public-sample-c` | 42,686 | 10,672 | 5,280 | 47,966 | no | no |

Approximate tokens are exactly `ceiling(character count / 4)` and are not provider
token counts. The compact host session did not expose its complete surrounding
context size, and the earlier teacher sessions did not record the exact source
context made available. Known input values therefore include only explicitly
measured packet/query payloads, both sides are marked incomplete, and no
compact/source input ratios are calculated.

The generator removes terminal newlines from intermediate and public JSON before
measurement. This makes the recorded payload sizes and committed bytes independent
of the host operating system's newline convention.

Exact provider input/output tokens, elapsed time, and monetary cost were not
exposed for the compact host session and were not recorded for the frozen teacher
sessions. All three telemetry fields are explicitly unavailable in all six
measurements. No time, token, cost, or context-savings claim can be drawn from this
checkpoint.

## Agreement results

The broader-source review is a comparison reference, not ground truth.

| Level | Samples | Baseline expected-hour MAE | Compact expected-hour MAE | Absolute-error reduction | Reduction rate |
| --- | ---: | ---: | ---: | ---: | ---: |
| Capability item | 30 | 3.7417 | 1.2917 | 73.50 h | 65.48% |
| Category | 33 | 3.3106 | 1.1742 | 70.50 h | 64.53% |
| Repository total | 3 | 4.9167 | 7.4167 | -7.50 h | -50.85% |

Compact review moved candidate and category ranges materially closer to the frozen
reference, but moved repository totals farther away overall. That mixed result,
the anchoring risk, one compact model session, incomplete source-context sizing,
and absent provider telemetry all prohibit a default-budget decision.

## Artifacts and reproduction

- [`0.1.0.benchmark.json`](0.1.0.benchmark.json) contains the aggregate and
  per-subject comparison.
- [`measurements/`](measurements) contains three compact/source measurement pairs.
- [`Generate-PublicCheckpoint.ps1`](../../Generate-PublicCheckpoint.ps1) regenerates
  packets, bounded queries, ledgers, measurements, and the benchmark from the exact
  source snapshots and frozen corpus.

To reproduce, build the Release CLI, place the exact extracted snapshots under the
ignored `artifacts/host-review-m8` layout used by the generator, then run the
script from the repository root. The script writes raw packets, queries, and
ledgers only beneath ignored `artifacts/`; public output remains sanitized.
