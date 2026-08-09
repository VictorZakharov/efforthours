using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;

namespace EffortHours.Tests;

public sealed class DotNetAnalyzerTests
{
    [Fact]
    public async Task PipelineProducesTraceableDotNetEvidenceWithoutBuildingTarget()
    {
        DotNetFixtureRepository repository = DotNetFixtureRepository.Create();
        RepositoryAnalysisPipeline pipeline = new(repository);

        RepositoryEvidence first = await pipeline.ScanAsync(repository.RootPath);
        RepositoryEvidence second = await pipeline.ScanAsync(repository.RootPath);
        string json = ContractJson.Serialize(first);

        Assert.Equal(json, ContractJson.Serialize(second));
        Assert.Empty(ContractValidation.Validate(first));
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.RepositoryEvidence,
            json);
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        Assert.DoesNotContain("/orders", json, StringComparison.Ordinal);
        Assert.DoesNotContain("fixture-secret-marker", json, StringComparison.Ordinal);

        EvidenceFact apiProject = Fact(first, "dotnet:project:src/Api/Api.csproj");
        Assert.Contains("project-role:web", apiProject.Tags);
        Assert.Contains("target-framework:net10.0", apiProject.Tags);
        EvidenceFact testProject = Fact(first, "dotnet:project:tests/Api.Tests/Api.Tests.csproj");
        Assert.Contains("project-role:test", testProject.Tags);

        EvidenceFact efPackage = Fact(
            first,
            "dotnet:package:src/Api/Api.csproj:microsoft.entityframeworkcore");
        Assert.Contains("version:10.0.0", efPackage.Tags);
        Assert.Contains("package-family:data", efPackage.Tags);
        Assert.Contains(first.Facts, fact => fact.Kind == EvidenceKinds.ProjectReference);
        Assert.Contains(first.Facts, fact => fact.Kind == EvidenceKinds.EntryPoint);
        Assert.Contains(first.Facts, fact => fact.Kind == EvidenceKinds.ApiSurface);
        Assert.Contains(first.Facts, fact => fact.Kind == EvidenceKinds.DataAccess);
        Assert.Contains(first.Facts, fact => fact.Kind == EvidenceKinds.BackgroundWork);
        Assert.Contains(first.Facts, fact => fact.Kind == EvidenceKinds.Integration);
        Assert.Contains(first.Facts, fact => fact.Kind == EvidenceKinds.SecurityConfiguration);
        Assert.Contains(first.Facts, fact => fact.Kind == EvidenceKinds.Validation);
        Assert.Contains(first.Facts, fact => fact.Kind == EvidenceKinds.UserInterface);

        EvidenceFact controllerApi = Fact(
            first,
            "dotnet:api:src/Api/OrdersController.cs");
        Assert.Equal(1m, Measurement(controllerApi, "controllers"));
        Assert.Equal(2m, Measurement(controllerApi, "attributed-endpoints"));
        EvidenceFact minimalApi = Fact(first, "dotnet:api:src/Api/Program.cs");
        Assert.Equal(1m, Measurement(minimalApi, "minimal-api-endpoints"));
        EvidenceFact data = Fact(first, "dotnet:data:src/Api/Data.cs");
        Assert.Equal(1m, Measurement(data, "db-contexts"));
        Assert.Equal(1m, Measurement(data, "migrations"));

