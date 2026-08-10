# Public Synthetic Change Teacher Corpus

## Status

The second Change calibration checkpoint is complete as of August 6, 2026. It
materializes the frozen 24-case source suite and compiles one disclosed host-AI
teacher pass. The estimator remains experimental and uncalibrated; no independent
review, admission threshold, prior change, or ML dependency is claimed.

## Reproducible source boundary

`change-fixture-generator/0.1.0` analyzes synthetic base/head states through
in-memory repository abstractions and writes 24 canonical effort-only
`change-seed/0.1.0+seed-rules/0.2.1` reports plus 24 blind authoring packets. The
suite owns eight repository families split 12/6/6 across development, validation,
and test. Formatting-only, exact movement, conventional generation, and complete
revert cases are exact zero.

The suite, generator version, source-report digests, final-delta digests, MIT
provenance, and reproduction command are checked in. Regeneration is byte-stable;
the process-level test runs a small suite twice. Ordinary unit tests remain
storage-independent.

## Preliminary teacher corpus

The separately authored category policy was frozen before evaluation and mapped to
source capability IDs by `change-teacher-plan-generator/0.1.0`. One host-AI teacher
created 121 targets: 99 positive work items and 22 lineage-preserving exact-zero
false-positive or duplicate exclusions. Four exact-zero final deltas correctly use
empty record target lists.

Candidate totals were visible during earlier fixture-invariant checks. That
exposure is recorded in every review provenance note; these labels are disclosed
weak supervision, not a blind estimate or ground truth. The exact corpus digest is
`sha256:ecfdb867ed2ba4912c9550277fc050b5e5511d0e15a107c8a08c044f61793c10`.

## Diagnostic evaluation

| Partition | Records | Teacher expected | Seed expected | Expected WAPE | Bias |
|---|---:|---:|---:|---:|---:|
| development | 12 | 38.50 h | 41.25 h | 0.3701 | 0.0714 |
| validation | 6 | 26.00 h | 27.50 h | 0.0962 | 0.0577 |
| test | 6 | 41.75 h | not evaluated | withheld | withheld |

Aggregate bias near zero does not imply small per-case error; development WAPE
demonstrates that cancellation can hide material case-level disagreement. The
validation measurement is diagnostic only. Numerical admission thresholds were
not invented from this one teacher, and the test comparison is withheld until
independent review, thresholds, and a release candidate are frozen.

## First real public follow-on

On August 10, 2026,
`efforthours-change-public-real-pilot/0.1.0` added one immutable MIT-licensed public
pull request from the GuardClauses repository family. That family was already in
development, so the Change record remains there. Released
`EffortHours.Tool` `0.9.0-alpha.2` produced a
`change-seed/0.2.0+seed-rules/0.2.1` range of `1.75/4.25/6.75` hours. A disclosed
host-AI teacher separately reasoned five target ranges totaling
`2.25/4.00/6.25` hours before compilation and evaluation.

The 0.25-hour expected difference and 0.0625 WAPE demonstrate the real-source
workflow, not estimator accuracy. Candidate guidance was visible, the record has
no independent correction, and it changes no prior, admission threshold, review
maturity, or model dependency. Exact commits, trees, license provenance, report
digest, corpus digest, and a blind five-target handoff are checked in without a
source checkout or source excerpts.

## Independent boundary

The checked-in blind packet contains all 121 targets with prior hours, rationales,
uncertainty, and explicit-zero decisions hidden. A genuinely distinct reviewer
must inspect the frozen fixture and blind evidence, replace every target with an
independent range or explicit exclusion, and sign off the four empty zero-delta
records. Until exact-digest compilation advances maturity, no Change calibration
accuracy claim is permitted.

## Next checkpoint

1. Complete and compile the distinct blind review without opening teacher files.
2. Add more redistributable real final-change families with the same provenance
   rules and preserve repository-held-out partitions.
3. Freeze numerical development/validation gates before any candidate fitting.
4. Evaluate the test partition once only for a frozen release decision.
