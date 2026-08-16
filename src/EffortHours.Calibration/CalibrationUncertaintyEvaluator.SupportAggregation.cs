using EffortHours.Contracts.V1;

namespace EffortHours.Calibration;

public static partial class CalibrationUncertaintyEvaluator
{
    private static CalibrationUncertaintySupportEvaluationData BuildSupportEvaluationData(
        CalibrationCorpus corpus,
        IReadOnlyList<CalibrationUncertaintyFeatureReport> reports,
        CalibrationUncertaintySupportProfile support,
        CalibrationUncertaintyEvaluationReport source)
    {
        Dictionary<string, CalibrationRecord> records = corpus.Records.ToDictionary(
            record => record.Id,
            StringComparer.Ordinal);
        Dictionary<string, CalibrationUncertaintyFeatureReport> reportsBySource = reports
            .ToDictionary(report => report.RepositorySourceDigest, StringComparer.Ordinal);
        Dictionary<(string RecordId, string WorkItemId),
            CalibrationUncertaintySupportWorkItem> supportItems = support.WorkItems.ToDictionary(
                item => (item.RecordId, item.WorkItemId));
        List<CalibrationUncertaintyObservation> observations = [];
        List<CalibrationUncertaintySupportTargetEvaluation> targets = [];
        int matchedSupportReferences = 0;

        foreach (CalibrationUncertaintyTargetEvaluation sourceTarget in source.Targets)
        {
            CalibrationRecord record = records[sourceTarget.RecordId];
            CalibrationTarget target = record.Targets.Single(candidate =>
                string.Equals(candidate.Id, sourceTarget.TargetId, StringComparison.Ordinal));
            CalibrationUncertaintyFeatureReport report = reportsBySource[
                record.Repository.SourceDigest];
            Dictionary<string, CalibrationUncertaintyWorkItemFeatures> featureItems =
                report.WorkItems.ToDictionary(item => item.WorkItemId, StringComparer.Ordinal);
            CalibrationUncertaintySupportWorkItem[] members =
            [
                .. target.SourceWorkItemIds.Select(id => supportItems[(record.Id, id)]),
            ];
            CalibrationUncertaintyWorkItemFeatures[] featureMembers =
            [
                .. target.SourceWorkItemIds.Select(id => featureItems[id]),
            ];
            CalibrationUncertaintySupportCell[] selectedCells =
            [
                .. members.Select(item => item.SupportCells.Single(cell => cell.Selected)),
            ];
            (CalibrationUncertaintySupportWorkItem Item, int Depth) = members
                .Select(item => (Item: item, Depth: SupportDepth(item)))
                .OrderByDescending(item => item.Depth)
                .ThenBy(item => item.Item.WorkItemId, StringComparer.Ordinal)
                .First();
            decimal totalWeight = featureMembers.Sum(item => item.ExpectedHours);
            decimal weightedOod = totalWeight > 0m
                ? members.Select((item, index) =>
                        item.OutOfDistribution.Score * featureMembers[index].ExpectedHours)
                    .Sum() / totalWeight
                : members.Average(item => item.OutOfDistribution.Score);
            weightedOod = CalibrationUncertaintyEvaluationMath.Round6(weightedOod);
            decimal maximumOod = members.Max(item => item.OutOfDistribution.Score);
            int minimumObservationCount = selectedCells.Min(
                cell => cell.TrainingObservationCount);
            int minimumRepositoryCount = selectedCells.Min(
                cell => cell.TrainingRepositoryCount);
            bool sufficient = members.All(item => item.SupportSufficient);
            CalibrationUncertaintySupportTargetEvaluation targetEvaluation = new()
            {
                Source = sourceTarget,
                SourceWorkItemCount = members.Length,
                WorstSupportLevel = Item.SelectedSupportLevel,
                WorstSupportDepth = Depth,
                SupportSufficient = sufficient,
                MinimumSelectedSupportObservationCount = minimumObservationCount,
                MinimumSelectedSupportRepositoryCount = minimumRepositoryCount,
                ExpectedWeightedOutOfDistributionScore = weightedOod,
                MaximumOutOfDistributionScore = maximumOod,
            };
            targets.Add(targetEvaluation);
            observations.Add(ToObservation(targetEvaluation));
            matchedSupportReferences += members.Length;
        }

        return new CalibrationUncertaintySupportEvaluationData
        {
            SourceEvaluation = source,
            Observations = observations,
            Targets = targets,
            MatchedSupportWorkItemReferenceCount = matchedSupportReferences,
        };
    }

    private static CalibrationUncertaintyObservation ToObservation(
        CalibrationUncertaintySupportTargetEvaluation target)
    {
        CalibrationUncertaintyTargetEvaluation source = target.Source;
        decimal residual = decimal.Abs(
            source.CandidateRange.Expected - source.ReviewedRange.Expected);
        Dictionary<string, CalibrationUncertaintyAggregatedFeature> features =
            new(StringComparer.Ordinal)
            {
                [CalibrationUncertaintySupportSignalIds.FallbackDepth] = Available(
                    target.WorstSupportDepth),
                [CalibrationUncertaintySupportSignalIds.MinimumRepositoryCount] = Available(
                    target.MinimumSelectedSupportRepositoryCount),
                [CalibrationUncertaintySupportSignalIds.WeightedMeanOutOfDistribution] = Available(
                    target.ExpectedWeightedOutOfDistributionScore),
                [CalibrationUncertaintySupportSignalIds.MaximumOutOfDistribution] = Available(
                    target.MaximumOutOfDistributionScore),
            };
        return new CalibrationUncertaintyObservation
        {
            RecordId = source.RecordId,
            RepositoryId = source.RepositoryId,
            TargetId = source.TargetId,
            Category = source.Category,
            Ecosystems = source.Ecosystems,
            ExpectedSizeBand = source.ExpectedSizeBand,
            CandidateRange = source.CandidateRange,
            ReviewedRange = source.ReviewedRange,
            AbsoluteResidualHours = residual,
            NormalizedAbsoluteResidual = CalibrationUncertaintyEvaluationMath.NormalizeResidual(
                residual,
                source.CandidateRange.Expected),
            Features = features,
            BaselineRange = source.CrossValidatedBaselineRange,
        };
    }

    private static CalibrationUncertaintyAggregatedFeature Available(decimal value) => new()
    {
        Availability = CalibrationUncertaintyFeatureAvailability.Available,
        Value = value,
    };

    private static int SupportDepth(CalibrationUncertaintySupportWorkItem item)
    {
        if (!item.SupportSufficient)
        {
            return 5;
        }

        return item.SelectedSupportLevel switch
        {
            CalibrationUncertaintySupportLevel.Exact => 0,
            CalibrationUncertaintySupportLevel.CategorySizeEcosystem => 1,
            CalibrationUncertaintySupportLevel.CategorySize => 2,
            CalibrationUncertaintySupportLevel.Category => 3,
            CalibrationUncertaintySupportLevel.Global => 4,
            _ => throw new InvalidOperationException(
                $"Unknown uncertainty support level '{item.SelectedSupportLevel}'."),
        };
    }
}
