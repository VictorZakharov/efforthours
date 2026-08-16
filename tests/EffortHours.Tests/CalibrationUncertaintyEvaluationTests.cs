using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class CalibrationUncertaintyEvaluationTests
{
    [Fact]
    public void DevelopmentEvaluationIsDeterministicSchemaValidAndMeasurementOnly()
    {
        (CalibrationCorpus corpus, CalibrationUncertaintyFeatureReport[] features) =
            CreateFixture();

        CalibrationUncertaintyEvaluationReport first =
            CalibrationUncertaintyEvaluator.EvaluateDevelopment(corpus, features);
        CalibrationUncertaintyEvaluationReport second =
            CalibrationUncertaintyEvaluator.EvaluateDevelopment(corpus, [.. features.Reverse()]);
        string json = ContractJson.Serialize(first);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationUncertaintyEvaluation,
            json);

        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(first));
        Assert.Equal(json, ContractJson.Serialize(second));
        Assert.Equal(CalibrationPartition.Development, first.Partition);
        Assert.True(first.Protocol.RepositoryIsolated);
        Assert.True(first.Protocol.DevelopmentOnly);
        Assert.False(first.Protocol.FitsProductionModel);
        Assert.False(first.Protocol.FormalProbabilityInterval);
        Assert.Equal(0.80m, first.Protocol.IntendedCoverageTarget);
        Assert.Equal(4, first.Summary.RepositoryCount);
        Assert.Equal(first.Summary.MatchedTargetCount, first.Targets.Count);
        Assert.All(first.Repositories, repository =>
        {
            Assert.Equal(repository.MatchedTargetCount, repository.CurrentIntervals.ObservationCount);
            Assert.Equal(
                repository.MatchedTargetCount,
                repository.CrossValidatedBaseline.ObservationCount);
        });
        Assert.Equal(
            CalibrationUncertaintyFeatureCatalog.Current.Features.Count,
            first.Features.Count);
        Assert.All(first.Features, feature =>
        {
            Assert.Equal(first.Targets.Count, feature.Availability.ObservationCount);
            Assert.Equal(
                first.Targets.Count,
                feature.ConditionedPredictionCount + feature.BaselineFallbackCount);
            Assert.Equal(first.Repositories.Count, feature.RepositoryFolds.Count);
            Assert.Equal(
                feature.ConditionedPredictionCount,
                feature.RepositoryFolds.Sum(fold => fold.ConditionedPredictionCount));
        });
        Assert.Contains(first.Features, feature => feature.ConditionedPredictionCount > 0);
        Assert.Contains(first.Slices, slice =>
            slice.Dimension == CalibrationUncertaintySliceDimension.Category);
        Assert.Contains(first.Slices, slice =>
            slice.Dimension == CalibrationUncertaintySliceDimension.Ecosystem);
        Assert.Contains(first.Slices, slice =>
            slice.Dimension == CalibrationUncertaintySliceDimension.ExpectedSizeBand);

        CalibrationUncertaintyEvaluationReport reordered = first with
        {
            Features = [.. first.Features.Reverse()],
        };
        Assert.Contains(ContractValidation.Validate(reordered), error => error.Contains(
            "frozen v1 feature order",
            StringComparison.Ordinal));
    }

    [Fact]
    public void HeldOutRepositoryLabelsCannotChangeItsBaselinePredictions()
    {
        (CalibrationCorpus corpus, CalibrationUncertaintyFeatureReport[] features) =
            CreateFixture();
        CalibrationUncertaintyEvaluationReport before =
            CalibrationUncertaintyEvaluator.EvaluateDevelopment(corpus, features);
        CalibrationRecord selected = corpus.Records[0];
        CalibrationCorpus changed = corpus with
        {
            Records =
            [
                selected with
                {
                    Targets = [.. selected.Targets.Select(target => target with
                    {
                        Hours = Symmetric(target.Hours.Expected + 10m),
                        SizeException = "Synthetic repository-isolation stress label.",
                    })],
                },
                .. corpus.Records.Skip(1),
            ],
        };

        CalibrationUncertaintyEvaluationReport after =
            CalibrationUncertaintyEvaluator.EvaluateDevelopment(changed, features);
        EffortRange[] beforeRanges = [.. before.Targets
            .Where(target => target.RecordId == selected.Id)
            .OrderBy(target => target.TargetId, StringComparer.Ordinal)
            .Select(target => target.CrossValidatedBaselineRange)];
        EffortRange[] afterRanges = [.. after.Targets
            .Where(target => target.RecordId == selected.Id)
            .OrderBy(target => target.TargetId, StringComparer.Ordinal)
            .Select(target => target.CrossValidatedBaselineRange)];

        Assert.Equal(beforeRanges, afterRanges);
        Assert.NotEqual(
            before.Targets.First(target => target.RecordId == selected.Id).ReviewedRange,
            after.Targets.First(target => target.RecordId == selected.Id).ReviewedRange);
    }

    [Fact]
    public void EvaluationRefusesValidationOnlyOrUnderSupportedDevelopmentData()
    {
        (CalibrationCorpus source, CalibrationUncertaintyFeatureReport[] features) =
            CreateFixture();
        CalibrationCorpus validationOnly = source with
        {
            Records = [.. source.Records.Select(record => record with
            {
                Partition = CalibrationPartition.Validation,
            })],
        };
        CalibrationEvaluationException noDevelopment =
            Assert.Throws<CalibrationEvaluationException>(() =>
                CalibrationUncertaintyEvaluator.EvaluateDevelopment(validationOnly, features));

        CalibrationCorpus twoRepositories = source with
        {
            Records = [.. source.Records.Take(2)],
        };
        CalibrationEvaluationException insufficient =
            Assert.Throws<CalibrationEvaluationException>(() =>
                CalibrationUncertaintyEvaluator.EvaluateDevelopment(twoRepositories, features));

        Assert.Contains(noDevelopment.Errors, error => error.Contains(
            "development-only corpus",
            StringComparison.Ordinal));
        Assert.Contains(insufficient.Errors, error => error.Contains(
            "at least three development repositories",
            StringComparison.Ordinal));
    }

    [Fact]
    public void EvaluationRefusesASelfConsistentButNonCanonicalFeatureContract()
    {
        (CalibrationCorpus corpus, CalibrationUncertaintyFeatureReport[] features) =
            CreateFixture();
        CalibrationUncertaintyFeatureContract alteredContract = features[0].FeatureContract with
        {
            Features =
            [
                features[0].FeatureContract.Features[0] with
                {
                    Description = "Synthetic noncanonical description.",
                },
                .. features[0].FeatureContract.Features.Skip(1),
            ],
        };
        CalibrationUncertaintyFeatureReport altered = features[0] with
        {
            FeatureContract = alteredContract,
            FeatureContractDigest = CalibrationDigest.Compute(alteredContract),
        };

        CalibrationEvaluationException exception =
            Assert.Throws<CalibrationEvaluationException>(() =>
                CalibrationUncertaintyEvaluator.EvaluateDevelopment(
                    corpus,
                    [altered, .. features.Skip(1)]));

        Assert.Contains(exception.Errors, error => error.Contains(
            "canonical frozen feature contract",
            StringComparison.Ordinal));
    }

    internal static (CalibrationCorpus Corpus, CalibrationUncertaintyFeatureReport[] Features)
        CreateFixture()
    {
        List<CalibrationUncertaintyFeatureReport> reports = [];
        List<CalibrationRecord> records = [];
        decimal[] adjustments = [0.250037m, 0.75m, 1.5m, 3m];
        for (int index = 0; index < adjustments.Length; index++)
        {
            char digestCharacter = (char)('a' + index);
            RepositoryEvidence source = TestRepositoryEvidence.CreateStructuredDotNet(
                sourceCopies: 2,
                endpoints: 3,
                testCases: 4);
            RepositoryEvidence evidence = source with
            {
                Repository = source.Repository with
                {
                    SourceDigest = "sha256:" + new string(digestCharacter, 64),
                },
            };
            EstimateReport estimate = new SeedEstimator().Estimate(
                evidence,
                EstimationProfile.Implementation);
            CalibrationUncertaintyFeatureReport report =
                CalibrationUncertaintyFeatureProjector.Project(estimate, evidence);
            reports.Add(report);
            records.Add(CreateRecord(index, report, adjustments[index]));
        }

        return (
            new CalibrationCorpus
            {
                Id = "uncertainty-evaluation-fixture",
                Version = "1.0.0",
                Description = "Synthetic development-only uncertainty evaluation fixture.",
                Rubric = new CalibrationRubricReference
                {
                    Id = "ehe-work-item",
                    Version = "1.1.0",
                },
                Records = records,
            },
            [.. reports]);
    }

    private static CalibrationRecord CreateRecord(
        int index,
        CalibrationUncertaintyFeatureReport report,
        decimal adjustment) => new()
        {
            Id = $"record:uncertainty-{index}",
            Repository = new CalibrationRepositoryReference
            {
                Id = $"repository:uncertainty-{index}",
                Name = $"Synthetic uncertainty repository {index}",
                SourceDigest = report.RepositorySourceDigest,
            },
            Profile = report.Profile,
            BaselineId = report.BaselineId,
            Partition = CalibrationPartition.Development,
            SourceEstimatorVersion = report.EstimatorVersion,
            SourceEstimateDigest = report.EstimateDigest,
            Source = new CalibrationSourceProvenance
            {
                DataClassification = CalibrationDataClassification.Synthetic,
                SourceReference = $"eh://tests/uncertainty-evaluation/{index}",
                Revision = "1",
                LicenseExpression = "MIT",
                RedistributionAllowed = true,
            },
            Review = new CalibrationReviewProvenance
            {
                Status = CalibrationReviewStatus.TeacherEstimate,
                CompletedOn = new DateOnly(2026, 8, 15),
                Reviewers =
            [
                new CalibrationReviewer
                {
                    Id = "host-ai:uncertainty-evaluation-fixture",
                    Kind = CalibrationReviewerKind.HostAi,
                    Role = CalibrationReviewerRole.Teacher,
                    ModelId = "synthetic-test-model",
                    ModelVersion = "1",
                },
            ],
            },
            Targets = [.. report.WorkItems.OrderBy(item => item.WorkItemId, StringComparer.Ordinal)
            .Select((item, itemIndex) => new CalibrationTarget
            {
                Id = $"target:{item.WorkItemId}",
                Category = item.Category,
                Title = "Synthetic uncertainty target",
                Scope = "synthetic-fixture",
                SourceWorkItemIds = [item.WorkItemId],
                EvidenceIds = item.ResolvedEvidenceIds.Count > 0
                    ? item.ResolvedEvidenceIds
                    : ["evidence:synthetic"],
                Hours = Symmetric(item.ExpectedHours + adjustment + ((itemIndex % 2) * 0.25m)),
                Rationale = "Synthetic reviewed range for repository-isolated evaluation.",
                SizeException = "Synthetic fixture deliberately spans ordinary size bands.",
            })],
        };

    private static EffortRange Symmetric(decimal expected)
    {
        decimal halfWidth = decimal.Min(1m, expected);
        return new EffortRange
        {
            Low = expected - halfWidth,
            Expected = expected,
            High = expected + halfWidth,
        };
    }
}
