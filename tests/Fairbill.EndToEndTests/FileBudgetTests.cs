using System.Text.Json;

namespace Fairbill.EndToEndTests;

public sealed class FileBudgetTests
{
    [Fact]
    public void CSharpFilesRespectRatchetBudgets()
    {
        string repositoryRoot = FindRepositoryRoot();
        string manifestPath = Path.Combine(repositoryRoot, "eng", "file-budgets.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement root = document.RootElement;
        Assert.Equal("1.0.0", root.GetProperty("schemaVersion").GetString());
        int defaultBudget = root.GetProperty("defaultMaxLines").GetInt32();
        Dictionary<string, int> directoryBudgets = ReadBudgets(
            root.GetProperty("directoryBudgets"));
        Dictionary<string, int> fileBudgets = ReadBudgets(root.GetProperty("fileBudgets"));
        List<string> failures = [];
        HashSet<string> discovered = new(StringComparer.Ordinal);

        foreach (string topLevel in new[] { "src", "tests", "benchmarks" })
        {
            string path = Path.Combine(repositoryRoot, topLevel);
            foreach (string file in Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');
                if (relativePath.Split('/').Any(segment => segment is "bin" or "obj"))
                {
                    continue;
                }

                discovered.Add(relativePath);
                int budget = ResolveBudget(
                    relativePath,
                    defaultBudget,
                    directoryBudgets,
                    fileBudgets);
                int lines = File.ReadLines(file).Count();
                if (lines > budget)
                {
                    failures.Add($"{relativePath}: {lines} lines exceeds {budget}");
                }
            }
        }

        string[] staleOverrides = [.. fileBudgets.Keys
            .Where(path => !discovered.Contains(path))
            .Order(StringComparer.Ordinal)];
        Assert.True(
            failures.Count == 0 && staleOverrides.Length == 0,
            string.Join(
                Environment.NewLine,
                failures.Concat(staleOverrides.Select(path => $"stale file-budget override: {path}"))));
    }

    private static Dictionary<string, int> ReadBudgets(JsonElement element)
    {
        Dictionary<string, int> budgets = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            int budget = property.Value.GetInt32();
            Assert.True(budget > 0, $"Budget for '{property.Name}' must be positive.");
            budgets.Add(property.Name.Replace('\\', '/').TrimEnd('/'), budget);
        }

        return budgets;
    }

    private static int ResolveBudget(
        string path,
        int defaultBudget,
        IReadOnlyDictionary<string, int> directoryBudgets,
        Dictionary<string, int> fileBudgets)
    {
        if (fileBudgets.TryGetValue(path, out int exact))
        {
            return exact;
        }

        return directoryBudgets
            .Where(pair => path.StartsWith(pair.Key + "/", StringComparison.Ordinal))
            .OrderByDescending(pair => pair.Key.Length)
            .Select(pair => pair.Value)
            .FirstOrDefault(defaultBudget);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Fairbill.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the Fairbill repository root.");
    }
}
