using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Scripting;

internal static class ScriptFactFactory
{
    public static IEnumerable<EvidenceFact> Create(IReadOnlyList<ScriptFileAnalysis> files)
    {
        foreach (IGrouping<ScriptLanguage, ScriptFileAnalysis> languageGroup in files
            .GroupBy(file => file.Language)
            .OrderBy(group => group.Key))
        {
            ScriptFileAnalysis[] production = [.. languageGroup
                .Where(file => file.Role is ScriptRole.Product or ScriptRole.Module)
                .OrderBy(file => file.File.Scope, StringComparer.Ordinal)];
            if (production.Length > 0)
            {
                yield return Package(languageGroup.Key, production);
                yield return SourceStructure(languageGroup.Key, production);
                EvidenceFact? entryPoint = EntryPoint(languageGroup.Key, production);
                if (entryPoint is not null) yield return entryPoint;
            }

            foreach (IGrouping<string, ScriptFileAnalysis> tests in languageGroup
                .Where(file => file.Role == ScriptRole.Test)
                .GroupBy(file => TestType(file.File.Scope), StringComparer.Ordinal))
                yield return Tests(languageGroup.Key, tests.Key, [.. tests]);

            foreach (ScriptRole role in new[]
                { ScriptRole.Build, ScriptRole.Ci, ScriptRole.Delivery, ScriptRole.Infrastructure })
            {
                ScriptFileAnalysis[] automation = [.. languageGroup.Where(file => file.Role == role)];
                if (automation.Length > 0) yield return Automation(languageGroup.Key, role, automation);
            }

            foreach (EvidenceFact semantic in Semantics(languageGroup.Key, production))
                yield return semantic;
        }
    }

    private static EvidenceFact Package(
        ScriptLanguage language,
        ScriptFileAnalysis[] files)
    {
        string ecosystem = Ecosystem(language);
        string role = files.Any(file => file.Role == ScriptRole.Product) ? "cli" : "library";
        ScriptSourceMetrics[] metrics = [.. files.Select(file => file.Syntax.Metrics)];
        return ScriptEvidence.Fact(
            $"script:package:{ecosystem}:repository",
            EvidenceKinds.EcosystemPackage,
            ".",
            $"Static {Display(language)} product script and reusable module scope.",
            EvidenceSourceKind.Inferred,
            "common-scanner ownership plus bounded token-backed script classification",
            Locations(files),
            [
                ScriptEvidence.Measurement("source-files", files.Length, "files"),
                ScriptEvidence.Measurement("entry-points", metrics.Sum(item => item.TopLevelCommands > 0 ? 1 : 0), "surfaces"),
                ScriptEvidence.Measurement("modules", files.Count(file => file.Role == ScriptRole.Module), "modules"),
            ],
            [
                $"ecosystem:{ecosystem}",
                $"package-role:{role}",
                "scope:analyzed",
                "metadata:static-only",
                "script-execution:not-performed",
            ]);
    }

