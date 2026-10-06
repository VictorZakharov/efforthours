# Public historical network checkpoint

## Scope and reproducibility

Recorded October 6, 2026 on Windows 10.0.26200, .NET 10.0.7, 24 logical
processors. These are real authenticated GitHub CLI calls and managed Git
acquisitions on the public `VictorZakharov/better-web-browser` repository and
`VictorZakharov/efforthours` control. The selected contributor is the public
`VictorZakharov` login. No NDA repository or private source was used.

The complete measurement interval is October 1 inclusive to October 6 exclusive,
2026, UTC, author-date selection, engineering scope, fixed eight-hour daily
reference, implementation profile, no pricing and no evidence checkpoint reuse.
The narrow scope includes only better-web-browser; the broad recent scope
explicitly includes these two repositories. This does not establish complete
account/organization coverage. The complete accessible owner inventory is still
validated before applying restrictions; unrelated account PR history is omitted.

Each scope starts with separate empty repository/provider caches, then repeats
with those exact caches in a new CLI process. A copied, frozen CLI directory
prevents builds from changing binaries during the experiment. No other build,
test or benchmark ran during the final four rows. Ordinary operating-system
background load and network latency remain uncontrolled. These are single
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
for the final checkpoint is
`37fcbb4943243ff4664be08c3796916b5110309fce5bc53c7adeecd3e7d0b751`.

## Complete cold and warm results

The explicit discovery/acquisition deadline is 900 seconds, observed growth budget
1,024 MiB, and driver deadline 1,800 seconds per row. Existing bounds remain:
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

## Annual scope and failure boundary

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
