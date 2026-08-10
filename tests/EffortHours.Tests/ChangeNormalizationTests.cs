using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class ChangeNormalizationTests
{
    [Fact]
    public void TenToFiveReportsFiftyPercentNormalizationWithoutCallingAllOfItRework()
    {
        ChangeNormalizationSummary summary = Calculate(
            grossExpected: 10m,
            normalizedExpected: 5m,
            adjustments:
            [
                Adjustment("shared", ChangeAdjustmentKind.SharedSetup, -1m),
                Adjustment("overlap", ChangeAdjustmentKind.Overlap, -2m),
                Adjustment("revert", ChangeAdjustmentKind.Revert, -1m),
                Adjustment("interaction", ChangeAdjustmentKind.Interaction, -1m),
            ]);

        Assert.Equal(ChangeNormalizationStatus.Calculated, summary.Status);
        Assert.Equal(5m, summary.ExpectedGrossToFinalNormalizationHours);
        Assert.Equal(0.5000m, summary.ExpectedGrossToFinalNormalizationShare);
        Assert.Equal(3m, summary.ExpectedReworkLikeHours);
        Assert.Equal(0.3000m, summary.ExpectedReworkLikeShare);
        Assert.Equal(2m, summary.ExpectedOtherNormalizationHours);
        Assert.Equal(0.2000m, summary.ExpectedOtherNormalizationShare);
        Assert.Equal(1m, summary.ExpectedSharedOrRepeatedHours);
        Assert.Equal(2m, summary.ExpectedOverlapHours);
        Assert.Equal(1m, summary.ExpectedRevertHours);
        Assert.Equal(1m, summary.ExpectedResidualInteractionHours);
        Assert.Equal(["overlap"], summary.OverlapAdjustmentIds);
        Assert.Equal(["revert"], summary.RevertAdjustmentIds);
    }

    [Fact]
    public void CleanAdditivityReportsZeroShares()
    {
        ChangeNormalizationSummary summary = Calculate(6m, 6m, []);

        Assert.Equal(0m, summary.ExpectedGrossToFinalNormalizationHours);
        Assert.Equal(0m, summary.ExpectedGrossToFinalNormalizationShare);
        Assert.Equal(0m, summary.ExpectedReworkLikeHours);
        Assert.Equal(0m, summary.ExpectedReworkLikeShare);
        Assert.Equal(0m, summary.ExpectedOtherNormalizationShare);
        Assert.Equal(0m, summary.ExpectedPositiveInteractionHours);
    }

    [Fact]
    public void ReworkLikeAttributionIsBoundedByGrossToFinalNormalization()
    {
        ChangeNormalizationSummary summary = Calculate(
            10m,
            5m,
            [
                Adjustment("overlap", ChangeAdjustmentKind.Overlap, -6m),
                Adjustment("interaction", ChangeAdjustmentKind.Interaction, 1m),
            ]);

        Assert.Equal(5m, summary.ExpectedGrossToFinalNormalizationHours);
        Assert.Equal(6m, summary.ExpectedOverlapHours);
        Assert.Equal(5m, summary.ExpectedReworkLikeHours);
        Assert.Equal(0.5000m, summary.ExpectedReworkLikeShare);
        Assert.Equal(0m, summary.ExpectedOtherNormalizationHours);
        Assert.Equal(1m, summary.ExpectedPositiveInteractionHours);
    }

    [Fact]
    public void ZeroGrossMakesAllSharesNotApplicable()
    {
        ChangeNormalizationSummary summary = Calculate(0m, 0m, []);

        Assert.Equal(ChangeNormalizationStatus.NotApplicableZeroGross, summary.Status);
        Assert.Null(summary.ExpectedGrossToFinalNormalizationShare);
        Assert.Null(summary.ExpectedReworkLikeShare);
        Assert.Null(summary.ExpectedOtherNormalizationShare);
    }

    [Fact]
    public void NormalizedAboveGrossPreservesPositiveInteractionWithoutNegativeNormalization()
    {
        ChangeNormalizationSummary summary = Calculate(
            5m,
            6m,
            [Adjustment("interaction", ChangeAdjustmentKind.Interaction, 1m)]);

        Assert.Equal(0m, summary.ExpectedGrossToFinalNormalizationHours);
        Assert.Equal(0m, summary.ExpectedGrossToFinalNormalizationShare);
        Assert.Equal(0m, summary.ExpectedReworkLikeHours);
        Assert.Equal(1m, summary.ExpectedPositiveInteractionHours);
        Assert.Equal(["interaction"], summary.PositiveInteractionAdjustmentIds);
    }

    [Fact]
    public void SharesRoundToFourDecimalsAwayFromZero()
    {
        ChangeNormalizationSummary summary = Calculate(
            3m,
            2m,
            [Adjustment("overlap", ChangeAdjustmentKind.Overlap, -1m)]);

        Assert.Equal(0.3333m, summary.ExpectedGrossToFinalNormalizationShare);
        Assert.Equal(0.3333m, summary.ExpectedReworkLikeShare);
    }

    [Fact]
    public void DiagnosticRequiresAnExplicitMultiCommitRange()
    {
        EffortRange effort = Range(2m);
        ChangeComponentEstimate[] components = Components(1m, 1m);

        Assert.Null(ChangeNormalizationCalculator.Calculate(
            Selection(ChangeSelectionKind.BaseHead),
            effort,
            effort,
            components,
            []));
        Assert.Null(ChangeNormalizationCalculator.Calculate(
            Selection(ChangeSelectionKind.Commit),
            effort,
            effort,
            components,
            []));
        Assert.Null(ChangeNormalizationCalculator.Calculate(
            Selection(ChangeSelectionKind.PullRequest),
            effort,
            effort,
            components,
            []));
        Assert.Null(ChangeNormalizationCalculator.Calculate(
            Selection(ChangeSelectionKind.Range),
            effort,
            effort,
            [components[0]],
            []));
    }

    private static ChangeNormalizationSummary Calculate(
        decimal grossExpected,
        decimal normalizedExpected,
        IReadOnlyList<ChangeAdjustment> adjustments) =>
        ChangeNormalizationCalculator.Calculate(
            Selection(ChangeSelectionKind.Range),
            Range(grossExpected),
            Range(normalizedExpected),
            Components(grossExpected / 2m, grossExpected / 2m),
            adjustments)!;

    private static ChangeSelection Selection(ChangeSelectionKind kind) => new()
    {
        Kind = kind,
        Base = new ChangeSnapshotReference
        {
            Selector = "base",
            ObjectId = "base-id",
            Kind = ChangeSnapshotKind.GitCommit,
        },
        Head = new ChangeSnapshotReference
        {
            Selector = "head",
            ObjectId = "head-id",
            Kind = ChangeSnapshotKind.GitCommit,
        },
        Commit = kind == ChangeSelectionKind.Commit ? "head" : null,
        Range = kind == ChangeSelectionKind.Range ? "base..head" : null,
        PullRequest = kind == ChangeSelectionKind.PullRequest
            ? new PullRequestReference { Input = "1", Number = 1 }
            : null,
    };

    private static ChangeComponentEstimate[] Components(decimal first, decimal second) =>
    [
        Component("component-1", first),
        Component("component-2", second),
    ];

    private static ChangeComponentEstimate Component(string id, decimal expected) => new()
    {
        Id = id,
        Kind = ChangeComponentKind.Commit,
        Selector = id,
        BaseObjectId = $"{id}-base",
        HeadObjectId = $"{id}-head",
        IsolatedEffort = Range(expected),
        AllocatedExpectedHours = expected,
    };

    private static ChangeAdjustment Adjustment(
        string id,
        ChangeAdjustmentKind kind,
        decimal expected) => new()
        {
            Id = id,
            Kind = kind,
            EffortDelta = new SignedEffortRange
            {
                Low = expected,
                Expected = expected,
                High = expected,
            },
            Reason = "Synthetic reconciliation test adjustment.",
        };

    private static EffortRange Range(decimal expected) => new()
    {
        Low = expected,
        Expected = expected,
        High = expected,
    };
}
