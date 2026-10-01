using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.GDScript;

internal static class GDScriptFactFactory
{
    public static EvidenceFact Package(string directory, string? manifest, IReadOnlyList<GDScriptFileAnalysis> files) =>
        GDScriptEvidence.Fact(
            $"gdscript:package:{GDScriptEvidence.IdToken(directory)}",
            EvidenceKinds.EcosystemPackage,
            directory,
            "Static Godot project ownership for maintained GDScript.",
            manifest is null ? EvidenceSourceKind.Inferred : EvidenceSourceKind.Observed,
            "deepest scanner-admitted project.godot directory, with a repository-root fallback",
            manifest is null ? files.Take(1).Select(file => GDScriptEvidence.Location(file.File.Scope)) : [GDScriptEvidence.Location(manifest)],
            [GDScriptEvidence.Measurement("source-files", files.Count, "files")],
            ["ecosystem:gdscript", "technology:godot", "scope:analyzed", "metadata:static-only",
                files.Count > 0 && files.All(file => file.IsTest) ? "package-role:test" : "package-role:application"]);

    public static EvidenceFact Structure(string directory, IReadOnlyList<GDScriptFileAnalysis> files) =>
        GDScriptEvidence.Fact(
            $"gdscript:source:{GDScriptEvidence.IdToken(directory)}",
            EvidenceKinds.SourceStructure,
            directory,
            "Bounded GDScript declarations, signals, annotations, and decision structure.",
            EvidenceSourceKind.Measured,
            "managed token and indentation analysis of unique maintained production bodies",
            files.Select(file => GDScriptEvidence.Location(file.File.Scope)),
            new[] { GDScriptEvidence.Measurement("files", files.Count, "files") }
                .Concat(files.SelectMany(file => file.Syntax.Metrics)
                    .GroupBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(group => GDScriptEvidence.Measurement(group.Key, group.Sum(pair => pair.Value), "units"))),
            ["ecosystem:gdscript", "syntax:token-backed", "parser:bounded-managed-tokenizer",
                "structure:production-only", "structure:projection-normalized", "content-normalization:exact-duplicates",
                $"parser-confidence:{(files.Any(file => file.Syntax.Confidence == "low") ? "low" : "medium")}",
                "source-excerpts:not-emitted"]);

    public static EvidenceFact Test(string directory, GDScriptFileAnalysis file) => GDScriptEvidence.Fact(
        $"gdscript:test:{GDScriptEvidence.IdToken(file.File.Scope)}",
        EvidenceKinds.EcosystemTest,
        directory,
        "Static GDScript test declarations and assertions.",
        EvidenceSourceKind.Measured,
        "conventional test-file classification with named test_ functions and assertion calls",
        [GDScriptEvidence.Location(file.File.Scope)],
        [GDScriptEvidence.Measurement("test-cases", Math.Max(1, file.Syntax.Metrics.GetValueOrDefault("test-cases")), "cases"),
            GDScriptEvidence.Measurement("assertions", file.Syntax.Metrics.GetValueOrDefault("assertions"), "assertions")],
        ["ecosystem:gdscript", "syntax:token-backed", "source-excerpts:not-emitted",
            $"parser-confidence:{file.Syntax.Confidence}",
            $"test-type:{TestType(file.File.Scope)}"]);

    private static string TestType(string path)
    {
        string lower = path.ToLowerInvariant();
        if (lower.Contains("e2e", StringComparison.Ordinal) || lower.Contains("end_to_end", StringComparison.Ordinal)) return "end-to-end";
        return lower.Contains("integration", StringComparison.Ordinal) ? "integration" : "unit";
    }
}

internal sealed record GDScriptFileAnalysis(EvidenceFact File, GDScriptSyntaxAnalysis Syntax)
{
    public bool IsTest => File.Tags.Contains("classification:test", StringComparer.Ordinal);
}
