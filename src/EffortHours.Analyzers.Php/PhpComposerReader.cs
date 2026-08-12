using System.Text.Json;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Php;

internal sealed class PhpComposerReader(PhpTextReader textReader)
{
    public async Task<PhpComposerReadResult> ReadAsync(
        IReadOnlyList<EvidenceFact> fileFacts,
        CancellationToken cancellationToken)
    {
        EvidenceFact[] manifests = [.. fileFacts
            .Where(IsComposerManifest)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        string[] declaredDirectories = [.. manifests
            .Select(fact => PhpPath.Directory(fact.Scope))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
        string[] packageDirectories = declaredDirectories;
        if (packageDirectories.Length == 0)
        {
            packageDirectories = ["."];
        }
        else if (!packageDirectories.Contains(".", StringComparer.Ordinal) &&
            fileFacts.Any(fact => IsMaintainedPhp(fact) &&
                !packageDirectories.Any(directory => PhpPath.IsWithin(fact.Scope, directory))))
        {
            packageDirectories = [".", .. packageDirectories];
        }

        List<PhpPackageModel> packages = [];
        List<Diagnostic> diagnostics = [];
        foreach (string directory in packageDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EvidenceFact? manifest = manifests.SingleOrDefault(fact =>
                PhpPath.Directory(fact.Scope) == directory);
            if (manifest is null)
            {
                packages.Add(ImplicitPackage(directory));
                continue;
            }

            PhpTextReadResult read = await textReader.ReadAsync(manifest, cancellationToken)
                .ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
                packages.Add(InvalidManifestPackage(directory, manifest.Scope));
                continue;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(
                    read.Text!,
                    new JsonDocumentOptions
                    {
                        AllowTrailingCommas = false,
                        CommentHandling = JsonCommentHandling.Disallow,
                        MaxDepth = 64,
                    });
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    diagnostics.Add(InvalidJson(manifest.Scope));
                    packages.Add(InvalidManifestPackage(directory, manifest.Scope));
                    continue;
                }

                packages.Add(ReadPackage(
                    directory,
                    manifest.Scope,
                    document.RootElement,
                    declaredDirectories));
            }
            catch (JsonException)
            {
                diagnostics.Add(InvalidJson(manifest.Scope));
                packages.Add(InvalidManifestPackage(directory, manifest.Scope));
            }
        }

        return new PhpComposerReadResult(packages, diagnostics);
    }

