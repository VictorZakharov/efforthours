using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class ChangeWorkItemBuilder
{
    // Owned by a bounded snapshot-analysis entry. Share only after exact evidence/stock proof.
    internal sealed class CapabilityCatalog(RepositoryEvidence evidence, EstimateReport estimate)
    {
        private readonly Lazy<Dictionary<string, Capability>> _value = new(() =>
            Capabilities(estimate, evidence.Facts.ToDictionary(fact => fact.Id, StringComparer.Ordinal)));
        internal Dictionary<string, Capability> Value => _value.Value;
    }
}
