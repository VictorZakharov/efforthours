using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidatePortfolioReplayEvents(IReadOnlyList<ChangePortfolioReplayEvent>? events, List<string> errors)
    {
        if (events is null) return;
        if (events.Count is < 1 or > 32 || events.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != events.Count)
            errors.Add("Replay ranges require 1 to 32 unique event IDs per repository.");
        foreach (ChangePortfolioReplayEvent value in events) ValidatePortfolioReplayEvent(value, errors);
    }

    private static void ValidatePortfolioReplayEvent(ChangePortfolioReplayEvent value, List<string> errors)
    {
        ValidatePublicId(value.Id, "replay event ID", errors);
        if (value.AttributionPolicy != ChangePortfolioReplayEvent.Policy) errors.Add("Unknown replay range allocation policy.");
        foreach (string id in new[] { value.OldBaseObjectId, value.OriginalObjectId, value.NewBaseObjectId, value.RewrittenObjectId }
            .Concat(value.ReplayObjectId is null ? [] : new[] { value.ReplayObjectId }))
            if (!IsObjectId(id)) errors.Add("Replay ranges require full immutable Git IDs.");
        if (value.OldBaseObjectId == value.OriginalObjectId || value.NewBaseObjectId == value.RewrittenObjectId || value.OriginalObjectId == value.RewrittenObjectId)
            errors.Add("Replay ranges require distinct non-empty original and retained endpoints.");
        ValidateReplayProvenance(value.ReplayObjectId is not null, value.ReplayProvenanceId, value.EventTimestamp, value.EventProvenanceId, errors);
    }

    public static IReadOnlyList<string> Validate(ChangePortfolioReplayEvidence evidence)
    {
        List<string> errors = [.. Validate(evidence.Review)];
        ValidatePortfolioReplayEvent(evidence.Event, errors);
        ChangePortfolioReplayEvent value = evidence.Event;
        foreach (IReadOnlyList<string> ids in new[] { evidence.OriginalObjectIds, evidence.RetainedObjectIds })
            if (ids.Count is < 1 or > 1024 || ids.Any(id => !IsObjectId(id)) || ids.Distinct(StringComparer.Ordinal).Count() != ids.Count)
                errors.Add("Replay membership requires complete bounded immutable ranges.");
        if (evidence.OriginalObjectIds.Intersect(evidence.RetainedObjectIds, StringComparer.Ordinal).Any() ||
            (evidence.OriginalObjectIds.Count == 0 ? null : evidence.OriginalObjectIds[^1]) != value.OriginalObjectId || (evidence.RetainedObjectIds.Count == 0 ? null : evidence.RetainedObjectIds[^1]) != value.RewrittenObjectId ||
            evidence.Review.OriginalCommitCount != evidence.OriginalObjectIds.Count || evidence.Review.RewrittenCommitCount != evidence.RetainedObjectIds.Count)
            errors.Add("Replay range members must bind disjoint complete endpoint ranges.");
        if (evidence.Review.EventTimestamp != value.EventTimestamp || evidence.Review.EventProvenanceId != value.EventProvenanceId ||
            evidence.Review.ReplayProvenanceId != value.ReplayProvenanceId || evidence.Review.Status == "unresolved-object-evidence")
            errors.Add("Portfolio replay evidence requires available immutable objects and matching provenance.");
        if (errors.Count == 0)
        {
            var roles = evidence.Review.Comparisons.ToDictionary(comparison => comparison.Role, StringComparer.Ordinal);
            if (roles["original-implementation"].Selection.Base.ObjectId != value.OldBaseObjectId ||
                roles["original-implementation"].Selection.Head.ObjectId != value.OriginalObjectId ||
                roles["retained-feature"].Selection.Base.ObjectId != value.NewBaseObjectId ||
                roles["retained-feature"].Selection.Head.ObjectId != value.RewrittenObjectId ||
                value.ReplayObjectId is { } replay && roles["replayed-implementation"].Selection.Head.ObjectId != replay)
                errors.Add("Portfolio replay evidence must bind the declared immutable endpoints.");
        }
        return errors;
    }

    private static void ValidatePortfolioReplayAllocations(ChangePortfolioReport report, List<string> errors)
    {
        IReadOnlyList<ChangePortfolioReplayAllocation> allocations = report.ReplayAllocations ?? [];
        Dictionary<(string, string), ChangePortfolioItemEstimate[]> memberGroups = allocations.Count == 0 ? [] : report.Items
            .Where(item => item.Attribution.Replay is not null).GroupBy(item => (item.RepositoryId, item.Attribution.Replay!.EventId))
            .ToDictionary(group => group.Key, group => group.ToArray());
        HashSet<(string, string)> keys = [];
        HashSet<string> mappedItems = new(StringComparer.Ordinal);
        foreach (ChangePortfolioReplayAllocation allocation in allocations)
        {
            ChangePortfolioReplayEvidence evidence = allocation.Evidence;
            int before = errors.Count;
            errors.AddRange(Validate(evidence));
            if (before != errors.Count) continue;
            string repository = evidence.Review.RepositoryId;
            if (!keys.Add((repository, evidence.Event.Id))) errors.Add("Replay allocation event IDs must be unique per repository.");
            if (allocation.Policy != ChangePortfolioReplayEvent.Policy || allocation.AvailableJointExpectedHours < 0m ||
                evidence.Review.SinceInclusive != report.Selection.AuthorPeriodManifest?.SinceInclusive || evidence.Review.UntilExclusive != report.Selection.AuthorPeriodManifest?.UntilExclusive ||
                evidence.Review.Comparisons.Any(value => value.Profile != report.Profile || value.EstimatorVersion != report.SourceChangeEstimatorVersion))
                errors.Add("Replay allocations must bind this portfolio's interval, profile, source estimator and nonnegative budget.");
            ChangePortfolioItemEstimate[] members = memberGroups.GetValueOrDefault((repository, evidence.Event.Id)) ?? [];
            bool resolved = evidence.Event.ReplayObjectId is not null && evidence.Event.EventTimestamp is not null;
            decimal? novel = evidence.Review.Comparisons.SingleOrDefault(value => value.Role == "novel-retained-delta")?.Effort.Expected;
            if (allocation.StandaloneNovelExpectedHours != novel || members.Length == 0 ||
                members.Select(item => item.Selection.Head.ObjectId).Distinct(StringComparer.Ordinal).Count() != members.Length ||
                resolved && members.Length != evidence.OriginalObjectIds.Count + evidence.RetainedObjectIds.Count)
                errors.Add("Replay allocations must expose the independent novel estimate and complete supported membership.");
            foreach (ChangePortfolioItemEstimate item in members)
            {
                ChangePortfolioReplayAttribution attribution = item.Attribution.Replay!;
                IReadOnlyList<string> ids = attribution.Role == "original" ? evidence.OriginalObjectIds : evidence.RetainedObjectIds;
                DateTimeOffset? date = attribution.Role == "original" || !resolved ? attribution.OriginalSelectedTimestamp : evidence.Event.EventTimestamp;
                bool support = resolved && (date < report.Selection.AuthorPeriodManifest?.SinceInclusive || date >= report.Selection.AuthorPeriodManifest?.UntilExclusive);
                if (!mappedItems.Add(item.Id) || attribution.Role is not ("original" or "retained") || !ids.Contains(item.Selection.Head.ObjectId, StringComparer.Ordinal) ||
                    item.Attribution.Rewrite is not null || attribution.OriginalSelectedTimestamp.Offset != TimeSpan.Zero ||
                    attribution.SupportOnly != support || support && item.AllocatedExpectedHours != 0m || !support && item.Attribution.SelectedTimestamp != date)
                    errors.Add("Replay row attribution must preserve exact membership, original UTC dates and zero-cost support.");
            }
            if (before != errors.Count) continue;
            if (resolved && members.All(item => !item.Attribution.Replay!.SupportOnly) &&
                members.Sum(item => item.AllocatedExpectedHours) != allocation.AvailableJointExpectedHours)
                errors.Add("Full-period replay allocations must exactly conserve the available joint member budget.");
            bool inside = evidence.Event.EventTimestamp >= report.Selection.AuthorPeriodManifest?.SinceInclusive && evidence.Event.EventTimestamp < report.Selection.AuthorPeriodManifest?.UntilExclusive;
            decimal? expected = resolved ? inside ? Math.Min(novel!.Value, allocation.AvailableJointExpectedHours) : 0m : null;
            if (allocation.AllocatedEventExpectedHours != expected || allocation.AllocationCapped != (resolved && novel > allocation.AvailableJointExpectedHours) ||
                allocation.Status != (!resolved ? evidence.Review.EventAttributionStatus : inside ? "allocated-under-declared-replay" : "event-outside-period") ||
                resolved && members.Where(item => item.Attribution.Replay!.Role == "retained").Sum(item => item.AllocatedExpectedHours) != expected)
                errors.Add("Replay event allocation must conserve the joint budget and expose any cap; missing evidence is unresolved.");
        }
        if (report.Items.Any(item => item.Attribution.Replay is not null && !mappedItems.Contains(item.Id)))
            errors.Add("Every replay row must have one canonical portfolio replay allocation.");
    }
}
