# Public historical network checkpoint

## Scope and reproducibility

Recorded October 6, 2026 on Windows 10.0.26200, .NET 10.0.7, 24 logical
processors. These are real authenticated GitHub CLI calls and managed Git
acquisitions on the public `VictorZakharov/better-web-browser` repository and
`VictorZakharov/efforthours` control. The selected contributor is the public
`VictorZakharov` login. No NDA repository or private source was used.

The earlier recent checkpoint covers October 1 inclusive to October 6 exclusive,
2026, UTC, author-date selection, engineering scope, fixed eight-hour daily
reference, implementation profile, no pricing and no evidence checkpoint reuse.
The narrow scope includes only better-web-browser; the broad recent scope
explicitly includes these two repositories. This does not establish complete
account/organization coverage. The complete accessible owner inventory is still
validated before applying restrictions; unrelated account PR history is omitted.

Each scope starts with separate empty repository/provider caches, then repeats
with those exact caches in a new CLI process. A copied, frozen CLI directory
prevents builds from changing binaries during the experiment. No other build,
test or benchmark ran during either checkpoint's four timed rows. Ordinary
operating-system background load and network latency remain uncontrolled. These are single
observations, not distributions or a general latency guarantee.

Use the explicit opt-in driver after a Release build, with a frozen CLI copy:

```text
python eng/benchmark-historical-network.py --allow-network --cli-dll <frozen-cli>/efforthours.dll --output artifacts/historical-network-new --owner VictorZakharov --author VictorZakharov --repository VictorZakharov/better-web-browser --repository VictorZakharov/efforthours --narrow-since 2026-10-01 --broad-since 2026-10-01 --until 2026-10-06
```

The output directory must be new and ignored/private. Full source reports, learned
identities, provider sidecars, immutable object caches and stderr stay there.
The driver records binary/input-selection/semantic digests, wall time, acquisition,
provider receipts, reuse, selected changes and incomplete failure context. It
checks binaries after every row. A changed live input is non-comparable; a
semantic mismatch on the same complete selection returns nonzero. Neither
benchmark times nor sampled memory gate ordinary CI. The frozen CLI binary digest
for the earlier recent checkpoint is
`37fcbb4943243ff4664be08c3796916b5110309fce5bc53c7adeecd3e7d0b751`.

## Earlier complete recent cold and warm results

The explicit discovery/acquisition deadline is 900 seconds, observed growth budget
1,024 MiB, and driver deadline 1,800 seconds per row. That earlier build enforced:
256 repositories, 512 heads overall and 32 per repository; 2,048 provider adapter
requests; four historical readers; 12 PRs per metadata batch; at most ten 100-row
scoped inventory pages under one cumulative 16-Mi-character response bound;
64-KiB metadata entries and 1,000 retained entries. Repository analysis retains
its two-session/four-row queue, at-most-24 managed CPU work items, eight Git
readers, cache/ledger/checkpoint/output and buffered-read bounds.

| Scope/cache | Outer wall s | CLI wall s | Discovery/acquisition s | Queries/pages/process receipts | PR metadata hits | Selected changes | Snapshot requests/hits | Acquired objects | Observed acquired MiB | Sampled peak MiB |
| --- | ---: | ---: | ---: | --- | ---: | ---: | --- | ---: | ---: | ---: |
| Narrow cold | 47.188 | 46.564 | 10.422 | 20/20/20 | 0/152 | 106 | 214/105 | 19,344 | 10.562 | 838.516 |
| Narrow warm | 39.047 | 38.517 | 4.793 | 5/5/5 | 152/152 | 106 | 214/105 | 0 | 0 | 830.691 |
| Two-repository cold | 63.765 | 63.057 | 12.445 | 36/36/36 | 0/306 | 152 | 308/149 | 28,991 | 18.748 | 1000.422 |
| Two-repository warm | 57.813 | 57.200 | 6.872 | 8/8/8 | 306/306 | 152 | 308/149 | 0 | 0 | 984.527 |

