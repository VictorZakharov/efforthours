# PR validation wall-clock investigation

Issue [#242](https://github.com/VictorZakharov/efforthours/issues/242), observed
2026-10-03. This is an explicit diagnostic checkpoint, not a CI timing gate or an
implemented speedup. No required platform, test assertion, current-head validation,
or release provenance check changes.

## Finding

The E2E test collection is the main opportunity. Ten recent successful full PR
runs took 334-517 seconds from run creation to aggregate package completion
(median 439 seconds). Windows E2E finished last in eight runs and macOS in two.
The original #241 Windows observation repeats, but Windows is not always last.

A local unchanged alpha.30 Windows run passes all 286 tests. Its TRX start-to-finish
is 283.36 seconds; 77 `ChangeCliTests` cases sum to 280.73 seconds. That one serial
collection accounts for almost the entire local tail. It spans 29 partial-class
files for Change, portfolios, calendars, and snapshots. Separate files do not
create separate xUnit collections. By default each class is one collection and
its tests run serially; different collections can overlap. See the
[xUnit parallelism contract](https://xunit.net/docs/running-tests-in-parallel).

A fresh class-only repeat passes all 77 cases with 279.00 summed test-body
seconds and 279.99 TRX wall seconds. Its similar tail corroborates the
collection bottleneck despite reduced contention; it is not a scheduling speedup.

The remaining collections already overlap: summed individual durations total
635.83 seconds, exceeding suite wall time. Increasing the global thread limit
alone cannot parallelize methods in the bottleneck class.

## Public CI evidence

The sample covers release PRs #238, #241, #244 and feature/correction PRs #236,
#237, #239, #240, #243, including two selected updates each for #240 and #243.
These are representative successful runs, not a random sample or controlled
cross-platform experiment. Cancelled/failed attempts are excluded. Different
revisions, runner load, suite sizes, and SDK/image state prevent attributing
between-run differences to a code change.

All values below are seconds. Gate includes prerequisite job scheduling and
aggregate promotion. Platform columns are the test step, excluding restore/build.

| Run | Gate | Linux tests | Windows tests | macOS tests | Final E2E platform |
| --- | ---: | ---: | ---: | ---: | --- |
| [37037008980](https://github.com/VictorZakharov/efforthours/actions/runs/37037008980) | 442 | 164 | 334 | 240 | windows |
| [37042428595](https://github.com/VictorZakharov/efforthours/actions/runs/37042428595) | 412 | 168 | 270 | 287 | macos |
| [37053877905](https://github.com/VictorZakharov/efforthours/actions/runs/37053877905) | 390 | 166 | 292 | 173 | windows |
| [37077694007](https://github.com/VictorZakharov/efforthours/actions/runs/37077694007) | 436 | 183 | 332 | 266 | windows |
| [37080666742](https://github.com/VictorZakharov/efforthours/actions/runs/37080666742) | 512 | 149 | 387 | 231 | windows |
| [37083387417](https://github.com/VictorZakharov/efforthours/actions/runs/37083387417) | 480 | 189 | 364 | 213 | windows |
| [37118742097](https://github.com/VictorZakharov/efforthours/actions/runs/37118742097) | 334 | 198 | 250 | 188 | windows |
| [37119893698](https://github.com/VictorZakharov/efforthours/actions/runs/37119893698) | 508 | 203 | 394 | 203 | windows |
| [37122898201](https://github.com/VictorZakharov/efforthours/actions/runs/37122898201) | 517 | 191 | 404 | 281 | windows |
| [37123657295](https://github.com/VictorZakharov/efforthours/actions/runs/37123657295) | 362 | 203 | 250 | 268 | macos |

Across these runs, test-step median/range is Linux 186 / 149-203, Windows
333 / 250-404, and macOS 235.5 / 173-287 seconds. Median E2E restore is
7 / 12 / 6.5 seconds and build is 63 / 60 / 47 seconds, respectively. The test
step contributes 73.6-81.0% of the critical E2E job in this sample.

E2E jobs start a median 10 / 10 / 15.5 seconds after run creation. This combines
the history prerequisite and scheduling: the API does not expose a separate
job-created timestamp, so it must not be labeled pure runner queue time. Setup,
checkout, SDK setup, gaps, restore/build, tests and job cleanup remain separately
recorded in [the checkpoint](../benchmarks/ci-validation/2026-10-03/runs.md).
API timestamps have one-second resolution; step durations need not sum exactly
to job duration.

Package candidates finish 266-445 seconds before aggregate completion. That is
required-gate waiting, not redundant packaging. Removing the wait would weaken
the artifact contract. The formatter and build/unit lanes finish earlier than
the E2E critical path in every sampled full run.

Four unchanged post-merge runs (#241, #239, #243, #244) finish in 27, 28, 29 and
21 seconds respectively. Existing tree/provenance reuse already avoids repeating
expensive validation; no new post-merge bypass is needed. Each PR update still
requires validation of its own current head. For the sampled #240 updates, full
CI sums to 948 seconds; for #243 it sums to 1,025 seconds. These sums measure
completed run cost, not continuous user waiting or necessarily avoidable work.

## Local diagnosis

Measurements use the alpha.30 tree at
`3896f800c4766c08196f184d493383c666d4fd21`, .NET 10 Release outputs, Windows and
24 logical processors. Local test execution excludes restore/build, which were
already verified during release preparation. No private project or target source
is used. TRX files and raw logs remain ignored; this checkpoint retains only public
names, durations and aggregate outcomes. See
[local measurements](../benchmarks/ci-validation/2026-10-03/local.md).

| Collection | Cases | Sum of test seconds |
| --- | ---: | ---: |
| `ChangeCliTests` | 77 | 280.73 |
| `ChangeBenchmarkCliTests` | 9 | 63.64 |
| `PullRequestSelectionGitTests` | 15 | 51.10 |
| `CliTests` | 24 | 25.12 |
| `LogicalCandidateProjectionCliTests` | 6 | 16.23 |

Slow individual tests are distributed. The longest local case is
`ManualQaReviewFreezeIsDeterministicAndRefusesChangedArtifacts` (14.35 seconds),
outside `ChangeCliTests`. Within Change, the daily benchmark ancestry/reproduction
case takes 11.92 seconds and calendar window/date-field invariance 11.32 seconds.
There is no one several-minute test to delete or move out of required coverage.

A separate warmed 20-process probe measures median complete `eh version` launch
at 0.080 seconds and `git --version` at 0.026 seconds. These include executable
startup and command work; they are not an isolated OS spawn measurement. They were
run while the full suite was active and cannot explain hosted Windows/Linux
ratios. Startup/serialization may accumulate across repeated CLI/Git operations,
but no per-command count or hosted process profile establishes its contribution.

## Prioritized options and acceptance

1. **Measure a responsibility split of `ChangeCliTests`.** Extract reusable
   process/fixture helpers and give Change selectors, portfolios, calendars and
   snapshots distinct concrete classes. Keep test identities/coverage mapped and
   each physical fixture isolated. Check shared static state, mutation of build
   inputs, caches, checkpoint paths, cancellation, and external resources before
   enabling overlap. Bound test concurrency rather than multiplying the product's
   fixed managed/Git worker limits without a memory budget. Repeat full tests and
   class timings on all three platforms before/after at fixed commits. Reject a
   split that introduces flakes, missing cases or excessive memory/process use.
   The local serial sum establishes a target; it does not establish a speedup.
2. **Persist diagnostic TRX results from each E2E lane.** Use distinct OS/run/head
   identities, upload even on failure, and retain no private paths/source/stdout.
   This fills the present hosted per-test visibility gap and tests whether the
   local bottleneck is also the hosted tail. Measure logging/upload overhead.
   Timing stays non-gating; deterministic assertions remain required.
3. **Evaluate same-OS build reuse only after the test tail is addressed.** Quality
   and E2E independently restore/build overlapping graphs. Combining their work
   could save worker-minutes, but may delay test start or reduce parallelism.
   Preserve independently named required checks and full OS-specific graphs;
   never transfer compiled binaries across operating systems. Compare complete
   gate time and worker cost, not just a deleted build step. The measured median
   restore costs make NuGet caching a smaller initial target.
4. **Reduce repeated local work during a single unchanged revision.** Reuse a
   completed build and scale local tests to changed behavior, then perform the
   release sequence when required. Do not substitute old-head results for new
   commits, avoid required platforms, or rerun a release dry run after its proven
   aggregate gate. Local validation can overlap CI where resources permit;
   elapsed local and CI times cannot simply be added as user waiting.

No implementation is admitted by this report, and no before/after claim exists.
A later scheduling change must include paired exact-commit measurements,
unchanged deterministic semantics/test case coverage, cross-platform passes and
explicit resource/flake observations. Ordinary CI must never gate on sampled
wall-clock or memory thresholds.

## Reproduction

Read public run metadata and step timestamps:

```text
gh api repos/VictorZakharov/efforthours/actions/runs/<run-id>
gh api repos/VictorZakharov/efforthours/actions/runs/<run-id>/jobs?per_page=100
```

Subtract `created_at` from the aggregate job's `completed_at` for gate wall time;
subtract each successful step's `started_at` from `completed_at` for phase time.
Keep step/job/conclusion names and commit identities; exclude skipped E2E steps
from the full-run sample. For local per-test measurements, after locked restore
and a Release build:

```text
dotnet test tests/EffortHours.EndToEndTests/EffortHours.EndToEndTests.csproj --no-build --no-restore --configuration Release --logger "trx;LogFileName=baseline.trx" --results-directory artifacts/ci-validation/baseline
dotnet test tests/EffortHours.EndToEndTests/EffortHours.EndToEndTests.csproj --no-build --no-restore --configuration Release --filter "FullyQualifiedName~EffortHours.EndToEndTests.ChangeCliTests" --logger "trx;LogFileName=repeat.trx" --results-directory artifacts/ci-validation/repeat
```

Join `UnitTestResult.testId` to `TestDefinitions/UnitTest.id` and group by
`TestMethod.className`, not source file or display-name text (theory arguments
can contain dots). Retain every case count and outcome; use TRX start/finish for wall time.
Do not compare a class-only run directly with a full-suite run as a speedup:
contention and collection scheduling differ.
