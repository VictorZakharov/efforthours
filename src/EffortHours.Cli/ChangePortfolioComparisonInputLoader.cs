using System.Globalization;
using System.Text;
using System.Text.Json;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal sealed record ChangePortfolioComparisonInputs(
    ChangePortfolioBucketPolicyKind BucketKind,
    string BucketPolicy,
    ChangePortfolioBucketManifest BucketManifest,
    IReadOnlyList<ChangePortfolioComparisonBucket> Buckets,
    ChangePortfolioCapacityManifest? CapacityManifest);

internal static partial class ChangePortfolioComparisonInputLoader
{
    private const int MaximumInputBytes = 1024 * 1024;

    public static async Task<ChangePortfolioComparisonInputs> LoadAsync(
        ChangePortfolioCommandOptions options,
        ChangePortfolioAuthorPeriodManifestSelection selection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(selection);
        ChangePortfolioComparisonInputs buckets = options.BucketManifestPath is not null
            ? await LoadCustomBucketsAsync(
                options.BucketManifestPath,
                selection,
                cancellationToken).ConfigureAwait(false)
            : CreateCalendarBuckets(options.Bucket!, selection);
        ChangePortfolioCapacityManifest? capacity = options.CapacityManifestPath is null
            ? null
            : await LoadAsync<ChangePortfolioCapacityManifest>(
                options.CapacityManifestPath,
                SchemaNames.ChangePortfolioCapacityManifest,
                ContractValidation.Validate,
                "capacity manifest",
                cancellationToken).ConfigureAwait(false);
        if (capacity is not null)
        {
            ValidateCapacityMatrix(capacity, buckets.Buckets, selection.ContributorIds);
        }

        return buckets with { CapacityManifest = capacity };
    }

