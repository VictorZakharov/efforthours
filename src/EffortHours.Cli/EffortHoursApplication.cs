using System.Globalization;
using System.Reflection;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;
using EffortHours.Pricing;
using EffortHours.Reporting;
using EffortHours.Review;

namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private readonly IEstimator _estimator;
    private readonly RepositoryInputLoader _repositoryInputs;

    public EffortHoursApplication()
        : this(new SeedEstimator(), new RepositoryAnalysisPipeline())
    {
    }

    public EffortHoursApplication(IEstimator estimator)
        : this(estimator, new RepositoryAnalysisPipeline())
    {
    }

    public EffortHoursApplication(IEstimator estimator, IRepositoryScanner scanner)
        : this(estimator, scanner, new RepositoryInputLoader(scanner))
    {
    }

    internal EffortHoursApplication(
        IEstimator estimator,
        IRepositoryScanner scanner,
        RepositoryInputLoader repositoryInputs)
    {
        _estimator = estimator ?? throw new ArgumentNullException(nameof(estimator));
        ArgumentNullException.ThrowIfNull(scanner);
        _repositoryInputs = repositoryInputs ?? throw new ArgumentNullException(nameof(repositoryInputs));
    }

    public async Task<int> RunAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(standardOutput);
        ArgumentNullException.ThrowIfNull(standardError);

        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(HelpText).ConfigureAwait(false);
            return CliExitCodes.Success;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return arguments[0].ToLowerInvariant() switch
            {
                "scan" => await ScanAsync(
                    [.. arguments.Skip(1)],
                    standardOutput,
                    standardError,
                    cancellationToken).ConfigureAwait(false),
                "estimate" => await EstimateAsync(
                    [.. arguments.Skip(1)],
                    standardOutput,
                    standardError,
                    cancellationToken).ConfigureAwait(false),
                "change" => await new ChangeCommand().ExecuteAsync(
                    [.. arguments.Skip(1)],
                    standardOutput,
                    standardError,
                    cancellationToken).ConfigureAwait(false),
                "report" => await ReportAsync(
                    [.. arguments.Skip(1)],
                    standardOutput,
                    standardError,
                    cancellationToken).ConfigureAwait(false),
                "explain" => await ExplainAsync(
                    [.. arguments.Skip(1)],
                    standardOutput,
                    standardError,
                    cancellationToken).ConfigureAwait(false),
                "review" => await ReviewAsync(
                    [.. arguments.Skip(1)],
                    standardOutput,
                    standardError,
                    cancellationToken).ConfigureAwait(false),
                "calibration" => await CalibrationAsync(
                    [.. arguments.Skip(1)],
                    standardOutput,
                    standardError,
                    cancellationToken).ConfigureAwait(false),
                "schema" => await SchemaAsync(
                    [.. arguments.Skip(1)],
                    standardOutput,
                    standardError).ConfigureAwait(false),
                "model" => await ModelAsync(
                    [.. arguments.Skip(1)],
                    standardOutput,
                    standardError).ConfigureAwait(false),
                "rate" => await RateAsync(
                    [.. arguments.Skip(1)],
                    standardOutput,
                    standardError).ConfigureAwait(false),
                "agent" => await AgentAsync(
                    [.. arguments.Skip(1)],
                    standardOutput,
                    standardError,
                    cancellationToken).ConfigureAwait(false),
                "version" or "--version" or "-v" => await VersionAsync(standardOutput).ConfigureAwait(false),
                _ => await UsageErrorAsync(
                    standardError,
                    $"Unknown command '{arguments[0]}'.").ConfigureAwait(false),
            };
        }
        catch (OperationCanceledException)
        {
            await standardError.WriteLineAsync("The operation was cancelled.").ConfigureAwait(false);
            return CliExitCodes.Cancelled;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await standardError.WriteLineAsync($"eh: {exception.Message}").ConfigureAwait(false);
            return CliExitCodes.InternalError;
        }
        catch (Exception exception)
        {
            await standardError.WriteLineAsync(
                $"eh: an unexpected internal error occurred: {exception.Message}").ConfigureAwait(false);
            return CliExitCodes.InternalError;
        }
    }

}
