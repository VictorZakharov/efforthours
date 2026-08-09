namespace EffortHours.Analyzers.JavaScript;

internal static class JavaScriptEntrypointClassifier
{
    public static void AddSignal(
        string path,
        JavaScriptPackageModel? package,
        JavaScriptTokenization tokens,
        JavaScriptSourceMetrics metrics,
        bool isTestFile)
    {
        if (isTestFile || IsBenchmarkPath(path))
        {
            // Token analysis records a raw hashbang signal before package/path context is known.
            // Development-only files must not retain that signal as a product entry point.
            metrics.EntryPointLine = null;
            return;
        }

        if (tokens.HasHashBang)
        {
            metrics.EntryPointLine = 1;
            return;
        }

        if (package?.Role is not ("application" or "server" or "full-stack-web" or "cli" or "worker"))
        {
            return;
        }

        string relativeToPackage = package.Scope == "."
            ? path
            : path.StartsWith(package.Scope + "/", StringComparison.Ordinal)
                ? path[(package.Scope.Length + 1)..]
                : path;
        string directory = relativeToPackage.Contains('/')
            ? relativeToPackage[..relativeToPackage.LastIndexOf('/')]
            : ".";
        string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        if ((directory == "." || directory == "src") &&
            name is "index" or "main" or "server" or "cli" or "worker")
        {
            metrics.EntryPointLine = 1;
        }
    }

    private static bool IsBenchmarkPath(string path)
    {
        string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment.Equals("bench", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("benchmark", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("benchmarks", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("performance", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        string name = Path.GetFileNameWithoutExtension(path);
        return name.Equals("bench", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("benchmark", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".bench", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".benchmark", StringComparison.OrdinalIgnoreCase);
    }
}
