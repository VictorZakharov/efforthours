using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Java;

internal sealed class JavaProjectReader(JavaTextReader textReader)
{
    public async Task<JavaProjectReadResult> ReadAsync(
        IReadOnlyList<EvidenceFact> fileFacts,
        bool hasMaintainedSource,
        CancellationToken cancellationToken)
    {
        List<Diagnostic> diagnostics = [];
        Dictionary<string, JavaProjectMetadata> projects = new(StringComparer.Ordinal);
        EvidenceFact[] descriptors = [.. fileFacts
            .Where(IsBuildDescriptor)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        foreach (EvidenceFact descriptor in descriptors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JavaTextReadResult read = await textReader.ReadAsync(descriptor, cancellationToken)
                .ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
                continue;
            }

            string directory = JavaPath.Directory(descriptor.Scope);
            JavaProjectMetadata metadata = Get(projects, directory);
            metadata.ManifestPaths.Add(descriptor.Scope);
            string name = Path.GetFileName(descriptor.Scope).ToLowerInvariant();
            if (name == "pom.xml")
            {
                metadata.BuildSystems.Add("maven");
                JavaMavenParser.Parse(read.Text!, descriptor.Scope, metadata, diagnostics);
            }
            else if (name is "build.gradle" or "build.gradle.kts")
            {
                metadata.BuildSystems.Add("gradle");
                JavaGradleParser.ParseBuild(read.Text!, metadata);
            }
            else
            {
                metadata.BuildSystems.Add("gradle");
                JavaGradleParser.ParseSettings(read.Text!, descriptor.Scope, metadata, diagnostics);
            }
        }

        AddDiscoveredProjects(projects, fileFacts);
        if (projects.Count == 0 && hasMaintainedSource) Get(projects, ".");
        foreach ((string directory, JavaProjectMetadata metadata) in projects)
        {
            if (metadata.BuildSystems.Count > 1)
                diagnostics.Add(JavaEvidence.Diagnostic(
                    "FB8108",
                    DiagnosticSeverity.Information,
                    $"Java scope '{directory}' contains both Maven and Gradle metadata; both were inventoried without selecting an active build.",
                    metadata.ManifestPaths.Order(StringComparer.Ordinal).FirstOrDefault()));
        }

        return new JavaProjectReadResult(
            [.. projects.Select(pair => ToModel(pair.Key, pair.Value))
                .OrderBy(project => project.Directory, StringComparer.Ordinal)],
            diagnostics);
    }

    private static void AddDiscoveredProjects(
        Dictionary<string, JavaProjectMetadata> projects,
        IReadOnlyList<EvidenceFact> facts)
    {
        HashSet<string> sourceDirectories = [.. facts
            .Where(fact => fact.Tags.Contains("language:java", StringComparer.Ordinal))
            .Select(fact => JavaPath.Directory(fact.Scope))];
        string[] referenced = [.. projects.Values
            .SelectMany(project => project.LocalProjectDirectories)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
        foreach (string directory in referenced)
        {
            bool exists = facts.Any(fact => JavaPath.IsWithin(fact.Scope, directory));
            if (exists || sourceDirectories.Any(source => JavaPath.IsWithin(source, directory)))
                Get(projects, directory);
        }
    }

    private static JavaProjectMetadata Get(
        Dictionary<string, JavaProjectMetadata> projects,
        string directory)
    {
        if (!projects.TryGetValue(directory, out JavaProjectMetadata? metadata))
        {
            metadata = new JavaProjectMetadata();
            projects[directory] = metadata;
        }

        return metadata;
    }

    private static JavaProjectModel ToModel(string directory, JavaProjectMetadata metadata) => new()
    {
        Directory = directory,
        Name = metadata.Name ?? (directory == "." ? "repository" : Path.GetFileName(directory)),
        Coordinate = metadata.Coordinate,
        Packaging = metadata.Packaging,
        Role = "library",
        BuildSystems = [.. metadata.BuildSystems.Order(StringComparer.Ordinal)],
        ManifestPaths = [.. metadata.ManifestPaths.Order(StringComparer.Ordinal)],
        Dependencies = [.. metadata.Dependencies.Order(StringComparer.Ordinal)],
        Plugins = [.. metadata.Plugins.Order(StringComparer.Ordinal)],
        LocalProjectDirectories = [.. metadata.LocalProjectDirectories.Order(StringComparer.Ordinal)],
        UnresolvedValues = metadata.UnresolvedValues,
        MavenProfiles = metadata.MavenProfiles,
        AnnotationProcessors = metadata.AnnotationProcessors,
    };

    private static bool IsBuildDescriptor(EvidenceFact fact)
    {
        if (fact.Kind != EvidenceKinds.File || fact.Tags.Any(tag => tag is
            "classification:generated" or "classification:vendored" or "content:binary")) return false;
        string name = Path.GetFileName(fact.Scope).ToLowerInvariant();
        return name is "pom.xml" or "build.gradle" or "build.gradle.kts" or
            "settings.gradle" or "settings.gradle.kts";
    }
}
