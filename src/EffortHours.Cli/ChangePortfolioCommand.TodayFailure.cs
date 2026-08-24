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
        EffortHoursAgentAction action = CreateAgentAction(exception, phase);
        phase = action.Phase;
        string message = SafeFailureMessage(action.FailureCode);
        string category = action.FailureCode;
        ChangePortfolioComparisonFailure failure = new()
        {
            RepositoryId = "provider",
            Phase = phase,
            Category = category,
            Message = message,
            MessageDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest(
                category + "\n" + message),
            AgentAction = action,
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
        await standardError.WriteLineAsync(ContractJson.SerializeCompact(action))
            .ConfigureAwait(false);
        return write == CliExitCodes.Success ? CliExitCodes.InvalidInput : write;
    }

    private static EffortHoursAgentAction CreateAgentAction(Exception exception, string phase)
    {
        if (exception is GitHubProviderException provider)
        {
            return provider.Action;
        }

        if (phase == ChangePortfolioExecutionPhases.Acquisition &&
            (exception is UnauthorizedAccessException ||
             exception.Message.Contains("access is denied", StringComparison.OrdinalIgnoreCase) ||
             exception.Message.Contains("permission denied", StringComparison.OrdinalIgnoreCase)))
        {
            return Action(
                "managed-cache-access-denied",
                "managed-cache-acquisition",
                "grant-managed-cache-access");
        }

        if (exception.Message.Contains("GitHub returned", StringComparison.OrdinalIgnoreCase))
        {
            return Action(
                "github-provider-response-malformed",
                phase,
                "retry-after-valid-provider-response");
        }

        return phase == ChangePortfolioExecutionPhases.ScopeLoading
            ? Action("engineering-scope-load-failed", phase, "repair-engineering-scope-profile")
            : Action("github-provider-request-failed", phase, "inspect-github-cli-health");
    }

    private static EffortHoursAgentAction Action(
        string code,
        string phase,
        string suggestedAction) => new()
        {
            FailureCode = code,
            Phase = phase,
            SuggestedAction = suggestedAction,
        };

    private static string SafeFailureMessage(string code) => code switch
    {
        "github-cli-executable-missing" => "The GitHub CLI executable could not be started.",
        "github-cli-config-access-denied" =>
            "The GitHub CLI could not access its authenticated user configuration.",
        "github-cli-unauthenticated" => "The GitHub CLI is not authenticated.",
        "github-owner-forbidden-or-not-found" =>
            "The requested GitHub owner was not found or is not accessible.",
        "github-provider-rate-limited" => "GitHub rate limiting prevented complete discovery.",
        "github-network-unavailable" => "GitHub could not be reached.",
        "github-provider-response-malformed" =>
            "GitHub returned a malformed or incomplete response.",
        "managed-cache-access-denied" =>
            "EffortHours could not access its managed repository cache.",
        "engineering-scope-load-failed" =>
            "The requested engineering scope profile could not be loaded safely.",
        _ => "GitHub provider discovery did not complete.",
    };

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
