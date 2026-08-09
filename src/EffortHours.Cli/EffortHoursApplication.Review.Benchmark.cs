using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Review;

namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private static async Task<int> ReviewBenchmarkAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || arguments.Any(IsHelp))
        {
            await standardOutput.WriteLineAsync(ReviewBenchmarkHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        List<string> paths = [];
        bool compact = false;
        string? outputPath = null;
        for (int index = 0; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case "--compact":
                    compact = true;
                    break;
                case "--output" when index + 1 < arguments.Length:
                    outputPath = arguments[++index];
                    break;
                case "--output":
                    return await UsageErrorAsync(
                        standardError,
                        "Option '--output' requires a value.").ConfigureAwait(false);
                default:
                    if (arguments[index].StartsWith('-'))
                    {
                        return await UsageErrorAsync(
                            standardError,
                            $"Unknown review benchmark option '{arguments[index]}'.")
                            .ConfigureAwait(false);
                    }

                    paths.Add(arguments[index]);
                    break;
            }
        }

        if (paths.Count < 2)
        {
            return await UsageErrorAsync(
                standardError,
                "Review benchmark requires at least one compact/broader-source measurement pair.")
                .ConfigureAwait(false);
        }

        List<HostReviewMeasurement> measurements = [];
        foreach (string path in paths)
        {
            ReviewDocument<HostReviewMeasurement>? document = await ReadReviewDocumentAsync<HostReviewMeasurement>(
                path,
                "measurement",
                SchemaNames.HostReviewMeasurement,
                ContractValidation.Validate,
                standardError,
                cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                return CliExitCodes.InvalidInput;
            }

            measurements.Add(document.Value);
        }

        HostReviewBenchmarkReport report;
        try
        {
            report = HostReviewBenchmarkEngine.Build(measurements, cancellationToken);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            await standardError.WriteLineAsync(
                $"eh: could not create host-review benchmark: {exception.Message}")
                .ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        string output = new HostReviewJsonRenderer(compact).Render(report);
        return await WriteCliOutputAsync(
            output,
            outputPath,
            "host-review benchmark",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private const string ReviewBenchmarkHelpText = """
        Usage:
          eh review benchmark <measurement.json>... [options]

        Options:
          --compact        Emit compact JSON
          --output <path>  Write to an explicit path instead of stdout
          -h, --help       Show this help

        Each opaque subject requires exactly one compact and one broader-source
        measurement over the same packet. The broader-source result is a comparison
        reference, not ground truth. No default AI budget is selected.
        """;
}
