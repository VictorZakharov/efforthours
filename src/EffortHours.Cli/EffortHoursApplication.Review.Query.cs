using System.Globalization;
using EffortHours.Analysis;
using EffortHours.Contracts.V1;
using EffortHours.Review;

namespace EffortHours.Cli;

public sealed partial class EffortHoursApplication
{
    private async Task<int> ReviewQueryAsync(
        string[] arguments,
        TextWriter standardOutput,
        TextWriter standardError,
        CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || IsHelp(arguments[0]))
        {
            await standardOutput.WriteLineAsync(ReviewQueryHelpText).ConfigureAwait(false);
            return arguments.Length == 0 ? CliExitCodes.UsageError : CliExitCodes.Success;
        }

        RepositoryInputOptionsBuilder inputOptions = new(arguments);
        EstimationProfile profile = EstimationProfile.Implementation;
        string? inputDigest = null;
        string? reason = null;
        string? outputPath = null;
        HostReviewQueryKind? queryKind = null;
        string? selector = null;
        int? offset = null;
        int? limit = null;
        int? startLine = null;
        int? lineCount = null;
        bool compact = false;

        for (int index = inputOptions.FirstOptionIndex; index < arguments.Length; index++)
        {
            string option = arguments[index];
            if (IsHelp(option))
            {
                await standardOutput.WriteLineAsync(ReviewQueryHelpText).ConfigureAwait(false);
                return CliExitCodes.Success;
            }

            if (inputOptions.TryConsume(arguments, ref index, out string? inputError))
            {
                if (inputError is not null)
                {
                    return await UsageErrorAsync(standardError, inputError).ConfigureAwait(false);
                }

                continue;
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
                case "--input-digest":
                    inputDigest = value;
                    break;
                case "--reason":
                    reason = value;
                    break;
                case "--capability":
                    if (!TrySetQuerySelector(
                        HostReviewQueryKind.Capability,
                        value,
                        ref queryKind,
                        ref selector))
                    {
                        return await MultipleReviewSelectorsAsync(standardError).ConfigureAwait(false);
                    }

                    break;
                case "--evidence":
                    if (!TrySetQuerySelector(
                        HostReviewQueryKind.Evidence,
                        value,
                        ref queryKind,
                        ref selector))
                    {
                        return await MultipleReviewSelectorsAsync(standardError).ConfigureAwait(false);
                    }

                    break;
                case "--scope":
                    if (!TrySetQuerySelector(
                        HostReviewQueryKind.Scope,
                        value,
                        ref queryKind,
                        ref selector))
                    {
                        return await MultipleReviewSelectorsAsync(standardError).ConfigureAwait(false);
                    }

                    break;
                case "--source":
                    if (!TrySetQuerySelector(
                        HostReviewQueryKind.SelectedSource,
                        value,
                        ref queryKind,
                        ref selector))
                    {
                        return await MultipleReviewSelectorsAsync(standardError).ConfigureAwait(false);
                    }

                    break;
                case "--offset":
                    if (!TryParseNonNegativeInteger(value, out int parsedOffset))
                    {
                        return await UsageErrorAsync(
                            standardError,
                            "Offset must be a non-negative integer.").ConfigureAwait(false);
                    }

                    offset = parsedOffset;
                    break;
                case "--limit":
                    if (!TryParsePositiveInteger(value, out int parsedLimit))
                    {
                        return await UsageErrorAsync(
                            standardError,
                            "Limit must be a positive integer.").ConfigureAwait(false);
                    }

                    limit = parsedLimit;
                    break;
                case "--start-line":
                    if (!TryParsePositiveInteger(value, out int parsedStartLine))
                    {
                        return await UsageErrorAsync(
                            standardError,
                            "Start line must be a positive integer.").ConfigureAwait(false);
                    }

                    startLine = parsedStartLine;
                    break;
                case "--line-count":
                    if (!TryParsePositiveInteger(value, out int parsedLineCount))
                    {
                        return await UsageErrorAsync(
                            standardError,
                            "Line count must be a positive integer.").ConfigureAwait(false);
                    }

                    lineCount = parsedLineCount;
                    break;
                case "--output":
                    outputPath = value;
                    break;
                default:
                    return await UsageErrorAsync(
                        standardError,
                        $"Unknown review query option '{option}'.").ConfigureAwait(false);
            }
        }

