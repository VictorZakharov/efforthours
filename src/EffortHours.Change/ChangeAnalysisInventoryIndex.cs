namespace EffortHours.Change;

internal sealed record ChangeAnalysisInventoryIndex(
    IReadOnlyList<string> ContextPaths,
    IReadOnlyList<string> RepresentativePaths);
