namespace EffortHours.Contracts.V1;

public sealed record ChangePortfolioAttributionCompleteness
{
    public string Policy { get; init; } = "retained-attribution-completeness/1.0.0";
    public required string DeclaredEventStatus { get; init; }
    public int MissingEventDateCount { get; init; }
    public int MissingReplayBaselineCount { get; init; }
    public string OriginalWorkdayStatus { get; init; } = "unresolved-original-workday";
    public string IntermediateHistoryStatus { get; init; } = "unknown";

    public static ChangePortfolioAttributionCompleteness From(ChangePortfolioReport source)
    {
        int missingDates = (source.ReplayAllocations ?? []).Count(value => value.Evidence.Event.EventTimestamp is null)
            + source.Items.Where(item => item.Attribution.Rewrite is { Evidence.EventTimestamp: null })
                .Select(item => (item.RepositoryId, item.Attribution.Rewrite!.Evidence.OriginalObjectId)).Distinct().Count();
        int missingReplay = (source.ReplayAllocations ?? []).Count(value => value.Evidence.Event.ReplayObjectId is null);
        missingDates = Math.Max(missingDates, source.Diagnostics.Count(value => value.Code is "FB5340" or "FB5344"));
        missingReplay = Math.Max(missingReplay, source.Diagnostics.Count(value => value.Code == "FB5345"));
        bool declared = source.ReplayAllocations is { Count: > 0 } || source.Items.Any(item => item.Attribution.Rewrite is not null);
        return new()
        {
            DeclaredEventStatus = missingDates + missingReplay > 0 ? "unresolved" : declared ? "available" : "not-declared",
            MissingEventDateCount = missingDates,
            MissingReplayBaselineCount = missingReplay,
        };
    }
}
