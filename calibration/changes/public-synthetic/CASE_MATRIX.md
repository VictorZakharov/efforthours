# Public synthetic Change EHE case matrix 0.1.0

Status: **design-frozen; preliminary teacher labels available; not independently reviewed**

This matrix was frozen on August 6, 2026, before reviewing candidate totals. It
defines the first 24 small final-change cases and their repository-owned
partitions. The fixtures will be synthetic, MIT-licensed EffortHours project assets;
Git activity, elapsed time, author data, and actual labor records are out of scope.

All cases from one repository family remain in one partition. Validation and test
cases must not influence priors, correction factors, thresholds, or candidate
selection.

| Case ID | Repository family | Partition | Ecosystem | Final-delta scenario | Required coverage tags |
|---|---|---|---|---|---|
| `change:dn-lib-production` | `repository:change-dn-lib-a` | development | .NET | Add one bounded production capability | `dotnet`, `production`, `clean-disjoint` |
| `change:dn-lib-tests` | `repository:change-dn-lib-a` | development | .NET | Add focused unit tests to existing behavior | `dotnet`, `tests`, `clean-disjoint` |
| `change:dn-lib-docs` | `repository:change-dn-lib-a` | development | .NET | Add maintained usage documentation | `dotnet`, `documentation`, `clean-disjoint` |
| `change:js-service-integration` | `repository:change-js-service-a` | development | JavaScript | Add an external HTTP integration boundary | `javascript`, `integration`, `production` |
| `change:js-service-config` | `repository:change-js-service-a` | development | JavaScript | Add integration configuration and validation | `javascript`, `configuration`, `integration` |
| `change:js-service-deletion` | `repository:change-js-service-a` | development | JavaScript | Deliberately remove an obsolete endpoint | `javascript`, `deletion`, `validation` |
| `change:ts-package-generated` | `repository:change-ts-package-a` | development | TypeScript | Add conventional generated client output only | `typescript`, `generated-conventional`, `zero-delta` |
| `change:ts-package-customization` | `repository:change-ts-package-a` | development | TypeScript | Add maintained generated-client customization | `typescript`, `generated-customization`, `integration` |
| `change:ts-package-delivery` | `repository:change-ts-package-a` | development | TypeScript | Add package/release configuration | `typescript`, `delivery`, `packaging` |
| `change:mixed-disjoint-range` | `repository:change-mixed-app-a` | development | mixed | Compose three clean disjoint commits | `mixed`, `clean-disjoint`, `range`, `additivity` |
| `change:mixed-overlap-range` | `repository:change-mixed-app-a` | development | mixed | Repeatedly edit one final capability | `mixed`, `overlap`, `range`, `reconciliation` |
| `change:mixed-revert-range` | `repository:change-mixed-app-a` | development | mixed | Add and completely revert one capability | `mixed`, `revert`, `range`, `zero-delta` |
| `change:dn-data-migration` | `repository:change-dn-data-b` | validation | .NET | Add a schema migration and model update | `dotnet`, `migration`, `data` |
| `change:dn-data-integration` | `repository:change-dn-data-b` | validation | .NET | Add API, persistence, and integration behavior | `dotnet`, `integration`, `data`, `production` |
| `change:dn-data-removal` | `repository:change-dn-data-b` | validation | .NET | Remove a persisted field with migration work | `dotnet`, `deletion`, `migration`, `validation` |
| `change:js-lib-formatting` | `repository:change-js-lib-b` | validation | JavaScript | Reformat maintained source only | `javascript`, `formatting-only`, `zero-delta` |
| `change:js-lib-move` | `repository:change-js-lib-b` | validation | JavaScript | Move an exact source body without behavior change | `javascript`, `exact-move`, `zero-delta` |
| `change:js-lib-quality` | `repository:change-js-lib-b` | validation | JavaScript | Add tests and documentation without production edits | `javascript`, `tests`, `documentation` |
| `change:ts-service-integration` | `repository:change-ts-service-c` | test | TypeScript | Add a protocol integration with error handling | `typescript`, `integration`, `production` |
| `change:ts-service-delivery` | `repository:change-ts-service-c` | test | TypeScript | Add CI and container delivery artifacts | `typescript`, `delivery`, `ci`, `container` |
| `change:ts-service-tests` | `repository:change-ts-service-c` | test | TypeScript | Add unit and integration tests | `typescript`, `tests`, `integration-testing` |
| `change:mixed-tool-feature` | `repository:change-mixed-tool-c` | test | mixed | Add one cross-ecosystem CLI capability | `mixed`, `production`, `integration` |
| `change:mixed-tool-simplify` | `repository:change-mixed-tool-c` | test | mixed | Remove obsolete behavior and update docs | `mixed`, `deletion`, `documentation`, `validation` |
| `change:mixed-tool-range` | `repository:change-mixed-tool-c` | test | mixed | Compose independent code, test, and docs commits | `mixed`, `clean-disjoint`, `range`, `additivity` |

The matrix intentionally includes exact-zero qualitative cases. Numerical corpus
records were created only after the reproducible fixtures and source estimates
were frozen. Generator `change-fixture-generator/0.1.0` materializes all 24
effort-only `change-seed/0.1.0` reports and matching blind authoring packets under
`0.1.0/`; formatting, exact movement, conventional generation, and complete revert
cases are exact zero. The preliminary teacher corpus has 121 targets, including 22
lineage-preserving exact-zero exclusions. A separate host-AI or human reviewer must
complete the independent pass before any record advances beyond
`teacher-estimate`.
