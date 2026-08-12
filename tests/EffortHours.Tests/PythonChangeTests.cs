using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class PythonChangeTests
{
    private const string PyProject =
        "[project]\nname = \"python-change\"\nversion = \"1.0.0\"\n";

    [Fact]
    public async Task FormattingAndCommentOnlyPythonChangeHasZeroEffort()
    {
        ChangeState before = State(
            ("pyproject.toml", PyProject),
            ("app.py", "def greet(name):\n    # old explanation\n    return f'Hello, {name}'\n"));
        ChangeState after = State(
            ("pyproject.toml", PyProject),
            ("app.py", "def greet( name ):\n  # rewritten explanation\n  return f'Hello, {name}'\n\n"));

        ChangeEstimateReport report = await EstimateAsync(before, after);
        ChangePathEvidence path = Assert.Single(report.Evidence.Paths);

        Assert.Equal(ChangePathClassification.FormattingOnly, path.Classification);
        Assert.False(path.Represented);
        Assert.Equal(0m, report.TotalEffort.Expected);
        Assert.Empty(report.WorkItems);
    }

    [Fact]
    public async Task PythonDedentRemainsAMeaningfulChange()
    {
        ChangeState before = State(
            ("pyproject.toml", PyProject),
            ("app.py", "def value(enabled):\n    if enabled:\n        return 1\n    return 0\n"));
        ChangeState after = State(
            ("pyproject.toml", PyProject),
            ("app.py", "def value(enabled):\n    if enabled:\n        return 1\nreturn 0\n"));

        ChangeEstimateReport report = await EstimateAsync(before, after);

        Assert.True(Assert.Single(report.Evidence.Paths).Represented);
        Assert.True(report.TotalEffort.Expected > 0m);
        Assert.Contains(report.WorkItems, item =>
            item.Category == EffortCategory.ProductionImplementation);
    }

    [Theory]
    [MemberData(nameof(SemanticCases))]
    public async Task ImportQualifiedPythonChangesReachTheirIntendedCategory(
        string beforeSource,
        string afterSource,
        EffortCategory category)
    {
        ChangeEstimateReport report = await EstimateAsync(
            State(("pyproject.toml", PyProject), ("app.py", beforeSource)),
            State(("pyproject.toml", PyProject), ("app.py", afterSource)));

        CategoryEstimate estimate = Assert.Single(
            report.Categories,
            candidate => candidate.Category == category);
        Assert.True(estimate.Hours.Expected > 0m);
        Assert.Contains(report.WorkItems, item => item.Category == category);
        Assert.Equal("change-seed/0.17.0+seed-rules/0.4.0", report.EstimatorVersion);
    }

    [Fact]
    public async Task AddingPythonTestsProducesTestEffort()
    {
        ChangeState before = State(
            ("pyproject.toml", PyProject),
            ("app.py", "def total(values):\n    return sum(values)\n"));
        ChangeState after = State(
            ("pyproject.toml", PyProject),
            ("app.py", "def total(values):\n    return sum(values)\n"),
            ("tests/test_app.py", "from app import total\n\ndef test_total():\n    assert total([1, 2]) == 3\n"));

        ChangeEstimateReport report = await EstimateAsync(before, after);

        Assert.Contains(report.Categories, category =>
            category.Category == EffortCategory.UnitTesting && category.Hours.Expected > 0m);
    }

    public static TheoryData<string, string, EffortCategory> SemanticCases => new()
    {
        {
            "from fastapi import FastAPI\napp = FastAPI()\ndef health():\n    return True\n",
            "from fastapi import FastAPI\napp = FastAPI()\n@app.get('/health')\ndef health():\n    return True\n",
            EffortCategory.ProductionImplementation
        },
        {
            "import httpx\ndef load():\n    return None\n",
            "import httpx\ndef load():\n    return httpx.get('https://example.invalid')\n",
            EffortCategory.ExternalIntegrationsAndProtocols
        },
        {
            "from sqlalchemy import select\ndef load():\n    return None\n",
            "from sqlalchemy import select\ndef load():\n    return select(Order)\n",
            EffortCategory.DataModelingPersistenceAndMigrations
        },
        {
            "from jose import jwt\ndef read():\n    return None\n",
            "from jose import jwt\ndef read():\n    return jwt.decode('token', 'key')\n",
            EffortCategory.SecurityAndAccessibility
        },
        {
            "from celery import Celery\napp = Celery('jobs')\ndef refresh():\n    return None\n",
            "from celery import Celery\napp = Celery('jobs')\n@app.task\ndef refresh():\n    return None\n",
            EffortCategory.ProductionImplementation
        },
    };

    private static Task<ChangeEstimateReport> EstimateAsync(
        ChangeState before,
        ChangeState after) => new ChangeEstimator().EstimateAsync(
            new ChangeEstimateInput
            {
                RepositoryName = "in-memory-python-change",
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
