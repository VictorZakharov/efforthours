using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Python;

internal sealed class PythonProjectReader(PythonTextReader textReader)
{
    private static readonly HashSet<string> MetadataNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "pyproject.toml", "setup.cfg", "setup.py", "pipfile", "requirements.txt",
        "requirements-dev.txt", "requirements-test.txt", "requirements-prod.txt",
        "requirements.in", "poetry.lock", "pdm.lock", "uv.lock",
    };

    public async Task<PythonProjectReadResult> ReadAsync(
        IReadOnlyList<EvidenceFact> fileFacts,
        CancellationToken cancellationToken)
    {
        EvidenceFact[] metadataFiles = [.. fileFacts
            .Where(IsMetadataFile)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        string[] packageDirectories = [.. metadataFiles
            .Where(fact => IsPrimaryManifest(Path.GetFileName(fact.Scope)))
            .Select(fact => Directory(fact.Scope))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];
        if (packageDirectories.Length == 0)
        {
            packageDirectories = ["."];
        }
        else if (!packageDirectories.Contains(".", StringComparer.Ordinal) &&
            fileFacts.Any(fact => fact.Kind == EvidenceKinds.File &&
                fact.Tags.Any(tag => tag is "language:python" or "language:jupyter") &&
                !packageDirectories.Any(directory => IsWithin(fact.Scope, directory))))
        {
            packageDirectories = [".", .. packageDirectories];
        }

        List<PythonPackageModel> packages = [];
        List<Diagnostic> diagnostics = [];
        foreach (string directory in packageDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PythonMetadata metadata = new();
            EvidenceFact[] owned = [.. metadataFiles
                .Where(fact => Directory(fact.Scope) == directory)
                .OrderBy(fact => MetadataPriority(Path.GetFileName(fact.Scope)))
                .ThenBy(fact => fact.Scope, StringComparer.Ordinal)];
            foreach (EvidenceFact file in owned)
            {
                PythonTextReadResult read = await textReader.ReadAsync(file, cancellationToken)
                    .ConfigureAwait(false);
                if (read.Diagnostic is not null)
                {
                    diagnostics.Add(read.Diagnostic);
                    continue;
                }

                ParseMetadata(Path.GetFileName(file.Scope), read.Text!, metadata);
            }

            string name = metadata.Name ?? (directory == "."
                ? "repository"
                : Path.GetFileName(directory));
            packages.Add(new PythonPackageModel
            {
                Directory = directory,
                Name = NormalizeName(name),
                Role = Role(metadata),
                ManifestPaths = [.. owned.Select(fact => fact.Scope)],
                Dependencies = [.. metadata.Dependencies.Order(StringComparer.Ordinal)],
                Scripts = [.. metadata.Scripts.Order(StringComparer.Ordinal)],
            });
        }

        return new PythonProjectReadResult(packages, diagnostics);
    }

    private static void ParseMetadata(string name, string text, PythonMetadata metadata)
    {
        if (name.Equals("pyproject.toml", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("pipfile", StringComparison.OrdinalIgnoreCase))
        {
            ParseTomlLike(text, metadata);
        }
        else if (name.Equals("setup.cfg", StringComparison.OrdinalIgnoreCase))
        {
            ParseSetupCfg(text, metadata);
        }
        else if (name.Equals("setup.py", StringComparison.OrdinalIgnoreCase))
        {
            ParseLiteralSetupPy(text, metadata);
        }
        else if (name.StartsWith("requirements", StringComparison.OrdinalIgnoreCase))
        {
            foreach (string line in Lines(text)) AddRequirement(line, metadata);
        }
    }

    private static void ParseTomlLike(string text, PythonMetadata metadata)
    {
        string section = string.Empty;
        bool dependencyArray = false;
        foreach (string rawLine in Lines(text))
        {
            string line = StripComment(rawLine).Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line.Trim('[', ']').Trim().ToLowerInvariant();
                dependencyArray = false;
                continue;
            }

            if (line.Length == 0) continue;
            int equals = line.IndexOf('=');
            string key = equals < 0 ? string.Empty : Unquote(line[..equals].Trim());
            string value = equals < 0 ? line : line[(equals + 1)..].Trim();
            if (metadata.Name is null && key == "name" &&
                section is "project" or "tool.poetry")
            {
                metadata.Name = FirstQuoted(value);
            }

            if (section is "project.scripts" or "project.gui-scripts" or
                "tool.poetry.scripts" or "tool.pdm.scripts" or "scripts")
            {
                if (key.Length > 0) metadata.Scripts.Add(key);
                continue;
            }

            if (key == "dependencies" && section is "project" or "tool.pdm")
            {
                dependencyArray = true;
                foreach (string item in QuotedValues(value)) AddRequirement(item, metadata);
                if (value.Contains(']')) dependencyArray = false;
                continue;
            }

            if (dependencyArray)
            {
                foreach (string item in QuotedValues(line)) AddRequirement(item, metadata);
                if (line.Contains(']')) dependencyArray = false;
                continue;
            }

            if (section is "tool.poetry.dependencies" or "tool.poetry.group.dev.dependencies" or
                "tool.pdm.dev-dependencies" or "packages" or "dev-packages")
            {
                if (key.Length > 0 && key != "python") AddRequirement(key, metadata);
            }
        }
    }

    private static void ParseSetupCfg(string text, PythonMetadata metadata)
    {
        string section = string.Empty;
        bool requirements = false;
        foreach (string rawLine in Lines(text))
        {
            string uncommented = StripComment(rawLine);
            string line = uncommented.Trim();
            bool indented = uncommented.Length > 0 && char.IsWhiteSpace(uncommented[0]);
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line.Trim('[', ']').Trim().ToLowerInvariant();
                requirements = false;
                continue;
            }

            if (requirements && line.Length > 0)
            {
                if (indented)
                {
                    AddRequirement(line, metadata);
                    continue;
                }

                requirements = false;
            }

            if (section == "metadata" && line.StartsWith("name", StringComparison.OrdinalIgnoreCase))
            {
                metadata.Name ??= ValueAfterEquals(line);
            }

            if (section == "options" && line.StartsWith("install_requires", StringComparison.OrdinalIgnoreCase))
            {
                string inline = ValueAfterEquals(line);
                if (inline.Length > 0) AddRequirement(inline, metadata);
                requirements = inline.Length == 0;
                continue;
            }

            if (section.StartsWith("options.entry_points", StringComparison.Ordinal) && line.Contains('='))
            {
                metadata.Scripts.Add(line[..line.IndexOf('=')].Trim());
            }
        }
    }

    private static void ParseLiteralSetupPy(string text, PythonMetadata metadata)
    {
        bool installRequirements = false;
        foreach (string rawLine in Lines(text))
        {
            string line = StripComment(rawLine).Trim();
            if (metadata.Name is null && TryNamedLiteral(line, "name", out string? packageName))
            {
                metadata.Name = packageName;
            }

            if (line.Contains("console_scripts", StringComparison.Ordinal) && line.Contains('='))
            {
                string? script = FirstQuoted(line);
                if (script is not null)
                {
                    int equals = script.IndexOf('=');
                    metadata.Scripts.Add((equals < 0 ? script : script[..equals]).Trim());
                }
            }

            if (line.Contains("install_requires", StringComparison.Ordinal))
            {
                int start = line.IndexOf("install_requires", StringComparison.Ordinal) +
                    "install_requires".Length;
                string value = line[start..];
                foreach (string item in QuotedValues(value))
                {
                    AddRequirement(item, metadata);
                }

                installRequirements =
                    value.Contains('[') && !value.Contains(']') ||
                    value.Contains('(') && !value.Contains(')');
                continue;
            }

            if (installRequirements)
            {
                foreach (string item in QuotedValues(line)) AddRequirement(item, metadata);
                installRequirements = !line.Contains(']') && !line.Contains(')');
            }
        }
    }

    private static bool TryNamedLiteral(string line, string key, out string? value)
    {
        value = null;
        int searchFrom = 0;
        while (searchFrom < line.Length)
        {
            int keyIndex = line.IndexOf(key, searchFrom, StringComparison.Ordinal);
            if (keyIndex < 0) return false;
            int afterKey = keyIndex + key.Length;
            bool validStart = keyIndex == 0 ||
                !(line[keyIndex - 1] == '_' || char.IsLetterOrDigit(line[keyIndex - 1]));
            while (afterKey < line.Length && char.IsWhiteSpace(line[afterKey])) afterKey++;
            if (validStart && afterKey < line.Length && line[afterKey] == '=')
            {
                value = FirstQuoted(line[(afterKey + 1)..]);
                return value is not null;
            }

            searchFrom = keyIndex + key.Length;
        }

        return false;
    }

    private static void AddRequirement(string value, PythonMetadata metadata)
    {
        string candidate = Unquote(value.Trim().Trim(',', '[', ']'));
        if (candidate.Length == 0 || candidate.StartsWith('-') || candidate.StartsWith('.') ||
            candidate.Contains("git+", StringComparison.OrdinalIgnoreCase)) return;
        int marker = candidate.IndexOf(';');
        if (marker >= 0) candidate = candidate[..marker];
        int extra = candidate.IndexOf('[');
        if (extra >= 0) candidate = candidate[..extra];
        int specifier = candidate.IndexOfAny(['<', '>', '=', '!', '~', ' ', '\t']);
        if (specifier >= 0) candidate = candidate[..specifier];
        candidate = NormalizeName(candidate);
        if (candidate.Length > 0) metadata.Dependencies.Add(candidate);
    }

    private static string Role(PythonMetadata metadata)
    {
        if (metadata.Dependencies.Any(item => item is "fastapi" or "flask" or "django")) return "server";
        if (metadata.Dependencies.Any(item => item is "celery" or "rq")) return "worker";
        if (metadata.Scripts.Count > 0 ||
            metadata.Dependencies.Any(item => item is "click" or "typer")) return "cli";
        return "library";
    }

    private static bool IsMetadataFile(EvidenceFact fact)
    {
        if (fact.Kind != EvidenceKinds.File || fact.Tags.Any(tag => tag is
            "classification:generated" or "classification:vendored" or "content:binary")) return false;
        string name = Path.GetFileName(fact.Scope);
        return MetadataNames.Contains(name) ||
            name.StartsWith("requirements", StringComparison.OrdinalIgnoreCase) &&
            (name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ||
             name.EndsWith(".in", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPrimaryManifest(string name) => name.ToLowerInvariant() is
        "pyproject.toml" or "setup.cfg" or "setup.py" or "pipfile";

    private static int MetadataPriority(string name) => name.ToLowerInvariant() switch
    {
        "pyproject.toml" => 0,
        "setup.cfg" => 1,
        "setup.py" => 2,
        "pipfile" => 3,
        _ => 4,
    };

    private static string Directory(string path)
    {
        string normalized = path.Replace('\\', '/');
        int separator = normalized.LastIndexOf('/');
        return separator < 0 ? "." : normalized[..separator];
    }

    private static bool IsWithin(string path, string directory) =>
        directory == "." || path.Equals(directory, StringComparison.Ordinal) ||
        path.StartsWith(directory + "/", StringComparison.Ordinal);

    private static string[] Lines(string text) =>
        text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    private static string StripComment(string value)
    {
        bool quoted = false;
        char quote = '\0';
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (current is '\'' or '"')
            {
                if (!quoted) { quoted = true; quote = current; }
                else if (quote == current && (index == 0 || value[index - 1] != '\\')) quoted = false;
            }
            else if (current == '#' && !quoted)
            {
                return value[..index];
            }
        }

        return value;
    }

    private static IEnumerable<string> QuotedValues(string value)
    {
        for (int index = 0; index < value.Length; index++)
        {
            if (value[index] is not ('\'' or '"')) continue;
            char quote = value[index++];
            int start = index;
            while (index < value.Length && value[index] != quote) index++;
            if (index < value.Length) yield return value[start..index];
            else yield break;
        }
    }

    private static string? FirstQuoted(string value) => QuotedValues(value).FirstOrDefault();

    private static string ValueAfterEquals(string line)
    {
        int equals = line.IndexOf('=');
        return equals < 0 ? string.Empty : Unquote(line[(equals + 1)..].Trim());
    }

    private static string Unquote(string value) => value.Trim().Trim('\'', '"');

    private static string NormalizeName(string value) =>
        value.Trim().ToLowerInvariant().Replace('_', '-').Replace('.', '-');
}
