using System.Text;
using System.Text.Json;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Pricing;
using EffortHours.Reporting;

namespace EffortHours.Cli;

internal sealed class ChangeCommand
{
    private readonly ChangeEstimator _estimator;
    private readonly GitChangePlanner _gitPlanner;
    private readonly ManagedGitQueryPlanner _managedGitQueryPlanner;
    private readonly ManagedPullRequestPlanner _managedPullRequestPlanner;
    private readonly NonGitChangePlanner _nonGitPlanner;

    public ChangeCommand()
        : this(
            new ChangeEstimator(),
            new GitChangePlanner(),
            new ManagedGitQueryPlanner(),
            new ManagedPullRequestPlanner(),
            new NonGitChangePlanner())
    {
    }

    public ChangeCommand(
        ChangeEstimator estimator,
        GitChangePlanner gitPlanner,
        NonGitChangePlanner nonGitPlanner)
        : this(
            estimator,
            gitPlanner,
            new ManagedGitQueryPlanner(),
            new ManagedPullRequestPlanner(),
            nonGitPlanner)
    {
    }

    internal ChangeCommand(
        ChangeEstimator estimator,
        GitChangePlanner gitPlanner,
        ManagedGitQueryPlanner managedGitQueryPlanner,
        ManagedPullRequestPlanner managedPullRequestPlanner,
        NonGitChangePlanner nonGitPlanner)
    {
        _estimator = estimator ?? throw new ArgumentNullException(nameof(estimator));
        _gitPlanner = gitPlanner ?? throw new ArgumentNullException(nameof(gitPlanner));
        _managedGitQueryPlanner = managedGitQueryPlanner ??
            throw new ArgumentNullException(nameof(managedGitQueryPlanner));
        _managedPullRequestPlanner = managedPullRequestPlanner ??
            throw new ArgumentNullException(nameof(managedPullRequestPlanner));
        _nonGitPlanner = nonGitPlanner ?? throw new ArgumentNullException(nameof(nonGitPlanner));
    }

    public async Task<int> ExecuteAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(HelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        if (arguments[0].Equals("explain", StringComparison.OrdinalIgnoreCase))
        {
            return await ChangeExplainCommand.ExecuteAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false);
        }

        string? portfolioMode = arguments[0].ToLowerInvariant() switch
        {
            "portfolio" => null,
            "today" => "--today",
            "period" => "--native-period",
            "compare-team" => "--compare-team",
            _ => string.Empty,
        };
        if (portfolioMode != string.Empty)
        {
            return await new ChangePortfolioCommand().ExecuteAsync(
                portfolioMode is null
                    ? [.. arguments.Skip(1)]
                    : [portfolioMode, .. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false);
        }

        if (arguments[0].Equals("scope", StringComparison.OrdinalIgnoreCase))
        {
            return await ChangeScopeCommand.ExecuteAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError).ConfigureAwait(false);
        }

        ChangeCommandParseResult parsed = ChangeCommandOptionsParser.Parse(arguments);
        if (parsed.ShowHelp)
        {
            await standardOutput.WriteLineAsync(HelpText).ConfigureAwait(false);
            return CliExitCodes.Success;
        }

        if (parsed.Error is not null)
        {
            return await UsageErrorAsync(standardError, parsed.Error).ConfigureAwait(false);
        }

        ChangeCommandOptions options = parsed.Options!;

