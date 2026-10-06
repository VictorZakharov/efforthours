using System.Globalization;
using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(ChangeWorkRecordManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        List<string> errors = [];
        RequireVersion(manifest.SchemaVersion, "work record manifest", errors);
        ValidateDigest(manifest.SourceSemanticDigest, "sourceSemanticDigest", errors);
        if (manifest.Records.Count is < 1 or > 4096) errors.Add("Supply between 1 and 4096 work records.");
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (ChangeWorkRecord record in manifest.Records)
        {
            ValidatePublicId(record.RecordId, "recordId", errors);
            if (!ids.Add(record.RecordId)) errors.Add("Work record IDs must be unique.");
            ValidateWorkRecordDate(record.Date, errors);
            ValidateWorkRecordKind(record.Kind, errors);
            ValidateWorkRecordRepositories(record.RepositoryIds, errors);
            if (record.Kind == "implementation" && record.RepositoryIds.Count == 0)
                errors.Add("Implementation records require explicit aggregate repository relationships.");
            if (record.LoggedHours is < 0 or > 24) errors.Add("Logged hours must be between zero and 24; they are context only.");
        }
        return errors;
    }

    public static IReadOnlyList<string> Validate(ChangeWorkdayReviewReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        List<string> errors = [];
        if (report.WorkdayResolution is not null || report.Policy == ChangeDeclaredWorkdayReviewPolicies.Review)
            return ValidateDeclaredWorkdayReview(report);
        RequireVersion(report.SchemaVersion, "workday review", errors);
        if (report.AttributionCompleteness is { } completeness &&
            (completeness.Policy != "retained-attribution-completeness/1.0.0" || completeness.MissingEventDateCount < 0 || completeness.MissingReplayBaselineCount < 0 ||
                completeness.OriginalWorkdayStatus != ChangeWorkdayPolicies.Unresolved || completeness.IntermediateHistoryStatus != "unknown" ||
                (completeness.MissingEventDateCount > 0 || completeness.MissingReplayBaselineCount > 0
                    ? completeness.DeclaredEventStatus != "unresolved" : completeness.DeclaredEventStatus is not ("available" or "not-declared"))))
            errors.Add("Workday attribution state must preserve unresolved event and intermediate-history evidence.");
        ValidateDigest(report.SourceSemanticDigest, "sourceSemanticDigest", errors);
        ValidateDigest(report.SourcePortfolioDigest, "sourcePortfolioDigest", errors);
        ValidateDigest(report.WorkRecordInputDigest, "workRecordInputDigest", errors);
        ValidatePublicId(report.ContributorId, "contributorId", errors);
        RequireCanonicalText(report.TimeZone, "timeZone", 128, errors);
        ValidateWorkRecordRepositories(report.RepositoryIds, errors);
        if (report.Policy != ChangeWorkdayReviewPolicies.Review || report.Boundary != ChangeWorkdayReviewPolicies.Boundary ||
            report.ReferenceHoursPerDay != 8 || report.EntryPolicy is not null && report.EntryPolicy != ChangeWorkdayReviewPolicies.EqualEntries)
            errors.Add("Workday review must retain its explicit policy, boundary and fixed eight-hour reference.");
        if (report.Days.Count is < 1 or > 512 || report.Days.Sum(day => day.Records.Count) is < 1 or > 4096 ||
            report.Days.Select(day => day.Date).Distinct(StringComparer.Ordinal).Count() != report.Days.Count ||
            report.Days.Select(day => day.BucketId).Distinct(StringComparer.Ordinal).Count() != report.Days.Count)
            errors.Add("Workday review must have bounded unique days and records.");
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (ChangeWorkdayReviewDay day in report.Days)
        {
            ValidateWorkRecordDate(day.Date, errors);
            ValidatePublicId(day.BucketId, "bucketId", errors);
            if (day.WorkdayEvidenceBasis is not null || day.AllocatedExpectedHours is not null)
                errors.Add("Retained reviews cannot silently apply declared-date projections.");
            if (day.SourceAttributedExpectedHours < 0 || day.OriginalWorkdayStatus != ChangeWorkdayPolicies.Unresolved ||
                day.RetainedEvidenceStatus is not ("measured-retained-change" or "no-retained-change" or "scope-excluded" or "normalized-zero" or "reconciled-zero"))
                errors.Add("Review must distinguish retained evidence from unresolved original workdays.");
            string expectedStatus = report.AttributionCompleteness?.DeclaredEventStatus == "unresolved" && day.Records.Any(record => record.Kind is "implementation" or "mixed")
                ? "unresolved-event-attribution" : day.Records.Any(record => record.Kind == "mixed") ? "mixed-work-records-unresolved"
                : day.Records.Any(record => record.Kind == "implementation" &&
                    (record.RepositoryIds.Count != report.RepositoryIds.Count || record.RepositoryIds.Except(report.RepositoryIds, StringComparer.Ordinal).Any())) ? "repository-scope-unresolved"
                : day.Records.Any(record => record.Kind == "implementation") ? day.SourceAttributedExpectedHours > 0 ? "retained-attribution-available" : "unresolved-workday"
                : day.SourceAttributedExpectedHours > 0 ? "missing-work-record" : "no-implementation-record";
            if (day.Status != expectedStatus) errors.Add("Review status must expose missing, mixed, scope-mismatched and blank retained workdays.");
            decimal? expectedMultiplier = day.Status == "retained-attribution-available" && report.EntryPolicy is not null
                ? decimal.Round(day.SourceAttributedExpectedHours / 8m, 2, MidpointRounding.AwayFromZero) : null;
            if (day.MatchedDailyMultiplier != expectedMultiplier) errors.Add("Only explicitly allocated matched retained values may have a multiplier.");
            ChangeWorkRecordReview[] implementation = [.. day.Records.Where(record => record.Kind == "implementation").OrderBy(record => record.RecordId, StringComparer.Ordinal)];
            Dictionary<string, int> ranks = new(StringComparer.Ordinal);
            for (int rank = 0; rank < implementation.Length; rank++) ranks.TryAdd(implementation[rank].RecordId, rank);
            foreach (ChangeWorkRecordReview record in day.Records)
            {
                ValidatePublicId(record.RecordId, "recordId", errors);
                if (!ids.Add(record.RecordId)) errors.Add("Reviewed work record IDs must be unique.");
                ValidateWorkRecordKind(record.Kind, errors);
                ValidateWorkRecordRepositories(record.RepositoryIds, errors);
                string expectedRecordStatus = record.Kind is "meeting" or "pto" ? "excluded-non-implementation" : day.Status;
                if (record.Status != expectedRecordStatus) errors.Add("Record review must preserve exclusions and unresolved daily attribution.");
                decimal? expectedPart = null;
                if (record.Kind == "implementation" && expectedMultiplier is { } multiplier && multiplier <= decimal.MaxValue / 100)
                {
                    decimal cents = multiplier * 100;
                    int rank = ranks[record.RecordId];
                    expectedPart = (decimal.Floor(cents / implementation.Length) + (rank < cents % implementation.Length ? 1 : 0)) / 100;
                }
                if (record.AllocatedMultiplierContribution != expectedPart)
                    errors.Add("Entry contributions must be canonical two-decimal allocations; unresolved and excluded records cannot receive zero values.");
            }
            if (expectedMultiplier is { } total && day.Records.Sum(record => record.AllocatedMultiplierContribution ?? 0m) != total)
                errors.Add("Entry contributions must conserve the rounded daily multiplier.");
        }
        string expectedReportStatus = report.Days.Any(day => day.Status is not ("retained-attribution-available" or "no-implementation-record")) ? "unresolved" : "reviewed-retained-attribution";
        if (report.Status != expectedReportStatus) errors.Add("Review status must preserve unresolved discrepancies.");
        return errors;
    }

    private static void ValidateWorkRecordDate(string date, List<string> errors)
    {
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            errors.Add("Work record dates must be real yyyy-MM-dd local dates.");
    }

    private static void ValidateWorkRecordKind(string kind, List<string> errors)
    {
        if (kind is not ("implementation" or "meeting" or "pto" or "mixed")) errors.Add("Work record kind must be implementation, meeting, pto or mixed.");
    }

    private static void ValidateWorkRecordRepositories(IReadOnlyList<string> repositories, List<string> errors)
    {
        if (repositories.Count > 256 || repositories.Distinct(StringComparer.Ordinal).Count() != repositories.Count)
            errors.Add("Work record repository relationships must be bounded and unique.");
        foreach (string repository in repositories) ValidatePublicId(repository, "repositoryId", errors);
    }
}
