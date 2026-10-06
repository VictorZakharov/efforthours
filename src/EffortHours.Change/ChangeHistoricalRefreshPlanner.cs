using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public static class ChangeHistoricalRefreshPlanner
{

    public static ChangeHistoricalRefreshPlan Plan(ChangeWorkdayReviewReport review,
        ChangeHistoricalRefreshManifest input, string fields = "notes")
    {
        RequireValid(ContractValidation.Validate(review));
        RequireValid(ContractValidation.Validate(input));
        if (fields is not ("notes" or "ehe" or "both")) throw new ArgumentException("Unknown refresh fields.");
        if (review.SourceSemanticDigest != input.SourceSemanticDigest || review.WorkRecordInputDigest != input.WorkRecordInputDigest)
            throw new ArgumentException("Refresh input must bind the exact completed source and work-record review.");
        var records = review.Days.SelectMany(day => day.Records.Select(record => (day, record)))
            .ToDictionary(value => value.record.RecordId, StringComparer.Ordinal);
        if (fields != "notes")
        {
            HashSet<string> selected = input.Entries.Select(value => value.RecordId).ToHashSet(StringComparer.Ordinal);
            if (review.WorkdayResolution is not null && review.Days.SelectMany(day => day.Records).Any(record => record.AllocatedMultiplierContribution is not null && !selected.Contains(record.RecordId)))
                throw new ArgumentException("Declared-date EHE refresh must select every contributing entry in the complete declared period.");
            foreach (ChangeWorkdayReviewDay day in review.Days.Where(day => day.Records.Any(record => selected.Contains(record.RecordId) && record.AllocatedMultiplierContribution is not null)))
                if (day.Records.Any(record => record.AllocatedMultiplierContribution is not null && !selected.Contains(record.RecordId)))
                    throw new ArgumentException("EHE refresh must select every matched entry for each affected day to conserve contributions.");
        }
        List<ChangeHistoricalRefreshProposal> proposals = [];
        foreach (ChangeHistoricalRefreshEntry entry in input.Entries.OrderBy(value => value.RecordId, StringComparer.Ordinal))
        {
            if (!records.TryGetValue(entry.RecordId, out var matched) ||
                string.CompareOrdinal(matched.day.Date, input.SinceInclusiveDate) < 0 ||
                string.CompareOrdinal(matched.day.Date, input.UntilExclusiveDate) >= 0)
                throw new ArgumentException("Every selected entry must exist in the reviewed records and explicit refresh range.");
            proposals.Add(ChangeHistoricalRefreshPolicy.Propose(review, entry, matched.day, matched.record, fields));
        }
        ChangeHistoricalRefreshPlan result = new()
        {
            RequestedFields = fields,
            Input = input with { Entries = [.. input.Entries.OrderBy(value => value.RecordId, StringComparer.Ordinal)] },
            Review = review,
            Proposals = proposals,
        };
        RequireValid(ContractValidation.Validate(result));
        return result;
    }

    private static void RequireValid(IReadOnlyList<string> errors)
    {
        if (errors.Count > 0) throw new ArgumentException("Invalid historical refresh input: " + string.Join(" ", errors));
    }
}
