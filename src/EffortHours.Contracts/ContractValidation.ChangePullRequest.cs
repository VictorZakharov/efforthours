using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidatePullRequestVerification(ChangeEvidence evidence, List<string> errors)
    {
        PullRequestReference? pullRequest = evidence.Selection.PullRequest;
        if (pullRequest is null)
        {
            return;
        }

        bool hasVerification = pullRequest.AnalyzedChangedPathCount is not null ||
            pullRequest.RepresentedChangedPathCount is not null ||
            pullRequest.PathCountStatus is not null;
        if (!hasVerification)
        {
            return;
        }

        if (pullRequest.AnalyzedChangedPathCount is null ||
            pullRequest.RepresentedChangedPathCount is null ||
            pullRequest.PathCountStatus is null)
        {
            errors.Add(
                "Pull-request path verification requires analyzed, represented, and status fields together.");
            return;
        }

        int analyzed = pullRequest.AnalyzedChangedPathCount.Value;
        int represented = pullRequest.RepresentedChangedPathCount.Value;
        if (analyzed != evidence.Paths.Count)
        {
            errors.Add("selection.pullRequest.analyzedChangedPathCount does not equal evidence.paths count.");
        }

        if (represented != evidence.Paths.Count(path => path.Represented))
        {
            errors.Add(
                "selection.pullRequest.representedChangedPathCount does not equal represented evidence.paths count.");
        }

        if (analyzed < 0 || represented < 0 || represented > analyzed)
        {
            errors.Add("Pull-request analyzed/represented path counts are inconsistent.");
        }

        int? provider = pullRequest.ProviderChangedFileCount;
        bool statusIsValid = pullRequest.PathCountStatus switch
        {
            PullRequestPathCountStatus.Match => provider is not null && provider.Value == analyzed,
            PullRequestPathCountStatus.Mismatch => provider is not null && provider.Value != analyzed,
            PullRequestPathCountStatus.ProviderUnavailable => provider is null,
            _ => false,
        };
        if (!statusIsValid)
        {
            errors.Add("selection.pullRequest.pathCountStatus is inconsistent with provider/analyzed counts.");
        }
    }
}
