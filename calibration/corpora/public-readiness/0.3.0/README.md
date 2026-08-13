# Complete public-readiness development checkpoint

## Status

`efforthours-public-readiness/0.3.0` completes `ehe-work-item/1.1.0` review for all
15 development families in the frozen public-readiness matrix. It contains 2,030
evidence-backed capability targets, including 162 explicit zero-hour exclusions.
All nine validation and nine test families remain `withheld-not-run` and
`not-authored` under the unchanged `0.2.0` custody record.

The records are logical weak supervision from one disclosed host-AI teacher. They
have no independent correction and are not actual labor, empirical production
observations, a fitted model, or an admission result.

## Blind review boundary

The maintainer review command accepts no estimate input. It verifies the frozen
sampling plan, reproduction manifest, strict-blind packet digests, evidence-bundle
digests, source digests, and capability/evidence lineage. Checked-in transparent
policies then assign repository-specific rubric judgments and exclusions from the
blind packets, static evidence measurements, and pinned public source.

The complete `0.3.0.development-review-plan.json` was frozen in commit `6bda0b3`
before any of the 12 newly reviewed candidate reports were unlocked. The three
previously frozen `0.2.0` judgment ranges are retained exactly. Candidate
disagreement was not used to retune the review plan.

The plan contains 402 cohesive targets above the ordinary eight-hour review size;
each has an explicit size exception. The 162 `0/0/0` decisions identify bounded
double counting, test-only semantics, benchmark entry points, generated/golden
fixtures, or false framework signals rather than silently dropping evidence.

## Development diagnostic

| Family | Targets | Candidate expected | Reviewed expected | Point covered | Range covered |
| --- | ---: | ---: | ---: | :---: | :---: |
| App-vNext/Polly | 119 | 1,532.75 h | 1,385.00 h | yes | yes |
| FastEndpoints/FastEndpoints | 242 | 2,552.50 h | 1,532.50 h | yes | no |
| FluentValidation/FluentValidation | 30 | 770.00 h | 674.50 h | yes | yes |
| CarterCommunity/Carter | 68 | 352.25 h | 365.25 h | yes | yes |
| dotnet/command-line-api | 80 | 1,015.00 h | 626.00 h | yes | yes |
| colinhacks/zod | 55 | 1,768.25 h | 1,586.25 h | yes | yes |
| fastify/fastify | 21 | 1,729.50 h | 1,338.50 h | yes | yes |
| lit/lit | 537 | 3,450.75 h | 2,886.00 h | yes | yes |
| sindresorhus/execa | 12 | 1,159.50 h | 1,384.25 h | yes | yes |
| tj/commander.js | 13 | 393.00 h | 494.50 h | yes | yes |
| btcpayserver/btcpayserver | 83 | 4,797.75 h | 5,366.00 h | yes | yes |
| MudBlazor/MudBlazor | 111 | 5,619.75 h | 4,949.25 h | yes | yes |
| oqtane/oqtane.framework | 88 | 2,098.25 h | 3,920.75 h | no | no |
| SimplCommerce/SimplCommerce | 399 | 5,000.50 h | 3,909.00 h | yes | yes |
| Squidex/squidex | 172 | 7,836.75 h | 10,146.25 h | yes | yes |

The unchanged `seed-rules/0.4.0` candidate totals 40,076.50 expected hours versus
40,564.00 reviewed hours. Repository-total expected WAPE is `0.2365`, aggregate
bias is `-0.0120`, expected-point coverage is `0.9333`, and full reviewed-range
coverage is `0.8667`. All 2,030 targets and all 11,161 candidate work items match
their frozen lineage. Mean candidate width is 3,569.98 hours, while mean reviewed
width is 1,126.67 hours.

Near-zero aggregate bias does not imply family or category agreement. Oqtane and
Squidex remain material underestimation signals; FastEndpoints and SimplCommerce
are material overestimation signals. These development diagnostics may inform a
finite candidate design, but they do not authorize a correction by themselves.

## Reproduction

The review plan is generated from the ignored, digest-verified evidence bundles:

```powershell
dotnet tools/EffortHours.RepositoryCalibration/bin/Release/net10.0/EffortHours.RepositoryCalibration.dll `
  review-development `
  --plan calibration/corpora/public-readiness/0.1.0.sampling-plan.json `
  --manifest calibration/corpora/public-readiness/0.2.0.reproduction-manifest.json `
  --packets calibration/corpora/public-readiness/0.2.0/authoring-packets `
  --outputs artifacts/calibration/public-readiness-0.2.0/outputs `
  --output calibration/corpora/public-readiness/0.3.0.development-review-plan.json
```

Only after freezing that plan, compile and evaluate it against the 15 exact source
estimate documents using `eh calibration compile` and `eh calibration evaluate
--partition development`. The committed corpus and evaluation are deterministic,
schema-valid outputs of those commands.

## Next boundary

Use development evidence only to define and freeze a finite candidate manifest,
including exact model/configuration identities, resource budgets, and the
precommitted validation selection rule. Validation labels remain unavailable until
that freeze. Test labels remain externally sealed for a one-time decision after
validation selects one candidate.
