# Development graph uncertainty evaluation

## Status

**No current graph diagnostic is admitted as a direct interval-width driver.**
Twelve of the 14 frozen variants lose held-out coverage and increase interval
miss. The two cycle variants satisfy the literal non-regression gate only because
their predicted intervals are exactly identical to the unconditional baseline;
they improve no metric, their residual correlations have the opposite sign from
the frozen hypothesis, and positive-cycle observations occur in only one
repository. They are no-op gate passes, not selected predictors.

The graph feature contract and evaluation policy were committed in `e4499e5`
before the public graph reports were joined to reviewed development labels. This
checkpoint does not retune aggregation, directions, buckets, sparse fallback, or
the gate after seeing results. No model is fitted, no estimate or reviewed label
changes, validation and test remain closed, and `seed-rules/0.4.0` remains the
shipped estimator.

## Frozen boundary

| Input or policy | Identity |
| --- | --- |
| Development corpus | `efforthours-public-readiness-development/0.3.0`; `sha256:6ef79b8b950de013263258107c8c19cdd7187e66ab3be84107f63e282a76d385` |
| Candidate estimates | `candidate-logical-capability/0.3.0+seed-rules/0.4.0` |
| Graph feature contract | `repository-uncertainty-graph-features/1.0.0`; `sha256:3b41238130578b02e3c1b3426103cbbc5f1b6656efafe50fe53c336b94570200` |
| Graph projector | `uncertainty-graph-feature-projector/1.0.0` |
| Evaluation policy | `uncertainty-graph-evaluation-policy/1.0.0`; `sha256:f742039c129f02e6e423c31cf486f0c33e4b2e20b329da27294786cc9385c0db` |
| Evaluator and metrics | `uncertainty-graph-feature-evaluator/1.0.0`; `uncertainty-feature-metrics/1.0.0` |
| Held-out protocol | `uncertainty-graph-feature-evaluation/1.0.0` |
| Interval policy | `symmetric-planning-interval/1.0.0` |

Each reviewed target unions the unique graph node IDs mapped from all of its
source work items. Candidate range arithmetic still includes complete
category-matched work items with no graph node. Fan distributions and cyclic-node
share are recomputed over the selected node set; largest cyclic component is the
largest repository-relative component touched by that set. Interface distributions
use selected nodes with available measurements, while an incompatible selected
node would make all four target interface fields unavailable.

All 14 expected directions are higher value to higher absolute normalized
residual. Count buckets are `0`, `1`, `2-3`, `4-7`, and `8+`; ratios use quarter
bands. A held-out bucket needs at least three training observations from two
repositories or the target falls back to its unconditional repository-held-out
q80 interval. The pooled gate requires at least one conditioned prediction, no
coverage loss, no normalized-width growth, and no interval-miss growth. Direction
and repository-fold diagnostics remain separate and are not waived by a pooled
gate pass.

## Cohort reconciliation

All 15 repository records, 2,030 reviewed targets, and 11,161 source work-item
references matched exactly. No graph feature report was ignored. Topology fields
were available for 1,840 targets across all 15 repositories; 190 targets had no
mapped graph node. Interface fields were available for 1,731 targets; 299 were
not applicable and none was unavailable.

Only 39 targets touch a cyclic node, and all belong to the one repository that
contains cycles. The positive-cycle buckets therefore have no eligible training
repository when that family is held out. Those observations use baseline fallback;
the well-supported zero-cycle bucket learns the same q80 factors as the
unconditional folds in this cohort.

The source candidate intervals and unconditional q80 baseline are:

| Interval source | Covered targets | Coverage | Mean normalized width | Mean miss |
| --- | ---: | ---: | ---: | ---: |
| Existing candidate intervals | 1,543/2,030 | 0.7601 | 0.4360 | 1.8225 h |
| Repository-held-out symmetric q80 baseline | 1,718/2,030 | 0.8463 | 1.1393 | 1.0946 h |

The baseline is the `before` value for every incremental comparison. Positive
coverage delta is better; lower width and miss are better.

## Before/after interval measurement

| Conditioned interval | Predictions | Coverage | Coverage delta | Mean normalized width | Width delta | Mean miss | Miss delta | Outcome |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| Repository-held-out q80 baseline | 0 | 0.8463 | 0.0000 | 1.1393 | 0.0000 | 1.0946 h | 0.0000 h | baseline |
| Fan-in p50 | 1,840 | 0.8266 | -0.0197 | 1.1324 | -0.0069 | 1.1247 h | +0.0301 h | reject |
| Fan-in p90 | 1,840 | 0.8246 | -0.0217 | 1.1289 | -0.0104 | 1.1284 h | +0.0338 h | reject |
| Fan-in maximum | 1,840 | 0.8286 | -0.0177 | 1.1260 | -0.0133 | 1.1347 h | +0.0401 h | reject |
| High fan-in share | 1,840 | 0.8360 | -0.0103 | 1.1331 | -0.0062 | 1.1120 h | +0.0174 h | reject |
| Fan-out p50 | 1,815 | 0.8305 | -0.0158 | 1.1224 | -0.0169 | 1.1176 h | +0.0230 h | reject |
| Fan-out p90 | 1,815 | 0.8281 | -0.0182 | 1.1203 | -0.0190 | 1.1148 h | +0.0202 h | reject |
| Fan-out maximum | 1,809 | 0.8310 | -0.0153 | 1.1240 | -0.0153 | 1.1141 h | +0.0195 h | reject |
| High fan-out share | 1,840 | 0.8325 | -0.0138 | 1.1061 | -0.0332 | 1.1281 h | +0.0335 h | reject |
| Cyclic-node share | 1,801 | 0.8463 | 0.0000 | 1.1393 | 0.0000 | 1.0946 h | 0.0000 h | no-op gate pass |
| Largest cyclic component share | 1,801 | 0.8463 | 0.0000 | 1.1393 | 0.0000 | 1.0946 h | 0.0000 h | no-op gate pass |
| Public-interface p50 | 1,731 | 0.8350 | -0.0113 | 1.2340 | +0.0947 | 1.1036 h | +0.0090 h | reject |
| Public-interface p90 | 1,731 | 0.8345 | -0.0118 | 1.2312 | +0.0919 | 1.1037 h | +0.0091 h | reject |
| Public-interface maximum | 1,731 | 0.8350 | -0.0113 | 1.2324 | +0.0931 | 1.1031 h | +0.0085 h | reject |
| High public-interface share | 1,719 | 0.8281 | -0.0182 | 1.1886 | +0.0493 | 1.1179 h | +0.0233 h | reject |

