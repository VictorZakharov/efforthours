using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed partial class GitHubAuthorPeriodDiscovery
{
    private static void ValidateRequest(GitHubAuthorPeriodDiscoveryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Owner) || request.Owner.Length > 100 ||
            request.Owner.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '-'))
        {
            throw new ArgumentException("The GitHub owner is invalid.", nameof(request));
        }

        if (request.Scope != EngineeringScopeProfile.ProfileName)
        {
            throw new ArgumentException(
                "Provider-assisted author-period discovery requires --scope engineering.",
                nameof(request));
        }

        bool sampled = request.ContributorSample is not null;
        if (!sampled && request.AuthorAliases.Count is < 1 or > 128 ||
            sampled && request.AuthorAliases.Count != 0 ||
            request.AuthorAliases.Any(string.IsNullOrWhiteSpace) ||
            !Enum.IsDefined(request.DateField) || !Enum.IsDefined(request.MergePolicy) ||
            !Enum.IsDefined(request.CoauthorPolicy))
        {
            throw new ArgumentException(
                "The provider-assisted identity or selection policy is invalid.",
                nameof(request));
        }

        if ((request.SinceInclusive is null) != (request.UntilExclusive is null) ||
            request.SinceInclusive >= request.UntilExclusive ||
            request.UntilExclusive > request.AsOf.ToUniversalTime())
        {
            throw new ArgumentException(
                "The provider-assisted interval must be complete, ordered, and no later than asOf.",
                nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ContributorId) ||
            request.ContributorId.Length > 128)
        {
            throw new ArgumentException("The public contributor ID is invalid.", nameof(request));
        }

        if (request.ContributorSample is GitHubContributorSampleRequest sample)
        {
            if (sample.SampleSize is < 1 or > 63 ||
                string.IsNullOrWhiteSpace(sample.SampleSeed) ||
                sample.SampleSeed.Length > 256 ||
                sample.IncludedAuthors.Count + sample.SampleSize >
                    ChangeAuthorPeriodManifestLimits.MaximumContributors ||
                sample.IncludedAuthors.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException(
                    "The contributor sample request is outside the supported bounds.",
                    nameof(request));
            }

            _ = GitHubRepositoryIdentity.Normalize(sample.ContributorsFrom);
        }
    }

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception exception) when (
            exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException($"Timezone '{id}' was not found on this host.", exception);
        }
    }

    internal static DateTimeOffset LocalDayStart(
        DateTimeOffset asOf,
        TimeZoneInfo zone)
    {
        DateTime localDate = TimeZoneInfo.ConvertTime(asOf, zone).Date;
        return ResolveLocal(localDate, zone);
    }

    internal static (DateTimeOffset Since, DateTimeOffset Until) LocalDay(
        DateTimeOffset asOf,
        TimeZoneInfo zone) => (LocalDayStart(asOf, zone), asOf.ToUniversalTime());

    private static DateTimeOffset ResolveLocal(DateTime value, TimeZoneInfo zone)
    {
        DateTime local = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local))
        {
            throw new InvalidOperationException(
                $"Local-day boundary '{local:O}' is not unique in timezone '{zone.Id}'.");
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }

    private async Task<ResolvedAliases> ResolveAliasesAsync(
        GitHubAuthorPeriodDiscoveryRequest request,
        string workingDirectory,
        string authenticatedLogin,
        IReadOnlyList<string>? cachedVerifiedEmails,
        ProviderQueryCounters counters,
        CancellationToken cancellationToken)
    {
        bool useMe = request.AuthorAliases.Any(alias =>
            alias.Equals("@me", StringComparison.OrdinalIgnoreCase));
        IReadOnlyList<string> verifiedEmails = [];
        List<string> aliases = [.. request.AuthorAliases.Where(alias =>
            !alias.Equals("@me", StringComparison.OrdinalIgnoreCase)).Select(alias => alias.Trim())];
        if (useMe)
        {
            aliases.Add(authenticatedLogin);
            verifiedEmails = cachedVerifiedEmails ??
                await GitHubAuthorPeriodDiscoveryJson.ResolveVerifiedEmailsAsync(
                    _commands,
                    workingDirectory,
                    counters,
                    cancellationToken).ConfigureAwait(false);
            aliases.AddRange(verifiedEmails);
        }

        string[] canonical = [.. aliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ThenBy(alias => alias, StringComparer.Ordinal)];
        if (canonical.Length is < 1 or > ChangeAuthorPeriodManifestLimits.MaximumAliasesPerContributor)
        {
            throw new InvalidOperationException(
                $"Resolved identity contains {canonical.Length} aliases; the v1 per-contributor bound is " +
                $"{ChangeAuthorPeriodManifestLimits.MaximumAliasesPerContributor}. Use explicit --author values.");
        }

        return new ResolvedAliases(canonical, verifiedEmails);
    }

    private static string IdentitySources(IReadOnlyList<string> aliases) => aliases.Any(alias =>
        alias.Equals("@me", StringComparison.OrdinalIgnoreCase))
            ? aliases.Count == 1
                ? "provider-viewer"
                : "provider-viewer-and-explicit-aliases"
            : "explicit-aliases";

    private sealed record ResolvedAliases(
        string[] Values,
        IReadOnlyList<string> VerifiedEmails);
}
