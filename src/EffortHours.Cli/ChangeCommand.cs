using System.Text;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Pricing;
using EffortHours.Reporting;

namespace EffortHours.Cli;

internal sealed class ChangeCommand
{
    private readonly ChangeEstimator _estimator;
    private readonly GitChangePlanner _planner;

    public ChangeCommand()
        : this(new ChangeEstimator(), new GitChangePlanner())
    {
    }

    public ChangeCommand(ChangeEstimator estimator, GitChangePlanner planner)
    {
        _estimator = estimator ?? throw new ArgumentNullException(nameof(estimator));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
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

        GitChangePlan plan;
        try
        {
            plan = options.Commit is not null
                ? await _planner.PlanCommitAsync(
                    options.RepositoryPath,
                    options.Commit,
                    options.Parent,
                    cancellationToken)
                    .ConfigureAwait(false)
                : options.Range is not null
                    ? await _planner.PlanRangeAsync(
                        options.RepositoryPath,
                        options.Range,
                        cancellationToken)
                        .ConfigureAwait(false)
                    : options.PullRequest is not null
                        ? await _planner.PlanPullRequestAsync(
                            options.RepositoryPath,
                            options.PullRequest,
                            options.GitHubRepository,
                            cancellationToken).ConfigureAwait(false)
                        : await _planner.PlanBaseHeadAsync(
                            options.RepositoryPath,
                            options.BaseRevision!,
                            options.HeadRevision!,
                            cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is ArgumentException or DirectoryNotFoundException or ExternalCommandException or
            InvalidOperationException)
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
            report = await _estimator.EstimateAsync(
                plan,
                options.Profile,
                rateCard,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is ArgumentException or DirectoryNotFoundException or ExternalCommandException or
            InvalidOperationException)
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
          eh change <repository> --range <base>..<head> [options]
          eh change <repository> --base <revision> --head <revision> [options]
          eh change <repository> --pr <number-or-url> [--repo <owner/name>] [options]
          eh change explain <change-estimate.json> --item <id> [options]

        Selectors:
          --commit <revision>  Compare one commit with its first parent; root uses the empty tree
          --parent <revision>  Required choice for a merge commit
          --range <base>..<head>
                               Compare the coherent final range and reconcile isolated commits
          --base <revision>    Explicit final base revision (requires --head)
          --head <revision>    Explicit final head revision (requires --base)
          --pr <number-or-url> Resolve one PR's immutable base/head through optional gh support
          --repo <owner/name>  Explicit GitHub repository for --pr

        Output:
          --profile <implementation|recreation>  Estimation profile (default: implementation)
          --format <json|markdown>                Output format (default: json)
          --compact                               Emit compact JSON
          --hourly-rate <number>                  Override the bundled 2026 US rate
          --currency <code>                       Currency for an overridden rate (default: USD)
          --no-rate                               Omit rate and cost projection
          --output <path>                         Write output to an explicit path instead of stdout
          -h, --help                              Show this help

        Change mode reads immutable Git objects directly and does not check out, fetch,
        execute, or write into the selected repository. Normalized effort values the final
        artifact delta; commit activity and intermediate churn are not effort multipliers.
        The current change model is experimental and uncalibrated.
        """;
}
