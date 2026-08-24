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
                "Today-to-date discovery currently requires --scope engineering.",
                nameof(request));
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
