# Change Estimation MVP

## Status

Complete as of August 6, 2026. At that checkpoint the implementation was
experimental and uncalibrated. It estimates counterfactual Equivalent Human Effort
represented by a final artifact delta; it does not reconstruct actual work or timesheets.
The global-tool package advances to `EffortHours.Tool` `0.8.0-alpha.1`.

The behavioral-safeguard checkpoint was completed on August 9, 2026. It adds the
remaining cancellation and category-isolation coverage without changing
`change-seed/0.1.0`, any contract, or any calibration artifact.

The non-Git snapshot follow-on was completed on August 10, 2026. It adds paired
directory and paired evidence-bundle base/head selectors without changing the v1
contracts or `change-seed/0.3.0` valuation rules.

The generated-customization normalization follow-on was completed on August 10,
2026. It advances the source identity to `change-seed/0.4.0` and represents only
valid, explicit, EffortHours-specific `<custom-code>` projections inside otherwise
generated source. Generated bodies, ambiguous markers, oversized blobs, and
bodyless evidence remain excluded. The v1 contracts, repository priors, frozen
Change reports, and labels are unchanged.

The range-normalization reporting follow-on was completed on August 10, 2026. It
advances the source identity to `change-seed/0.5.0`, adds deterministic expected-
point gross-to-final and bounded rework-like diagnostics for explicit multi-commit
ranges, and adds saved-report explanation lineage. It changes no EHE prior,
normalized final-delta total, calibration label, or frozen report.

The scale-and-admission follow-on was completed on August 10, 2026. It advances
the source identity to `change-seed/0.6.0`, passes the first model-authored logical
gate for 4-to-32-hour changes, partitions mixed-role evidence without inflating
totals, decomposes larger candidate items into named roughly-one-hour phases,
reuses immutable snapshot analysis, bounds optional range audits, and records local
million-line and 128-commit performance gates. The implementation remains
experimental and is not empirically production-validated.

## Delivered scope

- `eh change <repository> --base <revision> --head <revision>`
- `eh change <repository> --commit <revision> [--parent <revision>]`
- `eh change <repository> --range <base>..<head>`
- `eh change <repository> --pr <number-or-url> [--repo <owner/name>]`
- `eh change --base-path <directory> --head-path <directory>`
- `eh change --base-evidence <evidence.json> --head-evidence <evidence.json>`
- implementation and recreation profiles, JSON or Markdown, compact JSON, explicit
  output paths, the bundled dated rate, caller rate overrides, and effort-only mode
- saved-report work-item and normalization-lineage explanation through
  `eh change explain`

Multiple PRs, author-period portfolios, and shared credit remain deferred.

## Architecture

`EffortHours.Change` is a reusable, provider-neutral snapshot comparison and Change
EHE library. Git selection is an adapter around it. Immutable Git trees are exposed
through the existing repository-file-system abstraction using `git ls-tree` and a
bounded `git cat-file --batch` reader. No checkout, fetch, dependency installation,
target-code execution, or target-repository write occurs.

The non-Git adapter scans two directory roots through the same safe static pipeline,
bounds each snapshot to admitted file facts, and records the scanner-compatible
content digest as its immutable identity. Hash-checked reads reject a selected body
that changes after pinning. The evidence adapter validates two saved v1 inventories
and reuses their frozen analysis without target-tree access. Because evidence files
do not embed source bodies, modified maintained paths that otherwise qualify as
represented keep one conservative edit region and an explicit warning instead of
receiving formatting-only exclusion.

The optional GitHub adapter invokes `gh pr view` only for PR number/URL and immutable
base/head object IDs. Analysis requires those objects in the local Git database.
PR authors, text, comments, reviews, timestamps, activity, and private diff bodies
do not enter the report or effort model.

## Valuation and reconciliation

The analyzer compares final base/head objects and classifies added, modified,
removed, exact-move, formatting-only, generated, vendored, minified, binary,
lockfile, build-output, exact-copy, and unsupported paths. Maintained final changes
feed granular work items through repository capability deltas plus bounded
edit-region rules. Lines of code and deleted volume are not value multipliers.
Generated files may contribute only when exact, balanced, EffortHours-specific
`<custom-code>` markers isolate maintained content; stronger exclusions still win
and the generated body never contributes.

