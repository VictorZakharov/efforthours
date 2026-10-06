using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public static class ChangeDeclaredWorkdayReviewer
{
    public static ChangeWorkdayReviewReport Review(ChangePortfolioComparisonReport source,
        ChangeWorkRecordManifest records, ChangeWorkdayManifest workdays, string workdayPolicy, string? entryPolicy = null)
    {
        ChangeWorkdayReviewReport retained = ChangeWorkdayReviewer.Review(source, records);
        ChangeWorkdayAllocationReport allocation = ChangeWorkdayAllocator.Allocate(source, workdays, workdayPolicy);
        ChangeWorkdayReviewReport result = ChangeDeclaredWorkdayReviewPolicy.Apply(retained, workdays, allocation, entryPolicy);
        if (ContractValidation.Validate(result).Count > 0) throw new ArgumentException("Invalid declared-workday review output.");
        return result;
    }
}
