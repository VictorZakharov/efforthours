using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidateRewriteEvents(IReadOnlyList<ChangeRewriteEvent>? events, List<string> errors)
    {
        if (events is null) return;
        if (events.Count is < 1 or > 32)
            errors.Add("Rewrite evidence requires between 1 and 32 disjoint single-commit pairs per repository.");
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (ChangeRewriteEvent evidence in events)
        {
            ValidateRewriteEvent(evidence, errors);
            if (!seen.Add(evidence.OriginalObjectId) || !seen.Add(evidence.RewrittenObjectId))
                errors.Add("Rewrite pairs must be disjoint; chained or competing pairings are unsupported.");
        }
    }

    private static void ValidateRewriteEvent(ChangeRewriteEvent evidence, List<string> errors)
    {
        foreach (string objectId in new[] { evidence.OriginalObjectId, evidence.RewrittenObjectId,
            evidence.OldBaseObjectId, evidence.NewBaseObjectId })
            if (!IsObjectId(objectId)) errors.Add("Rewrite object IDs must be full immutable Git IDs.");
        if (evidence.AttributionPolicy != ChangeRewriteEvent.Policy ||
            evidence.OriginalObjectId == evidence.RewrittenObjectId ||
            evidence.EventTimestamp?.Offset != null && evidence.EventTimestamp.Value.Offset != TimeSpan.Zero)
            errors.Add("Rewrite evidence requires the declared event policy, distinct commits and a UTC event instant.");
    }
    private static void ValidateRewriteAttribution(ChangePortfolioItemEstimate item, List<string> errors)
    {
        if (item.Attribution.Rewrite is not { } rewrite) return;
        ValidateRewriteEvent(rewrite.Evidence, errors);
        string expectedHead = rewrite.Role == "original" ? rewrite.Evidence.OriginalObjectId : rewrite.Evidence.RewrittenObjectId;
        string expectedBase = rewrite.Role == "original" ? rewrite.Evidence.OldBaseObjectId : rewrite.Evidence.NewBaseObjectId;
        if (rewrite.Role is not ("original" or "rewritten") ||
            rewrite.Basis != "caller-declared-immutable-pair" || rewrite.Confidence != "declared-not-verified-workday" ||
            item.Selection.Head.ObjectId != expectedHead || item.Selection.Base.ObjectId != expectedBase ||
            rewrite.OriginalAuthorTimestamp.Offset != TimeSpan.Zero || rewrite.RewrittenCommitterTimestamp.Offset != TimeSpan.Zero ||
            rewrite.SupportOnly && item.AllocatedExpectedHours != 0m ||
            rewrite.Treatment is not ("evidence-support" or "no-retained-increment" or "original-contribution" or "retained-resolution-contribution"))
            errors.Add("Rewrite attribution must bind the exact pair, timestamps, declared confidence, role and zero-cost support treatment.");
    }

}
