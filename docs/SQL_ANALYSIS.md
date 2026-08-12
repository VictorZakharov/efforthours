# Static SQL analysis

## Status

EffortHours has experimental, offline static SQL support. Common scanner `0.2.2`
admits `.sql` files and SQL analyzer `0.1.0` produces bounded schema, migration,
stored-program, query, test, delivery, and cross-database evidence. Repository
estimator `seed-rules/0.3.0` maps supported SQL evidence to its existing transparent
data, integration, testing, and packaging priors; no SQL-specific rate was fitted.

The current Change source identity is
`change-seed/0.16.0+seed-rules/0.4.0`. SQL repository and Change estimates remain
experimental and uncalibrated. The earlier `change-seed/0.6.0` Stage A admission
did not contain SQL changes, so it is not evidence for SQL accuracy.

## Safety and admission boundary

The analyzer uses only common-scanner-admitted regular files inside the selected
repository scope. Each read is checked against the scanner's byte length and
SHA-256 digest. It accepts valid UTF-8 text up to eight mebibytes and tokenizes at
most 200,000 tokens per file. Symlinks, changed files, invalid encodings, oversized
files, and malformed or truncated constructs fail closed or retain explicitly low
confidence.

EffortHours does not:

- connect to a database or select a server;
- execute SQL, migrations, stored programs, seed data, or target tooling;
- install a database driver, parser service, or repository dependency;
- access the network or inspect Git history during ordinary analysis;
- use migration timestamps, version numbers, row count, or dump volume as effort
  multipliers; or
- emit SQL source excerpts in ordinary evidence or estimate output.

## Evidence recognized

The comment-, string-, quoted-identifier-, and PostgreSQL-dollar-quote-aware token
stream recognizes bounded occurrences of:

- DDL and schema surfaces: tables, views, indexes, primary/foreign/check/unique
  constraints, sequences, types/domains, functions, procedures, and triggers;
- query and modification surfaces: `SELECT`, joins, CTEs, parenthesized
  subqueries, window functions, transactions, and insert/update/delete/merge
  statements; and
- explicit cross-database syntax such as PostgreSQL foreign data or `dblink`, SQL
  Server linked/open queries, SQLite attached databases, MySQL federated engines,
  and database-link forms.

The analyzer labels parser confidence separately from dialect confidence. It can
identify conservative signals for PostgreSQL, SQL Server, MySQL/MariaDB, and
SQLite. Files without distinctive syntax remain `standard-or-unknown`; conflicting
signals remain `mixed-or-ambiguous`. Detection describes syntax evidence only. It
does not prove that a script is valid for, portable to, or successfully deployed
on a particular database version.

SQL files are associated with the deepest unambiguous containing .NET project or
JavaScript package. Otherwise they remain in a standalone SQL scope. Equal-depth
ownership conflicts are reported and do not get guessed.

## Artifact roles and effort mapping

Path and syntax evidence assign one conservative primary role:

| SQL role | Represented mapping |
| --- | --- |
| schema, migration, stored program, query | Existing data-modeling, persistence, and migration priors |
| maintained seed data | One bounded seed-intent unit; repeated rows are not separately valued |
| test fixture or test script | Existing integration/contract/component-testing priors |
| deployment, installation, bootstrap, or provisioning script | Existing packaging/deployment priors |
| explicit cross-database boundary | Existing external-integration prior in addition to supported data semantics |
| unknown/vendor-specific statement | Visible artifact and diagnostic only; no guessed semantic unit |
| dump, backup, export, generated snapshot | Excluded metadata only |

Migration ordering is inferred only from explicit directory/name conventions such
as Flyway-style version/repeatable prefixes or numeric prefixes. Ordering is
evidence, never elapsed-time or churn evidence. Reversibility, successful prior
application, and runtime ordering across tools are not inferred.

Byte-identical maintained SQL bodies share one content-digest semantic value while
every path remains traceable. Seed and test-fixture paths are kept distinct from
bulk dumps, but repeated seed/fixture rows are deliberately bounded. Conventional
dump headers, dump/backup/export paths, `COPY ... FROM STDIN`, and strong bulk-
insert shapes outside maintained seed/test paths are generated/excluded content.

## Change EHE

When source-readable snapshots are available, Change EHE supports `.sql` with a
literal-, comment-, quoted-identifier-, and dollar-quote-aware formatting
signature. Whitespace between SQL tokens can normalize to zero; whitespace or
content inside a string, quoted identifier, or comment remains meaningful. Saved
evidence bundles have no bodies and therefore retain the existing conservative
bodyless-modification behavior.

Final-delta rules still win over intermediate history: exact moves, exact copies,
formatting-only changes, generated dumps, vendored content, and complete reverts
produce no SQL body effort. Meaningful schema/query changes and removals map to
data work; test, delivery, and explicit cross-database roles map to their intended
categories. Deletion is positive bounded simplification work, never negative hours
or a function of deleted volume.

## Known limitations

This is a bounded token/statement analyzer, not a full dialect grammar, database
compiler, schema diff engine, query optimizer, or migration runner. In particular,
it does not establish:

- name binding, type correctness, object dependencies, reachability, permissions,
  transaction isolation, or stored-program control-flow complexity;
- dynamic SQL assembled in strings, application-embedded SQL, ORM query semantics,
  database-project build behavior, or non-`.sql` migration formats;
- query plans, indexes actually used, cardinality, data volume, latency,
  concurrency, locking, operational risk, or production performance;
- semantic equivalence between differently written queries or schemas;
- generated customization inside a dump, beyond the separate explicit Change
  protected-region contract; or
- exhaustive vendor syntax, delimiter commands, procedural languages, or every
  feature/version of the four named dialect families.

Unknown or structurally incomplete syntax stays visible with reduced confidence
and receives no invented units. Review the evidence and range when SQL is material
to a consequential estimate.

## Verification checkpoint

Memory-only tests cover schema/query/program measurements, all four dialect
families, role and scope isolation, privacy, deterministic schemas, exact-copy and
dump exclusion, formatting invariance, bounded seed volume, and SQL Change
classification. A process-level test covers stdout/stderr and source-disclosure
boundaries. Public synthetic mutation suite `0.6.0` contains 67 states and 247
passing low/expected/high relations; its 11 SQL states add formatting, duplicate,
dump, unknown-syntax, semantic directionality, test, delivery, cross-database, and
seed-volume guardrails. These are qualitative relations, not reviewed hour labels
or an accuracy claim.
