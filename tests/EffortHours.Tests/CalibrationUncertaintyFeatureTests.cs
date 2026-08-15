using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class CalibrationUncertaintyFeatureTests
{
    [Fact]
    public void ProjectionIsDeterministicSchemaValidAndLabelIndependentInMemory()
    {
        (EstimateReport estimate, RepositoryEvidence evidence) = CreateInputs();

        CalibrationUncertaintyFeatureReport first =
            CalibrationUncertaintyFeatureProjector.Project(estimate, evidence);
        CalibrationUncertaintyFeatureReport second =
            CalibrationUncertaintyFeatureProjector.Project(estimate, evidence);
        string json = ContractJson.Serialize(first);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.CalibrationUncertaintyFeatures,
            json);

        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Empty(ContractValidation.Validate(first));
        Assert.Equal(json, ContractJson.Serialize(second));
        Assert.True(first.FeatureContract.LabelIndependent);
        Assert.Equal(0.80m, first.FeatureContract.IntervalPolicy.IntendedCoverageTarget);
        Assert.False(first.FeatureContract.IntervalPolicy.FormalProbabilityInterval);
        Assert.True(first.FeatureContract.IntervalPolicy.SymmetricAroundExpected);
        Assert.False(first.FeatureContract.IntervalPolicy.MissingValuesWidenAutomatically);
        Assert.Equal(first.FeatureContract.Features.Count, first.Summary.FeatureCount);
        Assert.NotEmpty(first.FeatureContract.DeferredCandidates);
        Assert.All(first.FeatureContract.Features, feature => Assert.Equal(
            CalibrationUncertaintyFeatureStage.AvailableOffline,
            feature.Stage));
        Assert.DoesNotContain(
            first.WorkItems.SelectMany(item => item.Features).Select(feature => feature.FeatureId),
            id => first.FeatureContract.DeferredCandidates.Any(candidate => candidate.Id == id));
        Assert.Contains(first.WorkItems.SelectMany(item => item.Features), feature =>
            feature.FeatureId == CalibrationUncertaintyFeatureIds.AggregateBranchDensity &&
            feature.Availability == CalibrationUncertaintyFeatureAvailability.Available);
        Assert.DoesNotContain("Synthetic C# source structure", json, StringComparison.Ordinal);
    }

    [Fact]
    public void MaterialGapIsWidthDrivingWhileOfflineNonExecutionIsDiagnosticOnly()
    {
        (EstimateReport estimate, RepositoryEvidence evidence) = CreateInputs();
        WorkItem item = estimate.WorkItems.First(candidate => candidate.EvidenceIds.Count > 0);
        string factId = item.EvidenceIds[0];
        RepositoryEvidence offlineOnly = AddTag(evidence, factId, "target-code:not-executed");
        CalibrationUncertaintyFeatureReport offlineReport =
            CalibrationUncertaintyFeatureProjector.Project(estimate, offlineOnly);
        CalibrationUncertaintyWorkItemFeatures offlineItem = GetItem(offlineReport, item.Id);

        Assert.Equal(item.Hours, offlineItem.SourceRange);
        Assert.Equal(
            0m,
            GetFeature(offlineItem, CalibrationUncertaintyFeatureIds.MaterialUnresolvedCount).Value);
        Assert.True(
            GetFeature(
                offlineItem,
                CalibrationUncertaintyFeatureIds.NonMaterialOfflineLimitationCount).Value > 0m);
        CalibrationUncertaintyFeatureDefinition offlineDefinition = GetDefinition(
            CalibrationUncertaintyFeatureIds.NonMaterialOfflineLimitationCount);
        Assert.Equal(
            CalibrationUncertaintyFeatureMonotonicity.DiagnosticOnly,
            offlineDefinition.Monotonicity);
        Assert.Empty(CalibrationUncertaintyIntervalRules.ValidateMonotonicChange(
            offlineDefinition,
            0m,
            1m,
            2m,
            1m));

        RepositoryEvidence material = AddTag(
            offlineOnly,
            factId,
            CalibrationUncertaintyFeatureCatalog.MaterialAccessGapTag + ":external-contract");
        CalibrationUncertaintyWorkItemFeatures materialItem = GetItem(
            CalibrationUncertaintyFeatureProjector.Project(estimate, material),
            item.Id);
        Assert.Equal(
            1m,
            GetFeature(materialItem, CalibrationUncertaintyFeatureIds.MaterialUnresolvedCount).Value);
        CalibrationUncertaintyFeatureDefinition materialDefinition = GetDefinition(
            CalibrationUncertaintyFeatureIds.MaterialUnresolvedCount);
        Assert.NotEmpty(CalibrationUncertaintyIntervalRules.ValidateMonotonicChange(
            materialDefinition,
            0m,
            1m,
            0.4m,
            0.4m));
        Assert.Empty(CalibrationUncertaintyIntervalRules.ValidateMonotonicChange(
            materialDefinition,
            0m,
            1m,
            0.4m,
            0.401m));
    }

    [Fact]
    public void MissingEvidenceReferenceIsExplicitAndCannotNarrowComparableInterval()
    {
        (EstimateReport source, RepositoryEvidence evidence) = CreateInputs();
        WorkItem selected = source.WorkItems[0];
        EstimateReport estimate = source with
        {
            WorkItems =
            [
                selected with { EvidenceIds = [.. selected.EvidenceIds, "missing:fact"] },
                .. source.WorkItems.Skip(1),
            ],
        };

        CalibrationUncertaintyFeatureReport report =
            CalibrationUncertaintyFeatureProjector.Project(estimate, evidence);
        CalibrationUncertaintyWorkItemFeatures item = GetItem(report, selected.Id);
        CalibrationUncertaintyFeatureValue inferred = GetFeature(
            item,
            CalibrationUncertaintyFeatureIds.InferredFactShare);

        Assert.Equal(["missing:fact"], item.UnresolvedEvidenceIds);
        Assert.Equal(
            CalibrationUncertaintyFeatureAvailability.Unavailable,
            inferred.Availability);
        Assert.Equal("unresolved-supporting-evidence", inferred.ReasonCode);
        Assert.Equal(
            1m,
            GetFeature(item, CalibrationUncertaintyFeatureIds.MaterialUnresolvedCount).Value);
        Assert.Equal(1, report.Summary.UnresolvedEvidenceReferenceCount);
        Assert.Equal(1, report.Summary.MaterialAccessGapWorkItemCount);
        Assert.Equal(1, report.Summary.WidthDriverUnavailableWorkItemCount);
    }

    [Fact]
    public void ProjectionRejectsEvidenceFromAnotherImmutableSource()
    {
        (EstimateReport estimate, RepositoryEvidence source) = CreateInputs();
        RepositoryEvidence evidence = source with
        {
            Repository = source.Repository with
            {
                SourceDigest = "sha256:" + new string('c', 64),
            },
        };

        CalibrationEvaluationException exception = Assert.Throws<CalibrationEvaluationException>(
            () => CalibrationUncertaintyFeatureProjector.Project(estimate, evidence));

        Assert.Contains(exception.Errors, error => error.Contains(
            "source digests do not match",
            StringComparison.Ordinal));
    }

    [Fact]
    public void SymmetricPolicyPreservesExpectedPointAndOnlyZeroFloorCreatesAsymmetry()
    {
        EffortRange ordinary = CalibrationUncertaintyIntervalRules.BuildSymmetric(10m, 2m);
        EffortRange floored = CalibrationUncertaintyIntervalRules.BuildSymmetric(1m, 3m);

        Assert.Equal(new EffortRange { Low = 8m, Expected = 10m, High = 12m }, ordinary);
        Assert.Equal(new EffortRange { Low = 0m, Expected = 1m, High = 4m }, floored);
        Assert.True(CalibrationUncertaintyIntervalRules.IsPolicyCompliant(ordinary));
        Assert.True(CalibrationUncertaintyIntervalRules.IsPolicyCompliant(floored));
        Assert.Equal(0.4m, CalibrationUncertaintyIntervalRules.CalculateNormalizedWidth(ordinary));
        Assert.False(CalibrationUncertaintyIntervalRules.IsPolicyCompliant(
            new EffortRange { Low = 5m, Expected = 10m, High = 20m }));

        CalibrationUncertaintyFeatureDefinition confidence = GetDefinition(
            CalibrationUncertaintyFeatureIds.SourceConfidence);
        Assert.NotEmpty(CalibrationUncertaintyIntervalRules.ValidateMonotonicChange(
            confidence,
            0.9m,
            0.6m,
            0.4m,
            0.39m));
        Assert.Empty(CalibrationUncertaintyIntervalRules.ValidateMonotonicChange(
            confidence,
            0.9m,
            0.6m,
            0.4m,
            0.4m));
    }

    private static (EstimateReport Estimate, RepositoryEvidence Evidence) CreateInputs()
    {
        RepositoryEvidence source = TestRepositoryEvidence.CreateStructuredDotNet(
            sourceCopies: 2,
            endpoints: 3,
            testCases: 4);
        RepositoryEvidence evidence = source with
        {
            Repository = source.Repository with
            {
                SourceDigest = "sha256:" + new string('a', 64),
            },
            Facts = [.. source.Facts.Select(fact => fact.Kind == EvidenceKinds.SourceStructure
                ? fact with
                {
                    Tags = [.. fact.Tags, "syntax:parser-backed", "parser-confidence:high"],
                }
                : fact)],
        };
        return (
            new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation),
            evidence);
    }

    private static RepositoryEvidence AddTag(
        RepositoryEvidence evidence,
        string factId,
        string tag) => evidence with
        {
            Facts = [.. evidence.Facts.Select(fact => fact.Id == factId
                ? fact with { Tags = [.. fact.Tags, tag] }
                : fact)],
        };

    private static CalibrationUncertaintyWorkItemFeatures GetItem(
        CalibrationUncertaintyFeatureReport report,
        string id) => report.WorkItems.Single(item => item.WorkItemId == id);

    private static CalibrationUncertaintyFeatureValue GetFeature(
        CalibrationUncertaintyWorkItemFeatures item,
        string id) => item.Features.Single(feature => feature.FeatureId == id);

    private static CalibrationUncertaintyFeatureDefinition GetDefinition(string id) =>
        CalibrationUncertaintyFeatureCatalog.Current.Features.Single(feature => feature.Id == id);
}
