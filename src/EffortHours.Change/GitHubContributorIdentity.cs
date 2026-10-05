using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

// Provider associations resolve a selected login into exact local Git email aliases.
// These aliases remain private execution inputs and never become effort signals.
internal sealed class GitHubContributorIdentity(string login)
{
    private readonly Lock _gate = new();
    private readonly HashSet<string> _emails = new(StringComparer.OrdinalIgnoreCase);

    public string Login => login;

    public string[] AppendAliases(IReadOnlyList<string> aliases)
    {
        lock (_gate)
            return [.. aliases.Concat(_emails).Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)];
    }

    public void ObserveEmails(IReadOnlyList<string> emails)
    {
        foreach (string email in emails) AddEmail(email);
    }

    public void Observe(string? providerLogin, GitCommitMetadata commit)
    {
        if (login.Equals(providerLogin, StringComparison.OrdinalIgnoreCase)) AddEmail(commit.Author.Email);
    }

    private void AddEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        lock (_gate)
        {
            string email = value.ToLowerInvariant();
            if (!_emails.Contains(email) && _emails.Count >= ChangeAuthorPeriodManifestLimits.MaximumAliasesPerContributor)
                throw new InvalidOperationException("The selected provider identity exceeds the alias bound.");
            _emails.Add(email);
        }
    }

    public ResolvedDiscoveryContributors Apply(ResolvedDiscoveryContributors resolved)
    {
        ResolvedDiscoveryContributor contributor = resolved.Contributors.Single();
        string[] aliases;
        lock (_gate)
        {
            aliases = [.. contributor.Aliases.Concat(_emails)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value, StringComparer.Ordinal)];
        }

        if (aliases.Length > ChangeAuthorPeriodManifestLimits.MaximumAliasesPerContributor)
        {
            throw new InvalidOperationException("The selected provider identity exceeds the alias bound.");
        }

        return resolved with
        {
            Contributors = [contributor with { Aliases = aliases }],
            Selection = resolved.Selection with
            {
                InputDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest(
                    string.Join("\n", new[] { contributor.Id }.Concat(aliases))),
            },
        };
    }
}
