using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

internal static class TestRepositoryEvidence
{
    public static RepositoryEvidence Create()
    {
        EvidenceProvenance provenance = new()
        {
            SourceKind = EvidenceSourceKind.Observed,
            Analyzer = "test-fixture",
            AnalyzerVersion = "1.0.0",
            Method = "synthetic unit test",
        };

        return new RepositoryEvidence
        {
            Repository = new RepositoryDescriptor
            {
                Name = "Synthetic repository",
                Scope = ".",
                Ecosystems = ["dotnet", "typescript"],
                SourceDigest = "sha256:synthetic",
            },
            Facts =
            [
                Fact("component:billing", EvidenceKinds.Component, "src/Billing", "billing component", provenance),
                Fact("integration:payments", EvidenceKinds.Integration, "src/Billing", "payment provider", provenance),
                Fact("tests:billing", EvidenceKinds.TestSuite, "tests/Billing.Tests", "billing unit tests", provenance, "unit"),
                Fact("documentation:readme", EvidenceKinds.Documentation, ".", "repository onboarding", provenance),
                Fact("build:solution", EvidenceKinds.BuildConfiguration, ".", "solution build", provenance),
            ],
        };
    }

    public static EvidenceFact Fact(
        string id,
        string kind,
        string scope,
        string summary,
        EvidenceProvenance provenance,
        params string[] tags)
    {
        return new EvidenceFact
        {
            Id = id,
            Kind = kind,
            Scope = scope,
            Summary = summary,
            Provenance = provenance,
            Locations = [new EvidenceLocation { Path = scope == "." ? "README.md" : $"{scope}/sample.cs" }],
            Tags = tags.Length == 0 ? ["complexity:moderate"] : tags,
        };
    }

    public static RepositoryEvidence CreateStructuredDotNet(
        int sourceCopies = 1,
        int endpoints = 0,
        int testCases = 0,
        decimal? declaredCoverage = null,
        bool includeDocumentation = true,
        bool includeCi = true,
        bool includeGeneratedFile = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceCopies);

        EvidenceProvenance measured = Provenance(EvidenceSourceKind.Measured, "synthetic measurement");
        EvidenceProvenance observed = Provenance(EvidenceSourceKind.Observed, "synthetic observation");
        EvidenceProvenance inferred = Provenance(EvidenceSourceKind.Inferred, "synthetic inference");
        EvidenceProvenance declared = Provenance(EvidenceSourceKind.DeclaredAssumed, "synthetic declaration");
        const string projectPath = "src/App/App.csproj";
        List<EvidenceFact> facts =
        [
            new EvidenceFact
            {
                Id = "repository:inventory",
                Kind = EvidenceKinds.RepositoryInventory,
                Scope = ".",
                Summary = "Synthetic repository inventory.",
                Provenance = measured,
                Measurements =
                [
                    new EvidenceMeasurement { Name = "included-files", Value = sourceCopies, Unit = "files" },
                ],
                Tags = ["history:not-inspected"],
            },
            new EvidenceFact
            {
                Id = $"dotnet:project:{projectPath}",
                Kind = EvidenceKinds.DotNetProject,
                Scope = projectPath,
                Summary = "Synthetic .NET web project.",
                Provenance = inferred,
                Locations = [new EvidenceLocation { Path = projectPath }],
                Measurements =
                [
                    new EvidenceMeasurement { Name = "target-frameworks", Value = 1m, Unit = "frameworks" },
                    new EvidenceMeasurement { Name = "packages", Value = 4m, Unit = "references" },
                    new EvidenceMeasurement { Name = "project-references", Value = 0m, Unit = "references" },
                    new EvidenceMeasurement { Name = "unresolved-msbuild-values", Value = 0m, Unit = "values" },
                ],
                Tags = ["project-role:web", "target-framework:net10.0"],
            },
        ];

        for (int index = 0; index < sourceCopies; index++)
        {
            facts.Add(FileFact(
                $"src/App/Feature{index + 1}.cs",
                "source",
                new string('a', 64),
                measured,
                "language:csharp",
                "ecosystem:dotnet"));
        }

        if (testCases > 0)
        {
            facts.Add(FileFact(
                "src/App/FeatureTests.cs",
                "test",
                new string('b', 64),
                measured,
                "classification:test",
                "language:csharp",
                "ecosystem:dotnet"));
        }

