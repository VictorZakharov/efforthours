# GitHub-assisted today-to-date author portfolios

Status: implemented explicit orchestration boundary

## Purpose

The current-day engineering EHE-to-capacity workflow is one invocation:

```text
eh change today --owner <github-owner> --author "@me" \
  --timezone America/Toronto --include-open-prs --scope engineering \
  --capacity-hours 8 --format markdown --output <today.md> --no-rate
```

The command can run outside every source checkout. It does not accept or discover a
workspace and does not enumerate sibling repositories. It composes authenticated
provider discovery, a private EffortHours-managed bare cache, the v1 author-period
manifest selector, native engineering path admission, exact bounded preflight,
portfolio reconciliation, one partial daily bucket, and a caller-supplied reference
denominator. It does not introduce another estimator or alter an EHE prior.

## Frozen snapshot

The process captures one UTC `asOf` instant. The selected interval is local midnight
in `--timezone` inclusive through that exact `asOf` instant exclusive. Provider commit
queries, immutable heads, the in-memory manifest, bucket, report timestamp,
checkpoint identities, and verification lineage all refer to this snapshot. A normal
ISO-8601 `--generated-at` may supply the instant for reproducible runs.

The command fixes author-date selection, merge exclusion, and coauthor inclusion
unless the caller explicitly selects another supported policy. Identity and time
select immutable changes only and never multiply effort.

## Provider discovery

`change today` explicitly opts into GitHub access through authenticated `gh`. It:

- lists repositories for the requested owner, including archived/mirror metadata;
- ignores repositories without a default branch and repositories excluded by scope;
- queries default-branch commits inside the frozen interval;
- fully paginates current open PRs when `--include-open-prs` is present;
- considers only PRs authored by the authenticated/requested identity;
- retains only default or PR heads with an exact author/coauthor/date/merge match; and
- pins provider object IDs before acquisition.

Inactive repositories and open PRs with no selected work remain privacy-safe counts;
they cause no cache entry or analysis. Collaborator/bot PRs are not admitted merely
because the owner controls the repository.

`@me` resolves the active GitHub login and authorized verified commit emails, plus
explicit supplemental aliases. It never reads local Git configuration. Raw aliases,
owner names, repository display names, PR numbers, provider bodies, and credentials
are absent from reports.

## Managed repository cache

The default cache is:

```text
%LOCALAPPDATA%/EffortHours/repositories/github/<owner>/<repository>.git
```

`EFFORTHOURS_REPOSITORY_CACHE` may select another root. Each active entry is a bare
repository. Missing entries are created automatically. Acquisition fetches only the
selected default/PR source refs, with no tags, submodules, checkout, index,
`FETCH_HEAD`, local-ref update, or user worktree mutation. Each pinned commit is
verified after fetching, so a moved provider ref fails closed. Existing immutable
objects are reused. Acquired object and byte deltas are operational telemetry.

## Engineering scope

`--scope engineering` loads the bundled `engineering-scope/1.0.0` profile or a
persistent override from:

```text
%APPDATA%/EffortHours/scope-profiles/engineering.json
```

`EFFORTHOURS_ENGINEERING_SCOPE_PROFILE` selects an explicit override path. Missing,
malformed, noncanonical, or unsupported explicit profiles fail closed. Overrides
must state `mode: extend` or `mode: replace`. Effective rules are canonicalized and
digest-bound. Inspect the effective contract without estimation with:

```text
eh change scope show engineering
```

The standard profile admits maintained source, tests/fixtures, developer tooling,
build/project configuration, CI/CD, migrations/schemas, runtime/deployment
configuration, and code-bearing UI templates/styles. Exclusion wins over inclusion.
Documentation/prose, `AGENTS.md`, dependency locks, media/binaries, generated/vendor/
build trees, benchmark results, and configured content banks are excluded.

Required repository overrides exclude EffortHours calibration and result/artifact
directories, `pte-core-exam/public/questions`, and the duplicate
`dotnet-image-viewer-archive` repository. Archived and mirrored repositories are also
excluded. Admission occurs before snapshot inventory, diff/evidence construction,
overlap/revert/context normalization, reuse identity, and reconciliation. Reports
separate identity-selected, admitted-engineering, and scope-empty commit counts.

## Internal preflight, estimation, and reuse

After acquisition, the ordinary manifest planner validates pinned heads and performs
the exact bounded identity selection. A distinct `preflight` phase checks its measured
selected-change accounting before snapshots and estimation. Declared planner resource
bounds remain authoritative; exceeding one produces a nonzero incomplete report and
never substitutes omitted work with zero.

Each active repository is analyzed once. Successful repository evidence is written
atomically and keyed by immutable heads, selection policy, estimation profile,
estimator identity, and effective scope digest. A later exact hit skips replanning and
analysis; advancing one head invalidates only that repository shard. Checkpoint hits/
misses/writes are distinct from within-run snapshot, artifact, inventory, and blob
reuse counters.

## Capacity and zero-work semantics

`--capacity-hours` is a positive caller-supplied reference denominator. The command
constructs the one contributor/bucket capacity entry in memory. Exact ratios are:

```text
X low      = EHE low / reference capacity hours
X expected = EHE expected / reference capacity hours
X high     = EHE high / reference capacity hours
```

For actual hours supplied outside EffortHours, `actual X = expected EHE / actual
hours`. Capacity and actual hours do not alter EHE and are not attendance,
productivity, performance, compensation, or authorship evidence. A complete day with
no active repositories is valid zero EHE and zero X for positive capacity.

## Output and failures

JSON and today Markdown render from the same contract-validated semantic result.
`--output` writes UTF-8 through a same-directory temporary file and atomically replaces
the exact destination only after validation and flush. Today Markdown contains status,
snapshot coverage, EHE/X low-expected-high, capacity policy, repository/head/change
counts, compact repository-attributed expected EHE, scope identity/exclusions,
estimator identities, checkpoint versus in-run reuse, end-to-end/per-phase timings,
and interpretation limits. It contains no trend chart, one-point OLS/R-squared,
first/latest change, synthetic `0%`, or duplicate contributor series.

Scope, provider, acquisition, preflight, or repository failures exit nonzero. A
validated incomplete report preserves the privacy-safe root phase/category/digest and
publishes no aggregate EHE or X factor. Atomic writing preserves any prior destination
if rendering or writing fails. Structured telemetry includes provider calls/pages,
acquired objects/bytes, phase timings, last progress, checkpoint counters, reuse, and
working-set peak; operational observations never enter the semantic digest.

## Trust and verification boundary

Provider access and Git acquisition are explicit orchestration exceptions only.
Ordinary scan, Change, and manifest estimation remain deterministic, offline, and
provider-independent. Target code and tools are never executed.

CI uses fake paginated provider responses, synthetic Git repositories, cache creation
and reuse, native-scope evidence tests, schema validation, privacy assertions,
complete-zero coverage, relevant-open-head filtering, failure artifacts, and a
process-level one-command fixture on hosts that can provide an executable `gh` shim.
Live-provider access and wall-clock targets remain manual measurements, not CI gates.
