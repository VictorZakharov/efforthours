# Repository model-admission policy

## Status and claim boundary

Policy identity: **`repository-model-admission/1.0.0`**

Frozen: **2026-08-13**

This policy governs any transparent correction, statistical model, or local
learned model that would replace or adjust repository `seed-rules/0.4.0` EHE. It
was frozen before an eligible corpus or candidate set existed. Change EHE follows
the separate progressive policy in `CHANGE_MODEL_ADMISSION.md`.

No repository candidate is currently admitted. Passing this policy would support
an **experimental logical-admission** claim against disclosed weak supervision.
It would not establish historical truth, calibrated probability intervals,
empirical production accuracy, or production readiness.

The seed estimator remains the shipped default and mandatory offline fallback
unless one frozen candidate passes every applicable validation and one-time test
gate below.

## Admission scope

Each attempt must declare:

- the profiles and ecosystem strata to which the candidate applies;
- exact analyzer, evidence-contract, seed-baseline, feature-contract, candidate,
  runtime, and model-artifact identities and digests;
- the development records used for design or fitting;
- deterministic training seeds and hyperparameters, when applicable;
- an out-of-distribution rule and explicit seed fallback; and
- all runtime, package-size, license, and redistribution consequences.

Version 1 covers three primary ecosystem strata:

1. `.net`: maintained production scopes are .NET, apart from support assets;
2. `javascript-typescript`: maintained production scopes are JavaScript or
   TypeScript, apart from support assets; and
3. `mixed-dotnet-javascript-typescript`: at least one maintained .NET production
   scope and one maintained JavaScript/TypeScript production scope contribute
   represented capabilities.

A repository family belongs to exactly one primary stratum. Other analyzer
ecosystems remain outside this policy version and must use the seed fallback.
Support for an additional stratum requires a new policy version and fresh sealed
test evidence.

Candidate and seed reports must be produced from the same canonical repository
evidence, analyzer versions, profile, baseline, and exclusions. Pricing is never
a feature, label, metric, or admission input.

## Minimum evidence gate

### Repository-family matrix

The minimum distinct-family count for every claimed profile is:

| Primary stratum | Development | Validation | Sealed test | Total |
| --- | ---: | ---: | ---: | ---: |
| `.net` | 5 | 3 | 3 | 11 |
| `javascript-typescript` | 5 | 3 | 3 | 11 |
| `mixed-dotnet-javascript-typescript` | 5 | 3 | 3 | 11 |
| **Minimum** | **15** | **9** | **9** | **33** |

Within one profile matrix, a family counts once regardless of its number of
revisions or packages. The same family may supply both profile records, but every
revision, fork, and profile from that family remains in the same partition. A
profile may be admitted independently; a candidate claiming both `implementation`
and `recreation` must have the complete 33-family matrix for each profile and pass
every metric separately; the same 33 families may serve both matrices.

Five development families per stratum keep one repository from defining a fitted
correction. Three validation and three test families per stratum are the smallest
holdout cells in which one observation cannot determine the entire slice result.
These are conservative sufficiency floors for an experimental logical gate, not
a statistical-power or population-representativeness claim.

The sampling matrix must be frozen before candidate totals are inspected. Within
each ecosystem/partition cell, records must cover at least two materially
different product shapes. Across the complete matrix, each of these tags must
appear in at least three families, including at least one validation or test
family:

- library or SDK;
- CLI or desktop application;
- backend service;
- frontend or UI application;
- multi-package workspace or monorepository; and
- integration-heavy system.

The sampling plan must also define three source-shape size bands without using
candidate EHE. Each band needs at least three validation and three test families,
with the thresholds, evidence basis, and any boundary ambiguity recorded before
labels or candidates are opened.

All records require immutable source digests, repository-family identity,
redistributable source and license provenance, complete rubric lineage, and no
private source or historical activity signals. Near-copies and repeated releases
may provide diagnostics but do not increase the family minimum.

### Holdout and contamination rules

- Development records are the only records available for feature design,
  fitting, hyperparameter selection, error analysis, or correction design.
- Validation labels remain unavailable until the complete candidate manifest is
  frozen. Validation selects among that finite set; it does not tune another
  candidate.
- Test source identities and partition assignments may be public, but canonical
  test labels remain sealed. Their digest and custody record must be fixed before
  candidate selection, and their bodies are revealed only after one candidate is
  selected and frozen.
