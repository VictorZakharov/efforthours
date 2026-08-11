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
dotnet tool install --global EffortHours.Tool --version 0.9.0-alpha.3
eh version
eh --help
```

## Estimate a repository

```text
eh estimate ./my-repository --profile implementation --format markdown
```

EffortHours statically analyzes .NET, JavaScript, TypeScript, HTML/CSS-family
frontends, SQL, and mixed repositories. Frontend support includes bounded template
and stylesheet semantics plus static Angular component metadata. SQL support
includes bounded schema, migration, stored-program, query, test, deployment, and
cross-database evidence for common PostgreSQL, SQL Server, MySQL/MariaDB, and
SQLite syntax. It does not render, compile frameworks, execute preprocessors,
connect to a database, or execute SQL.
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
the selected Git objects must already exist locally. The current change rules
require changed capability evidence for existing-capability modifications and
consolidate repository work-item partitions for one capability into a bounded
logical budget while preserving distinct capabilities.

## Review consequential uncertainty

```text
eh review packet ./my-repository --compact
eh review query ./my-repository --input-digest <packet-digest> --capability <id> --reason <reason>
eh review validate review-packet.json proposed-adjustments.json
eh review measure review-packet.json proposed-adjustments.json --subject <opaque-id> --session <opaque-id> --context compact
eh review benchmark compact.measurement.json broader-source.measurement.json
```

The provider-neutral review packet is rate-free and contains no source excerpts.
A surrounding AI session can request bounded capability, evidence, scope, or
explicitly selected admitted-source detail. EffortHours does not call a provider,
transmit repository material, or apply proposed adjustments. The caller controls
provider, privacy, disclosure, and retention choices.

Optional measurement commands sanitize completed-session telemetry and compare
compact review with a broader-source reference. They record only telemetry the
caller supplies, do not infer missing provider tokens, time, or cost, and do not
select an automatic review budget. Caller-supplied IDs, telemetry bases, and notes
are retained verbatim and must be non-sensitive.

## Offline and safety boundary

Default repository analysis does not execute target code, install target
dependencies, fetch from the network, or inspect Git history. Source trees are
treated as untrusted input, and reports avoid source excerpts by default.

## Current limitations

- `seed-rules/0.3.0` and `change-seed/0.7.0` remain experimental and uncalibrated.
- SQL uses bounded token/statement evidence mapped to existing priors; it is not a
  full grammar, schema diff engine, query optimizer, or database validator.
- Public calibration labels have not completed genuinely independent correction.
- TypeScript and TSX evidence is token-backed rather than compiler-backed.
- Multiple-PR and contributor-period portfolio estimation is not implemented.
- Host-review token use, cost, and estimate improvement have not yet been measured
  across representative repositories; no automatic review budget is selected.

The schemas, estimation decisions, calibration provenance, benchmarks, source,
issues, and contribution process are available in the
[EffortHours GitHub repository](https://github.com/VictorZakharov/efforthours).

EffortHours is distributed under the
[MIT License](https://github.com/VictorZakharov/efforthours/blob/main/LICENSE).
