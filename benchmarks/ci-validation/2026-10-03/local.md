# Local per-test checkpoint - 2026-10-03

Source tree: `3896f800c4766c08196f184d493383c666d4fd21`. Windows, 24 logical processors; prebuilt .NET 10 Release outputs. All 286 cases passed. TRX start-to-finish: 283.359 seconds. These are test-body sums, so parallel collections can exceed wall time.

| Class | Cases | Test-body seconds |
| --- | ---: | ---: |
| `EffortHours.EndToEndTests.ChangeCliTests` | 77 | 280.730 |
| `EffortHours.EndToEndTests.ChangeBenchmarkCliTests` | 9 | 63.636 |
| `EffortHours.EndToEndTests.PullRequestSelectionGitTests` | 15 | 51.105 |
| `EffortHours.EndToEndTests.CliTests` | 24 | 25.124 |
| `EffortHours.EndToEndTests.LogicalCandidateProjectionCliTests` | 6 | 16.235 |
| `EffortHours.EndToEndTests.CandidatePreflightTests` | 26 | 15.927 |
| `EffortHours.EndToEndTests.ReleaseMergeProvenanceTests` | 4 | 13.722 |
| `EffortHours.EndToEndTests.CalibrationDiagnosticCliTests` | 3 | 13.574 |
| `EffortHours.EndToEndTests.CanonicalJsonOutputTests` | 2 | 13.023 |
| `EffortHours.EndToEndTests.CalibrationUncertaintyStructureCliTests` | 2 | 11.801 |
| `EffortHours.EndToEndTests.CalibrationUncertaintyGraphCliTests` | 2 | 11.492 |
| `EffortHours.EndToEndTests.BenchmarkCliTests` | 17 | 11.250 |
| `EffortHours.EndToEndTests.HostReviewCliTests` | 2 | 11.126 |
| `EffortHours.EndToEndTests.HostReviewMeasurementCliTests` | 2 | 7.697 |
| `EffortHours.EndToEndTests.NonGitChangeCliTests` | 3 | 6.990 |
| `EffortHours.EndToEndTests.GitBatchObjectReaderConcurrencyTests` | 3 | 6.757 |
| `EffortHours.EndToEndTests.ChangeCalibrationCliTests` | 2 | 6.749 |
| `EffortHours.EndToEndTests.ChangeCalibrationFixtureGeneratorTests` | 2 | 5.409 |
| `EffortHours.EndToEndTests.CalibrationReproductionArtifactTests` | 3 | 5.311 |
| `EffortHours.EndToEndTests.GoCliTests` | 2 | 5.248 |
| `EffortHours.EndToEndTests.CppCliTests` | 2 | 4.405 |
| `EffortHours.EndToEndTests.RustCliTests` | 2 | 3.810 |
| `EffortHours.EndToEndTests.GDScriptCliTests` | 1 | 3.765 |
| `EffortHours.EndToEndTests.ScriptingCliTests` | 2 | 3.745 |
| `EffortHours.EndToEndTests.PhpCliTests` | 2 | 3.510 |
| `EffortHours.EndToEndTests.KotlinCliTests` | 2 | 3.051 |
| `EffortHours.EndToEndTests.JupyterNotebookCliTests` | 2 | 3.021 |
| `EffortHours.EndToEndTests.DockerCliTests` | 2 | 2.891 |
| `EffortHours.EndToEndTests.JavaCliTests` | 2 | 2.770 |
| `EffortHours.EndToEndTests.TerraformCliTests` | 2 | 2.754 |
| `EffortHours.EndToEndTests.ValidationBoundaryVerifierTests` | 4 | 2.390 |
| `EffortHours.EndToEndTests.CoverageCliTests` | 1 | 2.375 |
| `EffortHours.EndToEndTests.PythonCliTests` | 1 | 1.905 |
| `EffortHours.EndToEndTests.CalibrationUncertaintySupportArtifactTests` | 1 | 1.822 |
| `EffortHours.EndToEndTests.AgentCliTests` | 1 | 1.731 |
| `EffortHours.EndToEndTests.SqlCliTests` | 1 | 1.649 |
| `EffortHours.EndToEndTests.ChangeCandidateDiagnosticArtifactTests` | 1 | 1.486 |
| `EffortHours.EndToEndTests.ChangeCalibrationArtifactTests` | 3 | 1.191 |
| `EffortHours.EndToEndTests.CandidateMeasurementArtifactTests` | 5 | 0.600 |
| `EffortHours.EndToEndTests.ValidationSelectionArtifactTests` | 1 | 0.597 |
| `EffortHours.EndToEndTests.PublicReleaseHygieneTests` | 8 | 0.557 |
| `EffortHours.EndToEndTests.CalibrationFixtureDependencyDispositionTests` | 3 | 0.548 |
| `EffortHours.EndToEndTests.FileBudgetTests` | 1 | 0.525 |
| `EffortHours.EndToEndTests.GitHubProviderMetadataCacheTests` | 2 | 0.521 |
| `EffortHours.EndToEndTests.ValidationReviewArtifactTests` | 3 | 0.298 |
| `EffortHours.EndToEndTests.ChangeLogicalDecompositionArtifactTests` | 1 | 0.250 |
| `EffortHours.EndToEndTests.SnapshotArchiveVerifierTests` | 3 | 0.172 |
| `EffortHours.EndToEndTests.LogicalCandidateV3MeasuredArtifactTests` | 1 | 0.154 |
| `EffortHours.EndToEndTests.CandidatePreflightArtifactTests` | 1 | 0.085 |
| `EffortHours.EndToEndTests.CandidateResourceMeasurementTests` | 4 | 0.084 |
| `EffortHours.EndToEndTests.CalibrationSamplingPlanArtifactTests` | 1 | 0.054 |
| `EffortHours.EndToEndTests.LogicalCandidateV2ArtifactTests` | 3 | 0.051 |
| `EffortHours.EndToEndTests.CandidateMeasuredOperationalArtifactTests` | 1 | 0.050 |
| `EffortHours.EndToEndTests.ValidationSelectionVerifierTests` | 2 | 0.033 |
| `EffortHours.EndToEndTests.CandidateOperationalGateTests` | 1 | 0.023 |
| `EffortHours.EndToEndTests.LogicalCandidateV3ArtifactTests` | 2 | 0.022 |
| `EffortHours.EndToEndTests.LogicalCandidateArtifactTests` | 2 | 0.020 |
| `EffortHours.EndToEndTests.LogicalCandidateOperationalArtifactTests` | 1 | 0.005 |
| `EffortHours.EndToEndTests.RuntimeConfigurationTests` | 2 | 0.004 |

