using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.JavaScript;

internal sealed class JavaScriptWorkspaceReader(RepositoryTextReader textReader)
{
    private const long MaximumWorkspaceFileBytes = 1024 * 1024;

    private readonly RepositoryTextReader _textReader = textReader;

    public async Task<(IReadOnlyList<JavaScriptWorkspaceDeclaration> Workspaces, IReadOnlyList<Diagnostic> Diagnostics)>
        ReadAsync(
            RepositoryEvidence evidence,
            IReadOnlyList<JavaScriptPackageModel> packages,
            CancellationToken cancellationToken)
    {
        List<JavaScriptWorkspaceDeclaration> declarations = [.. packages
            .Where(package => package.WorkspacePatterns.Count > 0)
            .Select(package => new JavaScriptWorkspaceDeclaration(
                package.Scope,
                package.ManifestPath,
                package.PackageManager ?? "package-json",
                package.WorkspacePatterns))];
        List<Diagnostic> diagnostics = [];

        foreach (EvidenceFact fact in evidence.Facts
            .Where(IsPnpmWorkspaceFile)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal))
        {
            RepositoryTextReadResult read = await _textReader.ReadAsync(
                fact,
                MaximumWorkspaceFileBytes,
                "FB4008",
                cancellationToken).ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
                continue;
            }

            IReadOnlyList<string> patterns = ReadPnpmPatterns(read.Text!);
            if (patterns.Count > 0)
            {
                declarations.Add(new JavaScriptWorkspaceDeclaration(
                    GetDirectory(fact.Scope),
                    fact.Scope,
                    "pnpm",
                    patterns));
            }
        }

        return (declarations, diagnostics);
    }

    private static IReadOnlyList<string> ReadPnpmPatterns(string text)
    {
        List<string> patterns = [];
        bool inPackages = false;
        foreach (string rawLine in text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n'))
        {
            string trimmed = rawLine.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            if (!char.IsWhiteSpace(rawLine[0]))
            {
                inPackages = trimmed.Equals("packages:", StringComparison.Ordinal);
                continue;
            }

            if (!inPackages || !trimmed.StartsWith('-'))
            {
                continue;
            }

            string value = trimmed[1..].Trim();
            int commentIndex = value.IndexOf(" #", StringComparison.Ordinal);
            if (commentIndex >= 0)
            {
                value = value[..commentIndex].TrimEnd();
            }

            if (value.Length >= 2 &&
                ((value[0] == '\'' && value[^1] == '\'') ||
                 (value[0] == '"' && value[^1] == '"')))
            {
                value = value[1..^1];
            }

            value = value.Replace('\\', '/').Trim();
            if (value.Length is > 0 and <= 1024)
            {
                patterns.Add(value);
            }
        }

        return [.. patterns.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)];
    }

    private static bool IsPnpmWorkspaceFile(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.File &&
        (Path.GetFileName(fact.Scope).Equals("pnpm-workspace.yaml", StringComparison.OrdinalIgnoreCase) ||
         Path.GetFileName(fact.Scope).Equals("pnpm-workspace.yml", StringComparison.OrdinalIgnoreCase)) &&
        !fact.Tags.Contains("content:binary", StringComparer.Ordinal);

    private static string GetDirectory(string path)
    {
        int separator = path.LastIndexOf('/');
        return separator < 0 ? "." : path[..separator];
    }
}
