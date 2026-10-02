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
measurement. `--fetch-missing` on a `gitHubRepository` mapping completes selected
ancestry in the locked private bare cache, including explicit unshallowing of that
cache when necessary. A failed acquisition remains an error. Ordinary runs never
silently deepen source repositories. See the acquisition recipe below.

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
heads/history appear as unavailable planning rows. Per-period failures retain their
known immutable pin and safe period/area IDs. Structured issues distinguish
`missing-object-or-ref`, `shallow-history`, `invalid-area-definition`,
`unmatched-selector`, `empty-owned-area`, `invalid-archive`,
`unsupported-source-entry`, and `incompatible-measurement-identity`; old saved
plans may retain the former `missing-history-or-head` value. Selector text, source
paths, and provider locators stay out of public plans. Expected adapter input errors
exit with invalid-input code 3 and an actionable stderr message.

Each project compiles its selectors once per distinct definition. Within one run,
identical trees reuse bounded inventories and attribute sensitivity, and identical
tree/definition plans reuse their exact path-only result. At most twelve selected
trees per active project are retained under the 100,000-file inventory bound.
Preflight remains read-only; progress identifies safe project IDs and phases on
stderr. Inventory/planning/compilation/reuse counts are optional operational fields
for saved-report compatibility and remain outside semantic digests.

## Daily replacement calendar

`--calendar daily` sets the additive manifest/report `calendarPolicy` to
`daily-replacement-calendar/1.0.0`. Omission preserves the monthly contract and
its saved JSON/digest verification. A daily batch uses the same curated projects, pinned
heads, observation, timezone, profile, archive/ownership policy, and checkpoint.
It measures whole repositories only: areas remain empty with `not-requested`
for selected snapshots. Area definitions are still validated input, but no
historical selector applicability, area scanning, or allocation is requested.
The absence of areas does not narrow whole-repository analysis context.

```text
eh estimate portfolio --manifest portfolio.json --local local.json --checkpoint daily-cache --calendar daily --reproduce monthly.json --import-receipts monthly.json --output daily.json --no-rate
eh estimate portfolio --manifest portfolio.json --local local.json --checkpoint daily-cache --calendar daily --reproduce daily.json --preflight
```

There is one January 1 zero opening baseline and 365 or 366 daily periods,
including weekends and idle dates. Each date selects the first eligible commit
in complete first-parent traversal order strictly before exclusive local next-day
midnight, capped at the frozen observation for the provisional date. Future dates
have no effort or delta. The first day's opening follows the existing explicit
January 1 zero convention even if a repository existed earlier. Every other
day uses the preceding day's stock. No earlier commit in proven complete history
is `assumed-zero`; missing objects, shallow history, and analysis failures are
errors. Selection never uses timestamp sorting or Git date-filter pruning.

`--reproduce monthly.json` with daily mode permits just this calendar expansion
of the original effective manifest and freezes its original heads and observation.
All other manifest fields/overrides must match. A daily reproduction file requires
the exact daily manifest. Imported compatible monthly whole bindings serve daily
dates and endpoints without exporting or re-estimating. Exact idle/unchanged
snapshots share native bindings, tree/content receipts, and durable artifacts.
Whole receipt values, hashes, producer provenance, model, and measurement identity
are preserved. A daily import from another measurement epoch fails explicitly;
the conservative build fingerprint changes with this implementation, so receipts
from an older binary may require a separately staged monthly rebuild under the
current producer. Keep the original monthly publication unchanged and review
that rebuild; never relabel old receipts as compatible to force reconciliation.

Each period retains selected commit/tree, cutoff, status, receipt reference, and
whole low/expected/high stock. `expectedChangeCentihours` is the signed integer
difference between successive expected stocks, including decreases. It is not
Change EHE, a new uncertainty range, or a labor measurement. `monthlyEndpoints`
contains the January baseline and twelve exact closing/provisional month snapshots
drawn from the same measurements. Validation requires exact per-project monthly
telescoping, which also conserves portfolio and any filtered-subset sums. When a
supplied monthly reference has the same head, timezone, year, and endpoint cutoff,
its selected commit/tree, receipt, and whole range must agree or publication fails.
There is no scaling, interpolation, commit-count allocation, or midpoint rewrite.

Daily periods also carry `activeCommitDateCount` and `benchmarkHours` (eight times
that count) from complete reachable ancestry of **that period's selected immutable
commit**, strictly before its exclusive cutoff. Count distinct local dates only
within the configured year, applying the configured timezone before admitting a
date. Final-head ancestry cannot supply capacity to an earlier snapshot: a branch
date becomes eligible only when its merge is reachable from the selected commit.
Nonmonotonic timestamps still require both reachability and the exclusive cutoff;
Git date pruning is not used. Baseline-zero and assumed-zero operands are explicitly
zero; future and unavailable operands remain absent. This is a presentation benchmark,
separate from first-parent artifact selection and EHE. Duplicate dates count once,
idle dates add nothing, and weekends/merged-branch commits count. The consumer can
calculate each project's stock expected hours divided by benchmark hours, then sum
project ratios. A zero
denominator is unavailable. Daily multiplier changes, if displayed, are differences
of successive values of that same benchmark, summed without rounding each date;
they are never Change EHE divided by eight. Future benchmark operands are absent.
Rates convert presentation values only and require no new source analysis.
Benchmark operands do not enter content receipt inputs or effort arithmetic;
implementation changes still follow the conservative epoch migration below.

