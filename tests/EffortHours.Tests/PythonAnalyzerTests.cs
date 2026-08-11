using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed partial class PythonAnalyzerTests
{
    private const string PyProject =
        """
        [project]
        name = "orders-api"
        version = "1.0.0"
        dependencies = [
          "fastapi>=0.100",
          "pydantic>=2",
          "sqlalchemy>=2",
          "httpx>=0.27",
          "celery>=5",
          "python-jose>=3"
        ]

        [project.scripts]
        orders = "orders.cli:main"
        """;

    [Fact]
    public async Task PackageStructureFrameworksAndTestsProduceTraceableEvidenceAndEffort()
    {
        InMemoryRepository repository = new();
        repository.WriteText("pyproject.toml", PyProject);
        repository.WriteText(
            "orders/api.py",
            """
            from fastapi import FastAPI
            from pydantic import BaseModel, Field
            from sqlalchemy.orm import DeclarativeBase
            import httpx
            from celery import Celery
            from jose import jwt

            app = FastAPI()
            worker = Celery("orders")

            class Base(DeclarativeBase):
                pass

            class OrderInput(BaseModel):
                quantity: int = Field(gt=0)

            @app.get("/orders/{order_id}")
            async def get_order(order_id: int) -> dict:
                if order_id <= 0:
                    raise ValueError("invalid")
                async with httpx.AsyncClient() as client:
                    await client.get("https://example.invalid")
                return jwt.decode("token", "key", algorithms=["HS256"])

            @worker.task
            def refresh_orders() -> None:
                return None
            """);
        repository.WriteText(
            "tests/test_api.py",
            """
            import pytest
            from unittest.mock import patch

            @pytest.mark.parametrize("order_id", [1, 2])
            def test_order(order_id):
                with patch("orders.api.get_order"):
                    assert order_id > 0
            """);

        RepositoryEvidence evidence = await ScanAsync(repository);
        EstimateReport estimate = new SeedEstimator().Estimate(
            evidence,
            EstimationProfile.Implementation);
        EvidenceFact package = FactOfKind(evidence, EvidenceKinds.EcosystemPackage);
        EvidenceFact structure = FactOfKind(evidence, EvidenceKinds.SourceStructure);
        EvidenceFact test = FactOfKind(evidence, EvidenceKinds.EcosystemTest);

        Assert.Equal("0.1.0", package.Provenance.AnalyzerVersion);
        Assert.Equal(".", package.Scope);
        Assert.Contains("package-role:server", package.Tags);
        Assert.Contains("syntax:token-backed", structure.Tags);
        Assert.Contains("parser-confidence:medium", structure.Tags);
        Assert.True(Measurement(structure, "functions") >= 1m);
        Assert.True(Measurement(structure, "methods") >= 0m);
        Assert.True(Measurement(structure, "types") >= 2m);
        Assert.True(Measurement(structure, "async-units") >= 1m);
        Assert.True(Measurement(structure, "branch-points") >= 1m);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ApiSurface &&
            fact.Tags.Contains("technology:fastapi", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.DataAccess &&
            fact.Tags.Contains("technology:sqlalchemy", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.Integration &&
            fact.Tags.Contains("technology:httpx", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.SecurityConfiguration);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.BackgroundWork &&
            fact.Tags.Contains("technology:celery", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.Validation &&
            fact.Tags.Contains("technology:pydantic", StringComparer.Ordinal));
        Assert.Equal(1m, Measurement(test, "test-cases"));
        Assert.Equal(1m, Measurement(test, "parameterized-cases"));
        Assert.Equal(1m, Measurement(test, "assertions"));
        Assert.Contains("analysis-status:analyzed", FactOfKind(evidence, EvidenceKinds.Language).Tags);
        Assert.Contains("analysis-depth:token-backed", FactOfKind(evidence, EvidenceKinds.Language).Tags);
        Assert.DoesNotContain(estimate.Diagnostics, diagnostic => diagnostic.Code == "FB1002");
        Assert.Contains(estimate.WorkItems, item => item.Estimator.Id == "seed-rule:polyglot-source-backbone");
        Assert.Contains(estimate.WorkItems, item => item.Category == EffortCategory.UnitTesting);
        Assert.Empty(ContractValidation.Validate(evidence));
        Assert.Empty(ContractValidation.Validate(estimate));
    }

    [Fact]
    public async Task FrameworkNamesWithoutImportsDoNotProduceSemanticEvidence()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "app.py",
            """
            class FastAPI:
                def get(self, route):
                    return route

            class requests:
                def get(self, route):
                    return route

            class Celery:
                def task(self, function):
                    return function

            app = FastAPI()
            worker = Celery()

            @app.get("/not-a-route")
            @worker.task
            def local_only():
                return requests().get("local")
            """);

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.DoesNotContain(evidence.Facts, fact => fact.Kind is
            EvidenceKinds.ApiSurface or EvidenceKinds.Integration or EvidenceKinds.BackgroundWork);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure);
    }

    [Fact]
    public async Task SetupPyIsReadLiterallyButNeverExecutedAndPrivateContentIsNotEmitted()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "setup.py",
            """
            from setuptools import setup
            raise RuntimeError("private-python-marker")
            setup(
                long_description="not-the-package-name",
                name="safe-package",
                install_requires=[
                    "requests>=2",
                    "httpx>=0.27",
                ],
            )
            """);
        repository.WriteText("requirements-dev.in", "pytest>=8\n");
        repository.WriteText("safe_package/__init__.py", "def value():\n    return 1\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        string json = ContractJson.Serialize(evidence);
        EvidenceFact package = FactOfKind(evidence, EvidenceKinds.EcosystemPackage);

        Assert.Contains("setup-py:not-executed", package.Tags);
        Assert.Contains("'safe-package'", package.Summary, StringComparison.Ordinal);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.PackageReference &&
            fact.Tags.Contains("dependency:requests", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.PackageReference &&
            fact.Tags.Contains("dependency:httpx", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.PackageReference &&
            fact.Tags.Contains("dependency:pytest", StringComparer.Ordinal));
        Assert.DoesNotContain("private-python-marker", json, StringComparison.Ordinal);
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB7000");
    }

    [Fact]
    public async Task UnsupportedMaintainedLanguageIsInventoryOnlyWhilePythonIsAnalyzed()
    {
        InMemoryRepository repository = new();
        repository.WriteText("main.py", "def run():\n    return 1\n");
        repository.WriteText("Main.kt", "class Main\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact python = evidence.Facts.Single(fact => fact.Kind == EvidenceKinds.Language &&
            fact.Tags.Contains("language:python", StringComparer.Ordinal));
        EvidenceFact kotlin = evidence.Facts.Single(fact => fact.Kind == EvidenceKinds.Language &&
            fact.Tags.Contains("language:kotlin", StringComparer.Ordinal));
        EstimateReport estimate = new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);

        Assert.Contains("analysis-status:analyzed", python.Tags);
        Assert.Contains("analysis-status:inventory-only", kotlin.Tags);
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB2002" &&
            diagnostic.Message.Contains("kotlin", StringComparison.Ordinal));
        Assert.Contains(estimate.Diagnostics, diagnostic => diagnostic.Code == "FB1002" &&
            diagnostic.Message.Contains("kotlin", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExactDuplicatePythonBodiesDoNotIncreaseEstimatedEffort()
    {
        const string source =
            "from fastapi import FastAPI\napp = FastAPI()\n@app.get('/health')\ndef health():\n    return {'ok': True}\n";
        EstimateReport single = await EstimateAsync(("app.py", source));
        EstimateReport duplicate = await EstimateAsync(("app.py", source), ("copy.py", source));

        Assert.Equal(single.TotalEffort.Expected, duplicate.TotalEffort.Expected);
        Assert.Equal(
            Category(single, EffortCategory.ProductionImplementation).Expected,
            Category(duplicate, EffortCategory.ProductionImplementation).Expected);
        Assert.Contains(duplicate.Diagnostics, diagnostic => diagnostic.Code == "FB1003");
    }

    [Fact]
    public async Task PythonCachesAndVirtualEnvironmentsAreExcludedBeforeSemanticAnalysis()
    {
        InMemoryRepository repository = new();
        repository.WriteText("app.py", "def run():\n    return 1\n");
        repository.WriteText(".venv/Lib/site-packages/vendor.py", "def huge():\n    return 1\n");
        repository.WriteText("__pycache__/compiled.py", "def generated():\n    return 1\n");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.DoesNotContain(evidence.Facts, fact =>
            (fact.Kind == EvidenceKinds.File ||
             fact.Provenance.Analyzer == "efforthours.python-analyzer") &&
            fact.Locations.Any(location =>
                location.Path.Contains("site-packages", StringComparison.Ordinal) ||
                location.Path.Contains("__pycache__", StringComparison.Ordinal)));
        Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure);
    }

    [Fact]
    public async Task NestedPythonPackagesHaveDeepestOwnershipAndLocalImportEdges()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "packages/domain/pyproject.toml",
            "[project]\nname = \"order-domain\"\nversion = \"1.0.0\"\n");
        repository.WriteText(
            "packages/domain/order_domain/model.py",
            "class Order:\n    pass\n");
        repository.WriteText(
            "packages/api/pyproject.toml",
            "[project]\nname = \"order-api\"\nversion = \"1.0.0\"\n");
        repository.WriteText(
            "packages/api/order_api/app.py",
            "from order_domain import model\n\ndef load():\n    return model.Order()\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact[] packages = [.. evidence.Facts
            .Where(fact => fact.Kind == EvidenceKinds.EcosystemPackage)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        EvidenceFact reference = Assert.Single(
            evidence.Facts,
            fact => fact.Kind == EvidenceKinds.ProjectReference &&
                fact.Provenance.Analyzer == "efforthours.python-analyzer");

        Assert.Equal(["packages/api", "packages/domain"], packages.Select(fact => fact.Scope));
        Assert.Equal("packages/api", reference.Scope);
        Assert.Contains("target-scope:packages/domain", reference.Tags);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure &&
            fact.Scope == "packages/api");
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure &&
            fact.Scope == "packages/domain");
    }

    private static async Task<RepositoryEvidence> ScanAsync(InMemoryRepository repository) =>
        await new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);

    private static async Task<EstimateReport> EstimateAsync(
        params (string Path, string Content)[] files)
    {
        InMemoryRepository repository = new();
        foreach ((string path, string content) in files) repository.WriteText(path, content);
        RepositoryEvidence evidence = await ScanAsync(repository);
        return new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);
    }

    private static EvidenceFact FactOfKind(RepositoryEvidence evidence, string kind) =>
        Assert.Single(evidence.Facts, fact => fact.Kind == kind);

    private static decimal Measurement(EvidenceFact fact, string name) =>
        Assert.Single(fact.Measurements, measurement => measurement.Name == name).Value;

    private static EffortRange Category(EstimateReport report, EffortCategory category) =>
        report.Categories.SingleOrDefault(item => item.Category == category)?.Hours ?? new EffortRange
        {
            Low = 0m,
            Expected = 0m,
            High = 0m,
        };
}
