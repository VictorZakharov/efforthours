namespace EffortHours.Analyzers.Cpp;

internal static class CppProjectResolver
{
    public static IReadOnlyList<CppFileAnalysis> Resolve(
        IReadOnlyList<CppRawFileAnalysis> files,
        IReadOnlyList<CppProjectModel> projects)
    {
        HashSet<string> availablePaths = files.Select(file => file.File.Scope)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, Assignment> assignments = files.ToDictionary(
            file => file.File.Scope,
            file => Initial(file.File.Scope, projects),
            StringComparer.Ordinal);

        Dictionary<string, IReadOnlyList<string>> includes = [];
        foreach (CppRawFileAnalysis file in files)
        {
            Assignment owner = assignments[file.File.Scope];
            includes[file.File.Scope] = ResolveIncludes(
                file.File.Scope,
                file.Syntax.Preprocessor.Includes,
                owner.Project,
                availablePaths);
        }

        foreach (CppRawFileAnalysis header in files.Where(file => CppPath.IsHeader(file.File.Scope)))
        {
            CppProjectModel[] includeOwners = [.. files
                .Where(file => includes[file.File.Scope].Contains(header.File.Scope, StringComparer.Ordinal))
                .Select(file => assignments[file.File.Scope].Project)
                .DistinctBy(project => project.Directory, StringComparer.Ordinal)];
            Assignment current = assignments[header.File.Scope];
            bool explicitOwner = current.Project.ExplicitSources.Contains(header.File.Scope, StringComparer.Ordinal);
            if (!explicitOwner && includeOwners.Length == 1)
                assignments[header.File.Scope] = new Assignment(includeOwners[0], current.Ambiguous);
            else if (!explicitOwner && includeOwners.Length > 1)
                assignments[header.File.Scope] = current with { Ambiguous = true };
        }

        return [.. files.Select(file => new CppFileAnalysis(
                file.File,
                assignments[file.File.Scope].Project,
                file.Syntax,
                includes[file.File.Scope],
                assignments[file.File.Scope].Ambiguous))
            .OrderBy(file => file.File.Scope, StringComparer.Ordinal)];
    }

    public static CppProjectModel PreliminaryOwner(
        string path,
        IReadOnlyList<CppProjectModel> projects) => Initial(path, projects).Project;

    private static Assignment Initial(string path, IReadOnlyList<CppProjectModel> projects)
    {
        CppProjectModel[] explicitOwners = [.. projects
            .Where(project => project.ExplicitSources.Contains(path, StringComparer.Ordinal))
            .OrderByDescending(project => project.Directory.Length)
            .ThenBy(project => project.Directory, StringComparer.Ordinal)];
        if (explicitOwners.Length > 0)
            return new Assignment(explicitOwners[0], explicitOwners.Length > 1);
        CppProjectModel project = projects
            .Where(item => CppPath.IsWithin(path, item.Directory))
            .OrderByDescending(item => item.Directory.Length)
            .ThenBy(item => item.Directory, StringComparer.Ordinal)
            .FirstOrDefault() ?? projects.OrderBy(item => item.Directory, StringComparer.Ordinal).First();
        return new Assignment(project, false);
    }

    private static IReadOnlyList<string> ResolveIncludes(
        string sourcePath,
        IReadOnlyList<CppInclude> includes,
        CppProjectModel project,
        HashSet<string> availablePaths)
    {
        HashSet<string> resolved = new(StringComparer.Ordinal);
        string sourceDirectory = CppPath.Directory(sourcePath);
        foreach (CppInclude include in includes)
        {
            List<string> candidates = [];
            if (include.Quoted)
            {
                string? local = CppPath.ResolveWithinRepository(sourceDirectory, include.Value);
                if (local is not null && availablePaths.Contains(local)) candidates.Add(local);
            }
            foreach (string root in project.IncludeRoots.Prepend(project.Directory).Distinct(StringComparer.Ordinal))
            {
                string? candidate = CppPath.ResolveWithinRepository(root, include.Value);
                if (candidate is not null && availablePaths.Contains(candidate)) candidates.Add(candidate);
            }
            string[] unique = [.. candidates.Distinct(StringComparer.Ordinal)];
            if (unique.Length == 1) resolved.Add(unique[0]);
        }
        return [.. resolved.Order(StringComparer.Ordinal)];
    }

    private sealed record Assignment(CppProjectModel Project, bool Ambiguous);
}
