# Development uncertainty-feature evaluation

## Status

**The first repository-held-out uncertainty measurement is complete; no interval
model or feature is selected.** The exact `logical-capability/0.3.0` candidate
points were measured against all 2,030 reviewed development targets. A simple
symmetric baseline improves coverage and interval miss but is substantially
wider. None of the 11 available scalar features improves coverage, normalized
width, and interval miss together relative to that baseline.

This is a development weak-label diagnostic. It does not reopen validation,
access test, fit a model, change any estimate, or provide a probability interval.
The rejected candidate remains retired, test remains sealed, and
`seed-rules/0.4.0` remains the shipped estimator.

## Frozen boundary

| Input | Identity |
| --- | --- |
| Development corpus | `efforthours-public-readiness-development/0.3.0`; `sha256:6ef79b8b950de013263258107c8c19cdd7187e66ab3be84107f63e282a76d385` |
| Candidate model | `logical-capability/0.3.0`; `1.0.0.logical-capability-model.json` |
| Candidate model digest | `sha256:492e10b2f427a471a8edcf4f7e3f19d65b098e4822e25c552956c9ce992fa1ea` |
| Candidate estimator | `candidate-logical-capability/0.3.0+seed-rules/0.4.0` |
| Feature contract | `repository-uncertainty-features/1.0.0`; `sha256:a2fea34b25d0c963bb9e96d8c538e130f6b2b23d4d98d851584a7fbe69916077` |
| Interval policy | `symmetric-planning-interval/1.0.0` |
| Evaluation protocol | `uncertainty-feature-evaluation/1.0.0` |
| Metrics | `uncertainty-feature-metrics/1.0.0` |

All 15 development repositories, 2,030 targets, and 11,161 source work-item
references matched. Repository identity defines each held-out fold. The target
residual is `abs(candidate expected - reviewed expected)`, normalized by
`max(candidate expected, 0.5 hours)`. Training-fold widths use nearest-rank q80.
Feature buckets require at least three training observations from at least two
repositories or fall back to the unconditional held-out width.

## Baseline result

| Interval source | Reviewed-expected coverage | Mean width | Mean normalized width | Mean absolute residual | Mean interval miss |
| --- | ---: | ---: | ---: | ---: | ---: |
| Existing candidate ranges | 0.7601 | 7.2766 h | 0.4360 | 4.7293 h | 1.8225 h |
| Repository-held-out symmetric q80 | 0.8463 | 23.2965 h | 1.1393 | 4.7293 h | 1.0946 h |

Across the 15 held-out repository folds, baseline coverage has mean `0.7630`,
median `0.8000`, and range `0.5000-0.9255`; only 8 of 15 folds meet the `0.80`
target. Existing candidate ranges meet it in 3 of 15. The pooled result therefore
does not establish family-stable coverage. It does show why one uniform multiplier
is not a satisfactory product answer: pooled coverage improves with approximately
3.2 times the mean width, yet several repositories remain poorly covered. The next
model needs evidence that narrows well-supported work without hiding uncertainty
on weakly supported work.

## Scalar feature result

The deltas below are relative to the repository-held-out symmetric q80 baseline.
Positive coverage is better; negative normalized width and interval miss are
better. Correlation is Spearman association with normalized absolute residual,
not causal importance.

| Feature | Available | Residual correlation | Coverage delta | Width delta | Miss delta | Decision |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| Source confidence | 2,030 | -0.3254 | -0.0069 | +0.0314 | +0.0378 h | Directional signal; revise conditioning |
| Inferred-fact share | 2,030 | +0.1563 | -0.0167 | -0.0784 | +0.1391 h | Narrower but under-covers; do not retain directly |
| Parser risk | 239 | +0.0423 | -0.0074 | +0.0128 | +0.0173 h | Sparse and weak; defer |
| Explicit uncertainty count | 2,030 | +0.1702 | -0.0429 | -0.1290 | +0.3292 h | Directional signal; buckets narrow too aggressively |
| Material unresolved count | 2,030 | n/a | 0.0000 | 0.0000 | 0.0000 h | No variation; not evaluated |
| Non-material offline limitation count | 2,030 | +0.0507 | +0.0059 | +0.1248 | -0.0463 h | Diagnostic only; gain comes from wider ranges |
| Dynamic boundary count | 2,030 | -0.0541 | -0.0020 | -0.0031 | +0.0130 h | Diagnostic only; no incremental value shown |
| Unsupported boundary count | 2,030 | -0.0343 | -0.0015 | +0.0061 | +0.0132 h | Diagnostic only; no incremental value shown |
| Resolved fact count | 2,030 | -0.0566 | +0.0005 | +0.0097 | -0.0292 h | Primarily a size proxy; do not use as width driver |
| Aggregate branch density | 285 | -0.0578 | -0.0153 | -0.0580 | +0.1423 h | Sparse diagnostic; no incremental value shown |
| Public-interface concentration | 304 | -0.2480 | -0.0143 | -0.0447 | +0.1388 h | Sparse diagnostic; no incremental value shown |

The source-confidence direction is credible enough to preserve as a research
lead, and explicit uncertainty moves in the expected direction. Their fixed
one-feature buckets are not good interval rules: both worsen aggregate coverage
and miss. Resolved-fact count has a `+0.6178` association with candidate size but
only `-0.0566` with normalized residual, so it is not evidence of incremental
uncertainty value.

The feature report also retains all 165 feature-by-repository fold rows. Source
confidence still reaches the target in only 8 of 15 folds. The diagnostic-only
non-material limitation count reaches 9, but only by widening aggregate intervals;
its minimum fold coverage remains `0.5000`. No pooled metric is being allowed to
hide that family instability.

## Reproduction and artifact policy

The maintainer projector now accepts `--output`, allowing the exact checked-in
candidate model to project each saved development estimate/evidence pair without
shell redirection. Each projection then runs:

```text
eh calibration uncertainty-features <candidate.json> <evidence.json> \
  --compact --output <features.json>
```

The complete deterministic evaluation runs once over the 15 reports:

```text
eh calibration uncertainty-evaluate \
  calibration/corpora/public-readiness/0.3.0.development-corpus.json \
  <features.json>... --compact --output <evaluation.json>
```

The compact evaluation has raw and normalized SHA-256
`sha256:8f5f7c526cc83d07ca1eb030107c154ab3e1159e5c77ac15c34f41d8de255639`.
It contains 2,030 target audit rows and is generated under ignored `artifacts/`
rather than committed as a large derived file. The 15 feature reports are also
ignored generated artifacts; their exact digests and matching estimate digests
are preserved in the evaluation's repository rows. No local path or source
excerpt is serialized.

## Next boundary

Do not fit the current scalar buckets. Add deferred evidence in deliberate,
reviewable groups: function/complexity distributions, coupling and cycle shape,
reviewed-sample support, and out-of-distribution distance. Measure each group with
this same repository-held-out protocol, then choose hierarchical fallback and
correlated aggregation only if the incremental result justifies it. Any finite
successor still requires a new identity and a fresh blind validation boundary.
