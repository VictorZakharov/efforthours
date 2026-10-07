using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    public static IReadOnlyList<string> Validate(ChangeHistoricalRefreshCheck check)
    {
        List<string> errors = [.. Validate(check.Plan), .. ValidateHistoricalRefreshManifest(check.Current, allowEmpty: true)];
        RequireVersion(check.SchemaVersion, "historical refresh check", errors);
        if (check.Policy != "historical-refresh-preflight/1.0.0" || !check.DryRun || !check.RequiresEntryConfirmation)
            errors.Add("Refresh preflight must remain read-only and require separate confirmation.");
        if (check.Current.SourceSemanticDigest != check.Plan.Input.SourceSemanticDigest ||
            check.Current.WorkRecordInputDigest != check.Plan.Input.WorkRecordInputDigest ||
            check.Current.SinceInclusiveDate != check.Plan.Input.SinceInclusiveDate || check.Current.UntilExclusiveDate != check.Plan.Input.UntilExclusiveDate)
            errors.Add("Refresh preflight must preserve exact lineage and date range.");
        if (errors.Count == 0 && ContractJson.SerializeCompact(check) != ContractJson.SerializeCompact(ChangeHistoricalRefreshPreflight.Evaluate(check.Plan, check.Current)))
            errors.Add("Refresh preflight must match complete snapshot, proposal and permission checks.");
        return errors;
    }
}
