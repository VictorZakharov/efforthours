using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.JavaScript;

internal static class FrontendAccessibilityAnalyzer
{
    private const int MaximumCount = 2_000;
    private const int MaximumUnits = 40;

    public static void AnalyzeElement(
        string tagName,
        ReadOnlySpan<char> attributes,
        FrontendMarkupMetrics metrics)
    {
        if (tagName == "label")
        {
            metrics.AccessibilityLabels = Cap(metrics.AccessibilityLabels + 1);
        }

        int index = 0;
        while (index < attributes.Length)
        {
            while (index < attributes.Length &&
                (char.IsWhiteSpace(attributes[index]) || attributes[index] == '/'))
            {
                index++;
            }

            int start = index;
            while (index < attributes.Length &&
                !char.IsWhiteSpace(attributes[index]) && attributes[index] is not ('=' or '>' or '/'))
            {
                index++;
            }

            if (start == index)
            {
                index++;
                continue;
            }

            string name = attributes[start..index].ToString().Trim('[', ']', '(', ')').ToLowerInvariant();
            if (name.StartsWith("attr.", StringComparison.Ordinal))
            {
                name = name[5..];
            }
            if (name.StartsWith("aria-", StringComparison.Ordinal))
            {
                metrics.AccessibilityAttributes = Cap(metrics.AccessibilityAttributes + 1);
                if (name is "aria-label" or "aria-labelledby" or "aria-describedby")
                {
                    metrics.AccessibilityLabels = Cap(metrics.AccessibilityLabels + 1);
                }

                if (name == "aria-live")
                {
                    metrics.AccessibilityLiveRegions = Cap(metrics.AccessibilityLiveRegions + 1);
                }
            }
            else if (name == "role")
            {
                metrics.AccessibilityAttributes = Cap(metrics.AccessibilityAttributes + 1);
            }
            else if (name == "alt")
            {
                metrics.AccessibilityAlternativeTexts = Cap(metrics.AccessibilityAlternativeTexts + 1);
            }
            else if (name is "for" or "htmlfor")
            {
                metrics.AccessibilityLabels = Cap(metrics.AccessibilityLabels + 1);
            }
            else if (name is "tabindex" or "autofocus")
            {
                metrics.AccessibilityFocusControls = Cap(metrics.AccessibilityFocusControls + 1);
            }
            else if (IsKeyboardHandler(name))
            {
                metrics.AccessibilityKeyboardInteractions = Cap(
                    metrics.AccessibilityKeyboardInteractions + 1);
            }

            SkipAttributeValue(attributes, ref index);
        }
    }

    public static void Complete(FrontendMarkupMetrics metrics)
    {
        int signals = metrics.AccessibilityAttributes +
            metrics.AccessibilityLabels +
            metrics.AccessibilityAlternativeTexts +
            metrics.AccessibilityKeyboardInteractions +
            metrics.AccessibilityLiveRegions +
            metrics.AccessibilityFocusControls;
        metrics.AccessibilityUnits = Math.Min(MaximumUnits, (signals + 2) / 3);
    }

    public static EvidenceFact? CreateFact(
        string id,
        string scope,
        string summary,
        string method,
        FrontendMarkupMetrics metrics,
        IEnumerable<EvidenceLocation> locations,
        IEnumerable<string>? tags = null)
    {
        if (metrics.AccessibilityUnits == 0)
        {
            return null;
        }

        return JavaScriptEvidence.Fact(
            id,
            EvidenceKinds.Accessibility,
            scope,
            summary,
            EvidenceSourceKind.Inferred,
            method,
            locations,
            [
                JavaScriptEvidence.Measurement("accessibility-attributes", metrics.AccessibilityAttributes, "attributes"),
                JavaScriptEvidence.Measurement("labels", metrics.AccessibilityLabels, "signals"),
                JavaScriptEvidence.Measurement("alternative-texts", metrics.AccessibilityAlternativeTexts, "attributes"),
                JavaScriptEvidence.Measurement("keyboard-interactions", metrics.AccessibilityKeyboardInteractions, "handlers"),
                JavaScriptEvidence.Measurement("live-regions", metrics.AccessibilityLiveRegions, "regions"),
                JavaScriptEvidence.Measurement("focus-controls", metrics.AccessibilityFocusControls, "controls"),
                JavaScriptEvidence.Measurement("accessibility-units", metrics.AccessibilityUnits, "units"),
            ],
            [
                "accessibility-analysis:explicit-static",
                "accessibility-conformance:not-proven",
                "semantic-analysis:bounded",
                .. (tags ?? []),
            ]);
    }

    private static bool IsKeyboardHandler(string name) =>
        IsEventHandler(name, "keydown") ||
        IsEventHandler(name, "keyup") ||
        IsEventHandler(name, "keypress");

    private static bool IsEventHandler(string name, string eventName) =>
        name == eventName ||
        name == "on" + eventName ||
        name == "@" + eventName ||
        name.StartsWith(eventName + ".", StringComparison.Ordinal) ||
        name.StartsWith("on" + eventName + ".", StringComparison.Ordinal) ||
        name.StartsWith("@" + eventName + ".", StringComparison.Ordinal) ||
        name.EndsWith(":" + eventName, StringComparison.Ordinal) ||
        name.Contains(":" + eventName + ".", StringComparison.Ordinal);

    private static void SkipAttributeValue(ReadOnlySpan<char> attributes, ref int index)
    {
        while (index < attributes.Length && char.IsWhiteSpace(attributes[index]))
        {
            index++;
        }

        if (index >= attributes.Length || attributes[index] != '=')
        {
            return;
        }

        index++;
        while (index < attributes.Length && char.IsWhiteSpace(attributes[index]))
        {
            index++;
        }

        if (index < attributes.Length && attributes[index] is '\'' or '"')
        {
            char quote = attributes[index++];
            while (index < attributes.Length && attributes[index] != quote)
            {
                index++;
            }

            index = Math.Min(attributes.Length, index + 1);
            return;
        }

        while (index < attributes.Length && !char.IsWhiteSpace(attributes[index]))
        {
            index++;
        }
    }

    private static int Cap(int value) => Math.Min(MaximumCount, value);
}
