using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.Estimation;

public sealed class SeedEstimator : IEstimator
{
    public const string Version = "seed-rules/0.1.0";

    private static readonly EstimatorReference SeedRules = new()
    {
        Id = "seed-rules",
        Version = "0.1.0",
        Kind = EstimatorKind.Rule,
    };

    private static readonly EstimationBaseline Baseline = new()
    {
        Id = "senior-contractor-2026-no-ai",
        WorkerProfile = "Competent senior contractor familiar with the technical ecosystem",
        TechnologyBaselineYear = 2026,
        BusinessDomainFamiliar = false,
        UsesAi = false,
        Description = "One senior contractor recreating the described working system with modern 2026 tools and no AI.",
    };

    public EstimateReport Estimate(
        RepositoryEvidence evidence,
        EstimationProfile profile,
        RateCard? rateCard = null)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        IReadOnlyList<string> evidenceErrors = ContractValidation.Validate(evidence);
        if (evidenceErrors.Count > 0)
        {
            throw new ArgumentException(
                "Repository evidence is invalid: " + string.Join(" ", evidenceErrors),
                nameof(evidence));
        }

        List<WorkItem> items = [];
        List<EvidenceFact> unsupportedFacts = [];

        foreach (EvidenceFact fact in evidence.Facts.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            List<WorkItem> factItems = CreateItems(fact, profile);
            if (factItems.Count == 0)
            {
                unsupportedFacts.Add(fact);
            }
            else
            {
                items.AddRange(factItems);
            }
        }

        WorkItem[] orderedItems = [.. items
            .OrderBy(item => item.Category)
            .ThenBy(item => item.Id, StringComparer.Ordinal)];

        CategoryEstimate[] categories = [.. orderedItems
            .GroupBy(item => item.Category)
            .OrderBy(group => group.Key)
            .Select(group => new CategoryEstimate
            {
                Category = group.Key,
                Hours = ContractValidation.Sum(group.Select(item => item.Hours)),
            })];

        EffortRange totalEffort = ContractValidation.Sum(orderedItems.Select(item => item.Hours));
        List<Diagnostic> diagnostics = [.. evidence.Diagnostics];
        diagnostics.Add(new Diagnostic
        {
            Code = "FB1000",
            Severity = DiagnosticSeverity.Warning,
            Message = "This estimate uses uncalibrated seed rules intended only to verify the Fairbill pipeline.",
        });

