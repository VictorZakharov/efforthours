using EffortHours.Contracts.V1;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed partial class PythonAnalyzerTests
{
    [Fact]
    public async Task AdditionalQualifiedFrameworkFamiliesMapConservatively()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "app.py",
            """
            from flask import Flask
            from django.urls import path
            from django.db import models
            from alembic import op
            import requests
            import boto3
            from rq import Queue
            import argparse
            import click
            import typer

            flask_app = Flask(__name__)
            queue = Queue()
            typer_app = typer.Typer()
            parser = argparse.ArgumentParser()

            class Record(models.Model):
                pass

            @flask_app.route("/status")
            def status():
                op.create_table("status")
                requests.get("https://example.invalid")
                boto3.client("s3")
                queue.enqueue(refresh)
                return path("status", refresh)

            @click.command()
            def click_main():
                return None

            @typer_app.command()
            def typer_main():
                return None
            """);

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ApiSurface &&
            fact.Tags.Contains("technology:flask", StringComparer.Ordinal) &&
            fact.Tags.Contains("technology:django", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.DataAccess &&
            fact.Tags.Contains("technology:alembic", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.Integration &&
            fact.Tags.Contains("technology:requests", StringComparer.Ordinal) &&
            fact.Tags.Contains("technology:boto3", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.BackgroundWork &&
            fact.Tags.Contains("technology:rq", StringComparer.Ordinal));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.EntryPoint &&
            fact.Tags.Contains("technology:argparse", StringComparer.Ordinal) &&
            fact.Tags.Contains("technology:click", StringComparer.Ordinal) &&
            fact.Tags.Contains("technology:typer", StringComparer.Ordinal));
    }

    [Fact]
    public async Task NestedFunctionsAreNotCountedAsMethodsOrPublicSymbols()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "app.py",
            """
            class Service:
                def execute(self):
                    def helper():
                        return 1
                    return helper()

            def outer():
                def inner():
                    return 2
                return inner()
            """);

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact structure = FactOfKind(evidence, EvidenceKinds.SourceStructure);

        Assert.Equal(1m, Measurement(structure, "methods"));
        Assert.Equal(3m, Measurement(structure, "functions"));
        Assert.Equal(3m, Measurement(structure, "public-symbols"));
    }

    [Fact]
    public async Task PythonParticipatesInMixedDotNetJavaScriptRepositoryOwnership()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "src/App/App.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        repository.WriteText("src/App/Service.cs", "public sealed class Service { public int Value() => 1; }");
        repository.WriteText("web/package.json", "{\"name\":\"web\",\"version\":\"1.0.0\"}");
        repository.WriteText("web/src/index.js", "export function value() { return 1; }");
        repository.WriteText(
            "services/orders/pyproject.toml",
            "[project]\nname = \"orders\"\nversion = \"1.0.0\"\n");
        repository.WriteText("services/orders/orders/__init__.py", "def value():\n    return 1\n");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EstimateReport estimate = new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);

        Assert.Contains("dotnet", evidence.Repository.Ecosystems);
        Assert.Contains("javascript", evidence.Repository.Ecosystems);
        Assert.Contains("python", evidence.Repository.Ecosystems);
        Assert.Contains(estimate.WorkItems, item => item.Estimator.Id == "seed-rule:dotnet-source-backbone");
        Assert.Contains(estimate.WorkItems, item => item.Estimator.Id == "seed-rule:javascript-source-backbone");
        Assert.Contains(estimate.WorkItems, item => item.Estimator.Id == "seed-rule:polyglot-source-backbone");
        Assert.DoesNotContain(estimate.Diagnostics, diagnostic => diagnostic.Code is "FB1001" or "FB1002");
    }
}
