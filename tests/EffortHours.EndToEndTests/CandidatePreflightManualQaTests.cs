using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.RepositoryCalibration;

namespace EffortHours.EndToEndTests;

public sealed partial class CandidatePreflightTests
{
    private const string ManualQaPolicyDigest =
        "sha256:31afc595a033c0e2dc96e3116ebd72d7105520392828ae146c6cf91c66123571";

    [Fact]
    public void ManualQaCandidateReplacesSeedQaWithThirtyFortyFiftyPercentOfCoding()
    {
        EffortCategory[] categories = ManualQaCandidateTransformer.EligibleCategories;
        WorkItem[] eligible =
        [
            .. Enumerable.Range(0, 40).Select(index =>
                Item($"work:eligible:{index + 1}", $"scope-{index + 1}", 4m) with
                {
                    Category = categories[index % categories.Length],
                }),
        ];
        WorkItem[] excluded =
        [
            Excluded("spec", EffortCategory.SpecificationComprehensionAndDomainLearning),
            Excluded("setup", EffortCategory.RepositoryAndSolutionSetup),
            Excluded("design", EffortCategory.ArchitectureAndTechnicalDesign),
            Excluded("docs", EffortCategory.Documentation),
            Excluded("review", EffortCategory.SelfReviewAndSystemIntegration),
        ];
        WorkItem oldManualQa = Item("work:manual-validation:old", "repository", 3m) with
        {
            Category = EffortCategory.ManualValidationDebuggingAndHardening,
        };
        EstimateReport source = Report([.. eligible, .. excluded, oldManualQa]);

        EstimateReport candidate = ManualQaCandidateProjectionRunner.Project(
            source,
            ManualQaPolicy());
        WorkItem[] qaItems =
        [
            .. candidate.WorkItems.Where(item =>
                item.Category == EffortCategory.ManualValidationDebuggingAndHardening),
        ];
        EffortRange qa = ContractValidation.Sum(qaItems.Select(item => item.Hours));

        Assert.Equal(160m, eligible.Sum(item => item.Hours.Expected));
        Assert.Equal(new EffortRange { Low = 48m, Expected = 64m, High = 80m }, qa);
        Assert.Equal(new EffortRange { Low = 153m, Expected = 274m, High = 500m }, candidate.TotalEffort);
        Assert.Equal(eligible.Length, qaItems.Length);
        Assert.All(qaItems, item => Assert.InRange(item.Hours.Expected, 0.5m, 8m));
        Assert.DoesNotContain(candidate.WorkItems, item => item.Id == oldManualQa.Id);
        Assert.All(qaItems, item =>
        {
            string dependency = Assert.Single(item.DependencyIds);
            Assert.Equal(
                ManualQaSourceWorkItemLineage.CreateCandidateWorkItemId(dependency),
                item.Id);
            WorkItem sourceItem = Assert.Single(eligible, entry => entry.Id == dependency);
            Assert.Equal(sourceItem.EvidenceIds, item.EvidenceIds);
            Assert.Equal(sourceItem.Scope, item.Scope);
            Assert.Equal(decimal.Min(sourceItem.Confidence, 0.50m), item.Confidence);
            Assert.Contains("not compounded", item.Reason, StringComparison.Ordinal);
        });
        Assert.DoesNotContain(
            qaItems.SelectMany(item => item.DependencyIds),
            dependency => excluded.Any(item => item.Id == dependency));
        Assert.Equal(ManualQaCandidateTransformer.EstimatorVersion, candidate.EstimatorVersion);
        Assert.Empty(ContractValidation.Validate(candidate));
    }

    [Fact]
    public void ManualQaCandidateDoesNotCompoundTheSourceRange()
    {
        WorkItem narrow = Item("work:eligible:range", "src/App", 4m) with
        {
            Hours = new EffortRange { Low = 3m, Expected = 4m, High = 5m },
        };
        WorkItem wide = narrow with
        {
            Hours = new EffortRange { Low = 0.5m, Expected = 4m, High = 20m },
        };

        WorkItem narrowQa = Qa(ManualQaCandidateTransformer.Transform(Report(narrow), ManualQaPolicy()));
        WorkItem wideQa = Qa(ManualQaCandidateTransformer.Transform(Report(wide), ManualQaPolicy()));

        Assert.Equal(new EffortRange { Low = 1.2m, Expected = 1.6m, High = 2m }, narrowQa.Hours);
        Assert.Equal(narrowQa.Hours, wideQa.Hours);
    }

    [Fact]
    public void ManualQaCandidateIsOrderInvariantAndAllowsAnEmptyCodingBasis()
    {
        WorkItem first = Item("work:eligible:first", "src/A", 4m);
        WorkItem second = Item("work:eligible:second", "src/B", 2m) with
        {
            Category = EffortCategory.UnitTesting,
        };
        WorkItem excluded = Excluded("docs-only", EffortCategory.Documentation);
        EstimateReport forward = ManualQaCandidateTransformer.Transform(
            Report(first, second, excluded),
            ManualQaPolicy());
        EstimateReport reversed = ManualQaCandidateTransformer.Transform(
            Report(excluded, second, first),
            ManualQaPolicy());
        EstimateReport empty = ManualQaCandidateProjectionRunner.Project(
            Report(excluded),
            ManualQaPolicy());

        Assert.Equal(ContractJson.Serialize(forward), ContractJson.Serialize(reversed));
        Assert.DoesNotContain(
            empty.WorkItems,
            item => item.Category == EffortCategory.ManualValidationDebuggingAndHardening);
        Assert.Equal(excluded.Hours, empty.TotalEffort);
        Assert.True(empty.TotalEffort.Low >= 0m);
        Assert.Empty(ContractValidation.Validate(empty));
    }