    private static EvidenceFact SourceStructure(
        ScriptLanguage language,
        ScriptFileAnalysis[] files)
    {
        ScriptSourceMetrics[] metrics = [.. files.Select(file => file.Syntax.Metrics)];
        string ecosystem = Ecosystem(language);
        return ScriptEvidence.Fact(
            $"script:source:{ecosystem}:repository",
            EvidenceKinds.SourceStructure,
            ".",
            $"Bounded token structure for maintained {Display(language)} product scripts and modules.",
            EvidenceSourceKind.Measured,
            "bounded managed tokenization with command and declaration structure analysis",
            Locations(files),
            [
                Measure("async-units", metrics, item => item.AsyncUnits, "symbols"),
                Measure("branch-points", metrics, item => item.BranchPoints, "branches"),
                Measure("conditionals", metrics, item => item.Conditionals, "branches"),
                Measure("dynamic-expansions", metrics, item => item.DynamicExpansions, "expressions"),
                Measure("error-handlers", metrics, item => item.ErrorHandlers, "usages"),
                Measure("external-commands", metrics, item => item.ExternalCommands, "commands"),
                Measure("file-operations", metrics, item => item.FileOperations, "operations"),
                ScriptEvidence.Measurement("files", files.Length, "files"),
                Measure("functions", metrics, item => item.Functions, "symbols"),
                Measure("here-documents", metrics, item => item.HereDocuments, "blocks"),
                Measure("loops", metrics, item => item.Loops, "loops"),
                Measure("methods", metrics, item => item.Methods, "symbols"),
                Measure("modules", metrics, item => item.ModuleOperations, "operations"),
                Measure("network-operations", metrics, item => item.NetworkOperations, "operations"),
                Measure("parameters", metrics, item => item.Parameters, "parameters"),
                Measure("pipelines", metrics, item => item.Pipelines, "pipelines"),
                Measure("process-operations", metrics, item => item.ProcessOperations, "operations"),
                Measure("public-symbols", metrics, item => item.PublicSymbols, "symbols"),
                Measure("requires-directives", metrics, item => item.RequiresDirectives, "directives"),
                Measure("sourced-files", metrics, item => item.SourcedFiles, "references"),
                Measure("types", metrics, item => item.Types, "symbols"),
            ],
            [.. AggregateTags(language, files, "script-role:product-or-module"), "structure:production-only"]);
    }

    private static EvidenceFact? EntryPoint(
        ScriptLanguage language,
        IReadOnlyList<ScriptFileAnalysis> files)
    {
        ScriptFileAnalysis[] entries = [.. files.Where(file =>
            file.Role == ScriptRole.Product &&
            (file.Syntax.Metrics.TopLevelCommands > 0 || file.Syntax.Metrics.HasShebang))];
        if (entries.Length == 0) return null;
        ScriptFileAnalysis[] canonical = CanonicalFiles(entries);
        string ecosystem = Ecosystem(language);
        return ScriptEvidence.Fact(
            $"script:entry:{ecosystem}:repository",
            EvidenceKinds.EntryPoint,
            ".",
            $"Static {Display(language)} command entry surfaces.",
            EvidenceSourceKind.Inferred,
            "top-level command, shebang, and automation-role exclusion",
            Locations(entries),
            [
                ScriptEvidence.Measurement("entry-points", canonical.Length, "surfaces"),
                ScriptEvidence.Measurement("commands", canonical.Sum(file => file.Syntax.Metrics.TopLevelCommands), "commands"),
            ],
            AggregateTags(language, entries, "script-role:product"));
    }

    private static EvidenceFact Tests(
        ScriptLanguage language,
        string testType,
        IReadOnlyList<ScriptFileAnalysis> files)
    {
        ScriptFileAnalysis[] canonical = CanonicalFiles(files);
        ScriptSourceMetrics[] metrics = [.. canonical.Select(file => file.Syntax.Metrics)];
        string ecosystem = Ecosystem(language);
        return ScriptEvidence.Fact(
            $"script:test:{ecosystem}:{testType}",
            EvidenceKinds.EcosystemTest,
            ".",
            $"Static {Display(language)} {testType.Replace('-', ' ')} test evidence.",
            EvidenceSourceKind.Measured,
            "test path, invocation role, framework command, assertion, and mock evidence",
            Locations(files),
            [
                MeasureAtLeastFiles("test-cases", canonical, metrics, item => item.TestCases, "cases"),
                Measure("assertions", metrics, item => item.Assertions, "assertions"),
                Measure("mock-usages", metrics, item => item.MockUsages, "calls"),
            ],
            [.. AggregateTags(language, files, "script-role:test"), $"test-type:{testType}"]);
    }

