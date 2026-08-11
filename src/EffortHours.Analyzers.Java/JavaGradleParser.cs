using System.Text;
using System.Text.RegularExpressions;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Java;

internal static partial class JavaGradleParser
{
    public static void ParseBuild(string text, JavaProjectMetadata metadata)
    {
        string cleaned = StripComments(text);
        foreach (Match match in DependencyRegex().Matches(cleaned))
        {
            string configuration = match.Groups["configuration"].Value;
            string value = match.Groups["value"].Value;
            if (configuration.Contains("annotationProcessor", StringComparison.OrdinalIgnoreCase))
                metadata.AnnotationProcessors++;
            if (value.StartsWith("project", StringComparison.OrdinalIgnoreCase)) continue;
            string? coordinate = WithoutVersion(TrimQuotes(value.Trim().TrimEnd(')')));
            if (coordinate is not null) metadata.Dependencies.Add(coordinate);
        }

        foreach (Match match in ProjectDependencyRegex().Matches(cleaned))
        {
            string? target = GradleProjectPath(match.Groups["value"].Value);
            if (target is not null) metadata.LocalProjectDirectories.Add(target);
        }

        foreach (Match match in PluginRegex().Matches(cleaned))
        {
            string plugin = match.Groups["value"].Value;
            if (!IsLiteral(plugin)) continue;
            metadata.Plugins.Add(plugin);
            if (plugin == "application") metadata.Packaging ??= "application";
            else if (plugin == "java-library") metadata.Packaging ??= "library";
            else if (plugin == "org.springframework.boot") metadata.Packaging ??= "spring-boot";
        }

        metadata.UnresolvedValues += DynamicSignalRegex().Count(cleaned);
    }

    public static void ParseSettings(
        string text,
        string path,
        JavaProjectMetadata metadata,
        List<Diagnostic> diagnostics)
    {
        string cleaned = StripComments(text);
        Match rootName = RootNameRegex().Match(cleaned);
        if (rootName.Success && IsLiteral(rootName.Groups["value"].Value))
            metadata.Name ??= rootName.Groups["value"].Value;
        Dictionary<string, string> mappings = ProjectDirectoryMappings(cleaned, path, diagnostics);
        foreach (Match statement in IncludeStatementRegex().Matches(cleaned))
        {
            foreach (Match quoted in QuotedValueRegex().Matches(statement.Groups["values"].Value))
            {
                string project = quoted.Groups["value"].Value;
                if (!IsLiteral(project)) continue;
                string? target = mappings.GetValueOrDefault(project);
                if (target is null)
                {
                    string? relative = GradleProjectPath(project);
                    if (relative is not null)
                        target = JavaPath.ResolveWithinRepository(JavaPath.Directory(path), relative);
                }
                if (target is not null) metadata.LocalProjectDirectories.Add(target);
                else diagnostics.Add(UnsafePath(path, "Gradle included project"));
            }
        }

        foreach (Match includedBuild in IncludeBuildRegex().Matches(cleaned))
        {
            if (!IsLiteral(includedBuild.Groups["value"].Value)) continue;
            string? target = JavaPath.ResolveWithinRepository(
                JavaPath.Directory(path),
                includedBuild.Groups["value"].Value);
            if (target is not null) metadata.LocalProjectDirectories.Add(target);
            else diagnostics.Add(UnsafePath(path, "Gradle included build"));
        }

        metadata.UnresolvedValues += DynamicSignalRegex().Count(cleaned);
    }

    private static Dictionary<string, string> ProjectDirectoryMappings(
        string text,
        string path,
        List<Diagnostic> diagnostics)
    {
        Dictionary<string, string> mappings = new(StringComparer.Ordinal);
        foreach (Match match in ProjectDirectoryRegex().Matches(text))
        {
            if (!IsLiteral(match.Groups["directory"].Value)) continue;
            string? target = JavaPath.ResolveWithinRepository(
                JavaPath.Directory(path),
                match.Groups["directory"].Value);
            if (target is not null) mappings[match.Groups["project"].Value] = target;
            else diagnostics.Add(UnsafePath(path, "Gradle projectDir mapping"));
        }

        return mappings;
    }

    private static string? GradleProjectPath(string value)
    {
        string normalized = value.Trim().Trim(':').Replace(':', '/');
        return normalized.Length == 0 || normalized.Contains("..", StringComparison.Ordinal) ||
            !IsLiteral(normalized)
            ? null
            : normalized;
    }

