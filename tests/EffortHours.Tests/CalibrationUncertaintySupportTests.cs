using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class CalibrationUncertaintySupportTests
{
    [Fact]
    public void ProfileIsDeterministicSchemaValidAndRepositoryHeldOut()
    {
        (CalibrationUncertaintySupportPopulation population,
            CalibrationUncertaintyFeatureReport[] reports) = CreateFixture(3);

        CalibrationUncertaintySupportProfile first =
            CalibrationUncertaintySupportProfiler.Profile(population, reports);
        CalibrationUncertaintySupportProfile second =
            CalibrationUncertaintySupportProfiler.Profile(population, [.. reports.Reverse()]);
        string json = ContractJson.Serialize(first);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationUncertaintySupportProfile,
            json);

        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(first));
        Assert.Equal(json, ContractJson.Serialize(second));
        Assert.True(first.Policy.LabelIndependent);
        Assert.False(first.Policy.UsesReviewedValues);
        Assert.True(first.Policy.SameRepositoryExcluded);
        Assert.Equal(3, first.Summary.RepositoryCount);
        Assert.NotEmpty(first.WorkItems);
        Assert.All(first.WorkItems, item =>
        {
            Assert.NotEqual(item.RepositoryId, item.OutOfDistribution.NearestRepositoryId);
            Assert.Equal(5, item.SupportCells.Count);
            Assert.Single(item.SupportCells, cell => cell.Selected);
        });
        Assert.Contains(first.WorkItems, item => item.OutOfDistribution.Score == 0m);
        Assert.Equal(
            first.Summary.WorkItemCount,
            first.Summary.SupportLevels.Sum(level => level.WorkItemCount));
    }

    [Fact]
    public void SameRepositoryFamilyCannotSupplySupportOrNearestProfile()
    {
        (CalibrationUncertaintySupportPopulation sourcePopulation,
            CalibrationUncertaintyFeatureReport[] sourceReports) = CreateFixture(3);
        CalibrationUncertaintyFeatureReport first = sourceReports[0];
        string duplicateDigest = "sha256:" + new string('f', 64);
        CalibrationUncertaintyFeatureReport duplicate = first with
        {
            RepositorySourceDigest = duplicateDigest,
        };
        EffortCategory selectedCategory = first.WorkItems[0].Category;
        EffortCategory alternateCategory = selectedCategory == EffortCategory.Documentation
            ? EffortCategory.UnitTesting
            : EffortCategory.Documentation;
        CalibrationUncertaintyFeatureReport[] changedOthers =
        [
            .. sourceReports.Skip(1).Select(report => report with
            {
                WorkItems = [.. report.WorkItems.Select(item => item with
                {
                    Category = alternateCategory,
                })],
            }),
        ];
        CalibrationUncertaintySupportPopulation population = sourcePopulation with
        {
            Repositories =
            [
                sourcePopulation.Repositories[0],
                new CalibrationUncertaintySupportPopulationRepository
                {
                    RecordId = "record:00-same-family",
                    RepositoryId = sourcePopulation.Repositories[0].RepositoryId,
                    SourceDigest = duplicateDigest,
                },
                .. sourcePopulation.Repositories.Skip(1),
            ],
        };

        CalibrationUncertaintySupportProfile profile =
            CalibrationUncertaintySupportProfiler.Profile(
                population,
                [first, duplicate, .. changedOthers]);
        CalibrationUncertaintySupportWorkItem item = profile.WorkItems.Single(candidate =>
            candidate.RecordId == sourcePopulation.Repositories[0].RecordId &&
            candidate.WorkItemId == first.WorkItems[0].WorkItemId);
        CalibrationUncertaintySupportCell global = item.SupportCells.Single(cell =>
            cell.Level == CalibrationUncertaintySupportLevel.Global);

        Assert.Equal(changedOthers.Sum(report => report.WorkItems.Count),
            global.TrainingObservationCount);
        Assert.Equal(2, global.TrainingRepositoryCount);
        Assert.Equal(0, item.OutOfDistribution.ExactProfileTrainingObservationCount);
        Assert.NotEqual(item.RepositoryId, item.OutOfDistribution.NearestRepositoryId);
        Assert.True(item.OutOfDistribution.Score > 0m);
    }

    [Fact]
    public void ProfileRejectsAnUnderSupportedOrNonCanonicalPopulation()
    {
        (CalibrationUncertaintySupportPopulation population,
            CalibrationUncertaintyFeatureReport[] reports) = CreateFixture(3);
        CalibrationUncertaintySupportPopulation twoRepositories = population with
        {
            Repositories = [.. population.Repositories.Take(2)],
        };
        CalibrationUncertaintySupportPopulation alteredContract = population with
        {
            FeatureContractDigest = "sha256:" + new string('0', 64),
        };

        CalibrationEvaluationException insufficient = Assert.Throws<CalibrationEvaluationException>(
            () => CalibrationUncertaintySupportProfiler.Profile(
                twoRepositories,
                [.. reports.Take(2)]));
        CalibrationEvaluationException nonCanonical = Assert.Throws<CalibrationEvaluationException>(
            () => CalibrationUncertaintySupportProfiler.Profile(alteredContract, reports));

        Assert.Contains(insufficient.Errors, error => error.Contains(
            "at least three repository families",
            StringComparison.Ordinal));
        Assert.Contains(nonCanonical.Errors, error => error.Contains(
            "canonical v1 feature contract",
            StringComparison.Ordinal));
    }

    private static (CalibrationUncertaintySupportPopulation Population,
        CalibrationUncertaintyFeatureReport[] Reports) CreateFixture(int repositoryCount)
    {
        List<CalibrationUncertaintyFeatureReport> reports = [];
        for (int index = 0; index < repositoryCount; index++)
        {
            RepositoryEvidence source = TestRepositoryEvidence.CreateStructuredDotNet(
                sourceCopies: 2,
                endpoints: 3,
                testCases: 4);
            RepositoryEvidence evidence = source with
            {
                Repository = source.Repository with
                {
                    SourceDigest = "sha256:" + new string((char)('a' + index), 64),
                },
            };
            EstimateReport estimate = new SeedEstimator().Estimate(
                evidence,
                EstimationProfile.Implementation);
            reports.Add(CalibrationUncertaintyFeatureProjector.Project(estimate, evidence));
        }

        CalibrationUncertaintySupportPopulation population = new()
        {
            Id = "uncertainty-support-fixture",
            Version = "1.0.0",
            Description = "Synthetic label-independent uncertainty support fixture.",
            Partition = CalibrationPartition.Development,
            FeatureContractVersion = CalibrationUncertaintyFeatureCatalog.Version,
            FeatureContractDigest = CalibrationUncertaintyVersions.FeatureContractDigestV1,
            Profile = EstimationProfile.Implementation,
            BaselineId = reports[0].BaselineId,
            Repositories = [.. reports.Select((report, index) =>
                new CalibrationUncertaintySupportPopulationRepository
                {
                    RecordId = $"record:{index:D2}",
                    RepositoryId = $"repository:{index:D2}",
                    SourceDigest = report.RepositorySourceDigest,
                })],
        };
        return (population, [.. reports]);
    }
}
