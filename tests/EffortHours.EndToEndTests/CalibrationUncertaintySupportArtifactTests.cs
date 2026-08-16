using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed class CalibrationUncertaintySupportArtifactTests
{
    [Fact]
    public void PublicSupportPopulationIsLabelFreeAndMatchesDevelopmentFamilies()
    {
        string root = FindRepositoryRoot();
        string populationPath = Path.Combine(
            root,
            "calibration",
            "corpora",
            "public-readiness",
            "1.5.0.uncertainty-support-population.json");
        string corpusPath = Path.Combine(
            root,
            "calibration",
            "corpora",
            "public-readiness",
            "0.3.0.development-corpus.json");
        string json = File.ReadAllText(populationPath);
        CalibrationUncertaintySupportPopulation population =
            ContractJson.Deserialize<CalibrationUncertaintySupportPopulation>(json);
        CalibrationCorpus corpus = ContractJson.Deserialize<CalibrationCorpus>(
            File.ReadAllText(corpusPath));
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationUncertaintySupportPopulation,
            json);

        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(population));
        Assert.Equal(
            "sha256:c86af113b391d7171060f3b0be7c6d01ffa87f04ccb1ae03803f015199e61678",
            CalibrationDigest.Compute(population));
        Assert.Equal(15, population.Repositories.Count);
        Assert.Equal(
            corpus.Records.OrderBy(record => record.Id, StringComparer.Ordinal).Select(record => (
                record.Id,
                record.Repository.Id,
                record.Repository.SourceDigest)),
            population.Repositories.Select(repository => (
                repository.RecordId,
                repository.RepositoryId,
                repository.SourceDigest)));
        Assert.All(corpus.Records, record =>
        {
            Assert.Equal(CalibrationPartition.Development, record.Partition);
            Assert.Equal(population.Profile, record.Profile);
            Assert.Equal(population.BaselineId, record.BaselineId);
        });
        Assert.DoesNotContain("\"targets\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"hours\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"review\"", json, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EffortHours.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
