# Public synthetic mutation baselines

## Status

These are deterministic qualitative guardrail checkpoints, not reviewed
effort-label corpora, accuracy claims, or model-training data.

- Suite `0.1.0` records the Milestone 7B2 .NET-only checkpoint: 8 cases and 14
  assertions evaluated with `seed-rules/0.2.0`.
- Suite `0.2.0` records the Milestone 7B3 aggregate checkpoint: 30 cases and 84
  assertions evaluated with `seed-rules/0.2.1`.
- Suite `0.3.0` records the Milestone 7B4 behavior-and-delivery checkpoint: 48
  cases and 156 assertions evaluated with the same `seed-rules/0.2.1` model.

The August 8, 2026 analyzer-precision reevaluation with `.NET` analyzer `0.3.2`
and JavaScript analyzer `0.4.1` reproduced identical low, expected, and high
repository/category values for all 48 candidates. The checked-in 0.3.0 report is
therefore unchanged and all 156 relations remain green.

The fixtures are small synthetic repositories authored for EffortHours and distributed
under the repository's MIT License. They contain no copied project source, external
dataset, installed/vendored third-party code, private evidence, or Git history.
Some manifests, the CI fixture, and the Dockerfile contain inert public package,
action, or image identifiers solely to exercise static classification. EffortHours
does not install or redistribute that software and analyzed every fixture without
building, running, restoring, pulling images, or accessing the network.

`seed-rules/0.2.1` retains every 0.2.0 numerical prior. It corrects TypeScript file
ownership in the shared JavaScript/TypeScript estimation scope so byte-identical
TypeScript bodies and TypeScript test structure participate in normalization. The
catalog digest is
`sha256:57378795593acd2ff0a2f4361698193a11dca86da11493f072da6a9f9b344d4e`.

Suite 0.3.0 changes no seed-rule prior or estimator behavior. It was designed from
product invariants, then evaluated against the unchanged model. The new bounds are
qualitative guardrails and must not be treated as reviewed numeric labels.

## What suite 0.3.0 measures

| Ecosystem family | Cases | Principal variants |
| --- | ---: | --- |
| .NET | 13 | Existing variants plus renamed near-copy, compiler-disabled boundaries, data context, migration, security |
| JavaScript | 19 | Existing variants plus renamed near-copy, data, security, coverage levels, workspace boundaries, CI, container |
| TypeScript | 11 | Existing variants plus renamed near-copy, data, and security |
| Mixed | 5 | Base, generated JavaScript, .NET API, JavaScript UI, TypeScript tests |

All 156 assertions pass. The relations cover:

- zero-difference formatting, exact-copy, and conventional-generated invariants;
- low, expected, and high points rather than expected hours alone;
- positive production movement for API behavior and separately maintained
  customization beside generated output;
- positive UI, unit-test, documentation, and integration category movement; and
- bounded marginal rather than full-body treatment for small renamed near-copies;
- zero movement from compiler-disabled C# data and authorization syntax;
- positive data, migration, security, declared coverage, workspace-boundary, CI,
  and container movement in the intended categories; and
- category isolation for test-only, documentation-only, UI-only, data-only,
  security-only, coverage-only, CI-only, container-only, and production-only
  variants.

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

### Milestone 7B4 additions

