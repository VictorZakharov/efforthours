using System.Globalization;
using EffortHours.Contracts.V1;

namespace EffortHours.Estimation;

internal sealed record SeedCapabilityLedger(
    IReadOnlyList<CapabilityUnit> Represented,
    IReadOnlyList<CapabilityUnit> ProfessionalizationGap);

internal sealed class SeedCapabilityBuilder(
    SeedEvidenceIndex index,
    SeedWorkItemFactory workItemFactory)
{
    private static readonly EstimationProfile[] BothProfiles =
        [EstimationProfile.Implementation, EstimationProfile.Recreation];
    private static readonly EstimationProfile[] RecreationProfile =
        [EstimationProfile.Recreation];

    private static readonly string[] SemanticKinds =
    [
        EvidenceKinds.ApiSurface,
        EvidenceKinds.BackgroundWork,
        EvidenceKinds.DataAccess,
        EvidenceKinds.Integration,
        EvidenceKinds.SecurityConfiguration,
        EvidenceKinds.UserInterface,
        EvidenceKinds.Validation,
    ];

    private readonly SeedEvidenceIndex _index = index;
    private readonly SeedWorkItemFactory _workItemFactory = workItemFactory;
    private readonly Dictionary<string, decimal> _sourceExpectedByScope =
        new(StringComparer.Ordinal);

    public SeedCapabilityLedger Build(EstimationProfile selectedProfile)
    {
        List<CapabilityUnit> represented = [];
        AddRepositoryUnderstanding(represented);
        AddSolutionAndProjectSetup(represented);
        AddArchitecture(represented, selectedProfile);
        AddSourceBackbone(represented);
        AddSemanticCapabilities(represented);
        AddTests(represented);
        AddDocumentationAndTooling(represented);
        AddManualValidationAndReview(represented);
        List<CapabilityUnit> gap = BuildProfessionalizationGap();
        return new SeedCapabilityLedger(represented, gap);
    }

    private void AddRepositoryUnderstanding(List<CapabilityUnit> capabilities)
    {
        SeedEstimationScope[] productionScopes = [.. _index.Scopes.Where(scope => scope.IsProduction)];
        IReadOnlyList<EvidenceFact> anchors = _index.RepositoryAnchorEvidence();
        EvidenceFact[] evidence = Evidence(
            anchors,
            productionScopes.Select(scope => scope.Fact),
            SemanticKinds.SelectMany(kind => _index.FactsOfKind(kind)));
        if (evidence.Length == 0)
        {
            return;
        }

        decimal semanticFamilies = SemanticKinds.Count(kind => _index.FactsOfKind(kind).Count > 0);
        capabilities.Add(new CapabilityUnit
        {
            Id = "repository:specification-comprehension",
            RuleId = "specification-comprehension",
            Title = "Comprehend the supplied specification and bounded business domain",
            Scope = ".",
            Quantity = decimal.Max(1m, productionScopes.Length),
            Drivers = Drivers(
                ("production-scopes", productionScopes.Length),
                ("semantic-families", semanticFamilies)),
            Complexity = semanticFamilies >= 6m ? ComplexityLevel.High : ComplexityLevel.Moderate,
            Reason = "The modeled contractor must understand the specified behavior and bounded domain represented across the repository before implementing it.",
            Evidence = evidence,
            Profiles = BothProfiles,
            CorrelationGroup = "repository:understanding-and-design",
            Assumptions =
            [
                "A clear specification exists at the level promised by the selected profile.",
                "Open-ended stakeholder discovery is excluded.",
            ],
            UncertaintyReasons =
            [
                "No external specification artifact is represented in the current estimator request contract.",
            ],
            ConfidencePenalty = 0.06m,
        });
    }

    private void AddSolutionAndProjectSetup(List<CapabilityUnit> capabilities)
    {
        EvidenceFact[] solutions = [.. _index.FactsOfKind(EvidenceKinds.DotNetSolution)];
        EvidenceFact[] workspaces = [.. _index.FactsOfKind(EvidenceKinds.JavaScriptWorkspace)];
        EvidenceFact[] references = [.. _index.FactsOfKind(EvidenceKinds.ProjectReference)];
        EvidenceFact[] coordinationEvidence = Evidence(solutions, workspaces, references);
        if (coordinationEvidence.Length > 0)
        {
            capabilities.Add(new CapabilityUnit
            {
                Id = "repository:solution-coordination",
                RuleId = "solution-coordination",
                Title = "Coordinate solution, workspace, and local dependency boundaries",
                Scope = ".",
                Quantity = decimal.Max(1m, solutions.Length + workspaces.Length + references.Length),
                Drivers = Drivers(
                    ("solutions", solutions.Length),
                    ("workspaces", workspaces.Length),
                    ("reference-edges", references.Length)),
                Complexity = references.Any(HasUnresolvedReference)
                    ? ComplexityLevel.High
                    : ComplexityLevel.Moderate,
                Reason = "Multi-project and workspace coordination represents explicit graph, configuration, and integration setup.",
                Evidence = coordinationEvidence,
                Profiles = BothProfiles,
                CorrelationGroup = "repository:setup",
                UncertaintyReasons = references.Any(HasUnresolvedReference)
                    ? ["At least one project or configuration reference is unresolved or outside the analyzed scope."]
                    : [],
            });
        }

        foreach (SeedEstimationScope scope in _index.Scopes)
        {
            EvidenceFact[] packageReferences = [.. _index.FactsOfKind(EvidenceKinds.PackageReference)
                .Where(fact => fact.Scope == scope.Scope)];
            EvidenceFact[] configurations = [.. _index.FactsOfKind(EvidenceKinds.JavaScriptConfiguration)
                .Where(fact => fact.Scope == scope.Scope)];
            decimal targetFrameworks = SeedEvidenceIndex.Measurement(scope.Fact, "target-frameworks");
            decimal dependencies = decimal.Max(
                packageReferences.Length,
                decimal.Max(
                    SeedEvidenceIndex.Measurement(scope.Fact, "packages"),
                    SeedEvidenceIndex.Measurement(scope.Fact, "dependencies")));
            decimal configurationOptions = configurations.Sum(fact =>
                SeedEvidenceIndex.Measurement(fact, "compiler-options"));
            EvidenceFact[] evidence = Evidence(
                [scope.Fact],
                packageReferences,
                configurations);
            bool unresolved = SeedEvidenceIndex.Measurement(
                scope.Fact,
                "unresolved-msbuild-values") > 0m ||
                configurations.Any(fact => fact.Tags.Contains(
                    "reference:unresolved",
                    StringComparer.Ordinal));
            capabilities.Add(new CapabilityUnit
            {
                Id = $"scope:{scope.Id}:project-setup",
                RuleId = "project-setup",
                Title = $"Set up {DisplayScope(scope)}",
                Scope = scope.Scope,
                Quantity = 1m,
                Drivers = Drivers(
                    ("scopes", 1m),
                    ("target-frameworks", targetFrameworks),
                    ("dependencies", dependencies),
                    ("configuration-options", configurationOptions)),
                Complexity = unresolved ? ComplexityLevel.High : ComplexityLevel.Routine,
                Reason = "A project or package manifest represents ecosystem setup, dependency selection, configuration, and validation work.",
                Evidence = evidence,
                Profiles = BothProfiles,
                CorrelationGroup = $"scope:{scope.Id}:setup",
                UncertaintyReasons = unresolved
                    ? ["Some declared configuration values could not be resolved by static analysis."]
                    : [],
            });
        }
    }

    private void AddArchitecture(
        List<CapabilityUnit> capabilities,
        EstimationProfile selectedProfile)
    {
        foreach (SeedEstimationScope scope in _index.Scopes.Where(scope => scope.IsProduction))
        {
            EvidenceFact[] semantic = Evidence(SemanticKinds.SelectMany(kind =>
                _index.FactsOfKind(kind).Where(fact => ScopeId(fact) == scope.Id)));
            NormalizedEvidenceFact[] normalizedSemantic = NormalizedSemanticFactsForScope(scope);
            EvidenceFact[] references = [.. _index.FactsOfKind(EvidenceKinds.ProjectReference)
                .Where(fact => fact.Scope == scope.Scope)];
            int boundaryFamilies = normalizedSemantic
                .Select(fact => fact.Kind)
                .Distinct(StringComparer.Ordinal)
                .Count();
            EvidenceFact[] evidence = Evidence(_index.ScopeEvidence(scope), semantic, references);
            capabilities.Add(new CapabilityUnit
            {
                Id = $"scope:{scope.Id}:architecture",
                RuleId = "architecture-design",
                Title = $"Design the technical structure of {DisplayScope(scope)}",
                Scope = scope.Scope,
                Quantity = 1m,
                Drivers = Drivers(
                    ("production-scopes", 1m),
                    ("boundary-families", boundaryFamilies),
                    ("reference-edges", references.Length)),
                Complexity = boundaryFamilies >= 5 ? ComplexityLevel.High : ComplexityLevel.Moderate,
                Reason = "Even with supplied requirements, a competent implementation requires bounded technical design across the observed boundaries.",
                Evidence = evidence,
                Profiles = BothProfiles,
                CorrelationGroup = $"scope:{scope.Id}:design",
                Assumptions = ["Detailed product and visual design inputs are supplied under the implementation profile."],
            });

            if (selectedProfile != EstimationProfile.Recreation)
            {
                continue;
            }

            decimal publicSurface = normalizedSemantic.Count(fact => fact.Kind is
                EvidenceKinds.ApiSurface or EvidenceKinds.DataAccess or
                EvidenceKinds.UserInterface or EvidenceKinds.Integration);
            decimal decisionSurfaces = decimal.Max(1m, boundaryFamilies + publicSurface);
            capabilities.Add(new CapabilityUnit
            {
                Id = $"scope:{scope.Id}:recreation-design",
                RuleId = "recreation-design",
                Title = $"Recover architecture and interface decisions for {DisplayScope(scope)}",
                Scope = scope.Scope,
                Quantity = decisionSurfaces,
                Drivers = Drivers(("decision-surfaces", decisionSurfaces)),
                Complexity = boundaryFamilies >= 5 ? ComplexityLevel.High : ComplexityLevel.Moderate,
                Reason = "The recreation profile explicitly values recovering or making architecture, data, API, interface, and UX decisions embodied in the artifact.",
                Evidence = evidence,
                Profiles = RecreationProfile,
                CorrelationGroup = $"scope:{scope.Id}:design",
                Assumptions = ["Open-ended stakeholder discovery and historical rework are excluded."],
                UncertaintyReasons = ["Repository structure is only a proxy for the design decisions supplied to the original implementation."],
                ConfidencePenalty = 0.08m,
            });
        }
    }

    private void AddSourceBackbone(List<CapabilityUnit> capabilities)
    {
        foreach (EvidenceFact fact in _index.FactsOfKind(EvidenceKinds.SourceStructure))
        {
            SeedEstimationScope? scope = _index.FindScope(fact);
            if (scope is null || scope.IsTest)
            {
                continue;
            }

            StructureNormalization normalization = _index.GetStructureNormalization(scope);
            if (normalization.Factor <= 0m)
            {
                continue;
            }

            string ruleId = SeedEvidenceIndex.EcosystemFor(fact) == "dotnet"
                ? "dotnet-source-backbone"
                : "javascript-source-backbone";
            string[] allowedDrivers = ruleId == "dotnet-source-backbone"
                ? ["files", "types", "public-types", "methods", "public-methods", "async-methods", "branch-points"]
                : ["files", "functions", "methods", "classes", "interfaces", "type-aliases", "enums", "async-functions", "branch-points", "exports", "dynamic-imports"];
            Dictionary<string, decimal> drivers = allowedDrivers.ToDictionary(
                name => name,
                name => SeedEvidenceIndex.Measurement(fact, name) * normalization.Factor,
                StringComparer.Ordinal);
            decimal quantity = decimal.Max(0.01m, drivers.Values.Sum());
            List<EvidenceFact> evidence = [fact];
            if (normalization.HasDuplicates)
            {
                evidence.AddRange(_index.Files
                    .Where(file => file.IsMaintained && !file.IsTest && !_index.IsCanonical(file))
                    .Where(file => _index.FindScopeForPath(file.Path, scope.Ecosystem)?.Id == scope.Id)
                    .Select(file => file.Fact));
            }

            List<string> assumptions =
            [
                "Backbone priors value residual internal code construction at lower rates than complete feature delivery.",
            ];
            List<string> uncertainty = [];
            if (normalization.HasTests)
            {
                assumptions.Add("Test-file structure is excluded from the production implementation backbone.");
                uncertainty.Add("Aggregate source structure was proportionally adjusted using production/test file classification.");
            }

            if (normalization.HasDuplicates)
            {
                assumptions.Add(
                    $"Exact duplicate source was normalized from {normalization.ProductionFiles.ToString(CultureInfo.InvariantCulture)} " +
                    $"production files to {normalization.CanonicalProductionFiles.ToString(CultureInfo.InvariantCulture)} unique bodies.");
                uncertainty.Add("Aggregate parser structure was proportionally normalized because per-file structure is not serialized.");
            }

            CapabilityUnit capability = new()
            {
                Id = $"scope:{scope.Id}:source-backbone:{fact.Id}",
                RuleId = ruleId,
                Title = $"Implement maintained internal logic for {DisplayScope(scope)}",
                Scope = scope.Scope,
                Quantity = quantity,
                Drivers = drivers,
                Complexity = SourceComplexity(drivers),
                Reason = "Parser or token structure represents maintained internal logic that routine semantic classifiers cannot name.",
                Evidence = Evidence(evidence),
                Profiles = BothProfiles,
                CorrelationGroup = $"scope:{scope.Id}:implementation",
                Assumptions = assumptions,
                Exclusions = _index.FactsOfKind(EvidenceKinds.ExcludedContent).Count > 0
                    ? ["Generated, vendored, minified, binary, and other excluded bodies are not valued as hand-written implementation."]
                    : [],
                UncertaintyReasons = uncertainty,
                ConfidencePenalty = normalization.HasDuplicates || normalization.HasTests ? 0.04m : 0m,
            };
            capabilities.Add(capability);
            _sourceExpectedByScope[scope.Id] =
                _sourceExpectedByScope.GetValueOrDefault(scope.Id) +
                _workItemFactory.EvaluateExpected(capability);
        }
    }

    private void AddSemanticCapabilities(List<CapabilityUnit> capabilities)
    {
        AddEntryPoints(capabilities);
        AddApiSurfaces(capabilities);
        AddUiSurfaces(capabilities);
        AddDataSurfaces(capabilities);
        AddIntegrationSurfaces(capabilities);
        AddSecuritySurfaces(capabilities);
        AddValidationSurfaces(capabilities);
        AddBackgroundSurfaces(capabilities);
    }

    private void AddEntryPoints(List<CapabilityUnit> capabilities)
    {
        foreach (FactAggregate aggregate in GroupByScope(EvidenceKinds.EntryPoint))
        {
            capabilities.Add(SemanticCapability(
                aggregate,
                "application-entry-point",
                "entry-point",
                $"Implement application entry points for {DisplayScope(aggregate.Scope)}",
                Drivers(("entry-points", aggregate.NormalizedCount)),
                aggregate.NormalizedCount,
                ComplexityLevel.Routine,
                "Application startup and command entry points require explicit wiring and usable startup behavior."));
        }
    }

    private void AddApiSurfaces(List<CapabilityUnit> capabilities)
    {
        foreach (FactAggregate aggregate in GroupByScope(EvidenceKinds.ApiSurface))
        {
            decimal endpoints = aggregate.Measurement("attributed-endpoints") +
                aggregate.Measurement("minimal-api-endpoints") +
                aggregate.Measurement("endpoints");
            decimal controllers = aggregate.Measurement("controllers");
            decimal graphql = aggregate.Measurement("graphql-operations");
            decimal routeGroups = aggregate.Measurement("route-groups");
            decimal quantity = decimal.Max(1m, endpoints + graphql + controllers);
            capabilities.Add(SemanticCapability(
                aggregate,
                "api-surface",
                "api",
                $"Implement API and route behavior for {DisplayScope(aggregate.Scope)}",
                Drivers(
                    ("endpoints", endpoints),
                    ("controllers", controllers),
                    ("graphql-operations", graphql),
                    ("route-groups", routeGroups)),
                quantity,
                endpoints + graphql > 40m ? ComplexityLevel.High : ComplexityLevel.Moderate,
                "API priors value routing, contracts, framework wiring, edge behavior, and adaptation beyond residual method construction."));
        }
    }

    private void AddUiSurfaces(List<CapabilityUnit> capabilities)
    {
        foreach (FactAggregate aggregate in GroupByScope(EvidenceKinds.UserInterface))
        {
            decimal pages = aggregate.Measurement("pages") +
                aggregate.Measurement("page-directives");
            decimal components = aggregate.Measurement("components");
            if (components == 0m && aggregate.Tags.Contains("razor-kind:component", StringComparer.Ordinal))
            {
                components = aggregate.Measurement("files");
            }

            decimal uiTypes = aggregate.Measurement("ui-types");
            decimal forms = aggregate.Measurement("forms") + aggregate.Measurement("form-usages");
            decimal assetFiles = aggregate.Tags.Contains("ui-asset:maintained", StringComparer.Ordinal)
                ? aggregate.Measurement("files")
                : 0m;
            decimal quantity = decimal.Max(
                1m,
                pages + components + uiTypes + forms + assetFiles);
            capabilities.Add(SemanticCapability(
                aggregate,
                "ui-surface",
                "ui",
                $"Implement represented UI and UX behavior for {DisplayScope(aggregate.Scope)}",
                Drivers(
                    ("pages", pages),
                    ("components", components),
                    ("ui-types", uiTypes),
                    ("forms", forms),
                    ("state-usages", aggregate.Measurement("state-usages")),
                    ("effect-usages", aggregate.Measurement("effect-usages")),
                    ("component-parameters", aggregate.Measurement("component-parameters")),
                    ("commands", aggregate.Measurement("commands")),
                    ("asset-files", assetFiles),
                    ("asset-lines", aggregate.Measurement("physical-lines"))),
                quantity,
                pages + forms > 20m ? ComplexityLevel.High : ComplexityLevel.Moderate,
                "UI priors value represented pages, components, state, forms, maintained assets, and UX decisions beyond residual source construction."));
        }
    }

    private void AddDataSurfaces(List<CapabilityUnit> capabilities)
    {
        foreach (FactAggregate aggregate in GroupByScope(EvidenceKinds.DataAccess))
        {
            Dictionary<string, decimal> drivers = Drivers(
                ("db-contexts", aggregate.Measurement("db-contexts")),
                ("db-sets", aggregate.Measurement("db-sets")),
                ("migrations", aggregate.Measurement("migrations")),
                ("entity-configurations", aggregate.Measurement("entity-configurations")),
                ("repository-types", aggregate.Measurement("repository-types")),
                ("data-calls", aggregate.Measurement("data-calls")));
            capabilities.Add(SemanticCapability(
                aggregate,
                "data-persistence",
                "data",
                $"Implement data modeling and persistence for {DisplayScope(aggregate.Scope)}",
                drivers,
                decimal.Max(1m, drivers.Values.Sum()),
                drivers.GetValueOrDefault("migrations") > 20m ? ComplexityLevel.High : ComplexityLevel.Moderate,
                "Data priors value schema decisions, persistence configuration, migrations, queries, and validation beyond residual source construction."));
        }
    }

    private void AddIntegrationSurfaces(List<CapabilityUnit> capabilities)
    {
        foreach (FactAggregate aggregate in GroupByScope(EvidenceKinds.Integration))
        {
            int technologies = aggregate.Tags.Count(tag => tag.StartsWith("technology:", StringComparison.Ordinal));
            decimal boundaries = decimal.Max(1m, technologies);
            Dictionary<string, decimal> drivers = Drivers(
                ("boundaries", boundaries),
                ("client-constructions", aggregate.Measurement("client-constructions")),
                ("integration-calls", aggregate.Measurement("integration-calls")),
                ("integration-namespaces", aggregate.Measurement("integration-namespaces")));
            capabilities.Add(SemanticCapability(
                aggregate,
                "external-integration",
                "integration",
                $"Integrate external services and protocols for {DisplayScope(aggregate.Scope)}",
                drivers,
                boundaries,
                technologies > 3 ? ComplexityLevel.High : ComplexityLevel.Moderate,
                "Integration priors value selecting, configuring, adapting, and validating each distinct external boundary without reimplementing the dependency.",
                technologies == 0
                    ? ["Static analysis detected an integration shape but could not identify a distinct technology family."]
                    : []));
        }
    }

    private void AddSecuritySurfaces(List<CapabilityUnit> capabilities)
    {
        foreach (FactAggregate aggregate in GroupByScope(EvidenceKinds.SecurityConfiguration))
        {
            Dictionary<string, decimal> drivers = Drivers(
                ("security-surfaces", decimal.Max(1m, aggregate.NormalizedCount)),
                ("security-configuration-calls", aggregate.Measurement("security-configuration-calls")),
                ("authorization-attributes", aggregate.Measurement("authorization-attributes")),
                ("security-usages", aggregate.Measurement("security-usages")));
            capabilities.Add(SemanticCapability(
                aggregate,
                "security-surface",
                "security",
                $"Implement represented authentication and security behavior for {DisplayScope(aggregate.Scope)}",
                drivers,
                decimal.Max(1m, aggregate.NormalizedCount),
                ComplexityLevel.High,
                "Security priors value authentication, authorization, credential handling, pipeline configuration, and careful validation beyond residual source construction."));
        }
    }

    private void AddValidationSurfaces(List<CapabilityUnit> capabilities)
    {
        foreach (FactAggregate aggregate in GroupByScope(EvidenceKinds.Validation))
        {
            Dictionary<string, decimal> drivers = Drivers(
                ("validator-types", aggregate.Measurement("validator-types")),
                ("validation-rules", aggregate.Measurement("validation-rules")),
                ("validation-attributes", aggregate.Measurement("validation-attributes")),
                ("validation-usages", aggregate.Measurement("validation-usages")));
            capabilities.Add(SemanticCapability(
                aggregate,
                "validation-surface",
                "validation",
                $"Implement represented validation behavior for {DisplayScope(aggregate.Scope)}",
                drivers,
                decimal.Max(1m, drivers.Values.Sum()),
                ComplexityLevel.Moderate,
                "Validation priors value explicit input and domain rules beyond their residual source syntax."));
        }
    }

    private void AddBackgroundSurfaces(List<CapabilityUnit> capabilities)
    {
        foreach (FactAggregate aggregate in GroupByScope(EvidenceKinds.BackgroundWork))
        {
            Dictionary<string, decimal> drivers = Drivers(
                ("hosted-services", aggregate.Measurement("hosted-services")),
                ("message-handlers", aggregate.Measurement("message-handlers")),
                ("functions", aggregate.Measurement("functions")),
                ("background-usages", aggregate.Measurement("background-usages")),
                ("hosted-service-registrations", aggregate.Measurement("hosted-service-registrations")));
            capabilities.Add(SemanticCapability(
                aggregate,
                "background-work",
                "background",
                $"Implement background, queue, and handler behavior for {DisplayScope(aggregate.Scope)}",
                drivers,
                decimal.Max(1m, drivers.Values.Sum()),
                drivers.Values.Sum() > 20m ? ComplexityLevel.High : ComplexityLevel.Moderate,
                "Background-work priors value lifecycle, scheduling, delivery semantics, handler wiring, and validation beyond residual source construction."));
        }
    }

    private static CapabilityUnit SemanticCapability(
        FactAggregate aggregate,
        string ruleId,
        string discriminator,
        string title,
        IReadOnlyDictionary<string, decimal> drivers,
        decimal quantity,
        ComplexityLevel complexity,
        string reason,
        IReadOnlyList<string>? additionalUncertainty = null)
    {
        List<string> assumptions =
        [
            "The specialized prior values boundary-specific design, configuration, adaptation, and validation rather than a second full price for source syntax.",
        ];
        List<string> uncertainty = [.. additionalUncertainty ?? []];
        if (aggregate.HasExactDuplicates)
        {
            assumptions.Add("Byte-identical semantic source bodies are valued once while all paths remain traceable.");
            uncertainty.Add("Exact duplicate semantic facts were collapsed by maintained-file content digest.");
        }

        return new CapabilityUnit
        {
            Id = $"scope:{aggregate.Scope.Id}:{discriminator}",
            RuleId = ruleId,
            Title = title,
            Scope = aggregate.Scope.Scope,
            Quantity = decimal.Max(0.01m, quantity),
            Drivers = drivers,
            Complexity = complexity,
            Reason = reason,
            Evidence = aggregate.Evidence,
            Profiles = BothProfiles,
            CorrelationGroup = $"scope:{aggregate.Scope.Id}:implementation",
            Assumptions = assumptions,
            UncertaintyReasons = uncertainty,
            ConfidencePenalty = aggregate.HasExactDuplicates ? 0.03m : 0m,
        };
    }

    private void AddTests(List<CapabilityUnit> capabilities)
    {
        NormalizedEvidenceFact[] fineTests =
        [
            .. _index.NormalizedFactsOfKind(EvidenceKinds.DotNetTest),
            .. _index.NormalizedFactsOfKind(EvidenceKinds.JavaScriptTest),
        ];
        foreach (IGrouping<string, NormalizedEvidenceFact> group in fineTests
            .Where(fact => _index.FindScope(fact.Facts[0]) is not null)
            .GroupBy(
                fact =>
                {
                    SeedEstimationScope scope = _index.FindScope(fact.Facts[0])!;
                    return $"{scope.Id}|{TestType(fact)}";
                },
                StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            NormalizedEvidenceFact[] facts = [.. group];
            SeedEstimationScope scope = _index.FindScope(facts[0].Facts[0])!;
            string testType = TestType(facts[0]);
            FactAggregate aggregate = Aggregate(scope, facts);
            decimal testCases = aggregate.Measurement("test-methods") +
                aggregate.Measurement("test-cases");
            if (testCases == 0m)
            {
                testCases = aggregate.NormalizedCount;
            }

            string ruleId = testType switch
            {
                "end-to-end" => "end-to-end-tests",
                "component" or "integration" => "integration-tests",
                _ => "unit-tests",
            };
            ComplexityLevel complexity = testType == "end-to-end"
                ? ComplexityLevel.High
                : aggregate.Measurement("mock-usages") > 10m
                    ? ComplexityLevel.Moderate
                    : ComplexityLevel.Routine;
            CapabilityUnit capability = new()
            {
                Id = $"scope:{scope.Id}:tests:{testType}",
                RuleId = ruleId,
                Title = $"Create represented {testType.Replace('-', ' ')} tests for {DisplayScope(scope)}",
                Scope = scope.Scope,
                Quantity = decimal.Max(1m, testCases),
                Drivers = Drivers(
                    ("test-files", aggregate.NormalizedCount),
                    ("test-cases", testCases),
                    ("parameterized-cases", aggregate.Measurement("parameterized-cases")),
                    ("assertions", aggregate.Measurement("assertions")),
                    ("mock-usages", aggregate.Measurement("mock-usages"))),
                Complexity = complexity,
                Reason = "Test priors value the represented test type, cases, parameterization, assertions, fixtures, and isolation at the level statically observed.",
                Evidence = aggregate.Evidence,
                Profiles = BothProfiles,
                CorrelationGroup = $"scope:{scope.Id}:tests",
                Assumptions =
                [
                    "Discovered tests are assumed to pass on the default static path.",
                    "Repeated assertions and parameterized cases receive diminishing marginal effort.",
                ],
                UncertaintyReasons = aggregate.HasExactDuplicates
                    ? ["Byte-identical test bodies were collapsed by content digest."]
                    : [],
                ConfidencePenalty = aggregate.HasExactDuplicates ? 0.03m : 0m,
            };
            capabilities.Add(capability);
        }

        if (fineTests.Length == 0)
        {
            foreach (EvidenceFact fact in _index.FactsOfKind(EvidenceKinds.TestSuite))
            {
                string testType = fact.Tags.Contains("e2e", StringComparer.OrdinalIgnoreCase)
                    ? "end-to-end"
                    : fact.Tags.Contains("integration", StringComparer.OrdinalIgnoreCase)
                        ? "integration"
                        : "unit";
                string ruleId = testType switch
                {
                    "end-to-end" => "end-to-end-tests",
                    "integration" => "integration-tests",
                    _ => "unit-tests",
                };
                decimal files = decimal.Max(1m, SeedEvidenceIndex.Measurement(fact, "files"));
                CapabilityUnit capability = new()
                {
                    Id = $"repository:fallback-tests:{testType}:{fact.Id}",
                    RuleId = ruleId,
                    Title = $"Create represented {testType.Replace('-', ' ')} test artifacts",
                    Scope = fact.Scope,
                    Quantity = files,
                    Drivers = Drivers(("test-files", files), ("test-cases", files)),
                    Complexity = testType == "end-to-end"
                        ? ComplexityLevel.High
                        : ComplexityLevel.Routine,
                    Reason = "Common-scanner test artifacts provide a conservative fallback when ecosystem-specific test structure is unavailable.",
                    Evidence = [fact],
                    Profiles = BothProfiles,
                    CorrelationGroup = "repository:tests",
                    Assumptions = ["Discovered tests are assumed to pass on the default static path."],
                    UncertaintyReasons = ["Test cases and semantics were not available from an ecosystem-specific analyzer."],
                    ConfidencePenalty = 0.12m,
                };
                capabilities.Add(capability);
            }
        }

        AddCoverageCapabilities(capabilities);
    }

    private void AddCoverageCapabilities(List<CapabilityUnit> capabilities)
    {
        foreach (IGrouping<string, EvidenceFact> group in _index.FactsOfKind(EvidenceKinds.Coverage)
            .Where(fact => fact.Measurements.Any(measurement => measurement.Unit == "percent"))
            .Where(fact => _index.FindScope(fact) is not null)
            .GroupBy(fact => _index.FindScope(fact)!.Id, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            EvidenceFact[] facts = [.. group.OrderBy(fact => fact.Id, StringComparer.Ordinal)];
            SeedEstimationScope scope = _index.FindScope(facts[0])!;
            decimal[] percentages = [.. facts
                .SelectMany(fact => fact.Measurements)
                .Where(measurement => measurement.Unit == "percent")
                .Select(measurement => measurement.Value)];
            decimal target = percentages.Average();
            decimal sourceExpected = _sourceExpectedByScope.GetValueOrDefault(scope.Id);
            decimal coveredImplementationHours = sourceExpected * target / 100m;
            CapabilityUnit capability = new()
            {
                Id = $"scope:{scope.Id}:declared-coverage",
                RuleId = "coverage-achievement",
                Title = $"Achieve the declared coverage level for {DisplayScope(scope)}",
                Scope = scope.Scope,
                Quantity = decimal.Max(0.01m, coveredImplementationHours),
                Drivers = Drivers(("covered-implementation-hours", coveredImplementationHours)),
                Complexity = target >= 100m
                    ? ComplexityLevel.High
                    : target >= 80m
                        ? ComplexityLevel.Moderate
                        : ComplexityLevel.Routine,
                Reason = $"A declared-and-assumed average coverage target of {target.ToString("0.##", CultureInfo.InvariantCulture)}% represents breadth and edge-case effort beyond test syntax counts alone.",
                Evidence = facts,
                Profiles = BothProfiles,
                CorrelationGroup = $"scope:{scope.Id}:tests",
                Assumptions =
                [
                    "The declared configured coverage threshold is assumed achieved on the default static path.",
                    "This item values represented coverage breadth; it is not measured coverage.",
                ],
                UncertaintyReasons =
                [
                    "Declared coverage was not executed or measured by EffortHours.",
                    "Static source construction is a proxy for the amount of behavior covered.",
                ],
                ConfidencePenalty = 0.08m,
            };
            capabilities.Add(capability);
        }
    }

    private void AddDocumentationAndTooling(List<CapabilityUnit> capabilities)
    {
        AddDocumentation(capabilities);
        AddBuildTooling(capabilities);
        AddCiAndInfrastructure(capabilities);
        AddContainers(capabilities);
        AddPackaging(capabilities);
    }

    private void AddDocumentation(List<CapabilityUnit> capabilities)
    {
        SeedFileEvidence[] documents = [.. _index.Files
            .Where(file => file.IsMaintained && file.Role == "documentation")];
        SeedFileEvidence[] canonical = [.. documents.Where(_index.IsCanonical)];
        EvidenceFact[] evidence = Evidence(
            documents.Select(file => file.Fact),
            _index.FactsOfKind(EvidenceKinds.Documentation));
        if (evidence.Length == 0)
        {
            return;
        }

        decimal documentCount = canonical.Length;
        decimal physicalLines = canonical.Sum(file => file.PhysicalLines);
        if (documentCount == 0m)
        {
            documentCount = decimal.Max(
                1m,
                evidence.Sum(fact => SeedEvidenceIndex.Measurement(fact, "files")));
            physicalLines = evidence.Sum(fact => SeedEvidenceIndex.Measurement(fact, "physical-lines"));
        }

        capabilities.Add(new CapabilityUnit
        {
            Id = "repository:documentation",
            RuleId = "documentation",
            Title = "Author and verify maintained repository documentation",
            Scope = ".",
            Quantity = documentCount,
            Drivers = Drivers(
                ("documents", documentCount),
                ("physical-lines", physicalLines)),
            Complexity = ComplexityLevel.Routine,
            Reason = "Maintained onboarding, architecture, API, operational, and other guidance represents explicit authoring and verification work.",
            Evidence = evidence,
            Profiles = BothProfiles,
            CorrelationGroup = "repository:documentation",
            Assumptions = documents.Length > canonical.Length
                ? ["Byte-identical documentation bodies are valued once while all paths remain traceable."]
                : [],
        });
    }

    private void AddBuildTooling(List<CapabilityUnit> capabilities)
    {
        SeedFileEvidence[] files = [.. _index.Files.Where(file =>
            file.IsMaintained && file.Role is "build-configuration" or "dependency-lock")];
        SeedFileEvidence[] canonical = [.. files.Where(_index.IsCanonical)];
        EvidenceFact[] configurations = [.. _index.FactsOfKind(EvidenceKinds.JavaScriptConfiguration)];
        EvidenceFact[] packages = [.. _index.FactsOfKind(EvidenceKinds.JavaScriptPackage)];
        EvidenceFact[] evidence = Evidence(
            files.Select(file => file.Fact),
            configurations,
            packages);
        if (evidence.Length == 0)
        {
            return;
        }

        decimal configFiles = canonical.Count(file => file.Role == "build-configuration") +
            configurations.Length;
        decimal lockfiles = canonical.Count(file => file.Role == "dependency-lock");
        decimal compilerOptions = configurations.Sum(fact =>
            SeedEvidenceIndex.Measurement(fact, "compiler-options"));
        decimal scripts = packages.Sum(fact => SeedEvidenceIndex.Measurement(fact, "scripts"));
        capabilities.Add(new CapabilityUnit
        {
            Id = "repository:build-tooling",
            RuleId = "build-tooling",
            Title = "Configure build behavior and developer tooling",
            Scope = ".",
            Quantity = decimal.Max(1m, configFiles + lockfiles),
            Drivers = Drivers(
                ("configuration-files", configFiles),
                ("lockfiles", lockfiles),
                ("compiler-options", compilerOptions),
                ("scripts", scripts)),
            Complexity = compilerOptions + scripts > 50m ? ComplexityLevel.High : ComplexityLevel.Routine,
            Reason = "Maintained build, compiler, dependency-lock, and script configuration represents setup, integration, and validation work beyond individual project manifests.",
            Evidence = evidence,
            Profiles = BothProfiles,
            CorrelationGroup = "repository:setup",
        });
    }

    private void AddCiAndInfrastructure(List<CapabilityUnit> capabilities)
    {
        SeedFileEvidence[] files = [.. _index.Files.Where(file =>
            file.IsMaintained && file.Role is "ci-configuration" or "infrastructure")];
        SeedFileEvidence[] canonical = [.. files.Where(_index.IsCanonical)];
        EvidenceFact[] aggregateFacts = Evidence(
            _index.FactsOfKind(EvidenceKinds.CiConfiguration),
            _index.FactsOfKind(EvidenceKinds.Infrastructure));
        EvidenceFact[] evidence = Evidence(files.Select(file => file.Fact), aggregateFacts);
        if (evidence.Length == 0)
        {
            return;
        }

        decimal ciFiles = canonical.Count(file => file.Role == "ci-configuration");
        decimal infrastructureFiles = canonical.Count(file => file.Role == "infrastructure");
        decimal physicalLines = canonical.Sum(file => file.PhysicalLines);
        if (canonical.Length == 0)
        {
            ciFiles = aggregateFacts
                .Where(fact => fact.Kind == EvidenceKinds.CiConfiguration)
                .Sum(fact => SeedEvidenceIndex.Measurement(fact, "files"));
            infrastructureFiles = aggregateFacts
                .Where(fact => fact.Kind == EvidenceKinds.Infrastructure)
                .Sum(fact => SeedEvidenceIndex.Measurement(fact, "files"));
            physicalLines = aggregateFacts.Sum(fact =>
                SeedEvidenceIndex.Measurement(fact, "physical-lines"));
        }

        capabilities.Add(new CapabilityUnit
        {
            Id = "repository:ci-infrastructure",
            RuleId = "ci-infrastructure",
            Title = "Implement represented CI/CD and infrastructure configuration",
            Scope = ".",
            Quantity = decimal.Max(1m, ciFiles + infrastructureFiles),
            Drivers = Drivers(
                ("ci-files", ciFiles),
                ("infrastructure-files", infrastructureFiles),
                ("physical-lines", physicalLines)),
            Complexity = infrastructureFiles > 10m ? ComplexityLevel.High : ComplexityLevel.Moderate,
            Reason = "CI/CD and infrastructure artifacts represent maintained automation, environment, and deployment behavior.",
            Evidence = evidence,
            Profiles = BothProfiles,
            CorrelationGroup = "repository:delivery",
        });
    }

    private void AddContainers(List<CapabilityUnit> capabilities)
    {
        SeedFileEvidence[] files = [.. _index.Files.Where(file =>
            file.IsMaintained && file.Role == "container-configuration")];
        SeedFileEvidence[] canonical = [.. files.Where(_index.IsCanonical)];
        EvidenceFact[] aggregateFacts = [.. _index.FactsOfKind(EvidenceKinds.ContainerConfiguration)];
        EvidenceFact[] evidence = Evidence(files.Select(file => file.Fact), aggregateFacts);
        if (evidence.Length == 0)
        {
            return;
        }

        decimal count = canonical.Length > 0
            ? canonical.Length
            : aggregateFacts.Sum(fact => SeedEvidenceIndex.Measurement(fact, "files"));
        decimal lines = canonical.Length > 0
            ? canonical.Sum(file => file.PhysicalLines)
            : aggregateFacts.Sum(fact => SeedEvidenceIndex.Measurement(fact, "physical-lines"));
        capabilities.Add(new CapabilityUnit
        {
            Id = "repository:containers",
            RuleId = "container-deployment",
            Title = "Create represented container build and orchestration behavior",
            Scope = ".",
            Quantity = decimal.Max(1m, count),
            Drivers = Drivers(
                ("container-files", count),
                ("physical-lines", lines)),
            Complexity = count > 5m ? ComplexityLevel.High : ComplexityLevel.Moderate,
            Reason = "Container definitions represent packaging, environment configuration, orchestration, and validation work.",
            Evidence = evidence,
            Profiles = BothProfiles,
            CorrelationGroup = "repository:delivery",
        });
    }

    private void AddPackaging(List<CapabilityUnit> capabilities)
    {
        SeedEstimationScope[] packagingScopes = [.. _index.Scopes.Where(scope =>
            scope.Fact.Tags.Any(tag => tag is
                "packable:declared-true" or "package:cli-bin" or "package:library-exports"))];
        if (packagingScopes.Length == 0)
        {
            return;
        }

        capabilities.Add(new CapabilityUnit
        {
            Id = "repository:packaging-release",
            RuleId = "packaging-release",
            Title = "Prepare represented packages and release surfaces",
            Scope = ".",
            Quantity = packagingScopes.Length,
            Drivers = Drivers(
                ("packaging-surfaces", packagingScopes.Length),
                ("release-configurations", 0m)),
            Complexity = packagingScopes.Length > 4 ? ComplexityLevel.High : ComplexityLevel.Moderate,
            Reason = "Packable projects, CLI bins, and library exports require packaging metadata, compatibility checks, and release preparation.",
            Evidence = Evidence(packagingScopes.Select(scope => scope.Fact)),
            Profiles = BothProfiles,
            CorrelationGroup = "repository:delivery",
        });
    }

    private void AddManualValidationAndReview(List<CapabilityUnit> capabilities)
    {
        foreach (SeedEstimationScope scope in _index.Scopes.Where(scope => scope.IsProduction))
        {
            EvidenceFact[] semantic = Evidence(
                _index.FactsOfKind(EvidenceKinds.EntryPoint).Where(fact => ScopeId(fact) == scope.Id),
                SemanticKinds.SelectMany(kind =>
                    _index.FactsOfKind(kind).Where(fact => ScopeId(fact) == scope.Id)));
            NormalizedEvidenceFact[] normalized = NormalizedSemanticFactsForScope(
                scope,
                includeEntryPoints: true);
            decimal runtimeSurfaces = RuntimeSurfaceCount(normalized);
            decimal specializedBoundaries = normalized
                .Select(fact => fact.Kind)
                .Where(kind => kind is
                    EvidenceKinds.DataAccess or EvidenceKinds.Integration or
                    EvidenceKinds.SecurityConfiguration or EvidenceKinds.BackgroundWork)
                .Distinct(StringComparer.Ordinal)
                .Count();
            EvidenceFact[] evidence = Evidence(_index.ScopeEvidence(scope), semantic);
            capabilities.Add(new CapabilityUnit
            {
                Id = $"scope:{scope.Id}:manual-validation",
                RuleId = "manual-validation",
                Title = $"Manually validate, debug, and harden {DisplayScope(scope)}",
                Scope = scope.Scope,
                Quantity = decimal.Max(1m, runtimeSurfaces + specializedBoundaries),
                Drivers = Drivers(
                    ("runnable-scopes", scope.IsRunnable ? 1m : 0m),
                    ("runtime-surfaces", runtimeSurfaces),
                    ("specialized-boundaries", specializedBoundaries)),
                Complexity = specializedBoundaries >= 3m ? ComplexityLevel.High : ComplexityLevel.Moderate,
                Reason = "Working behavior requires explicit manual validation, debugging, and hardening based on the observed runtime and external boundaries rather than a fixed percentage.",
                Evidence = evidence,
                Profiles = BothProfiles,
                CorrelationGroup = $"scope:{scope.Id}:validation",
                Assumptions = ["Automated-test authoring is valued separately."],
                UncertaintyReasons = ["The target checkout was not executed on the default static path."],
                ConfidencePenalty = 0.05m,
            });
        }

        SeedEstimationScope[] productionScopes = [.. _index.Scopes.Where(scope => scope.IsProduction)];
        EvidenceFact[] references = [.. _index.FactsOfKind(EvidenceKinds.ProjectReference)];
        EvidenceFact[] semanticEvidence = Evidence(SemanticKinds.SelectMany(kind =>
            _index.FactsOfKind(kind)));
        EvidenceFact[] reviewEvidence = Evidence(
            _index.RepositoryAnchorEvidence(),
            productionScopes.Select(scope => scope.Fact),
            references,
            semanticEvidence);
        if (reviewEvidence.Length == 0)
        {
            return;
        }

        int specializedBoundariesCount = SemanticKinds.Count(kind =>
            semanticEvidence.Any(fact => fact.Kind == kind));
        capabilities.Add(new CapabilityUnit
        {
            Id = "repository:self-review",
            RuleId = "self-review",
            Title = "Self-review and integrate the completed system",
            Scope = ".",
            Quantity = decimal.Max(1m, productionScopes.Length + references.Length),
            Drivers = Drivers(
                ("production-scopes", productionScopes.Length),
                ("reference-edges", references.Length),
                ("specialized-boundaries", specializedBoundariesCount)),
            Complexity = productionScopes.Length + references.Length > 12
                ? ComplexityLevel.High
                : ComplexityLevel.Moderate,
            Reason = "A solo senior contractor must review cross-scope behavior and integrate the completed artifact as explicit work.",
            Evidence = reviewEvidence,
            Profiles = BothProfiles,
            CorrelationGroup = "repository:system-integration",
        });
    }

    private List<CapabilityUnit> BuildProfessionalizationGap()
    {
        List<CapabilityUnit> gap = [];
        SeedEstimationScope[] productionScopes = [.. _index.Scopes.Where(scope => scope.IsProduction)];
        EvidenceFact[] fineTests = Evidence(
            _index.FactsOfKind(EvidenceKinds.DotNetTest),
            _index.FactsOfKind(EvidenceKinds.JavaScriptTest),
            _index.FactsOfKind(EvidenceKinds.TestSuite));
        EvidenceFact[] semantic = Evidence(SemanticKinds.SelectMany(kind =>
            _index.FactsOfKind(kind)));
        NormalizedEvidenceFact[] normalizedSemantic = NormalizedSemanticFacts();
        EvidenceFact[] anchors = Evidence(
            _index.RepositoryAnchorEvidence(),
            productionScopes.Select(scope => scope.Fact));

        if (productionScopes.Length > 0 && fineTests.Length == 0 && anchors.Length > 0)
        {
            decimal sourceHours = _sourceExpectedByScope.Values.Sum();
            decimal runtimeSurfaces = RuntimeSurfaceCount(normalizedSemantic);
            gap.Add(new CapabilityUnit
            {
                Id = "gap:repository:automated-tests",
                RuleId = "gap-automated-tests",
                Title = "Add representative automated tests",
                Scope = ".",
                Quantity = decimal.Max(1m, runtimeSurfaces),
                Drivers = Drivers(
                    ("production-backbone-hours", sourceHours),
                    ("runtime-surfaces", runtimeSurfaces)),
                Complexity = ComplexityLevel.Moderate,
                Reason = "Maintained production scopes exist but no automated-test artifacts were detected; this is conservative missing professionalization work, not represented EHE.",
                Evidence = Evidence(anchors, semantic),
                Profiles = BothProfiles,
                CorrelationGroup = "gap:repository:tests",
                Assumptions = ["Only representative baseline coverage is proposed; an idealized coverage target is not invented."],
                UncertaintyReasons = ["Absence evidence cannot determine the product's desired test strategy."],
                ConfidencePenalty = 0.12m,
            });
        }

        bool hasRuntimeUiOrApi = semantic.Any(fact => fact.Kind is
            EvidenceKinds.ApiSurface or EvidenceKinds.UserInterface);
        bool hasSystemTests = fineTests.Any(fact => fact.Tags.Any(tag => tag is
            "test-type:integration" or "test-type:component" or "test-type:end-to-end" or
            "integration" or "e2e"));
        if (hasRuntimeUiOrApi && !hasSystemTests && anchors.Length > 0)
        {
            EvidenceFact[] surfaceEvidence = [.. semantic.Where(fact => fact.Kind is
                EvidenceKinds.ApiSurface or EvidenceKinds.UserInterface)];
            decimal surfaces = RuntimeSurfaceCount(normalizedSemantic.Where(fact => fact.Kind is
                EvidenceKinds.ApiSurface or EvidenceKinds.UserInterface));
            gap.Add(new CapabilityUnit
            {
                Id = "gap:repository:system-tests",
                RuleId = "gap-system-tests",
                Title = "Add representative integration or end-to-end tests",
                Scope = ".",
                Quantity = decimal.Max(1m, surfaces),
                Drivers = Drivers(("runtime-surfaces", surfaces)),
                Complexity = ComplexityLevel.High,
                Reason = "API or UI behavior is represented without detected integration, component, or end-to-end tests; the work remains outside represented EHE.",
                Evidence = Evidence(anchors, surfaceEvidence, fineTests),
                Profiles = BothProfiles,
                CorrelationGroup = "gap:repository:tests",
                UncertaintyReasons = ["Static evidence cannot determine whether system-level coverage is required by the product's acceptance criteria."],
                ConfidencePenalty = 0.15m,
            });
        }

        bool hasDocumentation = _index.Files.Any(file =>
            file.IsMaintained && file.Role == "documentation") ||
            _index.FactsOfKind(EvidenceKinds.Documentation).Count > 0;
        if (!hasDocumentation && anchors.Length > 0)
        {
            gap.Add(new CapabilityUnit
            {
                Id = "gap:repository:documentation",
                RuleId = "gap-documentation",
                Title = "Add basic repository onboarding documentation",
                Scope = ".",
                Quantity = 1m,
                Complexity = ComplexityLevel.Routine,
                Reason = "No maintained documentation was detected; a conservative onboarding document is reported only as missing professionalization work.",
                Evidence = anchors,
                Profiles = BothProfiles,
                CorrelationGroup = "gap:repository:documentation",
                UncertaintyReasons = ["The intended audience and required documentation depth are unknown."],
                ConfidencePenalty = 0.08m,
            });
        }

        bool hasCi = _index.Files.Any(file =>
            file.IsMaintained && file.Role == "ci-configuration") ||
            _index.FactsOfKind(EvidenceKinds.CiConfiguration).Count > 0;
        if (!hasCi && anchors.Length > 0 &&
            (productionScopes.Length > 1 || productionScopes.Any(scope => scope.IsRunnable)))
        {
            gap.Add(new CapabilityUnit
            {
                Id = "gap:repository:ci",
                RuleId = "gap-ci",
                Title = "Add a basic continuous-integration workflow",
                Scope = ".",
                Quantity = 1m,
                Drivers = Drivers(("production-scopes", productionScopes.Length)),
                Complexity = ComplexityLevel.Routine,
                Reason = "A multi-scope or runnable repository has no detected CI configuration; a basic workflow is reported only as missing professionalization work.",
                Evidence = anchors,
                Profiles = BothProfiles,
                CorrelationGroup = "gap:repository:delivery",
                UncertaintyReasons = ["Organizational delivery requirements are outside repository evidence."],
                ConfidencePenalty = 0.1m,
            });
        }

        return gap;
    }

    private IEnumerable<FactAggregate> GroupByScope(string kind)
    {
        return _index.NormalizedFactsOfKind(kind)
            .Where(fact => _index.FindScope(fact.Facts[0]) is not null)
            .GroupBy(
                fact => _index.FindScope(fact.Facts[0])!.Id,
                StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => Aggregate(
                _index.FindScope(group.First().Facts[0])!,
                group));
    }

    private static FactAggregate Aggregate(
        SeedEstimationScope scope,
        IEnumerable<NormalizedEvidenceFact> candidates)
    {
        NormalizedEvidenceFact[] facts = [.. candidates];
        Dictionary<string, decimal> measurements = facts
            .SelectMany(fact => fact.Measurements)
            .GroupBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(pair => pair.Value),
                StringComparer.Ordinal);
        return new FactAggregate(
            scope,
            facts.Length,
            measurements,
            [.. facts
                .SelectMany(fact => fact.Tags)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)],
            Evidence(facts.SelectMany(fact => fact.Facts)),
            facts.Any(fact => fact.HasExactDuplicates));
    }

    private string? ScopeId(EvidenceFact fact) => _index.FindScope(fact)?.Id;

    private static string TestType(NormalizedEvidenceFact fact)
    {
        string? tagged = fact.Tags
            .FirstOrDefault(tag => tag.StartsWith("test-type:", StringComparison.Ordinal))?[10..];
        if (tagged is not null)
        {
            return tagged;
        }

        if (fact.Tags.Contains("e2e", StringComparer.OrdinalIgnoreCase))
        {
            return "end-to-end";
        }

        return fact.Tags.Contains("integration", StringComparer.OrdinalIgnoreCase)
            ? "integration"
            : "unit";
    }

    private static bool HasUnresolvedReference(EvidenceFact fact) =>
        fact.Tags.Any(tag => tag is
            "reference:unresolved" or "scope:outside" or "reference:missing");

    private static ComplexityLevel SourceComplexity(IReadOnlyDictionary<string, decimal> drivers)
    {
        decimal methods = drivers.GetValueOrDefault("methods") +
            drivers.GetValueOrDefault("functions");
        decimal branches = drivers.GetValueOrDefault("branch-points");
        decimal async = drivers.GetValueOrDefault("async-methods") +
            drivers.GetValueOrDefault("async-functions");
        if (methods == 0m)
        {
            return ComplexityLevel.Routine;
        }

        decimal branchDensity = branches / methods;
        decimal asyncDensity = async / methods;
        if (branchDensity >= 0.75m || asyncDensity >= 0.4m)
        {
            return ComplexityLevel.High;
        }

        return branchDensity < 0.12m && asyncDensity < 0.08m
            ? ComplexityLevel.Routine
            : ComplexityLevel.Moderate;
    }

    private NormalizedEvidenceFact[] NormalizedSemanticFactsForScope(
        SeedEstimationScope scope,
        bool includeEntryPoints = false) =>
    [
        .. (includeEntryPoints
                ? SemanticKinds.Prepend(EvidenceKinds.EntryPoint)
                : SemanticKinds)
            .SelectMany(kind => _index.NormalizedFactsOfKind(kind))
            .Where(fact => _index.FindScope(fact.Facts[0])?.Id == scope.Id),
    ];

    private NormalizedEvidenceFact[] NormalizedSemanticFacts() =>
    [
        .. SemanticKinds.SelectMany(kind => _index.NormalizedFactsOfKind(kind)),
    ];

    private static decimal RuntimeSurfaceCount(IEnumerable<NormalizedEvidenceFact> facts)
    {
        decimal count = 0m;
        foreach (NormalizedEvidenceFact fact in facts)
        {
            count += fact.Kind switch
            {
                EvidenceKinds.EntryPoint => 1m,
                EvidenceKinds.ApiSurface => decimal.Max(
                    1m,
                    SeedEvidenceIndex.Measurement(fact, "attributed-endpoints") +
                    SeedEvidenceIndex.Measurement(fact, "minimal-api-endpoints") +
                    SeedEvidenceIndex.Measurement(fact, "endpoints") +
                    SeedEvidenceIndex.Measurement(fact, "graphql-operations")),
                EvidenceKinds.UserInterface => decimal.Max(
                    1m,
                    SeedEvidenceIndex.Measurement(fact, "pages") +
                    SeedEvidenceIndex.Measurement(fact, "page-directives") +
                    SeedEvidenceIndex.Measurement(fact, "components") +
                    SeedEvidenceIndex.Measurement(fact, "ui-types")),
                EvidenceKinds.BackgroundWork => 1m,
                _ => 0m,
            };
        }

        return count;
    }

    private static Dictionary<string, decimal> Drivers(
        params (string Name, decimal Value)[] values) =>
        values.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);

    private static EvidenceFact[] Evidence(
        params IEnumerable<EvidenceFact>[] groups) =>
    [
        .. groups
            .SelectMany(group => group)
            .DistinctBy(fact => fact.Id, StringComparer.Ordinal)
            .OrderBy(fact => fact.Id, StringComparer.Ordinal),
    ];

    private static string DisplayScope(SeedEstimationScope scope) =>
        $"'{scope.Scope}' ({scope.Role})";

    private sealed record FactAggregate(
        SeedEstimationScope Scope,
        int NormalizedCount,
        IReadOnlyDictionary<string, decimal> Measurements,
        IReadOnlyList<string> Tags,
        IReadOnlyList<EvidenceFact> Evidence,
        bool HasExactDuplicates)
    {
        public decimal Measurement(string name) => Measurements.GetValueOrDefault(name);
    }
}