The narrow inventory reviews 152 authored PR representations, retaining 12 PR
heads plus one default head. The broader recent inventory reviews 306 PRs,
retaining 38 PR heads plus two default heads. Warm rows perform no metadata batch
and acquire no new Git objects. They still refresh live inventory and fully
recalculate analysis without evidence checkpoints.

Cold and warm semantic digests match exactly within each final scope:

- Narrow: `sha256:c54a29c14f8d3f8c57da9430e208b5a7cffcfb7c5bdb7f0f38cf96f377c8d30a`.
- Two repositories: `sha256:0258d50aafa718daa9bc14af87bae2c5d0dca809392453455fcf946e23826a3c`.

The merged #260 baseline used the same narrow selected immutable rows and cutoff.
It recorded CLI wall 92.882 s cold / 63.953 s warm, discovery 56.163 / 29.012 s,
20/35 cold queries/pages and 18/33 warm, with zero warm PR metadata hits. Its
account-wide connection exceeded 1,000 account PRs after pagination; complete REST
inventory fallback discarded live base/count fields needed for cache reuse.
Scoped inventory removes those unrelated pages and preserves exact cache keys.
The final narrow CLI observations are about 1.99x and 1.66x faster respectively.
This is one public case, not an alpha.29 comparison or an NDA performance claim.

Exact total/category ranges, every item's isolated effort and allocated categories,
and all bucket/contributor series match the earlier narrow and broader results.
Initial cold/warm trials exposed a separate diagnostic-lineage defect: record
collection identity retained 230 warnings in one representation versus 135 with
shared cache objects, although their full diagnostic value sets were identical.
Structural diagnostic deduplication now makes both final digests deterministic;
it changes redundant warning transport, not numerical estimates or priors.

`phaseTimings[].elapsedMilliseconds` is cumulative overlapping work, explicitly
labeled `elapsedKind: cumulative-work`; it must not be summed or read as CLI
latency. CLI and per-repository elapsed values are separate wall observations.
Provider requests include child startup and transport; wire time is not isolated.
Observed Git growth is object-store growth, not a hard network-byte cap. Sampled
working set is an observation, not a heap quota or CI threshold.

## Earlier annual scope failure boundary

A separate cold/warm request included both repositories over October 6, 2025 to
October 6, 2026 UTC, under the same limits. Discovery completed all 306 candidate
PR metadata records and found 301 matching representations, including more than
32 distinct retained heads in each repository. It therefore failed at the existing
per-repository head bound before portfolio estimation. No aggregate, daily-zero
cells or semantic result were published. This is an annual discovery/failure
checkpoint, **not a complete annual EHE runtime measurement**.

| Annual row | Outer wall s | Queries/pages/process receipts | Metadata hits | Acquired objects | Observed acquired bytes | Result |
| --- | ---: | --- | ---: | ---: | ---: | --- |
| Cold | 15.781 | 36/36/36 | 0/306 | 28,991 | 19,658,752 | Incomplete head scope |
| Warm | 4.812 | 8/8/8 | 306/306 | 0 | 0 | Incomplete head scope |

The first observation exposed a generic GitHub-health error for this local scope
limit. A corrected repeat now reports `github-discovery-budget-exceeded`, active
phase `open-pr-discovery`, the observed distinct-head count, zero retries, and
`inspect-head-scope-or-use-pinned-manifest`. No heads are silently dropped.
Completed objects and metadata remain reusable. The successful two-repository
recent rows above establish end-to-end acquisition/analysis behavior separately
from this annual-bound failure.

Memory-only tests gate scoped pagination, exact cold/warm request/cache counts,
base invalidation, total/cursor/response bounds, root-failure cancellation/draining,
accurate head-limit context and diagnostic equality. Existing physical deadline,
byte-budget, provider-child termination and estimation-failure tests preserve
partial observations, reusable immutable evidence and nonzero incomplete output
without aggregate EHE. The synthetic 16-repository annual checkpoint remains in
[HISTORICAL_PR_DISCOVERY_BENCHMARK.md](HISTORICAL_PR_DISCOVERY_BENCHMARK.md).
The private large-repository case still needs a same-input consumer retest.

