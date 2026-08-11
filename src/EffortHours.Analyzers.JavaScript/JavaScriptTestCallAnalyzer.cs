namespace EffortHours.Analyzers.JavaScript;

internal static class JavaScriptTestCallAnalyzer
{
    public static void Analyze(
        JavaScriptTokenization tokenization,
        int nameIndex,
        int line,
        JavaScriptSourceMetrics metrics)
    {
        if (IsAny(tokenization, nameIndex, "test", "it"))
        {
            metrics.TestCases++;
            metrics.TestLine ??= line;
        }
        else if (IsAny(tokenization, nameIndex, "describe", "suite"))
        {
            metrics.TestSuites++;
            metrics.TestLine ??= line;
        }
        else if (IsAny(tokenization, nameIndex, "expect", "assert", "assertThat"))
        {
            metrics.Assertions++;
            metrics.TestLine ??= line;
        }
        else if (IsAny(tokenization, nameIndex, "mock", "spyOn", "stub", "vi", "jest"))
        {
            metrics.MockUsages++;
            metrics.TestLine ??= line;
        }

        if (IsAny(tokenization, nameIndex, "render", "renderHook", "mount", "shallow") &&
            metrics.TechnologyFamilies.Contains("test-component"))
        {
            metrics.ComponentTestUsages++;
            metrics.TestLine ??= line;
        }

        if (tokenization.Is(nameIndex, "request") && metrics.Technologies.Contains("supertest"))
        {
            metrics.IntegrationTestUsages++;
            metrics.TestLine ??= line;
        }

        if (IsAny(tokenization, nameIndex, "goto", "visit", "locator") &&
            metrics.TechnologyFamilies.Contains("test-e2e"))
        {
            metrics.EndToEndTestUsages++;
            metrics.TestLine ??= line;
        }

        if (IsAccessibilityCall(tokenization, nameIndex, metrics))
        {
            metrics.AccessibilityTestUsages++;
            metrics.TestLine ??= line;
        }
    }

    private static bool IsAccessibilityCall(
        JavaScriptTokenization tokenization,
        int nameIndex,
        JavaScriptSourceMetrics metrics) =>
        metrics.TechnologyFamilies.Any(family => family is
            "test-component" or "test-e2e" or
            "test-accessibility-component" or "test-accessibility-e2e") &&
        IsAny(
            tokenization,
            nameIndex,
            "axe", "checkA11y", "injectAxe", "toHaveNoViolations",
            "getByRole", "getByLabelText", "findByRole", "findByLabelText",
            "keyboard", "focus");

    private static bool IsAny(
        JavaScriptTokenization tokenization,
        int index,
        params ReadOnlySpan<string> values)
    {
        foreach (string value in values)
        {
            if (tokenization.Is(index, value))
            {
                return true;
            }
        }

        return false;
    }
}
