# EffortHours documentation

Start with the repository [README](../README.md) for installation, current
capabilities, limitations, and CLI examples. This directory keeps the deeper
product contracts, engineering records, and reproducible checkpoints out of the
repository root without discarding their decision history.

## Current product and engineering contracts

- [Product charter](PRODUCT.md) defines Equivalent Human Effort, the modeled
  worker, profiles, goals, and non-goals.
- [Estimation model](ESTIMATION_MODEL.md) defines how evidence becomes effort and
  Equivalent Replacement Cost.
- [Implementation plan](PLAN.md) records current architecture and roadmap status.
- [Change estimation](CHANGE_ESTIMATION.md) defines repository, commit, range,
  pull-request, portfolio, and contributor-period semantics.
- [Change portfolio checkpoint](MILESTONE_CHANGE_PORTFOLIOS.md) records the
  multiple-PR/manifest/author-period reconciliation policy and fixture matrix.
- [Static SQL analysis](SQL_ANALYSIS.md) defines supported SQL evidence, dialect
  confidence, effort mapping, safety bounds, Change behavior, and limitations.
- [Static Python analysis](PYTHON_ANALYSIS.md) defines package discovery, bounded
  token/indentation evidence, framework qualification, exclusions, Change
  behavior, benchmark results, and non-goals.
- [Static Go analysis](GO_ANALYSIS.md) defines module/workspace discovery, bounded
  token and import-qualified evidence, build uncertainty, exclusions, Change
  behavior, benchmark results, and non-goals.
- [Static Java analysis](JAVA_ANALYSIS.md) defines Maven/Gradle project discovery,
  bounded token and import-qualified evidence, build/runtime uncertainty,
  exclusions, Change behavior, benchmark results, and non-goals.
- [Static Kotlin/JVM analysis](KOTLIN_ANALYSIS.md) defines shared JVM project
  ownership, Kotlin source/script boundaries, server and Android semantics,
  compiler-plugin uncertainty, exclusions, Change behavior, benchmarks, and
  non-goals.
- [Static Terraform and HCL analysis](TERRAFORM_HCL_ANALYSIS.md) defines admitted
  artifacts, bounded semantics, local module ownership, exclusions, effort
  mapping, Change behavior, benchmarks, and non-goals.
- [Source-file budgets](CODE_BUDGETS.md) documents the enforced early-refactoring
  policy.
- [Release procedure](RELEASING.md) defines the source-visibility and NuGet
  publication boundary.

## Calibration and review records

- [Milestone 7](MILESTONE_7.md) defines reviewed labels, repository-held-out
  evaluation, mutation guardrails, and model-admission requirements.
- [Seed-model review records](MODEL_REVIEWS.md) disclose provisional realism and
  contamination checks without presenting them as independent calibration.
- [Change model-admission policy](CHANGE_MODEL_ADMISSION.md) freezes the admission
  order and metrics for future Change estimators.
- [Milestone 8](MILESTONE_8.md) and its
  [measurement checkpoint](MILESTONE_8_MEASUREMENT.md) define provider-neutral
  host review and sanitized cost/usefulness measurement.

## Reproducible engineering checkpoints

- [Scanner benchmarks](BENCHMARKS.md) records performance, memory, and read-only
  safety measurements.
- [Reporting benchmarks](REPORT_BENCHMARKS.md) records compact-output size and
  usefulness checks.
- [Milestone 5](MILESTONE_5.md) and [Milestone 6](MILESTONE_6.md) record the seed
  estimator and reporting decisions.
- [Change Milestones 1](MILESTONE_CHANGE_1.md),
  [2](MILESTONE_CHANGE_2.md), and [3](MILESTONE_CHANGE_3.md) record the initial
  Change implementation, calibration infrastructure, and synthetic teacher corpus.

These checkpoint documents remain intentionally available because they explain
why current schemas and semantics exist. They are historical evidence, not a claim
that every limitation described there has since been resolved.

## Project policies

Contribution, conduct, governance, security, licensing, notices, and release notes
remain at the repository root for normal GitHub discovery:

- [Contributing](../CONTRIBUTING.md)
- [Code of Conduct](../CODE_OF_CONDUCT.md)
- [Governance](../GOVERNANCE.md)
- [Security](../SECURITY.md)
- [Changelog](../CHANGELOG.md)
- [MIT License](../LICENSE)
- [Third-party notices](../THIRD-PARTY-NOTICES.md)
