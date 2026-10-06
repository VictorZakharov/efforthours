using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class ChangePortfolioGroupNormalizer
{
    private static List<ChangePortfolioReplayAllocation> AllocateReplayRanges(
        IReadOnlyList<ChangePortfolioItemDraft> drafts)
    {
        List<ChangePortfolioReplayAllocation> allocations = [];
        foreach (ChangePortfolioReplayEvidence evidence in drafts.Select(draft => draft.Candidate.ReplayEvidence)
            .OfType<ChangePortfolioReplayEvidence>().OrderBy(value => value.Event.Id, StringComparer.Ordinal))
        {
            IReadOnlyList<string> errors = ContractValidation.Validate(evidence);
            if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors), nameof(drafts));
            ChangePortfolioItemDraft[] members = [.. drafts.Where(draft => draft.Candidate.Attribution.Replay?.EventId == evidence.Event.Id)];
            decimal budget = members.Sum(draft => draft.AllocatedExpectedHours);
            decimal? novel = evidence.Review.Comparisons.SingleOrDefault(value => value.Role == "novel-retained-delta")?.Effort.Expected;
            bool resolved = evidence.Event.ReplayObjectId is not null && evidence.Event.EventTimestamp is not null;
            decimal? allocated = null;
            decimal? reserved = resolved ? Math.Min(budget, evidence.Review.Comparisons.Single(value => value.Role == "original-implementation").Effort.Expected) : null;
            string status = evidence.Review.EventAttributionStatus;
            if (resolved)
            {
                PreserveOriginalDuplicateDates();
                decimal eventBudget = Math.Min(novel!.Value, budget - reserved!.Value);
                Distribute("original", budget - eventBudget);
                Distribute("retained", eventBudget);
                bool inside = evidence.Event.EventTimestamp >= evidence.Review.SinceInclusive && evidence.Event.EventTimestamp < evidence.Review.UntilExclusive;
                allocated = inside ? eventBudget : 0m;
                status = inside ? "allocated-under-declared-replay" : "event-outside-period";
            }
            allocations.Add(new()
            {
                Evidence = evidence,
                Status = status,
                AvailableJointExpectedHours = budget,
                ReservedOriginalExpectedHours = reserved,
                StandaloneNovelExpectedHours = novel,
                AllocatedEventExpectedHours = allocated,
                AllocationCapped = resolved && novel > budget - reserved!.Value,
            });

            void PreserveOriginalDuplicateDates()
            {
                foreach (ChangePortfolioItemDraft original in members.Where(draft => draft.Candidate.Attribution.Replay!.Role == "original" && draft.DuplicateOfItemId is not null))
                {
                    ChangePortfolioItemDraft? keeper = members.SingleOrDefault(draft => draft.Id == original.DuplicateOfItemId && draft.Candidate.Attribution.Replay!.Role == "retained");
                    if (keeper is null || drafts.Any(draft => draft != original && draft.DuplicateOfItemId == keeper.Id &&
                        draft.Candidate.Attribution.HeadIds!.Intersect(original.Candidate.Attribution.HeadIds!, StringComparer.Ordinal).Any())) continue;
                    foreach (ChangePortfolioItemDraft duplicate in drafts.Where(draft => draft.DuplicateOfItemId == keeper.Id))
                        duplicate.DuplicateOfItemId = original.Id;
                    original.DuplicateOfItemId = null;
                    original.AllocatedExpectedHours = keeper.AllocatedExpectedHours;
                    keeper.AllocatedExpectedHours = 0m;
                    keeper.DuplicateOfItemId = original.Id;
                    original.UncertaintyReasons.Add("The original equivalent patch owns its declared range allocation date; ordinary keeper evidence already fixed the conserved canonical budget.");
                }
            }

            void Distribute(string role, decimal total)
            {
                ChangePortfolioItemDraft[] active = [.. members.Where(draft => draft.Candidate.Attribution.Replay!.Role == role && !draft.Suppressed)
                    .OrderBy(draft => draft.Id, StringComparer.Ordinal)];
                if (active.Length == 0 && total > 0m)
                    throw new InvalidOperationException("Replay allocation has no active evidence for a positive role budget; competing representations require review, not a fabricated zero or duplicate charge.");
                decimal weight = active.Sum(draft => draft.Candidate.Report.TotalEffort.Expected);
                decimal remaining = total;
                for (int index = 0; index < active.Length; index++)
                {
                    decimal share = index == active.Length - 1 ? remaining : Math.Min(remaining, decimal.Round(
                        weight == 0m ? total / active.Length : total * active[index].Candidate.Report.TotalEffort.Expected / weight,
                        2, MidpointRounding.AwayFromZero));
                    active[index].AllocatedExpectedHours = share; remaining -= share;
                }
            }
        }
        if (drafts.Any(draft => draft.Candidate.Attribution.Replay is { } replay &&
            !allocations.Any(allocation => allocation.Evidence.Event.Id == replay.EventId)))
            throw new ArgumentException("Replay member rows require one canonical replay review.", nameof(drafts));
        return allocations;
    }

    private static bool IsRewriteSupport(ChangePortfolioItemDraft draft) =>
        draft.Candidate.Attribution.Rewrite?.SupportOnly == true || draft.Candidate.Attribution.Replay?.SupportOnly == true;
}