    private static PhpPackageModel ReadPackage(
        string directory,
        string manifestPath,
        JsonElement root,
        IReadOnlyList<string> declaredDirectories)
    {
        List<PhpDependency> dependencies = [];
        AddDependencies(root, "require", "runtime", dependencies);
        AddDependencies(root, "require-dev", "development", dependencies);
        dependencies.Sort(static (left, right) =>
        {
            int name = StringComparer.Ordinal.Compare(left.Name, right.Name);
            return name != 0 ? name : StringComparer.Ordinal.Compare(left.Kind, right.Kind);
        });

        HashSet<string> namespaces = new(StringComparer.Ordinal);
        HashSet<string> roots = new(StringComparer.Ordinal);
        int mappings = 0;
        int files = 0;
        int unresolved = 0;
        ReadAutoload(root, "autoload", directory, namespaces, roots, ref mappings, ref files, ref unresolved);
        ReadAutoload(root, "autoload-dev", directory, namespaces, roots, ref mappings, ref files, ref unresolved);

        List<string> localDirectories = [];
        int externalRepositories = 0;
        ReadRepositories(
            root,
            directory,
            declaredDirectories,
            localDirectories,
            ref externalRepositories,
            ref unresolved);
        string[] scripts = ReadPropertyNames(root, "scripts");
        int bins = CountStringOrArray(root, "bin");
        string name = ReadString(root, "name") ??
            (directory == "." ? "repository" : Path.GetFileName(directory));
        string? type = ReadString(root, "type");
        return new PhpPackageModel
        {
            Directory = directory,
            Name = name.ToLowerInvariant(),
            Role = Role(dependencies, scripts, bins, type),
            PackageType = type?.ToLowerInvariant(),
            ManifestPaths = [manifestPath],
            Dependencies = dependencies,
            AutoloadNamespaces = [.. namespaces.Order(StringComparer.Ordinal)],
            AutoloadRoots = [.. roots.Order(StringComparer.Ordinal)],
            ScriptNames = scripts,
            PathRepositoryDirectories = [.. localDirectories
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)],
            AutoloadMappings = mappings,
            AutoloadFiles = files,
            BinEntries = bins,
            ExternalRepositories = externalRepositories,
            UnresolvedPaths = unresolved,
        };
    }

    private static void AddDependencies(
        JsonElement root,
        string propertyName,
        string kind,
        List<PhpDependency> dependencies)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement values) ||
            values.ValueKind != JsonValueKind.Object) return;
        foreach (JsonProperty property in values.EnumerateObject())
        {
            string name = property.Name.Trim().ToLowerInvariant();
            if (property.Value.ValueKind != JsonValueKind.String ||
                name == "php" || name.StartsWith("ext-", StringComparison.Ordinal) ||
                name.StartsWith("lib-", StringComparison.Ordinal)) continue;
            dependencies.Add(new PhpDependency(name, kind));
        }
    }

    private static void ReadAutoload(
        JsonElement root,
        string propertyName,
        string directory,
        HashSet<string> namespaces,
        HashSet<string> roots,
        ref int mappings,
        ref int files,
        ref int unresolved)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement autoload) ||
            autoload.ValueKind != JsonValueKind.Object) return;
        foreach (string standard in new[] { "psr-4", "psr-0" })
        {
            if (!autoload.TryGetProperty(standard, out JsonElement values) ||
                values.ValueKind != JsonValueKind.Object) continue;
            foreach (JsonProperty property in values.EnumerateObject())
            {
                mappings++;
                string namespaceName = property.Name.Trim().TrimStart('\\');
                if (namespaceName.Length > 0) namespaces.Add(namespaceName);
                foreach (string path in StringValues(property.Value))
                {
                    string? resolved = PhpPath.ResolveWithinRepository(directory, path);
                    if (resolved is null) unresolved++;
                    else roots.Add(resolved);
                }
            }
        }

        if (autoload.TryGetProperty("classmap", out JsonElement classmap))
        {
            foreach (string path in StringValues(classmap))
            {
                mappings++;
                string? resolved = PhpPath.ResolveWithinRepository(directory, path);
                if (resolved is null) unresolved++;
                else roots.Add(resolved);
            }
        }

        if (autoload.TryGetProperty("files", out JsonElement autoloadFiles))
            files += StringValues(autoloadFiles).Count();
    }

    private static void ReadRepositories(
        JsonElement root,
        string directory,
        IReadOnlyList<string> declaredDirectories,
        List<string> localDirectories,
        ref int externalRepositories,
        ref int unresolved)
    {
        if (!root.TryGetProperty("repositories", out JsonElement repositories)) return;
        IEnumerable<JsonElement> values = repositories.ValueKind switch
        {
            JsonValueKind.Array => repositories.EnumerateArray(),
            JsonValueKind.Object => repositories.EnumerateObject().Select(property => property.Value),
            _ => [],
        };
        foreach (JsonElement repository in values)
        {
            if (repository.ValueKind != JsonValueKind.Object ||
                !string.Equals(ReadString(repository, "type"), "path", StringComparison.OrdinalIgnoreCase))
            {
                externalRepositories++;
                continue;
            }

            string? value = ReadString(repository, "url");
            string? pattern = value is null ? null : PhpPath.ResolveWithinRepository(directory, value);
            if (pattern is null)
            {
                unresolved++;
                continue;
            }

            string[] matches = [.. declaredDirectories.Where(candidate => GlobMatches(pattern, candidate))];
            if (matches.Length == 0) unresolved++;
            else localDirectories.AddRange(matches);
        }
    }

    private static bool GlobMatches(string pattern, string path)
    {
        string[] patternSegments = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string[] pathSegments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return Match(patternSegments, 0, pathSegments, 0);
    }

    private static bool Match(string[] pattern, int patternIndex, string[] path, int pathIndex)
    {
        if (patternIndex == pattern.Length) return pathIndex == path.Length;
        if (pattern[patternIndex] == "**")
        {
            for (int index = pathIndex; index <= path.Length; index++)
                if (Match(pattern, patternIndex + 1, path, index)) return true;
            return false;
        }

        return pathIndex < path.Length && SegmentMatches(pattern[patternIndex], path[pathIndex]) &&
            Match(pattern, patternIndex + 1, path, pathIndex + 1);
    }

    private static bool SegmentMatches(string pattern, string value)
    {
        int patternIndex = 0;
        int valueIndex = 0;
        int star = -1;
        int retry = -1;
        while (valueIndex < value.Length)
        {
            if (patternIndex < pattern.Length &&
                (pattern[patternIndex] == '?' || pattern[patternIndex] == value[valueIndex]))
            {
                patternIndex++;
                valueIndex++;
            }
            else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                star = patternIndex++;
                retry = valueIndex;
            }
            else if (star >= 0)
            {
                patternIndex = star + 1;
                valueIndex = ++retry;
            }
            else return false;
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*') patternIndex++;
        return patternIndex == pattern.Length;
    }

    private static IEnumerable<string> StringValues(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            string? text = value.GetString();
            if (!string.IsNullOrWhiteSpace(text)) yield return text;
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in value.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    yield return item.GetString()!;
        }
    }

    private static string[] ReadPropertyNames(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Object
            ? [.. value.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal)]
            : [];

    private static int CountStringOrArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value)) return 0;
        return value.ValueKind switch
        {
            JsonValueKind.String => 1,
            JsonValueKind.Array => value.EnumerateArray().Count(item => item.ValueKind == JsonValueKind.String),
            JsonValueKind.Object => value.EnumerateObject().Count(),
            _ => 0,
        };
    }

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string Role(
        IReadOnlyList<PhpDependency> dependencies,
        IReadOnlyList<string> scripts,
        int bins,
        string? type)
    {
        HashSet<string> names = [.. dependencies.Select(item => item.Name)];
        if (names.Contains("laravel/framework") || names.Any(name => name.StartsWith("symfony/framework-", StringComparison.Ordinal)))
            return "server";
        if (names.Any(name => name is "laravel/horizon" or "symfony/messenger" or "enqueue/enqueue"))
            return "worker";
        if (bins > 0 || names.Contains("symfony/console") || scripts.Any(name => name is "run" or "start"))
            return "cli";
        return type is "project" ? "application" : "library";
    }

    private static PhpPackageModel ImplicitPackage(string directory) => new()
    {
        Directory = directory,
        Name = directory == "." ? "repository" : Path.GetFileName(directory).ToLowerInvariant(),
        Role = "library",
    };

    private static PhpPackageModel InvalidManifestPackage(string directory, string path) =>
        ImplicitPackage(directory) with { ManifestPaths = [path] };

    private static Diagnostic InvalidJson(string path) => PhpEvidence.Diagnostic(
        "FB8703",
        DiagnosticSeverity.Warning,
        $"Composer manifest '{path}' is invalid or is not a JSON object; literal package metadata was skipped.",
        path);

    private static bool IsComposerManifest(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.File &&
        Path.GetFileName(fact.Scope).Equals("composer.json", StringComparison.OrdinalIgnoreCase) &&
        !fact.Tags.Any(tag => tag is
            "classification:generated" or "classification:vendored" or "content:binary");

    private static bool IsMaintainedPhp(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.File && fact.Tags.Contains("language:php", StringComparer.Ordinal) &&
        fact.Tags.Any(tag => tag is "role:source" or "role:test") &&
        !fact.Tags.Any(tag => tag is
            "classification:generated" or "classification:minified" or
            "classification:vendored" or "content:binary");
}
