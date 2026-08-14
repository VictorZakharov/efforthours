using EffortHours.Contracts.V1;
using EffortHours.RepositoryCalibration;

namespace EffortHours.EndToEndTests;

public sealed partial class CandidatePreflightTests
{
    [Fact]
    public void LogicalCapabilityV3BoundsSpecificationSeparatelyAndRetainsV1Projection()
    {
        const string evidenceId = "common:repository";
        WorkItem item = Item(
            "work:specification-comprehension:logical:part-0001",
            "repository",
            2m) with
        {
            EvidenceIds = [evidenceId],
        };
        EstimateReport source = Report(item);
        RepositoryEvidence evidence = Evidence(
            source,
            new EvidenceFact
            {
                Id = evidenceId,
                Kind = "repository",
                Scope = "repository",
                Summary = "Synthetic repository evidence.",
                Provenance = Provenance(),
            });
        LogicalCandidatePointFactor point = new()
        {
            WorkItemKind = "specification-comprehension",
            LogicalSizeBand = "s",
            SampleCount = 3,
            LogicalExpectedHours = 6m,
            ReviewedExpectedHours = 24m,
            Factor = 4m,
        };
        LogicalCandidateRangeFactor range = new()
        {
            WorkItemKind = "specification-comprehension",
            ExpectedSizeBand = "l",
            SampleSource = "exact-kind-and-size",
            SampleCount = 3,
            LowFactor = 0.75m,
            HighFactor = 1.25m,
        };
        LogicalCandidateModel current = Model(point, range);

        EstimateReport candidate = LogicalCandidateTransformer.Transform(source, evidence, current);
        LogicalCandidateModel legacy = current with
        {
            ModelVersion = "repository-logical-capability-model/0.1.0",
            CandidateId = "logical-capability/0.1.0",
            EstimatorVersion = "candidate-logical-capability/0.1.0+seed-rules/0.4.0",
            FeatureContractVersion = "logical-capability-features/1.0.0",
            Point = current.Point with
            {
                ScorerVersion = LogicalCandidateScorer.LegacyVersion,
                MaximumFactorOverrides = [],
                SeedAnchorFactor = 0m,
                SeedAnchorMaximumLogicalHours = 0m,
                SeedAnchorWorkItemKinds = [],
                Factors = [point with { Factor = 3m }],
            },
            Range = current.Range with
            {
                UpperQuantile = LogicalCandidateModelFitter.RetiredUpperQuantile,
            },
        };
        EstimateReport legacyCandidate = LogicalCandidateTransformer.Transform(source, evidence, legacy);
        LogicalCandidateModel unrelatedCeiling = current with
        {
            Point = current.Point with
            {
                Factors = [point with { WorkItemKind = "self-review" }],
            },
        };

        Assert.Equal(8m, candidate.TotalEffort.Expected);
        Assert.Equal("0.3.0", candidate.WorkItems[0].Estimator.Version);
        Assert.Equal(6m, legacyCandidate.TotalEffort.Expected);
        Assert.Equal("0.1.0", legacyCandidate.WorkItems[0].Estimator.Version);
        Assert.Throws<InvalidDataException>(() => LogicalCandidateTransformer.Transform(
            source,
            evidence,
            unrelatedCeiling));
    }
}
