using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Go;

internal sealed class GoProjectReader(GoTextReader textReader)
{
    public async Task<GoProjectReadResult> ReadAsync(
        IReadOnlyList<EvidenceFact> fileFacts,
        bool hasMaintainedSource,
        CancellationToken cancellationToken)
    {
        EvidenceFact[] manifestCandidates = [.. fileFacts
            .Where(IsGoMod)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        List<Diagnostic> diagnostics = [];
        List<EvidenceFact> manifests = [];
        foreach (IGrouping<string, EvidenceFact> group in manifestCandidates
            .GroupBy(fact => GoPath.Directory(fact.Scope), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            EvidenceFact[] candidates = [.. group
                .OrderBy(fact => Path.GetFileName(fact.Scope) == "go.mod" ? 0 : 1)
                .ThenBy(fact => fact.Scope, StringComparer.Ordinal)];
            manifests.Add(candidates[0]);
            if (candidates.Length > 1)
                diagnostics.Add(GoEvidence.Diagnostic(
                    "FB8007",
                    DiagnosticSeverity.Warning,
                    $"Multiple case-insensitive go.mod candidates were found in '{group.Key}'; only '{candidates[0].Scope}' was analyzed.",
                    candidates[0].Scope));
        }

        List<GoModuleModel> modules = [];
        foreach (EvidenceFact manifest in manifests)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GoTextReadResult read = await textReader.ReadAsync(manifest, cancellationToken)
                .ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
                continue;
            }

            string directory = GoPath.Directory(manifest.Scope);
            GoModuleMetadata metadata = ParseGoMod(read.Text!, directory, diagnostics, manifest.Scope);
            string modulePath = metadata.ModulePath ??
                (directory == "." ? "repository" : Path.GetFileName(directory));
            modules.Add(new GoModuleModel
            {
                Directory = directory,
                ModulePath = modulePath,
                Role = "library",
                ManifestPaths = [manifest.Scope],
                Dependencies = [.. metadata.Dependencies.Order(StringComparer.Ordinal)],
                LocalReplacements = [.. metadata.LocalReplacements
                    .Distinct()
                    .OrderBy(item => item.ModulePath, StringComparer.Ordinal)
                    .ThenBy(item => item.TargetDirectory, StringComparer.Ordinal)],
            });
        }

        if (modules.Count == 0 && hasMaintainedSource)
        {
            modules.Add(new GoModuleModel
            {
                Directory = ".",
                ModulePath = "repository",
                Role = "library",
            });
        }

        GoWorkspaceModel? workspace = await ReadWorkspaceAsync(
            fileFacts,
            diagnostics,
            cancellationToken).ConfigureAwait(false);
        return new GoProjectReadResult(
            [.. modules.OrderBy(module => module.Directory, StringComparer.Ordinal)],
            workspace,
            diagnostics);
    }