- Any validation or test record whose label or candidate disagreement influences
  an analyzer, feature, rule, threshold, uncertainty width, or candidate becomes
  diagnostic-only for that attempt. It stays in its original partition but cannot
  satisfy a held-out count or metric.
- Looking at test labels, test candidate output, or a derived test metric before
  selection invalidates the test set for that attempt.

Existing public test labels have already been disclosed and some have informed
analyzer diagnostics. They remain valuable records but cannot serve as the sealed
test set for this policy.

## Review maturity gate

| Requirement | Version 1 minimum |
| --- | ---: |
| Valid `ehe-work-item/1.1.0` targets with exact evidence and total reconciliation | 100% |
| Stable teacher/reviewer/model identity and input-visibility provenance | 100% |
| Validation and test records authored without candidate hours, totals, ranges, confidence, or explanations | 100% |
| Maturity of `teacher-estimate` or higher | 100% |
| Distinct independent review (`reviewed`) | 0% |
| Adjudicated records | 0% |

Decomposed host-AI teacher judgment is accepted as logical weak supervision.
Independent replication is useful corroboration but is not a gate and does not
silently upgrade maturity. Metrics must disclose results by review maturity and
teacher identity whenever a slice has at least three families.

This zero-independent-review minimum limits the resulting claim to experimental
logical admission. Any claim of independent validation, calibrated uncertainty,
or empirical production accuracy requires a separately frozen policy and evidence
boundary.

Positive targets normally remain between 0.5 and 8 expected hours. Exact `0/0/0`
targets are permitted only for reviewed exclusions under the rubric. Labels must
not be inferred from actual labor, commits, authors, elapsed time, or candidate
output.

## Candidate freeze and selection

An attempt may contain the seed baseline plus at most four challengers. Before
validation, one canonical manifest must freeze for every challenger:

- candidate kind: transparent rule, statistical baseline, or learned model;
- source commit, build inputs, feature list, hyperparameters, training seed, and
  artifact digest;
- exact development families and excluded/contaminated records;
- applicability and out-of-distribution boundary;
- dependency, license, package, runtime, and fallback identity;
- expected explanation form; and
- commands and environments for evaluation, determinism, and performance gates.

All challengers are evaluated once on validation. A candidate that fails any
evidence, numerical, safety, lineage, determinism, or performance gate is
ineligible. Among eligible candidates, select the lowest repository-total
expected WAPE. Values within 0.01 absolute WAPE are tied; choose, in order, the
less complex candidate kind, the sharper qualifying range, the lower runtime
overhead, and then ordinal candidate ID.

A learned model must additionally improve validation repository-total expected
WAPE over the best eligible transparent or statistical challenger by at least
0.02 absolute and 10% relative. Otherwise the simpler candidate wins.

## Numerical agreement gates

Metric identity remains `calibration-metrics/1.0.0`. The derived comparisons in
this policy are part of `repository-model-admission/1.0.0`.

The current evaluator exposes the underlying hours, errors, mappings, coverage,
and widths but does not yet emit a repository-admission decision. Before
validation is opened, admission tooling must serialize a deterministic checklist
for every gate in this policy. A missing, malformed, or not-computable required
metric fails closed.

For seed WAPE `S` and candidate WAPE `C`, relative improvement is:

```text
(S - C) / S
```

For each positive reviewed repository `i`, candidate normalized width is:

```text
(candidateHigh[i] - candidateLow[i]) / reviewedExpected[i]
```

Exact-zero repositories are semantic guardrails and are excluded from ratio
denominators. Means give each repository family equal weight. Percentiles use the
nearest-rank method. Category WAPE pools category observations with the existing
weighted-absolute-error formula.

Every gate below applies independently to validation and the one-time test:

| Metric | Required result |
| --- | --- |
| Repository-total expected WAPE | At most 0.20 and at least 15% lower than seed |
| Absolute aggregate expected bias | At most 0.10 and no worse than seed |
| Median expected absolute error | No greater than seed |
| Per-family expected error | Every family at most `max(16 h, 50%)`; at least 90% at most `max(8 h, 25%)` |
| Low and high WAPE | Each at most 0.30 and no more than 0.03 worse than seed |
| Ecosystem-stratum expected WAPE | At most 0.30 and no more than 0.03 worse than seed |
| Ecosystem-stratum absolute bias | At most 0.15 |
| Pooled material-category expected WAPE | At most 0.35 and at least 10% lower than seed |
| Material-category regression | No category WAPE more than 0.05 worse than seed; absolute bias at most 0.20 |
| Mapping | Target, source-reference, and candidate-item match rates each at least 0.95 |
| Category mismatch | At most 2% of reviewed targets |

