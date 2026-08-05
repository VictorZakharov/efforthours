using System.Text.Json;
using Fairbill.Contracts.V1;

namespace Fairbill.Analyzers.JavaScript;

internal sealed class JavaScriptPackageReader(RepositoryTextReader textReader)
{
    private const long MaximumManifestBytes = 8 * 1024 * 1024;

    private readonly RepositoryTextReader _textReader = textReader;

    public async Task<JavaScriptPackageReadResult> ReadAsync(
        RepositoryEvidence evidence,
        CancellationToken cancellationToken)
    {
        List<JavaScriptPackageModel> packages = [];
        List<Diagnostic> diagnostics = [];
        foreach (EvidenceFact fileFact in evidence.Facts
            .Where(IsPackageManifest)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            RepositoryTextReadResult read = await _textReader.ReadAsync(
                fileFact,
                MaximumManifestBytes,
                "FB4001",
                cancellationToken).ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
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
                    diagnostics.Add(JavaScriptEvidence.Diagnostic(
                        "FB4002",
                        DiagnosticSeverity.Warning,
                        $"Package manifest '{fileFact.Scope}' is not a JSON object and was skipped.",
                        fileFact.Scope));
                    continue;
                }

                packages.Add(ReadPackage(fileFact.Scope, document.RootElement));
            }
            catch (JsonException exception)
            {
                diagnostics.Add(JavaScriptEvidence.Diagnostic(
                    "FB4002",
                    DiagnosticSeverity.Warning,
                    $"Package manifest '{fileFact.Scope}' is invalid JSON and was skipped.",
                    fileFact.Scope,
                    ToLine(exception.LineNumber)));
            }
        }

        return new JavaScriptPackageReadResult(packages, diagnostics);
    }

    private static JavaScriptPackageModel ReadPackage(string path, JsonElement root)
    {
        List<JavaScriptDependency> dependencies = [];
        AddDependencies(root, "dependencies", "runtime", dependencies);
        AddDependencies(root, "devDependencies", "development", dependencies);
        AddDependencies(root, "peerDependencies", "peer", dependencies);
        AddDependencies(root, "optionalDependencies", "optional", dependencies);
        dependencies.Sort((left, right) =>
        {
            int nameComparison = StringComparer.OrdinalIgnoreCase.Compare(left.Name, right.Name);
            return nameComparison != 0
                ? nameComparison
                : StringComparer.Ordinal.Compare(left.Kind, right.Kind);
        });

        string[] technologies = [.. dependencies
            .Select(dependency => JavaScriptTechnologyCatalog.Classify(dependency.Name))
            .Where(classification => classification is not null)
            .Select(classification => classification!.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)];
        string scope = GetDirectory(path);
        bool hasBin = root.TryGetProperty("bin", out JsonElement bin) &&
            bin.ValueKind is JsonValueKind.String or JsonValueKind.Object;
        bool hasLibraryExports = HasPropertyValue(root, "main") ||
            HasPropertyValue(root, "module") ||
            HasPropertyValue(root, "exports") ||
            HasPropertyValue(root, "types") ||
            HasPropertyValue(root, "typings");
        string[] scripts = ReadPropertyNames(root, "scripts");
        string? packageManager = ReadString(root, "packageManager")?.Split('@', 2)[0];
        return new JavaScriptPackageModel
        {
            ManifestPath = path,
            Scope = scope,
            Name = ReadString(root, "name"),
            Version = ReadString(root, "version"),
            ModuleType = ReadString(root, "type"),
            PackageManager = packageManager,
            IsPrivate = ReadBoolean(root, "private"),
            HasBin = hasBin,
            HasLibraryExports = hasLibraryExports,
            Role = ClassifyRole(scope, hasBin, hasLibraryExports, dependencies, scripts),
            WorkspacePatterns = ReadWorkspaces(root),
            ScriptNames = scripts,
            Dependencies = dependencies,
            Technologies = technologies,
            Coverage = ReadCoverage(root),
        };
    }

    private static string ClassifyRole(
        string scope,
        bool hasBin,
        bool hasLibraryExports,
        IReadOnlyList<JavaScriptDependency> dependencies,
        IReadOnlyList<string> scripts)
    {
        if (IsTestPath(scope))
        {
            return "test";
        }

        if (hasBin)
        {
            return "cli";
        }

        TechnologyClassification[] classifications = [.. dependencies
            .Select(dependency => JavaScriptTechnologyCatalog.Classify(dependency.Name))
            .Where(classification => classification is not null)
            .Select(classification => classification!)];
        if (classifications.Any(classification => classification.Family == "full-stack"))
        {
            return "full-stack-web";
        }

        if (classifications.Any(classification => classification.Family == "server"))
        {
            return "server";
        }

        if (classifications.Any(classification => classification.Family == "ui"))
        {
            return "web-ui";
        }

        if (classifications.Any(classification => classification.Family == "background"))
        {
            return "worker";
        }

        if (hasLibraryExports)
        {
            return "library";
        }

        if (scripts.Any(script => script is "start" or "dev" or "serve"))
        {
            return "application";
        }

        return "package";
    }

    private static void AddDependencies(
        JsonElement root,
        string propertyName,
        string kind,
        List<JavaScriptDependency> dependencies)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement values) ||
            values.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (JsonProperty property in values.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                dependencies.Add(new JavaScriptDependency(
                    property.Name,
                    property.Value.GetString() ?? string.Empty,
                    kind));
            }
        }
    }

    private static IReadOnlyList<string> ReadWorkspaces(JsonElement root)
    {
        if (!root.TryGetProperty("workspaces", out JsonElement workspaces))
        {
            return [];
        }

        JsonElement values = workspaces;
        if (workspaces.ValueKind == JsonValueKind.Object &&
            workspaces.TryGetProperty("packages", out JsonElement packageValues))
        {
            values = packageValues;
        }

        if (values.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return [.. values.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.String)
            .Select(value => NormalizePattern(value.GetString()))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)];
    }

    private static JavaScriptCoverageDeclaration? ReadCoverage(JsonElement root)
    {
        if (!root.TryGetProperty("jest", out JsonElement jest) ||
            jest.ValueKind != JsonValueKind.Object ||
            !jest.TryGetProperty("coverageThreshold", out JsonElement threshold) ||
            threshold.ValueKind != JsonValueKind.Object ||
            !threshold.TryGetProperty("global", out JsonElement global) ||
            global.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        JavaScriptCoverageDeclaration declaration = new(
            ReadDecimal(global, "lines"),
            ReadDecimal(global, "branches"),
            ReadDecimal(global, "functions"),
            ReadDecimal(global, "statements"));
        return declaration.Lines is null && declaration.Branches is null &&
            declaration.Functions is null && declaration.Statements is null
                ? null
                : declaration;
    }

    private static string[] ReadPropertyNames(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out JsonElement values) ||
            values.ValueKind != JsonValueKind.Object)
        {
            return [];
        }

        return [.. values.EnumerateObject()
            .Select(property => property.Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)];
    }

    private static string? ReadString(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out JsonElement value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool ReadBoolean(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out JsonElement value) &&
        value.ValueKind == JsonValueKind.True;

    private static decimal? ReadDecimal(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out JsonElement value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetDecimal(out decimal result)
            ? result
            : null;

    private static bool HasPropertyValue(JsonElement root, string propertyName) =>
        root.TryGetProperty(propertyName, out JsonElement value) &&
        value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;

    private static string NormalizePattern(string? value)
    {
        string pattern = (value ?? string.Empty).Replace('\\', '/').Trim();
        while (pattern.StartsWith("./", StringComparison.Ordinal))
        {
            pattern = pattern[2..];
        }

        return pattern.Trim('/');
    }

    private static string GetDirectory(string path)
    {
        int separator = path.LastIndexOf('/');
        return separator < 0 ? "." : path[..separator];
    }

    private static bool IsPackageManifest(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.File &&
        Path.GetFileName(fact.Scope).Equals("package.json", StringComparison.OrdinalIgnoreCase) &&
        !fact.Tags.Contains("classification:generated", StringComparer.Ordinal) &&
        !fact.Tags.Contains("classification:vendored", StringComparer.Ordinal) &&
        !fact.Tags.Contains("content:binary", StringComparer.Ordinal);

    private static bool IsTestPath(string scope) => scope.Split(
        '/',
        StringSplitOptions.RemoveEmptyEntries).Any(segment => segment.Equals("test", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("tests", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("e2e", StringComparison.OrdinalIgnoreCase));

    private static int? ToLine(long? zeroBasedLine) => zeroBasedLine is null
        ? null
        : checked((int)zeroBasedLine.Value + 1);
}