| Variant | Expected total | Intended category result |
| --- | ---: | --- |
| .NET renamed near-copy | 8.25 h | +1.00 h bounded production marginality |
| .NET compiler-disabled boundaries | 7.25 h | Total, data, and security unchanged |
| .NET data context | 13.25 h | +4.00 h data/persistence |
| .NET data context plus migration | 15.50 h | +1.25 h data/persistence over the context case |
| .NET security | 20.00 h | +9.00 h security/accessibility |
| JavaScript renamed near-copy | 6.75 h | +0.25 h bounded production marginality |
| JavaScript data | 9.25 h | +1.00 h data/persistence |
| JavaScript security | 16.75 h | +4.75 h security/accessibility |
| TypeScript renamed near-copy | 8.00 h | +0.25 h bounded production marginality |
| TypeScript data | 10.75 h | +1.00 h data/persistence |
| TypeScript security | 17.75 h | +4.75 h security/accessibility |
| Coverage base | 11.00 h | 1.75 h unit testing with no declared percentage |
| Declared-and-assumed 80% coverage | 12.25 h | +1.25 h unit testing over identical code/tests |
| Declared-and-assumed 100% coverage | 12.50 h | +0.25 h unit testing over the 80% case |
| One-package workspace | 12.75 h | Reference workspace boundary |
| Two-package workspace with exact body reuse | 18.00 h | +1.25 h setup and +1.00 h review; production unchanged |
| JavaScript CI workflow | 8.50 h | +2.00 h CI/CD; production unchanged |
| JavaScript container definition | 8.50 h | +2.00 h packaging/deployment; production unchanged |

The original .NET-only 0.1.0 table and its exact results remain represented by its
frozen suite, canonical estimates, and baseline report. Suites 0.2.0 and 0.3.0
retain all earlier assertions unchanged.

## Artifacts

- `0.1.0.suite.json` defines the frozen .NET-only suite.
- `baseline-seed-rules-0.2.0.json` is its original deterministic report.
- `estimates/*.estimate.json` contains its eight original 0.2.0 candidates.
- `0.2.0.suite.json` defines the 30-case aggregate suite.
- `baseline-seed-rules-0.2.1-suite-0.2.0.json` is the aggregate deterministic
  report.
- `0.3.0.suite.json` defines the 48-case expanded aggregate suite.
- `baseline-seed-rules-0.2.1-suite-0.3.0.json` is its deterministic report.
- `estimates/seed-rules-0.2.1/` contains all 48 current aggregate candidates.
- `fixtures/` contains every complete synthetic source state.

Every case has a distinct repository source digest. Assertions select canonical
estimates only by source digest, profile, and worker-baseline ID. File timestamps,
contributors, commit activity, and history are not inputs.

## Reproduce suite 0.3.0

From a Release build, regenerate each candidate with:

```text
eh estimate calibration/mutations/public-synthetic/fixtures/<fixture> \
  --profile implementation --no-rate \
  --output calibration/mutations/public-synthetic/estimates/seed-rules-0.2.1/<case>.estimate.json
```

For the original eight .NET cases, `<fixture>` is the case ID without the
`dotnet-` prefix. For every later case, fixture and case IDs are identical.

Then evaluate all versioned candidate paths:

```text
eh calibration mutations \
  calibration/mutations/public-synthetic/0.3.0.suite.json \
  calibration/mutations/public-synthetic/estimates/seed-rules-0.2.1/*.estimate.json \
  --output calibration/mutations/public-synthetic/baseline-seed-rules-0.2.1-suite-0.3.0.json
```

The wildcard is shell convenience, not part of EffortHours's argument semantics.
Callers on shells without wildcard expansion should list the estimate paths. Failed
assertions still produce the complete report and return process exit code `5`;
malformed inputs return the ordinary invalid-input code.

## Limitations and next expansion

The suite uses deliberately small archetypes. Near-copy assertions bound the
current marginal result; EffortHours does not yet perform semantic clone detection.
The dead-code case covers only C# syntax excluded by the compiler preprocessor, not
arbitrary unreachable or unreferenced behavior. Coverage cases are
declared-and-assumed Jest thresholds, not parsed measured results. Security cases
do not replace a security audit, and accessibility-specific evidence remains thin.
The workspace case is intentionally small and does not represent a realistic large
dependency graph. Large work-item partitioning, measured coverage, richer
infrastructure, in-body generated customization, general reachability, and
change-estimation semantics remain future guardrails. The TypeScript path remains
token-backed.

Passing these relations prevents known perverse movements. It does not establish
that any absolute hour or delta is numerically correct, and it does not make the
seed estimator calibrated or production-ready.
