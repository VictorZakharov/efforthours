# Manual-QA coding-ratio candidate freeze

## Status

**A development-only manual-QA candidate is frozen, implemented, and not
admitted.** It replaces the seed model's runtime-surface QA rule with a
dependency-linked 30/40/50 percent prior over eligible expected coding effort.
`seed-rules/0.4.0`, every frozen estimate, every reviewed label, Change EHE, and
the sealed test boundary remain unchanged.

The prior comes from disclosed maintainer experience: for the implemented
baseline, reasonable manual validation, debugging, and hardening commonly
represents approximately 30 to 50 percent of the coding effort, with 40 percent
as the planning point. It is not an empirical production observation, a
calibrated probability interval, or permission to infer actual labor.

## Frozen policy

| Field | Value |
| --- | --- |
| Policy | `manual-qa-coding-ratio-policy/1.0.0` |
| Policy digest | `sha256:31afc595a033c0e2dc96e3116ebd72d7105520392828ae146c6cf91c66123571` |
| Feature contract | `eligible-coding-effort/1.0.0` |
| Candidate | `manual-qa-coding-ratio/0.1.0` |
| Estimator | `candidate-manual-qa-coding-ratio/0.1.0+seed-rules/0.4.0` |
| Baseline | `seed-rules/0.4.0`, implementation profile only |
| Ratios | low `0.30`, expected `0.40`, high `0.50` |
| Maturity | `development-only-unvalidated` |

For eligible source items with expected coding hours `C`, the replacement manual-
QA category is exactly:

```text
manual QA = (0.30 C, 0.40 C, 0.50 C)
```

Every point uses expected coding hours. Source low/high values do not feed the
formula, so an already-wide source interval is not widened a second time. The QA
component is centered on its expected value: `0.40 C - 0.30 C` equals
`0.50 C - 0.40 C`.

Eligible categories are production implementation, UI/represented UX, data,
external integrations, unit tests, integration/component tests, end-to-end/UI
tests, build/developer tooling, CI/CD/infrastructure, security/accessibility, and
packaging/deployment/release artifacts. Specification/domain learning,
repository setup, architecture/design, documentation, existing manual QA,
self-review, professionalization gaps, and pricing are excluded.

This is not an opaque repository multiplier. The projector removes every seed
manual-QA item and creates one replacement QA item per eligible source item. Each
replacement preserves that source item's scope, evidence IDs, profiles,
complexity, and uncertainty; names the exact source item as a dependency; and
states the arithmetic in its reason. Category, report-total, and optional cost
arithmetic are then rebuilt exactly.

## Synthetic acceptance case

The executable regression fixture contains exactly 160 expected eligible coding
hours distributed across all 11 admitted categories, plus specification, setup,
design, documentation, self-review, and an old seed QA item.

| Result | Low | Expected | High |
| --- | ---: | ---: | ---: |
| Eligible coding basis | 160 h | 160 h | 160 h |
| Replacement manual QA | 48 h | 64 h | 80 h |

The test verifies that the excluded categories create no QA dependency, the old
QA item is absent, all 11 eligible categories do create one, source evidence and
scope remain attached, total/category arithmetic reconciles, and changing only a
source item's low/high values leaves its QA range equivalent.
The 40 synthetic coding parts produce 40 QA parts of `1.60` expected hours each,
inside the normal `0.5`-to-`8`-hour review boundary.

## Anonymized real-case diagnostic

A small interactive graphics application had a saved `seed-rules/0.4.0`
implementation estimate and a separate manual engineering assessment. No
repository identity, local path, source excerpt, contributor identity, commit,
or proprietary evidence is retained here.

The saved seed report contains 149 expected hours in the exact eligible category
set. Its manual-QA category is only `1.25/3.00/6.00` hours. Applying the frozen
formula replaces that category with `44.70/59.60/74.50` hours.

| Estimate | Low | Expected | High |
| --- | ---: | ---: | ---: |
| Saved seed report | 78.00 h | 161.50 h | 306.75 h |
| Manual-QA candidate | 121.45 h | 218.10 h | 375.25 h |
| Separate manual assessment | 182.00 h | 240.00 h | 302.00 h |

The expected-point shortfall falls from `78.50` hours (`-32.7%`) to `21.90`
hours (`-9.1%`), a `72.1%` reduction in absolute midpoint error. The low shortfall
also improves, from `104.00` to `60.55` hours.

The high result deliberately gets worse: it moves from `4.75` hours above the
manual assessment to `73.25` hours above it. That is because this candidate fixes
only the QA component while retaining every other asymmetric seed low/high value.
It is evidence that the QA point defect is material, not evidence that the whole
repository interval is solved. Overall symmetric uncertainty remains separately
governed research.

The manual assessment is itself a repository-based engineering judgment, not a
time-and-motion study or independent recreation. It is a development diagnostic,
not held-out validation of the ratio.

## Existing-label audit

The complete 15-family development corpus was authored under the earlier manual-
validation semantics. An exact category audit shows that those labels encode
roughly the same small QA allocation as the seed model:

| Development aggregate | Eligible expected coding | Manual-QA expected | QA/coding |
| --- | ---: | ---: | ---: |
| Reviewed teacher labels | 34,625.75 h | 1,691.75 h | 4.89% |
| Reproduced seed reports | 36,134.75 h | 1,626.50 h | 4.50% |
| Frozen candidate on seed basis | 36,134.75 h | 14,453.90 h | 40.00% |

The reviewed rows are disclosed host-AI weak supervision, not empirical labor.
They predate this explicit coding-basis definition and cannot independently test
it. A conventional evaluation would mechanically reject the candidate because
the teacher and seed share the same missing-QA assumption. The labels therefore
remain unchanged but require category-specific blind re-review before they can be
used to evaluate this candidate. Their development status does not become a new
holdout.

## Reproduction

The bounded projector reads one canonical saved seed estimate and the pinned
policy only. It scans no source, executes no target code, loads no labels, accesses
no network, and writes only to an explicit output path when supplied.

```text
dotnet EffortHours.RepositoryCalibration.dll manual-qa-candidate-project \
  --estimate <seed-estimate.json> \
  --policy calibration/corpora/public-readiness/1.9.0/manual-qa-coding-ratio-policy.json \
  --expected-policy-digest sha256:31afc595a033c0e2dc96e3116ebd72d7105520392828ae146c6cf91c66123571 \
  --output <candidate-estimate.json>
```

Synthetic and process-level tests cover the exact 160-hour result, category
eligibility, replacement rather than stacking, lineage, range non-compounding,
zero eligible coding, input-order invariance, determinism, policy-digest
rejection, profile rejection, cancellation, stdout/stderr behavior, and contract
reconciliation. Performance is not a timing gate.

## Decision and next boundary

- Keep `seed-rules/0.4.0` shipped and unchanged.
- Keep `manual-qa-coding-ratio/0.1.0` development-only.
- Do not rewrite old teacher labels or claim the real case calibrates the rule.
- Re-review manual-QA development targets under the explicit eligible-coding
  boundary, with candidate values hidden and exact source/evidence lineage.
- Run a complete development evaluation only after that label checkpoint, then
  exercise mutation, explanation, safety, determinism, and resource gates.
- If the candidate remains credible, freeze a new finite manifest and genuinely
  fresh blind validation boundary before any admission attempt. Test stays sealed.
- Continue correlated uncertainty research separately; this point correction
  does not authorize a post-hoc interval fit.
