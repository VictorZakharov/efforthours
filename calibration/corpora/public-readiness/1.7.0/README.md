# Development structural uncertainty evaluation

## Status

**Repository-held-out residual measurement rejects all 14 current structural
diagnostics as direct interval-width drivers.** The feature contract, target
aggregation, expected directions, fixed buckets, and pooled gate were committed
and pushed in `4c11c63` before the public development reports were reproduced or
joined to reviewed labels. No evaluated feature preserves coverage and interval
miss while narrowing the unconditional symmetric q80 baseline.

The result is not that code shape is useless. Median decision complexity and
median nesting depth have the expected residual correlation and zero adjacent-
bucket direction violations. Several other features have the expected correlation
sign. But every univariate conditioned interval loses 23 to 29 covered targets,
increases mean interval miss, and regresses coverage in 7 to 10 of the 15
repository folds. The fields remain useful diagnostics; none is admitted into the
product interval.

No model is fitted, no estimate or reviewed label changes, validation and test
remain closed, and `seed-rules/0.4.0` remains the shipped estimator.

## Frozen boundary

| Input or policy | Identity |
| --- | --- |
| Development corpus | `efforthours-public-readiness-development/0.3.0`; `sha256:6ef79b8b950de013263258107c8c19cdd7187e66ab3be84107f63e282a76d385` |
| Candidate estimates | `candidate-logical-capability/0.3.0+seed-rules/0.4.0` |
| Structural feature contract | `repository-uncertainty-structural-features/1.0.0`; `sha256:a186c6e61ef7fcbd294ca4de27ed8504b313599e96246d8bcc7321eda04204ab` |
| Structural projector | `uncertainty-structural-feature-projector/1.0.0` |
| Evaluation policy | `uncertainty-structural-evaluation-policy/1.0.0`; `sha256:2f3dc2417747ac8557744eb2d59c3b2e816158620eb26086f383d3809610f610` |
| Evaluator and metrics | `uncertainty-structural-feature-evaluator/1.0.0`; `uncertainty-feature-metrics/1.0.0` |
| Held-out protocol | `uncertainty-structural-feature-evaluation/1.0.0` |
| Interval policy | `symmetric-planning-interval/1.0.0` |

The policy aggregates callable size, decision complexity, nesting, threshold
shares, and analyzer ambiguity by maximum across each reviewed target's source
work items. Callable measurement coverage uses the minimum. Counts are never
summed, so the target feature cannot become a repository-size proxy. The expected
direction is higher value/higher residual except for measurement coverage, where
higher coverage is expected to mean lower residual.

Each supported feature bucket learns its nearest-rank q80 normalized-residual
factor without the target repository. A bucket needs at least three training
observations from two repositories; otherwise that target uses the unconditional
repository-held-out baseline. The predeclared pooled gate requires at least one
conditioned prediction, non-negative coverage delta, non-positive normalized-width
delta, and non-positive interval-miss delta.

## Cohort reconciliation

All 15 repository records, 2,030 reviewed targets, and 11,161 source work-item
references matched exactly. No feature report was ignored. Shape distributions
were available for 201 targets across 14 repositories. Callable coverage was
available for 269 targets across all 15 repositories, and analyzer ambiguity for
310 targets across all 15. Sparse fallback therefore remains explicit rather than
turning missing parser coverage into a low-complexity value.

The source candidate intervals and the unconditional q80 baseline provide context:

| Interval source | Coverage | Mean normalized width | Mean miss |
| --- | ---: | ---: | ---: |
| Existing candidate intervals | 0.7601 | 0.4360 | 1.8225 h |
| Repository-held-out symmetric q80 baseline | 0.8463 | 1.1393 | 1.0946 h |

The baseline is the `before` value for every incremental feature comparison below.
Positive coverage is better; lower width and miss are better.

## Before/after interval measurement

| Conditioned interval | Predictions | Coverage | Coverage delta | Mean normalized width | Width delta | Mean miss | Miss delta | Gate |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Repository-held-out q80 baseline | 0 | 0.8463 | 0.0000 | 1.1393 | 0.0000 | 1.0946 h | 0.0000 h | baseline |
| Callable size p50 | 189 | 0.8330 | -0.0133 | 1.0975 | -0.0418 | 1.2186 h | +0.1240 h | reject |
| Callable size p90 | 201 | 0.8320 | -0.0143 | 1.1070 | -0.0323 | 1.2816 h | +0.1870 h | reject |
| Callable size maximum | 201 | 0.8325 | -0.0138 | 1.0890 | -0.0503 | 1.2952 h | +0.2006 h | reject |
| Oversized-callable share | 199 | 0.8330 | -0.0133 | 1.0869 | -0.0524 | 1.3266 h | +0.2320 h | reject |
| Decision complexity p50 | 201 | 0.8345 | -0.0118 | 1.0932 | -0.0461 | 1.2366 h | +0.1420 h | reject |
| Decision complexity p90 | 199 | 0.8325 | -0.0138 | 1.0947 | -0.0446 | 1.2410 h | +0.1464 h | reject |
| Decision complexity maximum | 201 | 0.8335 | -0.0128 | 1.0893 | -0.0500 | 1.2578 h | +0.1632 h | reject |
| High-complexity share | 200 | 0.8320 | -0.0143 | 1.0901 | -0.0492 | 1.2568 h | +0.1622 h | reject |
| Nesting depth p50 | 201 | 0.8335 | -0.0128 | 1.0929 | -0.0464 | 1.2395 h | +0.1449 h | reject |
| Nesting depth p90 | 199 | 0.8325 | -0.0138 | 1.0940 | -0.0453 | 1.1826 h | +0.0880 h | reject |
| Nesting depth maximum | 201 | 0.8340 | -0.0123 | 1.0904 | -0.0489 | 1.2944 h | +0.1998 h | reject |
| Deep-nesting share | 201 | 0.8350 | -0.0113 | 1.0927 | -0.0466 | 1.2749 h | +0.1803 h | reject |
| Callable measurement coverage | 199 | 0.8335 | -0.0128 | 1.1018 | -0.0375 | 1.2339 h | +0.1393 h | reject |
| Analyzer ambiguity | 302 | 0.8340 | -0.0123 | 1.1201 | -0.0192 | 1.2310 h | +0.1364 h | reject |

