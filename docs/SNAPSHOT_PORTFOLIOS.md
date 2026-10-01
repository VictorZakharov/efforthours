# Snapshot portfolios and dashboard refreshes

`snapshot-portfolio-manifest/1.0.0` measures whole codebases at calendar cutoffs.
It does not sum monthly Change EHE. The explicit maintainer request for this
surface reopens this product scope; seed priors and model admission are unchanged.
All repository EHE remains experimental and uncalibrated replacement effort.

```text
eh estimate portfolio --manifest portfolio.json --local local.json --checkpoint private-cache --as-of 2026-09-30T20:00:00Z --output result.json --no-rate
eh estimate portfolio --manifest portfolio.json --local local.json --checkpoint private-cache --preflight --previous-result result.json
eh portfolio-adapter --input result.json --studies studies.json --output numerical-asset.json
eh model measurement-identity
```

## Curated inputs and immutable selection

Public project IDs and ordered area definitions are separate from execution-only
paths/provider locators. Only explicitly mapped projects are used. No sibling
repository/workspace discovery occurs. JSON inputs reject unknown properties and
use the published schemas. A manifest contains:

```json
{
  "schemaVersion": "1.0.0",
  "protocolVersion": "snapshot-portfolio-manifest/1.0.0",
  "year": 2026,
  "timezone": "America/Toronto",
  "profile": "implementation",
  "snapshotPolicy": "git-archive/1.0.0",
  "baselineConvention": "january-1-zero",
  "projects": [{
    "id": "example",
    "ref": "main",
    "areas": [
      { "id": "application", "include": ["src/**"] },
      { "id": "support", "include": ["**"] }
    ]
  }]
}
```

The local-map schema is `snapshot-portfolio-local-map`. Its `projects` array must
match the manifest IDs exactly; each entry supplies either `repositoryPath` or
`gitHubRepository`. Provider inputs reuse the managed bare-cache acquisition
planner. Only `--fetch-missing` authorizes network/object acquisition. A provider
head alone does not prove complete history: insufficient/shallow local history
must be completed through an explicitly authorized acquisition workflow before
measurement. Ordinary runs never silently deepen source repositories.

Each ref is resolved once to an immutable head. One bounded first-parent history
read supplies committer timestamps and tree IDs. Traversal order, rather than
timestamp sorting, selects the first reachable commit strictly before the cutoff.
Timestamps only select artifacts and never value effort. January 1 is a declared
zero baseline; absent prior history is `assumed-zero`. A missing object or an
unproven shallow history is an error, never zero. Future periods omit effort;
the current period uses the single observation instant and remains partial until
the next local month begins. Calendar boundaries use IANA timezone/DST rules,
including skipped/ambiguous midnight. `--as-of` requires an offset and rejects
future instants. Without it, observation is captured once at command start.

`--reproduce result.json` binds the original manifest digest, heads, and observation;
moving upstream refs then cannot alter selection. `--year` and `--timezone` are
explicit manifest calendar overrides. Reproduction requires the original effective
manifest, including those overrides.

Preflight makes no exports, analysis, estimates, or checkpoint writes. It reports
pins, cutoffs, receipt hits, path-only area input comparisons, shallow/unavailable
history, and configured limits. Attribute-sensitive area predictions explicitly
require archive verification. Plans are `status: planned` and cannot be imported
as measurements or consumed by the adapter. An existing publication cannot be
overwritten by a plan. Missing local repositories fail input validation; unresolved
heads/history appear as unavailable planning rows.

## Archive and standalone-area policy

`git-archive/1.0.0` is an explicit new input policy, separate from the existing raw
Git-object reader. It preserves committed `export-ignore`, `export-subst`, and
explicit text/EOL attributes. The default conversion uses `core.autocrlf=false`
and `core.eol=lf` on every host. Global/system attributes are disabled. Mutable
`info/attributes` overrides are rejected, and configured clean/smudge/process
filters are disabled before archive conversion. No target tool or code executes.