Preflight reports daily selections, distinct selected commits, per-date receipt
hits/measurement requirements, and missing/shallow history without source export,
analysis, estimation, acquisition, or checkpoint writes. Daily mode retains the
same project/session, archive, artifact, output, memory, and deadline bounds.
At most twelve inventories per active project are retained across the year;
complete first-parent history and each reachable benchmark history read have a
128-MiB accounting bound. Benchmark reads stream directly into at most 366 local-date
starts. One query per distinct selected commit is reused across idle days in the
run; at most 366 compact commit inventories are retained per project. No full
timestamp histories are retained, and operands are recomputed read-only on warm
runs without exports or estimates. Successful receipts survive failures/cancellation
and resume through the shared atomic publication/lock mechanism. Dates are not extra projects.

The authored-study dashboard adapter remains monthly-only and rejects daily
reports explicitly. Consumers own safe calendar shards, index hashes, frontend
unit/filter behavior, signed-decrease presentation, and publication locks. Raw
native daily reports contain safe IDs, dates, immutable objects, benchmark operands,
receipt/provenance, and telemetry; they omit source paths/URLs, aliases, commit
messages, and source excerpts. Consumer public shards should allowlist the fields
they need and verify every project's monthly stock/delta reconciliation before
publishing. This feature does not update a consumer site or its saved measurements.

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

## Area request modes and evolving repositories

The additive v1 project field `areaMeasurementMode` defaults to `every-snapshot`,
which preserves the original strict behavior: the current definition must apply
to every selected archive. Explicit `latest-only` measures historical whole
snapshots across the year, then measures areas only for the last selected period
with a commit. This can be the current partial period. Historical measured periods
carry `areaDisposition: not-requested`, full whole receipts/ranges, and empty
`areas`; this means no area request, never zero area effort. The latest completed
allocation carries `areaDisposition: measured`. Plans use `requested` instead of
claiming a new measurement. Baseline, assumed-zero, and future periods retain their
original conventions. Every requested partition still enforces matching selectors,
nonempty ownership, exhaustive coverage, and exact allocation conservation.

For reviewed historical area trends, choose `revision-bound` and supply
`areaRevisions`, an array of at most thirteen `{commitObjectId, areas}` definitions.
Every selected commit must have an exact definition; missing applicability fails
as `invalid-area-definition`, and inapplicable selectors still fail explicitly.
The latest revision must equal the project's current `areas`. Each measured period
records its `areaDefinitionDigest`. This does not infer renames or invent historical
boundaries. Latest-only studies are preferable when historical reviews do not exist.

```json
{
  "id": "example",
  "ref": "main",
  "areaMeasurementMode": "latest-only",
  "areas": [
    {"id": "application", "include": ["src/**"]},
    {"id": "support", "include": ["**"]}
  ]
}
```

Whole commit/tree bindings exclude the area-definition digest. Requested-area
bindings include their exact definition. Changing a boundary can export the latest
archive to measure new areas, but reuses its unchanged whole content receipt and
all unchanged historical whole bindings. An exact warm rerun performs no exports
or estimator calls. Portable imports retain whole bindings independently of areas.
Measurement implementation changes still conservatively invalidate old epochs.

## Completing shallow history explicitly

An offline shallow plan exposes the pinned head, first available timestamp, and
`shallow-history`; all unproven periods remain unavailable. To leave the user's
clone untouched, replace only that project's execution-map locator with
`gitHubRepository: "owner/repository"` (omit `repositoryPath`), retain the curated
manifest/ref, and explicitly authorize acquisition:

```text
eh estimate portfolio --manifest portfolio.json --local managed-local.json --checkpoint private-cache --fetch-missing --as-of <original-observation> --output staged-result.json --no-rate
eh estimate portfolio --manifest portfolio.json --local managed-local.json --checkpoint private-cache --as-of <original-observation> --preflight
```

The first command resolves and verifies a pinned head and acquires complete
selected history in the managed cache. Use an immutable original head in the
manifest if exact original selection is required. The second command reuses it
offline; no provider request, source ref/index/worktree mutation, or implicit zero
is permitted. The caller needs provider access to private repositories. Provider
errors and incomplete upstream history fail closed.

Alternatively, the caller can deliberately complete their own clone before EH:

```text
git -C <clone> fetch --unshallow --no-tags origin
git -C <clone> rev-parse --is-shallow-repository
```