Fan variants narrow the baseline slightly, but lose 21 to 44 covered targets and
increase mean miss by 0.0174 to 0.0401 hours. Interface variants are worse on all
three pooled criteria: they lose 23 to 37 covered targets, widen the interval, and
increase miss. The cycle variants merely reproduce baseline intervals.

## Ordering and fold diagnostics

Correlation is Spearman association with normalized absolute residual. `no` means
the sign is opposite the all-higher direction frozen before label access.
Violations count adjacent non-empty buckets whose empirical q80 residual moves
against that direction. Regressed folds count repositories with lower conditioned
coverage than their unconditional baseline.

| Diagnostic | Available targets | Residual correlation | Expected sign | Bucket violations | Regressed folds |
| --- | ---: | ---: | --- | ---: | ---: |
| Fan-in p50 | 1,840 | -0.0394 | no | 2 | 8/15 |
| Fan-in p90 | 1,840 | -0.0532 | no | 2 | 8/15 |
| Fan-in maximum | 1,840 | -0.0552 | no | 2 | 8/15 |
| High fan-in share | 1,840 | -0.0162 | no | 2 | 5/15 |
| Fan-out p50 | 1,840 | -0.1746 | no | 3 | 4/15 |
| Fan-out p90 | 1,840 | -0.1784 | no | 3 | 4/15 |
| Fan-out maximum | 1,840 | -0.1804 | no | 3 | 4/15 |
| High fan-out share | 1,840 | -0.1118 | no | 2 | 5/15 |
| Cyclic-node share | 1,840 | -0.0260 | no | 1 | 0/15 |
| Largest cyclic component share | 1,840 | -0.0266 | no | 1 | 0/15 |
| Public-interface p50 | 1,731 | -0.0885 | no | 2 | 7/15 |
| Public-interface p90 | 1,731 | -0.0923 | no | 1 | 7/15 |
| Public-interface maximum | 1,731 | -0.0994 | no | 1 | 7/15 |
| High public-interface share | 1,731 | -0.1050 | no | 2 | 7/15 |

All 14 marginal correlations oppose the predeclared uncertainty hypothesis.
Reversing the direction after observing development labels would be post-hoc
retuning and is not authorized by this result. The negative association can also
reflect repository/category confounding; it is not evidence that coupling or
public surface should narrow production intervals.

## Decision

- Do not use any graph field as a direct interval-width driver.
- Record the two cycle fields as non-selected no-ops despite their literal pooled
  gate pass. Equality with baseline, opposite direction, one-repository positive
  support, and no improvement provide no admission evidence.
- Retain graph values and lineage as diagnostics. They describe architecture and
  applicability even though they do not predict residual width in this cohort.
- Do not reverse directions, retune buckets, or collapse sparse fallback after
  seeing these labels. A replacement hypothesis requires a new identity and
  explicit pre-label rationale.
- Permit only a small, predeclared correlated-combination checkpoint next. This
  result supplies no positive marginal reason to include graph fields
  automatically; any graph interaction must have a mechanistic rationale fixed
  before calculation. Development may guide that design, but any selected model
  still needs a new candidate identity and fresh blind validation.
- Keep validation and test sealed. This development result is not production
  accuracy, a formal probability calibration, or evidence of actual labor hours.

## Reproduction and artifact policy

```text
eh calibration uncertainty-graph <candidate-estimate.json> <evidence.json> \
  --compact --output <graph-features.json>
eh calibration uncertainty-graph-evaluate \
  calibration/corpora/public-readiness/0.3.0.development-corpus.json \
  <graph-features.json>... --compact --output <evaluation.json>
```

All 15 projectors accepted the reproduced source digest against the frozen
candidate estimate. The compact evaluation report is 4,723,453 bytes with raw
SHA-256 `adf6d0dad64cf518aa2e288ae224ea2683de4393f97cee8fafae3689cb8f7422`.
It remains under ignored `artifacts/` because it is deterministic and reproducible
from the checked-in corpus plus the exact saved candidate estimates and evidence.
The 15 projections consumed 39.557 cumulative seconds (8.925 seconds longest),
and evaluation took 11.483 seconds on the development workstation. Timings are
descriptive, not CI gates. No target code, dependency, or test was executed; no
network call, local path, source excerpt, generated timestamp, validation label,
or test label is serialized.
