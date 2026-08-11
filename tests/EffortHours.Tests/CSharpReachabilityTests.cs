using EffortHours.Contracts.V1;
using EffortHours.Core;

namespace EffortHours.Tests;

public sealed class CSharpReachabilityTests
{
    [Fact]
    public async Task BoundedReachabilityRetainsPrivateHelpersCalledByRepresentedBehavior()
    {
        InMemoryRepository repository = CreateRepository(
            "public sealed class Status { public string Load() => Fetch(); private string Fetch() => new System.Net.Http.HttpClient().GetStringAsync(\"https://example.invalid\").Result; }");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact structure = Fact(evidence, "dotnet:source-structure:App.csproj");

        Assert.Equal(2m, Measurement(structure, "methods"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.Integration);
        Assert.DoesNotContain(evidence.Facts, fact => fact.Id.StartsWith(
            "dotnet:excluded-unreferenced-private:",
            StringComparison.Ordinal));
    }

    [Fact]
    public async Task BoundedReachabilityExcludesPrivateBehaviorWithoutAnIntraFileReference()
    {
        InMemoryRepository repository = CreateRepository(
            "public sealed class Status { public string Format() => \"ready\"; private string Abandoned() => new System.Net.Http.HttpClient().GetStringAsync(\"https://example.invalid\").Result; }");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact structure = Fact(evidence, "dotnet:source-structure:App.csproj");
        EvidenceFact exclusion = Fact(
            evidence,
            "dotnet:excluded-unreferenced-private:Status.cs");

        Assert.Equal("0.3.3", exclusion.Provenance.AnalyzerVersion);
        Assert.Equal(1m, Measurement(structure, "methods"));
        Assert.Equal(0m, Measurement(structure, "branch-points"));
        Assert.Equal(1m, Measurement(exclusion, "excluded-private-methods"));
        Assert.Contains("classification:unreferenced-private", exclusion.Tags);
        Assert.DoesNotContain(evidence.Facts, fact => fact.Kind == EvidenceKinds.Integration);
    }

    [Fact]
    public async Task PartialTypesRemainOutsideTheBoundedReachabilityExclusion()
    {
        InMemoryRepository repository = CreateRepository(
            "public sealed partial class Status { private string ConventionHook() => \"ready\"; }");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Equal(
            1m,
            Measurement(Fact(evidence, "dotnet:source-structure:App.csproj"), "methods"));
        Assert.DoesNotContain(evidence.Facts, fact => fact.Id.StartsWith(
            "dotnet:excluded-unreferenced-private:",
            StringComparison.Ordinal));
    }

    [Fact]
    public async Task FrameworkBaseTypesRemainOutsideTheBoundedReachabilityExclusion()
    {
        InMemoryRepository repository = CreateRepository(
            "public abstract class FrameworkBase { } public sealed class Worker : FrameworkBase { private void Start() { } }");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Equal(
            1m,
            Measurement(Fact(evidence, "dotnet:source-structure:App.csproj"), "methods"));
        Assert.DoesNotContain(evidence.Facts, fact => fact.Id.StartsWith(
            "dotnet:excluded-unreferenced-private:",
            StringComparison.Ordinal));
    }

    [Fact]
    public async Task PrivateMainEntrypointsRemainRepresented()
    {
        InMemoryRepository repository = CreateRepository(
            "public static class Program { private static void Main() { } }");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Equal(
            1m,
            Measurement(Fact(evidence, "dotnet:source-structure:App.csproj"), "methods"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.EntryPoint);
        Assert.DoesNotContain(evidence.Facts, fact => fact.Id.StartsWith(
            "dotnet:excluded-unreferenced-private:",
            StringComparison.Ordinal));
    }

    private static InMemoryRepository CreateRepository(string source)
    {
        InMemoryRepository repository = new();
        repository.WriteText("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        repository.WriteText("Status.cs", source);
        return repository;
    }

    private static async Task<RepositoryEvidence> ScanAsync(InMemoryRepository repository) =>
        await new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);

    private static EvidenceFact Fact(RepositoryEvidence evidence, string id) =>
        Assert.Single(evidence.Facts, fact => fact.Id == id);

    private static decimal Measurement(EvidenceFact fact, string name) =>
        Assert.Single(fact.Measurements, measurement => measurement.Name == name).Value;
}
