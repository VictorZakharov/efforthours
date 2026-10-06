using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static class ChangeDeclaredWorkdayReviewPolicy
{
    public static ChangeWorkdayReviewReport Apply(ChangeWorkdayReviewReport retained,
        ChangeWorkdayManifest manifest, ChangeWorkdayAllocationReport allocation, string? entryPolicy = null)
    {
        Require(ContractValidation.Validate(retained));
        Require(ContractValidation.Validate(manifest));
        Require(ContractValidation.Validate(allocation));
        if (retained.Policy != ChangeWorkdayReviewPolicies.Review || retained.EntryPolicy is not null ||
            retained.WorkdayResolution is not null || entryPolicy is not null && entryPolicy != ChangeDeclaredWorkdayReviewPolicies.EqualEntries)
            throw new ArgumentException("Declared review requires an unallocated retained review and its explicit separate entry policy.");
        if (manifest.SourceSemanticDigest != retained.SourceSemanticDigest || allocation.SourceSemanticDigest != retained.SourceSemanticDigest ||
            allocation.SourcePortfolioDigest != retained.SourcePortfolioDigest || allocation.TimeZone != retained.TimeZone ||
            allocation.ContributorId != retained.ContributorId || allocation.WorkdayInputDigest != DeclarationDigest(manifest))
            throw new ArgumentException("Declared dates must bind the exact source, portfolio, contributor, timezone and declaration.");
        Dictionary<string, ChangeWorkdayAllocationDay> allocated = allocation.Days.ToDictionary(day => day.Date, StringComparer.Ordinal);
        if (allocated.Count != retained.Days.Count || retained.Days.Any(day => !allocated.TryGetValue(day.Date, out var value) ||
            day.BucketId != value.BucketId || day.SourceAttributedExpectedHours != value.SourceAttributedEffort.Expected))
            throw new ArgumentException("Allocation must preserve every exact retained date, bucket and source value.");
        ChangeDeclaredWorkday[] declared = [.. manifest.Workdays.OrderBy(day => day.Date, StringComparer.Ordinal)];
        Dictionary<string, ChangeWorkdayReviewDay> days = retained.Days.ToDictionary(day => day.Date, StringComparer.Ordinal);
        HashSet<string> dates = declared.Select(day => day.Date).ToHashSet(StringComparer.Ordinal);
        foreach (ChangeDeclaredWorkday day in declared)
        {
            if (!days.TryGetValue(day.Date, out var review) || allocated[day.Date].RecordId != day.RecordId ||
                !review.Records.Any(record => record.RecordId == day.RecordId && record.Kind == "implementation" && SameScope(record.RepositoryIds)))
                throw new ArgumentException("Each declared date must anchor an exact in-scope implementation record on that date.");
        }
        if (retained.Days.Any(day => day.Records.Any(record => record.Kind == "mixed" ||
            record.Kind == "implementation" && (!dates.Contains(day.Date) || !SameScope(record.RepositoryIds)))))
            throw new ArgumentException("Resolve mixed/scope records and declare every implementation date before assigning period effort.");
        if (allocation.Days.Any(day => (day.RecordId is not null) != dates.Contains(day.Date)))
            throw new ArgumentException("Allocated days must match the complete declaration exactly.");
        decimal? total = entryPolicy is not null && allocation.TotalEffort.Expected > 0
            ? decimal.Round(allocation.TotalEffort.Expected / 8m, 2, MidpointRounding.AwayFromZero) : null;
        if (total > decimal.MaxValue / 100) throw new ArgumentException("Period multiplier exceeds the cent arithmetic bound.");
        Dictionary<string, int> dateRanks = declared.Select((day, rank) => (day.Date, rank)).ToDictionary(value => value.Date, value => value.rank, StringComparer.Ordinal);
        List<ChangeWorkdayReviewDay> output = [];
        foreach (ChangeWorkdayReviewDay day in retained.Days)
        {
            bool selected = dateRanks.TryGetValue(day.Date, out int rank);
            string status = !selected ? "no-declared-workday" : allocation.TotalEffort.Expected > 0
                ? "declared-workday-allocation" : "declared-workday-no-retained-effort";
            decimal? multiplier = selected && total is { } value ? Part(value, declared.Length, rank) : null;
            int count = day.Records.Count(record => record.Kind == "implementation"), entryRank = 0;
            output.Add(day with
            {
                Status = status,
                WorkdayEvidenceBasis = selected ? "external-work-record" : "not-declared",
                AllocatedExpectedHours = allocated[day.Date].AllocatedEffort.Expected,
                MatchedDailyMultiplier = multiplier,
                Records = [.. day.Records.OrderBy(record => record.RecordId, StringComparer.Ordinal).Select(record => record with
                {
                    Status = record.Kind is "meeting" or "pto" ? "excluded-non-implementation" : status,
                    AllocatedMultiplierContribution = record.Kind == "implementation" && multiplier is { } share ? Part(share, count, entryRank++) : null,
                })],
            });
        }
        return retained with
        {
            Policy = ChangeDeclaredWorkdayReviewPolicies.Review,
            Boundary = ChangeDeclaredWorkdayReviewPolicies.Boundary,
            Status = allocation.TotalEffort.Expected > 0 ? "reviewed-declared-workdays" : "declared-workdays-no-retained-effort",
            EntryPolicy = entryPolicy,
            Days = output,
            WorkdayResolution = new() { Manifest = manifest with { Workdays = declared }, Allocation = allocation, ExpectedMultiplierTotal = total },
        };

        bool SameScope(IReadOnlyList<string> ids) => ids.Count == retained.RepositoryIds.Count && !ids.Except(retained.RepositoryIds, StringComparer.Ordinal).Any();
    }

    internal static ChangeWorkdayReviewReport RetainedProjection(ChangeWorkdayReviewReport report)
    {
        ChangeWorkdayReviewDay[] days = [.. report.Days.Select(day =>
        {
            string status = report.AttributionCompleteness?.DeclaredEventStatus == "unresolved" && day.Records.Any(record => record.Kind is "implementation" or "mixed")
                ? "unresolved-event-attribution" : day.Records.Any(record => record.Kind == "mixed") ? "mixed-work-records-unresolved"
                : day.Records.Any(record => record.Kind == "implementation" &&
                    (record.RepositoryIds.Count != report.RepositoryIds.Count || record.RepositoryIds.Except(report.RepositoryIds, StringComparer.Ordinal).Any())) ? "repository-scope-unresolved"
                : day.Records.Any(record => record.Kind == "implementation") ? day.SourceAttributedExpectedHours > 0 ? "retained-attribution-available" : "unresolved-workday"
                : day.SourceAttributedExpectedHours > 0 ? "missing-work-record" : "no-implementation-record";
            return day with { Status = status, WorkdayEvidenceBasis = null, AllocatedExpectedHours = null, MatchedDailyMultiplier = null,
                Records = [.. day.Records.Select(record => record with { Status = record.Kind is "meeting" or "pto" ? "excluded-non-implementation" : status, AllocatedMultiplierContribution = null })] };
        })];
        return report with
        {
            Policy = ChangeWorkdayReviewPolicies.Review,
            Boundary = ChangeWorkdayReviewPolicies.Boundary,
            Status = days.Any(day => day.Status is not ("retained-attribution-available" or "no-implementation-record")) ? "unresolved" : "reviewed-retained-attribution",
            EntryPolicy = null,
            WorkdayResolution = null,
            Days = days
        };
    }

    private static string DeclarationDigest(ChangeWorkdayManifest manifest) => ChangePortfolioComparisonIdentity.ComputeTextDigest(
        ContractJson.SerializeCompact(manifest with { Workdays = [.. manifest.Workdays.OrderBy(day => day.Date, StringComparer.Ordinal)] }));
    private static decimal Part(decimal value, int count, int rank)
    {
        decimal cents = value * 100;
        return (decimal.Floor(cents / count) + (rank < cents % count ? 1 : 0)) / 100;
    }
    private static void Require(IReadOnlyList<string> errors)
    {
        if (errors.Count > 0) throw new ArgumentException("Invalid declared-workday review input.");
    }
}
