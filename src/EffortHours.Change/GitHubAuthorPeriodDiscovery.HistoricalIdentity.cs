using System.Globalization;
using System.Text.Json;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class GitHubAuthorPeriodDiscoveryJson
{
    internal static async Task<string[]> ResolveHistoricalIdentityAsync(
        IExternalCommandRunner commands, string workingDirectory, string repository, string objectId,
        string login, ProviderQueryCounters counters, CancellationToken token)
    {
        HashSet<string> emails = new(StringComparer.OrdinalIgnoreCase);
        long chargedCharacters = 0;
        for (int page = 1; ; page++)
        {
            string endpoint = $"repos/{repository}/commits?sha={objectId}&author={Uri.EscapeDataString(login)}&per_page=100&page=" +
                page.ToString(CultureInfo.InvariantCulture);
            string json = await RunRequiredApiAsync(commands, workingDirectory, ["api", endpoint],
                counters, paginated: false, token).ConfigureAwait(false);
            chargedCharacters += json.Length;
            if (chargedCharacters > MaximumResponseCharacters)
                throw GitHubProviderFailure.DiscoveryBudget(GitHubProviderFailure.DefaultHeadPhase,
                    "Historical identity association exceeded its 16-MiB response charge. Supply exact Git email aliases or narrow scope; no incomplete identity selection is a zero result.");
            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;
                JsonElement[] values = root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0 &&
                    root[0].ValueKind == JsonValueKind.Array ? [.. Pages(root)] : [.. root.EnumerateArray()];
                if (values.Length > 100) throw new JsonException("Identity page exceeded its requested row bound.");
                foreach (JsonElement value in values)
                {
                    GitCommitMetadata commit = ParseCommit(value);
                    string? observedLogin = ProviderAuthorLogin(value);
                    if (login.Equals(observedLogin, StringComparison.OrdinalIgnoreCase))
                    {
                        counters.ObserveIdentity(observedLogin, commit);
                        emails.Add(commit.Author.Email.ToLowerInvariant());
                        if (emails.Count > ChangeAuthorPeriodManifestLimits.MaximumAliasesPerContributor)
                            throw new InvalidOperationException("The selected provider identity exceeds the alias bound.");
                    }
                }
                if (values.Length < 100) return [.. emails.Order(StringComparer.OrdinalIgnoreCase)];
            }
            catch (Exception exception) when (exception is JsonException or KeyNotFoundException or FormatException)
            {
                throw GitHubProviderFailure.Malformed(GitHubProviderFailure.DefaultHeadPhase, exception);
            }
        }
    }
}
