# EffortHours CLI

EffortHours is an experimental .NET 10 command-line tool for estimating **Equivalent
Human Effort (EHE)**: the counterfactual time one competent senior contractor,
unfamiliar with the business domain and not using AI, would need to recreate a
software repository's current functional and quality state from a clear
specification.

> This is a public alpha. The bundled estimators are transparent but uncalibrated.
> EffortHours output is not actual labor history, a timesheet, an invoice, or an
> empirically validated billing determination.

## Install

```text
dotnet tool install --global EffortHours.Tool --version 0.9.0-alpha.1
eh version
eh --help
```

## Estimate a repository

```text
eh estimate ./my-repository --profile implementation --format markdown
```

EffortHours statically analyzes .NET, JavaScript, TypeScript, and mixed repositories.
It reports evidence-backed work items across implementation, testing,
documentation, integration, delivery, validation, and review, then optionally
applies a dated contractor rate without changing the effort estimate.

## Estimate a final change

```text
eh change ./my-repository --commit <revision> --format markdown
eh change ./my-repository --range <base>..<head> --format markdown
eh change ./my-repository --pr <number> --format markdown
```

Change EHE estimates the normalized final functional and quality delta. Commit
activity, author identity, timestamps, and intermediate churn do not multiply
effort. Pull-request identity resolution optionally uses an installed `gh` CLI;
the selected Git objects must already exist locally.

## Offline and safety boundary

Default repository analysis does not execute target code, install target
dependencies, fetch from the network, or inspect Git history. Source trees are
treated as untrusted input, and reports avoid source excerpts by default.

## Current limitations

- `seed-rules/0.2.1` and `change-seed/0.1.0` remain experimental and uncalibrated.
- Public calibration labels have not completed genuinely independent correction.
- TypeScript and TSX evidence is token-backed rather than compiler-backed.
- Multiple-PR and contributor-period portfolio estimation is not implemented.

The schemas, estimation decisions, calibration provenance, benchmarks, source,
issues, and contribution process are available in the
[EffortHours GitHub repository](https://github.com/VictorZakharov/efforthours).

EffortHours is distributed under the
[MIT License](https://github.com/VictorZakharov/efforthours/blob/main/LICENSE).