## Complete annual cold and warm results

The follow-up implementation at `59cbfffa58f011362b7e528dcc5cf399a775b127`
removes the earlier per-repository 32-head barrier without dropping retained
heads. One repository can now use the existing 512-head overall envelope through
one bounded reachability walk and a separately charged 128-MiB head-membership
ledger. Acquisition uses sequential batches of at most 32 refs and 32 negotiation
tips; verified completed heads seed later batches so shared history need not be
retransferred. The 256-repository limit and existing analysis caches, queues,
checkpoint/output limits and processor/read concurrency bounds remain fixed.
The metadata reader/page/response bounds above also remain fixed.

This checkpoint uses October 6, 2025 inclusive to October 6, 2026 exclusive, UTC,
with the same author-date, engineering, implementation, no-pricing settings and
365 daily buckets. Both scopes cover the full year: narrow is better-web-browser,
broad adds EffortHours. Each starts from its own empty provider/object caches;
its warm repeat is a fresh CLI process over those same caches, with no evidence
checkpoint reuse. The frozen CLI digest is
`551411b9340d6f16e0926567a4ac012a57c8caf459283b64b21c3160c37f54ec`.
Use the same driver with the annual interval explicitly selected:

```text
python eng/benchmark-historical-network.py --allow-network --cli-dll <frozen-cli>/efforthours.dll --output artifacts/historical-network-annual-new --owner VictorZakharov --author VictorZakharov --repository VictorZakharov/better-web-browser --repository VictorZakharov/efforthours --narrow-since 2025-10-06 --broad-since 2025-10-06 --until 2026-10-06 --discovery-seconds 900 --acquired-mib 1024 --run-seconds 1800
```

All four rows exit zero with complete scope, no pending metadata, no failures and
no driver deadline. These complete annual measurements supersede the earlier
annual failure boundary above; those earlier failure times are not runtime
baselines. Live inventory now includes 307 candidate PR representations across
the two repositories, rather than the earlier 306, so only the new cold/warm
pairs are compared on exact pinned inputs.

| Annual scope/cache | Outer wall s | CLI wall s | Discovery/acquisition s | Queries/pages/process receipts | PR metadata hits | Selected changes | Snapshot requests/hits | Acquired objects | Observed acquired MiB | Sampled peak MiB |
| --- | ---: | ---: | ---: | --- | ---: | ---: | --- | ---: | ---: | ---: |
| Narrow cold | 142.812 | 140.059 | 19.735 | 20/20/20 | 0/152 | 610 | 1220/607 | 19,348 | 10.562 | 1179.332 |
| Narrow warm | 137.312 | 134.372 | 16.219 | 5/5/5 | 152/152 | 610 | 1220/607 | 0 | 0.000 | 1177.395 |
| Two-repository cold | 169.578 | 165.886 | 29.554 | 36/36/36 | 0/307 | 849 | 1698/843 | 29,065 | 18.814 | 1533.082 |
| Two-repository warm | 149.157 | 144.464 | 19.981 | 8/8/8 | 307/307 | 849 | 1698/843 | 0 | 0.000 | 1619.785 |

Narrow retains 152 historical PR heads plus one default head, all 153 distinct.
Broad retains the same 153 better-web-browser heads and 150 EffortHours heads,
303 distinct heads overall. Exact author selection includes 610 changes in
narrow (550 admitted, 60 scope-empty) and 849 in broad (758 admitted, 91
scope-empty). No row truncates the head, commit or daily-bucket selection.

| Annual scope/cache | Candidate ledger charge bytes | Rendered output bytes | File artifact requests/hits | Unique blob objects | Blob read bytes |
| --- | ---: | ---: | --- | ---: | ---: |
| Narrow cold | 873,992 | 4,117,360 | 863,081/850,965 | 10,070 | 85,796,961 |
| Narrow warm | 873,992 | 4,117,315 | 863,685/851,564 | 10,070 | 85,796,961 |
| Two-repository cold | 1,330,728 | 6,525,903 | 1,201,744/1,182,532 | 13,639 | 117,325,712 |
| Two-repository warm | 1,330,728 | 6,525,861 | 1,201,464/1,182,247 | 13,639 | 117,327,306 |

