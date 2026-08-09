using EffortHours.Contracts.V1;

namespace EffortHours.ScannerBenchmarks;

internal sealed record InventoryMeasurements(long Files, long Bytes, long TextLines)
{
    public static InventoryMeasurements From(RepositoryEvidence evidence)
    {
        EvidenceFact inventory = evidence.Facts.Single(fact => fact.Id == "inventory:repository");
        return new InventoryMeasurements(
            Measurement(inventory, "included-files"),
            Measurement(inventory, "included-bytes"),
            Measurement(inventory, "text-lines"));
    }

    private static long Measurement(EvidenceFact inventory, string name) => checked((long)inventory.Measurements
        .Single(measurement => measurement.Name == name)
        .Value);
}
