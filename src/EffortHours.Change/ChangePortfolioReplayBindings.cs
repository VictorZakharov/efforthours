using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public static class ChangePortfolioReplayBindings
{
    public static void Validate(IReadOnlyList<ChangePortfolioCandidate> candidates, EstimationProfile profile)
    {
        if (!candidates.Any(candidate => candidate.Attribution.Replay is not null || candidate.ReplayEvidence is not null)) return;
        Dictionary<(string Repository, string Event), ChangePortfolioReplayEvidence> evidenceByEvent = [];
        foreach (ChangePortfolioCandidate holder in candidates.Where(candidate => candidate.ReplayEvidence is not null))
        {
            ChangePortfolioReplayEvidence evidence = holder.ReplayEvidence!;
            if (ContractValidation.Validate(evidence).Count > 0 || evidence.Review.RepositoryId != holder.RepositoryId ||
                holder.Attribution.Replay?.EventId != evidence.Event.Id ||
                evidence.Review.Comparisons.Any(comparison => comparison.Profile != profile || comparison.EstimatorVersion != holder.Report.EstimatorVersion) ||
                !evidenceByEvent.TryAdd((holder.RepositoryId, evidence.Event.Id), evidence))
                throw new ArgumentException("Replay evidence must uniquely bind its repository, source profile/estimator and member rows.", nameof(candidates));
        }
        foreach (IGrouping<(string, string), ChangePortfolioCandidate> group in candidates.Where(candidate => candidate.Attribution.Replay is not null)
            .GroupBy(candidate => (candidate.RepositoryId, candidate.Attribution.Replay!.EventId)))
        {
            if (!evidenceByEvent.TryGetValue(group.Key, out ChangePortfolioReplayEvidence? evidence))
                throw new ArgumentException("Replay member rows are missing their canonical review.", nameof(candidates));
            bool resolved = evidence.Event.ReplayObjectId is not null && evidence.Event.EventTimestamp is not null;
            HashSet<string> seen = new(StringComparer.Ordinal);
            foreach (ChangePortfolioCandidate candidate in group)
            {
                ChangePortfolioReplayAttribution attribution = candidate.Attribution.Replay!;
                IReadOnlyList<string> ids = attribution.Role == "original" ? evidence.OriginalObjectIds : evidence.RetainedObjectIds;
                int index = -1;
                for (int slot = 0; slot < ids.Count; slot++) if (ids[slot] == candidate.Report.Selection.Head.ObjectId) index = slot;
                string before = index <= 0 ? attribution.Role == "original" ? evidence.Event.OldBaseObjectId : evidence.Event.NewBaseObjectId : ids[index - 1];
                if (index < 0 || !seen.Add(candidate.Report.Selection.Head.ObjectId) || attribution.Role is not ("original" or "retained") ||
                    candidate.Report.Selection.Kind != ChangeSelectionKind.Commit || candidate.Report.Selection.Base.ObjectId != before ||
                    candidate.Attribution.ParentCount != 1 || candidate.Attribution.MergeCommit || candidate.Attribution.Rewrite is not null)
                    throw new ArgumentException("Replay members require disjoint exact first-parent canonical commit reports.", nameof(candidates));
            }
            if (resolved && seen.Count != evidence.OriginalObjectIds.Count + evidence.RetainedObjectIds.Count)
                throw new ArgumentException("Resolved replay attribution requires complete original and retained range membership.", nameof(candidates));
        }
    }
}
