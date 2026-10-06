# Historical PR discovery request checkpoint

This is a deterministic provider-fixture request plan plus an explicit latency
simulation, not a measurement on an NDA repository or a GitHub service guarantee.
The fixture contains 258 merged authored PRs in one admitted repository and one
PR in an excluded repository, fully paginated in three account pages. One PR
contains January 19 author-date work with a March 13 committer date; all other
PRs have out-of-window author dates. Each contains one immutable commit. The
exact five-day selection keeps one head in both cold and warm runs.

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