A material category has at least five observations and at least 20 reviewed
expected hours in the evaluated partition. Metrics for smaller categories remain
visible but do not independently fail admission. Shape and size slices with at
least three families must not regress seed expected WAPE by more than 0.05.

These thresholds are policy judgments frozen before eligible validation or test
results exist. They define the minimum useful improvement over the transparent
seed; they do not imply a population error guarantee.

## Range sharpness gates

Coverage without sharpness is not useful confidence. A broad `4-16` range may
contain the right answer, but it is materially less informative than an accurate
`8-10` range. Full reviewed-range containment remains diagnostic because rewarding
it directly would favor wider bounds.

Every validation and test result must satisfy all of these:

| Range metric | Required result |
| --- | --- |
| Reviewed expected point inside candidate range | At least 0.80 and no more than 0.15 below seed |
| Mean repository normalized width | At most 0.50 |
| 90th-percentile repository normalized width | At most 0.80 |
| Mean width relative to seed | At most 0.75, a minimum 25% sharpness improvement |
| Mean width relative to reviewed ranges | At most 1.25 |
| Matched-target expected coverage | At least 0.75 |
| Matched-target mean normalized width | At most 0.75 |

For a reviewed expected value of 9 hours, `8-10` has normalized width `0.22`,
while `4-16` has normalized width `1.33`. The latter fails the per-repository
sharpness boundary even if it covers the point.

These are empirical planning-bound gates, not nominal 80%, 90%, or 95%
probability intervals. A later quantile or confidence interpretation requires a
new policy based on separately governed empirical evidence.

## Qualitative, safety, and explanation gates

A candidate must also:

- pass every versioned public mutation assertion applicable to its scope without
  weakening, deleting, or retuning an assertion after candidate freeze;
- produce byte-identical canonical estimates for the same saved evidence,
  configuration, and model artifact in three fresh processes on Windows, Linux,
  and macOS;
- preserve schema validation, stable evidence/work-item IDs, exact category and
  repository reconciliation, saved-report explanation, and stdout/stderr rules;
- keep every adjusted hour traceable to the seed item, evidence, feature-contract
  version, candidate identity, adjustment or prediction, and bounded reason;
- expose an explanation for every materially adjusted work item without emitting
  source excerpts, secrets, configured values, or private paths;
- detect unsupported or out-of-distribution inputs deterministically and use the
  named seed fallback with a visible diagnostic;
- remain offline, read-only, cancellable, and bounded, with no target execution,
  provider call, dependency installation, external process, or history-derived
  feature; and
- pass corrupt, missing, incompatible, and tampered model-artifact tests without
  silently producing partial learned output.

An opaque repository-wide multiplier cannot pass the explanation gate.

## Latency, memory, and package gates

Use paired seed/candidate runs over the same small, medium, and large saved
evidence bundles on each supported operating system. Record at least five fresh
processes per shape, including model load, inference, work-item construction, and
serialization but excluding repository scanning.

| Resource | Required result |
| --- | --- |
| Median wall time | At most `max(seed x 1.15, seed + 250 ms)` |
| Slowest wall time | At most `max(seed x 1.25, seed + 500 ms)` |
| Sampled peak working set | At most `max(seed x 1.15, seed + 64 MiB)` |
| Installed package increase | At most 25 MiB |

Whole-command scanner benchmarks and read-only target fingerprints must also
retain applicable CI thresholds. Where a published checkpoint has no frozen
cross-platform threshold, the paired overhead limits above govern model impact. A
candidate may request a new policy version when a measurably valuable but larger
runtime cannot satisfy these limits; it may not relax them after seeing test
results.

## One-time test decision and fallback

After validation selects exactly one candidate, freeze its manifest and release
decision record. Reveal the sealed test labels, verify their precommitted digest,
and evaluate the selected candidate and seed exactly once.

The candidate is admitted only if test satisfies every evidence, numerical,
sharpness, qualitative, safety, explanation, and resource gate. A failed or
ambiguous gate leaves `seed-rules/0.4.0` as the shipped estimator. The attempt may
not fall through to another challenger after test disclosure.