Archive entries remain in a bounded virtual read-only filesystem; there is no
worktree materialization. Links, submodules, special entries, traversal, duplicate
portable paths, and empty archives fail closed. Source refs, index, worktree, and
FETCH_HEAD stay untouched. Reports/checkpoints must be outside measured source
repositories. Source paths never enter public portfolio reports.

Every archived file, including hidden paths and scanner-excluded content, has one
ordered first-match area owner. Selectors support relative paths/directories,
`*`, `**`, and `?`; they reject traversal and unsupported syntax. Each selector
must match, every area must own files, and the final area must use exactly `**`.
Applicable ancestor `.gitignore` and `.efforthoursignore` files are added as
context without changing ownership. Original relative paths are retained.

The full archive and each area are separately scanned and estimated in one
process. Immutable data-only file artifacts can be shared; whole-project evidence
is never sliced into area estimates. Standalone area semantics match independent
area runs with the same selected/context files and ownership decisions.

Allocated expected EHE equals project expected EHE times each standalone expected
weight divided by their sum. Deterministic largest remainders round to 0.01 hours,
with area order breaking ties. Allocations sum exactly to the whole project.
Positive whole effort without positive weights is rejected. These shares are
planning allocations, not standalone measurements or remaining migration budgets.

Local immutable repository-content commands accept a positional repository with
`--revision <pin>` under the archive policy. The matching provider form uses
`--repo <owner/name> --revision <pin> --snapshot-policy git-archive/1.0.0`.
Existing provider inputs retain raw-tree behavior when the policy is omitted.
Local trees/evidence without a revision keep their existing semantics.

## Receipts, compatibility, and migration

`snapshot-measurement-receipt/1.0.0` contains input and evidence digests, exact
low/expected/high effort and category totals, selected/context counts, original
producer version, measurement identity, hashed directory coverage, and hashes of
maintained production bodies. It emits no raw source, paths, aliases, URLs, or
private cache location. Its content-bound ID includes the original provenance.

Measurement compatibility includes profile, rules digest, normalization, inventory,
ignore, archive, area, deterministic options, and reviewed ownership digest. The
implementation digest is generated at build time from analysis/analyzer/core/
estimation/contract/selection source, relevant project files, dependency locks,
SDK selection, and shared build properties. Release/E2E tests recompute that
fingerprint. A source or dependency change conservatively invalidates compatibility;
a producer-only CLI version change does not. This avoids trusting a manually
maintained analyzer/version label. CLI-specific presentation changes are outside
analysis compatibility. Rates, display prose, output paths, and observation time
are outside measurement identity.

Commit bindings allow identical later snapshots to avoid both export and analysis.
Trees without attributes also reuse bindings under another commit while retaining
distinct Git provenance. Attribute-bearing trees conservatively bind the commit
because `export-subst` can change bytes. Changed snapshots reuse exact content
receipts for unchanged areas; changed whole snapshots are always reaggregated.

Successful receipts are durable before the final report is published. A stable
explicit `--checkpoint` supports reuse across output names/processes. Corrupt,
incomplete, and incompatible receipts/bindings are misses and are recalculated.
`--import-receipts` validates a complete known public result and preserves original
producer identities, allowing a fresh machine to reuse measurements without raw
private reports. `--import-historical` validates an existing v1 canonical estimate
and retains it privately as historical provenance. It cannot establish new semantic
compatibility, so it never becomes a current receipt. Unknown dashboard receipt
formats require a separately specified importer; they are not relabeled.

An existing output or explicit `--previous-result` with a different epoch blocks
publication unless `--upgrade rebuild` or `--upgrade new-epoch` is supplied.
Both produce a homogeneous requested series under current semantics and preserve
the prior publication as an immutable `.epoch-<digest>.json` sibling. Rebuild also
compares prior/current expected measurements. New-epoch starts an independently
labeled series without carrying those comparisons. Neither silently mixes old/new
semantics in one series; a consumer seeking mixed epochs needs a new contract.
The adapter retains that homogeneous-series requirement.

## Incremental reuse and reviewed ownership

