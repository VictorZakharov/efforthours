# Change EHE model-admission policy

## Status

Metric identity and decision order are frozen as `change-model-admission/0.1.0`.
Numerical thresholds are deliberately not frozen yet because no independently
reviewed Change EHE corpus exists. No local ML fitting or production-readiness
claim is permitted until thresholds are set from development/validation error
scales without consulting the test partition.

The current source baseline is `change-seed/0.4.0+seed-rules/0.3.0`. Its Change
rules retain the 0.3.0 logical
marginality correction and adds a versioned, fail-closed generated-customization
normalization boundary; it is not an admitted calibrated candidate. The frozen
public synthetic corpus and source reports retain `change-seed/0.1.0` provenance.
The first real public Change pilot and subsequent six-family expansion retain
released `change-seed/0.2.0` provenance. Separate 0.3.0 development/validation
diagnostics leave the expansion's test comparison withheld. A released-alpha.3
public validation follow-on exercises 0.3.0 directly. All three real corpora have
one host-AI teacher and no independent correction.

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

## First real-public `change-seed/0.2.0` diagnostic

`efforthours-change-public-real-pilot/0.1.0` freezes one MIT-licensed public pull
request from a repository family already assigned to development. Released
alpha.2 estimates `1.75/4.25/6.75` hours; one disclosed host-AI teacher estimates
`2.25/4.00/6.25`. The expected absolute error is 0.25 hour and expected WAPE is
0.0625.

This result was compiled only after the teacher range was frozen. Candidate
guidance was visible, and there is no distinct reviewer, validation-family
observation, or held-out comparison. It therefore cannot set a correction factor,
threshold, uncertainty width, candidate choice, or release decision. It changes no
rule or prior.

## Blind real-public expansion diagnostic

`efforthours-change-public-real-expansion/0.1.0` freezes six immutable
MIT-licensed pull requests from six new repository families before candidate
analysis. The 3/2/1 development/validation/test partition assignment, exact
commits and trees, unchanged license blobs, and source boundary were committed
first. Released alpha.2 reports were written without displaying numeric content;
the 34-target teacher plan was then completed and committed without candidate
hours or category totals before compilation and evaluation.

| Partition | Reviewed expected | 0.2.0 expected | WAPE | Bias | Mapping |
|---|---:|---:|---:|---:|---:|
| development | 19.00 h | 55.75 h | 1.9605 | +1.9342 | 17/17 targets; 24/24 candidate items |
| validation | 16.00 h | 34.75 h | 1.1719 | +1.1719 | 12/12 targets; 16/16 candidate items |
| test | 4.00 h | withheld | withheld | withheld | not evaluated |

The visible defect is concentrated in partitioned category work. Zod emits four
security items totaling 16.00 expected hours and four unit-test items totaling
another 16.00; the corresponding logical teacher targets are 1.25 and 2.25 hours.
Axios emits four unit-test items totaling 14.50 hours against 3.50 teacher hours,
and p-limit emits two production items totaling 5.25 hours against 0.50 hour.
BenchmarkDotNet's total happens to agree while its categories cancel materially,
so the evidence does not support a single aggregate correction factor.

This is teacher-label diagnosis, not calibration or admission. The general
double-counting exception in the immutable decision boundary permits a new
transparent correctness revision only if it uses subject-neutral rules, synthetic
semantic regressions, a new estimator version, and development/validation
disclosure without consulting the test result. This corpus itself changes no
prior, threshold, candidate, or release decision.

## `change-seed/0.3.0` logical-marginality diagnostic

Version 0.3.0 uses the correctness exception above. Subject-neutral in-memory
regressions were frozen before its candidate reports were generated: changing the
number of repository partitions for one production, test, or security capability
cannot change its logical Change budget; distinct added capabilities remain
additive; and a capability detected on a materially modified production artifact
cannot collapse to a 0.25-hour classification delta.

The rule does not fit a teacher ratio or change a repository prior. Existing or
modified capabilities replace the summed positive repository-capability difference
with one evidence-derived marginal budget. Fixed edit-region bands contribute one
to four logical units per changed path, existing diminishing tiers cap the budget,
and unmapped fallbacks use the same units. Distinct capabilities added through new
artifacts retain their repository marginal. The estimator identity advances to
`change-seed/0.3.0+seed-rules/0.2.1`.

The five visible candidate reports and two evaluations are stored separately under
`calibration/changes/public-real-expansion/diagnostics/change-seed-0.3.0`; no frozen
alpha.2 report or label changed.

| Partition | Reviewed expected | 0.2.0 expected / WAPE / bias | 0.3.0 expected / WAPE / bias |
|---|---:|---:|---:|
| development | 19.00 h | 55.75 h / 1.9605 / +1.9342 | 20.75 h / 0.1447 / +0.0921 |
| validation | 16.00 h | 34.75 h / 1.1719 / +1.1719 | 15.75 h / 0.0156 / -0.0156 |

