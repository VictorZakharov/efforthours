using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public static class ChangePortfolioReplayReviewer
{
    public static async Task<IReadOnlyList<ChangePortfolioCandidate>> AttachAsync(
        GitAuthorPeriodManifestPortfolioPlan plan, IReadOnlyList<ChangePortfolioCandidate> candidates,
        EstimationProfile profile, bool engineeringScope, CancellationToken token)
    {
        ChangePortfolioCandidate[] result = [.. candidates];
        foreach (GitPortfolioReplayRangePlan range in plan.ReplayRanges)
        {
            int holder = Array.FindIndex(result, candidate => candidate.RepositoryId == range.RepositoryId &&
                candidate.Attribution.Replay?.EventId == range.Event.Id);
            if (holder < 0) continue;
            ChangePortfolioReplayEvent value = range.Event with { EventTimestamp = range.Event.EventTimestamp?.ToUniversalTime() };
            ChangeAuthorPeriodManifestRepository repository = plan.Manifest.Repositories.Single(repository => repository.Id == range.RepositoryId);
            ChangeRewriteReviewReport review = await new ChangeRewriteReviewer().ReviewAsync(new ChangeRewriteReviewManifest
            {
                RepositoryId = range.RepositoryId,
                RepositoryPath = range.RootPath,
                ScopeRepository = repository.ScopeRepository ?? repository.GitHubRepository ?? repository.Id,
                OriginalObjectId = value.OriginalObjectId,
                OldBaseObjectId = value.OldBaseObjectId,
                RewrittenObjectId = value.RewrittenObjectId,
                NewBaseObjectId = value.NewBaseObjectId,
                ReplayObjectId = value.ReplayObjectId,
                ReplayProvenanceId = value.ReplayProvenanceId,
                EventTimestamp = value.EventTimestamp,
                EventProvenanceId = value.EventProvenanceId,
                SinceInclusive = plan.Manifest.Selection.SinceInclusive,
                UntilExclusive = plan.Manifest.Selection.UntilExclusive,
            }, profile, engineeringScope, token).ConfigureAwait(false);
            ChangePortfolioReplayEvidence evidence = new()
            {
                Event = value,
                Review = review,
                OriginalObjectIds = range.OriginalObjectIds,
                RetainedObjectIds = range.RetainedObjectIds,
            };
            IReadOnlyList<string> errors = ContractValidation.Validate(evidence);
            if (errors.Count > 0) throw new InvalidOperationException("Portfolio replay evidence is incomplete or invalid: " + string.Join(" ", errors));
            result[holder] = result[holder] with { ReplayEvidence = evidence };
        }
        return result;
    }
}