Warm rows reuse all PR metadata and acquire zero objects/bytes. They refresh live
inventory and recalculate static analysis. Both pinned-selection and complete
semantic digests match exactly within each scope:

- Narrow selection: `e2150bf12a6c9828326aff7b736f1e9451619815991e0e008fb59a8e4653c50a`; semantic: `sha256:b96e74f44b94534b2d3e1291bc95367a709e65fb5d75764e919680623a501daf`.
- Two repositories selection: `81ed0197d66ff57d5ed159f6c579372c8bdaa173f411481e2ac192c5606cb824`; semantic: `sha256:a9fa386aa4cb66255d7d7e7bbfa567e57f6d802c7561c9a23e7e372cacc2735a`.

The observations establish successful bounded annual discovery, acquisition,
selection, reconciliation and daily reporting on these public inputs. They do
not measure replay-event calibration, lost workdays, complete owner coverage,
alpha.29 equivalence, or the NDA repository's field latency. Peak working set is
sampled and is not a memory quota; phase timings remain overlapping cumulative
work. Within-run request/hit counts can vary with scheduling while exact
selection and semantic results remain equal. The private consumer still needs
its own same-input retest.

## Alpha.37 replay/provider follow-up revalidation

Recorded October 6, 2026 using implementation commit
`adfcf7086d0bba991c0e0bebda6d6a998784612f` and frozen CLI digest
`9d4c01ac78bec8190ef12ffbb76075a60a17d3954dc3b0b2c9e2d9983e88e5bf`. Platform, public contributor, engineering scope,
implementation profile and UTC author-date policy are the same as above.
This fresh four-row checkpoint covers October 1 inclusive to October 6 exclusive,
2026, with five complete daily buckets. Narrow selects better-web-browser;
broad explicitly adds EffortHours. Each uses its own initially empty managed
provider/object caches, followed by a fresh-process warm repeat over those caches.
No evidence checkpoints are used. No EH build, test or other EH benchmark ran
during these timed rows; ordinary machine load and network latency are uncontrolled.

The EH discovery/acquisition deadline is 90 seconds, matching the reported
five-day incident configuration; observed object-store growth is bounded at
1,024 MiB and the driver deadline is 1,800 seconds per row. Use the recent
driver command above with `--discovery-seconds 90 --acquired-mib 1024
--run-seconds 1800` and a new private output directory.

All four rows exit zero with complete selection, no pending metadata, no failures
and no driver timeout. Narrow reviews all 153 candidate PR representations and
retains 12 historical PR heads plus one default head. Broad reviews all 310
candidate PR representations and retains 38 historical PR heads plus two default
heads. The respective selected populations are 106 and 152 changes, with no head,
change or daily-bucket truncation. This is explicit two-repository coverage, not
complete account coverage. Live candidate inventories differ from older checkpoints.

| Scope/cache | Outer wall s | CLI wall s | Discovery/acquisition s | Queries/pages/process receipts | PR metadata hits | Selected changes | Snapshot requests/hits | Acquired objects | Observed acquired bytes | Sampled peak MiB |
| --- | ---: | ---: | ---: | --- | ---: | ---: | --- | ---: | ---: | ---: |
| Narrow cold | 48.922 | 48.488 | 11.656 | 20/20/20 | 0/153 | 106 | 214/105 | 19,704 | 11,497,472 | 874.934 |
| Narrow warm | 41.188 | 40.748 | 4.537 | 5/5/5 | 153/153 | 106 | 214/105 | 0 | 0 | 855.746 |
| Two-repository cold | 65.094 | 64.325 | 13.945 | 37/37/37 | 0/310 | 152 | 308/149 | 29,503 | 20,247,552 | 1040.520 |
| Two-repository warm | 57.812 | 57.169 | 6.743 | 8/8/8 | 310/310 | 152 | 308/149 | 0 | 0 | 1018.930 |

