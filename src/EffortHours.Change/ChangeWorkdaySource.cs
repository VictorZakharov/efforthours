using System.Globalization;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal sealed record ChangeWorkdaySource(ChangePortfolioReport Portfolio,
    ChangePortfolioAuthorPeriodManifestSelection Selection, ChangePortfolioComparisonSeries Series,
    IReadOnlyList<(ChangePortfolioComparisonBucket Bucket, string Date)> Geometry, string Digest)
{
    public static ChangeWorkdaySource Read(ChangePortfolioComparisonReport source, string expectedDigest)
    {
        ArgumentNullException.ThrowIfNull(source);
        IReadOnlyList<string> errors = ContractValidation.Validate(source);
        if (errors.Count > 0) throw new ArgumentException("Invalid workday source: " + string.Join(" ", errors));
        ChangePortfolioReport portfolio = source.SourcePortfolio ??
            throw new ArgumentException("Workday processing requires complete evidence; incomplete discovery cannot produce values.");
        ChangePortfolioAuthorPeriodManifestSelection selection = source.Selection.AuthorPeriodManifest ??
            throw new ArgumentException("Workday processing requires an author-period manifest comparison.");
        if (source.Status != ChangePortfolioComparisonStatus.Complete || !source.Verification.CompleteAggregates ||
            portfolio.DailyNormalization is not null || selection.ContributorIds.Count != 1 ||
            source.BucketPolicy.ContributorNormalization != ChangePortfolioContributorNormalization.Joint ||
            source.BucketPolicy.Kind != ChangePortfolioBucketPolicyKind.CalendarDay)
            throw new ArgumentException("Workday processing requires a complete, joint, single-contributor calendar-day report.");
        string digest = source.NativePeriod is null
            ? ChangePortfolioComparisonIdentity.ComputeSemanticDigest(portfolio, source.BucketPolicy, source.Buckets, source.Series, source.ScopeProfile)
            : ChangePortfolioComparisonIdentity.ComputeSemanticDigest(portfolio, source.BucketPolicy, source.Buckets, source.Series, source.ScopeProfile, source.NativePeriod);
        if (digest != source.Verification.SemanticDigest || digest != expectedDigest ||
            ChangePortfolioComparisonIdentity.ComputePortfolioDigest(portfolio) != source.Verification.SourcePortfolioDigest)
            throw new ArgumentException("Workday source digest mismatch; bind records to this exact complete report.");
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(selection.TimeZone); }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new ArgumentException("The source timezone is unavailable on this host.", nameof(source), exception); }
        List<(ChangePortfolioComparisonBucket Bucket, string Date)> geometry = [];
        foreach (ChangePortfolioComparisonBucket bucket in source.Buckets.OrderBy(bucket => bucket.SinceInclusive))
        {
            DateTime localStart = TimeZoneInfo.ConvertTime(bucket.SinceInclusive, zone).DateTime;
            DateTime localEnd = TimeZoneInfo.ConvertTime(bucket.UntilExclusive, zone).DateTime;
            if (bucket.PartialStart || bucket.PartialEnd || localStart.TimeOfDay != TimeSpan.Zero || localEnd != localStart.AddDays(1))
                throw new ArgumentException("Workday processing requires whole local calendar days, including DST-aware boundaries.");
            geometry.Add((bucket, localStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        }
        return new(portfolio, selection, source.Series.Single(value => value.Kind == ChangePortfolioSeriesKind.Portfolio), geometry, digest);
    }
}
