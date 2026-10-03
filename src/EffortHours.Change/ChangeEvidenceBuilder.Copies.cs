using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class ChangeEvidenceBuilder
{
    private static void CollapseAddedCopies(List<ChangePathEvidence> paths)
    {
        HashSet<string> copies = paths.Where(path => path.Represented && path.Status == ChangePathStatus.Added && path.HeadObjectId is not null)
            .GroupBy(path => path.HeadObjectId, StringComparer.Ordinal)
            .SelectMany(group => group.OrderBy(path => path.Path, StringComparer.Ordinal).Skip(1))
            .Select(path => path.Id).ToHashSet(StringComparer.Ordinal);
        for (int index = 0; index < paths.Count; index++)
        {
            ChangePathEvidence path = paths[index];
            if (!copies.Contains(path.Id)) continue;
            paths[index] = path with
            {
                Classification = ChangePathClassification.ExactDuplicate,
                Represented = false,
                EditRegions = 0,
                Reason = "This added body is byte-identical to another represented addition; one deterministic representative carries its effort.",
                Tags = [.. path.Tags.Where(tag => tag != "classification:represented").Append("classification:exact-duplicate").Order(StringComparer.Ordinal)],
            };
        }
    }
}
