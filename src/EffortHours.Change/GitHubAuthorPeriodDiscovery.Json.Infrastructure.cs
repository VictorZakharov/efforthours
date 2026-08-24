using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EffortHours.Change;

internal static partial class GitHubAuthorPeriodDiscoveryJson
{
    private static async Task<string?> RunApiAsync(
        IExternalCommandRunner commands,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        ProviderQueryCounters counters,
        bool paginated,
        bool optional,
        CancellationToken cancellationToken,
        bool emptyRepositoryIsEmpty = false)
    {
        counters.AddQuery();
        ExternalCommandResult result;
        try
        {
            result = await commands.RunAsync(
                "gh",
                workingDirectory,
                arguments,
                cancellationToken,
                requireSuccess: !optional && !emptyRepositoryIsEmpty).ConfigureAwait(false);
        }
        catch (ExternalCommandException exception)
        {
            throw new InvalidOperationException(
                "GitHub discovery failed. Confirm that gh is installed, authenticated, and authorized " +
                "for the requested owner scope.",
                exception);
        }

        if (result.ExitCode != 0 && emptyRepositoryIsEmpty && IsEmptyRepository(result))
        {
            counters.AddPages(1);
            return paginated ? "[[]]" : "{}";
        }

        if (optional && result.ExitCode != 0)
        {
            return null;
        }

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "GitHub discovery failed. Confirm that gh is installed, authenticated, and authorized " +
                "for the requested owner scope.");
        }

        if (result.StandardOutput.Length > MaximumResponseCharacters)
        {
            throw new InvalidOperationException(
                "GitHub discovery response exceeded the bounded adapter input size.");
        }

        if (paginated)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    throw new JsonException();
                }

                counters.AddPages(document.RootElement.GetArrayLength());
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException(
                    "GitHub returned malformed paginated JSON.",
                    exception);
            }
        }
        else
        {
            counters.AddPages(1);
        }

        return result.StandardOutput;
    }

    private static async Task<string> RunRequiredApiAsync(
        IExternalCommandRunner commands,
        string workingDirectory,
        IReadOnlyList<string> arguments,
        ProviderQueryCounters counters,
        bool paginated,
        CancellationToken cancellationToken,
        bool emptyRepositoryIsEmpty = false) =>
        await RunApiAsync(
            commands,
            workingDirectory,
            arguments,
            counters,
            paginated,
            optional: false,
            cancellationToken,
            emptyRepositoryIsEmpty).ConfigureAwait(false) ??
        throw new InvalidOperationException("GitHub discovery returned no response.");

    private static bool IsEmptyRepository(ExternalCommandResult result)
    {
        string response = result.StandardOutput + "\n" + result.StandardError;
        return response.Contains("Git Repository is empty.", StringComparison.OrdinalIgnoreCase) &&
            (response.Contains("HTTP 409", StringComparison.OrdinalIgnoreCase) ||
             response.Contains("\"status\":\"409\"", StringComparison.OrdinalIgnoreCase) ||
             response.Contains("\"status\":409", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<JsonElement> Pages(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Paginated GitHub output must be an array of pages.");
        }

        foreach (JsonElement page in root.EnumerateArray())
        {
            if (page.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException("Each paginated GitHub page must be an array.");
            }

            foreach (JsonElement item in page.EnumerateArray())
            {
                yield return item;
            }
        }
    }

    private static string RequireObjectId(string? value, string subject)
    {
        string objectId = value?.ToLowerInvariant() ?? string.Empty;
        if (objectId.Length is not (40 or 64) || objectId.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidOperationException($"GitHub returned an invalid {subject} object ID.");
        }

        return objectId;
    }

    private static string RequireRepositoryIdentity(string? value)
    {
        string identity = value?.Trim() ?? string.Empty;
        string[] parts = identity.Split('/');
        if (parts.Length != 2 || parts.Any(part => string.IsNullOrWhiteSpace(part)) ||
            parts.Any(part => part.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.')))
        {
            throw new InvalidOperationException("GitHub returned an invalid repository identity.");
        }

        return identity;
    }

    private static string OpaqueId(string prefix, string value)
    {
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
        return prefix + "-" + digest[..20];
    }
}
