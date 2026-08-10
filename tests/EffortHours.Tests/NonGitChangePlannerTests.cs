using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class NonGitChangePlannerTests
{
    private const string ProjectFile =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
        "<TargetFramework>net10.0</TargetFramework>" +
        "</PropertyGroup></Project>\n";

    [Fact]
    public async Task DirectoryPairProducesBoundedContentPinnedSnapshotsWithoutGit()
    {
        InMemoryRepository repository = RepositoryPair(
            "namespace Demo; public sealed class Widget { public int Value => 1; }\n",
            "namespace Demo; public sealed class Widget { public int Value => 2; }\n");
        NonGitChangePlanner planner = new(repository);

        ChangeEstimateInput input = await planner.PlanDirectoriesAsync(
            Path.Combine(repository.RootPath, "base"),
            Path.Combine(repository.RootPath, "head"));
        ChangeEstimateReport report = await new ChangeEstimator().EstimateAsync(
            input,
            EstimationProfile.Implementation);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.ChangeEstimateReport,
            ContractJson.Serialize(report));

        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.Equal(ChangeSelectionKind.BaseHead, report.Selection.Kind);
        Assert.Equal(ChangeSnapshotKind.Directory, report.Selection.Base.Kind);
        Assert.Equal(ChangeSnapshotKind.Directory, report.Selection.Head.Kind);
        Assert.StartsWith("sha256:", report.Selection.Base.ObjectId, StringComparison.Ordinal);
        Assert.StartsWith("sha256:", report.Selection.Head.ObjectId, StringComparison.Ordinal);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "FB5110");
        ChangePathEvidence path = Assert.Single(report.Evidence.Paths);
        Assert.Equal("Widget.cs", path.Path);
        Assert.Equal(ChangePathClassification.Represented, path.Classification);
        Assert.True(report.TotalEffort.Expected > 0m);

        await using IChangeSnapshot head = await input.OpenHeadAsync(CancellationToken.None);
        Assert.Equal(
            ["Demo.csproj", "Widget.cs"],
            [.. head.Files.Select(file => file.Path)]);
        Assert.DoesNotContain(head.Files, file => file.Path.StartsWith("../", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DirectoryPairFailsIfASelectedBodyChangesAfterItsIdentityWasPinned()
    {
        InMemoryRepository repository = RepositoryPair(
            "namespace Demo; public sealed class Widget { public int Value => 1; }\n",
            "namespace Demo; public sealed class Widget { public int Value => 2; }\n");
        NonGitChangePlanner planner = new(repository);
        ChangeEstimateInput input = await planner.PlanDirectoriesAsync(
            Path.Combine(repository.RootPath, "base"),
            Path.Combine(repository.RootPath, "head"));
        repository.WriteText(
            "head/Widget.cs",
            "namespace Demo; public sealed class Widget { public int Value => 9; }\n");

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new ChangeEstimator().EstimateAsync(input, EstimationProfile.Implementation));

        Assert.Contains("changed after its content identity was pinned", exception.Message);
    }

    [Fact]
    public async Task EvidencePairReusesFrozenAnalysisAndRetainsBodylessModificationConservatively()
    {
        InMemoryRepository repository = RepositoryPair(
            "namespace Demo; public sealed class Widget { public int Value => 1; }\n",
            "namespace Demo;\n\npublic sealed class Widget\n{\n    public int Value => 1;\n}\n");
        NonGitChangePlanner planner = new(repository);
        ChangeEstimateInput directoryInput = await planner.PlanDirectoriesAsync(
            Path.Combine(repository.RootPath, "base"),
            Path.Combine(repository.RootPath, "head"));
        RepositoryEvidence baseEvidence = await EvidenceAsync(directoryInput.OpenBaseAsync);
        RepositoryEvidence headEvidence = await EvidenceAsync(directoryInput.OpenHeadAsync);

        ChangeEstimateInput firstInput = NonGitChangePlanner.PlanEvidence(baseEvidence, headEvidence);
        ChangeEstimateInput secondInput = NonGitChangePlanner.PlanEvidence(baseEvidence, headEvidence);
        ChangeEstimateReport report = await new ChangeEstimator().EstimateAsync(
            firstInput,
            EstimationProfile.Implementation);

        Assert.Equal(firstInput.Selection, secondInput.Selection);
        Assert.Equal(ChangeSnapshotKind.Evidence, report.Selection.Base.Kind);
        Assert.Equal(ChangeSnapshotKind.Evidence, report.Selection.Head.Kind);
        Assert.Equal(directoryInput.Selection.Base.ObjectId, report.Selection.Base.ObjectId);
        Assert.Equal(directoryInput.Selection.Head.ObjectId, report.Selection.Head.ObjectId);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Code == "FB5111");
        ChangePathEvidence path = Assert.Single(report.Evidence.Paths);
        Assert.Equal(ChangePathClassification.Represented, path.Classification);
        Assert.True(path.Represented);
        Assert.Equal(1, path.EditRegions);
        Assert.Contains("Source bodies were unavailable", path.Reason, StringComparison.Ordinal);
        Assert.True(report.TotalEffort.Expected > 0m);
    }

    [Fact]
    public async Task BodylessGeneratedEvidenceCannotClaimCustomRegionEffort()
    {
        InMemoryRepository repository = new();
        repository.WriteText("base/Demo.csproj", ProjectFile);
        repository.WriteText("head/Demo.csproj", ProjectFile);
        repository.WriteText(
            "base/Generated.g.cs",
            Generated("internal partial class Client { public int Timeout => 10; }"));
        repository.WriteText(
            "head/Generated.g.cs",
            Generated("internal partial class Client { public int Timeout => 30; }"));
        NonGitChangePlanner planner = new(repository);
        ChangeEstimateInput directoryInput = await planner.PlanDirectoriesAsync(
            Path.Combine(repository.RootPath, "base"),
            Path.Combine(repository.RootPath, "head"));
        RepositoryEvidence baseEvidence = await EvidenceAsync(directoryInput.OpenBaseAsync);
        RepositoryEvidence headEvidence = await EvidenceAsync(directoryInput.OpenHeadAsync);

        ChangeEstimateReport sourceReport = await new ChangeEstimator().EstimateAsync(
            directoryInput,
            EstimationProfile.Implementation);
        ChangeEstimateReport evidenceReport = await new ChangeEstimator().EstimateAsync(
            NonGitChangePlanner.PlanEvidence(baseEvidence, headEvidence),
            EstimationProfile.Implementation);

        Assert.True(Assert.Single(sourceReport.Evidence.Paths).Represented);
        ChangePathEvidence path = Assert.Single(evidenceReport.Evidence.Paths);
        Assert.Equal(ChangePathClassification.Generated, path.Classification);
        Assert.False(path.Represented);
        Assert.Contains("Source bodies are unavailable", path.Reason, StringComparison.Ordinal);
        Assert.Equal(0m, evidenceReport.TotalEffort.Expected);
    }

    [Fact]
    public async Task EvidencePairRejectsADeclaredDigestThatDoesNotMatchItsFileInventory()
    {
        InMemoryRepository repository = RepositoryPair(
            "namespace Demo; public sealed class Widget { }\n",
            "namespace Demo; public sealed class Widget { public int Value => 1; }\n");
        NonGitChangePlanner planner = new(repository);
        ChangeEstimateInput directoryInput = await planner.PlanDirectoriesAsync(
            Path.Combine(repository.RootPath, "base"),
            Path.Combine(repository.RootPath, "head"));
        RepositoryEvidence baseEvidence = await EvidenceAsync(directoryInput.OpenBaseAsync);
        RepositoryEvidence headEvidence = await EvidenceAsync(directoryInput.OpenHeadAsync);
        RepositoryEvidence tampered = baseEvidence with
        {
            Repository = baseEvidence.Repository with
            {
                SourceDigest = "sha256:" + new string('0', 64),
            },
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            NonGitChangePlanner.PlanEvidence(tampered, headEvidence));

        Assert.Contains("sourceDigest", exception.Message, StringComparison.Ordinal);
    }

    private static InMemoryRepository RepositoryPair(string baseSource, string headSource)
    {
        InMemoryRepository repository = new();
        repository.WriteText("base/Demo.csproj", ProjectFile);
        repository.WriteText("base/Widget.cs", baseSource);
        repository.WriteText("head/Demo.csproj", ProjectFile);
        repository.WriteText("head/Widget.cs", headSource);
        return repository;
    }

    private static string Generated(string customCode) =>
        "// <auto-generated />\n" +
        "namespace Demo;\n" +
        "// <custom-code>\n" +
        customCode + "\n" +
        "// </custom-code>\n";

    private static async Task<RepositoryEvidence> EvidenceAsync(
        Func<CancellationToken, Task<IChangeSnapshot>> openAsync)
    {
        await using IChangeSnapshot snapshot = await openAsync(CancellationToken.None);
        return Assert.IsType<IRepositoryEvidenceChangeSnapshot>(snapshot, exactMatch: false).Evidence;
    }
}
