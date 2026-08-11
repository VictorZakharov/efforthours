using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed partial class JavaAnalyzerTests
{
    [Fact]
    public async Task MavenReactorAndGradleProjectsRemainDistinctWithLocalEdges()
    {
        InMemoryRepository repository = new();
        repository.WriteText("pom.xml", "<project><groupId>com.example</groupId><artifactId>root</artifactId><modules><module>service</module></modules></project>");
        repository.WriteText("service/pom.xml", "<project><parent><groupId>com.example</groupId></parent><artifactId>service</artifactId><dependencies><dependency><groupId>com.example</groupId><artifactId>domain</artifactId></dependency></dependencies></project>");
        repository.WriteText("service/src/main/java/com/example/service/App.java", "package com.example.service; import com.example.domain.Order; class App { Order order; }");
        repository.WriteText("domain/build.gradle.kts", "plugins { id(\"java-library\") }\n");
        repository.WriteText("domain/src/main/java/com/example/domain/Order.java", "package com.example.domain; public record Order(long id) { }");
        repository.WriteText("audit/build.gradle.kts", "plugins { id(\"java-library\") }\n");
        repository.WriteText("audit/src/main/java/com/example/audit/Audit.java", "package com.example.audit; public final class Audit { }");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact[] projects = [.. evidence.Facts.Where(fact => fact.Kind == EvidenceKinds.EcosystemPackage &&
            fact.Tags.Contains("scope:analyzed", StringComparer.Ordinal) &&
            fact.Tags.Contains("ecosystem:java", StringComparer.Ordinal))];

        Assert.Equal([".", "audit", "domain", "service"], projects.Select(project => project.Scope).Order(StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ProjectReference &&
            fact.Scope == "." && fact.Tags.Contains("target-scope:service", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ProjectReference &&
            fact.Scope == "service" && fact.Tags.Contains("target-scope:domain", StringComparer.Ordinal));
        Assert.DoesNotContain(evidence.Facts, fact => fact.Kind == EvidenceKinds.ProjectReference &&
            fact.Scope == "service" && fact.Tags.Contains("target-scope:audit", StringComparer.Ordinal));
    }

    [Fact]
    public async Task GradleSettingsLiteralIncludesMappingsAndDynamicValuesStayBounded()
    {
        InMemoryRepository repository = new();
        repository.WriteText("settings.gradle", "rootProject.name = 'suite'\ninclude ':api', ':domain'\nproject(':domain').projectDir = file('libs/domain')\nincludeBuild('../outside')\n");
        repository.WriteText("build.gradle", "plugins { id 'java' }\ndef versionName = providers.gradleProperty('version')\n");
        repository.WriteText("api/build.gradle", "dependencies { implementation project(':domain'); implementation \"org.example:client:$versionName\"; api 'org.example:contracts:1.2.3' }");
        repository.WriteText("api/src/main/java/example/Api.java", "package example; class Api { }");
        repository.WriteText("libs/domain/build.gradle", "plugins { id 'java-library' }");
        repository.WriteText("libs/domain/src/main/java/example/Domain.java", "package example; class Domain { }");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8103");
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8104");
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ProjectReference &&
            fact.Scope == "." && fact.Tags.Contains("target-scope:api", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ProjectReference &&
            fact.Scope == "." && fact.Tags.Contains("target-scope:libs/domain", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.PackageReference &&
            fact.Tags.Contains("dependency:org.example:client", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.PackageReference &&
            fact.Tags.Contains("dependency:org.example:contracts", StringComparer.Ordinal));
    }

    [Fact]
    public async Task GeneratedVendorAndBuildOutputsAreExcludedBeforeJavaAnalysis()
    {
        InMemoryRepository repository = new();
        repository.WriteText("pom.xml", "<project><artifactId>safe</artifactId></project>");
        repository.WriteText("src/main/java/Safe.java", "public class Safe { }");
        repository.WriteText("generated/Huge.java", "public class Huge { }");
        repository.WriteText("vendor/ThirdParty.java", "public class ThirdParty { }");
        repository.WriteText("target/generated-sources/Output.java", "public class Output { }");
        repository.WriteText("build/generated/source/Output.java", "public class GradleOutput { }");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact structure = FactOfKind(evidence, EvidenceKinds.SourceStructure);

        Assert.Equal(1m, Measurement(structure, "files"));
        Assert.DoesNotContain(evidence.Facts, fact => fact.Provenance.Analyzer == "efforthours.java-analyzer" &&
            fact.Locations.Any(location => location.Path.StartsWith("generated/", StringComparison.Ordinal) ||
                location.Path.StartsWith("vendor/", StringComparison.Ordinal) ||
                location.Path.StartsWith("target/", StringComparison.Ordinal) ||
                location.Path.StartsWith("build/", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task AnnotationProcessorsModulesAndMalformedBuildsExposeUncertaintyWithoutSourceText()
    {
        InMemoryRepository repository = new();
        repository.WriteText("pom.xml", "<!DOCTYPE project SYSTEM 'private-marker'><project>");
        repository.WriteText("src/main/java/module-info.java", "module safe.module { requires java.net.http; exports safe.api; uses safe.Plugin; }");
        repository.WriteText("src/main/java/safe/api/Api.java", "package safe.api; public interface Api { String privateMarker(); }");

        RepositoryEvidence evidence = await ScanAsync(repository);
        string json = ContractJson.Serialize(evidence);

        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8107");
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8106");
        EvidenceFact structure = FactOfKind(evidence, EvidenceKinds.SourceStructure);
        Assert.Equal(1m, Measurement(structure, "module-requires"));
        Assert.Equal(1m, Measurement(structure, "module-exports"));
        Assert.Equal(1m, Measurement(structure, "module-services"));
        Assert.DoesNotContain("privateMarker", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-marker", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PropertyBackedMavenIdentitiesRemainUnresolvedInsteadOfBecomingEvidence()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "pom.xml",
            "<project><groupId>${project.group}</groupId><artifactId>${project.name}</artifactId>" +
            "<dependencies><dependency><groupId>${dependency.group}</groupId>" +
            "<artifactId>client</artifactId></dependency></dependencies></project>");
        repository.WriteText("src/main/java/example/Value.java", "package example; public final class Value { }");

        RepositoryEvidence evidence = await ScanAsync(repository);
        string json = ContractJson.Serialize(evidence);

        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB8104");
        Assert.DoesNotContain("${", json, StringComparison.Ordinal);
        Assert.DoesNotContain(evidence.Facts, fact => fact.Kind == EvidenceKinds.PackageReference);
    }

    [Fact]
    public async Task JavaParticipatesInMixedRepositoryOwnership()
    {
        InMemoryRepository repository = new();
        repository.WriteText("App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        repository.WriteText("App.cs", "public sealed class App { }");
        repository.WriteText("web/package.json", "{\"name\":\"web\"}");
        repository.WriteText("web/index.js", "export const value = 1;");
        repository.WriteText("service/pom.xml", "<project><groupId>example</groupId><artifactId>service</artifactId></project>");
        repository.WriteText("service/src/main/java/example/Value.java", "package example; public record Value(int number) { }");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EstimateReport estimate = new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);

        Assert.Contains("dotnet", evidence.Repository.Ecosystems);
        Assert.Contains("javascript", evidence.Repository.Ecosystems);
        Assert.Contains("java", evidence.Repository.Ecosystems);
        Assert.Contains(estimate.WorkItems, item => item.Estimator.Id == "seed-rule:dotnet-source-backbone");
        Assert.Contains(estimate.WorkItems, item => item.Estimator.Id == "seed-rule:javascript-source-backbone");
        Assert.Contains(estimate.WorkItems, item => item.Estimator.Id == "seed-rule:polyglot-source-backbone");
        Assert.DoesNotContain(estimate.Diagnostics, diagnostic => diagnostic.Code is "FB1001" or "FB1002");
    }

    [Fact]
    public async Task ProductionNamesEndingInItAreNotMisclassifiedAsIntegrationTests()
    {
        InMemoryRepository repository = new();
        repository.WriteText("pom.xml", "<project><artifactId>audit</artifactId></project>");
        repository.WriteText("src/main/java/example/Audit.java", "package example; public final class Audit { }");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.DoesNotContain(evidence.Facts, fact => fact.Kind == EvidenceKinds.EcosystemTest);
        Assert.Contains("package:library-exports", ProjectFact(evidence).Tags);
    }

    [Fact]
    public async Task ModuleKeywordsOutsideModuleInfoDoNotCreateModuleDirectives()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "Words.java",
            "class Words { void requires() { } void exports() { } void uses() { } void provides() { } }");

        EvidenceFact structure = FactOfKind(await ScanAsync(repository), EvidenceKinds.SourceStructure);

        Assert.Equal(0m, Measurement(structure, "module-requires"));
        Assert.Equal(0m, Measurement(structure, "module-exports"));
        Assert.Equal(0m, Measurement(structure, "module-services"));
    }

    [Fact]
    public async Task ImplicitProjectUsesDeclaredPackagesForInternalImports()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "src/one/First.java",
            "package example.one; import example.two.Second; class First { Second value; }");
        repository.WriteText(
            "src/two/Second.java",
            "package example.two; public final class Second { }");

        EvidenceFact structure = FactOfKind(await ScanAsync(repository), EvidenceKinds.SourceStructure);

        Assert.Equal(1m, Measurement(structure, "internal-imports"));
    }

    [Fact]
    public async Task TestOnlyFrameworkCallsDoNotCreateProductionSemanticFacts()
    {
        InMemoryRepository repository = new();
        repository.WriteText("pom.xml", "<project><artifactId>tests</artifactId></project>");
        repository.WriteText(
            "src/test/java/example/RemoteTest.java",
            """
            package example;
            import java.net.http.HttpClient;
            import org.springframework.jdbc.core.JdbcTemplate;
            import org.springframework.web.bind.annotation.RestController;
            @RestController class RemoteTest {
                HttpClient client; JdbcTemplate jdbc;
                void checks() { client.sendAsync(null, null); jdbc.query("select 1", null); }
            }
            """);

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.EcosystemTest);
        Assert.DoesNotContain(evidence.Facts, fact => fact.Kind is
            EvidenceKinds.ApiSurface or EvidenceKinds.Integration or EvidenceKinds.DataAccess);
        Assert.Contains("package-role:library", ProjectFact(evidence).Tags);
    }
}
