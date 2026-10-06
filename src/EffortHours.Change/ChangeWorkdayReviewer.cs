using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public static class ChangeWorkdayReviewer
{
    public static ChangeWorkdayReviewReport Review(ChangePortfolioComparisonReport source,
        ChangeWorkRecordManifest manifest, string? entryPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (entryPolicy is not null && entryPolicy != ChangeWorkdayReviewPolicies.EqualEntries)
            throw new ArgumentException("Unknown workday entry policy.");
        RequireValid(ContractValidation.Validate(manifest));
        ChangeWorkdaySource input = ChangeWorkdaySource.Read(source, manifest.SourceSemanticDigest);
        if (manifest.Records.Select(record => record.Date).Except(input.Geometry.Select(value => value.Date), StringComparer.Ordinal).Any())
            throw new ArgumentException("Work records outside the source period require a new complete source report.");
        string[] repositories = [.. input.Selection.Repositories.Select(repository => repository.Id).Order(StringComparer.Ordinal)];
        Dictionary<string, ChangeWorkRecord[]> recordsByDate = manifest.Records.GroupBy(record => record.Date, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(record => record.RecordId, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        Dictionary<string, ChangePortfolioComparisonPoint> points = input.Series.Points.ToDictionary(point => point.BucketId, StringComparer.Ordinal);
        Dictionary<string, string> nativeEvidence = (source.NativePeriod?.DailyEvidence ?? []).ToDictionary(day => day.BucketId, day => day.State, StringComparer.Ordinal);
        HashSet<string> selectedBuckets = nativeEvidence.Count > 0 ? [] : SelectedBuckets(input);
        ChangePortfolioAttributionCompleteness completeness = ChangePortfolioAttributionCompleteness.From(input.Portfolio);
        List<ChangeWorkdayReviewDay> days = [];
        foreach ((ChangePortfolioComparisonBucket bucket, string date) in input.Geometry)
        {
            ChangePortfolioComparisonPoint point = points[bucket.Id];
            ChangeWorkRecord[] records = recordsByDate.GetValueOrDefault(date) ?? [];
            string status = completeness.DeclaredEventStatus == "unresolved" && records.Any(record => record.Kind is "implementation" or "mixed")
                ? "unresolved-event-attribution" : records.Any(record => record.Kind == "mixed") ? "mixed-work-records-unresolved"
                : records.Any(record => record.Kind == "implementation" && !SameScope(record.RepositoryIds)) ? "repository-scope-unresolved"
                : records.Any(record => record.Kind == "implementation") ? point.Effort.Expected > 0 ? "retained-attribution-available" : "unresolved-workday"
                : point.Effort.Expected > 0 ? "missing-work-record" : "no-implementation-record";
            decimal? multiplier = status == "retained-attribution-available" && entryPolicy is not null
                ? decimal.Round(point.Effort.Expected / 8m, 2, MidpointRounding.AwayFromZero) : null;
            if (multiplier > decimal.MaxValue / 100) throw new ArgumentException("Entry multiplier exceeds the cent arithmetic bound.");
            int count = records.Count(record => record.Kind == "implementation"), rank = 0;
            ChangeWorkRecordReview[] reviews = [.. records.Select(record => new ChangeWorkRecordReview
            {
                RecordId = record.RecordId, Kind = record.Kind,
                RepositoryIds = [.. record.RepositoryIds.Order(StringComparer.Ordinal)],
                Status = record.Kind is "meeting" or "pto" ? "excluded-non-implementation"
                    : status == "retained-attribution-available" ? "retained-attribution-available" : status,
                AllocatedMultiplierContribution = record.Kind == "implementation" && multiplier is { } value
                    ? Part(value, count, rank++) : null,
            })];
            string evidence = nativeEvidence.GetValueOrDefault(bucket.Id)
                ?? (!selectedBuckets.Contains(bucket.Id) ? "no-retained-change" : point.Effort.Expected > 0 ? "measured-retained-change" : "reconciled-zero");
            days.Add(new()
            {
                Date = date,
                BucketId = bucket.Id,
                Status = status,
                RetainedEvidenceStatus = evidence,
                SourceAttributedExpectedHours = point.Effort.Expected,
                MatchedDailyMultiplier = multiplier,
                Records = reviews,
            });
        }
        ChangeWorkdayReviewReport report = new()
        {
            Status = days.Any(day => day.Status is not ("retained-attribution-available" or "no-implementation-record")) ? "unresolved" : "reviewed-retained-attribution",
            SourceSemanticDigest = input.Digest,
            SourcePortfolioDigest = source.Verification.SourcePortfolioDigest!,
            WorkRecordInputDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest(ContractJson.SerializeCompact(manifest with
            {
                Records = [.. manifest.Records.OrderBy(record => record.Date, StringComparer.Ordinal).ThenBy(record => record.RecordId, StringComparer.Ordinal)
                    .Select(record => record with { RepositoryIds = [.. record.RepositoryIds.Order(StringComparer.Ordinal)] })],
            })),
            TimeZone = input.Selection.TimeZone,
            ContributorId = input.Selection.ContributorIds[0],
            EntryPolicy = entryPolicy,
            AttributionCompleteness = completeness,
            RepositoryIds = repositories,
            Days = days,
        };
        RequireValid(ContractValidation.Validate(report));
        return report;

        bool SameScope(IReadOnlyList<string> ids) => ids.Count == repositories.Length && !ids.Except(repositories, StringComparer.Ordinal).Any();
    }

    private static HashSet<string> SelectedBuckets(ChangeWorkdaySource input)
    {
        HashSet<string> result = new(StringComparer.Ordinal);
        foreach (ChangePortfolioItemEstimate item in input.Portfolio.Items)
        {
            if (item.Attribution.Rewrite?.SupportOnly == true || item.Attribution.SelectedTimestamp is not { } timestamp) continue;
            int low = 0, high = input.Geometry.Count - 1;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                ChangePortfolioComparisonBucket bucket = input.Geometry[middle].Bucket;
                if (timestamp < bucket.SinceInclusive) high = middle - 1;
                else if (timestamp >= bucket.UntilExclusive) low = middle + 1;
                else { result.Add(bucket.Id); break; }
            }
        }
        return result;
    }

    private static decimal Part(decimal value, int count, int rank)
    {
        decimal cents = value * 100;
        return (decimal.Floor(cents / count) + (rank < cents % count ? 1 : 0)) / 100;
    }

    private static void RequireValid(IReadOnlyList<string> errors)
    {
        if (errors.Count > 0) throw new ArgumentException("Invalid workday review: " + string.Join(" ", errors));
    }
}
