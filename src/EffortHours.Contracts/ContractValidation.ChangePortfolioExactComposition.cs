using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidateExactComposition(
        ChangePortfolioItemEstimate item,
        Dictionary<string, ChangePortfolioItemEstimate> items,
        List<string> errors)
    {
        if (item.ExactComposition is not { } proof)
        {
            return;
        }

        if (proof.Protocol != "exact-retained-composition/1.0.0" ||
            proof.ItemIds.Count is < 2 or > 256 ||
            proof.ItemIds.Distinct(StringComparer.Ordinal).Count() != proof.ItemIds.Count ||
            proof.ItemIds.Contains(item.Id, StringComparer.Ordinal) ||
            proof.PatchDigest != item.PatchDigest || item.DuplicateOfItemId is not null ||
            item.AllocatedExpectedHours != 0m)
        {
            errors.Add($"Portfolio item '{item.Id}' has invalid exact composition provenance.");
        }

        ChangePortfolioItemEstimate? previous = null;
        foreach (string id in proof.ItemIds)
        {
            if (!items.TryGetValue(id, out ChangePortfolioItemEstimate? member) ||
                member.RepositoryId != item.RepositoryId || member.DuplicateOfItemId is not null ||
                member.ExactComposition is not null || member.Selection.Kind != ChangeSelectionKind.Commit ||
                member.Attribution.ParentCount > 1 ||
                previous is not null && previous.Selection.Head.ObjectId != member.Selection.Base.ObjectId)
            {
                errors.Add($"Portfolio item '{item.Id}' has an invalid retained composition member.");
            }

            previous = member;
        }
    }
}