| Slow case | Seconds |
| --- | ---: |
| `EffortHours.EndToEndTests.CandidatePreflightTests.ManualQaReviewFreezeIsDeterministicAndRefusesChangedArtifacts` | 14.348 |
| `EffortHours.EndToEndTests.ChangeBenchmarkCliTests.AuthorPeriodManifestBenchmarkReusesPreparedFixture` | 12.832 |
| `EffortHours.EndToEndTests.ChangeBenchmarkCliTests.AuthorPeriodBenchmarkPreservesReuseAndEquivalenceInCiFixture` | 12.646 |
| `EffortHours.EndToEndTests.ChangeCliTests.DailyBenchmarkBindsSelectedAncestryAndYearAcrossPreflightColdWarmResumeAndReproduction` | 11.920 |
| `EffortHours.EndToEndTests.CalibrationDiagnosticCliTests.UncertaintyEvaluateWritesDevelopmentOnlyRepositoryHeldOutReport` | 11.757 |
| `EffortHours.EndToEndTests.ChangeBenchmarkCliTests.AuthorPeriodManifestBenchmarkFreezesRegressionAndReuseMatrix` | 11.583 |
| `EffortHours.EndToEndTests.ChangeCliTests.CalendarDaysStayInvariantAcrossWindowsAndDateFieldsAreExplicit` | 11.318 |
| `EffortHours.EndToEndTests.ChangeCliTests.SnapshotPortfolioDependencyContextAndJavaScriptPersistenceMatchColdAnalysis` | 10.632 |
| `EffortHours.EndToEndTests.ChangeBenchmarkCliTests.AuthorPeriodProcessMatrixSharesObjectDatabaseWithoutTimingGates` | 10.626 |
| `EffortHours.EndToEndTests.CanonicalJsonOutputTests.CandidateBenchmarkProjectionHasPinnedCanonicalBytesForEveryMeasuredShape` | 10.012 |
| `EffortHours.EndToEndTests.HostReviewCliTests.PacketAndAllQueryKindsAreDeterministicDigestBoundAndExplicit` | 9.401 |
| `EffortHours.EndToEndTests.CalibrationUncertaintyStructureCliTests.EvaluatesStructuralReportsThroughTheCli` | 8.964 |
| `EffortHours.EndToEndTests.ChangeCliTests.ConflictResolvedRebaseRetainsNewBehaviorWithoutDuplicatingSharedWork(substantial: False)` | 8.782 |
| `EffortHours.EndToEndTests.ChangeCliTests.ConflictResolvedRebaseRetainsNewBehaviorWithoutDuplicatingSharedWork(substantial: True)` | 8.679 |
| `EffortHours.EndToEndTests.CalibrationUncertaintyGraphCliTests.EvaluatesGraphReportsThroughTheCli` | 8.423 |

