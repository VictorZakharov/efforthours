using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Estimation;

public sealed class SeedEstimator : IEstimator
{
    public const string Version = "seed-rules/0.3.0";

    private static readonly HashSet<string> KnownEvidenceKinds =
    [
        EvidenceKinds.Accessibility,
        EvidenceKinds.ApiSurface,
        EvidenceKinds.BackgroundWork,
        EvidenceKinds.BuildConfiguration,
        EvidenceKinds.CiConfiguration,
        EvidenceKinds.Component,
        EvidenceKinds.ContainerConfiguration,
        EvidenceKinds.Coverage,
        EvidenceKinds.DataAccess,
        EvidenceKinds.Documentation,
        EvidenceKinds.DotNetProject,
        EvidenceKinds.DotNetSolution,
        EvidenceKinds.DotNetTest,
        EvidenceKinds.EntryPoint,
        EvidenceKinds.ExcludedContent,
        EvidenceKinds.File,
        EvidenceKinds.Infrastructure,
        EvidenceKinds.Integration,
        EvidenceKinds.JavaScriptConfiguration,
        EvidenceKinds.JavaScriptPackage,
        EvidenceKinds.JavaScriptTest,
        EvidenceKinds.JavaScriptWorkspace,
        EvidenceKinds.Language,
        EvidenceKinds.PackageReference,
        EvidenceKinds.ProjectReference,
        EvidenceKinds.RepositoryInventory,
        EvidenceKinds.SecurityConfiguration,
        EvidenceKinds.SqlArtifact,
        EvidenceKinds.SqlDelivery,
        EvidenceKinds.SqlRepository,
        EvidenceKinds.SqlTest,
        EvidenceKinds.SourceStructure,
        EvidenceKinds.TestSuite,
        EvidenceKinds.UserInterface,
        EvidenceKinds.Validation,
    ];

