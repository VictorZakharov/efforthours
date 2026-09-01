using System.Text;
using System.Text.Json;
using EffortHours.Change;
using EffortHours.Contracts.V1;
using EffortHours.Pricing;
using EffortHours.Reporting;

namespace EffortHours.Cli;

internal sealed partial class ChangePortfolioCommand
{
    private readonly ChangeEstimator _changeEstimator;
    private readonly Func<string, string, string?, bool, CancellationToken, Task<GitChangePlan>>
        _planPullRequest;
    private readonly ManagedPullRequestPlanner _managedPullRequests = new();
    private readonly ManagedGitQueryPlanner _managedGitQueries = new();
    private readonly Func<string, GitAuthorPeriodPortfolioOptions, ChangePortfolioExecutionTelemetry?, CancellationToken,
        Task<GitAuthorPeriodPortfolioPlan>> _planAuthorPeriod;
    private readonly Func<string, CancellationToken,
        Task<IReadOnlyList<ResolvedChangePortfolioManifestItem>>> _loadManifest;
    private readonly Func<string, ChangePortfolioExecutionTelemetry, CancellationToken,
        Task<GitAuthorPeriodManifestPortfolioPlan>>
        _planAuthorPeriodManifest;
    private readonly Func<string, bool, ChangePortfolioExecutionTelemetry, CancellationToken,
        Task<GitAuthorPeriodManifestPortfolioPlan>>? _planAuthorPeriodManifestWithAcquisition;
    private readonly Func<string, ChangePortfolioExecutionTelemetry, CancellationToken,
        Task<GitAuthorPeriodManifestScopePlan>>
        _measureAuthorPeriodManifest;
    private readonly Func<GitHubAuthorPeriodDiscoveryRequest, CancellationToken,
        Task<GitHubAuthorPeriodDiscoveryResult>> _discoverToday;

    public async Task<int> ExecuteAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        ChangePortfolioCommandParseResult parsed = ChangePortfolioCommandOptionsParser.Parse(arguments);
        if (parsed.ShowHelp)
        {
            await standardOutput.WriteLineAsync(ChangePortfolioHelp.Text).ConfigureAwait(false);
            return CliExitCodes.Success;
        }

        if (parsed.Error is not null)
        {
            return await UsageErrorAsync(standardError, parsed.Error).ConfigureAwait(false);
        }

