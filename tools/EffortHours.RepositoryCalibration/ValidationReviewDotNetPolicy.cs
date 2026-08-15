using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static partial class DevelopmentReviewPolicy
{
    private static ReviewJudgment ConsoleAppFrameworkExpected(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence)
    {
        string kind = Kind(target);
        bool benchmark = target.Scope.Contains(
            "CliFrameworkBenchmark",
            StringComparison.Ordinal);
        if (benchmark &&
            kind is "application-entry-point" or "dotnet-source-backbone" or "ui-surface")
        {
            return Exclude(
                "the CLI comparison benchmark is performance-validation support rather than a separate shipped command application.");
        }

        decimal supportingFactor = target.Scope.StartsWith("sandbox/", StringComparison.Ordinal)
            ? 0.65m
            : 1m;
        decimal expected = kind switch
        {
            "specification-comprehension" => 20m,
            "solution-coordination" => 10m,
            "self-review" => 24m,
            "build-tooling" => 8m,
            "ci-infrastructure" => 5m,
            "container-deployment" => 2m,
            "documentation" => 60m,
            "packaging-release" => 5m,
            "application-entry-point" => EntryPoint(target, evidence, supportingFactor),
            "architecture-design" => Architecture(target, supportingFactor, 12m),
            "dotnet-source-backbone" => DotNetSource(
                target,
                evidence,
                supportingFactor * ConsoleAppSourceFactor(target.Scope)),
            "manual-validation" => ManualValidation(target, supportingFactor, 12m),
            "project-setup" => ProjectSetup(target, supportingFactor),
            "ui-surface" => Clamp(
                0.75m + (evidence.Sum(target, "commands") * 0.5m),
                0.75m,
                12m),
            "unit-tests" => TestEffort(target, evidence, 1.4m),
            _ => throw Unknown(target),
        };
        return Keep(expected);
    }

    private static decimal ConsoleAppSourceFactor(string scope) => scope switch
    {
        "src/ConsoleAppFramework/ConsoleAppFramework.csproj" => 1.5m,
        "src/ConsoleAppFramework.CliSchema/ConsoleAppFramework.CliSchema.csproj" => 1.2m,
        "src/ConsoleAppFramework.Abstractions/ConsoleAppFramework.Abstractions.csproj" => 1.1m,
        _ => 1m,
    };

    private static ReviewJudgment SpectreConsoleExpected(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence)
    {
        string kind = Kind(target);
        bool benchmark = IsBenchmarkScope(target.Scope);
        if ((kind is "application-entry-point" or "dotnet-source-backbone") &&
            (benchmark || target.Scope == "."))
        {
            return Exclude(
                benchmark
                    ? "benchmark entry/source is performance-validation support rather than production implementation."
                    : "root entry/source evidence belongs to repository build scripts already represented by build tooling.");
        }

        if (kind == "validation-surface")
        {
            return Exclude(
                "the validation signal belongs to repository PowerShell tooling and is already represented by build-tooling work.");
        }

        decimal expected = kind switch
        {
            "specification-comprehension" => 30m,
            "solution-coordination" => 16m,
            "self-review" => 36m,
            "build-tooling" => 14m,
            "ci-infrastructure" => 6m,
            "documentation" => 100m,
            "packaging-release" => 8m,
            "architecture-design" => Architecture(target, 1m, 16m),
            "dotnet-source-backbone" => DotNetSource(
                target,
                evidence,
                SpectreSourceFactor(target.Scope)),
            "manual-validation" => ManualValidation(target, 1m, 16m),
            "polyglot-source-backbone" => PolyglotSource(target, evidence, 0.7m),
            "project-setup" => ProjectSetup(target, IsTestScope(target.Scope) ? 0.7m : 1m),
            "unit-tests" => TestEffort(target, evidence, 1.35m),
            _ => throw Unknown(target),
        };
        return Keep(expected);
    }

    private static decimal SpectreSourceFactor(string scope) => scope switch
    {
        "src/Spectre.Console/Spectre.Console.csproj" => 1.45m,
        "src/Spectre.Console.Ansi/Spectre.Console.Ansi.csproj" => 1.3m,
        "src/Spectre.Console.SourceGenerator/Spectre.Console.SourceGenerator.csproj" => 1.3m,
        "src/Extensions/Spectre.Console.Json/Spectre.Console.Json.csproj" => 1.1m,
        "src/Extensions/Spectre.Console.ImageSharp/Spectre.Console.ImageSharp.csproj" => 1.1m,
        _ => 1m,
    };

    private static ReviewJudgment EfCoreExpected(
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence)
    {
        string kind = Kind(target);
        bool testScope = IsTestScope(target.Scope);
        bool benchmark = IsBenchmarkScope(target.Scope);
        bool engineering = target.Scope.StartsWith("eng/", StringComparison.Ordinal);
        if (kind == "api-surface")
        {
            return Exclude(
                "all route/controller evidence belongs to functional-test applications and is already represented by test authoring.");
        }

        if ((kind is "data-persistence" or "external-integration" or
             "ui-surface" or "validation-surface") &&
            (testScope || benchmark))
        {
            return Exclude(
                "the specialized semantic evidence belongs to tests or benchmarks and is already represented by test/validation authoring.");
        }

        if (kind == "application-entry-point" &&
            (testScope ||
             benchmark ||
             engineering ||
             target.Scope == "." ||
             target.Scope == "src/EFCore/EFCore.csproj"))
        {
            return Exclude(
                "the entry-like symbol is a test, benchmark, engineering script/tool, or false main-pattern match already represented by another retained capability.");
        }

        if (kind == "dotnet-source-backbone" &&
            (benchmark || engineering || target.Scope == "."))
        {
            return Exclude(
                "benchmark or engineering-tool source is retained through build/performance validation rather than as a separate product component.");
        }

        if (kind == "data-persistence" && target.Scope == ".")
        {
            return Exclude(
                "the root SQL cleanup utility is test/build support and not a product persistence surface.");
        }

        if (kind == "ui-surface" && engineering)
        {
            return Exclude(
                "ApiChief commands are engineering compatibility tooling already represented by build and review work.");
        }

        if (kind == "validation-surface" && target.Scope == ".")
        {
            return Exclude(
                "root shell validation is part of repository build tooling rather than product input validation.");
        }

        decimal supportingFactor = testScope
            ? 0.6m
            : benchmark
                ? 0.5m
                : engineering
                    ? 0.7m
                    : 1m;
        decimal expected = kind switch
        {
            "specification-comprehension" => 120m,
            "solution-coordination" => 100m,
            "self-review" => 160m,
            "build-tooling" => 80m,
            "ci-infrastructure" => 25m,
            "container-deployment" => 8m,
            "documentation" => 220m,
            "packaging-release" => 30m,
            "application-entry-point" => EntryPoint(target, evidence, 1.2m),
            "architecture-design" => Architecture(
                target,
                supportingFactor * EfCoreDesignFactor(target.Scope),
                24m),
            "data-persistence" => DataEffort(
                target,
                evidence,
                EfCoreDataFactor(target.Scope)),
            "dotnet-source-backbone" => DotNetSource(
                target,
                evidence,
                EfCoreSourceFactor(target.Scope)),
            "end-to-end-tests" => TestEffort(target, evidence, 1.35m),
            "external-integration" => IntegrationEffort(target, evidence, 1.4m),
            "integration-tests" => TestEffort(target, evidence, 1.25m),
            "manual-validation" => ManualValidation(
                target,
                supportingFactor * EfCoreDesignFactor(target.Scope),
                24m),
            "polyglot-source-backbone" => PolyglotSource(target, evidence, 0.7m),
            "project-setup" => ProjectSetup(target, supportingFactor),
            "ui-surface" => Clamp(
                1m + (evidence.Sum(target, "commands") * 0.6m),
                1m,
                16m),
            "unit-tests" => TestEffort(target, evidence, 1.4m),
            "validation-surface" => ValidationEffort(target, evidence, 0.8m),
            _ => throw Unknown(target),
        };
        return Keep(expected);
    }

    private static decimal EfCoreSourceFactor(string scope) => scope switch
    {
        "src/EFCore/EFCore.csproj" => 1.6m,
        "src/EFCore.Relational/EFCore.Relational.csproj" => 1.55m,
        "src/EFCore.SqlServer/EFCore.SqlServer.csproj" => 1.4m,
        "src/EFCore.Cosmos/EFCore.Cosmos.csproj" => 1.4m,
        "src/Microsoft.Data.Sqlite.Core/Microsoft.Data.Sqlite.Core.csproj" => 1.4m,
        "src/EFCore.Design/EFCore.Design.csproj" => 1.3m,
        "src/dotnet-ef/dotnet-ef.csproj" => 1.2m,
        "src/ef/ef.csproj" => 1.2m,
        _ => 1.15m,
    };

    private static decimal EfCoreDataFactor(string scope) => scope switch
    {
        "src/EFCore.Relational/EFCore.Relational.csproj" => 1.3m,
        "src/EFCore/EFCore.csproj" => 1.2m,
        "src/EFCore.Design/EFCore.Design.csproj" => 1.15m,
        _ => 1.1m,
    };

    private static decimal EfCoreDesignFactor(string scope) => scope switch
    {
        "src/EFCore/EFCore.csproj" => 1.4m,
        "src/EFCore.Relational/EFCore.Relational.csproj" => 1.35m,
        "src/EFCore.SqlServer/EFCore.SqlServer.csproj" => 1.2m,
        "src/EFCore.Cosmos/EFCore.Cosmos.csproj" => 1.2m,
        _ => 1m,
    };
}
