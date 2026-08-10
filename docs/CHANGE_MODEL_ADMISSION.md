# Change EHE model-admission policy

## Status

Metric identity and decision order are frozen as `change-model-admission/0.1.0`.
Numerical thresholds are deliberately not frozen yet because no independently
reviewed Change EHE corpus exists. No local ML fitting or production-readiness
claim is permitted until thresholds are set from development/validation error
scales without consulting the test partition.

The current source baseline is `change-seed/0.2.0`. It is a versioned structural
correctness revision to the original transparent rules, not an admitted calibrated
candidate. The frozen public corpus and source reports retain
`change-seed/0.1.0` provenance.

## Immutable decision boundary

The current deterministic baseline remains in place unless a fitted or calibrated
candidate passes every gate. Failure, insufficient data, ambiguous provenance, or
a regression leaves that transparent estimator in place. Candidate selection never
changes the normalized-final-delta, history-exclusion, pricing-separation, or
evidence-lineage semantics.

Before independent labels exist, a general correctness defect in the transparent
rules may advance the baseline without pretending to pass model admission. Such a
revision must use a new estimator version, be expressed without subject-specific
identifiers, add synthetic semantic and invariant regressions, preserve frozen
source reports and label provenance, avoid test-partition tuning, and disclose
mixed development/validation diagnostics without making an accuracy claim. This
exception covers rule correctness and double-counting defects only; it cannot be
used to fit numerical priors to preferred totals or bypass independent review.

## Frozen metric set

Change candidates use `calibration-metrics/1.0.0` at these levels:

1. final-change totals: expected WAPE, aggregate bias, mean/median absolute error,
   RMSE, and low/expected/high interval behavior;
2. effort categories: the same point and interval metrics plus maximum material
   per-category regression against the seed baseline;
3. fully matched reviewed targets: WAPE, bias, absolute error, interval behavior,
   target match rate, source-reference match rate, and candidate-item match rate;
4. semantic strata: ecosystem, profile, size, selection kind, and every required
   coverage tag in the frozen case matrix; and
5. range guardrails: no-rework additivity tolerance, exact allocations, and named
   overlap/revert/shared-setup/interaction reconciliation.

Intervals are planning bounds, not formal quantiles. Coverage improvement cannot
be purchased solely through materially wider ranges; candidate and reviewed mean
widths remain part of the decision.

## Non-numerical gates

A candidate must also:

- use only records with complete immutable base/head, source/license, reviewer,
  and repository-family provenance;
- preserve repository-owned development/validation/test isolation;
- use independently `reviewed` or `adjudicated` records for a release decision;
- pass all formatting, movement, generated, duplication, deletion, overlap,
  revert, additivity, history-boundary, and category-isolation guardrails;
- remain deterministic for identical evidence, configuration, and model versions;
- preserve stable work-item/evidence/adjustment lineage and explanation support;
- meet separately recorded latency, peak-memory, package-size, and offline-safety
  budgets; and
- add only dependencies and model artifacts compatible with MIT distribution.

Teacher labels may diagnose infrastructure and provisional error scales, but they
cannot select a validation candidate, establish held-out accuracy, support a
release decision, or advance independent maturity.

## Candidate selection order

1. Fit rules, correction factors, or ML only on development records.
2. Compare a finite, predeclared candidate set on validation and select at most one.
3. Freeze the candidate, numerical admission thresholds, and release rationale.
4. Evaluate the test partition once for that release decision.
5. Ship only if every numerical and non-numerical gate passes; otherwise retain the
   seed baseline and report the failed gates.

Test results must not tune priors, thresholds, features, uncertainty widths, or
hyperparameters. A later attempt after test failure is a new model/version and
requires a newly frozen decision protocol.

## `change-seed/0.2.0` structural-correction diagnostic

After the 0.2.0 rule mechanics and synthetic regression were fixed, the public
0.1.0 fixture suite was regenerated and compared with the existing preliminary
teacher corpus on development and validation only. The test partition was not
evaluated.

| Partition | Reviewed expected | 0.1.0 expected / WAPE / bias | 0.2.0 expected / WAPE / bias | 0.2.0 target / candidate mapping |
|---|---:|---:|---:|---:|
| development | 38.50 h | 41.25 h / 0.3701 / +0.0714 | 39.25 h / 0.2532 / +0.0195 | 38/49; 38/48 |
| validation | 26.00 h | 27.50 h / 0.0962 / +0.0577 | 22.75 h / 0.2788 / -0.1250 | 17/29; 17/21 |

The result is deliberately mixed: development agreement improves while validation
agreement and mapping worsen. Changed work-item identities are expected when broad
repository-derived modification items disappear, but unmatched reviewed targets
remain visible rather than being scored as zero. These teacher-label diagnostics
do not establish accuracy, select a calibrated model, or advance review maturity.
