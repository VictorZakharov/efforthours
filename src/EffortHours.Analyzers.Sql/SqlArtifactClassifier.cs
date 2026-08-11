using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Sql;

internal sealed record SqlArtifactRoleAssessment(
    string Role,
    string? MigrationOrdering,
    bool Excluded,
    bool BoundedSeedData);

internal static class SqlArtifactClassifier
{
    private static readonly HashSet<string> MigrationDirectories = new(
        ["migration", "migrations", "migrate", "changelog", "changesets"],
        StringComparer.Ordinal);

    private static readonly HashSet<string> SeedDirectories = new(
        ["seed", "seeds", "sample-data", "sampledata"],
        StringComparer.Ordinal);

    private static readonly HashSet<string> DeliveryDirectories = new(
        ["deploy", "deployment", "install", "installer", "bootstrap", "provision"],
        StringComparer.Ordinal);

    private static readonly HashSet<string> DumpDirectories = new(
        ["dump", "dumps", "backup", "backups", "snapshot", "snapshots", "export", "exports"],
        StringComparer.Ordinal);

    public static SqlArtifactRoleAssessment Classify(
        EvidenceFact fileFact,
        string text,
        SqlSemanticMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(fileFact);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(metrics);
        string path = fileFact.Scope.ToLowerInvariant();
        string fileName = Path.GetFileName(path);
        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (IsDump(segments, fileName, text))
        {
            return new SqlArtifactRoleAssessment("dump", null, Excluded: true, BoundedSeedData: false);
        }

        if (fileFact.Tags.Contains("classification:test", StringComparer.Ordinal) ||
            segments.Any(segment => segment is "fixture" or "fixtures" or "test-data" or "testdata"))
        {
            return new SqlArtifactRoleAssessment(
                "test-fixture",
                null,
                Excluded: false,
                BoundedSeedData: true);
        }

        if (TryMigrationOrdering(segments, fileName, out string? ordering))
        {
            return new SqlArtifactRoleAssessment(
                "migration",
                ordering,
                Excluded: false,
                BoundedSeedData: false);
        }

        if (segments.Any(SeedDirectories.Contains) ||
            fileName.StartsWith("seed", StringComparison.Ordinal) ||
            fileName.Contains(".seed.", StringComparison.Ordinal))
        {
            return new SqlArtifactRoleAssessment(
                "seed-data",
                null,
                Excluded: false,
                BoundedSeedData: true);
        }

        if (segments.Any(DeliveryDirectories.Contains) ||
            fileName.StartsWith("install", StringComparison.Ordinal) ||
            fileName.StartsWith("bootstrap", StringComparison.Ordinal) ||
            fileName.StartsWith("provision", StringComparison.Ordinal))
        {
            return new SqlArtifactRoleAssessment("delivery", null, Excluded: false, BoundedSeedData: false);
        }

        if (metrics.StoredPrograms > 0 || metrics.Triggers > 0)
        {
            return new SqlArtifactRoleAssessment("stored-program", null, Excluded: false, BoundedSeedData: false);
        }

        if (metrics.DdlStatements > 0)
        {
            return new SqlArtifactRoleAssessment("schema", null, Excluded: false, BoundedSeedData: false);
        }

        if (metrics.Queries > 0 || metrics.DataModificationStatements > 0)
        {
            return new SqlArtifactRoleAssessment("query", null, Excluded: false, BoundedSeedData: false);
        }

        return new SqlArtifactRoleAssessment("unknown", null, Excluded: false, BoundedSeedData: false);
    }

    private static bool IsDump(string[] segments, string fileName, string text)
    {
        bool maintainedDataPath = segments.Any(segment =>
            SeedDirectories.Contains(segment) ||
            segment is "fixture" or "fixtures" or "test" or "tests" or "test-data" or "testdata");
        if (!maintainedDataPath &&
            (segments.Any(DumpDirectories.Contains) ||
             fileName.EndsWith(".dump.sql", StringComparison.Ordinal) ||
             fileName.EndsWith(".bak.sql", StringComparison.Ordinal) ||
             fileName.EndsWith(".snapshot.sql", StringComparison.Ordinal)))
        {
            return true;
        }

        string prefix = text[..Math.Min(text.Length, 128 * 1024)].ToLowerInvariant();
        if (prefix.Contains("postgresql database dump", StringComparison.Ordinal) ||
            prefix.Contains("mysql dump", StringComparison.Ordinal) ||
            prefix.Contains("dumped from database version", StringComparison.Ordinal) ||
            prefix.Contains("copy ", StringComparison.Ordinal) &&
            prefix.Contains(" from stdin;", StringComparison.Ordinal))
        {
            return true;
        }

        return !maintainedDataPath && CountOccurrences(prefix, "insert into ", 100) >= 100;
    }

    private static bool TryMigrationOrdering(
        string[] segments,
        string fileName,
        out string? ordering)
    {
        string stem = Path.GetFileNameWithoutExtension(fileName);
        if (stem.StartsWith("r__", StringComparison.OrdinalIgnoreCase))
        {
            ordering = "repeatable";
            return true;
        }

        if (stem.Length > 3 &&
            stem[0] is 'v' or 'V' or 'u' or 'U' &&
            char.IsAsciiDigit(stem[1]) &&
            stem.Contains("__", StringComparison.Ordinal))
        {
            ordering = "version-prefix";
            return true;
        }

        int digitCount = 0;
        while (digitCount < stem.Length && char.IsAsciiDigit(stem[digitCount]))
        {
            digitCount++;
        }

        if (digitCount >= 8 && digitCount < stem.Length && stem[digitCount] is '_' or '-' or '.')
        {
            ordering = "timestamp-or-long-numeric-prefix";
            return true;
        }

        if (digitCount >= 3 && digitCount < stem.Length && stem[digitCount] is '_' or '-' or '.')
        {
            ordering = "numeric-prefix";
            return true;
        }

        if (segments.Any(MigrationDirectories.Contains) ||
            stem.Contains("migration", StringComparison.OrdinalIgnoreCase) ||
            stem.Contains("changelog", StringComparison.OrdinalIgnoreCase))
        {
            ordering = "directory-or-name-convention";
            return true;
        }

        ordering = null;
        return false;
    }

    private static int CountOccurrences(string value, string candidate, int limit)
    {
        int count = 0;
        int offset = 0;
        while (count < limit)
        {
            int index = value.IndexOf(candidate, offset, StringComparison.Ordinal);
            if (index < 0)
            {
                break;
            }

            count++;
            offset = index + candidate.Length;
        }

        return count;
    }
}
