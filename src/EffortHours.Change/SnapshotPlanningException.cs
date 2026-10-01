namespace EffortHours.Change;

/// <summary>A bounded public failure category; detailed input errors remain on stderr.</summary>
public sealed class SnapshotPlanningException(string category, string message, string? areaId = null, Exception? inner = null)
    : IOException(message, inner)
{
    public string Category { get; } = category;
    public string? AreaId { get; } = areaId;
}
