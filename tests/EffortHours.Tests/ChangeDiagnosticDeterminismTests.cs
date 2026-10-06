using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class ChangeDiagnosticDeterminismTests
{
    [Fact]
    public async Task IndependentAndSharedDiagnosticCollectionsProduceIdenticalCanonicalEvidenceAndEffort()
    {
        Diagnostic shared = Warning(1);
        ChangeEstimateReport cached = await Estimate([shared, shared]);
        ChangeEstimateReport cold = await Estimate([Warning(1), Warning(1)]);
        Assert.Equal(cached.TotalEffort, cold.TotalEffort);
        Assert.Equal(ContractJson.SerializeCompact(cached.Evidence), ContractJson.SerializeCompact(cold.Evidence));
        Assert.Single(cold.Evidence.Diagnostics, diagnostic => diagnostic.Code == "FB4102");
        Assert.Equal(ChangePortfolioComparisonIdentity.ComputeTextDigest(ContractJson.SerializeCompact(cached.Evidence)),
            ChangePortfolioComparisonIdentity.ComputeTextDigest(ContractJson.SerializeCompact(cold.Evidence)));
    }

    [Fact]
    public async Task DistinctLocationsAndEvidenceReferencesRemainInCanonicalDiagnostics()
    {
        ChangeEstimateReport report = await Estimate([Warning(1), Warning(2), Warning(1) with { EvidenceIds = ["file:Other.cs"] }]);
        Assert.Equal(3, report.Evidence.Diagnostics.Count(diagnostic => diagnostic.Code == "FB4102"));
        Assert.Empty(ContractValidation.Validate(report));
    }

    private static Diagnostic Warning(int line) => new()
    {
        Code = "FB4102",
        Severity = DiagnosticSeverity.Warning,
        Message = "Synthetic bounded parser warning.",
        EvidenceIds = ["file:Demo.cs"],
        Locations = [new() { Path = "Demo.cs", Line = line }],
    };

    private static Task<ChangeEstimateReport> Estimate(IReadOnlyList<Diagnostic> diagnostics)
    {
        InMemoryChangeSnapshot snapshot = new(("Demo.cs", "public class Demo { public int Value => 1; }"));
        ChangeSelection selection = new()
        {
            Kind = ChangeSelectionKind.BaseHead,
            Base = new() { Selector = snapshot.ObjectId, ObjectId = snapshot.ObjectId, Kind = ChangeSnapshotKind.GitCommit },
            Head = new() { Selector = snapshot.ObjectId, ObjectId = snapshot.ObjectId, Kind = ChangeSnapshotKind.GitCommit },
        };
        return new ChangeEstimator().EstimateAsync(new ChangeEstimateInput
        {
            RepositoryName = "synthetic",
            Selection = selection,
            Diagnostics = diagnostics,
            OpenBaseAsync = _ => Task.FromResult<IChangeSnapshot>(snapshot),
            OpenHeadAsync = _ => Task.FromResult<IChangeSnapshot>(snapshot),
        }, EstimationProfile.Implementation, rateCard: null);
    }
}
