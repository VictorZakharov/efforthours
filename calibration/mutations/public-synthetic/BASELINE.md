# Public synthetic mutation baselines

## Status

These are deterministic qualitative guardrail checkpoints, not reviewed
effort-label corpora, accuracy claims, or model-training data.

- Suite `0.1.0` records the Milestone 7B2 .NET-only checkpoint: 8 cases and 14
  assertions evaluated with `seed-rules/0.2.0`.
- Suite `0.2.0` records the Milestone 7B3 aggregate checkpoint: 30 cases and 84
  assertions evaluated with `seed-rules/0.2.1`.

The fixtures are small synthetic repositories authored for Fairbill and distributed
under the repository's MIT License. They contain no copied project source, external
dataset, third-party package dependency, private evidence, or Git history. Fairbill
analyzed them statically without building, running, restoring, or accessing the
network.

`seed-rules/0.2.1` retains every 0.2.0 numerical prior. It corrects TypeScript file
ownership in the shared JavaScript/TypeScript estimation scope so byte-identical
TypeScript bodies and TypeScript test structure participate in normalization. The
catalog digest is
`sha256:57378795593acd2ff0a2f4361698193a11dca86da11493f072da6a9f9b344d4e`.

## What suite 0.2.0 measures

| Ecosystem family | Cases | Principal variants |
| --- | ---: | --- |
| .NET | 8 | Base, formatting, excluded exact copy, generated body, API, tests, documentation, integration |
| JavaScript | 9 | Base, formatting, exact copy, generated body, generated customization, API, tests, documentation, integration |
| TypeScript | 8 | Base, formatting, exact copy, generated body, API, tests, documentation, integration |
| Mixed | 5 | Base, generated JavaScript, .NET API, JavaScript UI, TypeScript tests |

All 84 assertions pass. The relations cover:

- zero-difference formatting, exact-copy, and conventional-generated invariants;
- low, expected, and high points rather than expected hours alone;
- positive production movement for API behavior and separately maintained
  customization beside generated output;
- positive UI, unit-test, documentation, and integration category movement; and
- category isolation for test-only, documentation-only, UI-only, and
  production-only variants.

Missing categories are evaluated as zero. This allows, for example, the unit-test
category in a test variant to be compared directly with an absent base category.
Nested synthetic test packages keep test structure in an explicit test scope, so
their production-category invariants are not artifacts of aggregate rounding.

## Seed expected checkpoints

### JavaScript

| Variant | Expected total | Intended category result |
| --- | ---: | --- |
| Base | 6.50 h | 0.50 h production |
| Formatting | 6.50 h | Unchanged at every range point |
| Exact copy | 6.50 h | Unchanged at every range point |
| Generated body | 6.50 h | Unchanged at every range point |
| Generated customization | 7.00 h | +0.50 h production |
| API | 9.50 h | +1.75 h production |
| Unit tests | 9.25 h | +1.75 h unit testing; production unchanged |
| Documentation | 7.50 h | +1.00 h documentation; production unchanged |
| Integration | 11.50 h | +3.25 h external integration |

### TypeScript

| Variant | Expected total | Intended category result |
| --- | ---: | --- |
| Base | 7.75 h | 0.75 h production |
| Formatting | 7.75 h | Unchanged at every range point |
| Exact copy | 7.75 h | Unchanged at every range point |
| Generated body | 7.75 h | Unchanged at every range point |
| API | 10.75 h | +1.75 h production |
| Unit tests | 10.50 h | +1.75 h unit testing; production unchanged |
| Documentation | 8.75 h | +1.00 h documentation; production unchanged |
| Integration | 13.00 h | +3.25 h external integration |

### Mixed .NET and JavaScript/TypeScript

| Variant | Expected total | Intended category result |
| --- | ---: | --- |
| Base | 12.75 h | 2.00 h production |
| Generated JavaScript | 12.75 h | Unchanged at every range point |
| .NET API | 17.25 h | +2.00 h production; unit testing unchanged |
| JavaScript UI | 16.00 h | +1.75 h UI; unit testing unchanged |
| TypeScript tests | 15.50 h | +1.75 h unit testing; production unchanged |

The original .NET-only 0.1.0 table and its exact results remain represented by its
frozen suite, canonical estimates, and baseline report. Suite 0.2.0 includes all 14
of those assertions unchanged.

## Artifacts

- `0.1.0.suite.json` defines the frozen .NET-only suite.
- `baseline-seed-rules-0.2.0.json` is its original deterministic report.
- `estimates/*.estimate.json` contains its eight original 0.2.0 candidates.
- `0.2.0.suite.json` defines the 30-case aggregate suite.
- `baseline-seed-rules-0.2.1-suite-0.2.0.json` is the aggregate deterministic
  report.
- `estimates/seed-rules-0.2.1/` contains all 30 aggregate candidates.
- `fixtures/` contains every complete synthetic source state.

Every case has a distinct repository source digest. Assertions select canonical
estimates only by source digest, profile, and worker-baseline ID. File timestamps,
contributors, commit activity, and history are not inputs.

## Reproduce suite 0.2.0

From a Release build, regenerate each candidate with:

```text
fairbill estimate calibration/mutations/public-synthetic/fixtures/<fixture> \
  --profile implementation --no-rate \
  --output calibration/mutations/public-synthetic/estimates/seed-rules-0.2.1/<case>.estimate.json
```

For the eight .NET cases, `<fixture>` is the case ID without the `dotnet-` prefix.
For all other cases, fixture and case IDs are identical.

Then evaluate all versioned candidate paths:

```text
fairbill calibration mutations \
  calibration/mutations/public-synthetic/0.2.0.suite.json \
  calibration/mutations/public-synthetic/estimates/seed-rules-0.2.1/*.estimate.json \
  --output calibration/mutations/public-synthetic/baseline-seed-rules-0.2.1-suite-0.2.0.json
```

The wildcard is shell convenience, not part of Fairbill's argument semantics.
Callers on shells without wildcard expansion should list the estimate paths. Failed
assertions still produce the complete report and return process exit code `5`;
malformed inputs return the ordinary invalid-input code.

## Limitations and next expansion

The suite uses deliberately small archetypes. It does not yet test near-duplicate
or dead-code normalization, data and persistence behavior, security/accessibility,
coverage levels, CI/infrastructure, large work-item partitioning, realistic
multi-package dependency graphs, or change-estimation semantics. The TypeScript
path remains token-backed. Generated customization is represented as a maintained
companion file beside conventional generated output; distinguishing edits inside a
generated body remains an analyzer research problem.

Passing these relations prevents known perverse movements. It does not establish
that any absolute hour or delta is numerically correct, and it does not make the
seed estimator calibrated or production-ready.