        if (queryKind is null || string.IsNullOrWhiteSpace(selector))
        {
            return await UsageErrorAsync(
                standardError,
                "Exactly one of '--capability', '--evidence', '--scope', or '--source' is required.")
                .ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(inputDigest) || string.IsNullOrWhiteSpace(reason))
        {
            return await UsageErrorAsync(
                standardError,
                "Options '--input-digest' and '--reason' are required.").ConfigureAwait(false);
        }

        HostReviewQuery query = new()
        {
            Kind = queryKind.Value,
            Selector = selector,
            Reason = reason,
            Profile = profile,
            InputDigest = inputDigest,
            Offset = queryKind == HostReviewQueryKind.Scope ? offset ?? 0 : offset,
            Limit = queryKind == HostReviewQueryKind.Scope
                ? limit ?? HostReviewProtocol.MaximumScopeCapabilities
                : limit,
            StartLine = queryKind == HostReviewQueryKind.SelectedSource ? startLine ?? 1 : startLine,
            LineCount = queryKind == HostReviewQueryKind.SelectedSource ? lineCount ?? 80 : lineCount,
        };

        RepositoryInputSelection? selection = await BuildRepositoryInputAsync(
            inputOptions,
            standardError).ConfigureAwait(false);
        if (selection is null)
        {
            return CliExitCodes.UsageError;
        }

        await using RepositoryInputContext? input = await LoadRepositoryInputAsync(
            selection,
            allowEvidenceFile: true,
            scanOptions: null,
            standardError,
            cancellationToken).ConfigureAwait(false);
        if (input is null)
        {
            return CliExitCodes.InvalidInput;
        }

        RepositoryEvidence evidence = input.Evidence;

        EstimateReport report = _estimator.Estimate(evidence, profile, rateCard: null);
        HostReviewSourceContext? sourceContext = query.Kind == HostReviewQueryKind.SelectedSource
            ? input.SourceContext
            : null;

        HostReviewQueryResult result;
        try
        {
            result = await HostReviewQueryEngine.ExecuteAsync(
                report,
                evidence,
                query,
                sourceContext,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is
            ArgumentException or
            KeyNotFoundException or
            FileNotFoundException or
            IOException or
            UnauthorizedAccessException or
            InvalidOperationException)
        {
            await standardError.WriteLineAsync($"eh: {exception.Message}").ConfigureAwait(false);
            return CliExitCodes.InvalidInput;
        }

        string output = new HostReviewJsonRenderer(compact).Render(result);
        return await WriteCliOutputAsync(
            output,
            outputPath,
            "host-review query result",
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);
    }

    private static bool TrySetQuerySelector(
        HostReviewQueryKind kind,
        string value,
        ref HostReviewQueryKind? selectedKind,
        ref string? selector)
    {
        if (selectedKind is not null)
        {
            return false;
        }

        selectedKind = kind;
        selector = value;
        return true;
    }

    private static Task<int> MultipleReviewSelectorsAsync(TextWriter standardError) =>
        UsageErrorAsync(
            standardError,
            "Only one of '--capability', '--evidence', '--scope', or '--source' can be used.");

    private static bool TryParseNonNegativeInteger(string value, out int number) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number) &&
        number >= 0;

    private static bool TryParsePositiveInteger(string value, out int number) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number) &&
        number > 0;

    private const string ReviewQueryHelpText = """
        Usage:
          eh review query <repository-or-evidence.json> --input-digest <digest>
            (--capability <id> | --evidence <id> | --scope <scope> | --source <path>)
            --reason <text> [options]
          eh review query --repo <owner/name> [--revision <revision>]
            [--fetch-missing] --input-digest <digest> [selector] --reason <text> [options]

        Options:
          --repo <owner/name>                     Analyze an immutable GitHub snapshot without checkout
          --revision <value>                      Git revision for --repo (default: HEAD)
          --fetch-missing                         Resolve/fetch missing objects into the private cache
          --profile <implementation|recreation>  Estimation profile (default: implementation)
          --input-digest <sha256:digest>          Exact digest from the review packet
          --capability <id>                       Expand one capability and its evidence
          --evidence <id>                         Return one evidence fact
          --scope <scope>                         Return one page of capability summaries
          --source <path>                         Return explicit admitted source lines
          --reason <text>                         Record why the detail is needed
          --offset <number>                       Scope offset (default: 0)
          --limit <number>                        Scope page size (default/max: 20)
          --start-line <number>                   Source start line (default: 1)
          --line-count <number>                   Source line count (default: 80; max: 200)
          --compact                               Emit compact JSON
          --output <path>                         Write to an explicit path instead of stdout
          -h, --help                             Show this help

        Selected source is returned only for an admitted file in the selected repository snapshot.
        """;
}