For ranges, each commit is also estimated against its selected parent for audit.
The normalized final base-to-head estimate is authoritative. Reports name shared
setup, overlap, revert, and residual interaction adjustments and allocate normalized
expected hours exactly across components. Clean disjoint no-rework changes are
guarded by the greater of 10% or one hour.

Explicit ranges with at least two commit components also report gross isolated EHE,
normalized final-delta EHE, gross-to-final normalization share, and a separately
bounded rework-like share containing only negative overlap/revert attribution.
Zero-gross ranges report shares as not applicable; net-above-gross ranges preserve
positive interaction. These expected-point diagnostics are not multipliers,
historical labor, rework reconstruction, or productivity scoring. Base/head and PR
selectors do not infer an intermediate history.

The initial model identity was `change-seed/0.1.0`, composed with
`seed-rules/0.2.1`. The current source identity is
`change-seed/0.6.0+seed-rules/0.3.0`. It is admitted only as the experimental
Stage A logical baseline for 4-to-32-hour changes; its ranges are not formal
probability intervals and it is not production-validated.

## Contracts and output

The v1 schema catalog now includes:

- `change-evidence`
- `change-estimate-report`
- `change-estimate-explanation`

Selectors, observed path evidence, inferred normalization, work items,
reconciliation, verification, diagnostics, and pricing remain separate. Stable IDs
derive from immutable selection/evidence/rule inputs. Reports contain no source
excerpts.

## Verification

Memory-only unit fixtures cover meaningful code, tests, documentation, migrations,
external integrations, CI, container delivery, deletion and simplification,
formatting, literal whitespace, exact movement/copying, generated and lock output,
determinism, schema-valid reports/explanations, clean additivity, overlap, reverts,
and cancellation before and during snapshot opening. Category-isolation checks
retain low, expected, and high range behavior. The unit suite performs no physical
repository reads or writes.

The separate end-to-end suite uses temporary Git repositories to cover root and
ordinary commits, ranges, merge-parent ambiguity, immutable worktree behavior,
stdout/stderr separation, deterministic CLI output, and an offline PR identity
substitution. The `gh` JSON/error boundary is tested without filesystem or network
access.

The non-Git follow-on adds memory-only directory/evidence planner tests and
process-level pairs with no Git repository. They cover deterministic identities,
unchanged target trees, digest movement failure, saved-evidence reuse, conservative
bodyless modification, invalid inventory digests, and incomplete or mixed CLI
selector rejection.

The installed CLI now translates the first Ctrl+C into cooperative cancellation
through the complete command pipeline. Cancellation writes only a concise stderr
diagnostic and returns exit code 130; a second Ctrl+C retains the operating system's
immediate-termination behavior. This checkpoint passes 115 memory-only unit tests
and 33 process-level end-to-end tests.

## Maintainability checkpoint

The former 2,391-line `EffortHoursApplication.cs` was split into focused partial
command modules; its dispatcher is now small. `eng/file-budgets.json` enforces a
500-line default, a 400-line CLI ceiling, and explicit ratchets for legacy debt.
New code should be split near 80% of its applicable ceiling. The disk-backed budget
test lives only in the end-to-end suite; ordinary unit tests remain memory-only.

## Known limitations

- Change has model-authored logical calibration labels but no empirical production
  calibration; the current admission applies only to the first 4-to-32-hour band.
- TypeScript source semantics remain token-backed through the repository analyzer.
- Formatting exclusion is intentionally conservative and limited to the initial
  .NET and JavaScript/TypeScript source extensions.
- Saved repository evidence has no source bodies, so modified evidence-only paths
  cannot receive formatting-only exclusion or detailed edit-region analysis.
- Exact blob movement/copying is recognized; general semantic clone detection is
  not.
- Range component selection uses first-parent comparison for merge components and
  reports that limitation; normalized final effort remains authoritative.
- PR objects must already exist locally; EffortHours does not fetch them.
- Change-specific million-line and 128-commit performance gates are recorded for
  one workstation; cross-platform and realistic large-monorepo measurements remain.
