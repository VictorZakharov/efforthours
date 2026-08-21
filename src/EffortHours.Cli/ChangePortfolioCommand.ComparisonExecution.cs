using System.Diagnostics;
using System.Reflection;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Pricing;
using EffortHours.Reporting;

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
        DateTimeOffset generatedAt = (options.GeneratedAt ?? DateTimeOffset.UtcNow)
            .ToUniversalTime();
        string title = options.ReportTitle ??
            (options.ComparisonView == ChangePortfolioComparisonView.Findings
                ? "EffortHours engineering findings"
                : "EffortHours portfolio trend");
        ResolvedChangeAuthorPeriodManifest resolved =
            await ChangeAuthorPeriodManifestLoader.LoadAsync(
                options.AuthorPeriodManifestPath!,
                cancellationToken).ConfigureAwait(false);
        ChangePortfolioSelection selection =
            ChangeAuthorPeriodManifestIdentity.CreateReportSelection(
                resolved.Manifest,
                resolved.ManifestDigest);
        ChangePortfolioComparisonInputs inputs =
            await ChangePortfolioComparisonInputLoader.LoadAsync(
                options,
                selection.AuthorPeriodManifest!,
                cancellationToken).ConfigureAwait(false);
        ChangePortfolioRepositoryCheckpointStore? checkpoints = options.NoCheckpoint
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
                standardError,
                cancellationToken).ConfigureAwait(false);
            outcomes.Add(outcome with { Elapsed = Stopwatch.GetElapsedTime(started) });
        }

        if (outcomes.All(outcome => outcome.Failure is null) &&
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

        ChangePortfolioExecutionTelemetry portfolioTelemetry =
            CreateExecutionTelemetry(standardError, "portfolio");
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
            output = options.Format == "markdown"
                ? ChangePortfolioComparisonMarkdownRenderer.Render(comparison)
                : new ChangePortfolioComparisonJsonRenderer(options.Compact).Render(comparison);
        }

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
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        string digest = ChangePortfolioComparisonIdentity.ComputeRepositoryInputDigest(
            resolved.Manifest,
            repository.Id,
            options.Profile,
            ChangeEstimator.Version);
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
                    Telemetry = telemetry,
                };
            }
        }

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
            ChangePortfolioEstimateBatch estimate = plan.Items.Count == 0
                ? new ChangePortfolioEstimateBatch
                {
                    Reports = [],
                    Statistics = new ChangePortfolioExecutionStatistics(),
                }
                : await _changeEstimator.EstimatePortfolioCandidatesWithStatisticsAsync(
                    [.. plan.Items.Select(item => item.Plan)],
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
            List<Diagnostic> diagnostics = [.. plan.Diagnostics];
            if (plan.Items.Count > 0)
            {
                diagnostics.Add(estimate.Statistics.CreateDiagnostic());
            }

            if (checkpoints is not null)
            {
                try
                {
                    await checkpoints.WriteAsync(
                        repository.Id,
                        digest,
                        options.Profile,
                        candidates,
                        plan.Diagnostics,
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
                Telemetry = telemetry,
                Failure = failure,
            };
        }
    }

    private static IReadOnlyList<Diagnostic> CanonicalDiagnostics(
        IReadOnlyList<ChangePortfolioRepositoryOutcome> outcomes,
        ChangePortfolioComparisonCheckpoint checkpoint)
    {
        IEnumerable<Diagnostic> diagnostics = outcomes.SelectMany(outcome => outcome.Diagnostics);
        if (checkpoint.HitCount > 0)
        {
            diagnostics = diagnostics.Append(new Diagnostic
            {
                Code = "FB5333",
                Severity = DiagnosticSeverity.Information,
                Message = $"Reused {checkpoint.HitCount} immutable repository-evidence checkpoint(s); unchanged repositories were not replanned or reanalyzed.",
            });
        }

        return [.. diagnostics
            .DistinctBy(value => $"{value.Code}\0{value.Severity}\0{value.Message}", StringComparer.Ordinal)
            .OrderBy(value => value.Code, StringComparer.Ordinal)
            .ThenBy(value => value.Message, StringComparer.Ordinal)];
    }

    private static string SafeRepositoryFailure(
        string message,
        ChangeAuthorPeriodManifestRepository repository,
        ResolvedChangeAuthorPeriodManifest resolved)
    {
        string safe = message.Replace('\r', ' ').Replace('\n', ' ');
        safe = safe.Replace(repository.RepositoryPath, "<repository-path>", StringComparison.OrdinalIgnoreCase);
        safe = safe.Replace(
            resolved.RepositoryPaths[repository.Id],
            "<repository-path>",
            StringComparison.OrdinalIgnoreCase);
        foreach (string alias in resolved.Manifest.Contributors.SelectMany(value => value.Aliases))
        {
            safe = safe.Replace(alias, "<identity-alias>", StringComparison.OrdinalIgnoreCase);
        }

        return string.IsNullOrWhiteSpace(safe)
            ? $"Repository '{repository.Id}' failed without an actionable message."
            : safe;
    }

    private static Exception RootException(Exception exception)
    {
        Exception current = exception;
        while (current.InnerException is not null)
        {
            current = current.InnerException;
        }

        return current;
    }

    private static string CliVersion() => typeof(ChangePortfolioCommand).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
        .InformationalVersion ?? "unknown";
}
