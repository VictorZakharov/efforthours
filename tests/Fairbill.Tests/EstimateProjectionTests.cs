using Fairbill.Contracts;
using Fairbill.Contracts.V1;
using Fairbill.Estimation;
using Fairbill.Pricing;
using Fairbill.Reporting;

namespace Fairbill.Tests;

public sealed class EstimateProjectionTests
{
    private readonly RepositoryEvidence _evidence = TestRepositoryEvidence.CreateStructuredDotNet(
        sourceCopies: 8,
        endpoints: 4,
        testCases: 24,
        declaredCoverage: 80m,
        includeDocumentation: false,
        includeCi: false);

    [Theory]
    [InlineData(EstimateViewKind.Repository)]
    [InlineData(EstimateViewKind.Category)]
    [InlineData(EstimateViewKind.Scope)]
    [InlineData(EstimateViewKind.WorkItem)]
    [InlineData(EstimateViewKind.Review)]
    public void EveryProjectionIsSemanticallyAndSchemaValid(EstimateViewKind view)
    {
        EstimateReport report = Estimate();
        EstimateViewReport projection = EstimateProjector.Project(report, view);
        string json = new EstimateViewJsonRenderer().Render(projection);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.EstimateView,
            json);

        Assert.Empty(ContractValidation.Validate(projection));
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Equal(report.TotalEffort, projection.TotalEffort);
        Assert.Equal(report.TotalCost, projection.TotalCost);
        Assert.Equal(report.WorkItems.Count, projection.Counts.RepresentedWorkItems);
    }

    [Fact]
    public void CategoryScopeAndCapabilityViewsPreserveRepresentedTotals()
    {
        EstimateReport report = Estimate();
        EstimateViewReport category = EstimateProjector.Project(report, EstimateViewKind.Category);
        EstimateViewReport scope = EstimateProjector.Project(report, EstimateViewKind.Scope);
        EstimateViewReport workItem = EstimateProjector.Project(report, EstimateViewKind.WorkItem);

        Assert.Equal(report.TotalEffort, ContractValidation.Sum(category.Categories.Select(row => row.Hours)));
        Assert.Equal(report.TotalEffort, ContractValidation.Sum(scope.Scopes.Select(row => row.Hours)));
        Assert.Equal(report.TotalEffort, ContractValidation.Sum(workItem.Capabilities.Select(row => row.Hours)));
        Assert.Equal(report.WorkItems.Count, workItem.Capabilities.Sum(row => row.WorkItemCount));
        Assert.True(workItem.Capabilities.Count < report.WorkItems.Count);
        Assert.All(workItem.Capabilities, row => Assert.DoesNotContain(":part-", row.Id, StringComparison.Ordinal));
        Assert.All(
            workItem.Capabilities,
            row => Assert.Equal(
                row.Hours.Expected * DefaultRateCatalog.RateCard.HourlyRate,
                row.Cost!.Expected));
    }

    [Fact]
    public void ReviewProjectionIsBoundedAndAccountsForOmissions()
    {
        EstimateReport report = Estimate();
        EstimateViewReport all = EstimateProjector.Project(report, EstimateViewKind.WorkItem);
        EstimateViewReport review = EstimateProjector.Project(report, EstimateViewKind.Review);

        Assert.InRange(review.Scopes.Count, 0, EstimateProjector.MaximumReviewScopes);
        Assert.InRange(review.ReviewQueue.Count, 0, EstimateProjector.MaximumReviewCapabilities);
        Assert.Equal(
            all.Capabilities.Count - review.ReviewQueue.Count,
            review.Omissions.CapabilityCount);
        Assert.Equal(
            all.Capabilities
                .Where(capability => review.ReviewQueue.All(row => row.Id != capability.Id))
                .Sum(capability => capability.Hours.Expected),
            review.Omissions.CapabilityExpectedHours);
        Assert.All(review.ReviewQueue, row => Assert.NotEmpty(row.ReviewReasons));
    }

    [Fact]
    public void CompactProjectionRoundTripsAndIsSmallerThanPrettyJson()
    {
        EstimateViewReport projection = EstimateProjector.Project(
            Estimate(),
            EstimateViewKind.Review);
        string pretty = new EstimateViewJsonRenderer().Render(projection);
        string compact = new EstimateViewJsonRenderer(compact: true).Render(projection);
        EstimateViewReport roundTrip = ContractJson.Deserialize<EstimateViewReport>(compact);

        Assert.True(compact.Length < pretty.Length);
        Assert.DoesNotContain('\n', compact);
        Assert.Equal(projection.View, roundTrip.View);
        Assert.Equal(projection.TotalEffort, roundTrip.TotalEffort);
        Assert.Equal(projection.ReviewQueue.Count, roundTrip.ReviewQueue.Count);
    }

    [Fact]
    public void ProjectionValidationRejectsCostThatDoesNotMatchEffortAndRate()
    {
        EstimateViewReport projection = EstimateProjector.Project(
            Estimate(),
            EstimateViewKind.Category);
        EstimateViewReport tampered = projection with
        {
            TotalCost = projection.TotalCost! with
            {
                Expected = projection.TotalCost.Expected + 1m,
            },
        };

        IReadOnlyList<string> errors = ContractValidation.Validate(tampered);

        Assert.Contains(
            errors,
            error => error.Contains("does not equal effort", StringComparison.Ordinal));
    }

    [Fact]
    public void ExplanationTracesExactWorkItemToEvidenceAndEstimator()
    {
        EstimateReport report = Estimate();
        WorkItem item = report.WorkItems[0];
        EstimateExplanation explanation = EstimateProjector.Explain(report, _evidence, item.Id);
        string json = new EstimateExplanationJsonRenderer(compact: true).Render(explanation);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.EstimateExplanation,
            json);

        Assert.Equal(ExplanationMatchKind.WorkItem, explanation.MatchKind);
        Assert.Single(explanation.WorkItems);
        Assert.Equal(item.Id, explanation.WorkItems[0].Id);
        Assert.Empty(explanation.MissingEvidenceIds);
        Assert.NotEmpty(explanation.EvidenceFacts);
        Assert.NotEmpty(explanation.Estimators);
        Assert.Contains(explanation.Diagnostics, diagnostic => diagnostic.Code == "FB1000");
        Assert.Contains(explanation.Diagnostics, diagnostic => diagnostic.Code == "FB1005");
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
    }

    [Fact]
    public void ExplanationCanExpandAStableCapabilityIdAcrossSlices()
    {
        EstimateReport report = Estimate();
        IGrouping<string, WorkItem> group = report.WorkItems
            .GroupBy(item => EstimateProjector.GetCapabilityId(item.Id), StringComparer.Ordinal)
            .First(items => items.Count() > 1);
        string capabilityId = group.Key;
        WorkItem[] expectedItems = [.. group];

        EstimateExplanation explanation = EstimateProjector.Explain(
            report,
            _evidence,
            capabilityId);

        Assert.Equal(ExplanationMatchKind.Capability, explanation.MatchKind);
        Assert.Equal(expectedItems.Length, explanation.WorkItems.Count);
        Assert.Equal(capabilityId, explanation.Capability.Id);
        Assert.Equal(
            ContractValidation.Sum(expectedItems.Select(item => item.Hours)),
            explanation.Capability.Hours);
    }

    [Fact]
    public void MarkdownViewsPreserveTraceabilityAndGapTreatment()
    {
        EstimateReport report = Estimate();
        EstimateViewReport view = EstimateProjector.Project(report, EstimateViewKind.Review);
        string viewMarkdown = EstimateViewMarkdownRenderer.Render(view);
        string capabilityId = EstimateProjector.GetCapabilityId(report.WorkItems[0].Id);
        EstimateExplanation explanation = EstimateProjector.Explain(
            report,
            _evidence,
            capabilityId);
        string explanationMarkdown = EstimateExplanationMarkdownRenderer.Render(explanation);

        Assert.Contains("excluded from represented EHE", viewMarkdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(view.ReviewQueue[0].Id, viewMarkdown, StringComparison.Ordinal);
        Assert.Contains(capabilityId, explanationMarkdown, StringComparison.Ordinal);
        Assert.Contains(explanation.EvidenceFacts[0].Id, explanationMarkdown, StringComparison.Ordinal);
    }

    [Fact]
    public void UnknownExplanationIdIsRejected()
    {
        EstimateReport report = Estimate();

        KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(
            () => EstimateProjector.Explain(report, _evidence, "work:not-present"));

        Assert.Contains("work:not-present", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AmbiguousExplanationIdIsRejected()
    {
        EstimateReport report = Estimate();
        IGrouping<string, WorkItem> group = report.WorkItems
            .GroupBy(item => EstimateProjector.GetCapabilityId(item.Id), StringComparer.Ordinal)
            .First(items => items.Count() > 1);
        WorkItem first = group.First();
        WorkItem renamed = first with { Id = group.Key };
        EstimateReport ambiguous = report with
        {
            WorkItems =
            [
                renamed,
                .. report.WorkItems.Where(item => item.Id != first.Id),
            ],
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => EstimateProjector.Explain(ambiguous, _evidence, group.Key));

        Assert.Contains("ambiguous", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExplanationRejectsEvidenceFromAnotherRepository()
    {
        EstimateReport report = Estimate();
        RepositoryEvidence mismatched = _evidence with
        {
            Repository = _evidence.Repository with { SourceDigest = "sha256:different" },
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => EstimateProjector.Explain(report, mismatched, report.WorkItems[0].Id));

        Assert.Contains("does not match", exception.Message, StringComparison.Ordinal);
    }

    private EstimateReport Estimate() => new SeedEstimator().Estimate(
        _evidence,
        EstimationProfile.Implementation,
        DefaultRateCatalog.RateCard);
}
