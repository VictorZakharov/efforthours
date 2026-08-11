using System.Xml;
using System.Xml.Linq;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Java;

internal static class JavaMavenParser
{
    public static void Parse(
        string text,
        string path,
        JavaProjectMetadata metadata,
        List<Diagnostic> diagnostics)
    {
        XDocument document;
        try
        {
            XmlReaderSettings settings = new()
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = JavaTextReader.MaximumBytes,
            };
            using StringReader input = new(text);
            using XmlReader reader = XmlReader.Create(input, settings);
            document = XDocument.Load(reader, LoadOptions.None);
        }
        catch (XmlException)
        {
            diagnostics.Add(JavaEvidence.Diagnostic(
                "FB8107",
                DiagnosticSeverity.Warning,
                $"Maven descriptor '{path}' is not bounded well-formed XML and was only inventoried.",
                path));
            return;
        }

        XElement? project = document.Root;
        if (project is null || project.Name.LocalName != "project") return;
        string directory = JavaPath.Directory(path);
        string artifact = Literal(DirectValue(project, "artifactId"));
        string group = Literal(DirectValue(project, "groupId"));
        if (group.Length == 0)
        {
            XElement? parent = Direct(project, "parent");
            if (parent is not null) group = Literal(DirectValue(parent, "groupId"));
        }

        if (artifact.Length > 0) metadata.Name ??= artifact;
        if (artifact.Length > 0)
            metadata.Coordinate ??= group.Length > 0 ? $"{group}:{artifact}" : artifact;
        string packaging = Literal(DirectValue(project, "packaging"));
        if (packaging.Length > 0) metadata.Packaging ??= packaging;
        metadata.MavenProfiles += Direct(project, "profiles")?.Elements()
            .Count(element => element.Name.LocalName == "profile") ?? 0;
        metadata.UnresolvedValues += project.DescendantNodes()
            .OfType<XText>()
            .Count(node => node.Value.Contains("${", StringComparison.Ordinal));

        foreach (XElement dependency in project.Descendants()
            .Where(element => element.Name.LocalName == "dependency"))
        {
            string rawDependencyGroup = DirectValue(dependency, "groupId");
            string rawDependencyArtifact = DirectValue(dependency, "artifactId");
            if (!IsLiteral(rawDependencyGroup) || !IsLiteral(rawDependencyArtifact)) continue;
            string dependencyGroup = rawDependencyGroup;
            string dependencyArtifact = rawDependencyArtifact;
            if (dependencyArtifact.Length == 0) continue;
            metadata.Dependencies.Add(dependencyGroup.Length == 0
                ? dependencyArtifact
                : $"{dependencyGroup}:{dependencyArtifact}");
        }

        foreach (XElement plugin in project.Descendants()
            .Where(element => element.Name.LocalName == "plugin"))
        {
            string rawPluginGroup = DirectValue(plugin, "groupId");
            string rawPluginArtifact = DirectValue(plugin, "artifactId");
            if (!IsLiteral(rawPluginGroup) || !IsLiteral(rawPluginArtifact)) continue;
            string pluginGroup = rawPluginGroup;
            string pluginArtifact = rawPluginArtifact;
            if (pluginArtifact.Length == 0) continue;
            metadata.Plugins.Add(pluginGroup.Length == 0
                ? pluginArtifact
                : $"{pluginGroup}:{pluginArtifact}");
        }

        metadata.AnnotationProcessors += project.Descendants()
            .Where(element => element.Name.LocalName == "annotationProcessorPaths")
            .Sum(groupElement => groupElement.Elements()
                .Count(element => element.Name.LocalName == "path"));

        XElement? modules = Direct(project, "modules");
        if (modules is null) return;
        foreach (string module in modules.Elements()
            .Where(element => element.Name.LocalName == "module")
            .Select(element => element.Value.Trim())
            .Where(value => value.Length > 0))
        {
            if (module.Contains("${", StringComparison.Ordinal)) continue;
            string? target = JavaPath.ResolveWithinRepository(directory, module);
            if (target is not null) metadata.LocalProjectDirectories.Add(target);
            else diagnostics.Add(UnsafePath(path, "Maven reactor module"));
        }
    }

    private static XElement? Direct(XElement parent, string localName) => parent.Elements()
        .FirstOrDefault(element => element.Name.LocalName == localName);

    private static string DirectValue(XElement parent, string localName) =>
        Direct(parent, localName)?.Value.Trim() ?? string.Empty;

    private static string Literal(string value) =>
        value.Contains("${", StringComparison.Ordinal) ? string.Empty : value;

    private static bool IsLiteral(string value) =>
        !value.Contains("${", StringComparison.Ordinal);

    private static Diagnostic UnsafePath(string path, string kind) => JavaEvidence.Diagnostic(
        "FB8103",
        DiagnosticSeverity.Warning,
        $"A Java {kind} in '{path}' resolved outside repository scope and was not followed.",
        path);
}