    [Fact]
    public void ManualQaCandidateFailsClosedForPolicyProfileAndCancellation()
    {
        EstimateReport source = Report(Item("work:eligible:failure", "src/App", 4m));
        ManualQaCandidatePolicy policy = ManualQaPolicy();
        using CancellationTokenSource cancelled = new();
        cancelled.Cancel();

        Assert.Throws<InvalidDataException>(() => ManualQaCandidateTransformer.Transform(
            source,
            policy with { ExpectedRatio = 0.41m }));
        Assert.Throws<InvalidDataException>(() => ManualQaCandidateTransformer.Transform(
            source with { Profile = EstimationProfile.Recreation },
            policy));
        Assert.Throws<OperationCanceledException>(() =>
            ManualQaCandidateProjectionRunner.Project(source, policy, cancelled.Token));
    }

    [Fact]
    public void ManualQaCandidatePolicyArtifactIsFrozenAndValid()
    {
        string root = RepositoryRoot();
        string path = Path.Combine(
            root,
            "calibration",
            "corpora",
            "public-readiness",
            "1.9.0",
            "manual-qa-coding-ratio-policy.json");
        string json = File.ReadAllText(path);
        ManualQaCandidatePolicy policy = ContractJson.Deserialize<ManualQaCandidatePolicy>(json);

        ManualQaCandidateTransformer.ValidatePolicy(policy);
        ManualQaCandidateProjectionRunner.ValidateArtifactDigest(json, ManualQaPolicyDigest);
        Assert.Throws<InvalidDataException>(() =>
            ManualQaCandidateProjectionRunner.ValidateArtifactDigest(
                json,
                $"sha256:{new string('0', 64)}"));
    }

    [Fact]
    public void ManualQaCheckpointReconcilesTheFrozenDevelopmentCategoryAudit()
    {
        string path = Path.Combine(
            RepositoryRoot(),
            "calibration",
            "corpora",
            "public-readiness",
            "0.3.0.development-evaluation.json");
        CalibrationEvaluationReport evaluation =
            ContractJson.Deserialize<CalibrationEvaluationReport>(File.ReadAllText(path));
        HashSet<EffortCategory> eligible =
            [.. ManualQaCandidateTransformer.EligibleCategories];
        CalibrationCategoryMetrics qa = Assert.Single(
            evaluation.Categories,
            category =>
                category.Category == EffortCategory.ManualValidationDebuggingAndHardening);
        decimal reviewedEligible = evaluation.Categories
            .Where(category => eligible.Contains(category.Category))
            .Sum(category => category.Metrics.Expected.ReviewedHours);
        decimal seedEligible = evaluation.Categories
            .Where(category => eligible.Contains(category.Category))
            .Sum(category => category.Metrics.Expected.CandidateHours);

        Assert.Equal(34_625.75m, reviewedEligible);
        Assert.Equal(1_691.75m, qa.Metrics.Expected.ReviewedHours);
        Assert.Equal(36_134.75m, seedEligible);
        Assert.Equal(1_626.50m, qa.Metrics.Expected.CandidateHours);
        Assert.Equal(14_453.90m, seedEligible * ManualQaCandidateTransformer.ExpectedRatio);
    }

    [Fact]
    public void ManualQaCandidateOptionsRequireAPinnedPolicyDigest()
    {
        string[] valid =
        [
            "--estimate", "estimate.json",
            "--policy", "policy.json",
            "--expected-policy-digest", ManualQaPolicyDigest,
        ];

        bool parsed = ManualQaCandidateProjectionOptions.TryParse(
            valid,
            out ManualQaCandidateProjectionOptions? options,
            out string? error);
        bool invalid = ManualQaCandidateProjectionOptions.TryParse(
            [.. valid[..^1], "not-a-digest"],
            out _,
            out string? invalidError);

        Assert.True(parsed, error);
        Assert.Equal(ManualQaPolicyDigest, options!.ExpectedPolicyDigest);
        Assert.False(invalid);
        Assert.Contains("sha256", invalidError, StringComparison.Ordinal);
    }

    private static WorkItem Excluded(string id, EffortCategory category) =>
        Item($"work:excluded:{id}", "repository", 10m) with { Category = category };

    private static WorkItem Qa(EstimateReport report) => Assert.Single(
        report.WorkItems,
        item => item.Category == EffortCategory.ManualValidationDebuggingAndHardening);

    private static ManualQaCandidatePolicy ManualQaPolicy() => new()
    {
        PolicyVersion = ManualQaCandidateTransformer.PolicyVersion,
        Id = ManualQaCandidateTransformer.PolicyId,
        CandidateId = ManualQaCandidateTransformer.CandidateId,
        EstimatorVersion = ManualQaCandidateTransformer.EstimatorVersion,
        BaselineEstimatorVersion = ManualQaCandidateTransformer.BaselineEstimatorVersion,
        FeatureContractVersion = ManualQaCandidateTransformer.FeatureContractVersion,
        EffectiveDate = ManualQaCandidateTransformer.EffectiveDate,
        LicenseExpression = "MIT",
        Maturity = ManualQaCandidateTransformer.Maturity,
        Basis = ManualQaCandidateTransformer.Basis,
        Projection = ManualQaCandidateTransformer.Projection,
        LowRatio = ManualQaCandidateTransformer.LowRatio,
        ExpectedRatio = ManualQaCandidateTransformer.ExpectedRatio,
        HighRatio = ManualQaCandidateTransformer.HighRatio,
        MaximumConfidence = ManualQaCandidateTransformer.MaximumConfidence,
        EligibleCategories = ManualQaCandidateTransformer.EligibleCategories,
        Limitations = ["Synthetic test limitation."],
    };

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "EffortHours.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