        ChangePortfolioCommandOptions options = parsed.Options!;
        RateCard? rateCard = Rate(options);
        ChangePortfolioExecutionTelemetry? executionTelemetry = null;
        try
        {
            if (options.Preflight)
            {
                executionTelemetry = CreateExecutionTelemetry(standardError);
                return await ExecutePreflightAsync(
                    options,
                    executionTelemetry,
                    standardOutput,
                    standardError,
                    cancellationToken).ConfigureAwait(false);
            }

            if (options.IsComparison)
            {
                return await ExecuteComparisonAsync(
                    options,
                    rateCard,
                    standardOutput,
                    standardError,
                    cancellationToken).ConfigureAwait(false);
            }

            executionTelemetry =
                options.IsAuthorPeriod || options.IsAuthorPeriodManifest
                    ? CreateExecutionTelemetry(standardError)
                    : null;
            PortfolioCandidates planned = options.IsAuthorPeriodManifest
                ? await PlanAuthorPeriodManifestAsync(
                    options,
                    executionTelemetry!,
                    cancellationToken).ConfigureAwait(false)
                : options.IsManifest
                    ? await PlanManifestAsync(options, cancellationToken).ConfigureAwait(false)
                    : options.IsAuthorPeriod
                        ? await PlanAuthorPeriodAsync(
                            options,
                            executionTelemetry!,
                            cancellationToken).ConfigureAwait(false)
                        : await PlanPullRequestsAsync(options, cancellationToken).ConfigureAwait(false);
            ChangePortfolioReport report = ChangePortfolioReconciler.Reconcile(
                planned.Selection,
                planned.Candidates,
                options.Profile,
                rateCard,
                planned.Diagnostics,
                planned.ExecutionTelemetry);
            cancellationToken.ThrowIfCancellationRequested();
            string output;
            using (planned.ExecutionTelemetry?.Measure(ChangePortfolioExecutionPhases.Rendering))
            {
                output = options.Format == "markdown"
                    ? ChangePortfolioMarkdownRenderer.Render(report)
                    : new ChangePortfolioJsonRenderer(options.Compact).Render(report);
            }

            int exitCode = await WriteOutputAsync(
                output,
                options.OutputPath,
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false);
            if (exitCode == CliExitCodes.Success && planned.ExecutionTelemetry is not null)
            {
                await WriteExecutionTimingsAsync(
                    planned.ExecutionTelemetry,
                    standardError).ConfigureAwait(false);
            }

            return exitCode;
        }
        catch (OperationCanceledException)
        {
            if (executionTelemetry is not null)
            {
                await WriteInterruptedExecutionAsync(executionTelemetry, standardError)
                    .ConfigureAwait(false);
            }

            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or DirectoryNotFoundException or ExternalCommandException or
            InvalidOperationException or IOException or UnauthorizedAccessException or JsonException)
        {
            await standardError.WriteLineAsync($"eh: {exception.Message}").ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }
    }

    private async Task<PortfolioCandidates> PlanPullRequestsAsync(
        ChangePortfolioCommandOptions options,
        CancellationToken cancellationToken)
    {
        List<GitChangePlan> plans = [];
        foreach (string input in options.PullRequests)
        {
            plans.Add(options.RepositoryPath is null
                ? await _managedPullRequests.PlanAsync(
                    input,
                    options.GitHubRepository,
                    options.FetchMissing,
                    cancellationToken).ConfigureAwait(false)
                : await _planPullRequest(
                    options.RepositoryPath,
                    input,
                    options.GitHubRepository,
                    options.FetchMissing,
                    cancellationToken).ConfigureAwait(false));
        }

        ChangePortfolioEstimateBatch estimate =
            await _changeEstimator.EstimatePortfolioCandidatesWithStatisticsAsync(
                plans,
                options.Profile,
                cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ChangeEstimateReport> reports = estimate.Reports;
        List<ChangePortfolioCandidate> candidates = [];
        Dictionary<int, int> occurrences = [];
        foreach (ChangeEstimateReport report in reports)
        {
            int number = report.Selection.PullRequest!.Number;
            int occurrence = occurrences.GetValueOrDefault(number) + 1;
            occurrences[number] = occurrence;
            candidates.Add(new ChangePortfolioCandidate
            {
                RepositoryId = report.Selection.PullRequest.Repository ?? report.Repository.Name,
                SelectorId = occurrence == 1 ? $"pr:{number}" : $"pr:{number}:duplicate-{occurrence}",
                Report = report,
                Attribution = PullRequestAttribution(),
            });
        }

        return new PortfolioCandidates(
            new ChangePortfolioSelection { Kind = ChangePortfolioSelectionKind.PullRequests },
            candidates,
            []);
    }

    private async Task<PortfolioCandidates> PlanManifestAsync(
        ChangePortfolioCommandOptions options,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<ResolvedChangePortfolioManifestItem> manifest =
            await _loadManifest(options.ManifestPath!, cancellationToken)
                .ConfigureAwait(false);
        List<(ChangePortfolioManifestItem Item, GitChangePlan Plan)> planned = [];
        ChangePortfolioRepositoryMap repositories = new();
        foreach (ResolvedChangePortfolioManifestItem resolved in manifest)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ChangePortfolioManifestItem item = resolved.Item;
            GitChangePlan plan = resolved.RepositoryPath is null
                ? await _managedPullRequests.PlanAsync(
                    item.PullRequest,
                    item.GitHubRepository,
                    options.FetchMissing,
                    cancellationToken).ConfigureAwait(false)
                : await _planPullRequest(
                    resolved.RepositoryPath,
                    item.PullRequest,
                    item.GitHubRepository,
                    options.FetchMissing,
                    cancellationToken).ConfigureAwait(false);
            repositories.Add(item.RepositoryId, plan.RepositoryPath);
            planned.Add((item, plan));
        }

        ChangePortfolioEstimateBatch estimate =
            await _changeEstimator.EstimatePortfolioCandidatesWithStatisticsAsync(
                [.. planned.Select(entry => entry.Plan)],
                options.Profile,
                cancellationToken).ConfigureAwait(false);
        IReadOnlyList<ChangeEstimateReport> reports = estimate.Reports;
        List<ChangePortfolioCandidate> candidates = [];
        for (int index = 0; index < planned.Count; index++)
        {
            ChangePortfolioManifestItem item = planned[index].Item;
            ChangeEstimateReport report = reports[index];
            candidates.Add(new ChangePortfolioCandidate
            {
                RepositoryId = item.RepositoryId,
                SelectorId = item.Id,
                Report = report,
                Attribution = PullRequestAttribution(),
            });
        }

        return new PortfolioCandidates(
            new ChangePortfolioSelection
            {
                Kind = ChangePortfolioSelectionKind.PullRequests,
                ManifestBased = true,
            },
            candidates,
            [
                new Diagnostic
                {
                    Code = "FB5320",
                    Severity = DiagnosticSeverity.Information,
                    Message = "The manifest supplied execution-only local paths or provider repository identities. Reports retain caller repository IDs and immutable PR identities, not host paths or managed-cache locations.",
                },
            ]);
    }

    private static ChangePortfolioAttribution PullRequestAttribution() => new()
    {
        Kind = ChangePortfolioAttributionKind.PullRequest,
        MergeCommit = false,
        ParentCount = 0,
    };

    private static RateCard? Rate(ChangePortfolioCommandOptions options) => options.NoRate
        ? null
        : options.HourlyRate is null
            ? DefaultRateCatalog.RateCard
            : new RateCard
            {
                Id = "user-supplied-cli-rate",
                Name = "User-supplied CLI rate",
                Currency = options.Currency,
                HourlyRate = options.HourlyRate.Value,
                Methodology = "Explicit caller override; this rate is not an EffortHours market-rate claim.",
            };

    private static async Task<int> WriteOutputAsync(
        string content,
        string? outputPath,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        long renderedBytes = RenderedOutputByteCount(content);
        if (renderedBytes > ChangePortfolioLimits.MaximumRenderedOutputBytes)
        {
            await standardError.WriteLineAsync(
                $"eh: change portfolio output requires {renderedBytes} bytes; the declared limit is " +
                $"{ChangePortfolioLimits.MaximumRenderedOutputBytes} bytes. Use a time-bucketed " +
                "Markdown summary from the same globally reconciled calculation; do not add " +
                "independent interval reports.").ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        if (outputPath is null)
        {
            await standardOutput.WriteLineAsync(content.TrimEnd().AsMemory(), cancellationToken)
                .ConfigureAwait(false);
            return CliExitCodes.Success;
        }

        try
        {
            string fullPath = Path.GetFullPath(outputPath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temporaryPath = Path.Combine(
                directory ?? Environment.CurrentDirectory,
                $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
            try
            {
                await using (FileStream stream = new(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    4096,
                    FileOptions.Asynchronous | FileOptions.WriteThrough))
                await using (StreamWriter writer = new(stream, new UTF8Encoding(false)))
                {
                    await writer.WriteAsync(
                        (content.TrimEnd() + Environment.NewLine).AsMemory(),
                        cancellationToken).ConfigureAwait(false);
                    await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                    stream.Flush(flushToDisk: true);
                }

                File.Move(temporaryPath, fullPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            return CliExitCodes.Success;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await standardError.WriteLineAsync($"Could not write change portfolio: {exception.Message}")
                .ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }
    }

    private static long RenderedOutputByteCount(string content) =>
        Encoding.UTF8.GetByteCount(content.TrimEnd() + Environment.NewLine);

    private static async Task<int> UsageErrorAsync(TextWriter standardError, string message)
    {
        await standardError.WriteLineAsync($"eh: {message}").ConfigureAwait(false);
        await standardError.WriteLineAsync("Run 'eh change portfolio --help' for usage.").ConfigureAwait(false);
        return CliExitCodes.UsageError;
    }

    private sealed record PortfolioCandidates(
        ChangePortfolioSelection Selection,
        IReadOnlyList<ChangePortfolioCandidate> Candidates,
        IReadOnlyList<Diagnostic> Diagnostics,
        ChangePortfolioExecutionTelemetry? ExecutionTelemetry = null,
        ChangePortfolioExecutionStatistics? ExecutionStatistics = null,
        ChangeAuthorPeriodManifest? AuthorPeriodManifest = null);

}
