namespace EffortHours.Change;

internal sealed record GitCommitIdentity(string Name, string Email);

internal sealed record GitCommitMetadata
{
    public required string ObjectId { get; init; }

    public IReadOnlyList<string> ParentObjectIds { get; init; } = [];

    public required GitCommitIdentity Author { get; init; }

    public required DateTimeOffset AuthorTimestamp { get; init; }

    public required GitCommitIdentity Committer { get; init; }

    public required DateTimeOffset CommitterTimestamp { get; init; }

    public IReadOnlyList<GitCommitIdentity> Coauthors { get; init; } = [];
}

internal static class GitCommitMetadataParser
{
    private const int FieldCount = 9;

    public static IReadOnlyList<GitCommitMetadata> Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        string[] fields = output.Split('\0');
        int usable = fields.Length;
        if (usable > 0 && string.IsNullOrWhiteSpace(fields[usable - 1]))
        {
            usable--;
        }

        if (usable % FieldCount != 0)
        {
            throw new InvalidOperationException("Git returned malformed author-period commit metadata.");
        }

        List<GitCommitMetadata> commits = [];
        for (int index = 0; index < usable; index += FieldCount)
        {
            string objectId = RequireObjectId(fields[index].TrimStart('\r', '\n'));
            string[] parents = [.. fields[index + 1]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(RequireObjectId)];
            commits.Add(new GitCommitMetadata
            {
                ObjectId = objectId,
                ParentObjectIds = parents,
                Author = Identity(fields[index + 2], fields[index + 3]),
                AuthorTimestamp = Timestamp(fields[index + 4]),
                Committer = Identity(fields[index + 5], fields[index + 6]),
                CommitterTimestamp = Timestamp(fields[index + 7]),
                Coauthors = ParseCoauthors(fields[index + 8]),
            });
        }

        return commits;
    }

    private static GitCommitIdentity Identity(string name, string email) =>
        new(name.Trim(), email.Trim());

    private static DateTimeOffset Timestamp(string value) =>
        DateTimeOffset.TryParse(
            value.Trim(),
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind,
            out DateTimeOffset result)
            ? result
            : throw new InvalidOperationException("Git returned an invalid strict ISO commit timestamp.");

    private static IReadOnlyList<GitCommitIdentity> ParseCoauthors(string values)
    {
        List<GitCommitIdentity> identities = [];
        foreach (string rawValue in values.Split(
            ['\u001f', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string value = rawValue.Trim();
            int open = value.LastIndexOf('<');
            int close = value.LastIndexOf('>');
            if (open <= 0 || close != value.Length - 1 || open >= close)
            {
                continue;
            }

            string name = value[..open].Trim();
            string email = value[(open + 1)..close].Trim();
            if (name.Length > 0 && email.Length > 0)
            {
                identities.Add(new GitCommitIdentity(name, email));
            }
        }

        return [.. identities.Distinct().OrderBy(identity => identity.Email, StringComparer.OrdinalIgnoreCase)];
    }

    private static string RequireObjectId(string value)
    {
        string objectId = value.Trim().ToLowerInvariant();
        if (objectId.Length is not (40 or 64) || objectId.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidOperationException("Git returned an invalid commit identity.");
        }

        return objectId;
    }
}
