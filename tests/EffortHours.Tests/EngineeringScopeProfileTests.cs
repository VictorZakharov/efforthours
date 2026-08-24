using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class EngineeringScopeProfileTests
{
    [Theory]
    [InlineData("src/App.cs", true)]
    [InlineData("tests/AppTests.cs", true)]
    [InlineData(".github/workflows/ci.yml", true)]
    [InlineData("deploy/schema.sql", true)]
    [InlineData("web/view.tsx", true)]
    [InlineData("README.md", false)]
    [InlineData("AGENTS.md", false)]
    [InlineData("package-lock.json", false)]
    [InlineData("vendor/library.js", false)]
    [InlineData("generated/client.cs", false)]
    [InlineData("assets/photo.png", false)]
    public void BundledProfileAdmitsEngineeringAndExcludesNonEngineeringPaths(
        string path,
        bool expected)
    {
        ChangePathAdmission admission = EngineeringScopeProfile.Load(null)
            .CreateAdmission("owner/repository");

        Assert.Equal(expected, admission.Admits(path));
    }

    [Theory]
    [InlineData("victorzakharov/efforthours", "calibration/case.json")]
    [InlineData("victorzakharov/efforthours", "artifacts/result.json")]
    [InlineData("victorzakharov/pte-core-exam", "public/questions/a.json")]
    public void RepositoryOverridesExcludeConfiguredContent(string repository, string path)
    {
        Assert.False(EngineeringScopeProfile.Load(null).CreateAdmission(repository).Admits(path));
    }

    [Fact]
    public void DuplicateArchivedAndMirrorRepositoriesAreExcluded()
    {
        EngineeringScopeProfile profile = EngineeringScopeProfile.Load(null);

        Assert.True(profile.ExcludesRepository(
            "victorzakharov/dotnet-image-viewer-archive", archived: false, mirror: false));
        Assert.True(profile.ExcludesRepository("owner/archived", archived: true, mirror: false));
        Assert.True(profile.ExcludesRepository("owner/mirror", archived: false, mirror: true));
    }

    [Fact]
    public void MissingExplicitOverrideFailsClosed()
    {
        string missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.json");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            EngineeringScopeProfile.Load(missing));

        Assert.Contains("does not exist", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EstimatorInventoriesAndEvidenceSeeOnlyAdmittedPaths()
    {
        (string Path, string Content)[] before =
        [
            ("src/App.cs", "namespace Demo; internal class App { public int Value => 1; }\n"),
            ("README.md", "old prose\n"),
            ("package-lock.json", "{\"lockfileVersion\":2}\n"),
        ];
        (string Path, string Content)[] after =
        [
            ("src/App.cs", "namespace Demo; internal class App { public int Value => 2; }\n"),
            ("README.md", "new prose\n"),
            ("package-lock.json", "{\"lockfileVersion\":3}\n"),
        ];
        InMemoryChangeSnapshot baseSnapshot = new(before);
        InMemoryChangeSnapshot headSnapshot = new(after);
        ChangeEstimateReport report = await new ChangeEstimator().EstimateAsync(
            new ChangeEstimateInput
            {
                RepositoryName = "owner/repository",
                Selection = new ChangeSelection
                {
                    Kind = ChangeSelectionKind.BaseHead,
                    Base = Reference("base", baseSnapshot.ObjectId),
                    Head = Reference("head", headSnapshot.ObjectId),
                },
                OpenBaseAsync = InMemoryChangeSnapshot.Factory(before),
                OpenHeadAsync = InMemoryChangeSnapshot.Factory(after),
                PathAdmission = EngineeringScopeProfile.Load(null)
                    .CreateAdmission("owner/repository"),
            },
            EstimationProfile.Implementation);

        ChangePathEvidence evidence = Assert.Single(report.Evidence.Paths);
        Assert.Equal("src/App.cs", evidence.Path);
        Assert.DoesNotContain(report.Evidence.Paths, path => path.Path is "README.md" or "package-lock.json");
    }

    private static ChangeSnapshotReference Reference(string selector, string objectId) => new()
    {
        Selector = selector,
        ObjectId = objectId,
        Kind = ChangeSnapshotKind.Evidence,
    };
}