Warm rows reuse every PR metadata record and acquire zero objects/bytes while
refreshing live inventory and recalculating static analysis. Both exact pinned
selection and complete semantic digests match within each scope:

- Narrow selection: `5e4ffc04570721098bd7b9bfaff6a8297d4cf9ff61d91d1ea47c12a2c2108bbf`; semantic: `sha256:3bde5ad153e94304378a534260fa9002e3061b8c3385dfdc4307b0156ed8a461`.
- Two repositories selection: `233f6edab31bf23ecbf1436ae7142b1dc69ad8941facb4ce22c15552678a5f79`; semantic: `sha256:67fad68bc9eab77dce56f6606ca23d6aea66a6d9aaae5f50d01b30f74e7a574c`.

Completed adapter observations record the actual REST/GraphQL API, success
outcome and exit zero. Synthetic failure tests separately verify HTTP 5xx,
GraphQL errors, transport timeout/failure, malformed JSON, accepted empty
repository/fallback receipts and root-failure preservation. The full physical
CLI suite verifies deadline expiry, caller/provider-child cancellation and
acquisition-byte exhaustion without aggregate EHE or manufactured daily zeros.

These single observations establish successful current public native discovery,
acquisition, selection and reporting under the 90-second bound. They do not
diagnose the original 58.83-second incident: the available sibling consumer
artifacts contain no original exit/HTTP receipt. They also do not establish NDA
field latency, alpha.29 equivalence, causal replay labor or numerical calibration.
Full reports, identity metadata, immutable caches and stderr remain ignored/private.

## Alpha.39 timeout/resume fix public revalidation

Recorded October 7, 2026 with the correction in this PR, frozen CLI digest
`27b4cc0c3e8f005235f4d72553c86a5f89375e2011d3c3b9821eff69d54604e7`.
Platform, contributor, author-date policy, engineering/implementation scope and
fixed eight-hour denominator match the earlier checkpoints. Narrow covers
October 1-6, 2026 (five days, better-web-browser); broad separately covers
October 6, 2025 to October 6, 2026 (365 days, better-web-browser plus EffortHours).
Each has its own cold object/provider caches followed by a fresh-process warm
repeat. Evidence checkpoints are disabled to measure full recalculation. No EH
build, test or other benchmark ran during these four rows; ordinary background
load and real network latency remain uncontrolled. These are single observations.

The discovery/acquisition deadline is **90 seconds** and observed object-store
growth budget **256 MiB**, matching the reported incident limits. The driver has
its separate 1,800-second bound. Run the existing opt-in driver with a fresh
private output directory:

```text
python eng/benchmark-historical-network.py --allow-network --cli-dll <frozen-cli>/efforthours.dll --output artifacts/historical-network-resume-new --owner VictorZakharov --author VictorZakharov --repository VictorZakharov/better-web-browser --repository VictorZakharov/efforthours --narrow-since 2026-10-01 --broad-since 2025-10-06 --until 2026-10-06 --discovery-seconds 90 --acquired-mib 256 --run-seconds 1800
```

| Scope/cache | Outer wall s | CLI wall s | Discovery/acquisition s | Queries/pages/receipts | Metadata hits | Selected changes | Acquired objects | Observed acquired bytes |
| --- | ---: | ---: | ---: | --- | ---: | ---: | ---: | ---: |
| Five-day single cold | 54.656 | 54.109 | 16.830 | 21/21/21 | 0/154 | 106 | 19,969 | 11,841,536 |
| Five-day single warm | 44.484 | 43.934 | 8.439 | 6/6/6 | 154/154 | 106 | 0 | 0 |
| Annual two repositories cold | 167.375 | 163.567 | 26.974 | 38/38/38 | 0/316 | 849 | 29,970 | 20,703,232 |
| Annual two repositories warm | 171.797 | 167.502 | 21.866 | 9/9/9 | 316/316 | 849 | 0 | 0 |

