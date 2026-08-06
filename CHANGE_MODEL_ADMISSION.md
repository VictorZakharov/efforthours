# Change EHE model-admission policy

## Status

Metric identity and decision order are frozen as `change-model-admission/0.1.0`.
Numerical thresholds are deliberately not frozen yet because no independently
reviewed Change EHE corpus exists. No local ML fitting or production-readiness
claim is permitted until thresholds are set from development/validation error
scales without consulting the test partition.

## Immutable decision boundary

The shipped baseline remains `change-seed/0.1.0` unless a candidate passes every
gate. Failure, insufficient data, ambiguous provenance, or a regression leaves the
deterministic seed estimator in place. Candidate selection never changes the
normalized-final-delta, history-exclusion, pricing-separation, or evidence-lineage
semantics.

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
