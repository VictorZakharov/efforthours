using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class CalibrationUncertaintyGraphEvaluationTests
{
    [Fact]
    public async Task EvaluationIsDeterministicSchemaValidAndDevelopmentOnly()
    {
        (CalibrationCorpus corpus, CalibrationUncertaintyGraphFeatureReport[] reports) =
            await CreateFixtureAsync();

        CalibrationUncertaintyGraphEvaluationReport first =
            CalibrationUncertaintyEvaluator.EvaluateGraphDevelopment(corpus, reports);
        CalibrationUncertaintyGraphEvaluationReport second =
            CalibrationUncertaintyEvaluator.EvaluateGraphDevelopment(
                corpus,
                [.. reports.Reverse()]);
        string json = ContractJson.Serialize(first);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationUncertaintyGraphEvaluation,
            json);

        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(first));
        Assert.Equal(json, ContractJson.Serialize(second));
        Assert.Equal(14, first.Features.Count);
        Assert.Equal(
            CalibrationUncertaintyVersions.GraphEvaluationPolicyDigestV1,
            first.EvaluationPolicyDigest);
        Assert.True(first.EvaluationPolicy.LabelIndependent);
        Assert.True(first.Protocol.RepositoryIsolated);
        Assert.True(first.Protocol.DevelopmentOnly);
        Assert.False(first.Protocol.FitsProductionModel);
        Assert.Equal(
            "union-of-unique-mapped-node-ids-per-target",
            first.Protocol.TargetNodePopulation);
        Assert.All(first.Targets, target => Assert.Equal(14, target.Features.Count));
        Assert.All(first.Features, feature =>
        {
            Assert.Equal(feature.FeatureId, feature.Evaluation.FeatureId);
            Assert.Equal(first.Targets.Count, feature.Evaluation.Availability.ObservationCount);
            Assert.Equal(first.Repositories.Count, feature.Evaluation.RepositoryFolds.Count);
        });
    }

    [Fact]
    public async Task TargetAggregationUnionsNodesBeforeRecomputingGraphFeatures()
    {
        (CalibrationCorpus sourceCorpus, CalibrationUncertaintyGraphFeatureReport[] sourceReports) =
            await CreateFixtureAsync();
        CalibrationUncertaintyGraphFeatureReport sourceReport = sourceReports[0];
        CalibrationUncertaintyGraphWorkItemMapping[] pair =
        [
            .. sourceReport.WorkItems.Where(item => item.NodeIds.Count > 0)
                .GroupBy(item => item.Category)
                .First(group => group.Count() >= 2)
                .Take(2),
        ];
        string[] selectedNodeIds =
        [
            .. sourceReport.Nodes.Take(3).Select(node => node.NodeId)
                .Order(StringComparer.Ordinal),
        ];
        CalibrationUncertaintyGraphWorkItemMapping[] changedItems =
        [
            pair[0] with { NodeIds = [selectedNodeIds[0], selectedNodeIds[1]] },
            pair[1] with { NodeIds = [selectedNodeIds[0], selectedNodeIds[2]] },
        ];
        CalibrationUncertaintyGraphFeatureReport changedReport = sourceReport with
        {
            WorkItems =
            [
                .. sourceReport.WorkItems.Select(item => changedItems.SingleOrDefault(changed =>
                    changed.WorkItemId == item.WorkItemId) ?? item),
            ],
        };
        Assert.Empty(ContractValidation.Validate(changedReport));

        CalibrationRecord sourceRecord = sourceCorpus.Records[0];
        CalibrationTarget firstTarget = sourceRecord.Targets.Single(target =>
            target.SourceWorkItemIds.SequenceEqual([pair[0].WorkItemId], StringComparer.Ordinal));
        CalibrationTarget combined = firstTarget with
        {
            Id = firstTarget.Id + ":combined",
            SourceWorkItemIds = [pair[0].WorkItemId, pair[1].WorkItemId],
            Hours = Symmetric(pair.Sum(item => item.ExpectedHours) + 0.5m),
        };
        HashSet<string> replacedIds =
            [pair[0].WorkItemId, pair[1].WorkItemId];
        CalibrationRecord changedRecord = sourceRecord with
        {
            Targets =
            [
                .. sourceRecord.Targets.Where(target =>
                    !target.SourceWorkItemIds.Any(replacedIds.Contains)),
                combined,
            ],
        };
        CalibrationCorpus corpus = sourceCorpus with
        {
            Records = [changedRecord, .. sourceCorpus.Records.Skip(1)],
        };
        CalibrationUncertaintyGraphFeatureReport[] reports =
            [changedReport, .. sourceReports.Skip(1)];

        CalibrationUncertaintyGraphEvaluationReport evaluation =
            CalibrationUncertaintyEvaluator.EvaluateGraphDevelopment(corpus, reports);
        CalibrationUncertaintyGraphTargetEvaluation target = evaluation.Targets.Single(value =>
            value.Source.RecordId == changedRecord.Id && value.Source.TargetId == combined.Id);
        decimal expectedP50 = sourceReport.Nodes.Where(node =>
                selectedNodeIds.Contains(node.NodeId, StringComparer.Ordinal))
            .Select(node => (decimal)node.FanIn)
            .Order()
            .ElementAt(1);

        Assert.Equal(selectedNodeIds, target.NodeIds);
        Assert.Equal(3, target.NodeIds.Count);
        Assert.Equal(
            expectedP50,
            TargetValue(target, CalibrationUncertaintyGraphFeatureIds.FanInP50));
        Assert.Equal(
            pair.Sum(item => item.ExpectedHours),
            target.Source.CandidateRange.Expected);
    }

    [Fact]
    public async Task EvaluationRejectsSealedPartitionsAndNonCanonicalReports()
    {
        (CalibrationCorpus source, CalibrationUncertaintyGraphFeatureReport[] reports) =
            await CreateFixtureAsync();
        CalibrationCorpus validation = source with
        {
            Records = [.. source.Records.Select(record => record with
            {
                Partition = CalibrationPartition.Validation,
            })],
        };
        CalibrationEvaluationException sealedPartition =
            Assert.Throws<CalibrationEvaluationException>(() =>
                CalibrationUncertaintyEvaluator.EvaluateGraphDevelopment(validation, reports));
        CalibrationUncertaintyFeatureContract alteredContract = reports[0].FeatureContract with
        {
            Features =
            [
                reports[0].FeatureContract.Features[0] with
                {
                    Description = "Synthetic noncanonical description.",
                },
                .. reports[0].FeatureContract.Features.Skip(1),
            ],
        };
        CalibrationUncertaintyGraphFeatureReport altered = reports[0] with
        {
            FeatureContract = alteredContract,
            FeatureContractDigest = CalibrationDigest.Compute(alteredContract),
        };
        CalibrationEvaluationException noncanonical =
            Assert.Throws<CalibrationEvaluationException>(() =>
                CalibrationUncertaintyEvaluator.EvaluateGraphDevelopment(
                    source,
                    [altered, .. reports.Skip(1)]));

        Assert.Contains(sealedPartition.Errors, error => error.Contains(
            "development-only corpus",
            StringComparison.Ordinal));
        Assert.Contains(noncanonical.Errors, error => error.Contains(
            "canonical frozen feature contract",
            StringComparison.Ordinal));
    }

    private static async Task<(CalibrationCorpus Corpus,
        CalibrationUncertaintyGraphFeatureReport[] Reports)> CreateFixtureAsync()
    {
        List<CalibrationRecord> records = [];
        List<CalibrationUncertaintyGraphFeatureReport> reports = [];
        for (int index = 0; index < 4; index++)
        {
            InMemoryRepository repository = new();
            repository.WriteText("A/A.csproj", Project("../B/B.csproj", "../C/C.csproj"));
            repository.WriteText("B/B.csproj", Project("../C/C.csproj"));
            repository.WriteText("C/C.csproj", Project());
            repository.WriteText("A/A.cs", $"public class A{index} {{ public void Run() {{ }} }}");
            repository.WriteText("B/B.cs", $"public class B{index} {{ public void Run() {{ }} }}");
            repository.WriteText("C/C.cs", $"internal class C{index} {{ private void Hide() {{ }} }}");
            RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
                .ScanAsync(repository.RootPath);
            EstimateReport estimate = new SeedEstimator().Estimate(
                evidence,
                EstimationProfile.Implementation);
            CalibrationUncertaintyGraphFeatureReport report =
                CalibrationUncertaintyGraphFeatureProjector.Project(estimate, evidence);
            reports.Add(report);
            records.Add(CreateRecord(index, report));
        }

        return (
            new CalibrationCorpus
            {
                Id = "graph-uncertainty-evaluation-fixture",
                Version = "1.0.0",
                Description = "Synthetic development-only graph evaluation fixture.",
                Rubric = new CalibrationRubricReference
                {
                    Id = "ehe-work-item",
                    Version = "1.1.0",
                },
                Records = records,
            },
            [.. reports]);
    }

    private static CalibrationRecord CreateRecord(
        int index,
        CalibrationUncertaintyGraphFeatureReport report) => new()
        {
            Id = $"record:graph-uncertainty-{index}",
            Repository = new CalibrationRepositoryReference
            {
                Id = $"repository:graph-uncertainty-{index}",
                Name = $"Synthetic graph uncertainty repository {index}",
                SourceDigest = report.RepositorySourceDigest,
            },
            Profile = report.Profile,
            BaselineId = report.BaselineId,
            Partition = CalibrationPartition.Development,
            SourceEstimatorVersion = report.EstimatorVersion,
            SourceEstimateDigest = report.EstimateDigest,
            Source = new CalibrationSourceProvenance
            {
                DataClassification = CalibrationDataClassification.Synthetic,
                SourceReference = $"eh://tests/graph-uncertainty-evaluation/{index}",
                Revision = "1",
                LicenseExpression = "MIT",
                RedistributionAllowed = true,
            },
            Review = new CalibrationReviewProvenance
            {
                Status = CalibrationReviewStatus.TeacherEstimate,
                CompletedOn = new DateOnly(2026, 8, 16),
                Reviewers =
                [
                    new CalibrationReviewer
                    {
                        Id = "host-ai:graph-uncertainty-evaluation-fixture",
                        Kind = CalibrationReviewerKind.HostAi,
                        Role = CalibrationReviewerRole.Teacher,
                        ModelId = "synthetic-test-model",
                        ModelVersion = "1",
                    },
                ],
            },
            Targets = [.. report.WorkItems.OrderBy(item => item.WorkItemId, StringComparer.Ordinal)
                .Select((item, itemIndex) => new CalibrationTarget
                {
                    Id = $"target:{item.WorkItemId}",
                    Category = item.Category,
                    Title = "Synthetic graph uncertainty target",
                    Scope = "synthetic-fixture",
                    SourceWorkItemIds = [item.WorkItemId],
                    EvidenceIds = item.ResolvedEvidenceIds.Count > 0
                        ? item.ResolvedEvidenceIds
                        : ["evidence:synthetic"],
                    Hours = Symmetric(item.ExpectedHours + 0.25m + index +
                        ((itemIndex % 2) * 0.25m)),
                    Rationale = "Synthetic reviewed range for graph evaluation.",
                    SizeException = "Synthetic fixture spans ordinary size bands.",
                })],
        };

    private static string Project(params string[] references)
    {
        string items = string.Join(
            string.Empty,
            references.Select(reference => $"<ProjectReference Include=\"{reference}\" />"));
        return "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
            "<TargetFramework>net10.0</TargetFramework></PropertyGroup>" +
            $"<ItemGroup>{items}</ItemGroup></Project>";
    }

    private static EffortRange Symmetric(decimal expected)
    {
        decimal halfWidth = decimal.Min(1m, expected);
        return new EffortRange
        {
            Low = expected - halfWidth,
            Expected = expected,
            High = expected + halfWidth,
        };
    }

    private static decimal TargetValue(
        CalibrationUncertaintyGraphTargetEvaluation target,
        string featureId) => target.Features.Single(feature => feature.FeatureId == featureId)
            .Value!.Value;
}
