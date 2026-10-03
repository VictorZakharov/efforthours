using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioComparisonTests
{
    [Fact]
    public async Task SharedReferencedSchemasValidateConcurrentDocumentsWithoutCorruptingCaches()
    {
        ChangeAuthorPeriodManifest manifest = Manifest();
        ChangePortfolioReport source = await SourceReportAsync(manifest);
        ChangePortfolioComparisonReport comparison = ChangePortfolioComparisonBuilder.Build(source, BuildOptions(manifest));
        (string Schema, string Json)[] documents =
        [
            (SchemaNames.ChangePortfolioReport, ContractJson.Serialize(source)),
            (SchemaNames.ChangePortfolioComparisonReport, ContractJson.Serialize(comparison)),
            (SchemaNames.ChangeAuthorPeriodManifest, ContractJson.Serialize(manifest)),
        ];
        using Barrier start = new(16);
        await Task.WhenAll(Enumerable.Range(0, 16).Select(worker => Task.Factory.StartNew(() =>
        {
            Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(30)));
            for (int iteration = 0; iteration < 12; iteration++)
            {
                (string schema, string json) = documents[(worker + iteration) % documents.Length];
                SchemaValidationResult valid = ContractSchemaValidator.Validate(schema, json);
                Assert.True(valid.IsValid, string.Join(" ", valid.Errors));
                SchemaValidationResult invalid = ContractSchemaValidator.Validate(schema, "{}");
                Assert.False(invalid.IsValid);
                Assert.NotEmpty(invalid.Errors);
            }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)));
    }
}