        int analyzedFiles = sourceCopies + (testCases > 0 ? 1 : 0);
        facts.Add(new EvidenceFact
        {
            Id = $"dotnet:source-structure:{projectPath}",
            Kind = EvidenceKinds.SourceStructure,
            Scope = projectPath,
            Summary = "Synthetic C# source structure.",
            Provenance = measured,
            Measurements =
            [
                new EvidenceMeasurement { Name = "files", Value = analyzedFiles, Unit = "files" },
                new EvidenceMeasurement { Name = "types", Value = sourceCopies * 4m + (testCases > 0 ? 2m : 0m), Unit = "types" },
                new EvidenceMeasurement { Name = "public-types", Value = sourceCopies, Unit = "types" },
                new EvidenceMeasurement { Name = "methods", Value = sourceCopies * 20m + testCases, Unit = "methods" },
                new EvidenceMeasurement { Name = "public-methods", Value = sourceCopies * 4m, Unit = "methods" },
                new EvidenceMeasurement { Name = "async-methods", Value = sourceCopies * 2m, Unit = "methods" },
                new EvidenceMeasurement { Name = "branch-points", Value = sourceCopies * 8m + testCases / 2m, Unit = "nodes" },
            ],
        });

        if (endpoints > 0)
        {
            for (int index = 0; index < sourceCopies; index++)
            {
                string path = $"src/App/Feature{index + 1}.cs";
                facts.Add(new EvidenceFact
                {
                    Id = $"dotnet:api:{path}",
                    Kind = EvidenceKinds.ApiSurface,
                    Scope = projectPath,
                    Summary = "Synthetic API endpoint surface.",
                    Provenance = observed,
                    Locations = [new EvidenceLocation { Path = path, Line = 1 }],
                    Measurements =
                    [
                        new EvidenceMeasurement { Name = "minimal-api-endpoints", Value = endpoints, Unit = "endpoints" },
                        new EvidenceMeasurement { Name = "controllers", Value = 0m, Unit = "types" },
                        new EvidenceMeasurement { Name = "route-groups", Value = 1m, Unit = "groups" },
                    ],
                    Tags = ["http-method:get"],
                });
            }
        }

        if (testCases > 0)
        {
            facts.Add(new EvidenceFact
            {
                Id = "dotnet:test:src/App/FeatureTests.cs",
                Kind = EvidenceKinds.DotNetTest,
                Scope = projectPath,
                Summary = "Synthetic unit tests.",
                Provenance = inferred,
                Locations = [new EvidenceLocation { Path = "src/App/FeatureTests.cs", Line = 1 }],
                Measurements =
                [
                    new EvidenceMeasurement { Name = "test-methods", Value = testCases, Unit = "methods" },
                    new EvidenceMeasurement { Name = "parameterized-cases", Value = testCases / 4m, Unit = "cases" },
                    new EvidenceMeasurement { Name = "assertions", Value = testCases * 2m, Unit = "calls" },
                    new EvidenceMeasurement { Name = "mock-usages", Value = 2m, Unit = "usages" },
                ],
                Tags = ["test-type:unit"],
            });
        }

        if (declaredCoverage is not null)
        {
            facts.Add(new EvidenceFact
            {
                Id = "dotnet:coverage:declared",
                Kind = EvidenceKinds.Coverage,
                Scope = projectPath,
                Summary = "Synthetic declared coverage threshold.",
                Provenance = declared,
                Locations = [new EvidenceLocation { Path = projectPath }],
                Measurements =
                [
                    new EvidenceMeasurement { Name = "lines", Value = declaredCoverage.Value, Unit = "percent" },
                    new EvidenceMeasurement { Name = "branches", Value = declaredCoverage.Value, Unit = "percent" },
                ],
                Tags = ["coverage:declared-and-assumed"],
            });
        }

        if (includeDocumentation)
        {
            facts.Add(FileFact(
                "README.md",
                "documentation",
                new string('d', 64),
                measured));
        }

        if (includeCi)
        {
            facts.Add(FileFact(
                ".github/workflows/build.yml",
                "ci-configuration",
                new string('c', 64),
                measured));
        }