        EvidenceFact tests = Assert.Single(
            first.Facts,
            fact => fact.Id == "dotnet:test:tests/Api.Tests/OrdersTests.cs");
        Assert.Contains("test-type:integration", tests.Tags);
        Assert.Equal(2m, Measurement(tests, "test-methods"));
        Assert.Equal(3m, Measurement(tests, "parameterized-cases"));
        Assert.Contains(first.Diagnostics, diagnostic => diagnostic.Code == "FB3000");
        Assert.DoesNotContain(first.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public async Task MissingProjectReferenceProducesWarningWithoutStoppingAnalysis()
    {
        DotNetFixtureRepository repository = new();
        repository.WriteText(
            "App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="Missing/Missing.csproj" />
              </ItemGroup>
            </Project>
            """);
        repository.WriteText("Program.cs", "public sealed class Program;\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB3003");
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure);
    }

    [Fact]
    public async Task StaticAnalyzerDoesNotExecuteMsBuildTargets()
    {
        DotNetFixtureRepository repository = new();
        string markerPath = Path.Combine(repository.RootPath, "target-executed.txt");
        repository.WriteText(
            "Dangerous.csproj",
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <Target Name="Danger" BeforeTargets="Build">
                <WriteLinesToFile File="{markerPath}" Lines="target executed" />
              </Target>
            </Project>
            """);
        repository.WriteText("Program.cs", "public sealed class Program;\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        Assert.False(repository.FileExists(markerPath));
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB3000");
    }

    [Fact]
    public async Task ClassicSolutionAndWindowsProjectReferencePathsResolveStatically()
    {
        DotNetFixtureRepository repository = new();
        repository.WriteText(
            "Sample.sln",
            """
            Microsoft Visual Studio Solution File, Format Version 12.00
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "App", "src\App\App.csproj", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            """);
        repository.WriteText(
            "src/App/App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\Library\Library.csproj" />
              </ItemGroup>
            </Project>
            """);
        repository.WriteText("src/App/Program.cs", "public sealed class Program;\n");
        repository.WriteText("src/Library/Library.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        repository.WriteText("src/Library/Library.cs", "public sealed class Library;\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        EvidenceFact solution = Fact(evidence, "dotnet:solution:Sample.sln");
        Assert.Equal(1m, Measurement(solution, "resolved-projects"));
        EvidenceFact reference = Assert.Single(
            evidence.Facts,
            fact => fact.Kind == EvidenceKinds.ProjectReference);
        Assert.Contains("reference:resolved", reference.Tags);
        Assert.DoesNotContain(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB3003");
    }

    [Fact]
    public async Task ProjectRolesCoverWorkerExecutableAndLibraryShapes()
    {
        DotNetFixtureRepository repository = new();
        repository.WriteText(
            "src/Worker/Worker.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk.Worker\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n");
        repository.WriteText("src/Worker/Worker.cs", "public sealed class Worker : BackgroundService;\n");
        repository.WriteText(
            "src/Tool/Tool.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup></Project>\n");
        repository.WriteText(
            "src/Tool/Program.cs",
            "public static class Program { public static void Main() { } }\n");
        repository.WriteText(
            "src/Library/Library.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        repository.WriteText("src/Library/Service.cs", "public sealed class Service;\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        Assert.Contains(
            "project-role:worker",
            Fact(evidence, "dotnet:project:src/Worker/Worker.csproj").Tags);
        Assert.Contains(
            "project-role:executable",
            Fact(evidence, "dotnet:project:src/Tool/Tool.csproj").Tags);
        Assert.Contains(
            "project-role:library",
            Fact(evidence, "dotnet:project:src/Library/Library.csproj").Tags);
        Assert.Contains(evidence.Facts, fact => fact.Id == "dotnet:entry-point:src/Tool/Program.cs");
    }

    [Fact]
    public async Task TestArtifactsAreClassifiedAsUnitComponentIntegrationAndEndToEnd()
    {
        DotNetFixtureRepository repository = new();
        repository.WriteText(
            "tests/Shapes.Tests/Shapes.Tests.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup></Project>\n");
        repository.WriteText(
            "tests/Shapes.Tests/UnitTests.cs",
            "public sealed class UnitTests { [Fact] public void Runs() => Assert.True(true); }\n");
        repository.WriteText(
            "tests/Shapes.Tests/ComponentTests.cs",
            "public sealed class ComponentTests { [Fact] public void Renders() { IRenderedComponent<Counter> view = RenderComponent<Counter>(); } }\n");
        repository.WriteText(
            "tests/Shapes.Tests/IntegrationTests.cs",
            "public sealed class IntegrationTests : IClassFixture<WebApplicationFactory<Program>> { [Fact] public void Runs() { } }\n");
        repository.WriteText(
            "tests/Shapes.Tests/BrowserTests.cs",
            "public sealed class BrowserTests { private IPage page; [Fact] public void Runs() { } }\n");
        repository.WriteText(
            "tests/Shapes.Tests/EndToEnd/SmokeTests.cs",
            "public sealed class SmokeTests { [Fact] public void Runs() { } }\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        AssertTestType(evidence, "tests/Shapes.Tests/UnitTests.cs", "unit");
        AssertTestType(evidence, "tests/Shapes.Tests/ComponentTests.cs", "component");
        AssertTestType(evidence, "tests/Shapes.Tests/IntegrationTests.cs", "integration");
        AssertTestType(evidence, "tests/Shapes.Tests/BrowserTests.cs", "end-to-end");
        AssertTestType(evidence, "tests/Shapes.Tests/EndToEnd/SmokeTests.cs", "end-to-end");
    }

    [Fact]
    public async Task ProjectXmlRejectsDocumentTypeDeclarations()
    {
        DotNetFixtureRepository repository = new();
        repository.WriteText(
            "Unsafe.csproj",
            "<!DOCTYPE Project [<!ENTITY external SYSTEM \"file:///efforthours-should-not-read\">]><Project Sdk=\"&external;\" />\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);

        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB3001");
        Assert.DoesNotContain(evidence.Facts, fact => fact.Id == "dotnet:project:Unsafe.csproj");
    }

    [Fact]
    public async Task OutsideProjectReferencesDoNotExposeAbsolutePaths()
    {
        DotNetFixtureRepository repository = new();
        string outsidePath = Path.GetFullPath(Path.Combine(
            repository.RootPath,
            "..",
            "private-client",
            "Secret.csproj"));
        repository.WriteText(
            "App.csproj",
            $"<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup><ProjectReference Include=\"{outsidePath}\" /></ItemGroup></Project>\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);
        string json = ContractJson.Serialize(evidence);

        Assert.DoesNotContain(outsidePath, json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB3004");
        EvidenceFact reference = Assert.Single(
            evidence.Facts,
            fact => fact.Kind == EvidenceKinds.ProjectReference);
        Assert.Contains(reference.Tags, tag => tag.StartsWith("target:outside-scope-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConditionalPackageVersionsReceiveDistinctStableFactIds()
    {
        DotNetFixtureRepository repository = new();
        repository.WriteText(
            "App.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Example.Client" Version="1.0.0" Condition="'$(TargetFramework)' == 'net9.0'" />
                <PackageReference Include="Example.Client" Version="2.0.0" Condition="'$(TargetFramework)' == 'net10.0'" />
              </ItemGroup>
            </Project>
            """);

        RepositoryEvidence first = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);
        RepositoryEvidence second = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);
        EvidenceFact[] packages =
        [
            .. first.Facts.Where(fact => fact.Kind == EvidenceKinds.PackageReference),
        ];

        Assert.Equal(2, packages.Length);
        Assert.Equal(2, packages.Select(fact => fact.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.All(packages, fact => Assert.Contains(":variant-", fact.Id, StringComparison.Ordinal));
        Assert.Equal(ContractJson.Serialize(first), ContractJson.Serialize(second));
    }

    private static EvidenceFact Fact(RepositoryEvidence evidence, string id) =>
        Assert.Single(evidence.Facts, fact => fact.Id == id);

    private static decimal Measurement(EvidenceFact fact, string name) =>
        Assert.Single(fact.Measurements, measurement => measurement.Name == name).Value;

    private static void AssertTestType(
        RepositoryEvidence evidence,
        string path,
        string expectedType) =>
        Assert.Contains(
            $"test-type:{expectedType}",
            Fact(evidence, $"dotnet:test:{path}").Tags);

    internal sealed class DotNetFixtureRepository : InMemoryRepository
    {
        public static DotNetFixtureRepository Create()
        {
            DotNetFixtureRepository repository = new();
            repository.WriteText(
                "Sample.slnx",
                """
                <Solution>
                  <Project Path="src/Api/Api.csproj" />
                  <Project Path="src/Domain/Domain.csproj" />
                  <Project Path="tests/Api.Tests/Api.Tests.csproj" />
                </Solution>
                """);
            repository.WriteText(
                "Directory.Packages.props",
                """
                <Project>
                  <ItemGroup>
                    <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
                    <PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
                    <PackageVersion Include="xunit" Version="2.9.3" />
                  </ItemGroup>
                </Project>
                """);
            repository.WriteText(
                "src/Api/Api.csproj",
                """
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.EntityFrameworkCore" />
                    <ProjectReference Include="../Domain/Domain.csproj" />
                  </ItemGroup>
                </Project>
                """);
            repository.WriteText(
                "src/Domain/Domain.csproj",
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            repository.WriteText(
                "tests/Api.Tests/Api.Tests.csproj",
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <IsTestProject>true</IsTestProject>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />
                    <PackageReference Include="xunit" />
                    <ProjectReference Include="../../src/Api/Api.csproj" />
                  </ItemGroup>
                </Project>
                """);
            repository.WriteText(
                "src/Api/Program.cs",
                """
                using System.Net.Http;
                var builder = WebApplication.CreateBuilder(args);
                builder.Services.AddAuthentication().AddJwtBearer();
                builder.Services.AddAuthorization();
                builder.Services.AddHttpClient();
                builder.Services.AddHostedService<BillingWorker>();
                var app = builder.Build();
                app.UseAuthentication();
                app.UseAuthorization();
                app.MapGet("/orders", () => "fixture-secret-marker").RequireAuthorization();
                app.Run();
                """);
            repository.WriteText(
                "src/Api/OrdersController.cs",
                """
                [ApiController]
                [Route("api/orders")]
                public sealed class OrdersController : ControllerBase
                {
                    [HttpGet]
                    [Authorize]
                    public IActionResult Get() => Ok();

                    [HttpPost]
                    public async Task<IActionResult> Create() => Ok();
                }
                """);
            repository.WriteText(
                "src/Api/Data.cs",
                """
                public sealed class AppDbContext : DbContext
                {
                    public DbSet<Order> Orders { get; set; }
                }

                public sealed class InitialMigration : Migration { }

                public sealed class OrderValidator : AbstractValidator<Order>
                {
                    public OrderValidator() => RuleFor(order => order.Id);
                }
                """);
            repository.WriteText(
                "src/Api/BillingWorker.cs",
                """
                public sealed class BillingWorker : BackgroundService
                {
                    private readonly HttpClient client = new();
                    protected override Task ExecuteAsync(CancellationToken token) => client.SendAsync(new HttpRequestMessage(), token);
                }
                """);
            repository.WriteText(
                "src/Api/Pages/Index.razor",
                """
                @page "/"
                @inject HttpClient Client
                <EditForm Model="model">
                    <ValidationSummary />
                </EditForm>
                @code { private object model = new(); }
                """);
            repository.WriteText("src/Domain/Order.cs", "public sealed record Order(int Id);\n");
            repository.WriteText(
                "tests/Api.Tests/OrdersTests.cs",
                """
                public sealed class OrdersTests : IClassFixture<WebApplicationFactory<Program>>
                {
                    [Fact]
                    public void Gets_order() => Assert.True(true);

                    [Theory]
                    [InlineData(1)]
                    [InlineData(2)]
                    public void Validates_order(int id) => Assert.Equal(id, id);
                }
                """);
            return repository;
        }

    }
}