    private static readonly HashSet<string> SupportedEcosystems =
        new(StringComparer.Ordinal)
        {
            "dotnet",
            "javascript",
            "sql",
            "typescript",
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

        SeedRuleModel model = SeedRuleCatalog.Model;
        SeedRuleCatalogInfo modelInfo = SeedRuleCatalog.Info;
        if (!string.Equals(modelInfo.EstimatorVersion, Version, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Seed estimator version '{Version}' does not match embedded model " +
                $"'{modelInfo.EstimatorVersion}'.");
        }

        SeedEvidenceIndex index = new(evidence);
        SeedWorkItemFactory itemFactory = new(model);
        SeedCapabilityLedger ledger = new SeedCapabilityBuilder(index, itemFactory).Build(profile);
        WorkItem[] representedItems = [.. ledger.Represented
            .Where(capability => capability.Profiles.Contains(profile))
            .SelectMany(itemFactory.Create)
            .OrderBy(item => item.Category)
            .ThenBy(item => item.Scope, StringComparer.Ordinal)
            .ThenBy(item => item.Id, StringComparer.Ordinal)];
        WorkItem[] gapItems = [.. ledger.ProfessionalizationGap
            .Where(capability => capability.Profiles.Contains(profile))
            .SelectMany(itemFactory.Create)
            .OrderBy(item => item.Category)
            .ThenBy(item => item.Scope, StringComparer.Ordinal)
            .ThenBy(item => item.Id, StringComparer.Ordinal)];

        CategoryEstimate[] categories = [.. representedItems
            .GroupBy(item => item.Category)
            .OrderBy(group => group.Key)
            .Select(group => new CategoryEstimate
            {
                Category = group.Key,
                Hours = ContractValidation.Sum(group.Select(item => item.Hours)),
            })];
        EffortRange totalEffort = ContractValidation.Sum(
            representedItems.Select(item => item.Hours));

        List<Diagnostic> diagnostics = [.. evidence.Diagnostics];
        diagnostics.Add(new Diagnostic
        {
            Code = "FB1000",
            Severity = DiagnosticSeverity.Warning,
            Message = "This estimate uses transparent but uncalibrated seed priors. It is experimental and requires review before consequential use.",
        });
        diagnostics.Add(new Diagnostic
        {
            Code = "FB1005",
            Severity = DiagnosticSeverity.Information,
            Message = $"Seed model '{modelInfo.EstimatorVersion}' ({modelInfo.Status}, {modelInfo.Sha256}) was loaded from the bundled offline artifact.",
        });

        EvidenceFact[] unknownFacts = [.. evidence.Facts
            .Where(fact => !KnownEvidenceKinds.Contains(fact.Kind))
            .OrderBy(fact => fact.Id, StringComparer.Ordinal)];
        if (unknownFacts.Length > 0)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = "FB1001",
                Severity = DiagnosticSeverity.Warning,
                Message = $"The seed estimator does not recognize {unknownFacts.Length} evidence fact(s); no effort was invented for them.",
                EvidenceIds = [.. unknownFacts.Select(fact => fact.Id)],
            });
        }

        string[] unsupportedEcosystems = [.. evidence.Repository.Ecosystems
            .Where(ecosystem => !SupportedEcosystems.Contains(ecosystem))
            .Order(StringComparer.Ordinal)];
        if (unsupportedEcosystems.Length > 0)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = "FB1002",
                Severity = DiagnosticSeverity.Warning,
                Message = "Unsupported ecosystem evidence may make the estimate incomplete: " +
                    string.Join(", ", unsupportedEcosystems) + ".",
            });
        }

        if (index.HasExactSourceDuplicates)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = "FB1003",
                Severity = DiagnosticSeverity.Information,
                Message = "Byte-identical maintained source or test bodies were normalized so copied bodies do not independently increase implementation effort.",
            });
        }

        if (gapItems.Length > 0)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = "FB1004",
                Severity = DiagnosticSeverity.Information,
                Message = $"{gapItems.Length} conservative professionalization-gap item(s) are reported separately and excluded from represented EHE and replacement cost.",
                EvidenceIds = [.. gapItems
                    .SelectMany(item => item.EvidenceIds)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)],
            });
        }

        bool hasKnownIssues = evidence.Diagnostics.Any(diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error);
        EstimateReport report = new()
        {
            EstimatorVersion = modelInfo.EstimatorVersion,
            Repository = evidence.Repository,
            Profile = profile,
            Baseline = Baseline,
            TotalEffort = totalEffort,
            RateCard = rateCard,
            TotalCost = rateCard is null ? null : CalculateCost(totalEffort, rateCard),
            Categories = categories,
            WorkItems = representedItems,
            ProfessionalizationGap = gapItems,
            Diagnostics = [.. diagnostics
                .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)],
            Assumptions =
            [
                "The estimate values a clean, competent recreation of current functional and quality state, not historical labor or rework.",
                "A clear specification exists at the level promised by the selected profile.",
                "Discovered tests are assumed to pass on the default static path.",
                "The described system is estimated as working even when the checkout was not executed.",
                "Generated, vendored, minified, binary, copied, and duplicate bodies do not create hand-written implementation value.",
                "Low and high values are preliminary planning bounds formed from item priors and evidence confidence; they are not calibrated probability intervals.",
                "Professionalization-gap work is excluded from represented effort and replacement cost.",
                "Seed productivity priors are experimental and uncalibrated.",
            ],
            Verification = new VerificationSummary
            {
                Mode = VerificationMode.StaticAssumed,
                WorkingState = hasKnownIssues ? WorkingState.KnownIssues : WorkingState.AssumedWorking,
                TestsAssumedPassing = true,
                Note = hasKnownIssues
                    ? "The evidence contains errors; the estimate still represents the materially described working system."
                    : "The repository was not built or executed.",
            },
        };

        IReadOnlyList<string> reportErrors = ContractValidation.Validate(report);
        if (reportErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "The seed estimator produced an invalid report: " +
                string.Join(" ", reportErrors));
        }

        return report;
    }

    private static CostRange CalculateCost(EffortRange effort, RateCard rateCard) => new()
    {
        Low = Round(effort.Low * rateCard.HourlyRate),
        Expected = Round(effort.Expected * rateCard.HourlyRate),
        High = Round(effort.High * rateCard.HourlyRate),
        Currency = rateCard.Currency,
    };

    private static decimal Round(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