        GitChangePlan? gitPlan = null;
        ChangeEstimateInput? nonGitPlan = null;
        try
        {
            if (options.IsDirectorySelection)
            {
                nonGitPlan = await _nonGitPlanner.PlanDirectoriesAsync(
                    options.BaseDirectory!,
                    options.HeadDirectory!,
                    cancellationToken).ConfigureAwait(false);
            }
            else if (options.IsEvidenceSelection)
            {
                RepositoryEvidence baseEvidence = await ChangeEvidenceFileLoader.LoadAsync(
                    options.BaseEvidencePath!,
                    cancellationToken).ConfigureAwait(false);
                RepositoryEvidence headEvidence = await ChangeEvidenceFileLoader.LoadAsync(
                    options.HeadEvidencePath!,
                    cancellationToken).ConfigureAwait(false);
                nonGitPlan = NonGitChangePlanner.PlanEvidence(
                    baseEvidence,
                    headEvidence,
                    cancellationToken);
            }
            else
            {
                gitPlan = await PlanGitAsync(options, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or DirectoryNotFoundException or ExternalCommandException or
            InvalidOperationException or IOException or UnauthorizedAccessException or JsonException)
        {
            await standardError.WriteLineAsync($"eh: {exception.Message}").ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        RateCard? rateCard = options.NoRate
            ? null
            : options.HourlyRate is null
                ? DefaultRateCatalog.RateCard
                : new RateCard
                {
                    Id = "user-supplied-cli-rate",
                    Name = "User-supplied CLI rate",
                    Currency = options.Currency,
                    HourlyRate = options.HourlyRate.Value,
                    Methodology = "Explicit caller override; this rate is not a EffortHours market-rate claim.",
                };
        ChangeEstimateReport report;
        try
        {
            report = nonGitPlan is not null
                ? await _estimator.EstimateAsync(
                    nonGitPlan,
                    options.Profile,
                    rateCard,
                    cancellationToken).ConfigureAwait(false)
                : await _estimator.EstimateAsync(
                    gitPlan!,
                    options.Profile,
                    rateCard,
                    cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is ArgumentException or DirectoryNotFoundException or ExternalCommandException or
            InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            await standardError.WriteLineAsync($"eh: {exception.Message}").ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        cancellationToken.ThrowIfCancellationRequested();
        string output = options.Format == "markdown"
            ? ChangeEstimateMarkdownRenderer.Render(report)
            : new ChangeEstimateJsonRenderer(options.Compact).Render(report);
        cancellationToken.ThrowIfCancellationRequested();
        return await WriteOutputAsync(
            output,
            options.OutputPath,
            "change estimate",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<GitChangePlan> PlanGitAsync(
        ChangeCommandOptions options,
        CancellationToken cancellationToken)
    {
        if (options.RepositoryPath is null)
        {
            if (options.PullRequest is not null)
            {
                return await _managedPullRequestPlanner.PlanAsync(
                    options.PullRequest,
                    options.GitHubRepository,
                    options.FetchMissing,
                    cancellationToken).ConfigureAwait(false);
            }

            string repository = options.GitHubRepository!;
            if (options.Commit is not null)
            {
                return await _managedGitQueryPlanner.PlanCommitAsync(
                    repository,
                    options.Commit,
                    options.Parent,
                    options.FetchMissing,
                    cancellationToken).ConfigureAwait(false);
            }

            if (options.Range is not null)
            {
                return await _managedGitQueryPlanner.PlanRangeAsync(
                    repository,
                    options.Range,
                    options.FetchMissing,
                    cancellationToken).ConfigureAwait(false);
            }

            return await _managedGitQueryPlanner.PlanBaseHeadAsync(
                repository,
                options.BaseRevision!,
                options.HeadRevision!,
                options.FetchMissing,
                cancellationToken).ConfigureAwait(false);
        }

        if (options.Commit is not null)
        {
            return await _gitPlanner.PlanCommitAsync(
                options.RepositoryPath,
                options.Commit,
                options.Parent,
                cancellationToken).ConfigureAwait(false);
        }

        if (options.Range is not null)
        {
            return await _gitPlanner.PlanRangeAsync(
                options.RepositoryPath,
                options.Range,
                cancellationToken).ConfigureAwait(false);
        }

        if (options.PullRequest is not null)
        {
            return await _gitPlanner.PlanPullRequestAsync(
                options.RepositoryPath,
                options.PullRequest,
                options.GitHubRepository,
                options.FetchMissing,
                cancellationToken).ConfigureAwait(false);
        }

        return await _gitPlanner.PlanBaseHeadAsync(
            options.RepositoryPath,
            options.BaseRevision!,
            options.HeadRevision!,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> WriteOutputAsync(
        string content,
        string? outputPath,
        string artifactName,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (outputPath is null)
        {
            await standardOutput.WriteLineAsync(
                content.TrimEnd().AsMemory(),
                cancellationToken).ConfigureAwait(false);
            return CliExitCodes.Success;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fullOutputPath = Path.GetFullPath(outputPath);
            string? directory = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(
                fullOutputPath,
                content.TrimEnd() + Environment.NewLine,
                new UTF8Encoding(false),
                cancellationToken).ConfigureAwait(false);
            return CliExitCodes.Success;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await standardError.WriteLineAsync($"Could not write {artifactName}: {exception.Message}")
                .ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }
    }

    private static async Task<int> UsageErrorAsync(TextWriter standardError, string message)
    {
        await standardError.WriteLineAsync($"eh: {message}").ConfigureAwait(false);
        await standardError.WriteLineAsync("Run 'eh change --help' for usage.").ConfigureAwait(false);
        return CliExitCodes.UsageError;
    }

    private static bool IsHelp(string value) => value is "help" or "--help" or "-h";

    private const string HelpText = """
        Usage:
          eh change <repository> --commit <revision> [--parent <revision>] [options]
          eh change --repo <owner/name> --commit <revision> [--parent <revision>] [options]
          eh change <repository> --range <base>..<head> [options]
          eh change --repo <owner/name> --range <base>..<head> [options]
          eh change <repository> --base <revision> --head <revision> [options]
          eh change --repo <owner/name> --base <revision> --head <revision> [options]
          eh change <repository> --pr <number-or-url> [--repo <owner/name>] [options]
          eh change --pr <github-pr-url> [--fetch-missing] [options]
          eh change --pr <number> --repo <owner/name> [--fetch-missing] [options]
          eh change --base-path <directory> --head-path <directory> [options]
          eh change --base-evidence <evidence.json> --head-evidence <evidence.json> [options]
          eh change explain <change-estimate.json> --item <id> [options]
          eh change portfolio <repository> --pr <pr> --pr <pr> [options]
          eh change portfolio --manifest <portfolio.json> [options]
          eh change portfolio <repository> --author <alias> --since <instant> --until <instant> [options]
          eh change today --owner <owner> --author @me --timezone <zone>
            --include-open-prs --scope engineering --capacity-hours <hours> [options]
          eh change period --owner <owner> --author <identity> --period <named-period> [options]
          eh change compare-team --owner <owner> --contributors-from <owner/repository> [options]
          eh change scope show engineering

        Selectors:
          --commit <revision>  Compare one commit with its first parent; root uses the empty tree
          --parent <revision>  Required choice for a merge commit
          --range <base>..<head>
                               Compare the coherent final range and reconcile isolated commits
          --base <revision>    Explicit final base revision (requires --head)
          --head <revision>    Explicit final head revision (requires --base)
          --pr <number-or-url> Resolve one PR and compare its unique merge base/head
          --repo <owner/name>  Explicit GitHub repository for any checkout-free Git selector
          --fetch-missing      Explicitly resolve and acquire missing selected objects without updating refs;
                               checkout-free mode uses the private managed bare cache
          --base-path <path>   Statically scan one local base directory (requires --head-path)
          --head-path <path>   Statically scan one local head directory (requires --base-path)
          --base-evidence <path>
                               Load one saved repository-evidence base snapshot
          --head-evidence <path>
                               Load one saved repository-evidence head snapshot

        Output:
          --profile <implementation|recreation>  Estimation profile (default: implementation)
          --format <json|markdown>                Output format (default: json)
          --compact                               Emit compact JSON
          --hourly-rate <number>                  Override the bundled 2026 US rate
          --currency <code>                       Currency for an overridden rate (default: USD)
          --no-rate                               Omit rate and cost projection
          --output <path>                         Write output to an explicit path instead of stdout
          -h, --help                              Show this help

        Directory and evidence pairs work without Git or GitHub. Directory inputs are
        statically scanned and content-pinned; saved evidence has no source bodies, so
        modified maintained paths retain conservative normalization. Git mode reads
        immutable objects directly and does not check out. It does not fetch by default;
        --fetch-missing explicitly allows provider-backed Git mode to resolve immutable
        commits and acquire only those selected objects without updating local refs,
        FETCH_HEAD, the index, or a worktree. A full GitHub PR URL, or any Git selector
        with --repo, can run without a checkout through the private EffortHours bare
        cache. Without --fetch-missing, checkout-free mode performs no provider or network
        access and succeeds only from complete immutable resolution and object caches.
        No selector executes target code.
        The current change model is experimental and uncalibrated.
        """;
}
