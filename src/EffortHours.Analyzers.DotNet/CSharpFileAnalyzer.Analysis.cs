using System.Security.Cryptography;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Contracts.V1;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ContractDiagnostic = EffortHours.Contracts.V1.Diagnostic;
using ContractDiagnosticSeverity = EffortHours.Contracts.V1.DiagnosticSeverity;

namespace EffortHours.Analyzers.DotNet;

internal sealed partial class CSharpFileAnalyzer
{
    private static CSharpFileAnalysis AnalyzeParsed(
        SyntaxTree tree,
        int syntaxErrors,
        string relativePath,
        string projectScope,
        bool isTestFile,
        CancellationToken cancellationToken)
    {
        CompilationUnitSyntax root = (CompilationUnitSyntax)tree.GetRoot(cancellationToken);
        List<ContractDiagnostic> diagnostics = [];
        if (syntaxErrors > 0)
        {
            diagnostics.Add(DotNetEvidence.Diagnostic(
                "FB3100",
                ContractDiagnosticSeverity.Warning,
                $"Roslyn reported {syntaxErrors} syntax error(s) in '{relativePath}'; partial evidence was retained.",
                relativePath));
        }

        CSharpSyntaxInventory inventory = CSharpSyntaxInventory.Create(root);
        CSharpReachabilityResult reachability = syntaxErrors == 0
            ? CSharpReachabilityAnalyzer.Analyze(
                root,
                relativePath,
                projectScope,
                inventory.Methods)
            : CSharpReachabilityResult.Empty;
        inventory = inventory.WithoutExcluded(reachability);
        IReadOnlyList<CallableStructuralMetric> detectedCallableMetrics =
            CSharpCallableStructuralAnalyzer.Analyze(inventory.Methods);
        CSharpStructureMetrics structure = new(
            Files: 1,
            Types: inventory.Types.Count + inventory.Delegates.Count,
            PublicTypes: inventory.Types.Count(IsPublic) + inventory.Delegates.Count(IsPublic),
            Methods: inventory.Methods.Count,
            PublicMethods: inventory.Methods.Count(IsPublic),
            AsyncMethods: inventory.Methods.Count(method =>
                method.Modifiers.Any(SyntaxKind.AsyncKeyword)),
            BranchPoints: inventory.BranchPoints,
            StructuralParserBackedFiles: syntaxErrors == 0 ? 1 : 0,
            StructuralDetectedCallables: detectedCallableMetrics.Count,
            CallableStructuralMetrics: syntaxErrors == 0 ? detectedCallableMetrics : []);

        List<EvidenceFact> facts = [];
        if (reachability.ExclusionFact is not null)
        {
            facts.Add(reachability.ExclusionFact);
        }

        CSharpApplicationBoundaryAnalyzer.AddFacts(
            facts,
            relativePath,
            projectScope,
            inventory);
        CSharpDataEvidenceAnalyzer.AddFact(facts, relativePath, projectScope, inventory);
        CSharpServiceBoundaryAnalyzer.AddFacts(
            facts,
            relativePath,
            projectScope,
            inventory,
            root);
        AddValidationFact(facts, relativePath, projectScope, inventory);
        AddTestFact(facts, relativePath, projectScope, inventory, isTestFile);
        AddUiFact(facts, relativePath, projectScope, inventory);

        return new CSharpFileAnalysis(structure, facts, diagnostics);
    }

    private static string AnalysisArtifactKey(
        string contentId,
        string expectedSha256,
        string relativePath,
        string projectScope,
        bool isTestFile)
    {
        string identity = string.Join(
            '\0',
            contentId,
            expectedSha256,
            relativePath,
            projectScope,
            isTestFile ? "test" : "source");
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))
            .ToLowerInvariant();
        return $"dotnet-csharp/{DotNetEvidence.AnalyzerVersion}/{digest}";
    }
}