A fresh class-only repeat passed all 77 cases: 278.999 summed test-body seconds and 279.989 TRX wall seconds. No other E2E collection ran in that repeat.

## Scheduling correction

The same Release suite after the five-way collection split passes all 286 cases.
TRX start-to-finish falls from 283.359 to 123.873 seconds (56.3% less, 2.29x).
Case display identities including theory arguments match exactly after ignoring
only the deliberate class-name moves. Assertions, fixtures, CLI/Git commands,
runner limits and required platform gates are unchanged.

| Split collection | Cases | Test-body seconds |
| --- | ---: | ---: |
| `EffortHours.EndToEndTests.ChangePortfolioCliTests` | 24 | 88.139 |
| `EffortHours.EndToEndTests.SnapshotPortfolioCliTests` | 24 | 116.526 |
| `EffortHours.EndToEndTests.ChangeCliTests` | 12 | 59.655 |
| `EffortHours.EndToEndTests.SnapshotDailyCliTests` | 9 | 72.223 |
| `EffortHours.EndToEndTests.CalendarCliTests` | 8 | 64.772 |

A second full split-suite run passes the same 286 cases in 121.807 TRX seconds,
57.0% below the unchanged baseline. Split C# sources are pinned
at `68f548200efb402c6e4a3e3f43dde5ec4cd5f019`; this follow-up changes only the measurement record.
Both after runs use fresh test processes with the same Release outputs and runner
configuration. There are no timing/memory CI thresholds or cross-platform speedup
claims from these local Windows observations. Hosted phase results are recorded
separately after required checks.

## Hosted shard checkpoint

At `4c4b6565d22bb7c8f3db211e31a49e24fb6676a8`,
[run 37127105344](https://github.com/VictorZakharov/efforthours/actions/runs/37127105344)
passed every check and verified all 296 cases exactly once on each OS. Four
Windows/macOS and two Linux shards built their OS-specific graph without
repeating the analyzer work retained in the required Quality jobs.

| Measurement | Seconds |
| --- | ---: |
| Complete run, creation to final package job completion | 313 |
| Creation to history job start | 68 |
| Slowest Windows E2E job | 184 |
| That job's restore/build/test steps | 20 / 47 / 99 |
| Slowest macOS E2E job | 139 |
| Slowest Linux E2E job | 131 |

This improves the Windows job from the preceding split-only hosted run's 391
seconds, but does not meet the under-three-minute complete PR target. Later
configuration changes are measured separately; these queue-inclusive observations
are checkpoints, not guarantees or CI thresholds.

The six-Windows/four-macOS/three-Linux follow-up at
`99a6ccd160922722d9f4e9abf6a459580b8085a5` also passed all checks:
[run 37127725632](https://github.com/VictorZakharov/efforthours/actions/runs/37127725632).
Total queue-inclusive time was 236 seconds. The slowest Windows E2E job fell to
135 seconds, but the fourth macOS shard queued for 79 seconds after history
completed. Windows Quality took 178 seconds, including a 55-second cold restore
and 82-second analyzer-enabled build. The next configuration removes the fourth
macOS shard and enables lock-file-keyed dependency caching while retaining locked
restore, every analyzer-enabled Quality build and all platform tests.

The six-Windows/three-macOS/three-Linux cold-cache run at
`7a59714` passed all checks:
[run 37128078683](https://github.com/VictorZakharov/efforthours/actions/runs/37128078683).
Total time was 234 seconds. The last shard completed 179 seconds after creation,
but queued coverage gates and final packaging added 55 seconds. The next revision
runs coverage coordination within the first shard of each platform, eliminating
three separate verifier-runner requests. Dependency caches are separated by graph
to prevent partial-restore cache races. This checkpoint still misses the target.