    private static EvidenceFact Automation(
        ScriptLanguage language,
        ScriptRole role,
        ScriptFileAnalysis[] files)
    {
        ScriptFileAnalysis[] canonical = CanonicalFiles(files);
        ScriptSourceMetrics[] metrics = [.. canonical.Select(file => file.Syntax.Metrics)];
        string roleName = RoleName(role);
        string kind = role switch
        {
            ScriptRole.Build => EvidenceKinds.BuildConfiguration,
            ScriptRole.Ci => EvidenceKinds.CiConfiguration,
            ScriptRole.Delivery => EvidenceKinds.DeliveryAutomation,
            ScriptRole.Infrastructure => EvidenceKinds.Infrastructure,
            _ => throw new ArgumentOutOfRangeException(nameof(role)),
        };
        return ScriptEvidence.Fact(
            $"script:{roleName}:{Ecosystem(language)}:repository",
            kind,
            ".",
            $"Static {Display(language)} {roleName} automation.",
            EvidenceSourceKind.Measured,
            "path, manifest invocation, and bounded command-structure classification",
            Locations(files),
            [
                Measure("branch-points", metrics, item => item.BranchPoints, "branches"),
                Measure("commands", metrics, item => item.ExternalCommands + item.CmdletCalls, "commands"),
                ScriptEvidence.Measurement("files", canonical.Length, "files"),
                Measure("pipelines", metrics, item => item.Pipelines, "pipelines"),
            ],
            AggregateTags(language, files, $"script-role:{roleName}"));
    }

    private static IEnumerable<EvidenceFact> Semantics(
        ScriptLanguage language,
        IReadOnlyList<ScriptFileAnalysis> files)
    {
        string ecosystem = Ecosystem(language);
        ScriptFileAnalysis[] network = [.. files.Where(file => file.Syntax.Metrics.NetworkOperations > 0)];
        if (network.Length > 0)
        {
            ScriptFileAnalysis[] canonical = CanonicalFiles(network);
            yield return ScriptEvidence.Fact(
                $"script:integration:{ecosystem}:repository",
                EvidenceKinds.Integration,
                ".",
                $"Static external command and network boundary evidence in {Display(language)} product scripts.",
                EvidenceSourceKind.Inferred,
                "literal command names matched bounded network and remote-service command families",
                Locations(network),
                [ScriptEvidence.Measurement("integration-calls", canonical.Sum(file => file.Syntax.Metrics.NetworkOperations), "calls")],
                [.. AggregateTags(language, network, "script-role:product-or-module"),
                    .. canonical.SelectMany(file => file.Syntax.Metrics.Technologies)
                        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
                        .Select(technology => $"technology:{technology}")]);
        }

        ScriptFileAnalysis[] security = [.. files.Where(file => file.Syntax.Metrics.CredentialCandidates > 0)];
        if (security.Length > 0)
        {
            ScriptFileAnalysis[] canonical = CanonicalFiles(security);
            yield return ScriptEvidence.Fact(
                $"script:security:{ecosystem}:repository",
                EvidenceKinds.SecurityConfiguration,
                ".",
                $"Static credential-handling surface in {Display(language)} product scripts.",
                EvidenceSourceKind.Inferred,
                "credential-shaped identifiers and secure-input commands without values or excerpts",
                Locations(security),
                [ScriptEvidence.Measurement("security-usages", canonical.Sum(file => file.Syntax.Metrics.CredentialCandidates), "usages")],
                AggregateTags(language, security, "script-role:product-or-module"));
        }

        ScriptFileAnalysis[] validation = [.. files.Where(file => file.Syntax.Metrics.ErrorHandlers > 0)];
        if (validation.Length > 0)
        {
            ScriptFileAnalysis[] canonical = CanonicalFiles(validation);
            yield return ScriptEvidence.Fact(
                $"script:validation:{ecosystem}:repository",
                EvidenceKinds.Validation,
                ".",
                $"Static error-handling and validation surface in {Display(language)} product scripts.",
                EvidenceSourceKind.Inferred,
                "strict-mode, trap, branch, exit, throw, catch, and error-action structure",
                Locations(validation),
                [ScriptEvidence.Measurement("validation-usages", canonical.Sum(file => file.Syntax.Metrics.ErrorHandlers), "usages")],
                AggregateTags(language, validation, "script-role:product-or-module"));
        }
    }