    private async Task<GoWorkspaceModel?> ReadWorkspaceAsync(
        IReadOnlyList<EvidenceFact> fileFacts,
        List<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        EvidenceFact[] candidates = [.. fileFacts
            .Where(candidate => Path.GetFileName(candidate.Scope).Equals(
                "go.work",
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => candidate.Scope.Count(character => character == '/'))
            .ThenBy(candidate => candidate.Scope, StringComparer.Ordinal)];
        EvidenceFact? fact = candidates.FirstOrDefault();
        if (fact is null) return null;
        if (candidates.Length > 1)
            diagnostics.Add(GoEvidence.Diagnostic(
                "FB8008",
                DiagnosticSeverity.Warning,
                $"Multiple go.work files were found; only the shallowest deterministic workspace '{fact.Scope}' was analyzed.",
                fact.Scope));
        GoTextReadResult read = await textReader.ReadAsync(fact, cancellationToken)
            .ConfigureAwait(false);
        if (read.Diagnostic is not null)
        {
            diagnostics.Add(read.Diagnostic);
            return null;
        }

        string directory = GoPath.Directory(fact.Scope);
        List<string> uses = [];
        List<GoLocalReplacement> replacements = [];
        string block = string.Empty;
        foreach (string rawLine in Lines(read.Text!))
        {
            string line = StripComment(rawLine).Trim();
            if (line.Length == 0) continue;
            if (line.EndsWith('('))
            {
                block = FirstWord(line);
                continue;
            }
            if (line == ")")
            {
                block = string.Empty;
                continue;
            }

            string directive = block.Length > 0 ? block : FirstWord(line);
            string value = block.Length > 0 ? line : Remainder(line);
            if (directive == "use")
            {
                string? target = ResolveLocal(directory, Unquote(FirstWord(value)));
                if (target is not null) uses.Add(target);
                else diagnostics.Add(UnsafePathDiagnostic(fact.Scope, "workspace module"));
            }
            else if (directive == "replace" && TryReplacement(value, directory, out GoLocalReplacement? replacement))
            {
                replacements.Add(replacement!);
            }
        }

        return new GoWorkspaceModel(
            fact.Scope,
            [.. uses.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            [.. replacements.Distinct().OrderBy(item => item.ModulePath, StringComparer.Ordinal)]);
    }

    private static GoModuleMetadata ParseGoMod(
        string text,
        string directory,
        List<Diagnostic> diagnostics,
        string path)
    {
        GoModuleMetadata metadata = new();
        string block = string.Empty;
        foreach (string rawLine in Lines(text))
        {
            string line = StripComment(rawLine).Trim();
            if (line.Length == 0) continue;
            if (line.EndsWith('('))
            {
                block = FirstWord(line);
                continue;
            }
            if (line == ")")
            {
                block = string.Empty;
                continue;
            }

            string directive = block.Length > 0 ? block : FirstWord(line);
            string value = block.Length > 0 ? line : Remainder(line);
            if (directive == "module" && metadata.ModulePath is null)
            {
                metadata.ModulePath = Unquote(FirstWord(value));
            }
            else if (directive == "require")
            {
                string dependency = Unquote(FirstWord(value));
                if (dependency.Length > 0) metadata.Dependencies.Add(dependency);
            }
            else if (directive == "replace")
            {
                string sourceModule = Unquote(FirstWord(value));
                if (sourceModule.Length > 0) metadata.Dependencies.Add(sourceModule);
                if (TryReplacement(value, directory, out GoLocalReplacement? replacement))
                    metadata.LocalReplacements.Add(replacement!);
                else if (value.Contains("=>", StringComparison.Ordinal) && IsLocalTarget(value))
                    diagnostics.Add(UnsafePathDiagnostic(path, "local replacement"));
            }
        }

        return metadata;
    }

    private static bool TryReplacement(
        string value,
        string directory,
        out GoLocalReplacement? replacement)
    {
        replacement = null;
        int arrow = value.IndexOf("=>", StringComparison.Ordinal);
        if (arrow < 0) return false;
        string modulePath = Unquote(FirstWord(value[..arrow]));
        string targetValue = Unquote(FirstWord(value[(arrow + 2)..]));
        if (modulePath.Length == 0 || !IsLocalPath(targetValue)) return false;
        string? target = ResolveLocal(directory, targetValue);
        if (target is null) return false;
        replacement = new GoLocalReplacement(modulePath, target);
        return true;
    }

    private static string? ResolveLocal(string directory, string value)
    {
        string normalized = value.Replace('\\', '/').TrimEnd('/');
        if (!IsLocalPath(normalized)) return null;
        List<string> segments = directory == "."
            ? []
            : [.. directory.Split('/', StringSplitOptions.RemoveEmptyEntries)];
        foreach (string segment in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".") continue;
            if (segment == "..")
            {
                if (segments.Count == 0) return null;
                segments.RemoveAt(segments.Count - 1);
            }
            else
            {
                segments.Add(segment);
            }
        }

        return segments.Count == 0 ? "." : string.Join('/', segments);
    }

    private static bool IsGoMod(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.File &&
        Path.GetFileName(fact.Scope).Equals("go.mod", StringComparison.OrdinalIgnoreCase) &&
        !fact.Tags.Any(tag => tag is
            "classification:generated" or "classification:vendored" or "content:binary");

    private static bool IsLocalTarget(string value)
    {
        int arrow = value.IndexOf("=>", StringComparison.Ordinal);
        return arrow >= 0 && IsLocalPath(Unquote(FirstWord(value[(arrow + 2)..])));
    }

    private static bool IsLocalPath(string value) =>
        value == "." || value.StartsWith("./", StringComparison.Ordinal) ||
        value == ".." || value.StartsWith("../", StringComparison.Ordinal);

    private static Diagnostic UnsafePathDiagnostic(string path, string kind) =>
        GoEvidence.Diagnostic(
            "FB8003",
            DiagnosticSeverity.Warning,
            $"A Go {kind} in '{path}' resolved outside repository scope and was not followed.",
            path);

    private static string[] Lines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    private static string StripComment(string line)
    {
        int comment = line.IndexOf("//", StringComparison.Ordinal);
        return comment < 0 ? line : line[..comment];
    }

    private static string FirstWord(string value) =>
        value.Trim().Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;

    private static string Remainder(string value)
    {
        string trimmed = value.Trim();
        int separator = trimmed.IndexOfAny([' ', '\t']);
        return separator < 0 ? string.Empty : trimmed[(separator + 1)..].Trim();
    }

    private static string Unquote(string value) => value.Trim().Trim('"', '`');
}
