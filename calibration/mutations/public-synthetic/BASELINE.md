# Public synthetic mutation baseline 0.1.0

## Status

This is a deterministic Milestone 7B2 guardrail checkpoint for
`seed-rules/0.2.0`. It is not a reviewed effort-label corpus, an accuracy claim,
or model-training data.

The fixtures are small synthetic .NET repositories authored for Fairbill and
distributed under the repository's MIT License. They contain no copied project
source, external dataset, package dependency, private evidence, or Git history.
Fairbill analyzed them statically without building, running, restoring, or
accessing the network.

## What the suite measures

The base case is a minimal maintained library. Seven variants change one intended
dimension:

| Variant | Intended relation to base | Seed expected result |
| --- | --- | ---: |
| Formatting | Same behavior with different whitespace/layout | 7.25 h, unchanged |
| Exact duplicate | Excluded exact source copy | 7.25 h, unchanged |
| Generated body | Conventional `.g.cs` output | 7.25 h, unchanged |
| API behavior | Runnable HTTP status endpoint | 11.75 h, +4.50 h total; +2.00 h production |
| Unit tests | Test project with two behavioral cases | 11.25 h, +1.75 h unit testing; production unchanged |
| Documentation | Maintained onboarding and usage guidance | 8.25 h, +1.00 h documentation; production unchanged |
| Integration | External HTTP client boundary | 13.00 h, +5.75 h total; +3.50 h integration |

All 14 assertions pass. The exact invariants use zero-hour lower and upper
difference bounds. Meaningful additions require at least a 0.5-hour increase in
the relevant total or category. These are qualitative guardrails against perverse
model behavior, not reviewed claims that the numerical deltas are correct.

## Artifacts

- `0.1.0.suite.json` defines 8 cases and 14 relational assertions using
  `calibration-mutation-metrics/1.0.0`.
- `baseline-seed-rules-0.2.0.json` is the deterministic evaluation report.
- `fixtures/` contains the complete synthetic source states.
- `estimates/` contains the canonical effort-only estimate for every case so the
  checked-in report can be reproduced without rescanning.

Every case has a distinct repository source digest. Assertions select canonical
estimates only by source digest, profile, and worker-baseline ID. File volume,
timestamps, contributor identity, and history are not inputs.

## Reproduce

From a Release build, regenerate each canonical estimate:

```text
fairbill estimate calibration/mutations/public-synthetic/fixtures/<case> \
  --profile implementation --no-rate \
  --output calibration/mutations/public-synthetic/estimates/<case>.estimate.json
```

Then evaluate the suite with all eight estimate paths:

```text
fairbill calibration mutations \
  calibration/mutations/public-synthetic/0.1.0.suite.json \
  calibration/mutations/public-synthetic/estimates/*.estimate.json \
  --output calibration/mutations/public-synthetic/baseline-seed-rules-0.2.0.json
```

The wildcard is shell convenience, not part of Fairbill's argument semantics.
Callers on shells without wildcard expansion should list the estimate paths.
Failed assertions still produce the complete report and return process exit code
`5`; malformed inputs return the existing invalid-input code.

## Limitations and next expansion

This first mutation suite is intentionally narrow. It does not yet test
JavaScript/TypeScript, mixed repositories, near-duplication, dead-code changes,
generated customization, UI/data/security behavior, coverage levels, low/high
range relations, or change-estimation semantics. It must be expanded before it can
serve as a complete local-model admission gate.
