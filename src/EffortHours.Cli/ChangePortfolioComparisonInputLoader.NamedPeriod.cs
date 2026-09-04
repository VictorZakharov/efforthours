using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal static partial class ChangePortfolioComparisonInputLoader
{
    public static ChangePortfolioComparisonInputs CreateNamedPeriod(
        ChangePortfolioAuthorPeriodManifestSelection selection,
        ChangePortfolioNativePeriodKind period,
        ChangePortfolioNativeBreakdown breakdown,
        decimal capacityHoursPerDay)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacityHoursPerDay);
        ChangePortfolioComparisonInputs daily = CreateCalendarBuckets("calendar-day", selection);
        IReadOnlyList<ChangePortfolioComparisonBucket> buckets;
        ChangePortfolioBucketManifest manifest;
        ChangePortfolioBucketPolicyKind kind;
        string policy;
        decimal capacityPerBucket;
        if (breakdown == ChangePortfolioNativeBreakdown.CalendarDay)
        {
            buckets = daily.Buckets;
            manifest = daily.BucketManifest;
            kind = ChangePortfolioBucketPolicyKind.CalendarDay;
            policy = ChangePortfolioComparisonPolicies.CalendarDayV1;
            capacityPerBucket = capacityHoursPerDay;
        }
        else
        {
            ChangePortfolioComparisonBucket first = daily.Buckets[0];
            ChangePortfolioComparisonBucket last = daily.Buckets[^1];
            ChangePortfolioComparisonBucket bucket = new()
            {
                Id = "period-" + PeriodValue(period),
                Label = PeriodLabel(period),
                SinceInclusive = selection.SinceInclusive,
                UntilExclusive = selection.UntilExclusive,
                PartialStart = first.PartialStart,
                PartialEnd = last.PartialEnd,
            };
            buckets = [bucket];
            manifest = new ChangePortfolioBucketManifest
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
            kind = ChangePortfolioBucketPolicyKind.Custom;
            policy = ChangePortfolioComparisonPolicies.NamedPeriodTotalV1;
            capacityPerBucket = capacityHoursPerDay * daily.Buckets.Count;
        }

        ChangePortfolioCapacityManifest capacity = new()
        {
            CalendarPolicy =
                "Caller-supplied reference capacity per local calendar day; a partial final day uses the full-day denominator.",
            Entries =
            [
                .. buckets.SelectMany(bucket => selection.ContributorIds.Select(contributor =>
                    new ChangePortfolioCapacityEntry
                    {
                        BucketId = bucket.Id,
                        ContributorId = contributor,
                        Hours = capacityPerBucket,
                    })),
            ],
        };
        ValidateCapacityMatrix(capacity, buckets, selection.ContributorIds);
        return new ChangePortfolioComparisonInputs(kind, policy, manifest, buckets, capacity);
    }

    private static string PeriodValue(ChangePortfolioNativePeriodKind period) => period switch
    {
        ChangePortfolioNativePeriodKind.ThisWeek => "this-week",
        ChangePortfolioNativePeriodKind.LastWeek => "last-week",
        ChangePortfolioNativePeriodKind.ThisMonth => "this-month",
        ChangePortfolioNativePeriodKind.LastMonth => "last-month",
        _ => throw new ArgumentOutOfRangeException(nameof(period)),
    };

    private static string PeriodLabel(ChangePortfolioNativePeriodKind period) => period switch
    {
        ChangePortfolioNativePeriodKind.ThisWeek => "This week",
        ChangePortfolioNativePeriodKind.LastWeek => "Last week",
        ChangePortfolioNativePeriodKind.ThisMonth => "This month",
        ChangePortfolioNativePeriodKind.LastMonth => "Last month",
        _ => throw new ArgumentOutOfRangeException(nameof(period)),
    };
}
