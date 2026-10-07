using EffortHours.Change;
using EffortHours.Contracts;

namespace EffortHours.Tests;

public sealed partial class ChangePortfolioComparisonTests
{
    [Theory]
    [InlineData("No work occurred.", "zero-labor-claim")]
    [InlineData("No work was done.", "zero-labor-claim")]
    [InlineData("Zero actual labor.", "zero-labor-claim")]
    [InlineData("No retained change was selected; original labor is unknown.", "retained-no-change-annotation")]
    [InlineData("Retained evidence: no-retained-change; zero retained EHE is not zero labor.", "retained-no-change-annotation")]
    public async Task RefreshIdentifiesManagedZeroLaborVerdictsSeparatelyFromRetainedZeros(string managed, string classification)
    {
        var source = await WorkdaySourceAsync();
        var review = ChangeWorkdayReviewer.Review(source, WorkRecords(source, Record(source, "lost", 1)));
        string original = "Factual task: rebased and tested.\n[EffortHours historical annotation]\n" + managed +
            "\n[/EffortHours historical annotation]\nUser text: no work occurred on vacation.";
        var input = RefreshInput(review, RefreshEntry("lost") with { Original = Snapshot(original) });
        var plan = ChangeHistoricalRefreshPlanner.Plan(review, input);
        Assert.Equal(classification, plan.Proposals[0].PriorAnnotationStatus);
        Assert.Equal("unresolved-workday", plan.Proposals[0].EvidenceStatus);
        Assert.StartsWith("Factual task: rebased and tested.\n", plan.Proposals[0].ProposedDescription, StringComparison.Ordinal);
        Assert.EndsWith("\nUser text: no work occurred on vacation.", plan.Proposals[0].ProposedDescription, StringComparison.Ordinal);
        Assert.Contains("External implementation record exists on this date", plan.Proposals[0].ProposedDescription, StringComparison.Ordinal);
        Assert.Contains("not measured by Change EHE", plan.Proposals[0].ProposedDescription, StringComparison.Ordinal);
        Assert.DoesNotContain("squash", plan.Proposals[0].ProposedDescription!, StringComparison.Ordinal);
        Assert.DoesNotContain("because of rebase", plan.Proposals[0].ProposedDescription!, StringComparison.Ordinal);
        Assert.Null(plan.Proposals[0].ProposedMultiplierContribution);
        var repeated = ChangeHistoricalRefreshPlanner.Plan(review, input with { Entries = [input.Entries[0] with { Original = Snapshot(plan.Proposals[0].ProposedDescription!) }] });
        Assert.Equal("unchanged", repeated.Proposals[0].NoteStatus);
        Assert.Empty(ContractValidation.Validate(repeated));
    }

    [Theory]
    [InlineData("mixed")]
    [InlineData("implementation")]
    public async Task UnresolvedMatchingBlocksNewNotesAndNumericProposals(string kind)
    {
        var source = await WorkdaySourceAsync();
        var review = ChangeWorkdayReviewer.Review(source, WorkRecords(source, Record(source, "ambiguous", 0) with { Kind = kind, RepositoryIds = ["wrong-scope"] }));
        var plan = ChangeHistoricalRefreshPlanner.Plan(review, RefreshInput(review, RefreshEntry("ambiguous")), "both");
        Assert.Equal("blocked-unresolved-matching", plan.Proposals[0].NoteStatus);
        Assert.Equal("blocked-unresolved-attribution", plan.Proposals[0].EheStatus);
        Assert.Null(plan.Proposals[0].ProposedDescription);
        Assert.Null(plan.Proposals[0].ProposedNoteRecordDigest);
        Assert.Equal("blocked", ChangeHistoricalRefreshPreflight.Check(plan, plan.Input).Status);
    }

    [Fact]
    public void AnnotationClassificationNeverReadsUnmanagedUserVerdicts()
    {
        Assert.Equal("none", ChangeHistoricalAnnotation.Classify("No work occurred. Factual original note."));
        Assert.Equal("ambiguous-managed-annotation", ChangeHistoricalAnnotation.Classify("[EffortHours historical annotation] No work occurred."));
        Assert.Equal("other-managed-annotation", ChangeHistoricalAnnotation.Classify("[EffortHours historical annotation]\nThis cannot prove zero work.\n[/EffortHours historical annotation]"));
    }
}
