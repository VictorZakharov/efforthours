using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed partial class GoAnalyzerTests
{
    [Fact]
    public async Task WorkspaceNestedModulesPackagesAndLocalReferencesRemainDistinct()
    {
        InMemoryRepository repository = new();
        repository.WriteText("go.work", "go 1.24\nuse (\n ./services/api\n ./libs/domain\n)\n");
        repository.WriteText("tools/go.work", "go 1.24\nuse ../libs/domain\n");
        repository.WriteText(
            "services/api/go.mod",
            "module example.com/api\nrequire example.com/domain v0.0.0\nreplace example.com/domain => ../../libs/domain\n");
        repository.WriteText(
            "services/api/cmd/api/main.go",
            "package main\nimport (\n \"example.com/api/internal/config\"\n \"example.com/domain/model\"\n)\nfunc main() { _ = model.Order{}; _ = config.Value }\n");
        repository.WriteText(
            "services/api/internal/config/config.go",
            "package config\nconst Value = 1\n");
        repository.WriteText("libs/domain/go.mod", "module example.com/domain\n");
        repository.WriteText("libs/domain/model/order.go", "package model\ntype Order struct { ID int }\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact[] modules = [.. evidence.Facts
            .Where(fact => fact.Kind == EvidenceKinds.EcosystemPackage &&
                fact.Tags.Contains("scope:analyzed", StringComparer.Ordinal))
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        EvidenceFact reference = Assert.Single(evidence.Facts, fact =>
            fact.Kind == EvidenceKinds.ProjectReference &&
            fact.Provenance.Analyzer == "efforthours.go-analyzer");

        Assert.Equal(["libs/domain", "services/api"], modules.Select(fact => fact.Scope));
        Assert.Contains(evidence.Facts, fact => fact.Id.StartsWith("go:workspace:", StringComparison.Ordinal));
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8008");
        Assert.Contains(evidence.Facts, fact => fact.Id == "go:package:libs~domain~model");
        Assert.Contains(evidence.Facts, fact => fact.Id == "go:package:services~api~cmd~api");
        Assert.Equal("services/api", reference.Scope);
        Assert.Contains("target-scope:libs/domain", reference.Tags);
        Assert.Contains("reference-kind:local-replace", reference.Tags);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure && fact.Scope == "services/api" &&
            fact.Measurements.Any(measurement => measurement.Name == "internal-imports" && measurement.Value == 1m));
    }

    [Fact]
    public async Task UnsafeLocalReplacementIsRejectedWithoutFollowingIt()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "go.mod",
            "module example.com/safe\nrequire example.com/private v0.0.0\nreplace example.com/private => ../../outside\n");
        repository.WriteText("safe.go", "package safe\nfunc Value() int { return 1 }\n");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8003");
        Assert.DoesNotContain(evidence.Facts, fact => fact.Kind == EvidenceKinds.ProjectReference);
    }

    [Fact]
    public async Task GoSumAloneDoesNotInventAnAnalyzedModuleScope()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "go.sum",
            "example.com/dependency v1.0.0 h1:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=\n");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains("go", evidence.Repository.Ecosystems);
        Assert.DoesNotContain(evidence.Facts, fact =>
            fact.Provenance.Analyzer == "efforthours.go-analyzer");
        Assert.DoesNotContain(evidence.Diagnostics, diagnostic => diagnostic.Code.StartsWith("FB80", StringComparison.Ordinal));
    }

    [Fact]
    public async Task IntegrationEndToEndAndTestdataConventionsRemainTestEvidence()
    {
        InMemoryRepository repository = new();
        repository.WriteText("go.mod", "module example.com/tests\n");
        repository.WriteText("integration/orders_test.go", "package integration\nfunc TestOrders() {}\n");
        repository.WriteText("e2e/cli_test.go", "package e2e\nfunc TestCli() {}\n");
        repository.WriteText("pkg/testdata/fixture.go", "package fixture\nvar Value = 1\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact[] tests = [.. evidence.Facts.Where(fact =>
            fact.Kind == EvidenceKinds.EcosystemTest &&
            fact.Provenance.Analyzer == "efforthours.go-analyzer")];

        Assert.Equal(3, tests.Length);
        Assert.Contains(tests, fact => fact.Tags.Contains("test-type:integration", StringComparer.Ordinal));
        Assert.Contains(tests, fact => fact.Tags.Contains("test-type:end-to-end", StringComparer.Ordinal));
    }

    [Fact]
    public async Task GeneratedVendorAndToolOutputAreExcludedBeforeGoAnalysis()
    {
        InMemoryRepository repository = new();
        repository.WriteText("go.mod", "module example.com/safe\n");
        repository.WriteText("safe.go", "package safe\nfunc Value() int { return 1 }\n");
        repository.WriteText("generated/client.go", "package generated\nfunc Huge() int { return 1 }\n");
        repository.WriteText("vendor/example.com/lib/lib.go", "package lib\nfunc Vendor() {}\n");
        repository.WriteText("target/cache.go", "package cache\nfunc Cache() {}\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact structure = FactOfKind(evidence, EvidenceKinds.SourceStructure);

        Assert.Equal(1m, Measurement(structure, "files"));
        Assert.DoesNotContain(evidence.Facts, fact =>
            fact.Provenance.Analyzer == "efforthours.go-analyzer" &&
            fact.Locations.Any(location => location.Path.StartsWith("generated/", StringComparison.Ordinal) ||
                location.Path.StartsWith("vendor/", StringComparison.Ordinal) ||
                location.Path.StartsWith("target/", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task CgoBuildSelectionAndBlankRegistrationRemainExplicitUncertaintyWithoutSourceDisclosure()
    {
        InMemoryRepository repository = new();
        repository.WriteText("go.mod", "module example.com/native\n");
        repository.WriteText(
            "native_windows.go",
            """
            //go:build windows
            package native
            /*
            #cgo LDFLAGS: -lprivate_native_marker
            */
            import "C"
            import _ "example.com/private/runtime-driver"
            """);

        RepositoryEvidence evidence = await ScanAsync(repository);
        string json = ContractJson.Serialize(evidence);

        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8004");
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8005");
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8006");
        Assert.DoesNotContain("private_native_marker", json, StringComparison.Ordinal);
        Assert.DoesNotContain("runtime-driver", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GoParticipatesInMixedRepositoryOwnership()
    {
        InMemoryRepository repository = new();
        repository.WriteText("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        repository.WriteText("App.cs", "public sealed class App { }");
        repository.WriteText("web/package.json", "{\"name\":\"web\"}");
        repository.WriteText("web/index.js", "export const value = 1;");
        repository.WriteText("service/go.mod", "module example.com/service\n");
        repository.WriteText("service/main.go", "package service\nfunc Value() int { return 1 }\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EstimateReport estimate = new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);

        Assert.Contains("dotnet", evidence.Repository.Ecosystems);
        Assert.Contains("javascript", evidence.Repository.Ecosystems);
        Assert.Contains("go", evidence.Repository.Ecosystems);
        Assert.Contains(estimate.WorkItems, item => item.Estimator.Id == "seed-rule:dotnet-source-backbone");
        Assert.Contains(estimate.WorkItems, item => item.Estimator.Id == "seed-rule:javascript-source-backbone");
        Assert.Contains(estimate.WorkItems, item => item.Estimator.Id == "seed-rule:polyglot-source-backbone");
        Assert.DoesNotContain(estimate.Diagnostics, diagnostic => diagnostic.Code is "FB1001" or "FB1002");
    }
}
