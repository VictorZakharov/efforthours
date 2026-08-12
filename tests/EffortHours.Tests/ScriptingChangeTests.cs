using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class ScriptingChangeTests
{
    [Theory]
    [InlineData(
        "run.sh",
        "#!/bin/sh\ngreet() { echo \"hello $1\"; } # old\ngreet \"$@\"\n",
        "#!/bin/sh\n# new explanation\ngreet()\n{\n  echo \"hello $1\"\n}\ngreet \"$@\"\n")]
    [InlineData(
        "run.ps1",
        "function Get-Greeting { param($Name); Write-Output \"Hello $Name\" } # old\nGet-Greeting world\n",
        "# new explanation\nfunction Get-Greeting {\n param($Name)\n Write-Output \"Hello $Name\"\n}\nGet-Greeting world\n")]
    [InlineData(
        "run",
        "#!/bin/sh\ngreet() { echo \"hello $1\"; }\ngreet \"$@\"\n",
        "#!/bin/sh\ngreet()\n{\n  echo \"hello $1\"\n}\ngreet \"$@\"\n")]
    [InlineData(
        "run.command",
        "#!/usr/bin/env pwsh\nfunction Get-Value { Write-Output 'value' }\n",
        "#!/usr/bin/env pwsh\nfunction Get-Value\n{\n  Write-Output 'value'\n}\n")]
    [InlineData(
        ".bashrc",
        "configure() { printf '%s' \"$1\"; }\n",
        "# ordinary comment\nconfigure()\n{\n  printf '%s' \"$1\"\n}\n")]
    [InlineData(
        "run.ksh",
        "#!/bin/ksh\ngreet() { print -- \"hello $1\"; }\n",
        "#!/bin/ksh\ngreet()\n{\n  print -- \"hello $1\"\n}\n")]
    public async Task FormattingAndOrdinaryCommentsHaveZeroEffort(
        string path,
        string before,
        string after)
    {
        ChangeEstimateReport report = await EstimateAsync(State((path, before)), State((path, after)));

        ChangePathEvidence change = Assert.Single(report.Evidence.Paths);
        Assert.Equal(ChangePathClassification.FormattingOnly, change.Classification);
        Assert.False(change.Represented);
        Assert.Equal(0m, report.TotalEffort.Expected);
    }

    [Theory]
    [InlineData("run.sh", "printf '%s' one\n", "printf '%s' two\n")]
    [InlineData("run.sh", "cat <<EOF\none\nEOF\n", "cat <<EOF\ntwo\nEOF\n")]
    [InlineData("run.ps1", "Write-Output 'one'\n", "Write-Output 'two'\n")]
    [InlineData("run.ps1", "$value = @'\none\n'@\n", "$value = @'\ntwo\n'@\n")]
    [InlineData("run.ps1", "#requires -Version 7.2\nWrite-Output ok\n", "#requires -Version 7.4\nWrite-Output ok\n")]
    public async Task LiteralsHereDocumentsAndRequirementsRemainMeaningful(
        string path,
        string before,
        string after)
    {
        ChangeEstimateReport report = await EstimateAsync(State((path, before)), State((path, after)));

        Assert.True(Assert.Single(report.Evidence.Paths).Represented);
        Assert.True(report.TotalEffort.Expected > 0m);
    }

    [Fact]
    public async Task ExtensionlessNonScriptDoesNotGainFormattingExclusion()
    {
        ChangeEstimateReport report = await EstimateAsync(
            State(("NOTICE", "first line\nsecond line\n")),
            State(("NOTICE", "first line\n  second line\n")));

        ChangePathEvidence change = Assert.Single(report.Evidence.Paths);
        Assert.NotEqual(ChangePathClassification.FormattingOnly, change.Classification);
        Assert.True(change.Represented);
    }

    [Theory]
    [InlineData("tests/check.bats", "@test \"check\" { run true; [ \"$status\" -eq 0 ]; }", EffortCategory.UnitTesting)]
    [InlineData("build/build.sh", "#!/bin/sh\ndotnet build\n", EffortCategory.BuildConfigurationAndDeveloperTooling)]
    [InlineData("ci/check.ps1", "dotnet test\n", EffortCategory.CiCdAndInfrastructureAsCode)]
    [InlineData("release/publish.ps1", "dotnet pack\n", EffortCategory.PackagingDeploymentAndReleaseArtifacts)]
    [InlineData("deploy/apply.sh", "#!/bin/sh\nkubectl apply -f app.yaml\n", EffortCategory.CiCdAndInfrastructureAsCode)]
    [InlineData("src/call.sh", "#!/bin/sh\ncurl https://example.invalid\n", EffortCategory.ExternalIntegrationsAndProtocols)]
    [InlineData("src/secure.ps1", "$ApiToken = Get-Secret -Name api\n", EffortCategory.SecurityAndAccessibility)]
    public async Task AddedScriptsReachTheirIntendedCategory(
        string path,
        string content,
        EffortCategory category)
    {
        ChangeEstimateReport report = await EstimateAsync(State(), State((path, content)));

        Assert.Contains(report.Categories, candidate =>
            candidate.Category == category && candidate.Hours.Expected > 0m);
        Assert.Equal("change-seed/0.14.0+seed-rules/0.4.0", report.EstimatorVersion);
    }

    private static Task<ChangeEstimateReport> EstimateAsync(ChangeState before, ChangeState after) =>
        new ChangeEstimator().EstimateAsync(
            new ChangeEstimateInput
            {
                RepositoryName = "in-memory-scripting-change",
                Selection = new ChangeSelection
                {
                    Kind = ChangeSelectionKind.BaseHead,
                    Base = Reference("base", before.ObjectId),
                    Head = Reference("head", after.ObjectId),
                },
                OpenBaseAsync = before.OpenAsync,
                OpenHeadAsync = after.OpenAsync,
            },
            EstimationProfile.Implementation);

    private static ChangeSnapshotReference Reference(string selector, string objectId) => new()
    {
        Selector = selector,
        ObjectId = objectId,
        Kind = ChangeSnapshotKind.GitTree,
    };

    private static ChangeState State(params (string Path, string Content)[] files)
    {
        InMemoryChangeSnapshot snapshot = new(files);
        return new ChangeState(snapshot.ObjectId, InMemoryChangeSnapshot.Factory(files));
    }

    private sealed record ChangeState(
        string ObjectId,
        Func<CancellationToken, Task<IChangeSnapshot>> OpenAsync);
}
