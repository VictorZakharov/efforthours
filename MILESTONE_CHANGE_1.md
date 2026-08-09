# Change Estimation MVP

## Status

Complete as of August 6, 2026. The implementation is experimental and
uncalibrated. It estimates counterfactual Equivalent Human Effort represented by a
final artifact delta; it does not reconstruct actual work or timesheets.
The global-tool package advances to `EffortHours.Tool` `0.8.0-alpha.1`.

## Delivered scope

- `eh change <repository> --base <revision> --head <revision>`
- `eh change <repository> --commit <revision> [--parent <revision>]`
- `eh change <repository> --range <base>..<head>`
- `eh change <repository> --pr <number-or-url> [--repo <owner/name>]`
- implementation and recreation profiles, JSON or Markdown, compact JSON, explicit
  output paths, the bundled dated rate, caller rate overrides, and effort-only mode
- saved-report work-item explanation through `eh change explain`

Multiple PRs, directory/evidence selectors, author-period portfolios, and shared
credit remain deferred.

## Architecture

`EffortHours.Change` is a reusable, provider-neutral snapshot comparison and Change
EHE library. Git selection is an adapter around it. Immutable Git trees are exposed
through the existing repository-file-system abstraction using `git ls-tree` and a
bounded `git cat-file --batch` reader. No checkout, fetch, dependency installation,
target-code execution, or target-repository write occurs.

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

For ranges, each commit is also estimated against its selected parent for audit.
The normalized final base-to-head estimate is authoritative. Reports name shared
setup, overlap, revert, and residual interaction adjustments and allocate normalized
expected hours exactly across components. Clean disjoint no-rework changes are
guarded by the greater of 10% or one hour.

The initial model identity is `change-seed/0.1.0`, composed with
`seed-rules/0.2.1`. Both its range behavior and confidence bounds require reviewed
calibration before consequential use.

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

Memory-only unit fixtures cover meaningful code, tests, documentation, deletion,
formatting, literal whitespace, exact movement/copying, generated and lock output,
determinism, schema-valid reports/explanations, clean additivity, overlap, and
reverts. The unit suite performs no physical repository reads or writes.

The separate end-to-end suite uses temporary Git repositories to cover root and
ordinary commits, ranges, merge-parent ambiguity, immutable worktree behavior,
stdout/stderr separation, deterministic CLI output, and an offline PR identity
substitution. The `gh` JSON/error boundary is tested without filesystem or network
access.

## Maintainability checkpoint

The former 2,391-line `EffortHoursApplication.cs` was split into focused partial
command modules; its dispatcher is now small. `eng/file-budgets.json` enforces a
500-line default, a 400-line CLI ceiling, and explicit ratchets for legacy debt.
New code should be split near 80% of its applicable ceiling. The disk-backed budget
test lives only in the end-to-end suite; ordinary unit tests remain memory-only.

## Known limitations

- Change priors have no reviewed Change EHE corpus or empirical calibration.
- TypeScript source semantics remain token-backed through the repository analyzer.
- Formatting exclusion is intentionally conservative and limited to the initial
  .NET and JavaScript/TypeScript source extensions.
- Exact blob movement/copying is recognized; general semantic clone detection is
  not.
- Range component selection uses first-parent comparison for merge components and
  reports that limitation; normalized final effort remains authoritative.
- PR objects must already exist locally; EffortHours does not fetch them.
- Change-specific million-line and large-range performance measurements remain to
  be recorded.
