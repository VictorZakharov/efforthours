# Historical PR discovery request checkpoint

This is a deterministic provider-fixture request plan plus an explicit latency
simulation, not a measurement on an NDA repository or a GitHub service guarantee.
The narrow fixture contains 258 merged authored PRs in one admitted repository
and one PR in an excluded repository, fully paginated in three account pages.
One PR contains January 19, 2026 author-date work with a March 13, 2026 committer
date; all other PRs have January 1, 2025 author dates. Each contains one immutable
commit. The exact five-day selection keeps one head in both cold and warm runs.
The broader variant distributes those same 258 PRs across 16 admitted repositories
and selects the full 2025 UTC calendar year: 257 heads, at most 17 per repository.
Both scopes use separate initially empty caches, followed by an exact warm repeat.

The former account inventory plus per-PR detail and commit plan would invoke
517 adapters for those 258 admitted PRs. The batch reader invokes 23: one live
inventory and 22 batches of at most 12. An exact warm repeat invokes only the
live inventory and reuses all 258 complete metadata entries. Changing the
upstream base forces a fresh 23-call plan even if heads and counts remain equal.
Incomplete batches use complete REST; changed metadata fails, and restrictions
exclude the foreign PR before detail/cache/acquisition reads. CI asserts these
counts, selected objects, cache invalidation and the four-call ceiling, not time.

Run the explicit checkpoint after the Release build:

```text
dotnet benchmarks/EffortHours.ChangeBenchmarks/bin/Release/net10.0/EffortHours.ChangeBenchmarks.dll --historical-pr-discovery 50
```

The optional number is simulated per-adapter latency in milliseconds (0-1000).
There is no actual network or provider subprocess. Adapter process counts model
successful runner receipts, not operating-system launches. Memory cache reuse and
JSON parsing use the same product discovery reader; this checkpoint omits owner
inventory, identity bootstrap, Git acquisition, estimation and output rendering.

Recorded October 6, 2026, .NET 10.0.7, Windows 10.0.26200, 24 logical processors,
50-ms simulated adapter latency, one cold run followed by one warm run:

| Run | Queries | Pages | Adapter process receipts | Metadata hits | Selected heads | Wall ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Cold | 23 | 25 | 23 | 0 | 1 | 549.011 |
| Warm | 1 | 3 | 1 | 258 | 1 | 60.538 |

These timings are a single simulation observation, not a distribution or field
latency estimate. Hardware and scheduling can affect local parsing/overlap;
real network, account size, large PR fallbacks and cold Git acquisition can
materially increase runtime. No universal annual-report or 30-second claim is
made. The native Git fixture separately verifies retained squash/chain aggregate
and engineering-scope parity. The private reported case needs a consumer retest.

## Repeated narrow and annual checkpoint

The command now emits four rows: narrow five-day cold/warm, then broad annual
cold/warm. Each row has a fresh runner so peak concurrency is specific to that
row. Each process starts with empty in-memory metadata caches; a warm row reuses
only its preceding cold row's scope/cache. No persisted Git cache, immutable Git
object acquisition, provider child processes, repository estimation or output
rendering is exercised. `acquisitionExecuted: false` and zero object/byte fields
mean an omitted phase, not measured zero-cost acquisition. Selected heads and
matching PR representations are reported; analyzed Change rows are not measured.

The synthetic bounds are a 12-PR batch, four concurrent adapters, a 16-Mi-character
response envelope, 2,048 adapter requests, 512 heads per repository, 64-KiB complete
metadata entries and 1,000 retained metadata entries. The annual population stays
within those bounds. The record includes exact interval, included repository
count, runtime, OS, processor count, request plan and cache hits. It does not
claim complete real organization coverage from a fixture or a restricted scope.

Recorded October 6, 2026, .NET 10.0.7, Windows 10.0.26200, 24 logical processors,
50-ms simulated per-adapter latency, three sequential fresh checkpoint processes.
The local build/test validation was also active; these observations do not control
background load, network behavior or hosted scheduling.

| Scope and cache | Queries / process receipts | Pages | Metadata hits | Selected heads / PRs | Peak adapters | Median wall ms | Min-max wall ms |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Five-day, one repository, cold | 23 | 25 | 0 | 1 | 4 | 544.920 | 544.507-545.106 |
| Five-day, one repository, warm | 1 | 3 | 258 | 1 | 1 | 57.982 | 54.934-60.841 |
| Annual, 16 repositories, cold | 33 | 35 | 0 | 257 | 4 | 559.821 | 557.578-561.215 |
| Annual, 16 repositories, warm | 1 | 3 | 258 | 257 | 1 | 65.282 | 64.176-65.619 |

