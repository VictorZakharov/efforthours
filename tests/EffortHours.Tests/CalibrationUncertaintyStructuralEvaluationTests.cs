using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class CalibrationUncertaintyStructuralEvaluationTests
{
    [Fact]
    public async Task EvaluationIsDeterministicSchemaValidAndDevelopmentOnly()
    {
        (CalibrationCorpus corpus, CalibrationUncertaintyStructuralFeatureReport[] reports) =
            await CreateFixtureAsync();

        CalibrationUncertaintyStructuralEvaluationReport first =
            CalibrationUncertaintyEvaluator.EvaluateStructuralDevelopment(corpus, reports);
        CalibrationUncertaintyStructuralEvaluationReport second =
            CalibrationUncertaintyEvaluator.EvaluateStructuralDevelopment(
                corpus,
                [.. reports.Reverse()]);
        string json = ContractJson.Serialize(first);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationUncertaintyStructuralEvaluation,
            json);

        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(first));
        Assert.Equal(json, ContractJson.Serialize(second));
        Assert.Equal(14, first.Features.Count);
        Assert.Equal(
            CalibrationUncertaintyVersions.StructuralEvaluationPolicyDigestV1,
            first.EvaluationPolicyDigest);
        Assert.True(first.EvaluationPolicy.LabelIndependent);
        Assert.True(first.Protocol.RepositoryIsolated);
        Assert.True(first.Protocol.DevelopmentOnly);
        Assert.False(first.Protocol.FitsProductionModel);
        Assert.All(first.Features, feature =>
        {
            Assert.Equal(feature.FeatureId, feature.Evaluation.FeatureId);
            Assert.Equal(first.Targets.Count, feature.Evaluation.Availability.ObservationCount);
            Assert.Equal(
                first.Repositories.Count,
                feature.Evaluation.RepositoryFolds.Count);
        });
        Assert.All(first.Targets, target => Assert.Equal(14, target.Features.Count));
    }

    [Fact]
    public async Task TargetAggregationUsesWorstLocalShapeAndWeakestCoverage()
    {
        (CalibrationCorpus sourceCorpus,
            CalibrationUncertaintyStructuralFeatureReport[] sourceReports) =
            await CreateFixtureAsync();
        CalibrationUncertaintyStructuralFeatureReport sourceReport = sourceReports[0];
        CalibrationUncertaintyStructuralWorkItemFeatures original = sourceReport.WorkItems.First(
            item => Available(item, CalibrationUncertaintyStructuralFeatureIds.CallableSizeMaximum) &&
                Available(
                    item,
                    CalibrationUncertaintyStructuralFeatureIds.CallableMeasurementCoverage));
        decimal originalMaximum = Value(
            original,
            CalibrationUncertaintyStructuralFeatureIds.CallableSizeMaximum);
        CalibrationUncertaintyStructuralWorkItemFeatures duplicate = original with
        {
            WorkItemId = original.WorkItemId + ":evaluation-duplicate",
            Features =
            [
                .. original.Features.Select(feature => feature.FeatureId switch
                {
                    CalibrationUncertaintyStructuralFeatureIds.CallableSizeMaximum =>
                        feature with { Value = originalMaximum + 100m },
                    CalibrationUncertaintyStructuralFeatureIds.CallableMeasurementCoverage =>
                        feature with { Value = 0m },
                    _ => feature,
                }),
            ],
        };
        CalibrationUncertaintyStructuralFeatureReport expandedReport = sourceReport with
        {
            WorkItems = [.. sourceReport.WorkItems, duplicate],
            Summary = AddToSummary(sourceReport.Summary, duplicate),
        };
        CalibrationRecord sourceRecord = sourceCorpus.Records[0];
        CalibrationTarget sourceTarget = sourceRecord.Targets.Single(target =>
            target.SourceWorkItemIds.Contains(original.WorkItemId, StringComparer.Ordinal));
        CalibrationTarget expandedTarget = sourceTarget with
        {
            SourceWorkItemIds = [original.WorkItemId, duplicate.WorkItemId],
            EvidenceIds = [.. sourceTarget.EvidenceIds.Distinct(StringComparer.Ordinal)],
            Hours = Symmetric(sourceTarget.Hours.Expected + duplicate.ExpectedHours),
        };
        CalibrationRecord expandedRecord = sourceRecord with
        {
            Targets = [.. sourceRecord.Targets.Select(target => target.Id == sourceTarget.Id
                ? expandedTarget
                : target)],
        };
        CalibrationCorpus corpus = sourceCorpus with
        {
            Records = [expandedRecord, .. sourceCorpus.Records.Skip(1)],
        };
        CalibrationUncertaintyStructuralFeatureReport[] reports =
            [expandedReport, .. sourceReports.Skip(1)];

        CalibrationUncertaintyStructuralEvaluationReport evaluation =
            CalibrationUncertaintyEvaluator.EvaluateStructuralDevelopment(corpus, reports);
        CalibrationUncertaintyStructuralTargetEvaluation target = evaluation.Targets.Single(value =>
            value.Source.RecordId == expandedRecord.Id &&
            value.Source.TargetId == expandedTarget.Id);

        Assert.Equal(
            originalMaximum + 100m,
            TargetValue(
                target,
                CalibrationUncertaintyStructuralFeatureIds.CallableSizeMaximum));
        Assert.Equal(
            0m,
            TargetValue(
                target,
                CalibrationUncertaintyStructuralFeatureIds.CallableMeasurementCoverage));
    }

    [Fact]
    public async Task EvaluationRejectsSealedPartitionsAndNonCanonicalReports()
    {
        (CalibrationCorpus source, CalibrationUncertaintyStructuralFeatureReport[] reports) =
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
                CalibrationUncertaintyEvaluator.EvaluateStructuralDevelopment(
                    validation,
                    reports));
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
        CalibrationUncertaintyStructuralFeatureReport altered = reports[0] with
        {
            FeatureContract = alteredContract,
            FeatureContractDigest = CalibrationDigest.Compute(alteredContract),
        };
        CalibrationEvaluationException noncanonical =
            Assert.Throws<CalibrationEvaluationException>(() =>
                CalibrationUncertaintyEvaluator.EvaluateStructuralDevelopment(
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
        CalibrationUncertaintyStructuralFeatureReport[] Reports)> CreateFixtureAsync()
    {
        List<CalibrationRecord> records = [];
        List<CalibrationUncertaintyStructuralFeatureReport> reports = [];
        decimal[] adjustments = [0.25m, 0.75m, 1.5m, 3m];
        for (int index = 0; index < adjustments.Length; index++)
        {
            InMemoryRepository repository = new();
            repository.WriteText(
                "App.csproj",
                $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><RootNamespace>Fixture{index}</RootNamespace></PropertyGroup></Project>\n");
            repository.WriteText("Service.cs", Source(index));
            RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
                .ScanAsync(repository.RootPath);
            EstimateReport estimate = new SeedEstimator().Estimate(
                evidence,
                EstimationProfile.Implementation);
            CalibrationUncertaintyStructuralFeatureReport report =
                CalibrationUncertaintyStructuralFeatureProjector.Project(estimate, evidence);
            reports.Add(report);
            records.Add(CreateRecord(index, report, adjustments[index]));
        }

        return (
            new CalibrationCorpus
            {
                Id = "structural-uncertainty-evaluation-fixture",
                Version = "1.0.0",
                Description = "Synthetic development-only structural evaluation fixture.",
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
        CalibrationUncertaintyStructuralFeatureReport report,
        decimal adjustment) => new()
        {
            Id = $"record:structural-uncertainty-{index}",
            Repository = new CalibrationRepositoryReference
            {
                Id = $"repository:structural-uncertainty-{index}",
                Name = $"Synthetic structural uncertainty repository {index}",
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
                SourceReference = $"eh://tests/structural-uncertainty-evaluation/{index}",
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
                        Id = "host-ai:structural-uncertainty-evaluation-fixture",
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
                    Title = "Synthetic structural uncertainty target",
                    Scope = "synthetic-fixture",
                    SourceWorkItemIds = [item.WorkItemId],
                    EvidenceIds = item.ResolvedEvidenceIds.Count > 0
                        ? item.ResolvedEvidenceIds
                        : ["evidence:synthetic"],
                    Hours = Symmetric(
                        item.ExpectedHours + adjustment + ((itemIndex % 2) * 0.25m)),
                    Rationale = "Synthetic reviewed range for structural evaluation.",
                    SizeException = "Synthetic fixture spans ordinary size bands.",
                })],
        };

    private static string Source(int index) => $$"""
        namespace Fixture{{index}};
        public sealed class Service{{index}}
        {
            public int Run(int value)
            {
                if (value > {{index}})
                {
                    return value;
                }

                return {{index}};
            }
        }
        """;

    private static CalibrationUncertaintyStructuralFeatureSummary AddToSummary(
        CalibrationUncertaintyStructuralFeatureSummary summary,
        CalibrationUncertaintyStructuralWorkItemFeatures item) => summary with
        {
            WorkItemCount = summary.WorkItemCount + 1,
            CompleteWorkItemCount = summary.CompleteWorkItemCount +
                (item.CoverageStatus ==
                    CalibrationUncertaintyStructuralCoverageStatus.Complete ? 1 : 0),
            PartialWorkItemCount = summary.PartialWorkItemCount +
                (item.CoverageStatus ==
                    CalibrationUncertaintyStructuralCoverageStatus.Partial ? 1 : 0),
            NotApplicableWorkItemCount = summary.NotApplicableWorkItemCount +
                (item.CoverageStatus ==
                    CalibrationUncertaintyStructuralCoverageStatus.NotApplicable ? 1 : 0),
            UnavailableWorkItemCount = summary.UnavailableWorkItemCount +
                (item.CoverageStatus ==
                    CalibrationUncertaintyStructuralCoverageStatus.Unavailable ? 1 : 0),
            ResolvedEvidenceReferenceCount = summary.ResolvedEvidenceReferenceCount +
                item.ResolvedEvidenceIds.Count,
            UnresolvedEvidenceReferenceCount = summary.UnresolvedEvidenceReferenceCount +
                item.UnresolvedEvidenceIds.Count,
            StructuralEvidenceReferenceCount = summary.StructuralEvidenceReferenceCount +
                item.StructuralEvidenceIds.Count,
            IncompatibleStructuralEvidenceReferenceCount =
                summary.IncompatibleStructuralEvidenceReferenceCount +
                item.IncompatibleStructuralEvidenceIds.Count,
        };

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

    private static bool Available(
        CalibrationUncertaintyStructuralWorkItemFeatures item,
        string featureId) => item.Features.Single(feature => feature.FeatureId == featureId)
        .Availability == CalibrationUncertaintyFeatureAvailability.Available;

    private static decimal Value(
        CalibrationUncertaintyStructuralWorkItemFeatures item,
        string featureId) => item.Features.Single(feature => feature.FeatureId == featureId)
            .Value!.Value;

    private static decimal TargetValue(
        CalibrationUncertaintyStructuralTargetEvaluation target,
        string featureId) => target.Features.Single(feature => feature.FeatureId == featureId)
            .Value!.Value;
}
