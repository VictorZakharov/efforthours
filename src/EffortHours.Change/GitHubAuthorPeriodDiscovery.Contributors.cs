using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed partial class GitHubAuthorPeriodDiscovery
{
    private async Task<ResolvedDiscoveryContributors> ResolveSingleContributorAsync(
        GitHubAuthorPeriodDiscoveryRequest request,
        string workingDirectory,
        string authenticatedLogin,
        IReadOnlyList<string>? cachedVerifiedEmails,
        ProviderQueryCounters counters,
        CancellationToken cancellationToken)
    {
        ResolvedAliases resolved = await ResolveAliasesAsync(
            request,
            workingDirectory,
            authenticatedLogin,
            cachedVerifiedEmails,
            counters,
            cancellationToken).ConfigureAwait(false);
        ChangePortfolioContributorSelection selection = new()
        {
            Mode = ChangePortfolioContributorSelectionMode.SingleContributor,
            Complete = true,
            RequestedSampleSize = 0,
            EligiblePopulationCount = 1,
            IncludedContributorIds = [request.ContributorId],
            InputDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest(
                string.Join("\n", new[] { request.ContributorId }.Concat(resolved.Values))),
        };
        return new ResolvedDiscoveryContributors(
            [new ResolvedDiscoveryContributor(
                request.ContributorId,
                authenticatedLogin,
                resolved.Values)],
            selection,
            resolved.VerifiedEmails);
    }

    private async Task<ResolvedDiscoveryContributors> ResolveTeamContributorsAsync(
        GitHubAuthorPeriodDiscoveryRequest request,
        IReadOnlyList<GitHubDiscoveryRepository> repositories,
        string workingDirectory,
        string authenticatedLogin,
        IReadOnlyList<string>? cachedVerifiedEmails,
        DateTimeOffset since,
        DateTimeOffset until,
        ProviderQueryCounters counters,
        CancellationToken cancellationToken)
    {
        GitHubContributorSampleRequest sample = request.ContributorSample!;
        string referenceIdentity = GitHubRepositoryIdentity.Normalize(sample.ContributorsFrom);
        string requestedOwner = referenceIdentity.Split('/')[0];
        if (!requestedOwner.Equals(request.Owner, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The contributor reference repository must belong to the requested owner.");
        }

        GitHubDiscoveryRepository reference = repositories.SingleOrDefault(repository =>
            repository.Identity.Equals(referenceIdentity, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                "The contributor reference repository is absent from the complete owner inventory.");
        if (reference.DefaultBranch is null)
        {
            throw new InvalidOperationException(
                "The contributor reference repository has no default branch.");
        }

        IReadOnlyList<GitHubActiveContributor> population =
            await GitHubAuthorPeriodDiscoveryJson.ListActiveContributorsAsync(
                _commands,
                workingDirectory,
                reference,
                since,
                until,
                request.DateField,
                request.MergePolicy,
                counters,
                cancellationToken).ConfigureAwait(false);
        List<(string Login, IReadOnlyList<string> Aliases)> included = [];
        List<string> verifiedEmails = [];
        foreach (string requested in sample.IncludedAuthors
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value, StringComparer.Ordinal))
        {
            string login = requested.Equals("@me", StringComparison.OrdinalIgnoreCase)
                ? authenticatedLogin
                : requested.Trim();
            GitHubActiveContributor? active = population.FirstOrDefault(contributor =>
                contributor.Login.Equals(login, StringComparison.OrdinalIgnoreCase));
            IReadOnlyList<string> aliases = active?.Aliases ?? [login];
            if (requested.Equals("@me", StringComparison.OrdinalIgnoreCase))
            {
                ResolvedAliases me = await ResolveAliasesAsync(
                    request with
                    {
                        AuthorAliases = ["@me"],
                        ContributorSample = null,
                    },
                    workingDirectory,
                    authenticatedLogin,
                    cachedVerifiedEmails,
                    counters,
                    cancellationToken).ConfigureAwait(false);
                aliases = [.. aliases.Concat(me.Values)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase)
                    .ThenBy(value => value, StringComparer.Ordinal)];
                verifiedEmails.AddRange(me.VerifiedEmails);
            }

            if (aliases.Count > ChangeAuthorPeriodManifestLimits.MaximumAliasesPerContributor)
            {
                throw new InvalidOperationException(
                    "An explicitly included contributor exceeds the v1 alias bound.");
            }

            int existing = included.FindIndex(value =>
                value.Login.Equals(login, StringComparison.OrdinalIgnoreCase));
            if (existing >= 0)
            {
                included[existing] = (included[existing].Login,
                    [.. included[existing].Aliases.Concat(aliases)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Order(StringComparer.OrdinalIgnoreCase)
                        .ThenBy(value => value, StringComparer.Ordinal)]);
            }
            else
            {
                included.Add((login, aliases));
            }
        }

        if (included.Any(value =>
            value.Aliases.Count > ChangeAuthorPeriodManifestLimits.MaximumAliasesPerContributor))
        {
            throw new InvalidOperationException(
                "A de-duplicated explicitly included contributor exceeds the v1 alias bound.");
        }

        HashSet<string> includedLogins = new(
            included.Select(value => value.Login),
            StringComparer.OrdinalIgnoreCase);
        GitHubActiveContributor[] candidates = [.. population.Where(contributor =>
            !includedLogins.Contains(contributor.Login))];
        if (candidates.Length < sample.SampleSize)
        {
            throw new InvalidOperationException(
                $"The reference repository has {population.Count} eligible active human contributors, " +
                $"but only {candidates.Length} remain after explicit inclusions for a sample of " +
                $"{sample.SampleSize}.");
        }

        GitHubActiveContributor[] sampled = [.. candidates
            .OrderBy(contributor => SampleRank(sample.SampleSeed, contributor.Login), StringComparer.Ordinal)
            .ThenBy(contributor => contributor.Login, StringComparer.OrdinalIgnoreCase)
            .Take(sample.SampleSize)];
        List<ResolvedDiscoveryContributor> contributors = [];
        List<string> includedIds = [];
        for (int index = 0; index < included.Count; index++)
        {
            string id = $"included-{index + 1}";
            includedIds.Add(id);
            contributors.Add(new ResolvedDiscoveryContributor(
                id,
                included[index].Login,
                included[index].Aliases));
        }

        List<string> sampledIds = [];
        for (int index = 0; index < sampled.Length; index++)
        {
            string id = $"sample-{index + 1}";
            sampledIds.Add(id);
            contributors.Add(new ResolvedDiscoveryContributor(
                id,
                sampled[index].Login,
                sampled[index].Aliases));
        }

        contributors = RemoveAmbiguousAliases(contributors);
        int aliasCount = contributors.Sum(contributor => contributor.Aliases.Count);
        if (aliasCount > ChangeAuthorPeriodManifestLimits.MaximumAliases)
        {
            throw new InvalidOperationException(
                $"Resolved team identities contain {aliasCount} aliases; the v1 overall bound is " +
                $"{ChangeAuthorPeriodManifestLimits.MaximumAliases}.");
        }

        string inputDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest(string.Join(
            '\n',
            ChangePortfolioComparisonPolicies.ContributorSampleV1,
            referenceIdentity,
            since.ToUniversalTime().ToString("O"),
            until.ToUniversalTime().ToString("O"),
            sample.SampleSeed,
            string.Join(",", population.Select(value => value.Login.ToLowerInvariant())),
            string.Join(",", contributors.Select(value => value.Login.ToLowerInvariant()))));
        ChangePortfolioContributorSelection selection = new()
        {
            Mode = ChangePortfolioContributorSelectionMode.Team,
            Complete = true,
            SampleSeed = sample.SampleSeed,
            RequestedSampleSize = sample.SampleSize,
            EligiblePopulationCount = population.Count,
            SampledContributorIds = sampledIds,
            IncludedContributorIds = includedIds,
            InputDigest = inputDigest,
        };
        return new ResolvedDiscoveryContributors(
            contributors,
            selection,
            [.. verifiedEmails.Distinct(StringComparer.OrdinalIgnoreCase)]);
    }

    internal static string SampleRank(string seed, string login) =>
        ChangePortfolioComparisonIdentity.ComputeTextDigest(
            ChangePortfolioComparisonPolicies.ContributorSampleV1 + "\n" +
            seed + "\n" + login.ToLowerInvariant());

    private static List<ResolvedDiscoveryContributor> RemoveAmbiguousAliases(
        IReadOnlyList<ResolvedDiscoveryContributor> contributors)
    {
        Dictionary<string, int> owners = new(StringComparer.OrdinalIgnoreCase);
        foreach (ResolvedDiscoveryContributor contributor in contributors)
        {
            foreach (string alias in contributor.Aliases.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                owners[alias] = owners.GetValueOrDefault(alias) + 1;
            }
        }

        return [.. contributors.Select(contributor => contributor with
        {
            Aliases = [.. contributor.Aliases
                .Where(alias => owners[alias] == 1 ||
                    alias.Equals(contributor.Login, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ThenBy(alias => alias, StringComparer.Ordinal)],
        })];
    }
}