A later attempt requires a new candidate identity, a new frozen decision record,
and fresh sealed test families. Test results may diagnose future work but must not
tune the failed candidate, this policy version, or its uncertainty widths.

Any successful decision is recorded in `MODEL_REVIEWS.md` with exact manifests,
metrics, benchmark environments, known contamination, review maturity, and claim
limitations. The seed fallback remains available in every admitted release.

## Current readiness

The two legacy public repository corpora plus the complete public-readiness
development partition contain 21 implementation-profile families:

| Primary stratum | Development | Validation | Test |
| --- | ---: | ---: | ---: |
| `.net` | 6 | 1 | 0 |
| `javascript-typescript` | 6 | 1 | 1 |
| `mixed-dotnet-javascript-typescript` | 5 | 0 | 1 |

They share one host-AI teacher. The legacy test labels are already public, and some
legacy validation/test findings informed analyzer corrections. These records
remain useful development and contamination diagnostics. The separate 33-family
cohort now meets its 15-family development-label boundary, but its finite-candidate
freeze, blind validation boundary, and sealed nine-family test set are not met. No
holdout comparison or admission decision is authorized.

`efforthours-public-readiness/0.1.0` now freezes a separate implementation-profile
source cohort with exactly 5/3/3 development/validation/test families in each
primary stratum. Every validation and test cell has one small, medium, and large
tree, all required product-shape tags cross at least three families and a holdout,
and prior real-Change family assignments are inherited. The plan pins complete
Git trees and license checksums without copying source or inspecting candidate
totals.

Reproduction checkpoint `0.2.0` verifies all 33 commit/tree/blob/license
identities. It publishes strict-blind packets for the 15 development families and
source-custody status for the 18 holdouts; no validation or test snapshot was
scanned or estimated. Development checkpoint `0.3.0` freezes all 2,030 capability
judgments before unlocking candidate estimates. Against `seed-rules/0.4.0`, the 15
records have expected WAPE `0.2365`, aggregate bias `-0.0120`, expected-point
coverage `0.9333`, and full-range coverage `0.8667`. These single-teacher
development diagnostics authorize no fitted correction or candidate selection.

Development preflight `0.4.0` serializes every gate and fails missing or unrun
checks closed. Its strongest bounded transparent scope-marginality design improves
expected WAPE to `0.1963` and mean normalized width to `0.4808`, but fails the
aggregate-bias, ordinary-family-error, and matched-target-coverage gates. It is
recorded as `rejected-before-candidate-freeze`; no finite candidate manifest or
selection rule was frozen.

Development preflight `0.5.0` freezes `logical-capability/0.1.0` and its complete
development-fitted table. It passes all 16 numerical gates: expected WAPE
`0.1141`, absolute aggregate bias `0.0108`, ordinary-family pass rate `0.9333`,
repository expected coverage `0.8667`, and matched-target coverage/normalized
width `0.8202/0.7407`. Operational checkpoint `0.6.0` rejects that exact identity
because specification-comprehension aggregate bias is `-0.2505` against the
frozen `0.20` limit. Seven later measured gates were not run, and the candidate is
retired without a manifest or holdout access.

Development preflight `0.7.0` freezes successor `logical-capability/0.2.0`. It
raises only the specification-comprehension factor ceiling from `3.00` to `4.00`
under a new feature, model, candidate, and estimator identity. All 16 numerical
gates pass: expected WAPE is `0.1137`, absolute aggregate bias is `0.0094`,
ordinary-family pass rate is `0.9333`, repository expected coverage is `0.8667`,
and matched-target coverage/normalized width are `0.8217/0.7406`. Operational
checkpoint `0.8.0` passes the five development-computable gates, including
material-category agreement with specification-comprehension bias `-0.1253`.
Seven measured gates remain `not-evaluated` and non-passing. No manifest or
selection rule is frozen, and no validation or test access is authorized.

Validation labels are not authored and must remain unavailable until a finite
candidate manifest is frozen from development evidence. Test labels are not
authored and require external body/digest custody before the one-time reveal
boundary. The numerical validation/test gates and sealed test decision therefore
remain unopened.

## Policy changes

Changing a family count, eligible population, review requirement, formula,
threshold, candidate-selection rule, test boundary, or fallback rule requires a
new `repository-model-admission` version. If test labels have been revealed, the
new version also requires a fresh sealed test set. Editorial clarifications may
retain the version only when they cannot change any admission outcome.
