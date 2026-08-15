using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

internal static class CalibrationUncertaintySupportObservations
{
    public static IReadOnlyList<CalibrationUncertaintySupportObservation> Create(
        IReadOnlyList<CalibrationUncertaintySupportInput> inputs,
        CancellationToken cancellationToken)
    {
        List<CalibrationUncertaintySupportObservation> observations = [];
        foreach (CalibrationUncertaintySupportInput input in inputs)
        {
            foreach (CalibrationUncertaintyWorkItemFeatures item in input.Report.WorkItems.OrderBy(
                         workItem => workItem.WorkItemId,
                         StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                string[] ecosystems = ResolveEcosystems(item);
                CalibrationUncertaintySupportFeaturePoint[] features = CreateFeatures(
                    item,
                    input.Report.FeatureContract.Features);
                string sizeBand = CalibrationUncertaintyBucketing.SizeBand(item.ExpectedHours);
                string ecosystemKey = ContractJson.SerializeCompact(ecosystems);
                string[] signatureComponents =
                [
                    $"category:{(int)item.Category}",
                    $"size:{sizeBand}",
                    $"complexity:{(int)item.SourceComplexity}",
                    $"ecosystems:{ecosystemKey}",
                    .. features.Select(feature =>
                        $"feature:{feature.Id}:{(int)feature.Availability}:{feature.BucketId}"),
                ];
                string profileDigest = CalibrationDigest.ComputeSequence(signatureComponents);
                observations.Add(new CalibrationUncertaintySupportObservation
                {
                    RecordId = input.Population.RecordId,
                    RepositoryId = input.Population.RepositoryId,
                    WorkItemId = item.WorkItemId,
                    Category = item.Category,
                    ExpectedSizeBand = sizeBand,
                    SourceComplexity = item.SourceComplexity,
                    Ecosystems = ecosystems,
                    EcosystemKey = ecosystemKey,
                    Features = features,
                    ProfileSignature = profileDigest,
                    ProfileDigest = profileDigest,
                });
            }
        }

        return observations;
    }

    private static string[] ResolveEcosystems(CalibrationUncertaintyWorkItemFeatures item) =>
    [
        .. item.Ecosystems.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];

    private static CalibrationUncertaintySupportFeaturePoint[] CreateFeatures(
        CalibrationUncertaintyWorkItemFeatures item,
        IReadOnlyList<CalibrationUncertaintyFeatureDefinition> definitions)
    {
        CalibrationUncertaintySupportFeaturePoint[] result =
            new CalibrationUncertaintySupportFeaturePoint[definitions.Count];
        for (int index = 0; index < definitions.Count; index++)
        {
            CalibrationUncertaintyFeatureDefinition definition = definitions[index];
            CalibrationUncertaintyFeatureValue value = item.Features[index];
            int maximumOrder = CalibrationUncertaintyBucketing.FeatureMaximumOrder(
                definition.ValueKind);
            CalibrationUncertaintyBucket? bucket = value.Value is null
                ? null
                : CalibrationUncertaintyBucketing.FeatureBucket(
                    definition.ValueKind,
                    value.Value.Value);
            result[index] = new CalibrationUncertaintySupportFeaturePoint
            {
                Id = definition.Id,
                Availability = value.Availability,
                BucketId = bucket?.Id ?? $"availability-{(int)value.Availability}",
                BucketOrder = bucket?.Order ?? 0,
                MaximumOrder = maximumOrder,
            };
        }

        return result;
    }
}