Per-repository batching makes 32 broad batches rather than 22 narrow batches.
These are scope-specific request observations, not comparative throughput claims;
the fixed row order also gives the narrow cold row different JIT state. CI gates
request/page/cache/selection counts and the concurrency ceiling, not these times.
A one-repository restriction of the annual fixture discovers/selects only its 17
PRs and reuses only those entries; it does not advertise 16-repository coverage.

Physical failure-path tests separately assert active managed-acquisition phase,
partial request/page/process counters, termination and cancellation, unchanged
working trees, reusable acquired immutable objects and incomplete outputs with no
portfolio or aggregate EHE. A request already restricted to one repository gets
`inspect-acquisition-or-use-pinned-manifest`, rather than advice to narrow to that
same repository. This checkpoint does not extend discovery evidence into an
end-to-end annual runtime or a new estimation-performance claim.

The opt-in [public network checkpoint](HISTORICAL_NETWORK_BENCHMARK.md) now
records actual narrow/two-repository cold/warm discovery, acquisition, analysis,
digest parity and an explicit annual head-limit failure. The synthetic request
plan above remains a separate deterministic checkpoint, not a field result.

## Large unrelated historical inventory checkpoint

The opt-in `--large-historical-inventory` checkpoint uses 16,384 PRs: 258
authored candidates and 16,126 unrelated authors. The five-day single-repository
scope is January 19 inclusive to January 24 exclusive, 2026 UTC; the annual
single-repository and 16-repository scopes cover 2025 UTC. PR creation/update
dates are outside the narrow interval. The author-date commit selection retains
one narrow head or 257 annual heads after complete minimal inventory traversal.

```text
dotnet benchmarks/EffortHours.ChangeBenchmarks/bin/Release/net10.0/EffortHours.ChangeBenchmarks.dll --large-historical-inventory
```

Each fresh process has separate initially empty in-memory metadata caches for
each scope, followed by its exact warm repeat. The product reader admits pages
of at most 100 rows and 1,048,576 response characters, under a deterministic
16-MiB per-repository inventory ledger, 1,000 authored candidates per repository,
2,048 adapter requests and four process-wide historical readers. Unrelated PRs
consume only exact number-ledger charges after page parsing. The existing head
bound is 512 per repository/overall; the older synthetic metadata incorrectly
printed 32 and is corrected without increasing any product limit.

Recorded October 6, 2026, .NET 10.0.7, Windows 10.0.26200, 24 logical processors,
zero simulated adapter latency, three sequential fresh checkpoint processes.
Local unit/process validation was active. Background load, GC and JIT are not
controlled; medians below are observations, never CI performance gates.

| Scope/cache | Queries/pages/process receipts | Metadata hits | Selected heads | Peak adapters | Median wall ms | Min-max wall ms | Median sampled peak MiB |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Five-day, one repository, cold | 186/186/186 | 0 | 1 | 1 | 1102.694 | 817.696-1159.443 | 51.500 |
| Five-day, one repository, warm | 164/164/164 | 258 | 1 | 1 | 529.004 | 300.664-624.106 | 61.816 |
| Annual, one repository, cold | 186/186/186 | 0 | 257 | 1 | 421.272 | 322.973-608.563 | 64.922 |
| Annual, one repository, warm | 164/164/164 | 258 | 257 | 1 | 348.175 | 332.230-558.503 | 71.465 |
| Annual, 16 repositories, cold | 208/208/208 | 0 | 257 | 1 | 389.935 | 292.750-462.587 | 67.848 |
| Annual, 16 repositories, warm | 176/176/176 | 258 | 257 | 1 | 352.191 | 347.040-388.237 | 70.973 |

The narrow/annual single repository needs 164 inventory pages and 22 cold
metadata batches. Splitting the same population across 16 repositories needs
176 pages and 32 cold batches. Warm runs reuse all 258 complete metadata entries
but still refresh every live inventory page. A separate REST regression fixture
checks explicit minimal `--jq` pages, the required terminal empty page after an
exact multiple of 100, frozen head/base/count header refresh and default-head
overlap. A public REST projection smoke check returned only the four requested
fields for 100 PRs in 12,367 UTF-8 bytes; it is not a complete discovery benchmark.

Acquisition is explicitly omitted: `acquisitionExecuted: false`, zero object/byte
fields are placeholders, not measured zero-cost network/Git work. No real provider
child startup, network, repository analysis or output rendering occurs. Sampled
working set includes the synthetic fixture/runtime and is not a memory quota.
The earlier public network checkpoint remains separate; these measurements do
not establish NDA field latency, organization completeness or annual EHE accuracy.
CI gates exact selection, reuse/request counts, malformed/bound failures and
concurrency, never wall time or sampled memory. The private case needs a retest.
