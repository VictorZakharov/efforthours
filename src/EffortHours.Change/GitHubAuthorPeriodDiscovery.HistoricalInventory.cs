using System.Globalization;
using System.Text.Json;

namespace EffortHours.Change;

internal static partial class GitHubAuthorPeriodDiscoveryJson
{
    private sealed class HistoricalPullInventory(string repository)
    {
        public const int MaximumPageCharacters = 1048576;
        private const int MaximumLedgerBytes = 16 * 1024 * 1024;
        private readonly HashSet<int> _numbers = [];
        private readonly List<AccountPullRequest> _authored = [];
        private int _charged;

        public bool Observe(int number)
        {
            if (number <= 0 || !_numbers.Add(number)) return false;
            Charge(64);
            return true;
        }

        public void Add(AccountPullRequest pull)
        {
            if (_authored.Count == 1000)
                throw Budget("authored-candidate", 1000, 1001);
            Charge(512 + 2 * repository.Length);
            _authored.Add(pull);
        }

        private void Charge(int bytes)
        {
            _charged += bytes;
            if (_charged > MaximumLedgerBytes) throw Budget("inventory-ledger-byte", MaximumLedgerBytes, _charged);
        }

        public AccountPullRequest[] Complete() => [.. _authored.OrderBy(pull => pull.Number)];

        private static GitHubProviderException Budget(string resource, int limit, int observed) =>
            GitHubProviderFailure.DiscoveryBudget(GitHubProviderFailure.OpenPullRequestPhase,
                $"Historical pull-inventory {resource} bound exceeded: limit {limit}, observed {observed}. " +
                "Resume with the same scope/checkpoint after correction or supply a complete pinned manifest; narrower dates do not preserve coverage. No selection was truncated.",
                "inspect-pr-discovery-or-use-pinned-manifest");
    }

    private static async Task<AccountPullRequest[]> ReadHistoricalRestInventoryAsync(IExternalCommandRunner commands,
        string directory, string identity, IReadOnlyList<string> logins, string viewer, bool includeViewer,
        ProviderQueryCounters counters, CancellationToken token)
    {
        await HistoricalPullGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await ReadPages().ConfigureAwait(false);
        }
        finally { HistoricalPullGate.Release(); }

        async Task<AccountPullRequest[]> ReadPages()
        {
            HistoricalPullInventory inventory = new(identity);
            for (int page = 1; ; page++)
            {
                string endpoint = $"repos/{identity}/pulls?state=all&sort=created&direction=asc&per_page=100&page={page.ToString(CultureInfo.InvariantCulture)}";
                // gh evaluates this per response page; no PR body, dates or repeated repository descriptors cross the pipe.
                string json = await RunApiAsync(commands, directory, ["api", endpoint, "--jq",
                "[.[] | {number,state,user:{login:.user.login},head:{sha:.head.sha}}]"], counters, false, false, token,
                    failurePhase: GitHubProviderFailure.OpenPullRequestPhase,
                    maximumResponseCharacters: HistoricalPullInventory.MaximumPageCharacters).ConfigureAwait(false)
                    ?? throw new InvalidOperationException("Missing historical inventory page.");
                try
                {
                    using JsonDocument document = JsonDocument.Parse(json);
                    JsonElement rows = document.RootElement;
                    if (rows.ValueKind != JsonValueKind.Array || rows.GetArrayLength() > 100) throw new JsonException();
                    foreach (JsonElement pull in rows.EnumerateArray())
                    {
                        if (!inventory.Observe(pull.GetProperty("number").GetInt32())) throw new JsonException();
                        if (!PullAuthorMatches(pull, logins, viewer, includeViewer)) continue;
                        string state = pull.GetProperty("state").GetString() ?? "";
                        if (state is not ("open" or "closed")) throw new JsonException();
                        inventory.Add(new(identity, pull.GetProperty("number").GetInt32(), state == "open",
                            RequireObjectId(pull.GetProperty("head").GetProperty("sha").GetString(), "historical head")));
                    }
                    // The empty page after an exactly full final page is required evidence of completion.
                    if (rows.GetArrayLength() < 100) return inventory.Complete();
                }
                catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException && exception is not GitHubProviderException)
                { throw GitHubProviderFailure.Malformed(GitHubProviderFailure.OpenPullRequestPhase, exception); }
            }
        }
    }
}
