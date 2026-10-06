using System.Globalization;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

/// <summary>An execution-owned, digest-bound canonical endpoint estimate.</summary>
public sealed record ChangePortfolioFinalDelta
{
    public const string Policy = "selected-final-delta/1.1.0";

    public required IReadOnlyList<string> SelectorIds { get; init; }

    public required string InputDigest { get; init; }

    public required string RawDeltaDigest { get; init; }

    public required ChangeEstimateReport Report { get; init; }

    public bool Matches(IReadOnlyDictionary<string, ChangePortfolioCandidate> candidates)
    {
        HashSet<string> selected = SelectorIds.ToHashSet(StringComparer.Ordinal);
        ChangePortfolioItemDraft[] active = [.. SelectorIds.Where(candidates.ContainsKey)
            .Select(selector => ChangePortfolioIdentity.CreateDraft(candidates[selector]))];
        if (selected.Count != SelectorIds.Count || active.Length != selected.Count || active.Length < 2)
            return false;
        try
        {
            ChangePortfolioItemDraft owner = ChangePortfolioIdentity.CreateDraft(active[0].Candidate with { FinalDelta = this });
            return ChangePortfolioFinalDeltaProof.Find([owner], active) is not null;
        }
        catch (InvalidOperationException) { return false; }
    }
}

public sealed partial class ChangeEstimator
{
    /// <summary>
    /// Re-estimates exact selected final effects, never a range containing
    /// unselected effects. Failed structural proofs retain ordinary reconciliation.
    /// Snapshot acquisition and analysis failures propagate to the caller.
    /// </summary>
    public async Task<IReadOnlyList<ChangePortfolioCandidate>> PreparePortfolioFinalDeltasAsync(
        ChangePortfolioSelection selection,
        IReadOnlyList<ChangePortfolioCandidate> candidates,
        EstimationProfile profile,
        Func<string, string, CancellationToken, Task<IChangeSnapshot>> openSnapshot,
        bool independentDays = false,
        ChangePathAdmission? pathAdmission = null,
        ChangePortfolioExecutionTelemetry? telemetry = null,
        Action<ChangePortfolioExecutionStatistics>? observeStatistics = null,
        CancellationToken cancellationToken = default)
    {
        if (selection.Kind != ChangePortfolioSelectionKind.AuthorPeriod)
        {
            return candidates;
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (independentDays && selection.AuthorPeriodManifest is null)
            throw new ArgumentException("Independent days require a manifest author-period selection.");
        SnapshotAnalysisCache analyses = new();
        TimeZoneInfo? zone = independentDays
            ? TimeZoneInfo.FindSystemTimeZoneById(selection.AuthorPeriodManifest!.TimeZone)
            : null;
        ChangePortfolioCandidate[] prepared = [.. candidates.Select(candidate => candidate with { FinalDelta = null, FinalDeltaRejection = null })];
        var groups = prepared.Select((candidate, index) => (Candidate: candidate, Index: index))
            .GroupBy(item => (item.Candidate.RepositoryId, Date: zone is null ? "" :
                TimeZoneInfo.ConvertTime(item.Candidate.Attribution.SelectedTimestamp!.Value, zone)
                    .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))
            .OrderBy(group => group.Key.RepositoryId, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Date, StringComparer.Ordinal);
        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ChangePortfolioItemDraft[] drafts = [.. group.Select(item =>
                ChangePortfolioIdentity.CreateDraft(item.Candidate)).OrderBy(draft => draft.Id, StringComparer.Ordinal)];
            ChangePortfolioExactCompositionNormalizer.Mark(drafts);
            ChangePortfolioCandidate[] active = [.. drafts.Where(draft => !draft.Suppressed)
                .OrderBy(draft => ChangePortfolioIdentity.EvidenceTimestamp(draft.Candidate))
                .ThenBy(draft => draft.Id, StringComparer.Ordinal).Select(draft => draft.Candidate)];
            int owner = group.OrderBy(item => item.Candidate.SelectorId, StringComparer.Ordinal).First().Index;
            void Reject(string code, string? path = null) => prepared[owner] = prepared[owner] with
            {
                FinalDeltaRejection = new ChangePortfolioFinalDeltaRejection
                {
                    Code = code,
                    InputDigest = ChangePortfolioFinalDeltaProof.InputDigest(group.Select(item => item.Candidate)),
                    PathDigest = path is null ? null : ChangePortfolioIdentity.Digest(path),
                },
            };
            if (active.Length < 2) continue;
            ChangePathEvidence? unsupported = drafts.SelectMany(draft => draft.Candidate.Report.Evidence.Paths)
                .Where(path => path.Classification == ChangePathClassification.Unsupported)
                .OrderBy(path => path.Path, StringComparer.Ordinal).FirstOrDefault();
            if (unsupported is not null)
            {
                Reject("unsupported-mode", unsupported.Path);
                continue;
            }
            if (!ChangePortfolioFinalDeltaProof.SuppressionPreservesRawEffects(drafts))
            {
                Reject("suppressed-raw-mismatch");
                continue;
            }
            if (!ChangePortfolioFinalDeltaProof.TryCompose(active, out var effects, out string? path))
            {
                Reject("composition-unproven", path);
                continue;
            }
            using IDisposable? phase = telemetry?.Measure(ChangePortfolioExecutionPhases.Reconciliation);
            var endpoint = await ChangePortfolioEndpointSearch.FindAsync(group.Key.RepositoryId, active, effects,
                openSnapshot, pathAdmission, cancellationToken).ConfigureAwait(false);
            if (endpoint.Pair is null)
            {
                Reject(endpoint.Code, endpoint.Path);
                continue;
            }
            var (before, after, baseSnapshot, headSnapshot) = endpoint.Pair;
            await using IChangeSnapshot ownedBase = baseSnapshot;
            await using IChangeSnapshot ownedHead = headSnapshot;
            ChangeEstimateReport report = await EstimateCoreAsync(new ChangeEstimateInput
            {
                RepositoryName = group.Key.RepositoryId,
                Selection = new ChangeSelection
                {
                    Kind = ChangeSelectionKind.BaseHead,
                    Base = before,
                    Head = after,
                },
                OpenBaseAsync = _ => Task.FromResult(baseSnapshot),
                OpenHeadAsync = _ => Task.FromResult(headSnapshot),
                PathAdmission = pathAdmission,
            }, profile, null, analyses, "final-delta:" + group.Key.RepositoryId, telemetry, ownsSnapshots: false, cancellationToken).ConfigureAwait(false);
            prepared[owner] = prepared[owner] with
            {
                FinalDelta = new ChangePortfolioFinalDelta
                {
                    SelectorIds = [.. active.Select(candidate => candidate.SelectorId).Order(StringComparer.Ordinal)],
                    InputDigest = ChangePortfolioFinalDeltaProof.InputDigest(active),
                    RawDeltaDigest = ChangePortfolioIdentity.PatchDigest(effects.Values.Where(effect =>
                        effect.BaseState != effect.HeadState)),
                    Report = report,
                },
            };
        }
        ExecutionStatisticsAccumulator statistics = new(0);
        statistics.Add(analyses.GetStatistics());
        observeStatistics?.Invoke(statistics.Build());
        return prepared;
    }
}
