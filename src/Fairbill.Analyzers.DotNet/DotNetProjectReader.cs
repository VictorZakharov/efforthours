using System.Collections.Frozen;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Fairbill.Analysis;
using Fairbill.Contracts.V1;

namespace Fairbill.Analyzers.DotNet;

internal sealed partial class DotNetProjectReader(
    IRepositoryFileSystem fileSystem,
    string rootPath)
{
    private static readonly FrozenSet<string> TestPackageIds = new[]
    {
        "Microsoft.NET.Test.Sdk",
        "MSTest.TestFramework",
        "NUnit",
        "NUnit3TestAdapter",
        "xunit",
        "xunit.v3",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private readonly IRepositoryFileSystem _fileSystem = fileSystem;
    private readonly string _rootPath = fileSystem.GetFullPath(rootPath);

    public async Task<DotNetProjectReadResult> ReadAsync(
        RepositoryEvidence evidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        string[] projectPaths = GetFilePaths(evidence, IsProjectFile);
        string[] solutionPaths = GetFilePaths(evidence, IsSolutionFile);
        string[] centralPackagePaths = GetFilePaths(
            evidence,
            path => Path.GetFileName(path).Equals(
                "Directory.Packages.props",
                StringComparison.OrdinalIgnoreCase));
        List<DotNetProjectModel> projects = [];
        List<DotNetSolutionModel> solutions = [];
        List<Diagnostic> diagnostics = [];
        Dictionary<string, IReadOnlyDictionary<string, string>> centralPackages =
            await ReadCentralPackagesAsync(
                centralPackagePaths,
                diagnostics,
                cancellationToken).ConfigureAwait(false);
        foreach (string projectPath in projectPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                projects.Add(await ReadProjectAsync(
                    projectPath,
                    centralPackages,
                    cancellationToken).ConfigureAwait(false));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or XmlException)
            {
                diagnostics.Add(ReadDiagnostic("FB3001", "project", projectPath, exception));
            }
        }

        foreach (string solutionPath in solutionPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                solutions.Add(await ReadSolutionAsync(solutionPath, cancellationToken).ConfigureAwait(false));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or XmlException)
            {
                diagnostics.Add(ReadDiagnostic("FB3002", "solution", solutionPath, exception));
            }
        }

        return new DotNetProjectReadResult(projects, solutions, diagnostics);
    }

    private async Task<DotNetProjectModel> ReadProjectAsync(
        string relativePath,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> centralPackages,
        CancellationToken cancellationToken)
    {
        string fullPath = ToFullPath(relativePath);
        XDocument document = await SafeXml.LoadAsync(
            _fileSystem,
            fullPath,
            cancellationToken).ConfigureAwait(false);
        XElement root = document.Root
            ?? throw new XmlException("Project XML has no root element.");
        string projectDirectory = NormalizePath(Path.GetDirectoryName(relativePath) ?? string.Empty);
        string? sdk = Attribute(root, "Sdk") ??
            string.Join(
                ';',
                root.Elements()
                    .Where(element => element.Name.LocalName == "Sdk")
                    .Select(element => Attribute(element, "Name"))
                    .Where(value => !string.IsNullOrWhiteSpace(value)));
        string[] targetFrameworks = [.. PropertyValues(root, "TargetFramework", "TargetFrameworks")
            .SelectMany(value => value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)];
        List<DotNetPackageReference> packages = [];
        foreach (XElement element in Descendants(root, "PackageReference"))
        {
            string? id = Attribute(element, "Include") ?? Attribute(element, "Update");
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            string? version = Attribute(element, "Version") ?? ChildValue(element, "Version");
            version ??= FindCentralPackageVersion(projectDirectory, id, centralPackages);
            packages.Add(new DotNetPackageReference(id, NullIfWhiteSpace(version)));
        }

        DotNetPackageReference[] normalizedPackages = [.. packages
            .DistinctBy(
                package => $"{package.Id.ToUpperInvariant()}\0{package.Version}",
                StringComparer.Ordinal)
            .OrderBy(package => package.Id, StringComparer.OrdinalIgnoreCase)
            .ThenBy(package => package.Version, StringComparer.Ordinal)];
        DotNetProjectReference[] projectReferences = [.. Descendants(root, "ProjectReference")
            .Select(element => Attribute(element, "Include"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => ResolveProjectReference(projectDirectory, value!))
            .Distinct()
            .OrderBy(reference => reference.DeclaredPath, StringComparer.Ordinal)];
        string[] frameworkReferences = [.. Descendants(root, "FrameworkReference")
            .Select(element => Attribute(element, "Include"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)];
        string? outputType = FirstPropertyValue(root, "OutputType");
        bool isTest = IsTrue(FirstPropertyValue(root, "IsTestProject")) ||
            TestPackageIds.Overlaps(normalizedPackages.Select(package => package.Id)) ||
            HasTestPath(relativePath);
        string role = DetermineRole(
            sdk,
            outputType,
            isTest,
            normalizedPackages,
            frameworkReferences,
            IsTrue(FirstPropertyValue(root, "UseWPF")),
            IsTrue(FirstPropertyValue(root, "UseWindowsForms")));
        string[] inspectedValues =
        [
            .. targetFrameworks,
            sdk ?? string.Empty,
            outputType ?? string.Empty,
            FirstPropertyValue(root, "RuntimeIdentifier") ?? string.Empty,
            .. normalizedPackages.Select(package => package.Version ?? string.Empty),
            .. projectReferences.Select(reference => reference.DeclaredPath),
        ];

        return new DotNetProjectModel
        {
            Path = relativePath,
            Directory = projectDirectory,
            Name = Path.GetFileNameWithoutExtension(relativePath),
            Sdk = NullIfWhiteSpace(sdk),
            Role = role,
            TargetFrameworks = targetFrameworks,
            Packages = normalizedPackages,
            ProjectReferences = projectReferences,
            FrameworkReferences = frameworkReferences,
            OutputType = NullIfWhiteSpace(outputType),
            LanguageVersion = NullIfWhiteSpace(FirstPropertyValue(root, "LangVersion")),
            Nullable = NullIfWhiteSpace(FirstPropertyValue(root, "Nullable")),
            RuntimeIdentifier = NullIfWhiteSpace(
                FirstPropertyValue(root, "RuntimeIdentifier") ??
                FirstPropertyValue(root, "RuntimeIdentifiers")),
            IsPackable = IsTrue(FirstPropertyValue(root, "IsPackable")),
            PublishAot = IsTrue(FirstPropertyValue(root, "PublishAot")),
            UsesWpf = IsTrue(FirstPropertyValue(root, "UseWPF")),
            UsesWindowsForms = IsTrue(FirstPropertyValue(root, "UseWindowsForms")),
            UnresolvedValueCount = inspectedValues.Count(ContainsMsBuildProperty),
        };
    }

    private async Task<DotNetSolutionModel> ReadSolutionAsync(
        string relativePath,
        CancellationToken cancellationToken)
    {
        string extension = Path.GetExtension(relativePath);
        string[] declaredPaths;
        if (extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase))
        {
            XDocument document = await SafeXml.LoadAsync(
                _fileSystem,
                ToFullPath(relativePath),
                cancellationToken).ConfigureAwait(false);
            declaredPaths = [.. document
                .Descendants()
                .Where(element => element.Name.LocalName == "Project")
                .Select(element => Attribute(element, "Path"))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => NormalizePath(value!))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)];
        }
        else
        {
            string[] lines = await _fileSystem.ReadAllLinesAsync(
                ToFullPath(relativePath),
                cancellationToken).ConfigureAwait(false);
            declaredPaths = [.. lines
                .Select(TryReadSlnProjectPath)
                .Where(value => value is not null)
                .Select(value => NormalizePath(value!))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)];
        }

        string solutionDirectory = NormalizePath(Path.GetDirectoryName(relativePath) ?? string.Empty);
        string[] resolvedPaths = [.. declaredPaths
            .Where(path => !ContainsMsBuildProperty(path))
            .Select(path => NormalizePath(Path.Combine(solutionDirectory, path)))
            .Where(path => IsInsideRoot(ToFullPath(path)))
            .Where(path => _fileSystem.FileExists(ToFullPath(path)))
            .OrderBy(path => path, StringComparer.Ordinal)];
        return new DotNetSolutionModel(relativePath, declaredPaths, resolvedPaths);
    }

    private async Task<Dictionary<string, IReadOnlyDictionary<string, string>>> ReadCentralPackagesAsync(
        IEnumerable<string> relativePaths,
        List<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        Dictionary<string, IReadOnlyDictionary<string, string>> catalogs =
            new(StringComparer.OrdinalIgnoreCase);
        foreach (string relativePath in relativePaths)
        {
            try
            {
                XDocument document = await SafeXml.LoadAsync(
                    _fileSystem,
                    ToFullPath(relativePath),
                    cancellationToken).ConfigureAwait(false);
                Dictionary<string, string> versions = new(StringComparer.OrdinalIgnoreCase);
                foreach (XElement element in document.Descendants()
                    .Where(element => element.Name.LocalName == "PackageVersion"))
                {
                    string? id = Attribute(element, "Include") ?? Attribute(element, "Update");
                    string? version = Attribute(element, "Version") ?? ChildValue(element, "Version");
                    if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(version))
                    {
                        versions[id] = version;
                    }
                }

                string directory = NormalizePath(Path.GetDirectoryName(relativePath) ?? string.Empty);
                catalogs[directory] = versions;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or XmlException)
            {
                diagnostics.Add(ReadDiagnostic(
                    "FB3006",
                    "central package file",
                    relativePath,
                    exception));
            }
        }

        return catalogs;
    }

    private DotNetProjectReference ResolveProjectReference(string projectDirectory, string declaredPath)
    {
        string normalizedDeclaredPath = NormalizePath(declaredPath);
        if (ContainsMsBuildProperty(declaredPath))
        {
            return new DotNetProjectReference(normalizedDeclaredPath, null, false, false);
        }

        string nativeDeclaredPath = declaredPath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.GetFullPath(Path.Combine(
            _rootPath,
            projectDirectory.Replace('/', Path.DirectorySeparatorChar),
            nativeDeclaredPath));
        bool outsideScope = !IsInsideRoot(fullPath);
        string? resolvedPath = outsideScope ? null : NormalizeRelativePath(fullPath);
        return new DotNetProjectReference(
            normalizedDeclaredPath,
            resolvedPath,
            !outsideScope && _fileSystem.FileExists(fullPath),
            outsideScope);
    }

    private static string? FindCentralPackageVersion(
        string projectDirectory,
        string packageId,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> catalogs)
    {
        string directory = projectDirectory;
        while (true)
        {
            if (catalogs.TryGetValue(directory, out IReadOnlyDictionary<string, string>? versions) &&
                versions.TryGetValue(packageId, out string? version))
            {
                return version;
            }

            int separator = directory.LastIndexOf('/');
            if (separator < 0)
            {
                return catalogs.TryGetValue(string.Empty, out versions) &&
                       versions.TryGetValue(packageId, out version)
                    ? version
                    : null;
            }

            directory = directory[..separator];
        }
    }

    private static string DetermineRole(
        string? sdk,
        string? outputType,
        bool isTest,
        IReadOnlyList<DotNetPackageReference> packages,
        IReadOnlyList<string> frameworkReferences,
        bool usesWpf,
        bool usesWindowsForms)
    {
        if (isTest)
        {
            return "test";
        }

        if (usesWpf || usesWindowsForms)
        {
            return "desktop";
        }

        if (sdk?.Contains(".Web", StringComparison.OrdinalIgnoreCase) == true ||
            frameworkReferences.Contains("Microsoft.AspNetCore.App", StringComparer.OrdinalIgnoreCase))
        {
            return "web";
        }

        if (sdk?.Contains(".Worker", StringComparison.OrdinalIgnoreCase) == true ||
            packages.Any(package => package.Id.Equals(
                "Microsoft.Extensions.Hosting",
                StringComparison.OrdinalIgnoreCase)))
        {
            return "worker";
        }

        return outputType is not null &&
               (outputType.Equals("Exe", StringComparison.OrdinalIgnoreCase) ||
                outputType.Equals("WinExe", StringComparison.OrdinalIgnoreCase))
            ? "executable"
            : "library";
    }

    private static string? TryReadSlnProjectPath(string line)
    {
        Match match = SolutionProjectRegex().Match(line);
        string path = match.Success ? match.Groups["path"].Value : string.Empty;
        return IsProjectFile(path) ? path : null;
    }

    private static string[] GetFilePaths(
        RepositoryEvidence evidence,
        Func<string, bool> predicate) =>
        [
            .. evidence.Facts
                .Where(fact => fact.Kind == EvidenceKinds.File)
                .Select(fact => fact.Scope)
                .Where(predicate)
                .OrderBy(path => path, StringComparer.Ordinal),
        ];

    private static IEnumerable<XElement> Descendants(XElement root, string localName) =>
        root.Descendants().Where(element => element.Name.LocalName == localName);

    private static IEnumerable<string> PropertyValues(XElement root, params string[] names) =>
        root.Descendants()
            .Where(element => names.Contains(element.Name.LocalName, StringComparer.Ordinal))
            .Select(element => element.Value.Trim())
            .Where(value => value.Length > 0);

    private static string? FirstPropertyValue(XElement root, string name) =>
        PropertyValues(root, name).FirstOrDefault();

    private static string? ChildValue(XElement element, string name) =>
        element.Elements().FirstOrDefault(child => child.Name.LocalName == name)?.Value.Trim();

    private static string? Attribute(XElement element, string name) =>
        element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == name)?.Value.Trim();

    private static bool IsProjectFile(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".csproj" or ".fsproj" or ".vbproj" or ".esproj";

    private static bool IsSolutionFile(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".sln" or ".slnx";

    private static bool IsTrue(string? value) =>
        bool.TryParse(value, out bool result) && result;

    private static bool ContainsMsBuildProperty(string value) =>
        value.Contains("$(", StringComparison.Ordinal);

    private static bool HasTestPath(string path)
    {
        string[] segments = NormalizePath(path).Split('/');
        return segments.Any(segment => segment.Equals("test", StringComparison.OrdinalIgnoreCase) ||
            segment.Equals("tests", StringComparison.OrdinalIgnoreCase) ||
            segment.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
            segment.EndsWith(".Test", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').TrimStart('/');

    private string NormalizeRelativePath(string fullPath) =>
        NormalizePath(Path.GetRelativePath(_rootPath, fullPath));

    private string ToFullPath(string relativePath) => Path.GetFullPath(Path.Combine(
        _rootPath,
        relativePath.Replace('/', Path.DirectorySeparatorChar)));

    private bool IsInsideRoot(string fullPath)
    {
        string relativePath = Path.GetRelativePath(_rootPath, fullPath);
        return relativePath != ".." &&
               !Path.IsPathRooted(relativePath) &&
               !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
               !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static Diagnostic ReadDiagnostic(
        string code,
        string subject,
        string path,
        Exception exception) => new()
        {
            Code = code,
            Severity = DiagnosticSeverity.Warning,
            Message = $"Could not statically read .NET {subject} '{path}': {DescribeReadFailure(exception)}",
            Locations = [new EvidenceLocation { Path = path }],
        };

    private static string DescribeReadFailure(Exception exception) => exception switch
    {
        XmlException => "XML was malformed or contained a prohibited construct.",
        UnauthorizedAccessException => "access was denied.",
        IOException => "repository content could not be read.",
        _ => "content could not be interpreted.",
    };

    [GeneratedRegex(
        "^Project\\(\"[^\"]*\"\\)\\s*=\\s*\"[^\"]*\",\\s*\"(?<path>[^\"]+)\"",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex SolutionProjectRegex();
}

internal sealed record DotNetProjectReadResult(
    IReadOnlyList<DotNetProjectModel> Projects,
    IReadOnlyList<DotNetSolutionModel> Solutions,
    IReadOnlyList<Diagnostic> Diagnostics);
