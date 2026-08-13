using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.RepositoryCalibration;

namespace EffortHours.EndToEndTests;

public sealed class CandidatePreflightTests
{
    [Fact]
    public void ScopeMarginalityCandidateUsesBoundedGeneralRolesAndPreservesLineage()
    {
        WorkItem testSemantic = Item(
            "work:api-surface:test:part-0001",
            "tests/Example.Tests/Example.Tests.csproj",
            8m);
        WorkItem testSource = Item(
            "work:dotnet-source-backbone:test:part-0001",
            "tests/Example.Tests/Example.Tests.csproj",
            8m);
        WorkItem productionSource = Item(
            "work:dotnet-source-backbone:source:part-0001",
            "src/Example/Example.csproj",
            8m);
        WorkItem benchmarkEntry = Item(
            "work:application-entry-point:bench:part-0001",
            "benchmarks/Example.Benchmarks/Example.Benchmarks.csproj",
            8m);
        WorkItem benchmarkSemantic = Item(
            "work:external-integration:bench:part-0001",
            "benchmarks/Example.Benchmarks/Example.Benchmarks.csproj",
            8m);
        WorkItem generatedSupport = Item(
            "work:manual-validation:fixture:part-0001",
            "fixtures/test-projects/generated",
            8m);
        EstimateReport source = Report(
            testSemantic,
            testSource,
            productionSource,
            benchmarkEntry,
            benchmarkSemantic,
            generatedSupport);

        EstimateReport candidate = CandidatePreflightTransformer.Transform(source);

        Assert.Equal(0m, CandidatePreflightTransformer.GetPointFactor(testSemantic));
        Assert.Equal(0.25m, CandidatePreflightTransformer.GetPointFactor(testSource));
        Assert.Equal(1m, CandidatePreflightTransformer.GetPointFactor(productionSource));
        Assert.Equal(0m, CandidatePreflightTransformer.GetPointFactor(benchmarkEntry));
        Assert.Equal(0.25m, CandidatePreflightTransformer.GetPointFactor(benchmarkSemantic));
        Assert.Equal(0m, CandidatePreflightTransformer.GetPointFactor(generatedSupport));
        Assert.Equal(
            source.WorkItems.Select(item => item.Id),
            candidate.WorkItems.Select(item => item.Id));
        Assert.Equal(new EffortRange { Low = 9.24m, Expected = 12m, High = 15.12m }, candidate.TotalEffort);
        Assert.Empty(ContractValidation.Validate(candidate));
        Assert.All(candidate.WorkItems, item =>
        {
            Assert.Equal(EstimatorKind.Rule, item.Estimator.Kind);
            Assert.Contains(CandidatePreflightTransformer.CandidateId, item.Reason, StringComparison.Ordinal);
            Assert.Contains(item.Id, item.Reason, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void CandidatePreflightOptionsRequireExactCommitAndCompletePaths()
    {
        string[] valid =
        [
            "--plan", "plan.json",
            "--corpus", "corpus.json",
            "--seed-evaluation", "evaluation.json",
            "--outputs", "outputs",
            "--output", "preflight.json",
            "--source-commit", new string('a', 40),
        ];

        bool parsed = CandidatePreflightOptions.TryParse(
            valid,
            out CandidatePreflightOptions? options,
            out string? error);
        bool invalid = CandidatePreflightOptions.TryParse(
            [.. valid[..^1], "not-a-commit"],
            out _,
            out string? invalidError);

        Assert.True(parsed, error);
        Assert.NotNull(options);
        Assert.Equal(new string('a', 40), options.SourceCommit);
        Assert.False(invalid);
        Assert.Contains("40-character", invalidError, StringComparison.Ordinal);
    }

    private static EstimateReport Report(params WorkItem[] workItems)
    {
        EffortRange total = ContractValidation.Sum(workItems.Select(item => item.Hours));
        return new EstimateReport
        {
            EstimatorVersion = CandidatePreflightTransformer.BaselineEstimatorVersion,
            Repository = new RepositoryDescriptor
            {
                Name = "synthetic",
                SourceDigest = $"sha256:{new string('1', 64)}",
            },
            Profile = EstimationProfile.Implementation,
            Baseline = new EstimationBaseline
            {
                Id = "senior-contractor-2026-no-ai",
                WorkerProfile = "Senior contractor",
                TechnologyBaselineYear = 2026,
                BusinessDomainFamiliar = false,
                UsesAi = false,
                Description = "Synthetic baseline.",
            },
            TotalEffort = total,
            Categories =
            [
                new CategoryEstimate
                {
                    Category = EffortCategory.ProductionImplementation,
                    Hours = total,
                },
            ],
            WorkItems = workItems,
            Verification = new VerificationSummary
            {
                Mode = VerificationMode.StaticAssumed,
                WorkingState = WorkingState.AssumedWorking,
                TestsAssumedPassing = true,
            },
        };
    }

    private static WorkItem Item(string id, string scope, decimal expected) => new()
    {
        Id = id,
        Category = EffortCategory.ProductionImplementation,
        Title = "Synthetic item",
        Scope = scope,
        EvidenceIds = [$"evidence:{id}"],
        Complexity = ComplexityLevel.Moderate,
        Hours = new EffortRange
        {
            Low = expected / 2m,
            Expected = expected,
            High = expected * 2m,
        },
        Confidence = 0.75m,
        Reason = "Synthetic seed item.",
        Estimator = new EstimatorReference
        {
            Id = "seed-rules",
            Version = "0.4.0",
            Kind = EstimatorKind.Rule,
        },
        Profiles = [EstimationProfile.Implementation],
    };
}