Persistence extends `RepositoryAnalysisArtifactCache`; it does not introduce a
second analyzer. Only data-only common inspections, C# and JavaScript file results,
and supported Razor results are admitted. Syntax trees/project-reader state are
not serialized. Keys retain the existing content/analyzer/path/scope/package
context and add measurement semantics, the complete path set, and all relevant
configuration/control bytes. Changed configuration, ownership, imports inside
content, and path ownership invalidate rather than guessing. Other ecosystem
analyzers run cold while exact area/whole receipts remain reusable. Aggregation
and allocation always rerun over the complete selected snapshot.

Each project retains at most 8,192 private artifacts / 128 MiB, with 1-MiB entry
limits and deterministic hash-ranked eviction. Malformed entries are misses;
request/hit/invalidation/eviction counters remain telemetry. This is conservative
incremental reuse, not an approximation based on changed-file effort.

Projects may embed the existing reviewed vendor manifest. Exact hashes are
verified on each selected input; relevant decisions are reapplied to standalone
areas, while owned integration/configuration/tests remain represented. Public
shared-body overlap proposals identify project pairs and counts/bytes only.
Their disposition is always `review-required-no-exclusion`: duplicate content
never disappears across distinct products without a reviewed ownership decision.
That decision changes ownership semantics and may change totals, separately from
performance improvements. No cross-project integration allowance is invented.

## Publication, adapter, and bounds

Only a fully validated `snapshot-portfolio-report/1.0.0` replaces the requested
result, through a same-directory temporary file and atomic rename. Run/checkpoint
and output locks prevent overlapping publication. Failure/cancellation retains the
previous complete file and successful receipts, exits nonzero, and writes a
separate privacy-safe `.failure.json`; detailed errors are private stderr.
Operational cache/timing/memory observations, previous-value comparisons, and
review-status diagnostics do not enter the measurement semantic digest. Reviewed
area definitions and actual measured inputs remain digest-bound.

Limits include 256 projects, 256 areas/project, 64 selectors/area, 100,000 archived
files, 128-MiB history accounting, at most two simultaneous project sessions,
configurable archive/checkpoint/output/memory budgets, and a cancellable deadline.
Defaults are one project, 256-MiB export, 512-MiB checkpoint, 32-MiB output,
2-GiB observed process working set, and 3,600 seconds. A sampled memory budget
cancels a run; it is not an exact allocator quota. Git children are hidden on
Windows. Timing/memory thresholds never gate ordinary CI.

Pricing is rate-free by default. Explicit `--hourly-rate` and optional `--currency`
create a caller-supplied rate card after effort estimation; no currency conversion
occurs and changing rates requires no export/analysis.

The adapter reads one complete validated report and `snapshot-dashboard-studies`.
Every project/area requires an authored study, an area-definition digest, a public
repository URL, an authored folder, and a reviewed commit binding. Folder presence
is checked through hashed directory coverage before constructing commit-pinned
links; presence does not prove runtime/tested behavior. Changed inputs flag review,
and that flag persists across repeat refreshes. A binding must match the current
pin or a prior pin with the exact same area receipt to approve its reuse. The
adapter rejects missing/stale studies, changed unreviewed inputs, and missing
folders before replacing its asset. It does not invent product descriptions.

`snapshot-category-groups/1.0.0` maps the complete category taxonomy into design,
implementation, validation, delivery, and documentation. The adapter's one atomic
`snapshot-dashboard-asset/1.0.0` contains numerical periods, standalone/allocated
areas, receipt/epoch bindings, category groups, links, and derived Markdown tables.
Website layouts, authored prose, site commits/tests, and deployment remain consumer
responsibilities. No actual website or private representative portfolio is changed.

CI uses synthetic/public-safe unit and process fixtures for selection, DST,
standalone parity, warm reuse, corruption, imports, dependency context, archive
safety, failure/resume, and adapter checks. Explicit benchmark records compare
cold/warm native runs with separate installed-CLI whole/area runs on one machine;
they report operation counts and measured times, not universal latency promises.