    private static string? WithoutVersion(string coordinate)
    {
        string[] parts = coordinate.Split(':');
        return parts.Length >= 2 && IsLiteral(parts[0]) && IsLiteral(parts[1]) &&
            parts[0].Length > 0 && parts[1].Length > 0
            ? $"{parts[0]}:{parts[1]}"
            : null;
    }

    private static bool IsLiteral(string value) => !value.Contains('$');

    private static string TrimQuotes(string value) => value.Trim().Trim('\'', '"');

    private static string StripComments(string value)
    {
        StringBuilder result = new(value.Length);
        int index = 0;
        char quote = '\0';
        bool triple = false;
        while (index < value.Length)
        {
            char current = value[index];
            if (quote != '\0')
            {
                if (!triple && current == '\\' && index + 1 < value.Length)
                {
                    result.Append(current);
                    result.Append(value[index + 1]);
                    index += 2;
                }
                else if (triple && current == quote && index + 2 < value.Length &&
                    value[index + 1] == quote && value[index + 2] == quote)
                {
                    result.Append(quote, 3);
                    index += 3;
                    quote = '\0';
                    triple = false;
                }
                else
                {
                    result.Append(current);
                    index++;
                    if (!triple && current == quote) quote = '\0';
                }

                continue;
            }

            if (current is '\'' or '"')
            {
                quote = current;
                triple = index + 2 < value.Length && value[index + 1] == current &&
                    value[index + 2] == current;
                result.Append(current, triple ? 3 : 1);
                index += triple ? 3 : 1;
            }
            else if (current == '/' && index + 1 < value.Length && value[index + 1] == '/')
            {
                index += 2;
                while (index < value.Length && value[index] is not ('\r' or '\n')) index++;
            }
            else if (current == '/' && index + 1 < value.Length && value[index + 1] == '*')
            {
                index += 2;
                while (index + 1 < value.Length && !(value[index] == '*' && value[index + 1] == '/'))
                {
                    if (value[index] is '\r' or '\n') result.Append(value[index]);
                    index++;
                }

                if (index + 1 < value.Length) index += 2;
            }
            else
            {
                result.Append(current);
                index++;
            }
        }

        return result.ToString();
    }

    private static Diagnostic UnsafePath(string path, string kind) => JavaEvidence.Diagnostic(
        "FB8103",
        DiagnosticSeverity.Warning,
        $"A Java {kind} in '{path}' resolved outside repository scope and was not followed.",
        path);

    [GeneratedRegex("""(?m)\b(?<configuration>(?:(?:test|integrationTest|testFixtures)(?:Api|Implementation|CompileOnly|RuntimeOnly|AnnotationProcessor)|api|implementation|compileOnly|runtimeOnly|annotationProcessor))\b\s*(?:\(\s*)?(?<value>project\s*\(\s*['\"][^'\"]+['\"]\s*\)|['\"][^'\"]+['\"])""", RegexOptions.IgnoreCase)]
    private static partial Regex DependencyRegex();

    [GeneratedRegex("""\bproject\s*\(\s*['\"](?<value>:[^'\"]+)['\"]\s*\)""")]
    private static partial Regex ProjectDependencyRegex();

    [GeneratedRegex("""\bid\s*(?:\(\s*)?['\"](?<value>[^'\"]+)['\"]""")]
    private static partial Regex PluginRegex();

    [GeneratedRegex("""(?m)\brootProject\.name\s*=\s*['\"](?<value>[^'\"]+)['\"]""")]
    private static partial Regex RootNameRegex();

    [GeneratedRegex(@"(?m)\binclude\b\s*(?:\(\s*)?(?<values>[^\r\n\)]*)")]
    private static partial Regex IncludeStatementRegex();

    [GeneratedRegex("""['\"](?<value>[^'\"]+)['\"]""")]
    private static partial Regex QuotedValueRegex();

    [GeneratedRegex("""\bincludeBuild\s*\(\s*['\"](?<value>[^'\"]+)['\"]\s*\)""")]
    private static partial Regex IncludeBuildRegex();

    [GeneratedRegex("""\bproject\s*\(\s*['\"](?<project>:[^'\"]+)['\"]\s*\)\.projectDir\s*=\s*file\s*\(\s*['\"](?<directory>[^'\"]+)['\"]\s*\)""")]
    private static partial Regex ProjectDirectoryRegex();

    [GeneratedRegex(@"\$\{|\$[A-Za-z_]|\b(?:providers|findProperty|hasProperty|System\.getenv|fileTree|evaluationDependsOn|alias|libs\.)\b")]
    private static partial Regex DynamicSignalRegex();
}
