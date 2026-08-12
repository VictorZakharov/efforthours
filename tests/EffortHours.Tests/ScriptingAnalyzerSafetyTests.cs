using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed partial class ScriptingAnalyzerTests
{
    [Theory]
    [InlineData("build/build.sh", "#!/bin/sh\ndotnet build\n", EffortCategory.BuildConfigurationAndDeveloperTooling)]
    [InlineData("ci/check.sh", "#!/bin/sh\ndotnet test\n", EffortCategory.CiCdAndInfrastructureAsCode)]
    [InlineData("release/publish.sh", "#!/bin/sh\ndotnet pack\n", EffortCategory.PackagingDeploymentAndReleaseArtifacts)]
    [InlineData("deploy/apply.sh", "#!/bin/sh\nkubectl apply -f app.yaml\n", EffortCategory.CiCdAndInfrastructureAsCode)]
    [InlineData("tests/check.bats", "@test \"check\" { run true; [ \"$status\" -eq 0 ]; }\n", EffortCategory.UnitTesting)]
    [InlineData("src/request.sh", "#!/bin/sh\ncurl https://example.invalid/status\n", EffortCategory.ExternalIntegrationsAndProtocols)]
    [InlineData("src/secure.ps1", "$ApiToken = Get-Secret -Name api\n", EffortCategory.SecurityAndAccessibility)]
    [InlineData("src/validate.sh", "#!/bin/sh\nset -euo pipefail\nprintf '%s' ok\n", EffortCategory.SecurityAndAccessibility)]
    public async Task ExactDuplicateScriptsDoNotIncreaseEffort(
        string path,
        string content,
        EffortCategory category)
    {
        InMemoryRepository baseline = new();
        baseline.WriteText(path, content);
        InMemoryRepository duplicate = new();
        duplicate.WriteText(path, content);
        string directory = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? string.Empty;
        string duplicatePath = $"{directory}/copy-{Path.GetFileName(path)}".TrimStart('/');
        duplicate.WriteText(duplicatePath, content);

        EstimateReport baselineEstimate = new SeedEstimator().Estimate(
            await ScanAsync(baseline),
            EstimationProfile.Implementation);
        EstimateReport duplicateEstimate = new SeedEstimator().Estimate(
            await ScanAsync(duplicate),
            EstimationProfile.Implementation);

        Assert.Equal(
            Assert.Single(baselineEstimate.Categories, item => item.Category == category).Hours,
            Assert.Single(duplicateEstimate.Categories, item => item.Category == category).Hours);
        Assert.Equal(baselineEstimate.TotalEffort, duplicateEstimate.TotalEffort);
    }

    [Fact]
    public async Task DistinctDeliveryScriptsIncreaseReleaseConfigurationEffort()
    {
        InMemoryRepository baseline = new();
        baseline.WriteText("release/publish.sh", "#!/bin/sh\ndotnet pack\n");
        InMemoryRepository expanded = new();
        expanded.WriteText("release/publish.sh", "#!/bin/sh\ndotnet pack\n");
        expanded.WriteText("release/sign.ps1", "dotnet nuget sign package.nupkg\n");

        EstimateReport baselineEstimate = new SeedEstimator().Estimate(
            await ScanAsync(baseline),
            EstimationProfile.Implementation);
        EstimateReport expandedEstimate = new SeedEstimator().Estimate(
            await ScanAsync(expanded),
            EstimationProfile.Implementation);
        EffortRange baselineHours = Assert.Single(baselineEstimate.Categories, item =>
            item.Category == EffortCategory.PackagingDeploymentAndReleaseArtifacts).Hours;
        EffortRange expandedHours = Assert.Single(expandedEstimate.Categories, item =>
            item.Category == EffortCategory.PackagingDeploymentAndReleaseArtifacts).Hours;

        Assert.True(expandedHours.Expected > baselineHours.Expected);
    }

    [Fact]
    public async Task EvidenceAndDiagnosticsNeverDiscloseScriptValues()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "run.ps1",
            "$ApiToken = 'DO_NOT_DISCLOSE_73519'\n& $DynamicCommand $ApiToken\n. $PluginPath\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        string serialized = System.Text.Json.JsonSerializer.Serialize(evidence);

        Assert.DoesNotContain("DO_NOT_DISCLOSE_73519", serialized, StringComparison.Ordinal);
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8503");
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8504");
        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == Analyzer &&
            fact.Tags.Contains("source-excerpts:not-emitted", StringComparer.Ordinal));
        Assert.Empty(ContractValidation.Validate(evidence));
    }

    [Fact]
    public async Task OrdinaryPowerShellAssignmentsDoNotInventDynamicInvocation()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "assign.ps1",
            "$Name = 'value'\n$CancellationToken = 'none'\nWrite-Output $Name\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact structure = Fact(evidence, EvidenceKinds.SourceStructure, "ecosystem:powershell");

        Assert.DoesNotContain("dynamic-invocation:unresolved", structure.Tags);
        Assert.DoesNotContain(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8504");
        Assert.DoesNotContain(evidence.Facts, fact =>
            fact.Provenance.Analyzer == Analyzer && fact.Kind == EvidenceKinds.SecurityConfiguration);
    }

    [Fact]
    public async Task IncompleteTokenizationLowersConfidenceAndRemainsVisible()
    {
        InMemoryRepository repository = new();
        repository.WriteText("broken.sh", "#!/bin/sh\nprintf 'unterminated\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact structure = Fact(evidence, EvidenceKinds.SourceStructure, "ecosystem:shell");

        Assert.Contains("parser-confidence:low", structure.Tags);
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8502");
    }

    [Fact]
    public async Task ShellErrorHandlingCountsEachSurfaceOnce()
    {
        InMemoryRepository repository = new();
        repository.WriteText("trap.sh", "#!/bin/sh\ntrap 'exit 1' TERM\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact structure = Fact(evidence, EvidenceKinds.SourceStructure, "ecosystem:shell");

        Assert.Equal(1m, Measurement(structure, "error-handlers"));
    }

    [Fact]
    public async Task InvocationContextLimitIsDeterministicAndVisible()
    {
        InMemoryRepository repository = new();
        for (int index = 0; index <= 500; index++)
        {
            string invocation = index == 500 ? "run: scripts/run.sh\n" : "run: dotnet test\n";
            repository.WriteText($".github/workflows/{index:D3}.yml", invocation);
        }
        repository.WriteText("scripts/run.sh", "#!/bin/sh\nprintf '%s' value\n");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8506");
        Assert.Contains(evidence.Facts, fact =>
            IsScriptFact(fact, EvidenceKinds.SourceStructure, "script-role:product-or-module") &&
            fact.Locations.Any(location => location.Path == "scripts/run.sh"));
    }

    [Fact]
    public async Task LocalFunctionsNamedLikeRemoteCommandsDoNotInventIntegrations()
    {
        InMemoryRepository repository = new();
        repository.WriteText("local.sh", "#!/bin/sh\ncurl() { printf '%s' \"$1\"; }\ncurl local\n");
        repository.WriteText(
            "Local.psm1",
            "function global:Invoke-RestMethod { param($Value) return $Value }\nInvoke-RestMethod local\n");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.DoesNotContain(evidence.Facts, fact => fact.Provenance.Analyzer == Analyzer &&
            fact.Kind == EvidenceKinds.Integration);
        Assert.Empty(ContractValidation.Validate(evidence));
    }

    [Fact]
    public async Task ShellCommandAndFunctionMatchingRemainsCaseSensitive()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "case-sensitive.sh",
            "#!/bin/sh\nCURL() { printf '%s' local; }\ncurl https://example.invalid/status\n");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains(evidence.Facts, fact =>
            fact.Provenance.Analyzer == Analyzer && fact.Kind == EvidenceKinds.Integration);
    }
}
