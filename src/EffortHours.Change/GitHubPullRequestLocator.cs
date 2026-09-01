using System.Globalization;

namespace EffortHours.Change;

internal sealed record GitHubPullRequestLocator(string RepositoryIdentity, int Number);

internal static class GitHubPullRequestLocatorParser
{
    public static GitHubPullRequestLocator Parse(string input, string? repository)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        string? explicitRepository = string.IsNullOrWhiteSpace(repository)
            ? null
            : GitHubRepositoryIdentity.Normalize(repository);
        if (Uri.TryCreate(input, UriKind.Absolute, out Uri? uri))
        {
            GitHubPullRequestLocator fromUrl = ParseUrl(uri);
            if (explicitRepository is not null &&
                !string.Equals(
                    explicitRepository,
                    fromUrl.RepositoryIdentity,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Option --repo does not match the repository in the pull-request URL.",
                    nameof(repository));
            }

            return fromUrl;
        }

        if (!int.TryParse(input, NumberStyles.None, CultureInfo.InvariantCulture, out int number) ||
            number <= 0)
        {
            throw new ArgumentException(
                "Checkout-free --pr must be a full GitHub pull-request URL or a positive pull-request number.",
                nameof(input));
        }

        if (explicitRepository is null)
        {
            throw new ArgumentException(
                "Checkout-free pull-request numbers require --repo <owner/name>.",
                nameof(repository));
        }

        return new GitHubPullRequestLocator(explicitRepository, number);
    }

    private static GitHubPullRequestLocator ParseUrl(Uri uri)
    {
        if (uri.Scheme != Uri.UriSchemeHttps ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException(
                "Checkout-free pull-request URLs must be credential-free HTTPS GitHub URLs.",
                nameof(uri));
        }

        string[] segments = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 4 ||
            !segments[2].Equals("pull", StringComparison.Ordinal) ||
            !int.TryParse(segments[3], NumberStyles.None, CultureInfo.InvariantCulture, out int number) ||
            number <= 0)
        {
            throw new ArgumentException(
                "Checkout-free pull-request URLs must use https://github.com/<owner>/<repository>/pull/<number>.",
                nameof(uri));
        }

        string identity = GitHubRepositoryIdentity.Normalize(segments[0] + "/" + segments[1]);
        return new GitHubPullRequestLocator(identity, number);
    }
}

internal static class GitHubRepositoryIdentity
{
    public static string Normalize(string identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        if (identity.Length > 512)
        {
            throw new ArgumentException("The GitHub repository identity is invalid.", nameof(identity));
        }

        string[] parts = identity.Split('/');
        if (parts.Length != 2 || parts.Any(string.IsNullOrWhiteSpace) ||
            parts.Any(part => part is "." or ".." || part.Any(character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.')))
        {
            throw new ArgumentException("The GitHub repository identity is invalid.", nameof(identity));
        }

        return parts[0].ToLowerInvariant() + "/" + parts[1].ToLowerInvariant();
    }
}
