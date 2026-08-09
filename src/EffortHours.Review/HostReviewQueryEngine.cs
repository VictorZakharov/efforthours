using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Review;

public static class HostReviewQueryEngine
{
    public static async Task<HostReviewQueryResult> ExecuteAsync(
        EstimateReport report,
        RepositoryEvidence evidence,
        HostReviewQuery query,
        HostReviewSourceContext? sourceContext = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        if (query.Kind == HostReviewQueryKind.SelectedSource)
        {
            query = query with
            {
                Selector = SelectedSourceReader.NormalizeSelector(query.Selector),
            };
        }

        IReadOnlyList<string> queryErrors = ContractValidation.Validate(query);
        if (queryErrors.Count > 0)
        {
            throw new ArgumentException(
                "The host-review query is invalid: " + string.Join(" ", queryErrors),
                nameof(query));
        }

        if (report.Profile != query.Profile)
        {
            throw new ArgumentException(
                "The host-review query profile does not match the local estimate.",
                nameof(query));
        }

        string inputDigest = HostReviewInputDigest.Compute(
            report,
            evidence,
            cancellationToken);
        if (!string.Equals(inputDigest, query.InputDigest, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The host-review query input digest does not match the current estimate and evidence.",
                nameof(query));
        }

        HostReviewQueryResult result = query.Kind switch
        {
            HostReviewQueryKind.Capability => CapabilityResult(
                report,
                evidence,
                query,
                cancellationToken),
            HostReviewQueryKind.Evidence => EvidenceResult(evidence, query),
            HostReviewQueryKind.Scope => ScopeResult(
                report,
                evidence,
                query,
                cancellationToken),
            HostReviewQueryKind.SelectedSource => await SourceResultAsync(
                evidence,
                query,
                sourceContext,
                cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(query)),
        };

        IReadOnlyList<string> errors = ContractValidation.Validate(result);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "The host-review query result is invalid: " + string.Join(" ", errors));
        }

        return result;
    }

    private static HostReviewQueryResult CapabilityResult(
        EstimateReport report,
        RepositoryEvidence evidence,
        HostReviewQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EstimateExplanation explanation = EstimateProjector.Explain(
            report,
            evidence,
            query.Selector);
        IReadOnlyList<string> reviewReasons = FindReviewReasons(report, query.Selector);
        HostReviewCandidate candidate = HostReviewCandidateFactory.Create(
            explanation,
            reviewReasons);
        EvidenceFact[] facts =
        [
            .. explanation.EvidenceFacts
                .Take(HostReviewProtocol.MaximumQueryEvidenceFacts),
        ];

        return CreateResult(
            report.Repository,
            query,
            capabilities: [candidate],
            evidenceFacts: facts,
            omissions: new HostReviewQueryOmissions
            {
                CapabilityCount = 0,
                EvidenceFactCount = explanation.EvidenceFacts.Count - facts.Length,
                SourceLineCount = 0,
                SourceCharacterCount = 0,
            });
    }

    private static HostReviewQueryResult EvidenceResult(
        RepositoryEvidence evidence,
        HostReviewQuery query)
    {
        EvidenceFact fact = evidence.Facts.SingleOrDefault(candidate =>
            candidate.Id == query.Selector)
            ?? throw new KeyNotFoundException(
                $"No evidence fact has ID '{query.Selector}'.");
        return CreateResult(
            evidence.Repository,
            query,
            evidenceFacts: [fact]);
    }

    private static HostReviewQueryResult ScopeResult(
        EstimateReport report,
        RepositoryEvidence evidence,
        HostReviewQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EstimateViewReport allCapabilities = EstimateProjector.Project(
            report,
            EstimateViewKind.WorkItem);
        CapabilityViewEntry[] matches =
        [
            .. allCapabilities.Capabilities
                .Where(capability => capability.Scope == query.Selector)
                .OrderByDescending(capability => capability.Hours.Expected)
                .ThenBy(capability => capability.Id, StringComparer.Ordinal),
        ];
        if (matches.Length == 0)
        {
            throw new KeyNotFoundException(
                $"No estimated capability has scope '{query.Selector}'.");
        }

        int offset = query.Offset!.Value;
        int limit = query.Limit!.Value;
        List<HostReviewCandidate> selected = [];
        foreach (CapabilityViewEntry capability in matches.Skip(offset).Take(limit))
        {
            cancellationToken.ThrowIfCancellationRequested();
            selected.Add(HostReviewCandidateFactory.Create(
                EstimateProjector.Explain(report, evidence, capability.Id),
                FindReviewReasons(report, capability.Id)));
        }
        return CreateResult(
            report.Repository,
            query,
            capabilities: selected,
            omissions: new HostReviewQueryOmissions
            {
                CapabilityCount = matches.Length - selected.Count,
                EvidenceFactCount = 0,
                SourceLineCount = 0,
                SourceCharacterCount = 0,
            });
    }

    private static async Task<HostReviewQueryResult> SourceResultAsync(
        RepositoryEvidence evidence,
        HostReviewQuery query,
        HostReviewSourceContext? sourceContext,
        CancellationToken cancellationToken)
    {
        if (sourceContext is null)
        {
            throw new InvalidOperationException(
                "A selected-source query requires a physical or virtual repository source context.");
        }

        SelectedSourceReadResult selected = await SelectedSourceReader.ReadAsync(
            sourceContext,
            evidence,
            query.Selector,
            query.StartLine!.Value,
            query.LineCount!.Value,
            cancellationToken).ConfigureAwait(false);
        HostReviewQuery canonicalQuery = query with
        {
            Selector = selected.Excerpt.Path,
        };
        return CreateResult(
            evidence.Repository,
            canonicalQuery,
            evidenceFacts: [selected.Fact],
            sourceExcerpt: selected.Excerpt,
            omissions: new HostReviewQueryOmissions
            {
                CapabilityCount = 0,
                EvidenceFactCount = 0,
                SourceLineCount = selected.OmittedLines,
                SourceCharacterCount = selected.OmittedCharacters,
            });
    }

    private static HostReviewQueryResult CreateResult(
        RepositoryDescriptor repository,
        HostReviewQuery query,
        IReadOnlyList<HostReviewCandidate>? capabilities = null,
        IReadOnlyList<EvidenceFact>? evidenceFacts = null,
        HostReviewSourceExcerpt? sourceExcerpt = null,
        HostReviewQueryOmissions? omissions = null) => new()
        {
            InputDigest = query.InputDigest,
            Repository = repository,
            Query = query,
            Capabilities = capabilities ?? [],
            EvidenceFacts = evidenceFacts ?? [],
            SourceExcerpt = sourceExcerpt,
            Omissions = omissions ?? new HostReviewQueryOmissions
            {
                CapabilityCount = 0,
                EvidenceFactCount = 0,
                SourceLineCount = 0,
                SourceCharacterCount = 0,
            },
            ContainsSourceExcerpt = sourceExcerpt is not null,
            DisclosureNotice = sourceExcerpt is null
                ? "This result contains metadata and estimator reasoning, but no source excerpt."
                : "This result contains an explicitly requested source excerpt; provider disclosure remains the caller's responsibility.",
        };

    private static IReadOnlyList<string> FindReviewReasons(
        EstimateReport report,
        string capabilityId)
    {
        EstimateViewReport review = EstimateProjector.Project(report, EstimateViewKind.Review);
        return review.ReviewQueue.SingleOrDefault(candidate => candidate.Id == capabilityId)?
            .ReviewReasons ?? [];
    }
}
