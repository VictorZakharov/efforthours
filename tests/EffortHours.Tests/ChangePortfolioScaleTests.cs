using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioReconcilerTests
{
    [Fact]
    public async Task ClosedMonthScalePortfolioReconcilesMoreThanFormerRowLimit()
    {
        const int selectedChanges = 129;
        List<ChangePortfolioCandidate> candidates = [];
        DateTimeOffset firstTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        string? headObjectId = null;
        for (int index = 0; index < selectedChanges; index++)
        {
            ChangeState before = State(
                ("Demo.csproj", ProjectFile),
                ("Shared.cs", $"namespace Demo; public sealed class Shared {{ public int Value => {index}; }}\n"));
            ChangeState after = State(
                ("Demo.csproj", ProjectFile),
                ("Shared.cs", $"namespace Demo; public sealed class Shared {{ public int Value => {index + 1}; }}\n"));
            ChangeEstimateReport isolated = await ReportAsync(
                ChangeSelectionKind.Commit,
                index,
                before,
                after);
            headObjectId = after.ObjectId;
            candidates.Add(Candidate(
                "repository-a",
                $"commit-{index:D3}",
                isolated,
                firstTimestamp.AddHours(index)));
        }

        ChangePortfolioReport report = Reconcile(AuthorPeriod(headObjectId!), candidates);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.ChangePortfolioReport,
            ContractJson.Serialize(report));

        Assert.Equal(selectedChanges, report.Items.Count);
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(report));
        Assert.Equal(
            report.TotalEffort.Expected,
            report.Items.Sum(item => item.AllocatedExpectedHours));
    }

    [Fact]
    public async Task HighCommitMonthIsNotRejectedByAPresentationRowLimit()
    {
        const int selectedChanges = 1_701;
        ChangeState before = State(("Demo.csproj", ProjectFile));
        ChangeState after = State(
            ("Demo.csproj", ProjectFile),
            ("Feature.cs", "namespace Demo; public sealed class Feature { }\n"));
        ChangeEstimateReport isolated = await ReportAsync(
            ChangeSelectionKind.Commit,
            1,
            before,
            after);
        ChangePortfolioCandidate[] candidates = [.. Enumerable.Range(0, selectedChanges).Select(index =>
            Candidate(
                "repository-a",
                $"commit-{index:D4}",
                isolated,
                new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(index)))];

        ChangePortfolioReport report = Reconcile(AuthorPeriod(after.ObjectId), candidates);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.ChangePortfolioReport,
            ContractJson.Serialize(report));

        Assert.Equal(selectedChanges, report.Items.Count);
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(report));
    }
}