Every variant becomes narrower, but the narrowness is not earned: held-out
coverage falls by 1.13 to 1.43 percentage points and mean miss rises by 0.0880 to
0.2320 hours. None meets the simultaneous gate.

## Ordering and fold diagnostics

Correlation is Spearman association with normalized absolute residual. `yes` means
the sign matches the direction frozen before label access. Violations count
adjacent non-empty buckets whose empirical q80 residual moves contrary to that
direction. Regressed folds count repositories with lower conditioned coverage than
their own unconditional baseline.

| Diagnostic | Available targets | Residual correlation | Expected sign | Bucket violations | Regressed folds |
| --- | ---: | ---: | --- | ---: | ---: |
| Callable size p50 | 201 | -0.0094 | no | 2 | 9/15 |
| Callable size p90 | 201 | -0.0671 | no | 3 | 9/15 |
| Callable size maximum | 201 | -0.0409 | no | 3 | 10/15 |
| Oversized-callable share | 201 | -0.0394 | no | 2 | 10/15 |
| Decision complexity p50 | 201 | +0.1358 | yes | 0 | 9/15 |
| Decision complexity p90 | 201 | -0.0077 | no | 1 | 9/15 |
| Decision complexity maximum | 201 | -0.0126 | no | 2 | 8/15 |
| High-complexity share | 201 | +0.1196 | yes | 1 | 8/15 |
| Nesting depth p50 | 201 | +0.1465 | yes | 0 | 10/15 |
| Nesting depth p90 | 201 | +0.0179 | yes | 2 | 7/15 |
| Nesting depth maximum | 201 | -0.0249 | no | 2 | 9/15 |
| Deep-nesting share | 201 | +0.0068 | yes | 1 | 9/15 |
| Callable measurement coverage | 269 | -0.1285 | yes | 1 | 9/15 |
| Analyzer ambiguity | 310 | +0.1215 | yes | 2 | 8/15 |

Median decision complexity and median nesting depth are the cleanest ordering
signals, but their modest correlations do not justify the observed coverage and
miss regressions. Nesting p90 has the smallest miss increase, yet still loses 28
covered targets, violates bucket direction twice, and regresses 7 repository folds.

## Decision

- Do not use any of the 14 fields as a direct univariate interval-width driver.
- Retain all fields as diagnostic evidence. In particular, retain median decision
  complexity, median nesting depth, callable coverage, and analyzer ambiguity as
  hypotheses for later correlated analysis; this checkpoint does not admit them.
- Do not retune the frozen buckets or directions after seeing these labels. Any
  replacement evaluation policy needs a new identity and explicit rationale.
- Add local coupling, cycle, and interface-concentration distributions under a
  separate label-independent graph contract. Only then evaluate bounded correlated
  combinations on development and freeze a fresh candidate for blind validation.
- Keep validation and test sealed. This development result is not production
  accuracy, a formal probability calibration, or evidence of actual labor hours.

## Reproduction and artifact policy

```text
eh scan <exact-snapshot> --output <evidence.json>
eh calibration uncertainty-structure <candidate-estimate.json> <evidence.json> \
  --compact --output <structural-features.json>
eh calibration uncertainty-structure-evaluate \
  calibration/corpora/public-readiness/0.3.0.development-corpus.json \
  <structural-features.json>... --compact --output <evaluation.json>
```

All 15 projectors accepted the reproduced source digest against the frozen
candidate estimate. The compact evaluation report is 4,572,350 bytes with raw
SHA-256 `40d54a4b95d0e486f6d221106df4f06ffcfea322caa89044a96fd58e9d572e48`.
It remains under ignored `artifacts/` because it is deterministic and reproducible
from the checked-in corpus, exact public snapshots, and prior frozen candidate
estimates. The 15 scans consumed 75.764 cumulative seconds (14.075 seconds longest),
the projections consumed 43.590 cumulative seconds (8.771 seconds longest), and
evaluation took 15.125 seconds on the development workstation. These timings are
descriptive and are not CI gates. No target code, dependency, or test was executed;
no local path, source excerpt, generated timestamp, validation label, or test label
is serialized.
