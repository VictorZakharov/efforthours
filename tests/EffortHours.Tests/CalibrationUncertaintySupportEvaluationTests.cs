using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class CalibrationUncertaintySupportEvaluationTests
{
    [Fact]
    public void EvaluationIsDeterministicSchemaValidAndMeasurementOnly()
    {
        (CalibrationCorpus corpus, CalibrationUncertaintyFeatureReport[] reports) =
            CalibrationUncertaintyEvaluationTests.CreateFixture();
        CalibrationUncertaintySupportProfile support = BuildSupport(corpus, reports);

        CalibrationUncertaintySupportEvaluationReport first =
            CalibrationUncertaintyEvaluator.EvaluateSupportDevelopment(
                corpus,
                reports,
                support);
        CalibrationUncertaintySupportEvaluationReport second =
            CalibrationUncertaintyEvaluator.EvaluateSupportDevelopment(
                corpus,
                [.. reports.Reverse()],
                support);
        string json = ContractJson.Serialize(first);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationUncertaintySupportEvaluation,
            json);

        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(first));
        Assert.Equal(json, ContractJson.Serialize(second));
        Assert.True(first.Protocol.RepositoryIsolated);
        Assert.True(first.Protocol.SupportProfileLabelIndependent);
        Assert.True(first.Protocol.DevelopmentOnly);
        Assert.False(first.Protocol.FitsProductionModel);
        Assert.False(first.Protocol.FormalProbabilityInterval);
        Assert.Equal(4, first.Signals.Count);
        Assert.Equal(first.Targets.Count, first.Summary.MatchedTargetCount);
        Assert.Equal(
            first.Targets.Sum(target => target.SourceWorkItemCount),
            first.Summary.MatchedSupportWorkItemReferenceCount);
        Assert.All(first.Signals, signal =>
        {
            Assert.True(signal.WidthDriver);
            Assert.Equal(first.Targets.Count, signal.Availability.AvailableCount);
            Assert.Equal(first.Repositories.Count, signal.RepositoryFolds.Count);
        });
        Assert.Equal(
            [
                CalibrationUncertaintySupportSignalIds.FallbackDepth,
                CalibrationUncertaintySupportSignalIds.MinimumRepositoryCount,
                CalibrationUncertaintySupportSignalIds.WeightedMeanOutOfDistribution,
                CalibrationUncertaintySupportSignalIds.MaximumOutOfDistribution,
            ],
            first.Signals.Select(signal => signal.FeatureId));
    }

    [Fact]
    public void HeldOutLabelsCannotChangeTheirSignalConditionedWidths()
    {
        (CalibrationCorpus corpus, CalibrationUncertaintyFeatureReport[] reports) =
            CalibrationUncertaintyEvaluationTests.CreateFixture();
        CalibrationUncertaintySupportProfile support = BuildSupport(corpus, reports);
        CalibrationUncertaintySupportEvaluationReport before =
            CalibrationUncertaintyEvaluator.EvaluateSupportDevelopment(corpus, reports, support);
        CalibrationRecord selected = corpus.Records[0];
        CalibrationCorpus changed = corpus with
        {
            Records =
            [
                selected with
                {
                    Targets = [.. selected.Targets.Select(target => target with
                    {
                        Hours = Shift(target.Hours, 10m),
                        SizeException = "Synthetic repository-isolation stress label.",
                    })],
                },
                .. corpus.Records.Skip(1),
            ],
        };

        CalibrationUncertaintySupportEvaluationReport after =
            CalibrationUncertaintyEvaluator.EvaluateSupportDevelopment(changed, reports, support);
        foreach (CalibrationUncertaintyFeatureEvaluation beforeSignal in before.Signals)
        {
            CalibrationUncertaintyFeatureRepositoryFold beforeFold = beforeSignal.RepositoryFolds
                .Single(fold => fold.RecordId == selected.Id);
            CalibrationUncertaintyFeatureRepositoryFold afterFold = after.Signals
                .Single(signal => signal.FeatureId == beforeSignal.FeatureId)
                .RepositoryFolds.Single(fold => fold.RecordId == selected.Id);
            Assert.Equal(beforeFold.ConditionedPredictionCount, afterFold.ConditionedPredictionCount);
            Assert.Equal(beforeFold.BaselineFallbackCount, afterFold.BaselineFallbackCount);
            Assert.Equal(beforeFold.Intervals.MeanWidthHours, afterFold.Intervals.MeanWidthHours);
            Assert.Equal(
                beforeFold.Intervals.MeanNormalizedWidth,
                afterFold.Intervals.MeanNormalizedWidth);
        }

        Assert.NotEqual(
            before.Targets[0].Source.ReviewedRange,
            after.Targets[0].Source.ReviewedRange);
    }

    [Fact]
    public void EvaluationRejectsTamperedSupportLineage()
    {
        (CalibrationCorpus corpus, CalibrationUncertaintyFeatureReport[] reports) =
            CalibrationUncertaintyEvaluationTests.CreateFixture();
        CalibrationUncertaintySupportProfile source = BuildSupport(corpus, reports);
        CalibrationUncertaintySupportProfile tampered = source with
        {
            Repositories =
            [
                source.Repositories[0] with
                {
                    FeatureReportDigest = "sha256:" + new string('f', 64),
                },
                .. source.Repositories.Skip(1),
            ],
        };

        CalibrationEvaluationException exception = Assert.Throws<CalibrationEvaluationException>(
            () => CalibrationUncertaintyEvaluator.EvaluateSupportDevelopment(
                corpus,
                reports,
                tampered));

        Assert.Contains(exception.Errors, error => error.Contains(
            "does not match source evaluation lineage",
            StringComparison.Ordinal));
    }

    private static CalibrationUncertaintySupportProfile BuildSupport(
        CalibrationCorpus corpus,
        CalibrationUncertaintyFeatureReport[] reports)
    {
        CalibrationUncertaintySupportPopulation population = new()
        {
            Id = "uncertainty-support-evaluation-fixture",
            Version = "1.0.0",
            Description = "Synthetic support evaluation population.",
            Partition = CalibrationPartition.Development,
            FeatureContractVersion = CalibrationUncertaintyFeatureCatalog.Version,
            FeatureContractDigest = CalibrationUncertaintyVersions.FeatureContractDigestV1,
            Profile = reports[0].Profile,
            BaselineId = reports[0].BaselineId,
            Repositories = [.. corpus.Records.OrderBy(record => record.Id, StringComparer.Ordinal)
                .Select(record => new CalibrationUncertaintySupportPopulationRepository
                {
                    RecordId = record.Id,
                    RepositoryId = record.Repository.Id,
                    SourceDigest = record.Repository.SourceDigest,
                })],
        };
        return CalibrationUncertaintySupportProfiler.Profile(population, reports);
    }

    private static EffortRange Shift(EffortRange source, decimal amount) => new()
    {
        Low = source.Low + amount,
        Expected = source.Expected + amount,
        High = source.High + amount,
    };
}