        if (unsupportedFacts.Count > 0)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = "FB1001",
                Severity = DiagnosticSeverity.Information,
                Message = $"The seed estimator did not assign effort to {unsupportedFacts.Count} unsupported evidence fact(s).",
                EvidenceIds = [.. unsupportedFacts.Select(fact => fact.Id)],
            });
        }

        bool hasKnownIssues = evidence.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        EstimateReport report = new()
        {
            EstimatorVersion = Version,
            Repository = evidence.Repository,
            Profile = profile,
            Baseline = Baseline,
            TotalEffort = totalEffort,
            RateCard = rateCard,
            TotalCost = rateCard is null ? null : CalculateCost(totalEffort, rateCard),
            Categories = categories,
            WorkItems = orderedItems,
            Diagnostics = [.. diagnostics
                .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)],
            Assumptions =
            [
                "Discovered tests are assumed to pass on the default static path.",
                "The described system is estimated as working even when the checkout was not executed.",
                "Seed rules are scaffolding and are not calibrated for production estimates.",
            ],
            Verification = new VerificationSummary
            {
                Mode = VerificationMode.StaticAssumed,
                WorkingState = hasKnownIssues ? WorkingState.KnownIssues : WorkingState.AssumedWorking,
                TestsAssumedPassing = true,
                Note = hasKnownIssues
                    ? "The evidence contains errors; the estimate still represents the described working system."
                    : "The repository was not built or executed.",
            },
        };

        IReadOnlyList<string> reportErrors = ContractValidation.Validate(report);
        if (reportErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "The seed estimator produced an invalid report: " + string.Join(" ", reportErrors));
        }

        return report;
    }

    private static List<WorkItem> CreateItems(
        EvidenceFact fact,
        EstimationProfile selectedProfile)
    {
        ComplexityLevel complexity = GetComplexity(fact.Tags);
        List<WorkItem> items = [];

        switch (fact.Kind)
        {
            case EvidenceKinds.Component:
                items.Add(CreateItem(
                    fact,
                    "implementation",
                    EffortCategory.ProductionImplementation,
                    $"Implement {fact.Summary}",
                    Scale(new EffortRange { Low = 3m, Expected = 4m, High = 6m }, complexity),
                    complexity,
                    "A maintained component represents bounded production implementation work.",
                    [EstimationProfile.Implementation, EstimationProfile.Recreation]));

                items.Add(CreateItem(
                    fact,
                    "manual-validation",
                    EffortCategory.ManualValidationDebuggingAndHardening,
                    $"Validate {fact.Summary}",
                    Scale(new EffortRange { Low = 0.5m, Expected = 1m, High = 1.5m }, complexity),
                    complexity,
                    "Working behavior requires reasonable manual validation and debugging independent of automated tests.",
                    [EstimationProfile.Implementation, EstimationProfile.Recreation]));

                if (selectedProfile == EstimationProfile.Recreation)
                {
                    items.Add(CreateItem(
                        fact,
                        "design",
                        EffortCategory.ArchitectureAndTechnicalDesign,
                        $"Design {fact.Summary}",
                        Scale(new EffortRange { Low = 0.75m, Expected = 1.5m, High = 3m }, complexity),
                        complexity,
                        "The recreation profile includes recovering or making design decisions embodied in the component.",
                        [EstimationProfile.Recreation]));
                }

                break;

            case EvidenceKinds.Integration:
                items.Add(CreateItem(
                    fact,
                    "integration",
                    EffortCategory.ExternalIntegrationsAndProtocols,
                    $"Integrate {fact.Summary}",
                    Scale(new EffortRange { Low = 2m, Expected = 4m, High = 8m }, complexity),
                    complexity,
                    "An external boundary requires configuration, adaptation, and validation rather than reimplementation of the dependency.",
                    [EstimationProfile.Implementation, EstimationProfile.Recreation]));
                break;

            case EvidenceKinds.TestSuite:
                (EffortCategory category, EffortRange range, string testKind) = ClassifyTestSuite(fact.Tags);
                items.Add(CreateItem(
                    fact,
                    "tests",
                    category,
                    $"Create {fact.Summary}",
                    Scale(range, complexity),
                    complexity,
                    $"The repository represents a maintained {testKind} test suite whose effort is valued at its observed level.",
                    [EstimationProfile.Implementation, EstimationProfile.Recreation]));
                break;

            case EvidenceKinds.Documentation:
                items.Add(CreateItem(
                    fact,
                    "documentation",
                    EffortCategory.Documentation,
                    $"Document {fact.Summary}",
                    Scale(new EffortRange { Low = 0.5m, Expected = 1m, High = 2m }, complexity),
                    complexity,
                    "Maintained documentation represents explicit authoring and verification work.",
                    [EstimationProfile.Implementation, EstimationProfile.Recreation]));
                break;

            case EvidenceKinds.BuildConfiguration:
                items.Add(CreateItem(
                    fact,
                    "build",
                    EffortCategory.BuildConfigurationAndDeveloperTooling,
                    $"Configure {fact.Summary}",
                    Scale(new EffortRange { Low = 0.5m, Expected = 1m, High = 2m }, complexity),
                    complexity,
                    "Maintained build configuration represents setup, integration, and validation work.",
                    [EstimationProfile.Implementation, EstimationProfile.Recreation]));
                break;
        }

        return items;
    }

    private static WorkItem CreateItem(
        EvidenceFact fact,
        string discriminator,
        EffortCategory category,
        string title,
        EffortRange hours,
        ComplexityLevel complexity,
        string reason,
        IReadOnlyList<EstimationProfile> profiles)
    {
        return new WorkItem
        {
            Id = $"work:{discriminator}:{fact.Id}",
            Category = category,
            Title = title,
            Scope = fact.Scope,
            EvidenceIds = [fact.Id],
            Complexity = complexity,
            Hours = hours,
            Confidence = fact.Provenance.SourceKind == EvidenceSourceKind.Inferred ? 0.45m : 0.60m,
            Reason = reason,
            Estimator = SeedRules,
            Profiles = profiles,
            UncertaintyReasons = fact.Provenance.SourceKind == EvidenceSourceKind.Inferred
                ? ["The source evidence is inferred rather than directly observed."]
                : [],
        };
    }

    private static (EffortCategory Category, EffortRange Range, string Name) ClassifyTestSuite(
        IReadOnlyList<string> tags)
    {
        if (tags.Contains("e2e", StringComparer.OrdinalIgnoreCase))
        {
            return (
                EffortCategory.EndToEndAndUiTesting,
                new EffortRange { Low = 3m, Expected = 5m, High = 8m },
                "end-to-end");
        }

        if (tags.Contains("integration", StringComparer.OrdinalIgnoreCase))
        {
            return (
                EffortCategory.IntegrationContractAndComponentTesting,
                new EffortRange { Low = 2m, Expected = 4m, High = 7m },
                "integration");
        }

        return (
            EffortCategory.UnitTesting,
            new EffortRange { Low = 1.5m, Expected = 3m, High = 5m },
            "unit");
    }

    private static ComplexityLevel GetComplexity(IReadOnlyList<string> tags)
    {
        if (tags.Contains("complexity:exceptional", StringComparer.OrdinalIgnoreCase))
        {
            return ComplexityLevel.Exceptional;
        }

        if (tags.Contains("complexity:high", StringComparer.OrdinalIgnoreCase))
        {
            return ComplexityLevel.High;
        }

        if (tags.Contains("complexity:routine", StringComparer.OrdinalIgnoreCase))
        {
            return ComplexityLevel.Routine;
        }

        return ComplexityLevel.Moderate;
    }

    private static EffortRange Scale(EffortRange range, ComplexityLevel complexity)
    {
        decimal multiplier = complexity switch
        {
            ComplexityLevel.Routine => 0.75m,
            ComplexityLevel.Moderate => 1m,
            ComplexityLevel.High => 1.5m,
            ComplexityLevel.Exceptional => 2m,
            _ => throw new ArgumentOutOfRangeException(nameof(complexity)),
        };

        return new EffortRange
        {
            Low = Round(range.Low * multiplier),
            Expected = Round(range.Expected * multiplier),
            High = Round(range.High * multiplier),
        };
    }

    private static CostRange CalculateCost(EffortRange effort, RateCard rateCard)
    {
        return new CostRange
        {
            Low = Round(effort.Low * rateCard.HourlyRate),
            Expected = Round(effort.Expected * rateCard.HourlyRate),
            High = Round(effort.High * rateCard.HourlyRate),
            Currency = rateCard.Currency,
        };
    }

    private static decimal Round(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
