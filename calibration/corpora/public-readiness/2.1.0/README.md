# Manual-QA decision compiler freeze

## Status

**The complete decision and compilation boundary is frozen before any of the 955
manual-QA answers are authored.** This checkpoint adds a blank, candidate-blind
decision template and an executable compiler contract. It contains no completed
review, replacement QA label, compiled corpus, candidate comparison, operational
gate, validation access, or test access.

The shipped `seed-rules/0.4.0` estimator and development-only
`manual-qa-coding-ratio/0.1.0` candidate remain unchanged.

## Frozen identities

| Artifact | Identity |
| --- | --- |
| Compiler policy | `manual-qa-development-decision-policy/1.0.0` |
| Policy normalized-text digest | `sha256:92c0b73259773cb3e4bec6e570043ea41ff8a83d4448ce2e2ea292be05455f60` |
| Authoring protocol | `manual-qa-development-decision-authoring/1.0.0` |
| Decision plan contract | `manual-qa-development-decision-plan/1.0.0` |
| Compiler | `manual-qa-development-review-compiler/1.0.0` |
| Source-work-item mapping | `manual-qa-source-work-item-lineage/1.0.0` |
| Review rubric | `manual-qa-work-item/1.0.0` |
| Output composition rubric | `ehe-work-item-with-manual-qa/1.0.0` |
| Blank template normalized-text digest | `sha256:d71c51ef4f9b7f71d295a6b3fa5cc42f56e35c2a148d89c7db1954fd63e56ff8` |
| Source packet manifest digest | `sha256:556fb26e096aece4c01bcaeb26118ae807d5f0d36102321a81087ce009816f9a` |
| Source corpus digest | `sha256:6ef79b8b950de013263258107c8c19cdd7187e66ab3be84107f63e282a76d385` |

Policy and template text digests use UTF-8 with line endings normalized to LF.
Packet-manifest and completed-plan provenance use canonical compact contract JSON.

## Frozen rebasing arithmetic

| Stage | Targets |
| --- | ---: |
| Immutable `0.3.0` source corpus | 2,030 |
| Legacy manual-QA targets removed | 320 |
| Non-QA targets preserved | 1,710 |
| Candidate-blind QA decisions required | 955 |
| Future compiled corpus | 2,665 |

The compiler preserves every non-QA target's range, rationale, uncertainty, size
exception, evidence, and source-work-item lineage. It removes only
`manual-validation-debugging-and-hardening` targets and adds exactly one
replacement QA target for each frozen packet target. Existing artifacts are never
rewritten.

Each replacement target maps the source responsibility's hidden work-item lineage
to the exact QA work-item IDs produced by the candidate projector. Explicit
exclusions and duplicates remain `0/0/0` targets mapped to those IDs, so candidate
false positives stay measurable. This mapping exposes no source work-item IDs or
derived candidate IDs in the blank template.

## Decision contract

Every completed decision must retain its source-target, hidden-lineage, packet,
and overlap identities and provide:

- disposition: `estimate`, `exclude`, or `duplicate`;
- low, expected, and high hours;
- concise rationale and cited packet evidence IDs;
- explicit overlap allocation;
- uncertainty reasons for an estimate;
- a direct estimated owner for a duplicate; and
- a size exception for `0/0/0` or an expected value outside the normal
  `0.5`-to-`8`-hour band.

An estimate must be positive. Exclusions and duplicates must be exactly `0/0/0`
and contain no range uncertainty. A duplicate must point directly to an estimated
target, not another duplicate. Evidence citations must come from the corresponding
candidate-blind packet.

The completed plan retains `teacher-estimate` maturity. Reusing a disclosed
teacher identity is allowed only with identical model provenance; it does not
create independent review or a maturity upgrade.

## Blankness and isolation

The committed 540,223-byte template has:

- 15 null review records;
- 955 null dispositions, hour ranges, rationales, overlap allocations, duplicate
  owners, and size exceptions;
- 955 empty evidence-citation and uncertainty arrays; and
- zero seed/candidate estimator names, ratio values, QA outputs, repository totals,
  or comparisons.

The author must copy the template rather than edit the frozen artifact. The
compiler accepts only `completed` plans and requires an explicit plan-file digest.
It reads the exact development corpus, review policy, manifest, and 15 packet files;
it has no validation, test, model-output, repository-source, or network input.

## Reproduction

```powershell
dotnet tools/EffortHours.RepositoryCalibration/bin/Release/net10.0/EffortHours.RepositoryCalibration.dll `
  manual-qa-decision-template-freeze `
  --corpus calibration/corpora/public-readiness/0.3.0.development-corpus.json `
  --review-policy calibration/corpora/public-readiness/2.0.0/manual-qa-review-policy.json `
  --expected-review-policy-digest sha256:bc4d9a29aac4fd80a917219c77a02dd0c8d1b71ecfb91d4f2b1ad287ecb231e0 `
  --review-manifest calibration/corpora/public-readiness/2.0.0/manual-qa-review-manifest.json `
  --packets calibration/corpora/public-readiness/2.0.0/manual-qa-review-packets `
  --compiler-policy calibration/corpora/public-readiness/2.1.0/manual-qa-decision-compiler-policy.json `
  --expected-compiler-policy-digest sha256:92c0b73259773cb3e4bec6e570043ea41ff8a83d4448ce2e2ea292be05455f60 `
  --output <blank-decision-plan.json>
```

After every answer is complete, compilation additionally requires
`--plan <completed.json>`, `--expected-plan-digest <sha256:digest>`, and
`--output <new-corpus.json>`. The committed blank template is rejected by that
command.

Memory-only tests compile a synthetic completed plan and verify preservation,
replacement, zero-target measurement, order invariance, and rejection of omitted,
tampered, or unsupported evidence. Process-level tests reproduce the committed
template byte-for-byte and confirm the blank plan cannot compile. Performance is
not a timing gate.

## Next boundary

- Keep this policy, compiler, schemas, rubric composition, and template immutable.
- Copy the template and author all 955 decisions from the frozen packets and
  pinned public source with candidate values still unavailable.
- Compile the completed plan under its exact digest into the new development-only
  corpus identity.
- In a later checkpoint, compare seed and the exact candidate at low, expected,
  and high overall and by repository, including coverage, exclusions, overlap,
  concentration, and the anonymized real-case diagnostic.
- Record advance, revise, or reject. An advance still needs a fresh candidate
  identity and fresh blind validation boundary. Test remains sealed.
