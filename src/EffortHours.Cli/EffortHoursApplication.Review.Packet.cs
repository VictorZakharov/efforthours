using EffortHours.Contracts.V1;
using EffortHours.Review;

namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private async Task<int> ReviewPacketAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(ReviewPacketHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        string inputPath = arguments[0];
        EstimationProfile profile = EstimationProfile.Implementation;
        string? model = null;
        string? provider = null;
        string? modelVersion = null;
        string? outputPath = null;
        bool compact = false;

        for (int index = 1; index < arguments.Length; index++)
        {
            string option = arguments[index];
            if (IsHelp(option))
            {
                await standardOutput.WriteLineAsync(ReviewPacketHelpText).ConfigureAwait(false);
                return CliExitCodes.Success;
            }

            if (option == "--compact")
            {
                compact = true;
                continue;
            }

            if (index + 1 >= arguments.Length)
            {
                return await UsageErrorAsync(
                    standardError,
                    $"Option '{option}' requires a value.").ConfigureAwait(false);
            }

            string value = arguments[++index];
            switch (option)
            {
                case "--profile":
                    if (!TryParseProfile(value, out profile))
                    {
                        return await UsageErrorAsync(
                            standardError,
                            "Profile must be 'implementation' or 'recreation'.").ConfigureAwait(false);
                    }

                    break;
                case "--model":
                    model = value;
                    break;
                case "--provider":
                    provider = value;
                    break;
                case "--model-version":
                    modelVersion = value;
                    break;
                case "--output":
                    outputPath = value;
                    break;
                default:
                    return await UsageErrorAsync(
                        standardError,
                        $"Unknown review packet option '{option}'.").ConfigureAwait(false);
            }
        }

        if (string.IsNullOrWhiteSpace(model) &&
            (!string.IsNullOrWhiteSpace(provider) || !string.IsNullOrWhiteSpace(modelVersion)))
        {
            return await UsageErrorAsync(
                standardError,
                "Options '--provider' and '--model-version' require '--model'.")
                .ConfigureAwait(false);
        }

        if (model is not null && string.IsNullOrWhiteSpace(model))
        {
            return await UsageErrorAsync(
                standardError,
                "Option '--model' cannot be empty.").ConfigureAwait(false);
        }

        if (provider is not null && string.IsNullOrWhiteSpace(provider) ||
            modelVersion is not null && string.IsNullOrWhiteSpace(modelVersion))
        {
            return await UsageErrorAsync(
                standardError,
                "Options '--provider' and '--model-version' cannot be empty.")
                .ConfigureAwait(false);
        }

        RepositoryEvidence? evidence = await LoadEvidenceAsync(
            inputPath,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (evidence is null)
        {
            return CliExitCodes.InvalidInput;
        }

        EstimateReport report = _estimator.Estimate(evidence, profile, rateCard: null);
        HostReviewPacket packet = HostReviewPacketBuilder.Build(
            report,
            evidence,
            new HostReviewModelIdentity
            {
                IsAvailable = model is not null,
                Provider = provider,
                Model = model,
                Version = modelVersion,
            },
            cancellationToken);
        string output = new HostReviewJsonRenderer(compact).Render(packet);
        return await WriteCliOutputAsync(
            output,
            outputPath,
            "host-review packet",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private const string ReviewPacketHelpText = """
        Usage:
          eh review packet <repository-or-evidence.json> [options]

        Options:
          --profile <implementation|recreation>  Estimation profile (default: implementation)
          --model <id>                           Record an available host model identity
          --provider <id>                        Optional provider for the recorded model
          --model-version <id>                   Optional version for the recorded model
          --compact                              Emit compact JSON
          --output <path>                        Write to an explicit path instead of stdout
          -h, --help                             Show this help

        The packet contains no pricing or source excerpts and does not call a provider.
        """;
}
