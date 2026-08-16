# EffortHours documentation

Start with the repository [README](../README.md) for installation, main workflows,
supported analyzers, safety boundaries, model status, and current performance.

This directory contains living contracts and reproducible engineering records.
Release history belongs in the [changelog](../CHANGELOG.md), not in the roadmap or
agent instructions.

## Product and estimation

- [Product charter](PRODUCT.md) defines EHE, the modeled worker, profiles, goals,
  product surfaces, and non-goals.
- [Estimation model](ESTIMATION_MODEL.md) defines how repository evidence becomes
  work items, planning ranges, uncertainty, gaps, and replacement cost.
- [Engineering plan](PLAN.md) records the implemented architecture, current
  baseline, delivery guardrails, and active priorities.
- [Reporting](REPORTING.md) defines canonical reports, compact projections,
  capability grouping, explanation, and output compatibility.
- [Pricing](PRICING.md) keeps the dated default rate and its public-data derivation
  separate from effort.

## Change EHE

- [Change estimation](CHANGE_ESTIMATION.md) governs immutable Git and non-Git
  selectors, final-delta normalization, range reconciliation, work items, and
  limitations.
- [Change portfolios](CHANGE_PORTFOLIOS.md) governs repeated PRs, cross-repository
  manifests, author-period selection, exact allocation, attribution uncertainty,
  and no-ranking safeguards.
- [Host-assisted author-period scaffolding](AUTHOR_PERIOD_SCAFFOLDING.md) records
  the accepted boundary for an optional provider adapter, reviewable discovery
  provenance, failure handling, privacy, and implementation tests.
- [Change model admission](CHANGE_MODEL_ADMISSION.md) freezes the progressive size,
  metric, performance, and evidence gates for Change estimators.

## Calibration and host review

- [Calibration](CALIBRATION.md) defines materiality-first repository comparison,
  corpus identity, repository-isolated partitions, review maturity, reviewed
  exclusions, mutation guardrails, offline metrics, and review workflows.
- [Repository model admission](MODEL_ADMISSION.md) preserves the historical frozen
  v1 evidence, accuracy, sharpness, safety, performance, selection, and one-time
  test gates for the completed rejected attempt. A future attempt needs a new
  policy.
- [Seed-model review records](MODEL_REVIEWS.md) retain dated diagnostic and
  contamination findings without presenting them as independent or empirical
  validation.
- [Host-review protocol](HOST_REVIEW.md) defines optional provider-neutral packets,
  digest-bound queries, selected-source disclosure, and non-applying adjustments.
- [Host-review measurement](HOST_REVIEW_MEASUREMENT.md) defines sanitized session
  telemetry, paired comparison, privacy, and budget-admission requirements.
- [Public calibration artifacts](../calibration/README.md) route to the exact
  corpora, rubrics, blind handoffs, mutation suites, and reproduction records.

## Analyzer boundaries

Each document states admitted inputs, static evidence, exclusions, Change behavior,
privacy/safety limits, model maturity, and non-goals for its ecosystem.

- [SQL](SQL_ANALYSIS.md)
- [Python](PYTHON_ANALYSIS.md)
- [Jupyter notebooks](JUPYTER_ANALYSIS.md)
- [Go](GO_ANALYSIS.md)
- [Java](JAVA_ANALYSIS.md)
- [Kotlin/JVM](KOTLIN_ANALYSIS.md)
- [Shell and PowerShell](SHELL_POWERSHELL_ANALYSIS.md)
- [Terraform and HCL](TERRAFORM_HCL_ANALYSIS.md)
- [PHP and Composer](PHP_COMPOSER_ANALYSIS.md)
- [Rust and Cargo](RUST_CARGO_ANALYSIS.md)
- [Docker and Compose](DOCKER_ANALYSIS.md)
- [C and C++](CPP_ANALYSIS.md)

The .NET and JavaScript/TypeScript/frontend boundaries are described in the
[README support matrix](../README.md#supported-analyzers), the product and model
contracts, and their implementation tests. A future extraction into dedicated
boundary documents should preserve those current semantics rather than duplicate
release history.

## Engineering records and procedures

- [Scanner benchmarks](BENCHMARKS.md) records reproducible performance, memory,
  allocation, and read-only safety measurements.
- [Reporting benchmarks](REPORT_BENCHMARKS.md) records compact-output size and
  usefulness measurements.
- [Source-file budgets](CODE_BUDGETS.md) documents the enforced early-refactoring
  policy and ratchets.
- [Release procedure](RELEASING.md) defines visibility, tagging, trusted NuGet
  publication, verification, and separately authorized release actions.
- [Host-review benchmark artifacts](../benchmarks/host-review/public-expansion/0.1.0/README.md)
  record the first sanitized paired comparison.

Completed milestone implementation playbooks are intentionally not part of the
living documentation set. Their durable decisions are consolidated above; exact
historical text remains available in Git, while immutable measurement and
calibration evidence remains checked in at its source.

## Repository policies

- [Contributing](../CONTRIBUTING.md)
- [Code of Conduct](../CODE_OF_CONDUCT.md)
- [Governance](../GOVERNANCE.md)
- [Security](../SECURITY.md)
- [Changelog](../CHANGELOG.md)
- [MIT License](../LICENSE)
- [Third-party notices](../THIRD-PARTY-NOTICES.md)