        if (includeGeneratedFile)
        {
            facts.Add(FileFact(
                "src/App/Generated.g.cs",
                "source",
                new string('e', 64),
                measured,
                "classification:generated",
                "language:csharp",
                "ecosystem:dotnet"));
        }

        return new RepositoryEvidence
        {
            Repository = new RepositoryDescriptor
            {
                Name = "Structured synthetic repository",
                Scope = ".",
                Ecosystems = ["dotnet"],
                SourceDigest = "sha256:structured-synthetic",
            },
            Facts = facts,
        };
    }

    public static RepositoryEvidence CreateStructuredTypeScript(int sourceCopies = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceCopies);

        EvidenceProvenance measured = Provenance(EvidenceSourceKind.Measured, "synthetic measurement");
        EvidenceProvenance inferred = Provenance(EvidenceSourceKind.Inferred, "synthetic inference");
        List<EvidenceFact> facts =
        [
            new EvidenceFact
            {
                Id = "javascript:package:package.json",
                Kind = EvidenceKinds.JavaScriptPackage,
                Scope = ".",
                Summary = "Synthetic TypeScript package.",
                Provenance = inferred,
                Locations = [new EvidenceLocation { Path = "package.json" }],
                Measurements =
                [
                    new EvidenceMeasurement { Name = "dependencies", Value = 0m, Unit = "references" },
                    new EvidenceMeasurement { Name = "scripts", Value = 0m, Unit = "scripts" },
                    new EvidenceMeasurement { Name = "workspace-patterns", Value = 0m, Unit = "patterns" },
                ],
                Tags = ["package-role:package", "package:private"],
            },
        ];

        for (int index = 0; index < sourceCopies; index++)
        {
            facts.Add(FileFact(
                $"src/status-{index + 1}.ts",
                "source",
                new string('t', 64),
                measured,
                "language:typescript",
                "ecosystem:typescript"));
        }

        facts.Add(new EvidenceFact
        {
            Id = "javascript:source-structure:.",
            Kind = EvidenceKinds.SourceStructure,
            Scope = ".",
            Summary = "Synthetic TypeScript source structure.",
            Provenance = measured,
            Locations = [new EvidenceLocation { Path = "src/status-1.ts" }],
            Measurements =
            [
                new EvidenceMeasurement { Name = "files", Value = sourceCopies, Unit = "files" },
                new EvidenceMeasurement { Name = "token-backed-files", Value = sourceCopies, Unit = "files" },
                new EvidenceMeasurement { Name = "functions", Value = sourceCopies, Unit = "functions" },
                new EvidenceMeasurement { Name = "interfaces", Value = sourceCopies, Unit = "interfaces" },
                new EvidenceMeasurement { Name = "exports", Value = sourceCopies * 2m, Unit = "exports" },
            ],
            Tags = ["syntax:token-backed"],
        });

        return new RepositoryEvidence
        {
            Repository = new RepositoryDescriptor
            {
                Name = "Structured synthetic TypeScript repository",
                Scope = ".",
                Ecosystems = ["typescript"],
                SourceDigest = $"sha256:structured-typescript-{sourceCopies}",
            },
            Facts = facts,
        };
    }

    private static EvidenceFact FileFact(
        string path,
        string role,
        string sha256,
        EvidenceProvenance provenance,
        params string[] additionalTags) => new()
        {
            Id = $"file:{path}",
            Kind = EvidenceKinds.File,
            Scope = path,
            Summary = $"Synthetic {role} file.",
            Provenance = provenance,
            Locations = [new EvidenceLocation { Path = path }],
            Measurements =
        [
            new EvidenceMeasurement { Name = "bytes", Value = 1_000m, Unit = "bytes" },
            new EvidenceMeasurement { Name = "physical-lines", Value = 100m, Unit = "lines" },
        ],
            Tags =
        [
            $"role:{role}",
            "content:text",
            $"sha256:{sha256}",
            $"extension:{Path.GetExtension(path).ToLowerInvariant()}",
            .. additionalTags,
        ],
        };

    private static EvidenceProvenance Provenance(
        EvidenceSourceKind sourceKind,
        string method) => new()
        {
            SourceKind = sourceKind,
            Analyzer = "test-fixture",
            AnalyzerVersion = "1.0.0",
            Method = method,
        };
}
