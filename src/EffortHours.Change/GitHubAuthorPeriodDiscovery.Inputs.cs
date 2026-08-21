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

        if (!Directory.Exists(request.WorkspacePath))
        {
            throw new DirectoryNotFoundException(
                "The requested workspace root was not found or is inaccessible.");
        }

        if (request.AuthorAliases.Count is < 1 or > 128 ||
            request.AuthorAliases.Any(string.IsNullOrWhiteSpace) ||
            !Enum.IsDefined(request.DateField) || !Enum.IsDefined(request.MergePolicy) ||
            !Enum.IsDefined(request.CoauthorPolicy))
        {
            throw new ArgumentException(
                "The today-to-date identity or selection policy is invalid.",
                nameof(request));
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

    internal static (DateTimeOffset Since, DateTimeOffset Until) LocalDay(
        DateTimeOffset asOf,
        TimeZoneInfo zone)
    {
        DateTime localDate = TimeZoneInfo.ConvertTime(asOf, zone).Date;
        return (ResolveLocal(localDate, zone), ResolveLocal(localDate.AddDays(1), zone));
    }

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

    private async Task<string[]> ResolveAliasesAsync(
        GitHubAuthorPeriodDiscoveryRequest request,
        string workspace,
        IReadOnlyList<MappedRepository> mapped,
        string authenticatedLogin,
        ProviderQueryCounters counters,
        CancellationToken cancellationToken)
    {
        bool useMe = request.AuthorAliases.Any(alias =>
            alias.Equals("@me", StringComparison.OrdinalIgnoreCase));
        List<string> aliases = [.. request.AuthorAliases.Where(alias =>
            !alias.Equals("@me", StringComparison.OrdinalIgnoreCase)).Select(alias => alias.Trim())];
        if (useMe)
        {
            aliases.Add(authenticatedLogin);
            aliases.AddRange(await GitHubAuthorPeriodDiscoveryJson.ResolveVerifiedEmailsAsync(
                _commands,
                workspace,
                counters,
                cancellationToken).ConfigureAwait(false));
            aliases.AddRange(mapped.SelectMany(value => value.Local.IdentityAliases));
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

        return canonical;
    }

    private static string IdentitySources(IReadOnlyList<string> aliases) => aliases.Any(alias =>
        alias.Equals("@me", StringComparison.OrdinalIgnoreCase))
            ? aliases.Count == 1
                ? "provider-viewer-and-local-git"
                : "provider-viewer-local-git-and-explicit-aliases"
            : "explicit-aliases";
}
