namespace EffortHours.Change;

// Provider-account scope is separate from the caller's immutable Git aliases.
// Only requested aliases are retained; coauthor text is never account evidence.
internal sealed class GitHubPullAuthorIdentity
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, HashSet<string>> _accounts;

    public GitHubPullAuthorIdentity(IReadOnlyList<string> aliases, string viewer,
        IReadOnlyList<string> verifiedEmails)
    {
        _accounts = aliases.Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(
            alias => alias, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        foreach (string alias in _accounts.Keys)
        {
            if (IsLogin(alias))
            {
                _accounts[alias].Add(alias);
            }
            else if (verifiedEmails.Contains(alias, StringComparer.OrdinalIgnoreCase))
            {
                _accounts[alias].Add(viewer);
            }
        }
    }

    public static bool IsLogin(string value) => value.Length is > 0 and <= 39 &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');

    public void Observe(string? login, GitCommitMetadata commit)
    {
        if (login is null || !IsLogin(login))
        {
            return;
        }

        foreach (string alias in _accounts.Keys)
        {
            if (alias.Equals(commit.Author.Email, StringComparison.OrdinalIgnoreCase) ||
                alias.Equals(commit.Author.Name, StringComparison.OrdinalIgnoreCase) ||
                alias.Equals($"{commit.Author.Name} <{commit.Author.Email}>", StringComparison.OrdinalIgnoreCase))
            {
                Associate(alias, login);
            }
        }
    }

    public void Associate(string alias, string login)
    {
        lock (_gate)
        {
            // Two distinct bindings suffice to retain a conflict without unbounded state.
            if (_accounts[alias].Count < 2)
            {
                _accounts[alias].Add(login);
            }
        }
    }

    public string[] UnresolvedAliases()
    {
        lock (_gate)
        {
            return [.. _accounts.Where(pair => pair.Value.Count == 0).Select(pair => pair.Key)
                .Order(StringComparer.OrdinalIgnoreCase)];
        }
    }

    public string[] KnownLogins()
    {
        lock (_gate)
        {
            if (_accounts.Values.Any(accounts => accounts.Count > 1))
            {
                throw GitHubProviderFailure.UnresolvedContributor();
            }

            return [.. _accounts.Values.SelectMany(accounts => accounts)
                .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];
        }
    }

    public void RequireComplete(IReadOnlyList<string> selectedLogins)
    {
        if (UnresolvedAliases().Length != 0 ||
            !KnownLogins().SequenceEqual(selectedLogins, StringComparer.OrdinalIgnoreCase))
        {
            throw GitHubProviderFailure.UnresolvedContributor();
        }
    }
}
