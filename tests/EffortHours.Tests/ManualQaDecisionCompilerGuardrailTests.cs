using EffortHours.Calibration;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ManualQaDecisionCompilerTests
{
    [Fact]
    public void CompilerRejectsIncompleteTamperedAndUnboundedPlans()
    {
        Fixture fixture = CreateFixture();
        ManualQaDecisionPlan template = CreateTemplate(fixture);
        ManualQaDecisionPlan completed = Complete(template, fixture.Packets);
        ManualQaDecisionPlanRecord record = Assert.Single(completed.Records);
        ManualQaDecision first = record.Decisions[0];

        Assert.Throws<CalibrationEvaluationException>(() => Compile(fixture, template));
        Assert.Throws<CalibrationEvaluationException>(() => Compile(
            fixture,
            completed with
            {
                DecisionCount = 1,
                Records = [record with { Decisions = [first] }],
            }));
        Assert.Throws<CalibrationEvaluationException>(() => Compile(
            fixture,
            completed with
            {
                Records =
                [
                    record with
                    {
                        Decisions =
                        [
                            first with { SourceLineageDigest = Digest('f') },
                            record.Decisions[1],
                        ],
                    },
                ],
            }));
        Assert.Throws<CalibrationEvaluationException>(() => Compile(
            fixture,
            completed with
            {
                Records =
                [
                    record with
                    {
                        Decisions =
                        [
                            first with { EvidenceIds = ["evidence:unknown"] },
                            record.Decisions[1],
                        ],
                    },
                ],
            }));
        Assert.Throws<CalibrationEvaluationException>(() =>
            ManualQaDecisionCompiler.Compile(
                fixture.Corpus,
                fixture.ReviewPolicy,
                ReviewPolicyDigest,
                fixture.Manifest,
                fixture.Packets,
                fixture.CompilerPolicy with { Partition = CalibrationPartition.Validation },
                CompilerPolicyDigest,
                completed));
    }
}
