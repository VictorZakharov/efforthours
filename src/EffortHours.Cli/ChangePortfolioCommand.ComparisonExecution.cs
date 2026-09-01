using System.Diagnostics;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Pricing;

namespace EffortHours.Cli;

internal sealed partial class ChangePortfolioCommand
{
    private async Task<int> ExecuteComparisonAsync(
        ChangePortfolioCommandOptions options,
        RateCard? rateCard,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        long workflowStarted = Stopwatch.GetTimestamp();
        ChangePortfolioExecutionTelemetry portfolioTelemetry =
            CreateExecutionTelemetry(standardError, "portfolio");
        DateTimeOffset generatedAt = (options.GeneratedAt ?? DateTimeOffset.UtcNow)
            .ToUniversalTime();
        string title = options.ReportTitle ??
            (options.Today
                ? "EffortHours today-to-date capacity"
                : options.ComparisonView == ChangePortfolioComparisonView.Findings
                ? "EffortHours engineering findings"
                : "EffortHours portfolio trend");
        TodayDiscoveryOutcome discovery = await DiscoverTodayAsync(
            options,
            title,
            generatedAt,
            workflowStarted,
            portfolioTelemetry,
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (discovery.ExitCode is int exitCode)
        {
            return exitCode;
        }

        GitHubAuthorPeriodDiscoveryResult? today = discovery.Today;
        ResolvedChangeAuthorPeriodManifest resolved = today is null
            ? await ChangeAuthorPeriodManifestLoader.LoadAsync(
                options.AuthorPeriodManifestPath!,
                cancellationToken).ConfigureAwait(false)
            : new ResolvedChangeAuthorPeriodManifest(
                today.Manifest,
                ChangeAuthorPeriodManifestIdentity.ComputeDigest(today.Manifest),
                today.RepositoryPaths);
        if (today is null)
        {
            resolved = await MaterializeComparisonManifestAsync(
                resolved, options, cancellationToken).ConfigureAwait(false);
        }
        ChangePortfolioSelection selection =
            ChangeAuthorPeriodManifestIdentity.CreateReportSelection(
                resolved.Manifest,
                resolved.ManifestDigest);
        ChangePortfolioComparisonInputs inputs = today is null
            ? await ChangePortfolioComparisonInputLoader.LoadAsync(
                options,
                selection.AuthorPeriodManifest!,
                cancellationToken).ConfigureAwait(false)
            : ChangePortfolioComparisonInputLoader.CreateTodayToDate(
                selection.AuthorPeriodManifest!,
                today.AsOf,
                options.CapacityHours!.Value);
        ChangePortfolioRepositoryCheckpointStore? checkpoints = options.NoCheckpoint ||
            options.OutputPath is null && options.CheckpointPath is null
            ? null
            : new ChangePortfolioRepositoryCheckpointStore(
                options.CheckpointPath ?? Path.GetFullPath(options.OutputPath!) + ".eh-checkpoint");
        GitPortfolioPlanner planner = new();
        List<ChangePortfolioRepositoryOutcome> outcomes = [];
        foreach (ChangeAuthorPeriodManifestRepository repository in
            resolved.Manifest.Repositories.OrderBy(value => value.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            long started = Stopwatch.GetTimestamp();
            ChangePortfolioRepositoryOutcome outcome = await ExecuteRepositoryShardAsync(
                options,
                resolved,
                repository,
                planner,
                checkpoints,
                today?.PathAdmissions.GetValueOrDefault(repository.Id),
                today?.ScopeProfile.Digest,
                standardError,
                cancellationToken).ConfigureAwait(false);
            outcomes.Add(outcome with { Elapsed = Stopwatch.GetElapsedTime(started) });
        }

        if (!options.Today && outcomes.All(outcome => outcome.Failure is null) &&
            outcomes.Sum(outcome => outcome.Candidates.Count) == 0)
        {
            ChangePortfolioRepositoryOutcome first = outcomes[0];
            const string message =
                "No commits matched the manifest contributors, selected timestamp field, and inclusive/exclusive interval.";
            outcomes[0] = first with
            {
                Status = ChangePortfolioRepositoryExecutionStatus.Failed,
                Failure = new ChangePortfolioComparisonFailure
                {
                    RepositoryId = first.RepositoryId,
                    Phase = ChangePortfolioExecutionPhases.Selection,
                    Category = nameof(InvalidOperationException),
                    Message = message,
                    MessageDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest(message),
                },
            };
        }

        ChangePortfolioComparisonExecution execution =
            ChangePortfolioComparisonExecutionFactory.Create(
                outcomes,
                checkpoints is not null,
                portfolioTelemetry);
        ChangePortfolioComparisonBuildOptions buildOptions = new()
        {
            View = options.ComparisonView,
            Title = title,
            GeneratedAt = generatedAt,
            CliVersion = CliVersion(),
            Profile = options.Profile,
            BucketKind = inputs.BucketKind,
            BucketPolicy = inputs.BucketPolicy,
            ContributorNormalization = options.ContributorNormalization,
            BucketManifest = inputs.BucketManifest,
            Buckets = inputs.Buckets,
            CapacityManifest = inputs.CapacityManifest,
            SourceManifest = resolved.Manifest,
            ExecutionTelemetry = portfolioTelemetry,
            ExecutionOverride = execution,
            AsOf = today?.AsOf,
            Discovery = today?.Discovery,
            ScopeProfile = today?.ScopeProfile,
            ScopeSummary = today is null ? null : ScopeSummary(outcomes),
        };
        ChangePortfolioComparisonReport comparison;
        if (execution.Failures.Count > 0)
        {
            comparison = ChangePortfolioComparisonBuilder.BuildIncomplete(buildOptions);
        }
        else
        {
            IReadOnlyList<ChangePortfolioCandidate> candidates =
                [.. outcomes.SelectMany(outcome => outcome.Candidates)];
            IReadOnlyList<Diagnostic> diagnostics =
                CanonicalDiagnostics(outcomes, execution.Checkpoint);
            ChangePortfolioReport source = ChangePortfolioReconciler.Reconcile(
                selection,
                candidates,
                options.Profile,
                rateCard,
                diagnostics,
                portfolioTelemetry);
            execution = ChangePortfolioComparisonExecutionFactory.Create(
                outcomes,
                checkpoints is not null,
                portfolioTelemetry);
            comparison = ChangePortfolioComparisonBuilder.Build(
                source,
                buildOptions with { ExecutionOverride = execution });
        }

        string output;
        using (portfolioTelemetry.Measure(ChangePortfolioExecutionPhases.Rendering))
        {
            (comparison, output) = RenderComparisonWithOutputUsage(
                comparison,
                options,
                workflowStarted);
        }

        execution = ChangePortfolioComparisonExecutionFactory.Create(
            outcomes,
            checkpoints is not null,
            portfolioTelemetry);
        comparison = comparison with { Execution = execution };
        (comparison, output) = RenderComparisonWithOutputUsage(
            comparison,
            options,
            workflowStarted);

        int write = await WriteOutputAsync(
            output,
            options.OutputPath,
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (write != CliExitCodes.Success)
        {
            return write;
        }

        return comparison.Status == ChangePortfolioComparisonStatus.Complete
            ? CliExitCodes.Success
            : CliExitCodes.InvalidInput;
    }

    private async Task<ChangePortfolioRepositoryOutcome> ExecuteRepositoryShardAsync(
        ChangePortfolioCommandOptions options,
        ResolvedChangeAuthorPeriodManifest resolved,
        ChangeAuthorPeriodManifestRepository repository,
        GitPortfolioPlanner planner,
        ChangePortfolioRepositoryCheckpointStore? checkpoints,
        ChangePathAdmission? pathAdmission,
        string? scopeProfileDigest,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        string digest = ChangePortfolioComparisonIdentity.ComputeRepositoryInputDigest(
            resolved.Manifest,
            repository.Id,
            options.Profile,
            ChangeEstimator.Version,
            scopeProfileDigest);
        ChangePortfolioExecutionTelemetry telemetry =
            CreateExecutionTelemetry(standardError, repository.Id);
        if (checkpoints is not null)
        {
            ChangePortfolioRepositoryCheckpointLoad? cached = await checkpoints.TryLoadAsync(
                repository.Id,
                digest,
                options.Profile,
                cancellationToken).ConfigureAwait(false);
            if (cached is not null)
            {
                return new ChangePortfolioRepositoryOutcome
                {
                    RepositoryId = repository.Id,
                    InputDigest = digest,
                    Status = ChangePortfolioRepositoryExecutionStatus.Reused,
                    CheckpointDisposition = ChangePortfolioCheckpointDisposition.Hit,
                    Candidates = cached.Candidates,
                    Diagnostics = cached.Diagnostics,
                    Scope = cached.Scope,
                    CheckpointReadBytes = cached.ReadBytes,
                    AdmittedChangeCount = AdmittedChangeCount(cached.Candidates),
                    ScopeEmptyChangeCount =
                        cached.Candidates.Count - AdmittedChangeCount(cached.Candidates),
                    Telemetry = telemetry,
                };
            }
        }

        GitAuthorPeriodManifestRepositoryScope? measuredScope = null;
        try
        {
            ChangeAuthorPeriodManifest submanifest = resolved.Manifest with
            {
                Repositories = [repository],
            };
            string submanifestDigest = ChangeAuthorPeriodManifestIdentity.ComputeDigest(submanifest);
            GitAuthorPeriodManifestPortfolioPlan plan =
                await planner.PlanAuthorPeriodManifestAsync(
                    submanifest,
                    submanifestDigest,
                    new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [repository.Id] = resolved.RepositoryPaths[repository.Id],
                    },
                    telemetry,
                    allowEmptySelection: true,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            measuredScope = plan.RepositoryScopes.Single();
            using (telemetry.Measure(ChangePortfolioExecutionPhases.Preflight))
            {
                if (measuredScope.SelectedChangeCount != plan.Items.Count)
                {
                    throw new InvalidOperationException(
                        "Internal preflight selected-change accounting is inconsistent.");
                }
            }

            GitChangePlan[] scopedPlans = [.. plan.Items.Select(item => item.Plan with
            {
                RepositoryName = repository.Id,
                PathAdmission = pathAdmission,
            })];
            ChangePortfolioEstimateBatch estimate = plan.Items.Count == 0
                ? new ChangePortfolioEstimateBatch
                {
                    Reports = [],
                    Statistics = new ChangePortfolioExecutionStatistics(),
                }
                : await _changeEstimator.EstimatePortfolioCandidatesWithStatisticsAsync(
                    scopedPlans,
                    options.Profile,
                    telemetry,
                    cancellationToken).ConfigureAwait(false);
            List<ChangePortfolioCandidate> candidates = [];
            for (int index = 0; index < plan.Items.Count; index++)
            {
                GitAuthorPeriodManifestPortfolioItem item = plan.Items[index];
                candidates.Add(new ChangePortfolioCandidate
                {
                    RepositoryId = item.RepositoryId,
                    SelectorId = item.SelectorId,
                    Report = estimate.Reports[index],
                    Attribution = item.Attribution,
                });
            }

            ChangePortfolioCheckpointDisposition disposition = checkpoints is null
                ? ChangePortfolioCheckpointDisposition.Disabled
                : ChangePortfolioCheckpointDisposition.MissWritten;
            long checkpointWrittenBytes = 0;
            List<Diagnostic> diagnostics = [.. plan.Diagnostics];
            if (plan.Items.Count > 0)
            {
                diagnostics.Add(estimate.Statistics.CreateDiagnostic());
            }

            if (checkpoints is not null)
            {
                try
                {
                    checkpointWrittenBytes = await checkpoints.WriteAsync(
                        repository.Id,
                        digest,
                        options.Profile,
                        candidates,
                        plan.Diagnostics,
                        measuredScope,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException or
                        InvalidOperationException or ArgumentException)
                {
                    disposition = ChangePortfolioCheckpointDisposition.MissFailed;
                    diagnostics.Add(new Diagnostic
                    {
                        Code = "FB5334",
                        Severity = DiagnosticSeverity.Warning,
                        Message = $"Repository '{repository.Id}' completed, but its resumable evidence checkpoint could not be written.",
                    });
                }
            }

            return new ChangePortfolioRepositoryOutcome
            {
                RepositoryId = repository.Id,
                InputDigest = digest,
                Status = ChangePortfolioRepositoryExecutionStatus.Complete,
                CheckpointDisposition = disposition,
                Candidates = candidates,
                Diagnostics = diagnostics,
                Telemetry = telemetry,
                Statistics = estimate.Statistics,
                Scope = measuredScope,
                CheckpointWrittenBytes = checkpointWrittenBytes,
                AdmittedChangeCount = AdmittedChangeCount(candidates),
                ScopeEmptyChangeCount = candidates.Count - AdmittedChangeCount(candidates),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or DirectoryNotFoundException or ExternalCommandException or
                InvalidOperationException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            ChangePortfolioProgress? progress = telemetry.GetLastProgress();
            string phase = progress?.Phase ?? ChangePortfolioExecutionPhases.ManifestValidation;
            Exception root = RootException(exception);
            string message = SafeRepositoryFailure(root.Message, repository, resolved);
            ChangePortfolioComparisonFailure failure = new()
            {
                RepositoryId = repository.Id,
                Phase = phase,
                Category = root.GetType().Name,
                Message = message,
                MessageDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest(
                    root.GetType().Name + "\n" + message),
            };
            await standardError.WriteLineAsync(
                $"eh: portfolio repository={repository.Id} failed phase={phase}; " +
                $"diagnostic={failure.MessageDigest}").ConfigureAwait(false);
            return new ChangePortfolioRepositoryOutcome
            {
                RepositoryId = repository.Id,
                InputDigest = digest,
                Status = ChangePortfolioRepositoryExecutionStatus.Failed,
                CheckpointDisposition = checkpoints is null
                    ? ChangePortfolioCheckpointDisposition.Disabled
                    : ChangePortfolioCheckpointDisposition.MissFailed,
                Scope = measuredScope,
                Telemetry = telemetry,
                ScopeEmptyChangeCount = measuredScope?.SelectedChangeCount ?? 0,
                Failure = failure,
            };
        }
    }

}
