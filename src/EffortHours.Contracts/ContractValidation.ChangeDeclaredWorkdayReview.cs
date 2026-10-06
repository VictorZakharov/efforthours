using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static List<string> ValidateDeclaredWorkdayReview(ChangeWorkdayReviewReport report)
    {
        List<string> errors = [];
        if (report.Policy != ChangeDeclaredWorkdayReviewPolicies.Review || report.Boundary != ChangeDeclaredWorkdayReviewPolicies.Boundary ||
            report.WorkdayResolution is not { Policy: "declared-workday-resolution/1.0.0" } resolution)
        { errors.Add("Declared reviews require their explicit policy, boundary and complete resolution receipt."); return errors; }
        ChangeWorkdayReviewReport retained = ChangeDeclaredWorkdayReviewPolicy.RetainedProjection(report);
        errors.AddRange(Validate(retained));
        errors.AddRange(Validate(resolution.Manifest));
        errors.AddRange(Validate(resolution.Allocation));
        if (errors.Count > 0) return errors;
        try
        {
            ChangeWorkdayReviewReport expected = ChangeDeclaredWorkdayReviewPolicy.Apply(retained, resolution.Manifest, resolution.Allocation, report.EntryPolicy);
            if (ContractJson.SerializeCompact(expected) != ContractJson.SerializeCompact(report))
                errors.Add("Declared review must preserve exact source/declaration lineage, external-date states and conserved period/entry contributions.");
        }
        catch (ArgumentException) { errors.Add("Declared workdays must anchor complete in-scope implementation records and exact source values."); }
        return errors;
    }
}