The latter must report `false`; EH then validates complete selected ancestry.
These are explicitly caller-run Git commands, never commands launched by ordinary
snapshot measurement. No limited-history equivalent policy is introduced.

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
Defaults are one concurrent project session, 256-MiB export, 512-MiB checkpoint, 32-MiB output,
2-GiB observed process working set, and 3,600 seconds. A sampled memory budget
cancels a run; it is not an exact allocator quota. Git children are hidden on
Windows. Timing/memory thresholds never gate ordinary CI.

Pricing is rate-free by default. Explicit `--hourly-rate` and optional `--currency`
create a caller-supplied rate card after effort estimation; no currency conversion
occurs and changing rates requires no export/analysis.

The adapter reads one complete validated report and `snapshot-dashboard-studies`.
Every project/area requires an authored study, an area-definition digest, and a
reviewed commit binding. Explicit `sourceVisibility` is `public` or `closed-source`
(the absent field retains legacy public-only behavior). Public studies require a
validated HTTPS GitHub repository URL and an authored folder. Closed-source studies
must omit the repository URL or set it to null. Their optional folders are private
verification inputs: supplied folders are hash-checked but never copied to assets.
Closed-source entries omit folder links entirely, without substitute labels,
private URLs, local paths, or access notices. Both visibility types enforce current
area-boundary and immutable review bindings. Folder presence
is checked through hashed directory coverage before constructing commit-pinned
links; presence does not prove runtime/tested behavior. Changed inputs flag review,
and that flag persists across repeat refreshes. A binding must match the current
pin or a prior pin with the exact same area receipt to approve its reuse. The
adapter rejects missing/stale studies, changed unreviewed inputs, and missing
folders before replacing its asset. It does not invent product descriptions.

`snapshot-category-groups/1.0.0` maps the complete category taxonomy into design,
implementation, validation, delivery, and documentation. The adapter's one atomic
`snapshot-dashboard-asset/1.1.0` contains explicit source visibility, numerical periods, standalone/allocated
areas, receipt/epoch bindings, category groups, links, and derived Markdown tables.
The asset schema also accepts saved public-only 1.0.0 assets. Manifest/report
fields are additive v1 extensions; absent mode means the original every-snapshot
behavior. Existing receipt contracts and producer identities are retained.
Website layouts, authored prose, site commits/tests, and deployment remain consumer
responsibilities. No actual website or private representative portfolio is changed.

CI uses synthetic/public-safe unit and process fixtures for selection, DST,
standalone parity, warm reuse, corruption, imports, dependency context, archive
safety, failure/resume, and adapter checks. Explicit benchmark records compare
cold/warm native runs with separate installed-CLI whole/area runs on one machine;
they report operation counts and measured times, not universal latency promises.

## Initial consumer migration recipe

The anonymized consumer's custom receipt contract was not supplied. Do not invent
an importer or label its receipts as native-compatible. Unknown input shapes fail
the bounded schema check. Preserve an immutable private copy of every original
receipt/publication, its checksum, producer identity, snapshot/ownership policy,
and low/expected/high ranges before migration. `--import-historical` accepts only
known canonical v1 estimate reports, retains their full old provenance privately,
and never creates current reusable receipts.

1. Freeze one observation instant and each project's immutable original head.
   Prepare one curated annual manifest with `latest-only` and an execution-only
   local/provider map. Resolve shallow history explicitly as above.
2. Validate authored current areas and studies, with explicit public/closed-source
   visibility and reviewed commit bindings. Run read-only preflight. Correct
   unavailable rows; do not split projects into independently published series or
   turn missing history into zero.
3. Rebuild once into a new staging output with one stable private checkpoint:

   ```text
   eh estimate portfolio --manifest migration.json --local local.json --checkpoint migration-cache --as-of <frozen-observation> --output staged-native.json --no-rate
   ```

   A successful output is one homogeneous native series after every project
   succeeds. Interrupted runs preserve the prior complete output and successful
   measurements; resume with the same inputs/checkpoint. For an old native epoch,
   use its result as `--previous-result` and explicitly `--upgrade rebuild`; retain
   the archived original epoch. That comparison records expected-hour differences.
4. Compare the original and rebuilt low/expected/high whole ranges and latest
   standalone area ranges privately for every project before consumer publication.
   Review policy/ownership differences explicitly. Never overwrite old ranges or
   producer identities to force equality. If old area boundaries cannot be proven,
   record the mismatch and deliberately review the new boundary; do not invent
   historical areas. Canonical original area estimates retain category totals too.
5. Produce the validated native asset in staging:

   ```text
   eh portfolio-adapter --input staged-native.json --studies reviewed-studies.json --output staged-asset.json
   ```

   A consumer-owned translation maps period IDs, native statuses, category groups,
   whole ranges, and latest numerical cards to its frontend contract. Preserve
   `not-requested` as absent area data, future as absent effort, and explicit zero
   conventions as zero. Verify all projects and comparison results, then atomically
   replace that consumer's complete publication under its own authorization.

EH does not modify authored studies, ownership decisions, source repositories, or
website numbers automatically. This recipe establishes a deliberate rebuild route;
it does not claim unverified custom legacy receipts are semantically compatible or
that the adapter is a drop-in frontend contract replacement.