    private static IEnumerable<string> AggregateTags(
        ScriptLanguage language,
        IReadOnlyList<ScriptFileAnalysis> files,
        string roleTag)
    {
        yield return $"ecosystem:{Ecosystem(language)}";
        foreach (string dialect in files
            .Select(file => file.Syntax.Dialect)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal))
            yield return $"dialect:{dialect}";
        yield return "syntax:token-backed";
        yield return "parser:bounded-managed-tokenizer";
        yield return $"parser-confidence:{(files.Any(file => file.Syntax.Confidence == "low") ? "low" : "medium")}";
        yield return roleTag;
        yield return "source-excerpts:not-emitted";
        yield return "script-execution:not-performed";
        yield return "command-resolution:not-performed";
        yield return "platform-effects:not-observed";
        if (files.Any(file => file.Syntax.Metrics.HasDynamicInvocation))
            yield return "dynamic-invocation:unresolved";
        if (files.Any(file => file.Syntax.Metrics.HasUnresolvedSourcing))
            yield return "sourced-content:unresolved";
        if (CanonicalFiles(files).Length < files.Count)
            yield return "content-normalization:exact-duplicates";
    }

    private static ScriptFileAnalysis[] CanonicalFiles(
        IReadOnlyList<ScriptFileAnalysis> files) =>
    [
        .. files
            .GroupBy(
                file => ScriptEvidence.TagValue(file.File.Tags, "sha256:") ?? $"path:{file.File.Scope}",
                StringComparer.Ordinal)
            .Select(group => group.OrderBy(file => file.File.Scope, StringComparer.Ordinal).First())
            .OrderBy(file => file.File.Scope, StringComparer.Ordinal),
    ];

    private static IEnumerable<EvidenceLocation> Locations(IEnumerable<ScriptFileAnalysis> files) =>
        files.Select(file => ScriptEvidence.Location(file.File.Scope));

    private static EvidenceMeasurement Measure(
        string name,
        IEnumerable<ScriptSourceMetrics> metrics,
        Func<ScriptSourceMetrics, int> selector,
        string unit) => ScriptEvidence.Measurement(name, metrics.Sum(selector), unit);

    private static EvidenceMeasurement MeasureAtLeastFiles(
        string name,
        ScriptFileAnalysis[] files,
        IEnumerable<ScriptSourceMetrics> metrics,
        Func<ScriptSourceMetrics, int> selector,
        string unit) => ScriptEvidence.Measurement(name, Math.Max(files.Length, metrics.Sum(selector)), unit);

    private static string Ecosystem(ScriptLanguage language) => language switch
    {
        ScriptLanguage.Shell => "shell",
        ScriptLanguage.PowerShell => "powershell",
        _ => throw new ArgumentOutOfRangeException(nameof(language)),
    };

    private static string Display(ScriptLanguage language) => language == ScriptLanguage.Shell
        ? "shell/Bash"
        : "PowerShell";

    private static string RoleName(ScriptRole role) => role switch
    {
        ScriptRole.Ci => "ci",
        ScriptRole.Infrastructure => "infrastructure",
        ScriptRole.Delivery => "delivery",
        ScriptRole.Build => "build",
        ScriptRole.Test => "test",
        ScriptRole.Module => "module",
        _ => "product",
    };

    private static string TestType(string path)
    {
        string lower = path.ToLowerInvariant();
        if (lower.Contains("e2e", StringComparison.Ordinal) ||
            lower.Contains("smoke", StringComparison.Ordinal)) return "end-to-end";
        return lower.Contains("integration", StringComparison.Ordinal) ? "integration" : "unit";
    }
}
