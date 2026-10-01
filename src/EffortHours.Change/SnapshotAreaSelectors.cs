using System.Text.RegularExpressions;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed class SnapshotAreaSelectors
{
    internal Regex[][] Patterns { get; }
    internal string DefinitionDigest { get; }

    public SnapshotAreaSelectors(IReadOnlyList<SnapshotAreaDefinition> definitions, Action? compiled = null)
    {
        SnapshotAreaPartition.ValidateDefinitions(definitions);
        DefinitionDigest = SnapshotMeasurementIdentity.Digest(definitions);
        Patterns = [.. definitions.Select(a => a.Include.Select(selector =>
        {
            compiled?.Invoke();
            return SnapshotAreaPartition.Compile(selector);
        }).ToArray())];
    }
}