Expected category movement is mixed. Development production moves from 6.25 to
5.00 hours against 5.50 reviewed, unit testing from 21.50 to 2.75 against 4.25,
security from 16.00 to 2.00 against 1.25, and CI from 1.75 to 0.75 against 0.75.
Validation production moves from 7.00 to 3.25 against 4.25 and unit testing from
19.75 to 4.50 against 4.75. Unchanged change-level validation and review rules
retain their prior disagreements.

Consolidated items change exact lineage coverage. Development target matches move
from 17/17 to 14/17, source-item-reference matches from 24/24 to 14/24, and
candidate-item matches from 24/24 to 14/17. Validation moves from 12/12 to 10/12,
16/16 to 10/16, and 16/16 to 10/12 respectively. Candidate high totals remain
39.25 versus 30.25 reviewed hours in development and 30.00 versus 26.00 in
validation. Repository/category metrics retain all items; item metrics disclose
the lower match coverage.

These diagnostics show that the identified multiplication was removed, but they
do not establish accuracy, calibrate uncertainty, set a threshold, advance review
maturity, or admit a model. The test-family candidate report and evaluation were
not generated for this candidate.

## Released-alpha.3 validation diagnostic

`efforthours-change-public-real-alpha3/0.1.0` freezes one new MIT-licensed .NET
repository family in validation before candidate analysis. Released
`EffortHours.Tool` `0.9.0-alpha.3` generated the
`change-seed/0.3.0+seed-rules/0.2.1` report and blind packet; the four-target
teacher plan was committed before candidate values were opened.

| Partition | Reviewed expected | 0.3.0 expected | WAPE | Bias | Mapping |
|---|---:|---:|---:|---:|---:|
| validation | 5.75 h | 7.00 h | 0.2174 | +0.2174 | 4/4 targets; 4/4 candidate items |

The candidate's `4.00/7.00/13.50` interval contains the complete reviewed
`4.00/5.75/8.75` interval but is twice as wide. Expected production is 4.00 hours
against 3.50 reviewed, validation 1.25 against 1.00, comprehension 0.50 against
0.75, and self-review 1.25 against 0.50. The production capability cites both
source and test paths, leaving no separately measurable unit-testing category.

This is one non-independent validation observation. It cannot fit a rule, select a
candidate, calibrate uncertainty, set an admission threshold, advance review
maturity, or establish held-out accuracy.

## `change-seed/0.4.0` generated-customization normalization

Version 0.4.0 uses the correctness exception only to close the settled generated-
artifact boundary. Exact, balanced, non-nested, EffortHours-specific
`<custom-code>` regions can be isolated from otherwise generated UTF-8 source when
bodies are available and bounded; unrelated generator-specific protected-region
syntax is not inferred. Only that projection can contribute edit-region work. The
surrounding generated body remains excluded; unchanged or formatting-only
projections remain zero; ambiguous, oversized, or bodyless cases fail closed.
Vendored, minified, binary, lockfile, build-output, and exact-copy exclusions take
precedence.

The rule is subject-neutral and changes no numerical prior, repository model,
label, dependency, threshold, or public schema. Memory-only regressions cover
generated-body invariance, meaningful/formatting custom-region changes, additions,
removals, malformed markers, and bodyless evidence. A process-level Git test covers
the packaged command boundary and verifies that custom source is not emitted.

The checked-in Change source cases contain no supported custom-code marker, and
their only generated-path case is the existing conventional exact-zero synthetic
case. Therefore every frozen numeric report and the visible 0.3.0 development/
validation diagnostic remain applicable without regeneration; rewriting only the
estimator identity would destroy frozen provenance without adding evidence. The
test comparison remains unopened. This is normalization correctness, not
calibration, accuracy evidence, or model admission.

## Repository `seed-rules/0.3.0` composition

The repository frontend semantic revision mechanically advances the current
composite identity from `change-seed/0.4.0+seed-rules/0.2.1` to
`change-seed/0.4.0+seed-rules/0.3.0`. It removes repository UI physical-line
pricing, adds bounded frontend semantic drivers, and preserves every non-UI
repository prior. No Change rule, normalized-final-delta policy, threshold,
label, candidate comparison, or admission gate changes.

All frozen Change reports retain their original composite identities. No frozen
corpus was regenerated merely to rewrite provenance, and the withheld expansion
test comparison remains unopened. The repository transition is documented in
`MODEL_REVIEWS.md` and its public 56-case/192-relation mutation checkpoint; it is
not Change calibration or Change model admission.
