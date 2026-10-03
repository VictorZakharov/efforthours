using System.Globalization;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

/// <summary>An execution-owned, digest-bound canonical endpoint estimate.</summary>
public sealed record ChangePortfolioFinalDelta
{
    public const string Policy = "selected-final-delta/1.0.0";

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
        ChangePortfolioCandidate[] prepared = [.. candidates.Select(candidate => candidate with { FinalDelta = null })];
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
                .OrderBy(draft => draft.Candidate.Attribution.SelectedTimestamp)
                .ThenBy(draft => draft.Id, StringComparer.Ordinal).Select(draft => draft.Candidate)];
            if (active.Length < 2 || !ChangePortfolioFinalDeltaProof.TryCompose(active, out var effects))
            {
                continue;
            }
            ChangeSnapshotReference before = active[0].Report.Selection.Base;
            ChangeSnapshotReference after = active[^1].Report.Selection.Head;
            using IDisposable? phase = telemetry?.Measure(ChangePortfolioExecutionPhases.Reconciliation);
            await using IChangeSnapshot baseSnapshot = await openSnapshot(group.Key.RepositoryId,
                before.ObjectId, cancellationToken).ConfigureAwait(false);
            await using IChangeSnapshot headSnapshot = await openSnapshot(group.Key.RepositoryId,
                after.ObjectId, cancellationToken).ConfigureAwait(false);
            if (baseSnapshot.ObjectId != before.ObjectId || headSnapshot.ObjectId != after.ObjectId)
            {
                throw new InvalidOperationException("Final-delta snapshot identity does not match its immutable selector.");
            }
            if (!ChangePortfolioFinalDeltaProof.MatchesInventories(effects, baseSnapshot, headSnapshot, pathAdmission))
            {
                continue;
            }
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
            int owner = group.OrderBy(item => item.Candidate.SelectorId, StringComparer.Ordinal).First().Index;
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
