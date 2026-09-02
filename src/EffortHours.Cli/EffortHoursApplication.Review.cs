namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private async Task<int> ReviewAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(ReviewHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        return arguments[0].ToLowerInvariant() switch
        {
            "packet" => await ReviewPacketAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            "query" => await ReviewQueryAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            "validate" => await ReviewValidateAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            "measure" => await ReviewMeasureAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            "benchmark" => await ReviewBenchmarkAsync(
                [.. arguments.Skip(1)],
                standardOutput,
                standardError,
                cancellationToken).ConfigureAwait(false),
            _ => await UsageErrorAsync(
                standardError,
                $"Unknown review command '{arguments[0]}'.").ConfigureAwait(false),
        };
    }

    private const string ReviewHelpText = """
        Usage:
          eh review packet <repository-or-evidence.json> [options]
          eh review packet --repo <owner/name> [--revision <revision>] [options]
          eh review query <repository-or-evidence.json> --input-digest <digest> [selector] [options]
          eh review query --repo <owner/name> [--revision <revision>] --input-digest <digest> [selector] [options]
          eh review validate <packet.json> <adjustment.json> [options]
          eh review measure <packet.json> <adjustment.json> --subject <id> --session <id> --context <mode> [options]
          eh review benchmark <measurement.json>... [options]

        Commands:
          packet    Build a compact provider-neutral uncertainty-review packet
          query     Answer one digest-bound capability, evidence, scope, or source query
          validate  Validate a proposed adjustment ledger without applying it
          measure   Record a sanitized, provider-neutral completed review session
          benchmark Compare compact and broader-source sessions by opaque subject

        Run 'eh review <command> --help' for command-specific options.
        EffortHours does not call an AI provider or transmit repository material.
        """;
}
