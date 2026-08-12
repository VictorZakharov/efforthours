using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Rust;

internal sealed class CargoManifestReader(RustTextReader textReader)
{
    public async Task<CargoReadResult> ReadAsync(
        IReadOnlyList<EvidenceFact> fileFacts,
        bool hasMaintainedSource,
        CancellationToken cancellationToken)
    {
        List<Diagnostic> diagnostics = [];
        List<CargoManifestMetadata> manifests = [];
        foreach (EvidenceFact fact in SelectManifests(fileFacts, diagnostics))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RustTextReadResult read = await textReader.ReadAsync(fact, cancellationToken)
                .ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
                continue;
            }

            CargoManifestMetadata parsed = CargoTomlParser.Parse(read.Text!, fact.Scope);
            manifests.Add(parsed);
            if (parsed.Malformed)
                diagnostics.Add(RustEvidence.Diagnostic(
                    "FB8803",
                    DiagnosticSeverity.Warning,
                    $"Cargo manifest '{fact.Scope}' contains unsupported or malformed static TOML; only unambiguous literal metadata was retained.",
                    fact.Scope));
        }

        List<CargoPackageModel> packages = [.. manifests
            .Where(manifest => manifest.HasPackage || manifest.HasWorkspace)
            .Select(manifest => BuildPackage(manifest, fileFacts))];
        ApplyWorkspaceMembers(packages, manifests);
        bool hasOrphanSource = hasMaintainedSource && fileFacts.Any(file =>
            IsMaintainedRust(file) && !packages.Any(package =>
                RustPath.IsWithin(file.Scope, package.Directory)));
        if ((packages.Count == 0 && hasMaintainedSource) || hasOrphanSource)
            packages.Add(new CargoPackageModel
            {
                Directory = ".",
                Name = "repository",
                Role = "library",
            });

        return new CargoReadResult(
            [.. packages.OrderBy(package => package.Directory, StringComparer.Ordinal)],
            diagnostics);
    }

    private static List<EvidenceFact> SelectManifests(
        IReadOnlyList<EvidenceFact> files,
        List<Diagnostic> diagnostics)
    {
        List<EvidenceFact> selected = [];
        foreach (IGrouping<string, EvidenceFact> group in files
            .Where(IsManifest)
            .GroupBy(file => RustPath.Directory(file.Scope), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            EvidenceFact[] candidates = [.. group.OrderBy(file => file.Scope, StringComparer.Ordinal)];
            selected.Add(candidates[0]);
            if (candidates.Length > 1)
                diagnostics.Add(RustEvidence.Diagnostic(
                    "FB8807",
                    DiagnosticSeverity.Warning,
                    $"Multiple case-insensitive Cargo.toml candidates were found in '{group.Key}'; only '{candidates[0].Scope}' was analyzed.",
                    candidates[0].Scope));
        }

        return selected;
    }

    private static CargoPackageModel BuildPackage(
        CargoManifestMetadata metadata,
        IReadOnlyList<EvidenceFact> files)
    {
        string name = metadata.Name ??
            (metadata.Directory == "." ? "workspace" : Path.GetFileName(metadata.Directory));
        List<CargoTarget> targets = [.. metadata.Targets.Select(target =>
            new CargoTarget(target.Kind, target.Name, TargetPath(target, metadata.Directory)))];
        if (metadata.HasLib)
            targets.Add(new CargoTarget("lib", metadata.LibName,
                metadata.LibPath is null ? "src/lib.rs" : TargetPath(metadata.LibPath, metadata.Directory)));
        AddConventionalTargets(targets, metadata, files);
        string? configuredBuildPath = metadata.ExplicitBuild is null
            ? "build.rs"
            : TargetPath(metadata.ExplicitBuild, metadata.Directory);
        string? declaredBuildPath = metadata.BuildDisabled || metadata.BuildScripts == 0
            ? null
            : configuredBuildPath;
        int buildScripts = metadata.BuildDisabled ? 0 : Math.Max(
            metadata.BuildScripts,
            configuredBuildPath is not null && HasFile(files, metadata.Directory, configuredBuildPath) ? 1 : 0);
        List<CargoDependency> dependencies = [];
        int unresolvedPaths = 0;
        foreach (CargoDependencyBuilder dependency in metadata.Dependencies)
        {
            string? candidate = dependency.Path is null
                ? null
                : RustPath.ResolveWithinRepository(metadata.Directory, dependency.Path);
            string? resolved = candidate is not null && HasFile(files, candidate, "Cargo.toml")
                ? candidate
                : null;
            if (dependency.Path is not null && resolved is null) unresolvedPaths++;
            dependencies.Add(new CargoDependency(
                dependency.Name,
                dependency.Kind,
                dependency.PackageName,
                resolved,
                dependency.WorkspaceInherited));
        }

        return new CargoPackageModel
        {
            Directory = metadata.Directory,
            Name = name,
            Role = targets.Any(target => target.Kind == "bin") ? "application" :
                metadata.HasWorkspace && !metadata.HasPackage ? "workspace" : "library",
            ManifestPaths = [metadata.Path],
            Dependencies = [.. dependencies
                .Distinct().OrderBy(item => item.Kind, StringComparer.Ordinal)
                .ThenBy(item => item.Name, StringComparer.Ordinal)],
            Targets = [.. targets
                .GroupBy(item => (item.Kind, item.Path), TargetIdentityComparer.Instance)
                .Select(group => group.OrderByDescending(item => item.Name is not null).First())
                .OrderBy(item => item.Kind, StringComparer.Ordinal)
                .ThenBy(item => item.Path, StringComparer.Ordinal)],
            Features = metadata.Features,
            BuildScripts = buildScripts,
            CrateTypes = metadata.CrateTypes,
            UnresolvedValues = metadata.UnresolvedValues +
                unresolvedPaths + metadata.DefaultMemberPatterns.Count +
                metadata.MemberPatterns.Count(pattern => !HasMatchingManifest(
                    files, metadata.Directory, pattern)) +
                (metadata.ExplicitBuild is not null && configuredBuildPath is null ? 1 : 0) +
                (declaredBuildPath is not null && !HasFile(files, metadata.Directory, declaredBuildPath) ? 1 : 0) +
                targets.Count(target => target.Path is null ||
                    !HasFile(files, metadata.Directory, target.Path)),
            IsWorkspace = metadata.HasWorkspace,
            IsVirtualWorkspace = metadata.HasWorkspace && !metadata.HasPackage,
            IsProcMacro = metadata.IsProcMacro,
        };
    }

    private static void AddConventionalTargets(
        List<CargoTarget> targets,
        CargoManifestMetadata metadata,
        IReadOnlyList<EvidenceFact> files)
    {
        string[] nestedManifestDirectories = [.. files.Where(IsManifest)
            .Select(file => RustPath.Directory(file.Scope))
            .Where(directory => directory != metadata.Directory &&
                RustPath.IsWithin(directory, metadata.Directory))
            .Distinct(StringComparer.Ordinal)];
        foreach (EvidenceFact file in files.Where(file => IsMaintainedRust(file) &&
            RustPath.IsWithin(file.Scope, metadata.Directory) &&
            !nestedManifestDirectories.Any(directory => RustPath.IsWithin(file.Scope, directory))))
        {
            string relative = Relative(metadata.Directory, file.Scope);
            CargoTarget? target = relative switch
            {
                "src/lib.rs" when !metadata.HasLib => new CargoTarget("lib", "lib", relative),
                "src/main.rs" when metadata.AutoBins => new CargoTarget("bin", metadata.Name, relative),
                "build.rs" when !metadata.BuildDisabled => new CargoTarget("build", "build", relative),
                _ when metadata.AutoBins && relative.StartsWith("src/bin/", StringComparison.Ordinal) =>
                    AutoTarget(relative, "src/bin/", "bin"),
                _ when metadata.AutoExamples && relative.StartsWith("examples/", StringComparison.Ordinal) =>
                    AutoTarget(relative, "examples/", "example"),
                _ when metadata.AutoTests && relative.StartsWith("tests/", StringComparison.Ordinal) =>
                    AutoTarget(relative, "tests/", "test"),
                _ when metadata.AutoBenches && relative.StartsWith("benches/", StringComparison.Ordinal) =>
                    AutoTarget(relative, "benches/", "bench"),
                _ => null,
            };
            if (target is not null) targets.Add(target);
        }
    }

    private static CargoTarget? AutoTarget(string relative, string root, string kind)
    {
        if (!relative.StartsWith(root, StringComparison.Ordinal)) return null;
        string remainder = relative[root.Length..];
        string? name = !remainder.Contains('/') && remainder.EndsWith(".rs", StringComparison.Ordinal)
            ? Path.GetFileNameWithoutExtension(remainder)
            : remainder.Split('/') is [string directory, "main.rs"] ? directory : null;
        return name is null ? null : new CargoTarget(kind, name, relative);
    }

    private static string? TargetPath(CargoTargetBuilder target, string directory)
    {
        if (target.Path is not null) return TargetPath(target.Path, directory);
        if (target.Name is null) return null;
        return target.Kind switch
        {
            "bin" => $"src/bin/{target.Name}.rs",
            "example" => $"examples/{target.Name}.rs",
            "test" => $"tests/{target.Name}.rs",
            "bench" => $"benches/{target.Name}.rs",
            _ => null,
        };
    }

    private static string? TargetPath(string value, string directory)
    {
        string? resolved = RustPath.ResolveWithinRepository(directory, value);
        return resolved is not null && RustPath.IsWithin(resolved, directory)
            ? Relative(directory, resolved)
            : null;
    }

    private static void ApplyWorkspaceMembers(
        List<CargoPackageModel> packages,
        IReadOnlyList<CargoManifestMetadata> manifests)
    {
        for (int index = 0; index < packages.Count; index++)
        {
            CargoPackageModel package = packages[index];
            CargoManifestMetadata metadata = manifests.Single(item => item.Path == package.ManifestPaths[0]);
            if (!metadata.HasWorkspace) continue;
            string[] members = [.. packages.Where(candidate => candidate.Directory != package.Directory)
                .Where(candidate => MatchesAny(package.Directory, candidate.Directory, metadata.MemberPatterns))
                .Where(candidate => !MatchesAny(package.Directory, candidate.Directory, metadata.ExcludePatterns))
                .Select(candidate => candidate.Directory).Order(StringComparer.Ordinal)];
            packages[index] = package with { WorkspaceMembers = members };
        }
    }

    private static bool MatchesAny(string root, string candidate, IReadOnlyList<string> patterns) =>
        patterns.Any(pattern => MatchPathPattern(
            Relative(root, candidate),
            pattern.Replace('\\', '/').Trim('/')));

    private static bool MatchPathPattern(string value, string pattern)
    {
        string[] values = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string[] patterns = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (values.Length != patterns.Length) return false;
        return values.Zip(patterns).All(pair => pair.Second == "*" ||
            pair.First.Equals(pair.Second, StringComparison.Ordinal));
    }

    private static string Relative(string directory, string path) => directory == "."
        ? path
        : path.StartsWith(directory + "/", StringComparison.Ordinal) ? path[(directory.Length + 1)..] : path;

    private static bool HasFile(IReadOnlyList<EvidenceFact> files, string directory, string relative) =>
        files.Any(file => file.Scope == (directory == "." ? relative : directory + "/" + relative));

    private static bool HasMatchingManifest(
        IReadOnlyList<EvidenceFact> files,
        string directory,
        string pattern) => files.Any(file => IsManifest(file) &&
            MatchPathPattern(Relative(directory, RustPath.Directory(file.Scope)),
                pattern.Replace('\\', '/').Trim('/')));

    private static bool IsManifest(EvidenceFact fact) => fact.Kind == EvidenceKinds.File &&
        Path.GetFileName(fact.Scope).Equals("Cargo.toml", StringComparison.OrdinalIgnoreCase) &&
        !fact.Tags.Any(tag => tag is "classification:generated" or "classification:vendored" or "content:binary");

    private static bool IsMaintainedRust(EvidenceFact fact) => fact.Kind == EvidenceKinds.File &&
        fact.Tags.Contains("language:rust", StringComparer.Ordinal) &&
        !fact.Tags.Any(tag => tag is "classification:generated" or "classification:vendored" or
            "classification:minified" or "content:binary");

    private sealed class TargetIdentityComparer : IEqualityComparer<(string Kind, string? Path)>
    {
        public static TargetIdentityComparer Instance { get; } = new();

        public bool Equals((string Kind, string? Path) left, (string Kind, string? Path) right) =>
            left.Kind == right.Kind && left.Path == right.Path;

        public int GetHashCode((string Kind, string? Path) value) =>
            HashCode.Combine(StringComparer.Ordinal.GetHashCode(value.Kind),
                value.Path is null ? 0 : StringComparer.Ordinal.GetHashCode(value.Path));
    }

}
