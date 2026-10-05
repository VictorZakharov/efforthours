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
        CancellationToken cancellationToken,
        ResolvedChangeAuthorPeriodManifest? calendarManifest = null)
    {
        long workflowStarted = Stopwatch.GetTimestamp();
        ChangePortfolioExecutionTelemetry portfolioTelemetry =
            CreateExecutionTelemetry(standardError, "portfolio");
        DateTimeOffset generatedAt = (options.GeneratedAt ?? DateTimeOffset.UtcNow)
            .ToUniversalTime();
        string title = options.ReportTitle ?? DefaultComparisonTitle(options);
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
        ResolvedChangeAuthorPeriodManifest resolved = calendarManifest ?? (today is null
            ? await ChangeAuthorPeriodManifestLoader.LoadAsync(
                options.AuthorPeriodManifestPath!,
                cancellationToken).ConfigureAwait(false)
            : new ResolvedChangeAuthorPeriodManifest(
                today.Manifest,
                ChangeAuthorPeriodManifestIdentity.ComputeDigest(today.Manifest),
                today.RepositoryPaths));
        if (today is null)
        {
            resolved = await MaterializeComparisonManifestAsync(
                resolved, options, cancellationToken).ConfigureAwait(false);
        }
        EngineeringScopeProfile? explicitScope = today is null && options.Scope == "engineering"
            ? EngineeringScopeProfile.Load() : null;
        ChangePortfolioSelection selection =
            ChangeAuthorPeriodManifestIdentity.CreateReportSelection(
                resolved.Manifest,
                resolved.ManifestDigest);
        ChangePortfolioComparisonInputs inputs = today is null
            ? await ChangePortfolioComparisonInputLoader.LoadAsync(
                options,
                selection.AuthorPeriodManifest!,
                cancellationToken).ConfigureAwait(false)
            : options.Today
                ? ChangePortfolioComparisonInputLoader.CreateTodayToDate(
                    selection.AuthorPeriodManifest!,
                    today.AsOf,
                    options.CapacityHours!.Value)
                : ChangePortfolioComparisonInputLoader.CreateNamedPeriod(
                    selection.AuthorPeriodManifest!,
                    options.Period!.Value,
                    options.Breakdown,
                    options.CapacityHoursPerDay!.Value);
        if (options.CalendarReport)
        {
            inputs = inputs with
            {
                CapacityManifest = new ChangePortfolioCapacityManifest
                {
                    CalendarPolicy = "Reference hours per local calendar day, including idle and partial days; not actual labor.",
                    Entries = [.. inputs.Buckets.Select(bucket => new ChangePortfolioCapacityEntry
                {
                    BucketId = bucket.Id, ContributorId = "self", Hours = options.CapacityHoursPerDay!.Value,
                })],
                }
            };
        }
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
                today?.PathAdmissions.GetValueOrDefault(repository.Id) ??
                    explicitScope?.CreateAdmission(repository.ScopeRepository ?? repository.GitHubRepository ?? repository.Id),
                today?.ScopeProfile.Digest ?? explicitScope?.Contract.Digest,
                standardError,
                cancellationToken).ConfigureAwait(false);
            outcomes.Add(outcome with { Elapsed = Stopwatch.GetElapsedTime(started) });
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
            ScopeProfile = today?.ScopeProfile ?? explicitScope?.Contract,
            ScopeSummary = today is null && explicitScope is null ? null : ScopeSummary(outcomes),
            NativePeriod = CreateNativePeriodMetadata(options, today),
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
            diagnostics = [.. diagnostics.Where(diagnostic =>
                diagnostic.Code is not ("FB5325" or "FB5333" or "FB5334"))];
            ChangePortfolioReport source = ChangePortfolioReconciler.Reconcile(
                selection,
                candidates,
                options.Profile,
                rateCard,
                diagnostics,
                portfolioTelemetry,
                independentDays: options.CalendarReport || options.Bucket == "independent-day");
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
        if (options.CalendarReport)
            await standardError.WriteLineAsync($"eh: calendar checkpoint hits={comparison.Execution.Checkpoint.HitCount}, " +
                $"writes={comparison.Execution.Checkpoint.WriteCount}; status={comparison.Status}").ConfigureAwait(false);
        if (write != CliExitCodes.Success)
        {
            return write;
        }

        return comparison.Status == ChangePortfolioComparisonStatus.Complete
            ? CliExitCodes.Success
            : CliExitCodes.InvalidInput;
    }

}