All rows exit zero with complete inventory/metadata/selection, zero pending PRs,
no fallback, no failure and no driver timeout. The adaptive plan chooses scoped
connections here because this contributor's complete account is larger. Census
adds one request; narrow inventory has three requests including census, broad
five. Cold metadata has 13 batches for 154 candidates or 27 for 316; warm has none.
The narrow manifest retains 12 PR heads plus one default head; broad retains 301
PR heads plus two defaults. Live candidate populations have advanced since earlier
measurements, so old wall times/digests are not a same-input performance baseline.

Exact cold/warm pinned-selection and complete semantic digests match:

- Five-day selection: `e7c444ad79168d37f58e2cbf11304836ed00941eed3116f71cd1ebc4a487c85f`; semantic: `sha256:4a1564c2f189e37e33f65aa941a6b51bcfde5ef3ae6c3e205af0fe29d8a97223`.
- Annual selection: `7601b9a664601bfd9b1e96de509309bf9a416d295110f80cc68603c602c779cc`; semantic: `sha256:bbdd864d1bceb84d286a86f001ceb284722fb537f0e07dd93c9b8b2436d70622`.

The five-day full report finishes below 90 seconds on this public input. Annual
**discovery/acquisition** also finishes below 90, but annual analysis/reporting is
separately measured at about 164-168 seconds; the discovery deadline never governs
that work. Warm annual wall time was slightly longer despite complete metadata and
object reuse, demonstrating why request elimination is not a general end-to-end
speed guarantee. Cumulative overlapping phase durations are not wall latency.
The new inventory/metadata/selection phases and completed-row progress are visible;
headers are absent here because the live scoped connection already supplies them.
Sampled peaks were 880.707/883.836 MiB narrow and 1614.699/1568.820 MiB annual;
these are observations, not quotas or CI gates.

The separate 440/204/236 [resume checkpoint](HISTORICAL_PR_DISCOVERY_BENCHMARK.md#alpha39-timeout-and-resume-correction-checkpoint)
verifies physical interruption/reopening and exact miss-only acquisition. Native
retained/squashed Git fixtures retain pinned/offline numerical parity. These public
runs do not reproduce the NDA incident or certify original workdays, labor or
calibration. Full reports, source caches, learned identities and stderr stay private.

These recorded runs precede the final request-cap/cancellation counter correction.
Requests rejected by the fixed cap or canceled before admission no longer charge
unsent work. Exhaustion and cancellation paths are tested separately; interrupted
attempt counts may decrease. Successful request plans, source selection, the
204/236 resume conservation and estimation rules remain identical.

## Warm inventory freshness assessment

The existing 24-hour cache applies only to immutable PR metadata. Current inventory
membership has **zero intentional staleness**: every run refreshes complete visible
repository/default/PR inventories. New/removed PRs, moved heads, advanced upstream
bases and changed counts cannot reuse cached membership. The alpha.40 checkpoint's
warm request counts are six for one restricted repository and nine for two; those
include live coverage and adaptive census, not missed metadata reuse. Warm runs
already target zero metadata batches and zero newly acquired objects for identical
live immutable inputs; CI gates those deterministic counts, not wall time.

Dropping the census can reduce a warm request but may force a much larger scoped
inventory for an account with few authored PRs. Caching whole membership or trusting
an unchanged count would weaken coverage (a replacement can preserve the count).
No such optimization is admitted here. A future optimization must first freeze
zero-stale membership parity, changed-head/base/count invalidation, the same bounded
requests/readers, and a measured complete-inventory request target on both account-
and repository-dominated populations. It must demonstrate the target without date
pruning or partial inventories before claiming a latency improvement.

The additive `historical-network-checkpoint/1.1.0` driver receipt now records
`attributionCompleteness`, exact repository restriction and native daily evidence
state counts alongside existing request/page/process receipts, population, selected
changes, bytes, peaks, wall timings and digests. Complete execution remains
separate from unresolved original workdays/unknown intermediate history. Earlier
measurement records retain their original protocols and values.
