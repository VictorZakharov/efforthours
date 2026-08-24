using System.Diagnostics;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal sealed partial class ChangePortfolioCommand
{
    private static async Task<int> WriteTodaySetupFailureAsync(
        ChangePortfolioCommandOptions options,
        string title,
        DateTimeOffset generatedAt,
        long workflowStarted,
        EngineeringScopeProfile? engineeringScope,
        ChangePortfolioExecutionTelemetry telemetry,
        Exception exception,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        string phase = telemetry.GetLastProgress()?.Phase ??
            ChangePortfolioExecutionPhases.ScopeLoading;
        string message = phase switch
        {
            ChangePortfolioExecutionPhases.ScopeLoading =>
                "The requested engineering scope profile could not be loaded safely.",
            ChangePortfolioExecutionPhases.Acquisition =>
                "Managed repository acquisition did not complete. Confirm GitHub access and cache permissions.",
            _ =>
                "GitHub provider discovery did not complete. Confirm gh authentication and owner access.",
        };
        string category = exception.GetType().Name;
        ChangePortfolioComparisonFailure failure = new()
        {
            RepositoryId = "provider",
            Phase = phase,
            Category = category,
            Message = message,
            MessageDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest(
                category + "\n" + message),
        };
        ChangePortfolioScopeProfile profile = engineeringScope?.Contract ?? UnavailableScopeProfile();
        ChangeAuthorPeriodManifest manifest = EmptyTodayManifest(options, generatedAt);
        ChangePortfolioSelection selection =
            ChangeAuthorPeriodManifestIdentity.CreateReportSelection(manifest);
        ChangePortfolioComparisonInputs inputs =
            ChangePortfolioComparisonInputLoader.CreateTodayToDate(
                selection.AuthorPeriodManifest!,
                generatedAt,
                options.CapacityHours!.Value);
        ChangePortfolioHostDiscovery discovery = new()
        {
            ScopeDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest(
                selection.AuthorPeriodManifest!.ManifestDigest + "\n" + profile.Digest + "\n" + phase),
            IdentitySources = "requested-provider-identity",
            Complete = false,
            ElapsedMilliseconds = decimal.Round(
                (decimal)Stopwatch.GetElapsedTime(workflowStarted).TotalMilliseconds,
                3,
                MidpointRounding.AwayFromZero),
        };
        bool checkpointEnabled = !options.NoCheckpoint &&
            (options.OutputPath is not null || options.CheckpointPath is not null);
        ChangePortfolioComparisonExecution execution =
            ChangePortfolioComparisonExecutionFactory.Create(
                [],
                checkpointEnabled,
                telemetry,
                failure);
        execution = execution with
        {
            Resources = execution.Resources! with { SelectionScopeComplete = false },
        };
        ChangePortfolioComparisonBuildOptions buildOptions = new()
        {
            View = options.ComparisonView,
            Title = title,
            GeneratedAt = generatedAt,
            AsOf = generatedAt,
            Discovery = discovery,
            ScopeProfile = profile,
            ScopeSummary = new ChangePortfolioScopeSummary(),
            CliVersion = CliVersion(),
            Profile = options.Profile,
            BucketKind = inputs.BucketKind,
            BucketPolicy = inputs.BucketPolicy,
            ContributorNormalization = options.ContributorNormalization,
            BucketManifest = inputs.BucketManifest,
            Buckets = inputs.Buckets,
            CapacityManifest = inputs.CapacityManifest,
            SourceManifest = manifest,
            ExecutionTelemetry = telemetry,
            ExecutionOverride = execution,
        };
        ChangePortfolioComparisonReport report =
            ChangePortfolioComparisonBuilder.BuildIncomplete(buildOptions);
        string output;
        using (telemetry.Measure(ChangePortfolioExecutionPhases.Rendering))
        {
            (report, output) = RenderComparisonWithOutputUsage(report, options, workflowStarted);
        }

        long renderedBytes = report.Execution.Resources!.RenderedOutputBytes;
        execution = ChangePortfolioComparisonExecutionFactory.Create(
            [],
            checkpointEnabled,
            telemetry,
            failure);
        execution = execution with
        {
            Resources = execution.Resources! with
            {
                SelectionScopeComplete = false,
                RenderedOutputBytes = renderedBytes,
            },
        };
        report = ChangePortfolioComparisonBuilder.BuildIncomplete(
            buildOptions with { ExecutionOverride = execution });
        (_, output) = RenderComparisonWithOutputUsage(report, options, workflowStarted);
        int write = await WriteOutputAsync(
            output,
            options.OutputPath,
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
        await standardError.WriteLineAsync(
            $"eh: today failed phase={phase}; diagnostic={failure.MessageDigest}")
            .ConfigureAwait(false);
        return write == CliExitCodes.Success ? CliExitCodes.InvalidInput : write;
    }

    private static ChangePortfolioScopeProfile UnavailableScopeProfile()
    {
        ChangePortfolioScopeProfile bundled = EngineeringScopeProfile.LoadBundled().Contract;
        return bundled with
        {
            Source = "unavailable",
            Digest = ChangePortfolioComparisonIdentity.ComputeTextDigest(
                bundled.Digest + "\nunavailable"),
        };
    }

    private static ChangeAuthorPeriodManifest EmptyTodayManifest(
        ChangePortfolioCommandOptions options,
        DateTimeOffset asOf)
    {
        TimeZoneInfo zone = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZone);
        DateTime localDate = TimeZoneInfo.ConvertTime(asOf, zone).Date;
        DateTime localMidnight = DateTime.SpecifyKind(localDate, DateTimeKind.Unspecified);
        DateTimeOffset since = new(localMidnight, zone.GetUtcOffset(localMidnight));
        return new ChangeAuthorPeriodManifest
        {
            Selection = new ChangeAuthorPeriodManifestSelection
            {
                SinceInclusive = since.ToUniversalTime(),
                UntilExclusive = asOf.ToUniversalTime(),
                TimeZone = zone.Id,
                DateField = options.DateField,
                MergePolicy = options.MergePolicy,
                CoauthorPolicy = options.CoauthorPolicy,
            },
            Contributors =
            [
                new ChangeAuthorPeriodManifestContributor
                {
                    Id = "me",
                    Aliases = [.. options.AuthorAliases
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Order(StringComparer.OrdinalIgnoreCase)],
                },
            ],
            Repositories = [],
        };
    }
}
