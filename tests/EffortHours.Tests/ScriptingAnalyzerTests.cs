using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed partial class ScriptingAnalyzerTests
{
    private const string Analyzer = "efforthours.scripting-analyzer";

    [Theory]
    [InlineData("tools/check.ksh", "#!/bin/ksh\nprintf '%s' ok\n", "shell", EvidenceKinds.SourceStructure)]
    [InlineData("tools/check.command", "#!/usr/bin/env pwsh\nWrite-Output ok\n", "powershell", EvidenceKinds.SourceStructure)]
    [InlineData(".profile", "configure() { printf '%s' ok; }\n", "shell", EvidenceKinds.BuildConfiguration)]
    public async Task RecognizedScriptShapesReceiveTokenBackedAnalysis(
        string path,
        string content,
        string language,
        string evidenceKind)
    {
        InMemoryRepository repository = new();
        repository.WriteText(path, content);

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains("analysis-depth:token-backed", Language(evidence, language).Tags);
        Assert.Contains(evidence.Facts, fact =>
            fact.Provenance.Analyzer == Analyzer &&
            fact.Kind == evidenceKind &&
            fact.Tags.Contains($"ecosystem:{language}", StringComparer.Ordinal));
    }

    [Fact]
    public async Task BomEncodedPowerShellRemainsAnalyzable()
    {
        InMemoryRepository repository = new();
        byte[] script = System.Text.Encoding.Unicode.GetBytes(
            "function Get-Value { Write-Output 'value' }\n");
        repository.WriteBytes(
            "legacy.ps1",
            [.. System.Text.Encoding.Unicode.GetPreamble(), .. script]);

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains(evidence.Facts, fact =>
            fact.Provenance.Analyzer == Analyzer &&
            fact.Kind == EvidenceKinds.SourceStructure &&
            fact.Tags.Contains("ecosystem:powershell", StringComparer.Ordinal));
    }

    [Fact]
    public async Task ShellAndPowerShellStructureProduceTraceableEvidenceAndEffort()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "bin/backup",
            """
            #!/usr/bin/env bash
            set -euo pipefail
            backup() {
              local destination="$1"
              if test -z "$destination"; then return 2; fi
              for item in "$@"; do printf '%s\n' "$item" | sed 's/x/y/'; done
              curl https://example.invalid/status
              wait
            }
            source "${PLUGIN_PATH}"
            backup "$@"
            """);
        repository.WriteText(
            "src/Operations.psm1",
            """
            #requires -Version 7.4
            function Invoke-Operation {
                param([string] $Name, [securestring] $Password)
                try {
                    Invoke-RestMethod -Uri 'https://example.invalid/status' -ErrorAction Stop |
                        ForEach-Object { $_ }
                    Start-Job { Get-Content './status.txt' }
                } catch { throw }
            }
            Export-ModuleMember -Function Invoke-Operation
            """);
        repository.WriteText(
            "tests/backup.bats",
            """
            @test "backup succeeds" {
              run ./bin/backup target
              [[ "$status" -eq 0 ]]
            }
            """);
        repository.WriteText(
            "tests/Operations.Tests.ps1",
            """
            Describe 'operation' {
                It 'runs' { Mock Invoke-RestMethod; Invoke-Operation demo | Should -Not -BeNullOrEmpty }
            }
            """);

        RepositoryEvidence evidence = await ScanAsync(repository);
        EstimateReport estimate = new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);
        EvidenceFact shell = Fact(evidence, EvidenceKinds.SourceStructure, "ecosystem:shell");
        EvidenceFact powerShell = Fact(evidence, EvidenceKinds.SourceStructure, "ecosystem:powershell");
        EvidenceFact shellTests = Fact(evidence, EvidenceKinds.EcosystemTest, "ecosystem:shell");

        Assert.Contains("analysis-depth:token-backed", Language(evidence, "shell").Tags);
        Assert.Contains("analysis-depth:token-backed", Language(evidence, "powershell").Tags);
        Assert.True(Measurement(shell, "functions") >= 1m);
        Assert.True(Measurement(shell, "branch-points") >= 2m);
        Assert.True(Measurement(shell, "conditionals") >= 1m);
        Assert.True(Measurement(shell, "loops") >= 1m);
        Assert.True(Measurement(shell, "error-handlers") >= 1m);
        Assert.True(Measurement(shell, "external-commands") >= 1m);
        Assert.True(Measurement(shell, "file-operations") >= 1m);
        Assert.True(Measurement(shell, "network-operations") >= 1m);
        Assert.True(Measurement(shell, "process-operations") >= 1m);
        Assert.True(Measurement(shell, "pipelines") >= 1m);
        Assert.True(Measurement(shell, "sourced-files") >= 1m);
        Assert.Contains("dialect:bash", shell.Tags);
        Assert.Contains("dynamic-invocation:unresolved", shell.Tags);
        Assert.True(Measurement(shellTests, "assertions") >= 1m);
        Assert.True(Measurement(powerShell, "functions") >= 1m);
        Assert.True(Measurement(powerShell, "parameters") >= 2m);
        Assert.True(Measurement(powerShell, "async-units") >= 1m);
        Assert.True(Measurement(powerShell, "file-operations") >= 1m);
        Assert.True(Measurement(powerShell, "network-operations") >= 1m);
        Assert.True(Measurement(powerShell, "process-operations") >= 1m);
        Assert.True(Measurement(powerShell, "requires-directives") >= 1m);
        Assert.Contains("dialect:powershell", powerShell.Tags);
        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == Analyzer &&
            fact.Kind == EvidenceKinds.Integration &&
            fact.Tags.Contains("technology:http-client", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == Analyzer &&
            fact.Kind == EvidenceKinds.SecurityConfiguration);
        Assert.Equal(2, evidence.Facts.Count(fact =>
            fact.Provenance.Analyzer == Analyzer && fact.Kind == EvidenceKinds.EcosystemTest));
        Assert.Contains(estimate.WorkItems, item => item.Estimator.Id == "seed-rule:polyglot-source-backbone");
        Assert.Contains(estimate.WorkItems, item => item.Category == EffortCategory.UnitTesting);
        Assert.Contains(estimate.WorkItems, item => item.Category == EffortCategory.ExternalIntegrationsAndProtocols);
        Assert.DoesNotContain(estimate.Diagnostics, diagnostic => diagnostic.Code == "FB1002");
        Assert.Empty(ContractValidation.Validate(evidence));
        Assert.Empty(ContractValidation.Validate(estimate));
    }

    [Fact]
    public async Task PathsAndManifestInvocationsSeparateAutomationFromProductCode()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "package.json",
            """
            {"name":"automation","scripts":{"build":"./tools/release-helper.sh","test":"pwsh ./checks/verify.ps1"}}
            """);
        repository.WriteText("tools/release-helper.sh", "#!/bin/sh\nset -e\ndotnet build\n");
        repository.WriteText("checks/verify.ps1", "Set-StrictMode -Version Latest\ndotnet test\n");
        repository.WriteText("release/publish.ps1", "dotnet pack\ndotnet nuget push package.nupkg\n");
        repository.WriteText("deploy/provision.sh", "#!/bin/sh\nkubectl apply -f deployment.yaml\n");
        repository.WriteText("ci/check.ps1", "dotnet test\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EstimateReport estimate = new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);

        Assert.Contains(evidence.Facts, fact => IsScriptFact(fact, EvidenceKinds.BuildConfiguration, "script-role:build") &&
            fact.Locations.Any(location => location.Path == "tools/release-helper.sh"));
        Assert.Contains(evidence.Facts, fact => IsScriptFact(fact, EvidenceKinds.EcosystemTest, "script-role:test") &&
            fact.Locations.Any(location => location.Path == "checks/verify.ps1"));
        Assert.Contains(evidence.Facts, fact => IsScriptFact(fact, EvidenceKinds.DeliveryAutomation, "script-role:delivery"));
        Assert.Contains(evidence.Facts, fact => IsScriptFact(fact, EvidenceKinds.Infrastructure, "script-role:infrastructure"));
        Assert.Contains(evidence.Facts, fact => IsScriptFact(fact, EvidenceKinds.CiConfiguration, "script-role:ci"));
        Assert.DoesNotContain(evidence.Facts, fact => fact.Provenance.Analyzer == Analyzer &&
            fact.Kind == EvidenceKinds.SourceStructure);
        Assert.Contains(estimate.WorkItems, item => item.Category == EffortCategory.BuildConfigurationAndDeveloperTooling);
        Assert.Contains(estimate.WorkItems, item => item.Category == EffortCategory.CiCdAndInfrastructureAsCode);
        Assert.Contains(estimate.WorkItems, item => item.Category == EffortCategory.PackagingDeploymentAndReleaseArtifacts);
        Assert.Equal(
            1.25m,
            Assert.Single(estimate.Categories, category =>
                category.Category == EffortCategory.PackagingDeploymentAndReleaseArtifacts).Hours.Expected);
        Assert.Empty(ContractValidation.Validate(estimate));
    }

    [Fact]
    public async Task CaseDistinctScriptPathsRemainIndependentOnCaseSensitiveFileSystems()
    {
        if (OperatingSystem.IsWindows()) return;

        InMemoryRepository repository = new();
        repository.WriteText(
            "package.json",
            """
            {"name":"automation","scripts":{"build":"./scripts/Run.sh","test":"./scripts/run.sh"}}
            """);
        repository.WriteText("scripts/Run.sh", "#!/bin/sh\ndotnet build\n");
        repository.WriteText("scripts/run.sh", "#!/bin/sh\ndotnet test\n");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains(evidence.Facts, fact =>
            IsScriptFact(fact, EvidenceKinds.BuildConfiguration, "script-role:build") &&
            fact.Locations.Any(location => location.Path == "scripts/Run.sh"));
        Assert.Contains(evidence.Facts, fact =>
            IsScriptFact(fact, EvidenceKinds.EcosystemTest, "script-role:test") &&
            fact.Locations.Any(location => location.Path == "scripts/run.sh"));
    }

    [Fact]
    public async Task GeneratedCompletionsCopiedLaunchersAndExactDuplicatesDoNotInflateStructure()
    {
        InMemoryRepository repository = new();
        const string maintained = "#!/bin/sh\nvalue() { printf '%s' \"$1\"; }\nvalue \"$@\"\n";
        repository.WriteText("bin/value.sh", maintained);
        repository.WriteText("copy/value.sh", maintained);
        repository.WriteText("completions/value-completion.sh", "# generated completion\ncomplete -F _value value\n");
        repository.WriteText("gradlew", "#!/bin/sh\n# copied launcher\nexec java -jar gradle-wrapper.jar\n");
        repository.WriteText("mvnw", "#!/bin/sh\n# project-authored inert launcher stand-in\nexit 0\n");
        repository.WriteText("dotnet-install.sh", "#!/bin/sh\n# project-authored inert installer stand-in\nexit 0\n");
        repository.WriteText("install-powershell.ps1", "# project-authored inert installer stand-in\nexit 0\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EstimateReport estimate = new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);
        EvidenceFact structure = Fact(evidence, EvidenceKinds.SourceStructure, "ecosystem:shell");
        WorkItem backbone = Assert.Single(estimate.WorkItems, item =>
            item.Estimator.Id == "seed-rule:polyglot-source-backbone");

        Assert.Equal(2m, Measurement(structure, "files"));
        Assert.DoesNotContain(structure.Locations, location =>
            location.Path is
                "completions/value-completion.sh" or "gradlew" or "mvnw" or
                "dotnet-install.sh" or "install-powershell.ps1");
        Assert.Contains(backbone.Assumptions, assumption =>
            assumption.Contains("Exact duplicate source", StringComparison.Ordinal));
        Assert.Empty(ContractValidation.Validate(estimate));
    }

    [Fact]
    public async Task MixedRepositoryKeepsLanguageOwnershipAndBackbonesDistinct()
    {
        InMemoryRepository repository = new();
        repository.WriteText("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        repository.WriteText("Program.cs", "internal static class Program { public static void Main() { } }");
        repository.WriteText("package.json", "{\"name\":\"web\"}");
        repository.WriteText("index.ts", "export function value(): number { return 1; }");
        repository.WriteText("bin/report.sh", "#!/bin/sh\nreport() { printf '%s' \"$1\"; }\nreport \"$@\"\n");
        repository.WriteText("src/Report.psm1", "function Get-Report { param($Name) return $Name }");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EstimateReport estimate = new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);

        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == "efforthours.dotnet-analyzer" &&
            fact.Kind == EvidenceKinds.SourceStructure);
        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == "efforthours.javascript-analyzer" &&
            fact.Kind == EvidenceKinds.SourceStructure);
        Assert.Contains(evidence.Facts, fact => fact.Id == "script:source:shell:repository");
        Assert.Contains(evidence.Facts, fact => fact.Id == "script:source:powershell:repository");
        Assert.Equal(2, estimate.WorkItems.Count(item =>
            item.Estimator.Id == "seed-rule:polyglot-source-backbone"));
        Assert.Empty(ContractValidation.Validate(estimate));
    }

    private static bool IsScriptFact(EvidenceFact fact, string kind, string tag) =>
        fact.Provenance.Analyzer == Analyzer && fact.Kind == kind &&
        fact.Tags.Contains(tag, StringComparer.Ordinal);

    private static async Task<RepositoryEvidence> ScanAsync(InMemoryRepository repository) =>
        await new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);

    private static EvidenceFact Fact(RepositoryEvidence evidence, string kind, string tag) =>
        Assert.Single(evidence.Facts, fact => fact.Provenance.Analyzer == Analyzer &&
            fact.Kind == kind && fact.Tags.Contains(tag, StringComparer.Ordinal));

    private static EvidenceFact Language(RepositoryEvidence evidence, string language) =>
        Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.Language &&
            fact.Tags.Contains($"language:{language}", StringComparer.Ordinal));

    private static decimal Measurement(EvidenceFact fact, string name) =>
        Assert.Single(fact.Measurements, measurement => measurement.Name == name).Value;
}
