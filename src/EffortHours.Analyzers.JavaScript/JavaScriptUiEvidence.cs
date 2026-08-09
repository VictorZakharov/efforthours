using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.JavaScript;

internal static class JavaScriptUiEvidence
{
    public static void AddFact(
        List<EvidenceFact> facts,
        string path,
        string packageScope,
        string syntaxTag,
        JavaScriptSourceMetrics metrics)
    {
        bool hasStructuralUi = metrics.UiComponents > 0 ||
            metrics.UiPages > 0 ||
            metrics.JsxElements > 0;
        bool hasFrameworkUiBehavior = HasUiFramework(metrics) &&
            (metrics.StateUsages > 0 || metrics.EffectUsages > 0 || metrics.FormUsages > 0);
        if (!hasStructuralUi && !hasFrameworkUiBehavior)
        {
            return;
        }

        facts.Add(JavaScriptEvidence.Fact(
            $"javascript:ui:{path}",
            EvidenceKinds.UserInterface,
            packageScope,
            $"Web UI component or page structure detected in '{path}'.",
            EvidenceSourceKind.Inferred,
            "AST or token structure, UI-framework context, component, page-path, and state classification",
            [JavaScriptEvidence.Location(path, metrics.UiLine)],
            [
                JavaScriptEvidence.Measurement("components", metrics.UiComponents, "components"),
                JavaScriptEvidence.Measurement("pages", metrics.UiPages, "pages"),
                JavaScriptEvidence.Measurement("jsx-elements", metrics.JsxElements, "elements"),
                JavaScriptEvidence.Measurement("state-usages", metrics.StateUsages, "usages"),
                JavaScriptEvidence.Measurement("effect-usages", metrics.EffectUsages, "usages"),
                JavaScriptEvidence.Measurement("form-usages", metrics.FormUsages, "usages"),
            ],
            [syntaxTag, .. UiTechnologyTags(metrics)]));
    }

    private static bool HasUiFramework(JavaScriptSourceMetrics metrics) =>
        metrics.TechnologyFamilies.Contains("ui") ||
        metrics.TechnologyFamilies.Contains("full-stack");

    private static IEnumerable<string> UiTechnologyTags(JavaScriptSourceMetrics metrics) =>
        HasUiFramework(metrics)
            ? metrics.Technologies.Select(technology => $"technology:{technology}")
            : [];
}
