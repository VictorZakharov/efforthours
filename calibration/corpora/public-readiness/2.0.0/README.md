# Candidate-blind manual-QA review packet freeze

## Status

**The development manual-QA review inputs are frozen before any replacement QA
hours are authored.** This checkpoint projects every eligible coding
responsibility from the immutable `0.3.0` development corpus into 15
candidate-blind packets under a QA-specific rubric. It contains no review answers,
compiled labels, candidate evaluation, operational preflight, holdout access, or
model-admission result.

The shipped `seed-rules/0.4.0` estimator and development-only
`manual-qa-coding-ratio/0.1.0` candidate are unchanged. Validation remains opened
only as diagnostic data from the retired logical-capability attempt; test remains
sealed.

## Frozen identities

| Artifact | Identity |
| --- | --- |
| Review policy | `manual-qa-development-review-policy/1.0.0` |
| Policy text digest | `sha256:bc4d9a29aac4fd80a917219c77a02dd0c8d1b71ecfb91d4f2b1ad287ecb231e0` |
| Authoring protocol | `manual-qa-development-review-authoring/1.0.0` |
| Review rubric | `manual-qa-work-item/1.0.0` |
| Packet manifest | `manual-qa-development-review-manifest/1.0.0` |
| Manifest canonical digest | `sha256:556fb26e096aece4c01bcaeb26118ae807d5f0d36102321a81087ce009816f9a` |
| Source corpus | `efforthours-public-readiness-development/0.3.0` |
| Source-corpus canonical digest | `sha256:6ef79b8b950de013263258107c8c19cdd7187e66ab3be84107f63e282a76d385` |
| Partition/profile | `development` / `implementation` |
| Packet set | 15 repository families, 955 targets |

The policy digest is SHA-256 over UTF-8 text with line endings normalized to LF.
Packet and manifest digests use canonical compact contract JSON. The policy pins
the exact source corpus, eligible category order, profile, baseline, record count,
target count, hidden inputs, review practices, and limitations.

## Blindness boundary

Packets contain only the immutable public-source identity and license boundary,
source-target identity, category, title, scope, evidence IDs, a hidden-lineage
digest, and an exact-scope overlap-group ID. Their schema has no place to serialize:

- source coding hours, confidence, rationale, uncertainty decisions, or size
  exceptions;
- prior manual-QA targets or judgments;
- source work-item IDs or partition counts;
- seed or candidate QA values;
- category or repository totals;
- the candidate estimator/model identity; or
- a ratio formula or suggested answer.

The 955 targets include every source-corpus target in the 11 categories governed
by `eligible-coding-effort/1.0.0`, even when the earlier teacher judged the coding
signal excluded. That earlier decision is hidden. This lets the new review make an
explicit QA exclusion instead of silently omitting a candidate false positive.
Existing manual-QA targets and all ineligible categories are absent.

`sourceLineageDigest` binds each visible source target to its exact sorted hidden
source-work-item lineage without disclosing candidate-derived partition counts.
`overlapGroupId` binds targets with the same exact repository scope so the reviewer
can allocate shared validation once. Evidence and source inspection must still be
used to detect overlap across different scopes.

## Target inventory

| Repository family | Review targets |
| --- | ---: |
| App-vNext/Polly | 53 |
| BTCPayServer/BTCPayServer | 42 |
| CarterCommunity/Carter | 34 |
| colinhacks/zod | 27 |
| dotnet/command-line-api | 41 |
| FastEndpoints/FastEndpoints | 110 |
| fastify/fastify | 13 |
| FluentValidation/FluentValidation | 10 |
| lit/lit | 199 |
| MudBlazor/MudBlazor | 52 |
| oqtane/oqtane.framework | 36 |
| SimplCommerce/SimplCommerce | 237 |
| sindresorhus/execa | 6 |
| Squidex/squidex | 89 |
| tj/commander.js | 6 |
| **Total** | **955** |

Counts describe the finite review workload. They are not effort signals or
multipliers.

## Review semantics

`manual-qa-work-item/1.0.0` asks for the additional hands-on validation,
diagnosis, correction, and hardening needed after recreating each represented
coding responsibility. It includes representative execution and debugging of
behavior, tests, integrations, data, UI, security/accessibility, build, CI,
infrastructure, and packaging surfaces where applicable.

It excludes implementation and automated-test authoring already represented by
the source responsibility, discovery/design/setup/docs/self-review, historical
rework, waiting, professionalization gaps, exhaustive certification not present
in the artifact, and validation assigned to another target. The reviewer must
reason low/expected/high from concrete activities, not hidden coding hours, a
percentage, or a preferred total. Exact zero requires a wholly excluded or
duplicate responsibility with an explicit explanation.

## Reproduction

From a release build:

```powershell
dotnet tools/EffortHours.RepositoryCalibration/bin/Release/net10.0/EffortHours.RepositoryCalibration.dll `
  manual-qa-review-freeze `
  --corpus calibration/corpora/public-readiness/0.3.0.development-corpus.json `
  --policy calibration/corpora/public-readiness/2.0.0/manual-qa-review-policy.json `
  --expected-policy-digest sha256:bc4d9a29aac4fd80a917219c77a02dd0c8d1b71ecfb91d4f2b1ad287ecb231e0 `
  --packets calibration/corpora/public-readiness/2.0.0/manual-qa-review-packets `
  --manifest calibration/corpora/public-readiness/2.0.0/manual-qa-review-manifest.json
```

The command reads only the supplied public development corpus and policy. It has
no validation/test, repository-source, estimate, model, candidate, or network
input. It validates all three published schemas, canonical identities, exact
record/target coverage, partition/profile/baseline consistency, public
redistribution, unique source lineage, and deterministic ordering. Existing
identical artifacts are accepted; a different artifact at a frozen output path is
not overwritten.

Memory-only tests cover policy/packet/manifest validation, blindness, hidden-value
invariance, shared eligible-category identity, overlap grouping, and holdout
rejection. Process-level tests reproduce every committed byte and digest, verify
all 955 source-target mappings, scan for forbidden fields and values, rerun
deterministically, and confirm immutable-output refusal. Performance is not a
timing gate.

## Decision and next boundary

- Keep these packets immutable and unreviewed.
- Freeze a separate complete decision-plan schema, compiler identity, exact
  completeness checks, target-to-candidate-QA lineage mapping, zero/size-exception
  behavior, and corpus-rebasing semantics before authoring any answers.
- Only after that pre-answer compiler freeze, author all 955 decisions under the
  rubric with candidate values still unavailable.
- Compile a new development-only label identity without rewriting `0.3.0`, then
  compare seed and the exact QA candidate at low, expected, and high, overall and
  by repository family.
- A favorable development result would still require a fresh candidate identity,
  finite manifest, and fresh blind validation boundary. Test remains sealed.
