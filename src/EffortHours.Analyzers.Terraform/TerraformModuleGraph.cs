using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Terraform;

internal sealed record TerraformAnalyzedFile(
    EvidenceFact File,
    TerraformFileAnalysis Analysis,
    bool ExactDuplicate,
    string ModuleDirectory);

internal sealed record TerraformModuleModel(
    string Directory,
    IReadOnlyList<TerraformAnalyzedFile> Files)
{
    public IReadOnlyList<TerraformAnalyzedFile> CanonicalFiles =>
        [.. Files.Where(file => !file.ExactDuplicate)];
}

internal sealed record TerraformLocalModuleReference(
    string SourceDirectory,
    string? TargetDirectory,
    string SourcePath,
    int Line,
    bool OutsideRepository,
    bool MissingTarget);

internal static class TerraformModuleGraph
{
    public static IReadOnlyList<TerraformModuleModel> Build(
        IReadOnlyList<(EvidenceFact File, TerraformFileAnalysis Analysis, bool ExactDuplicate)> files)
    {
        HashSet<string> moduleDirectories = files
            .Where(item => DefinesModule(item.File.Scope, item.Analysis.Artifact))
            .Select(item => Directory(item.File.Scope))
            .ToHashSet(StringComparer.Ordinal);
        if (moduleDirectories.Count == 0 && files.Count > 0) moduleDirectories.Add(".");

        TerraformAnalyzedFile[] owned = [.. files.Select(item => new TerraformAnalyzedFile(
            item.File,
            item.Analysis,
            item.ExactDuplicate,
            ResolveOwner(item.File.Scope, moduleDirectories)))];
        return [.. owned
            .GroupBy(file => file.ModuleDirectory, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new TerraformModuleModel(
                group.Key,
                [.. group.OrderBy(file => file.File.Scope, StringComparer.Ordinal)]))];
    }

    public static IReadOnlyList<TerraformLocalModuleReference> ResolveLocalReferences(
        IReadOnlyList<TerraformModuleModel> modules)
    {
        HashSet<string> known = modules.Select(module => module.Directory)
            .ToHashSet(StringComparer.Ordinal);
        List<TerraformLocalModuleReference> references = [];
        foreach (TerraformModuleModel module in modules)
        {
            foreach (TerraformAnalyzedFile file in module.CanonicalFiles)
            {
                foreach (TerraformModuleSource source in file.Analysis.Metrics.ModuleSources
                    .Where(source => source.Kind == "local"))
                {
                    bool outside = false;
                    string? target = source.Literal is null
                        ? null
                        : ResolveRelative(module.Directory, source.Literal, out outside);
                    bool outsideRepository = source.Literal is not null && outside;
                    bool missing = target is not null && !known.Contains(target);
                    references.Add(new TerraformLocalModuleReference(
                        module.Directory,
                        target,
                        file.File.Scope,
                        source.Line,
                        outsideRepository,
                        missing));
                }
            }
        }

        return references;
    }

    private static bool DefinesModule(string path, TerraformArtifactAssessment artifact) =>
        !artifact.IsTest && !artifact.IsVariableValues && !artifact.IsCliConfiguration &&
        (Path.GetExtension(path).Equals(".tf", StringComparison.OrdinalIgnoreCase) ||
         Path.GetExtension(path).Equals(".hcl", StringComparison.OrdinalIgnoreCase));

    private static string ResolveOwner(string path, IReadOnlySet<string> modules)
    {
        string directory = Directory(path);
        return modules
            .Where(module => IsWithin(directory, module))
            .OrderByDescending(module => module.Length)
            .ThenBy(module => module, StringComparer.Ordinal)
            .FirstOrDefault() ?? directory;
    }

    private static string? ResolveRelative(string directory, string source, out bool outside)
    {
        outside = false;
        string normalizedSource = source.Replace('\\', '/').Split('?')[0].TrimEnd('/');
        if (normalizedSource.StartsWith('/') ||
            normalizedSource.Contains(':'))
        {
            outside = true;
            return null;
        }

        List<string> segments = directory == "."
            ? []
            : [.. directory.Split('/', StringSplitOptions.RemoveEmptyEntries)];
        foreach (string segment in normalizedSource.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    outside = true;
                    return null;
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        return segments.Count == 0 ? "." : string.Join('/', segments);
    }

    private static bool IsWithin(string path, string directory) =>
        directory == "." || path == directory ||
        path.StartsWith(directory + "/", StringComparison.Ordinal);

    private static string Directory(string path)
    {
        int separator = path.LastIndexOf('/');
        return separator < 0 ? "." : path[..separator];
    }
}
