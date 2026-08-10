# Public real Change pilot

`efforthours-change-public-real-pilot/0.1.0` is the first EffortHours Change
calibration record built from an immutable public open-source pull request rather
than a project-authored synthetic change. It exercises the released
`EffortHours.Tool` `0.9.0-alpha.2` and current
`change-seed/0.2.0+seed-rules/0.2.1` estimator.

The one record is development-only weak supervision. It has one disclosed host-AI
teacher and no independent correction, model fitting, prior change, held-out
result, or production-readiness claim.

## Frozen result

| Range | Released alpha.2 | Teacher |
|---|---:|---:|
| low | 1.75 h | 2.25 h |
| expected | 4.25 h | 4.00 h |
| high | 6.75 h | 6.25 h |

The expected absolute error is 0.25 hour and expected WAPE is 0.0625. One small
development example cannot establish estimator accuracy or justify a correction.
The record is useful because it proves the public-source provenance, exact-digest
compilation, current-estimator evaluation, and blind handoff on a real final delta.

The committed material contains derived evidence, repository-relative paths,
hashes, review reasoning, and ranges. It does not contain a source checkout,
source excerpt, contributor activity, elapsed-time signal, or actual-labor label.
See [SOURCES.md](SOURCES.md) for provenance,
[REPRODUCING.md](REPRODUCING.md) for deterministic commands, and
[INDEPENDENT_REVIEW.md](INDEPENDENT_REVIEW.md) for the blind follow-up boundary.
