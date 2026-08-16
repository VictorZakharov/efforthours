# Development uncertainty support/OOD evaluation

## Status

**Repository-held-out residual measurement rejects the current support and OOD
signals as interval-width drivers.** The four signals were frozen without labels,
aggregated across all 11,161 source work-item references behind the 2,030 reviewed
development targets, and evaluated against the same symmetric q80 baseline used by
the scalar-feature checkpoint. Every signal reduced coverage and increased interval
miss. Their bucket order was also non-monotonic or moved opposite the hypothesized
uncertainty direction.

The support profile remains useful diagnostic evidence: it shows where comparable
cross-family examples exist and where a work shape is unfamiliar. This result says
those values do not currently predict reviewed residual width well enough to drive
hours. No model is fitted, no estimate changes, validation and test stay closed, and
`seed-rules/0.4.0` remains the shipped estimator.

## Frozen boundary

| Input or policy | Identity |
| --- | --- |
| Development corpus | `efforthours-public-readiness-development/0.3.0`; `sha256:6ef79b8b950de013263258107c8c19cdd7187e66ab3be84107f63e282a76d385` |
| Source evaluation | `sha256:fa8ec68a4b5fd254be10a1ddd4cdbf0a656911ec62af307869f168994a3f8ddb` |
| Support profile | `sha256:92bdbec842feaee1ebefe708427688de7a816b772f69f8a6a841134443374de2` |
| Support population | `efforthours-public-readiness-uncertainty-support/1.0.0`; `sha256:c86af113b391d7171060f3b0be7c6d01ffa87f04ccb1ae03803f015199e61678` |
| Feature contract | `repository-uncertainty-features/1.0.0`; `sha256:a2fea34b25d0c963bb9e96d8c538e130f6b2b23d4d98d851584a7fbe69916077` |
| Interval policy | `symmetric-planning-interval/1.0.0` |
| Target aggregation | `uncertainty-support-target-aggregation/1.0.0` |
| Evaluator and metrics | `uncertainty-support-evaluator/1.0.0`; `uncertainty-support-metrics/1.0.0` |

The four label-independent signals were fixed before inspecting target residuals:

1. worst hierarchical support fallback depth;
2. minimum selected-cell training repository count;
3. candidate-expected-hour-weighted mean OOD distance; and
4. maximum OOD distance.

Fallback depth uses exact, category/size/ecosystem, category/size, category,
global, and insufficient levels. OOD uses fixed `0`, `<=0.008333`, `<=0.025`,
`<=0.05`, `<=0.1`, and `>0.1` buckets. Each target joins only through immutable
work-item IDs. The support profile remains label-independent, and the complete
target repository is held out when learning each bucket's q80 residual width.

## Before/after measurement

Positive coverage is better; lower normalized width and interval miss are better.
Residual correlation is Spearman association with normalized absolute residual.
The expected directions were positive for fallback depth and OOD, and negative for
minimum repository count.

| Interval source | Residual correlation | Coverage | Coverage delta | Mean normalized width | Width delta | Mean miss | Miss delta | Monotonic violations |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Repository-held-out symmetric q80 baseline | n/a | 0.8463 | 0.0000 | 1.1393 | 0.0000 | 1.0946 h | 0.0000 h | n/a |
| Worst support fallback depth | -0.1234 | 0.8345 | -0.0118 | 1.1110 | -0.0283 | 1.1275 h | +0.0329 h | 2 |
| Minimum support repository count | +0.2865 | 0.8350 | -0.0113 | 1.1011 | -0.0382 | 1.1359 h | +0.0413 h | 1 |
| Expected-weighted mean OOD | -0.1554 | 0.8360 | -0.0103 | 1.1302 | -0.0091 | 1.1135 h | +0.0189 h | 4 |
| Maximum OOD | -0.1556 | 0.8360 | -0.0103 | 1.1302 | -0.0091 | 1.1135 h | +0.0189 h | 4 |

All four condition every target without sparse fallback, so the loss is not an
availability artifact. The narrower results under-cover and miss by more than the
unconditional baseline. Worse, fallback depth and OOD have negative residual
association, while greater support-repository count has positive association—the
opposite of all three predeclared uncertainty hypotheses. The two OOD aggregates
produce the same fixed buckets in this cohort and therefore the same interval
performance.

Repository folds do not rescue the pooled result:

| Interval source | Mean fold coverage | Median | Range | Folds at or above 0.80 |
| --- | ---: | ---: | ---: | ---: |
| Baseline | 0.7630 | 0.8000 | 0.5000-0.9255 | 8/15 |
| Fallback depth | 0.7581 | 0.8000 | 0.5000-0.9255 | 8/15 |
| Minimum repository count | 0.7506 | 0.8000 | 0.4545-0.9199 | 8/15 |
| Expected-weighted mean OOD | 0.7551 | 0.7941 | 0.5000-0.9292 | 7/15 |
| Maximum OOD | 0.7551 | 0.7941 | 0.5000-0.9292 | 7/15 |

## Decision

- Do not fit fallback depth, support count, or either OOD aggregate as a direct
  interval-width driver from this corpus.
- Retain support cells, counts, distances, and nearest-profile lineage as
  diagnostics and future applicability evidence.
- Do not retune buckets after seeing these labels. A revised support hypothesis
  needs a new pre-label contract and an explicit explanation of the confounding it
  addresses.
- Continue with deferred within-artifact structural distributions—function size,
  complexity, nesting, coupling/cycles, interface concentration, and analyzer
  ambiguity—in bounded groups. Measure each group through the same repository-held-
  out protocol before considering correlated aggregation or a finite candidate.

## Reproduction and artifact policy

```text
eh calibration uncertainty-support-evaluate \
  calibration/corpora/public-readiness/0.3.0.development-corpus.json \
  <support-profile.json> <features.json>... \
  --compact --output <support-evaluation.json>
```

The compact report is 2,069,757 bytes with raw SHA-256
`ffcff9969a6181b13f25724caf204d74d7d2450ee83a7593c3fafb142e38f489`.
It contains all 2,030 target audit rows, four signal evaluations, and 60 repository
fold rows. It remains under ignored `artifacts/` because it is deterministic and
reproducible from the checked-in corpus plus the earlier generated feature/support
artifacts. The observed run completed in 28.9 seconds on the development
workstation; wall time is descriptive and is not a CI gate. No local path, source
excerpt, or generated timestamp is serialized.
