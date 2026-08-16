using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class CalibrationUncertaintyStructuralFeatureTests
{
    [Fact]
    public async Task ProjectionIsDeterministicSchemaValidLabelIndependentAndDiagnosticOnly()
    {
        (EstimateReport estimate, RepositoryEvidence evidence) = await CreateDotNetInputsAsync();

        CalibrationUncertaintyStructuralFeatureReport first =
            CalibrationUncertaintyStructuralFeatureProjector.Project(estimate, evidence);
        CalibrationUncertaintyStructuralFeatureReport second =
            CalibrationUncertaintyStructuralFeatureProjector.Project(estimate, evidence);
        string json = ContractJson.Serialize(first);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationUncertaintyStructuralFeatures,
            json);

        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(first));
        Assert.Equal(json, ContractJson.Serialize(second));
        Assert.True(first.FeatureContract.LabelIndependent);
        Assert.All(first.FeatureContract.Features, feature => Assert.Equal(
            CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly,
            feature.Monotonicity));
        Assert.Equal(14, first.FeatureContract.Features.Count);
        Assert.Contains(first.WorkItems, item =>
            item.CoverageStatus == CalibrationUncertaintyStructuralCoverageStatus.Complete);
        Assert.Equal(estimate.TotalEffort, new SeedEstimator().Estimate(
            evidence,
            estimate.Profile).TotalEffort);
    }

    [Fact]
    public async Task MultipleLocalScopesUseFrozenWorstLocalShapeRatherThanRepositoryTotals()
    {
        (EstimateReport source, RepositoryEvidence sourceEvidence) = await CreateDotNetInputsAsync();
        EvidenceFact original = sourceEvidence.Facts.Single(fact =>
            IsCompatible(fact));
        EvidenceFact larger = original with
        {
            Id = original.Id + ":second-scope",
            Measurements =
            [
                .. original.Measurements.Select(measurement => measurement.Name ==
                    StructuralEvidenceMeasurementNames.CallableSizeMaximum
                        ? measurement with { Value = measurement.Value + 500m }
                        : measurement),
            ],
        };
        RepositoryEvidence evidence = sourceEvidence with
        {
            Facts = [.. sourceEvidence.Facts, larger],
        };
        WorkItem selected = source.WorkItems.First(item => item.EvidenceIds.Contains(
            original.Id,
            StringComparer.Ordinal));
        EstimateReport estimate = source with
        {
            WorkItems =
            [
                .. source.WorkItems.Select(item => item.Id == selected.Id
                    ? item with { EvidenceIds = [.. item.EvidenceIds, larger.Id] }
                    : item),
            ],
        };

        CalibrationUncertaintyStructuralWorkItemFeatures item =
            CalibrationUncertaintyStructuralFeatureProjector.Project(estimate, evidence)
                .WorkItems.Single(value => value.WorkItemId == selected.Id);
        CalibrationUncertaintyFeatureValue maximum = Feature(
            item,
            CalibrationUncertaintyStructuralFeatureIds.CallableSizeMaximum);

        Assert.Equal(
            Measurement(larger, StructuralEvidenceMeasurementNames.CallableSizeMaximum),
            maximum.Value);
        Assert.Equal([original.Id, larger.Id], maximum.EvidenceIds);
    }

    [Fact]
    public async Task UnsupportedStructuralEvidenceAndMissingReferencesFailVisible()
    {
        (EstimateReport source, RepositoryEvidence sourceEvidence) = await CreateDotNetInputsAsync();
        EvidenceFact structure = sourceEvidence.Facts.Single(fact =>
            IsCompatible(fact));
        RepositoryEvidence oldEvidence = sourceEvidence with
        {
            Facts =
            [
                .. sourceEvidence.Facts.Select(fact => fact.Id == structure.Id
                    ? fact with
                    {
                        Tags =
                        [
                            .. fact.Tags.Where(tag =>
                                tag != StructuralEvidenceVersions.CallableMetricsV1Tag),
                        ],
                    }
                    : fact),
            ],
        };
        WorkItem selected = source.WorkItems.First(item => item.EvidenceIds.Contains(
            structure.Id,
            StringComparer.Ordinal));
        EstimateReport estimate = source with
        {
            WorkItems =
            [
                .. source.WorkItems.Select(item => item.Id == selected.Id
                    ? item with { EvidenceIds = [.. item.EvidenceIds, "missing:structural"] }
                    : item),
            ],
        };

        CalibrationUncertaintyStructuralWorkItemFeatures item =
            CalibrationUncertaintyStructuralFeatureProjector.Project(estimate, oldEvidence)
                .WorkItems.Single(value => value.WorkItemId == selected.Id);

        Assert.Equal(CalibrationUncertaintyStructuralCoverageStatus.Unavailable, item.CoverageStatus);
        Assert.Equal([structure.Id], item.IncompatibleStructuralEvidenceIds);
        Assert.Equal(["missing:structural"], item.UnresolvedEvidenceIds);
        Assert.All(item.Features, feature => Assert.Equal(
            CalibrationUncertaintyFeatureAvailability.Unavailable,
            feature.Availability));
    }

    [Fact]
    public async Task PartialCoverageCanMixAvailableCoverageWithUnavailableDistributions()
    {
        (EstimateReport source, RepositoryEvidence sourceEvidence) = await CreateDotNetInputsAsync();
        EvidenceFact measured = sourceEvidence.Facts.Single(IsCompatible);
        HashSet<string> retained =
        [
            StructuralEvidenceMeasurementNames.SourceFiles,
            StructuralEvidenceMeasurementNames.ParserBackedFiles,
            StructuralEvidenceMeasurementNames.DetectedCallables,
            StructuralEvidenceMeasurementNames.MeasuredCallables,
            StructuralEvidenceMeasurementNames.CallableMeasurementCoverage,
            StructuralEvidenceMeasurementNames.AnalyzerAmbiguityConcentration,
        ];
        EvidenceFact unmeasured = measured with
        {
            Id = measured.Id + ":unmeasured",
            Measurements =
            [
                .. measured.Measurements
                    .Where(measurement => retained.Contains(measurement.Name))
                    .Select(measurement => measurement.Name switch
                    {
                        StructuralEvidenceMeasurementNames.SourceFiles =>
                            measurement with { Value = 1m },
                        StructuralEvidenceMeasurementNames.ParserBackedFiles =>
                            measurement with { Value = 0m },
                        StructuralEvidenceMeasurementNames.DetectedCallables =>
                            measurement with { Value = 1m },
                        StructuralEvidenceMeasurementNames.MeasuredCallables =>
                            measurement with { Value = 0m },
                        StructuralEvidenceMeasurementNames.CallableMeasurementCoverage =>
                            measurement with { Value = 0m },
                        StructuralEvidenceMeasurementNames.AnalyzerAmbiguityConcentration =>
                            measurement with { Value = 1m },
                        _ => measurement,
                    }),
            ],
        };
        RepositoryEvidence evidence = sourceEvidence with
        {
            Facts = [.. sourceEvidence.Facts, unmeasured],
        };
        WorkItem selected = source.WorkItems.First(item => item.EvidenceIds.Contains(
            measured.Id,
            StringComparer.Ordinal));
        EstimateReport estimate = source with
        {
            WorkItems =
            [
                .. source.WorkItems.Select(item => item.Id == selected.Id
                    ? item with { EvidenceIds = [.. item.EvidenceIds, unmeasured.Id] }
                    : item),
            ],
        };

        CalibrationUncertaintyStructuralFeatureReport report =
            CalibrationUncertaintyStructuralFeatureProjector.Project(estimate, evidence);
        CalibrationUncertaintyStructuralWorkItemFeatures item = report.WorkItems.Single(value =>
            value.WorkItemId == selected.Id);

        Assert.Equal(CalibrationUncertaintyStructuralCoverageStatus.Partial, item.CoverageStatus);
        Assert.Equal(
            CalibrationUncertaintyFeatureAvailability.Available,
            Feature(
                item,
                CalibrationUncertaintyStructuralFeatureIds.CallableMeasurementCoverage)
                .Availability);
        Assert.Equal(
            CalibrationUncertaintyFeatureAvailability.Unavailable,
            Feature(item, CalibrationUncertaintyStructuralFeatureIds.CallableSizeP90)
                .Availability);
        Assert.Empty(ContractValidation.Validate(report));
    }

    [Fact]
    public async Task TaggedButMalformedStructuralEvidenceFailsClosed()
    {
        (EstimateReport estimate, RepositoryEvidence source) = await CreateDotNetInputsAsync();
        EvidenceFact structure = source.Facts.Single(fact =>
            IsCompatible(fact));
        RepositoryEvidence evidence = source with
        {
            Facts =
            [
                .. source.Facts.Select(fact => fact.Id == structure.Id
                    ? fact with
                    {
                        Measurements =
                        [
                            .. fact.Measurements.Where(measurement => measurement.Name !=
                                StructuralEvidenceMeasurementNames.MeasuredCallables),
                        ],
                    }
                    : fact),
            ],
        };

        CalibrationEvaluationException exception = Assert.Throws<CalibrationEvaluationException>(
            () => CalibrationUncertaintyStructuralFeatureProjector.Project(estimate, evidence));

        Assert.Contains(exception.Errors, error => error.Contains(
            StructuralEvidenceMeasurementNames.MeasuredCallables,
            StringComparison.Ordinal));
    }

    [Fact]
    public void CanonicalStructuralFeatureContractDigestIsPinned()
    {
        Assert.Equal(
            CalibrationUncertaintyVersions.StructuralFeatureContractDigestV1,
            CalibrationDigest.Compute(CalibrationUncertaintyStructuralFeatureCatalog.Current));
    }

    private static async Task<(EstimateReport Estimate, RepositoryEvidence Evidence)>
        CreateDotNetInputsAsync()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "App.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n");
        repository.WriteText(
            "Service.cs",
            "public sealed class Service { public int Run(int x) { if (x > 0) return x; return 0; } }\n");
        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);
        return (
            new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation),
            evidence);
    }

    private static CalibrationUncertaintyFeatureValue Feature(
        CalibrationUncertaintyStructuralWorkItemFeatures item,
        string id) => item.Features.Single(feature => feature.FeatureId == id);

    private static decimal Measurement(EvidenceFact fact, string name) =>
        fact.Measurements.Single(measurement => measurement.Name == name).Value;

    private static bool IsCompatible(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.SourceStructure &&
        fact.Tags.Contains(StructuralEvidenceVersions.CallableMetricsV1Tag, StringComparer.Ordinal);
}