    public static ChangePortfolioComparisonInputs CreateTodayToDate(
        ChangePortfolioAuthorPeriodManifestSelection selection,
        DateTimeOffset asOf,
        decimal capacityHours)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacityHours);
        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(selection.TimeZone);
        }
        catch (Exception exception) when (
            exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException(
                $"Timezone '{selection.TimeZone}' was not found on this host.",
                exception);
        }

        DateTimeOffset utcAsOf = asOf.ToUniversalTime();
        if (utcAsOf < selection.SinceInclusive || utcAsOf != selection.UntilExclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(asOf),
                "The today-to-date as-of instant must equal the interval's exclusive snapshot end.");
        }

        string date = TimeZoneInfo.ConvertTime(utcAsOf, zone)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string bucketId = "today-" + date;
        ChangePortfolioComparisonBucket bucket = new()
        {
            Id = bucketId,
            Label = "Today " + date,
            SinceInclusive = selection.SinceInclusive,
            UntilExclusive = selection.UntilExclusive,
            PartialStart = false,
            PartialEnd = true,
        };
        ChangePortfolioBucketManifest buckets = new()
        {
            Buckets =
            [
                new ChangePortfolioBucketDefinition
                {
                    Id = bucket.Id,
                    Label = bucket.Label,
                    SinceInclusive = bucket.SinceInclusive,
                    UntilExclusive = bucket.UntilExclusive,
                },
            ],
        };
        ChangePortfolioCapacityManifest capacity = new()
        {
            CalendarPolicy =
                "Caller-supplied full-day reference capacity for the partial today-to-date bucket.",
            Entries =
            [
                new ChangePortfolioCapacityEntry
                {
                    BucketId = bucketId,
                    ContributorId = selection.ContributorIds.Single(),
                    Hours = capacityHours,
                },
            ],
        };
        ValidateCapacityMatrix(capacity, [bucket], selection.ContributorIds);
        return new ChangePortfolioComparisonInputs(
            ChangePortfolioBucketPolicyKind.Custom,
            ChangePortfolioComparisonPolicies.TodayToDateV1,
            buckets,
            [bucket],
            capacity);
    }

    private static async Task<ChangePortfolioComparisonInputs> LoadCustomBucketsAsync(
        string path,
        ChangePortfolioAuthorPeriodManifestSelection selection,
        CancellationToken cancellationToken)
    {
        ChangePortfolioBucketManifest manifest = await LoadAsync<ChangePortfolioBucketManifest>(
            path,
            SchemaNames.ChangePortfolioBucketManifest,
            ContractValidation.Validate,
            "bucket manifest",
            cancellationToken).ConfigureAwait(false);
        ChangePortfolioBucketDefinition[] ordered =
            [.. manifest.Buckets
                .OrderBy(bucket => bucket.SinceInclusive)
                .ThenBy(bucket => bucket.Id, StringComparer.Ordinal)];
        EnsureExactPartition(ordered, selection);
        ChangePortfolioBucketManifest canonical = manifest with { Buckets = ordered };
        return new ChangePortfolioComparisonInputs(
            ChangePortfolioBucketPolicyKind.Custom,
            ChangePortfolioComparisonPolicies.CustomClosedBucketsV1,
            canonical,
            [.. ordered.Select(bucket => new ChangePortfolioComparisonBucket
            {
                Id = bucket.Id,
                Label = bucket.Label,
                SinceInclusive = bucket.SinceInclusive.ToUniversalTime(),
                UntilExclusive = bucket.UntilExclusive.ToUniversalTime(),
                PartialStart = false,
                PartialEnd = false,
            })],
            null);
    }

    private static ChangePortfolioComparisonInputs CreateCalendarBuckets(
        string policy,
        ChangePortfolioAuthorPeriodManifestSelection selection)
    {
        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(selection.TimeZone);
        }
        catch (Exception exception) when (
            exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException(
                $"Timezone '{selection.TimeZone}' was not found on this host.",
                exception);
        }

        ChangePortfolioBucketPolicyKind kind = policy switch
        {
            "calendar-month" => ChangePortfolioBucketPolicyKind.CalendarMonth,
            "calendar-week" => ChangePortfolioBucketPolicyKind.CalendarWeek,
            "calendar-day" => ChangePortfolioBucketPolicyKind.CalendarDay,
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };
        string versionedPolicy = kind switch
        {
            ChangePortfolioBucketPolicyKind.CalendarMonth =>
                ChangePortfolioComparisonPolicies.CalendarMonthV1,
            ChangePortfolioBucketPolicyKind.CalendarWeek =>
                ChangePortfolioComparisonPolicies.CalendarWeekV1,
            ChangePortfolioBucketPolicyKind.CalendarDay =>
                ChangePortfolioComparisonPolicies.CalendarDayV1,
            _ => throw new ArgumentOutOfRangeException(nameof(policy)),
        };
        List<ChangePortfolioComparisonBucket> buckets = [];
        DateTimeOffset cursor = selection.SinceInclusive.ToUniversalTime();
        while (cursor < selection.UntilExclusive)
        {
            DateTimeOffset localCursor = TimeZoneInfo.ConvertTime(cursor, zone);
            DateTime naturalStart = kind switch
            {
                ChangePortfolioBucketPolicyKind.CalendarMonth =>
                    new DateTime(localCursor.Year, localCursor.Month, 1),
                ChangePortfolioBucketPolicyKind.CalendarWeek =>
                    localCursor.Date.AddDays(-WeekdayOffset(localCursor.DayOfWeek)),
                _ => localCursor.Date,
            };
            DateTime naturalEnd = kind == ChangePortfolioBucketPolicyKind.CalendarMonth
                ? naturalStart.AddMonths(1)
                : naturalStart.AddDays(
                    kind == ChangePortfolioBucketPolicyKind.CalendarWeek ? 7 : 1);
            DateTimeOffset naturalStartInstant = ResolveLocal(naturalStart, zone);
            DateTimeOffset naturalEndInstant = ResolveLocal(naturalEnd, zone);
            DateTimeOffset end = naturalEndInstant < selection.UntilExclusive
                ? naturalEndInstant
                : selection.UntilExclusive.ToUniversalTime();
            string id = kind switch
            {
                ChangePortfolioBucketPolicyKind.CalendarMonth =>
                    naturalStart.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                ChangePortfolioBucketPolicyKind.CalendarWeek =>
                    "week-" + naturalStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                _ => "day-" + naturalStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            };
            string label = kind switch
            {
                ChangePortfolioBucketPolicyKind.CalendarMonth =>
                    naturalStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture),
                ChangePortfolioBucketPolicyKind.CalendarWeek =>
                    "Week of " + naturalStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                _ => naturalStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            };
            buckets.Add(new ChangePortfolioComparisonBucket
            {
                Id = id,
                Label = label,
                SinceInclusive = cursor,
                UntilExclusive = end,
                PartialStart = cursor != naturalStartInstant,
                PartialEnd = end != naturalEndInstant,
            });
            cursor = end;
        }

        ChangePortfolioBucketManifest manifest = new()
        {
            Buckets = [.. buckets.Select(bucket => new ChangePortfolioBucketDefinition
            {
                Id = bucket.Id,
                Label = bucket.Label,
                SinceInclusive = bucket.SinceInclusive,
                UntilExclusive = bucket.UntilExclusive,
            })],
        };
        return new ChangePortfolioComparisonInputs(
            kind,
            versionedPolicy,
            manifest,
            buckets,
            null);
    }

    private static DateTimeOffset ResolveLocal(DateTime local, TimeZoneInfo zone)
    {
        DateTime unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(unspecified))
        {
            throw new ArgumentException(
                $"Calendar boundary '{unspecified:O}' is invalid in timezone '{zone.Id}'. " +
                "Use a custom bucket manifest with explicit offsets.");
        }

        TimeSpan offset = zone.GetUtcOffset(unspecified);
        return new DateTimeOffset(unspecified, offset).ToUniversalTime();
    }

    private static int WeekdayOffset(DayOfWeek day) =>
        ((int)day - (int)DayOfWeek.Monday + 7) % 7;

    private static void EnsureExactPartition(
        ChangePortfolioBucketDefinition[] buckets,
        ChangePortfolioAuthorPeriodManifestSelection selection)
    {
        if (buckets[0].SinceInclusive.ToUniversalTime() != selection.SinceInclusive.ToUniversalTime() ||
            buckets[^1].UntilExclusive.ToUniversalTime() != selection.UntilExclusive.ToUniversalTime())
        {
            throw new JsonException(
                "Custom buckets must start and end at the author-period manifest boundaries.");
        }

        for (int index = 1; index < buckets.Length; index++)
        {
            if (buckets[index - 1].UntilExclusive.ToUniversalTime() !=
                buckets[index].SinceInclusive.ToUniversalTime())
            {
                throw new JsonException(
                    "Custom buckets must form one gap-free, non-overlapping partition.");
            }
        }
    }

    internal static void ValidateCapacityMatrix(
        ChangePortfolioCapacityManifest manifest,
        IReadOnlyList<ChangePortfolioComparisonBucket> buckets,
        IReadOnlyList<string> contributors)
    {
        HashSet<(string BucketId, string ContributorId)> actual =
            [.. manifest.Entries.Select(entry => (entry.BucketId, entry.ContributorId))];
        HashSet<(string BucketId, string ContributorId)> expected =
            [.. buckets.SelectMany(bucket => contributors.Select(contributor => (bucket.Id, contributor)))];
        if (!actual.SetEquals(expected))
        {
            (string BucketId, string ContributorId)[] missing =
            [.. expected.Except(actual).OrderBy(value => value.BucketId, StringComparer.Ordinal)
                .ThenBy(value => value.ContributorId, StringComparer.Ordinal)];
            (string BucketId, string ContributorId)[] unexpected =
            [.. actual.Except(expected).OrderBy(value => value.BucketId, StringComparer.Ordinal)
                .ThenBy(value => value.ContributorId, StringComparer.Ordinal)];
            throw new JsonException(
                "The capacity manifest must contain exactly one positive entry for every requested " +
                $"contributor and bucket. Missing cells: {FormatCells(missing)}. " +
                $"Unexpected cells: {FormatCells(unexpected)}.");
        }
    }

    private static string FormatCells(
        (string BucketId, string ContributorId)[] cells)
    {
        const int maximumShown = 12;
        if (cells.Length == 0)
        {
            return "none";
        }

        string shown = string.Join(
            ", ",
            cells.Take(maximumShown).Select(cell => $"{cell.BucketId}/{cell.ContributorId}"));
        return cells.Length <= maximumShown
            ? shown
            : $"{shown}, and {cells.Length - maximumShown} more";
    }

    private static async Task<T> LoadAsync<T>(
        string path,
        string schemaName,
        Func<T, IReadOnlyList<string>> validate,
        string subject,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(path);
        FileInfo file = new(fullPath);
        if (!file.Exists || file.Length > MaximumInputBytes)
        {
            throw new IOException(
                $"The {subject} was not found, is inaccessible, or exceeds {MaximumInputBytes} bytes.");
        }

        string json;
        try
        {
            await using FileStream stream = new(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using StreamReader reader = new(
                stream,
                new UTF8Encoding(false, true),
                detectEncodingFromByteOrderMarks: true);
            json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or DecoderFallbackException)
        {
            throw new IOException($"The {subject} could not be read as UTF-8 JSON.", exception);
        }

        SchemaValidationResult schema = ContractSchemaValidator.Validate(schemaName, json);
        if (!schema.IsValid)
        {
            throw new JsonException(
                $"The {subject} does not satisfy its JSON Schema: " + string.Join(" ", schema.Errors));
        }

        T value = ContractJson.Deserialize<T>(json);
        IReadOnlyList<string> errors = validate(value);
        if (errors.Count > 0)
        {
            throw new JsonException(
                $"The {subject} is semantically invalid: " + string.Join(" ", errors));
        }

        return value;
    }
}
