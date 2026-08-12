using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Cpp;

internal sealed class CppBuildMetadataReader(CppTextReader reader, string rootPath)
{
    private readonly CppTextReader _reader = reader;
    private readonly string _rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));

    public async Task<CppBuildReadResult> ReadAsync(
        IReadOnlyList<EvidenceFact> files,
        bool hasSource,
        CancellationToken cancellationToken)
    {
        Dictionary<string, CppBuildAccumulator> projects = new(StringComparer.Ordinal);
        List<Diagnostic> diagnostics = [];
        HashSet<string> scannerPaths = files.Select(file => file.Scope).ToHashSet(StringComparer.Ordinal);
        foreach (EvidenceFact file in files.Where(IsBuildFile).OrderBy(file => file.Scope, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            CppTextReadResult read = await _reader.ReadAsync(file, cancellationToken).ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
                continue;
            }
            string directory = CppPath.Directory(file.Scope);
            CppBuildAccumulator project = projects.GetValueOrDefault(directory) ?? new(directory);
            projects[directory] = project;
            project.ManifestPaths.Add(file.Scope);
            try
            {
                Parse(file.Scope, read.Text!, project, scannerPaths);
            }
            catch (Exception exception) when (exception is XmlException or JsonException or InvalidDataException)
            {
                project.Unresolved++;
                diagnostics.Add(CppEvidence.Diagnostic(
                    "FB8903",
                    DiagnosticSeverity.Warning,
                    $"C/C++ build metadata '{file.Scope}' was malformed or outside the bounded literal subset; supported static values remain conservative.",
                    file.Scope));
            }
        }

        if (projects.Count == 0 && hasSource) projects["."] = new CppBuildAccumulator(".");
        return new CppBuildReadResult(
            [.. projects.Values.Select(project => project.ToModel())
                .OrderBy(project => project.Directory, StringComparer.Ordinal)],
            diagnostics);
    }

    private void Parse(
        string path,
        string text,
        CppBuildAccumulator project,
        IReadOnlySet<string> scannerPaths)
    {
        string name = Path.GetFileName(path).ToLowerInvariant();
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (name == "cmakelists.txt" || extension == ".cmake") ParseCMake(text, project);
        else if (name is "cmakepresets.json" or "cmakeuserpresets.json")
            CppCMakePresetReader.Parse(text, project);
        else if (name is "makefile" or "gnumakefile" || extension == ".mk") ParseMake(text, project);
        else if (name is "meson.build" or "meson.options" or "meson_options.txt") ParseMeson(text, project);
        else if (extension is ".vcxproj" or ".props") ParseVcx(text, project);
        else if (name == "compile_commands.json")
            CppSupplementaryMetadataReader.ParseCompileCommands(
                text, project, scannerPaths, _rootPath);
        else if (name == "vcpkg.json") CppSupplementaryMetadataReader.ParseVcpkg(text, project);
        else if (name == "conanfile.txt") CppSupplementaryMetadataReader.ParseConan(text, project);
    }

    private static void ParseCMake(string text, CppBuildAccumulator project)
    {
        project.BuildSystems.Add("cmake");
        Dictionary<string, IReadOnlyList<string>> variables = new(StringComparer.Ordinal);
        foreach (IReadOnlyList<string> call in CppBuildSyntax.Calls(text, "project"))
        {
            int languages = call.ToList().FindIndex(value =>
                value.Equals("LANGUAGES", StringComparison.OrdinalIgnoreCase));
            if (languages >= 0)
                foreach (string value in call.Skip(languages + 1)) CppBuildLanguage.Add(value, project);
        }
        foreach (IReadOnlyList<string> call in CppBuildSyntax.Calls(text, "set"))
            if (call.Count > 1 && IsIdentifier(call[0]) && call.Skip(1).All(CppBuildSyntax.IsLiteral))
            {
                variables[call[0]] = [.. call.Skip(1)];
                if (call[0].Equals("CMAKE_CXX_STANDARD", StringComparison.OrdinalIgnoreCase))
                {
                    string standard = CppBuildStandard.Normalize("c++" + call[1]);
                    if (standard.Length > 0) project.Standards.Add(standard);
                }
                if (call[0].Equals("CMAKE_C_STANDARD", StringComparison.OrdinalIgnoreCase))
                {
                    string standard = CppBuildStandard.Normalize("c" + call[1]);
                    if (standard.Length > 0) project.Standards.Add(standard);
                }
            }

        ReadTargets("add_executable", executable: true, library: false);
        ReadTargets("add_library", executable: false, library: true);
        foreach (IReadOnlyList<string> call in CppBuildSyntax.Calls(text, "target_sources"))
            AddSources(Expand(call.Skip(1), variables), project);
        foreach (IReadOnlyList<string> call in CppBuildSyntax.Calls(text, "include_directories"))
            AddIncludeRoots(Expand(call, variables), project);
        foreach (IReadOnlyList<string> call in CppBuildSyntax.Calls(text, "target_include_directories"))
            AddIncludeRoots(Expand(call.Skip(1), variables), project);
        foreach (IReadOnlyList<string> call in CppBuildSyntax.Calls(text, "target_compile_definitions"))
        {
            string[] definitions = [.. Expand(call.Skip(1), variables)
                .Where(value => value is not ("PUBLIC" or "PRIVATE" or "INTERFACE"))];
            project.CompileDefinitions += definitions.Count(CppBuildSyntax.IsLiteral);
            project.Unresolved += definitions.Count(value => !CppBuildSyntax.IsLiteral(value));
        }
        foreach (IReadOnlyList<string> call in CppBuildSyntax.Calls(text, "target_link_libraries"))
            AddDependencies(call.Skip(1), project);
        foreach (IReadOnlyList<string> call in CppBuildSyntax.Calls(text, "find_package"))
            AddDependencies(call.Take(1), project);
        foreach (IReadOnlyList<string> call in CppBuildSyntax.Calls(text, "add_subdirectory"))
        {
            if (call.Count > 0 && CppBuildSyntax.IsLiteral(call[0]))
            {
                string? directory = CppPath.ResolveWithinRepository(project.Directory, call[0]);
                if (directory is not null)
                {
                    project.LocalReferences++;
                    project.LocalReferenceDirectories.Add(directory);
                }
            }
            else project.Unresolved++;
        }
        project.Tests += CppBuildSyntax.Calls(text, "add_test").Count();
        project.InstallRules += CppBuildSyntax.Calls(text, "install").Count();
        project.GenerationSignals += CppBuildSyntax.Calls(text, "add_custom_command").Count() +
            CppBuildSyntax.Calls(text, "configure_file").Count();
        ReadStandards(text, project);
        return;

        void ReadTargets(string callName, bool executable, bool library)
        {
            foreach (IReadOnlyList<string> call in CppBuildSyntax.Calls(text, callName))
            {
                if (call.Count == 0 || !CppBuildSyntax.IsLiteral(call[0]))
                {
                    project.Unresolved++;
                    continue;
                }
                project.Targets++;
                project.TargetNames.Add(call[0]);
                if (executable) project.Executables++;
                if (library) project.Libraries++;
                if (call.Any(value => value.Equals("MODULE", StringComparison.OrdinalIgnoreCase))) project.Plugins++;
                if (call[0].Contains("test", StringComparison.OrdinalIgnoreCase)) project.Tests++;
                if (call[0].Contains("bench", StringComparison.OrdinalIgnoreCase)) project.Benchmarks++;
                AddSources(Expand(call.Skip(1), variables), project);
            }
        }
    }

    private static void ParseMake(string text, CppBuildAccumulator project)
    {
        project.BuildSystems.Add("make");
        foreach (string original in LogicalLines(text))
        {
            string line = original.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            int colon = line.IndexOf(':');
            int assignment = line.IndexOf('=');
            if (colon > 0 && (assignment < 0 || colon < assignment) && !line.Contains("::=", StringComparison.Ordinal))
            {
                string target = line[..colon].Trim();
                if (!CppBuildSyntax.IsLiteral(target)) { project.Unresolved++; continue; }
                project.Targets++;
                if (target.Contains("test", StringComparison.OrdinalIgnoreCase)) project.Tests++;
                if (target.Contains("bench", StringComparison.OrdinalIgnoreCase)) project.Benchmarks++;
                if (target is "install" or "package") project.InstallRules++;
                AddSources(CppBuildSyntax.SplitArguments(line[(colon + 1)..]), project);
            }
            else if (assignment > 0 && line[assignment - 1] != '?')
            {
                AddSources(CppBuildSyntax.SplitArguments(line[(assignment + 1)..]), project);
                ReadStandards(line, project);
            }
            if (line.StartsWith("include ", StringComparison.Ordinal) ||
                line.StartsWith("-include ", StringComparison.Ordinal)) project.LocalReferences++;
            if (original.StartsWith('\t')) project.GenerationSignals++;
        }
    }

    private static void ParseMeson(string text, CppBuildAccumulator project)
    {
        project.BuildSystems.Add("meson");
        foreach (IReadOnlyList<string> call in CppBuildSyntax.Calls(text, "project"))
            foreach (string value in call.Skip(1)) CppBuildLanguage.Add(value, project);
        ReadTargets("executable", executable: true, library: false);
        ReadTargets("library", executable: false, library: true);
        ReadTargets("shared_library", executable: false, library: true);
        ReadTargets("static_library", executable: false, library: true);
        project.Tests += CppBuildSyntax.Calls(text, "test").Count();
        project.Benchmarks += CppBuildSyntax.Calls(text, "benchmark").Count();
        project.GenerationSignals += CppBuildSyntax.Calls(text, "custom_target").Count() +
            CppBuildSyntax.Calls(text, "generator").Count();
        foreach (IReadOnlyList<string> call in CppBuildSyntax.Calls(text, "dependency"))
            AddDependencies(call.Take(1), project);
        foreach (IReadOnlyList<string> call in CppBuildSyntax.Calls(text, "include_directories"))
            AddIncludeRoots(call, project);
        foreach (IReadOnlyList<string> call in CppBuildSyntax.Calls(text, "files"))
            AddSources(call, project);
        foreach (IReadOnlyList<string> call in CppBuildSyntax.Calls(text, "subdir"))
        {
            if (call.Count == 0 || !CppBuildSyntax.IsLiteral(call[0])) continue;
            string? directory = CppPath.ResolveWithinRepository(project.Directory, call[0]);
            if (directory is null) continue;
            project.LocalReferences++;
            project.LocalReferenceDirectories.Add(directory);
        }
        project.InstallRules += CppBuildSyntax.Calls(text, "install_headers").Count() +
            CppBuildSyntax.Calls(text, "install_data").Count() +
            CppBuildSyntax.Calls(text, "install_subdir").Count();
        ReadStandards(text, project);
        return;

        void ReadTargets(string name, bool executable, bool library)
        {
            foreach (IReadOnlyList<string> call in CppBuildSyntax.Calls(text, name))
            {
                project.Targets++;
                if (call.Count > 0 && CppBuildSyntax.IsLiteral(call[0])) project.TargetNames.Add(call[0]);
                if (executable) project.Executables++;
                if (library) project.Libraries++;
                if (call.Count > 0 && call[0].Contains("test", StringComparison.OrdinalIgnoreCase)) project.Tests++;
                if (call.Count > 0 && call[0].Contains("bench", StringComparison.OrdinalIgnoreCase)) project.Benchmarks++;
                AddSources(call.Skip(1), project);
            }
        }
    }

    private static void ParseVcx(string text, CppBuildAccumulator project)
    {
        project.BuildSystems.Add("msbuild");
        project.DeclaredLanguages.Add("cpp");
        XmlReaderSettings settings = new()
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = CppTextReader.MaximumBytes,
        };
        using StringReader stringReader = new(text);
        using XmlReader xmlReader = XmlReader.Create(stringReader, settings);
        XDocument document = XDocument.Load(xmlReader, LoadOptions.None);
        IEnumerable<XElement> elements = document.Descendants();
        foreach (XElement item in elements.Where(element => element.Name.LocalName == "ClCompile"))
            AddSources([item.Attribute("Include")?.Value ?? string.Empty], project);
        foreach (XElement item in elements.Where(element => element.Name.LocalName == "ClInclude"))
            AddSources([item.Attribute("Include")?.Value ?? string.Empty], project);
        foreach (XElement reference in elements.Where(element => element.Name.LocalName == "ProjectReference"))
        {
            string? path = CppPath.ResolveWithinRepository(
                project.Directory,
                reference.Attribute("Include")?.Value ?? string.Empty);
            if (path is null) continue;
            project.LocalReferences++;
            project.LocalReferenceDirectories.Add(CppPath.Directory(path));
        }
        project.Targets = Math.Max(1, project.Targets);
        string[] types = [.. elements.Where(element => element.Name.LocalName == "ConfigurationType")
            .Select(element => element.Value.Trim())];
        project.Executables += types.Count(value => value == "Application");
        project.Libraries += types.Count(value => value is "StaticLibrary" or "DynamicLibrary");
        project.Plugins += types.Count(value => value == "DynamicLibrary");
        foreach (string standard in elements.Where(element => element.Name.LocalName == "LanguageStandard")
            .Select(element => CppBuildStandard.Normalize(element.Value)))
            if (standard.Length > 0) project.Standards.Add(standard);
        foreach (string includePath in elements
            .Where(element => element.Name.LocalName == "AdditionalIncludeDirectories")
            .SelectMany(element => element.Value.Split(';', StringSplitOptions.RemoveEmptyEntries)))
            AddIncludeRoots([includePath], project);
        project.CompileDefinitions += elements
            .Where(element => element.Name.LocalName == "PreprocessorDefinitions")
            .SelectMany(element => element.Value.Split(';', StringSplitOptions.RemoveEmptyEntries))
            .Count(value => !value.StartsWith("%(", StringComparison.Ordinal));
        project.Unresolved += elements.Count(element => element.Attribute("Condition") is not null);
        project.GenerationSignals += elements.Count(element => element.Name.LocalName is "CustomBuild" or "CustomBuildStep");
    }

    private static void AddSources(IEnumerable<string> values, CppBuildAccumulator project)
    {
        foreach (string value in values)
        {
            if (!CppBuildSyntax.IsLiteral(value) || !(CppPath.IsSource(value) || CppPath.IsHeader(value))) continue;
            string? path = CppPath.ResolveWithinRepository(project.Directory, value);
            if (path is not null)
            {
                project.ExplicitSources.Add(path);
                CppBuildLanguage.Add(Path.GetExtension(path), project);
            }
        }
    }

    private static void AddIncludeRoots(IEnumerable<string> values, CppBuildAccumulator project)
    {
        foreach (string value in values.Where(CppBuildSyntax.IsLiteral))
        {
            string? path = CppPath.ResolveWithinRepository(project.Directory, value);
            if (path is not null) project.IncludeRoots.Add(path);
        }
    }

    private static void AddDependencies(IEnumerable<string> values, CppBuildAccumulator project)
    {
        foreach (string value in values.Where(CppBuildSyntax.IsLiteral))
            if (value is not ("PUBLIC" or "PRIVATE" or "INTERFACE")) project.Dependencies.Add(value);
    }

    private static IEnumerable<string> Expand(
        IEnumerable<string> values,
        Dictionary<string, IReadOnlyList<string>> variables)
    {
        foreach (string value in values)
        {
            if (value.StartsWith("${", StringComparison.Ordinal) && value.EndsWith('}') &&
                variables.TryGetValue(value[2..^1], out IReadOnlyList<string>? expanded))
            {
                foreach (string item in expanded) yield return item;
            }
            else yield return value;
        }
    }

    private static void ReadStandards(string text, CppBuildAccumulator project)
    {
        string lower = text.ToLowerInvariant();
        foreach (string standard in new[]
        {
            "c99", "c11", "c17", "c23", "c++11", "c++14", "c++17", "c++20", "c++23",
        })
            if (lower.Contains(standard, StringComparison.Ordinal)) project.Standards.Add(standard);
    }

    private static string[] LogicalLines(string text) =>
        text.Replace("\\\r\n", string.Empty, StringComparison.Ordinal)
            .Replace("\\\n", string.Empty, StringComparison.Ordinal)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

    private static bool IsIdentifier(string value) => value.Length > 0 &&
        (char.IsAsciiLetter(value[0]) || value[0] == '_') &&
        value.All(character => char.IsAsciiLetterOrDigit(character) || character == '_');

    private static bool IsBuildFile(EvidenceFact fact)
    {
        if (fact.Kind != EvidenceKinds.File || fact.Tags.Any(tag => tag is
            "classification:generated" or "classification:minified" or "classification:vendored" or
            "content:binary")) return false;
        string name = Path.GetFileName(fact.Scope).ToLowerInvariant();
        string extension = Path.GetExtension(fact.Scope).ToLowerInvariant();
        return name is "cmakelists.txt" or "makefile" or "gnumakefile" or
            "meson.build" or "meson.options" or "meson_options.txt" or
            "cmakepresets.json" or "cmakeuserpresets.json" or
            "compile_commands.json" or "vcpkg.json" or "conanfile.txt" ||
            extension is ".cmake" or ".mk" or ".vcxproj" or ".props";
    }
}
