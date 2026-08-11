using EffortHours.Calibration;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ChangeCalibrationTests
{
    [Fact]
    public async Task CurrentScaffoldRequiresDecomposedModelAuthoredLogicAndPreservesLegacyReproduction()
    {
        ChangeEstimateReport estimate = await EstimateAsync(
            State(),
            State(("src/status.ts", "export const status = 'ready';\n")));

        CalibrationAuthoringPacket current = ChangeCalibrationAuthoring.Scaffold(
            estimate,
            "repository:logical-model-label",
            "change:logical-model-label",
            ["typescript", "production"]);
        CalibrationAuthoringPacket legacy = ChangeCalibrationAuthoring.ScaffoldLegacy100(
            estimate,
            "repository:logical-model-label",
            "change:logical-model-label",
            ["typescript", "production"]);

        Assert.Equal("change-calibration-authoring/0.2.0", current.AuthoringVersion);
        Assert.Equal("1.1.0", current.Rubric.Version);
        Assert.Contains(current.Instructions, instruction =>
            instruction.Contains("host-AI teacher", StringComparison.Ordinal));
        Assert.Contains(current.Instructions, instruction =>
            instruction.Contains("0.5 to 1.5", StringComparison.Ordinal));
        Assert.Equal("change-calibration-authoring/0.1.0", legacy.AuthoringVersion);
        Assert.Equal("1.0.0", legacy.Rubric.Version);
    }

    [Fact]
    public async Task RubricElevenRequiresExceptionAboveTwoHourLogicalBlock()
    {
        ChangeEstimateReport estimate = await EstimateAsync(
            State(),
            State(("src/status.ts", "export const status = 'ready';\n")));
        CalibrationAuthoringPacket packet = ChangeCalibrationAuthoring.Scaffold(
            estimate,
            "repository:logical-boundary",
            "change:logical-boundary",
            ["typescript", "production"]);
        CalibrationReviewPlan plan = Plan(packet, estimate, CalibrationPartition.Development);
        CalibrationCapabilityReviewDecision capability = plan.Records[0].Capabilities[0];
        CalibrationReviewTargetDecision target = capability.Targets[0] with
        {
            Hours = new EffortRange { Low = 1m, Expected = 3m, High = 5m },
            SizeException = null,
        };
        plan = plan with
        {
            Records =
            [
                plan.Records[0] with
                {
                    Capabilities =
                    [
                        capability with { Targets = [target] },
                        .. plan.Records[0].Capabilities.Skip(1),
                    ],
                },
            ],
        };

        CalibrationEvaluationException exception = Assert.Throws<CalibrationEvaluationException>(() =>
            ChangeCalibrationReviewCompiler.Compile(plan, [estimate]));

        Assert.Contains(exception.Errors, error =>
            error.Contains("2-hour logical-decomposition boundary", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RubricElevenRequiresCurrentCompilerWhileLegacyRubricRemainsReproducible()
    {
        ChangeEstimateReport estimate = await EstimateAsync(
            State(),
            State(("src/status.ts", "export const status = 'ready';\n")));
        CalibrationAuthoringPacket packet = ChangeCalibrationAuthoring.Scaffold(
            estimate,
            "repository:compiler-boundary",
            "change:compiler-boundary",
            ["typescript", "production"]);
        CalibrationReviewPlan plan = Plan(packet, estimate, CalibrationPartition.Development) with
        {
            CompilerVersion = ChangeCalibrationReviewCompiler.LegacyCompilerVersion,
        };

        CalibrationEvaluationException exception = Assert.Throws<CalibrationEvaluationException>(() =>
            ChangeCalibrationReviewCompiler.Compile(plan, [estimate]));

        Assert.Contains(exception.Errors, error =>
            error.Contains("retained only for rubric 1.0.0 reproduction", StringComparison.Ordinal));
    }
}
