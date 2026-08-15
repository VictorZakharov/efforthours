using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static partial class DevelopmentReviewPolicy
{
    public static CalibrationCapabilityReviewDecision Review(
        string repositoryName,
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence)
    {
        ReviewJudgment judgment = repositoryName switch
        {
            "CarterCommunity/Carter" => Existing(target, CarterExpected(target)),
            "tj/commander.js" => Keep(CommanderExpected(target)),
            "oqtane/oqtane.framework" => Existing(target, OqtaneExpected(target)),
            "App-vNext/Polly" => PollyExpected(target, evidence),
            "FastEndpoints/FastEndpoints" => FastEndpointsExpected(target, evidence),
            "FluentValidation/FluentValidation" => FluentValidationExpected(target, evidence),
            "dotnet/command-line-api" => CommandLineExpected(target, evidence),
            "colinhacks/zod" => ZodExpected(target, evidence),
            "fastify/fastify" => FastifyExpected(target, evidence),
            "lit/lit" => LitExpected(target, evidence),
            "sindresorhus/execa" => ExecaExpected(target, evidence),
            "btcpayserver/btcpayserver" => BtcPayExpected(target, evidence),
            "MudBlazor/MudBlazor" => MudBlazorExpected(target, evidence),
            "simplcommerce/SimplCommerce" => SimplCommerceExpected(target, evidence),
            "Squidex/squidex" => SquidexExpected(target, evidence),
            "sindresorhus/ky" => KyExpected(target, evidence),
            "axios/axios" => AxiosExpected(target, evidence),
            "nrwl/nx" => NxExpected(target, evidence),
            "Cysharp/ConsoleAppFramework" => ConsoleAppFrameworkExpected(target, evidence),
            "spectreconsole/spectre.console" => SpectreConsoleExpected(target, evidence),
            "dotnet/efcore" => EfCoreExpected(target, evidence),
            "jasontaylordev/CleanArchitecture" => CleanArchitectureExpected(target, evidence),
            "ElectronNET/Electron.NET" => ElectronNetExpected(target, evidence),
            "OrchardCMS/OrchardCore" => OrchardCoreExpected(target, evidence),
            _ => throw new InvalidDataException($"No teacher policy exists for '{repositoryName}'."),
        };
        decimal expected = judgment.Expected;
        bool excluded = expected == 0m;
        return new CalibrationCapabilityReviewDecision
        {
            SourceCapabilityId = target.SourceCapabilityId,
            Rationale = Rationale(repositoryName, target, evidence, judgment.ExclusionReason),
            Targets =
            [
                new CalibrationReviewTargetDecision
                {
                    Hours = Range(repositoryName, expected),
                    UncertaintyReasons = Uncertainty(repositoryName, target, expected),
                    SizeException = excluded
                        ? $"Explicit rubric-qualified exclusion: {judgment.ExclusionReason}"
                        : expected > 8m
                            ? "The strict-blind review keeps this cohesive capability as one target because candidate-derived partition counts were hidden; a later review may decompose it without changing the logical total."
                            : null,
                },
            ],
        };
    }

    private sealed record ReviewJudgment(decimal Expected, string? ExclusionReason = null);

    private static ReviewJudgment Keep(decimal expected) => new(expected);

    private static ReviewJudgment Exclude(string reason) => new(0m, reason);

    private static ReviewJudgment Existing(CalibrationAuthoringTarget target, decimal expected) =>
        expected == 0m
            ? Exclude(IsTestScope(target.Scope)
                ? "the evidence belongs to test fixtures already represented by test authoring, so retaining a separate capability would double count the same work."
                : "the static evidence contains no represented behavior beyond another retained capability, so it must not receive a second effort allocation.")
            : Keep(expected);

    private static decimal CarterExpected(CalibrationAuthoringTarget target)
    {
        string kind = Kind(target);
        return kind switch
        {
            "specification-comprehension" => 8m,
            "solution-coordination" => 5m,
            "self-review" => 10m,
            "build-tooling" => 3m,
            "ci-infrastructure" => 5m,
            "documentation" => 5m,
            "data-persistence" => 5m,
            "integration-tests" => target.Scope.Contains("Samples", StringComparison.Ordinal) ? 8m : 2m,
            "unit-tests" => target.Scope.Contains("Newtonsoft", StringComparison.Ordinal) ? 4m : 85m,
            "dotnet-source-backbone" => CarterSource(target.Scope),
            "api-surface" => CarterApi(target.Scope),
            "application-entry-point" => 1m,
            "architecture-design" => CarterArchitecture(target),
            "manual-validation" => CarterValidation(target),
            "project-setup" => Clamp(1m + (0.4m * target.EvidenceIds.Count), 1.5m, 4m),
            "external-integration" => 0m,
            "security-surface" => target.Scope.StartsWith("test/", StringComparison.Ordinal) ? 0m : 2m,
            "validation-surface" => target.Scope.StartsWith("test/", StringComparison.Ordinal)
                ? 0m
                : Clamp(target.EvidenceIds.Count * 0.75m, 1m, 2.5m),
            _ => throw Unknown(target),
        };
    }

    private static decimal CarterSource(string scope) => scope switch
    {
        "src/Carter/Carter.csproj" => 60m,
        "samples/CarterSample/CarterSample.csproj" => 28m,
        "src/Carter.Analyzers/Carter.Analyzers.csproj" => 8m,
        "samples/EntityFwk/EntityFwk.csproj" => 3m,
        "samples/CarterAndMVC/CarterAndMVC.csproj" => 2m,
        "samples/ValidatorOnlyProject/ValidatorOnlyProject.csproj" => 1.5m,
        "template/content/CarterTemplate.csproj" => 2m,
        "src/Carter.ResponseNegotiators.Newtonsoft/Carter.ResponseNegotiators.Newtonsoft.csproj" => 1.5m,
        _ => throw new InvalidDataException($"Unreviewed Carter source scope '{scope}'."),
    };

    private static decimal CarterApi(string scope)
    {
        if (scope.StartsWith("test/", StringComparison.Ordinal))
        {
            return 0m;
        }

        return scope switch
        {
            "src/Carter/Carter.csproj" => 6m,
            "samples/CarterSample/CarterSample.csproj" => 18m,
            "samples/CarterAndMVC/CarterAndMVC.csproj" => 2m,
            "samples/EntityFwk/EntityFwk.csproj" => 1m,
            "template/content/CarterTemplate.csproj" => 1m,
            _ => throw new InvalidDataException($"Unreviewed Carter API scope '{scope}'."),
        };
    }

    private static decimal CarterArchitecture(CalibrationAuthoringTarget target) => target.Scope switch
    {
        "src/Carter/Carter.csproj" => 8m,
        "samples/CarterSample/CarterSample.csproj" => 6m,
        "samples/EntityFwk/EntityFwk.csproj" => 5m,
        "samples/CarterAndMVC/CarterAndMVC.csproj" => 2.5m,
        _ => Clamp(1m + (0.4m * target.EvidenceIds.Count), 1.5m, 3m),
    };

    private static decimal CarterValidation(CalibrationAuthoringTarget target) => target.Scope switch
    {
        "src/Carter/Carter.csproj" => 6m,
        "samples/CarterSample/CarterSample.csproj" => 7m,
        "samples/EntityFwk/EntityFwk.csproj" => 4m,
        _ => Clamp(0.75m + (0.35m * target.EvidenceIds.Count), 1m, 2.5m),
    };

    private static decimal CommanderExpected(CalibrationAuthoringTarget target) => Kind(target) switch
    {
        "specification-comprehension" => 12m,
        "solution-coordination" => 1.5m,
        "self-review" => 12m,
        "project-setup" => 3m,
        "architecture-design" => 12m,
        "build-tooling" => 8m,
        "ci-infrastructure" => 3m,
        "documentation" => 24m,
        "application-entry-point" => 15m,
        "javascript-source-backbone" => 160m,
        "unit-tests" => 220m,
        "manual-validation" => 20m,
        "packaging-release" => 4m,
        _ => throw Unknown(target),
    };

    private static decimal OqtaneExpected(CalibrationAuthoringTarget target)
    {
        string kind = Kind(target);
        return kind switch
        {
            "specification-comprehension" => 40m,
            "solution-coordination" => 24m,
            "self-review" => 50m,
            "build-tooling" => 5m,
            "documentation" => 16m,
            "container-deployment" => 4m,
            "packaging-release" => 5m,
            "api-surface" => 280m,
            "data-persistence" => target.Scope == "Oqtane.Server/Oqtane.Server.csproj" ? 220m : 4m,
            "background-work" => 8m,
            "dotnet-source-backbone" => OqtaneDotNetSource(target.Scope),
            "javascript-source-backbone" => 100m,
            "polyglot-source-backbone" => 1m,
            "ui-surface" => OqtaneUi(target.Scope),
            "security-surface" => target.Scope == "Oqtane.Server/Oqtane.Server.csproj" ? 70m : 2m,
            "external-integration" => OqtaneIntegration(target.Scope),
            "application-entry-point" => OqtaneEntryPoint(target.Scope),
            "architecture-design" => OqtaneArchitecture(target.Scope),
            "manual-validation" => OqtaneValidation(target.Scope),
            "project-setup" => OqtaneSetup(target),
            _ => throw Unknown(target),
        };
    }

    private static decimal OqtaneDotNetSource(string scope) => scope switch
    {
        "Oqtane.Server/Oqtane.Server.csproj" => 1150m,
        "Oqtane.Client/Oqtane.Client.csproj" => 480m,
        "Oqtane.Shared/Oqtane.Shared.csproj" => 160m,
        "Oqtane.Application/Server/Oqtane.Application.Server.csproj" => 35m,
        "Oqtane.Maui/Oqtane.Maui.csproj" => 30m,
        "Oqtane.Updater/Oqtane.Updater.csproj" => 12m,
        "Oqtane.Application/Client/Oqtane.Application.Client.csproj" => 2m,
        _ => throw new InvalidDataException($"Unreviewed Oqtane source scope '{scope}'."),
    };

    private static decimal OqtaneUi(string scope) => scope switch
    {
        "Oqtane.Client/Oqtane.Client.csproj" => 500m,
        "." => 90m,
        "Oqtane.Application/Server/Oqtane.Application.Server.csproj" => 24m,
        "Oqtane.Server/Oqtane.Server.csproj" => 15m,
        "Oqtane.Maui/Oqtane.Maui.csproj" => 4m,
        "Oqtane.Application/Client/Oqtane.Application.Client.csproj" => 0m,
        _ => throw new InvalidDataException($"Unreviewed Oqtane UI scope '{scope}'."),
    };

    private static decimal OqtaneIntegration(string scope) => scope switch
    {
        "Oqtane.Client/Oqtane.Client.csproj" => 60m,
        "Oqtane.Server/Oqtane.Server.csproj" => 20m,
        "Oqtane.Maui/Oqtane.Maui.csproj" => 2m,
        "." => 6m,
        _ => 1m,
    };

    private static decimal OqtaneEntryPoint(string scope) => scope switch
    {
        "Oqtane.Application/Server/Oqtane.Application.Server.csproj" => 6m,
        "Oqtane.Maui/Oqtane.Maui.csproj" => 4m,
        "Oqtane.Server/Oqtane.Server.csproj" => 3m,
        _ => 1m,
    };

    private static decimal OqtaneArchitecture(string scope) => scope switch
    {
        "Oqtane.Server/Oqtane.Server.csproj" => 80m,
        "Oqtane.Client/Oqtane.Client.csproj" => 50m,
        "." => 20m,
        "Oqtane.Application/Server/Oqtane.Application.Server.csproj" => 8m,
        "Oqtane.Maui/Oqtane.Maui.csproj" => 6m,
        "Oqtane.Shared/Oqtane.Shared.csproj" => 4m,
        _ when scope.Contains("Templates", StringComparison.Ordinal) => 2m,
        _ => 2.5m,
    };

    private static decimal OqtaneValidation(string scope) => scope switch
    {
        "Oqtane.Server/Oqtane.Server.csproj" => 100m,
        "Oqtane.Client/Oqtane.Client.csproj" => 60m,
        "." => 20m,
        "Oqtane.Application/Server/Oqtane.Application.Server.csproj" => 10m,
        "Oqtane.Maui/Oqtane.Maui.csproj" => 6m,
        _ when scope.Contains("Templates", StringComparison.Ordinal) => 1.5m,
        _ => 2m,
    };

    private static decimal OqtaneSetup(CalibrationAuthoringTarget target) => target.Scope switch
    {
        "Oqtane.Server/Oqtane.Server.csproj" => 6m,
        "Oqtane.Maui/Oqtane.Maui.csproj" => 5m,
        "Oqtane.Client/Oqtane.Client.csproj" => 4m,
        "Oqtane.Application/Server/Oqtane.Application.Server.csproj" => 4m,
        _ => Clamp(1m + (0.35m * target.EvidenceIds.Count), 1.5m, 3.5m),
    };

    private static EffortRange Range(string repositoryName, decimal expected)
    {
        if (expected == 0m)
        {
            return new EffortRange { Low = 0m, Expected = 0m, High = 0m };
        }

        bool frozenSlice = repositoryName is
            "CarterCommunity/Carter" or
            "tj/commander.js" or
            "oqtane/oqtane.framework";
        decimal lowFactor = repositoryName == "oqtane/oqtane.framework"
            ? 0.72m
            : frozenSlice
                ? 0.78m
                : repositoryName is
                    "btcpayserver/btcpayserver" or
                    "MudBlazor/MudBlazor" or
                    "simplcommerce/SimplCommerce" or
                    "Squidex/squidex" or
                    "lit/lit" or
                    "dotnet/efcore" or
                    "nrwl/nx" or
                    "OrchardCMS/OrchardCore" or
                    "ElectronNET/Electron.NET"
                        ? 0.82m
                        : 0.85m;
        decimal highFactor = repositoryName == "oqtane/oqtane.framework"
            ? 1.4m
            : frozenSlice
                ? 1.3m
                : repositoryName is
                    "btcpayserver/btcpayserver" or
                    "MudBlazor/MudBlazor" or
                    "simplcommerce/SimplCommerce" or
                    "Squidex/squidex" or
                    "lit/lit" or
                    "dotnet/efcore" or
                    "nrwl/nx" or
                    "OrchardCMS/OrchardCore" or
                    "ElectronNET/Electron.NET"
                        ? 1.22m
                        : 1.18m;
        decimal high = Math.Max(expected, RoundQuarter(expected * highFactor));
        if (!frozenSlice && high == expected)
        {
            high += 0.25m;
        }

        return new EffortRange
        {
            Low = Math.Min(expected, Math.Max(0.5m, RoundQuarter(expected * lowFactor))),
            Expected = expected,
            High = high,
        };
    }

    private static IReadOnlyList<string> Uncertainty(
        string repositoryName,
        CalibrationAuthoringTarget target,
        decimal expected)
    {
        List<string> reasons = [.. target.UncertaintyReasons];
        if (repositoryName == "oqtane/oqtane.framework")
        {
            reasons.Add("Dynamic framework composition, runtime configuration, and generated template expansion were not executed.");
        }

        if (repositoryName == "tj/commander.js" && Kind(target) == "javascript-source-backbone")
        {
            reasons.Add("A small TypeScript surface is token-backed; the dominant JavaScript surface is parser-backed.");
        }

        if (repositoryName is "dotnet/efcore" or "nrwl/nx" or "OrchardCMS/OrchardCore")
        {
            reasons.Add(
                "The large modular repository contains dynamic build, generation, plugin, or runtime composition that static review did not execute.");
        }

        if (target.Category is EffortCategory.UnitTesting or
            EffortCategory.IntegrationContractAndComponentTesting or
            EffortCategory.EndToEndAndUiTesting)
        {
            reasons.Add("The represented tests were not executed and are assumed passing.");
        }

        if (expected > 8m)
        {
            reasons.Add("Strict-blind review retains this large cohesive capability instead of using candidate-derived partitions.");
        }

        return [.. reasons.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }

    private static string Rationale(
        string repositoryName,
        CalibrationAuthoringTarget target,
        DevelopmentReviewEvidence evidence,
        string? exclusionReason)
    {
        if (exclusionReason is not null)
        {
            return $"Blind review excludes '{target.Title}' under ehe-work-item/1.1.0: {exclusionReason}";
        }

        string basis = target.Category switch
        {
            EffortCategory.ProductionImplementation =>
                "maintained behavior and public compatibility, with repeated declarations and test-only bodies discounted",
            EffortCategory.UiImplementationAndRepresentedUxDecisions =>
                "represented component, form, template, and style semantics rather than raw asset volume",
            EffortCategory.DataModelingPersistenceAndMigrations =>
                "schema, migration, repository, and persistence behavior with patterned migrations discounted",
            EffortCategory.UnitTesting or EffortCategory.IntegrationContractAndComponentTesting =>
                "distinct test behavior, parameterization, fixtures, and assertions with repeated cases discounted",
            EffortCategory.ArchitectureAndTechnicalDesign =>
                "coherent boundaries, dependency structure, and cross-surface design decisions",
            EffortCategory.Documentation =>
                "maintained explanatory coverage and examples rather than physical line count",
            EffortCategory.ManualValidationDebuggingAndHardening =>
                "bounded validation of the represented surface after automated-test creation",
            _ => "the distinct functional or quality responsibility evidenced by the frozen source",
        };
        return $"Blind review of {repositoryName} treats '{target.Title}' as {basis}; pinned source inspection " +
               $"and digest-verified evidence ({evidence.Summarize(target)}) support the bounded logical estimate.";
    }

    private static string Kind(CalibrationAuthoringTarget target)
    {
        const string prefix = "work:";
        int separator = target.SourceCapabilityId.IndexOf(':', prefix.Length);
        return separator > prefix.Length
            ? target.SourceCapabilityId[prefix.Length..separator]
            : throw Unknown(target);
    }

    private static InvalidDataException Unknown(CalibrationAuthoringTarget target) =>
        new($"No teacher judgment exists for '{target.SourceCapabilityId}'.");

    private static decimal Clamp(decimal value, decimal minimum, decimal maximum) =>
        Math.Min(maximum, Math.Max(minimum, RoundQuarter(value)));

    private static decimal RoundQuarter(decimal value) =>
        Math.Round(value * 4m, MidpointRounding.AwayFromZero) / 4m;
}
