using EffortHours.Analysis;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class RepositoryStockEvidenceEqualityTests
{
    [Fact]
    public async Task FullyAnalyzedMixedEditsReuseExactlyTheSameStockForEveryProfile()
    {
        RepositoryEvidence before = await Scan("100", "100", "item");
        RepositoryEvidence after = await Scan("101", "101", "node");
        Assert.True(RepositoryStockEvidenceEquality.Equivalent(before, after, CancellationToken.None));
        SeedEstimator estimator = new();
        foreach (EstimationProfile profile in Enum.GetValues<EstimationProfile>())
            Assert.Equal(ContractJson.Serialize(estimator.Estimate(after, profile)),
                ContractJson.Serialize(ChangeEstimator.RefreshDerivedEstimate(estimator.Estimate(before, profile), after)));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("role")]
    [InlineData("measurement")]
    [InlineData("location")]
    [InlineData("diagnostic")]
    [InlineData("ecosystem")]
    [InlineData("digest-format")]
    public async Task AnyChangedEstimationInputOrDuplicateRelationshipRejectsReuse(string scenario)
    {
        RepositoryEvidence before = await Scan("100", "100", "item");
        RepositoryEvidence after = await Scan("101", "101", "node");
        EvidenceFact target = after.Facts.Single(fact => fact.Id == "file:source.cs");
        EvidenceFact other = after.Facts.Single(fact => fact.Id == "file:web/source.ts");
        if (scenario == "diagnostic") after = after with
        {
            Diagnostics = [.. after.Diagnostics, new Diagnostic { Code = "FB9999",
                Severity = DiagnosticSeverity.Warning, Message = "changed evidence" }],
        };
        else if (scenario == "ecosystem") after = after with
        {
            Repository = after.Repository with { Ecosystems = ["unsupported"] },
        };
        else
        {
            EvidenceFact changed = scenario switch
            {
                "duplicate" => target with
                {
                    Tags = [.. target.Tags.Where(tag => !tag.StartsWith("sha256:",
                    StringComparison.Ordinal)), other.Tags.Single(tag => tag.StartsWith("sha256:", StringComparison.Ordinal))]
                },
                "digest-format" => target with
                {
                    Tags = [.. target.Tags.Where(tag => !tag.StartsWith("sha256:",
                    StringComparison.Ordinal)), "sha256:invalid"]
                },
                "role" => target with { Tags = [.. target.Tags, "classification:test"] },
                "location" => target with { Locations = [new() { Path = target.Scope, Line = 2 }] },
                _ => target with
                {
                    Measurements = [.. target.Measurements.Select(item =>
                    item.Name == "physical-lines" ? item with { Value = item.Value + 1 } : item)]
                },
            };
            after = after with { Facts = [.. after.Facts.Select(fact => fact.Id == target.Id ? changed : fact)] };
        }
        Assert.False(RepositoryStockEvidenceEquality.Equivalent(before, after, CancellationToken.None));
    }

    [Fact]
    public async Task EqualityProofRemainsCancellable()
    {
        RepositoryEvidence evidence = await Scan("100", "100", "item");
        using CancellationTokenSource source = new();
        source.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            RepositoryStockEvidenceEquality.Equivalent(evidence, evidence, source.Token));
    }

    private static async Task<RepositoryEvidence> Scan(string csharp, string typeScript, string table)
    {
        InMemoryRepository repository = new();
        repository.WriteText("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
            "<TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        repository.WriteText("source.cs", $"public class Source {{ public int Value() => {csharp}; }}");
        repository.WriteText("web/package.json", "{\"name\":\"web\"}");
        repository.WriteText("web/source.ts", $"export function value() {{ return {typeScript}; }}");
        repository.WriteText("query.sql", $"CREATE TABLE {table} (id INT PRIMARY KEY);");
        RepositoryEvidence result = await new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);
        return result with { Repository = result.Repository with { Name = "fixture" } };
    }
}
