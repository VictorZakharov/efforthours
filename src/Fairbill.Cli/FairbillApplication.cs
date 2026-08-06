using System.Globalization;
using System.Reflection;
using System.Text;
using Fairbill.Analysis;
using Fairbill.Calibration;
using Fairbill.Contracts;
using Fairbill.Contracts.V1;
using Fairbill.Core;
using Fairbill.Estimation;
using Fairbill.Pricing;
using Fairbill.Reporting;

namespace Fairbill.Cli;

public sealed partial class FairbillApplication
{
    private readonly IEstimator _estimator;
    private readonly IRepositoryScanner _scanner;

    public FairbillApplication()
        : this(new SeedEstimator(), new RepositoryAnalysisPipeline())
    {
    }

    public FairbillApplication(IEstimator estimator)
        : this(estimator, new RepositoryAnalysisPipeline())
    {
    }

    public FairbillApplication(IEstimator estimator, IRepositoryScanner scanner)
    {
        _estimator = estimator ?? throw new ArgumentNullException(nameof(estimator));
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
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
                "version" or "--version" or "-v" => await VersionAsync(standardOutput).ConfigureAwait(false),
                _ => await UsageErrorAsync(
                    standardError,
                    $"Unknown command '{arguments[0]}'.").ConfigureAwait(false),
            };
        }
        catch (OperationCanceledException)
        {
            await standardError.WriteLineAsync("The operation was cancelled.").ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            await standardError.WriteLineAsync($"fairbill: {exception.Message}").ConfigureAwait(false);
            return CliExitCodes.InternalError;
        }
        catch (Exception exception)
        {
            await standardError.WriteLineAsync(
                $"fairbill: an unexpected internal error occurred: {exception.Message}").ConfigureAwait(false);
            return CliExitCodes.InternalError;
        }
    }

}
