namespace EffortHours.Analyzers.Rust;

internal static class CargoTomlParser
{
    public static CargoManifestMetadata Parse(string text, string path)
    {
        CargoManifestMetadata metadata = new(path, RustPath.Directory(path));
        string section = string.Empty;
        CargoTargetBuilder? target = null;
        foreach (string statement in Statements(text, metadata))
        {
            if (statement.StartsWith("[[", StringComparison.Ordinal) &&
                statement.EndsWith("]]", StringComparison.Ordinal))
            {
                section = statement[2..^2].Trim().ToLowerInvariant();
                target = section is "bin" or "example" or "test" or "bench"
                    ? new CargoTargetBuilder(section)
                    : null;
                if (target is not null) metadata.Targets.Add(target);
                continue;
            }

            if (statement.StartsWith('[') && statement.EndsWith(']'))
            {
                section = statement[1..^1].Trim().ToLowerInvariant();
                target = null;
                metadata.HasPackage |= section == "package";
                metadata.HasWorkspace |= section == "workspace";
                metadata.HasLib |= section == "lib";
                continue;
            }

            int equals = FindEquals(statement);
            if (equals <= 0)
            {
                metadata.Malformed = true;
                continue;
            }

            string key = statement[..equals].Trim().Trim('\'', '"');
            ReadValue(metadata, section, target, key, statement[(equals + 1)..].Trim());
        }
        return metadata;
    }

    private static void ReadValue(
        CargoManifestMetadata metadata,
        string section,
        CargoTargetBuilder? target,
        string key,
        string value)
    {
        if (section == "package")
        {
            if (key == "name") metadata.Name = StringValue(value);
            else if (key == "build")
            {
                metadata.BuildDisabled = value.Equals("false", StringComparison.OrdinalIgnoreCase);
                metadata.ExplicitBuild = StringValue(value);
                if (!metadata.BuildDisabled && (metadata.ExplicitBuild is not null ||
                    value.Equals("true", StringComparison.OrdinalIgnoreCase))) metadata.BuildScripts = 1;
            }
            else if (key == "default-run") metadata.DefaultRun = StringValue(value);
            else if (key is "autobins" or "autoexamples" or "autotests" or "autobenches")
            {
                bool? enabled = BooleanValue(value);
                if (enabled is null) metadata.UnresolvedValues++;
                else if (key == "autobins") metadata.AutoBins = enabled.Value;
                else if (key == "autoexamples") metadata.AutoExamples = enabled.Value;
                else if (key == "autotests") metadata.AutoTests = enabled.Value;
                else metadata.AutoBenches = enabled.Value;
            }
            else if (key.EndsWith(".workspace", StringComparison.Ordinal) ||
                value.Equals("{ workspace = true }", StringComparison.OrdinalIgnoreCase))
                metadata.UnresolvedValues++;
            return;
        }

        if (section == "workspace")
        {
            if (key == "members") metadata.MemberPatterns.AddRange(StringArray(value));
            else if (key == "exclude") metadata.ExcludePatterns.AddRange(StringArray(value));
            else if (key == "default-members") metadata.DefaultMemberPatterns.AddRange(StringArray(value));
            else if (key == "resolver" && StringValue(value) is null) metadata.UnresolvedValues++;
            return;
        }

        if (section == "features")
        {
            metadata.Features++;
            if (!value.TrimStart().StartsWith('[')) metadata.UnresolvedValues++;
            return;
        }

        if (section == "lib")
        {
            if (key == "name") metadata.LibName = StringValue(value);
            else if (key == "path") metadata.LibPath = StringValue(value);
            else if (key == "proc-macro")
                metadata.IsProcMacro = value.Equals("true", StringComparison.OrdinalIgnoreCase);
            else if (key == "crate-type") metadata.CrateTypes = StringArray(value).Count;
            return;
        }

        if (target is not null)
        {
            if (key == "name") target.Name = StringValue(value);
            else if (key == "path") target.Path = StringValue(value);
            return;
        }

        if (TryDependencyTable(section, out string tableKind, out string dependencyName))
        {
            string? packageName = key == "package" ? StringValue(value) : null;
            string? path = key == "path" ? StringValue(value) : null;
            bool tableInherited = key == "workspace" &&
                value.Equals("true", StringComparison.OrdinalIgnoreCase);
            AddDependency(metadata, dependencyName, tableKind, packageName, path, tableInherited);
            if (tableInherited) metadata.UnresolvedValues++;
            if (section.StartsWith("target.", StringComparison.Ordinal)) metadata.UnresolvedValues++;
            return;
        }

        string? dependencyKind = DependencyKind(section);
        if (dependencyKind is null) return;
        if (TryDependencyProperty(key, out string dottedDependencyName, out string property))
        {
            bool inheritedProperty = property == "workspace" && BooleanValue(value) == true;
            AddDependency(metadata,
                dottedDependencyName,
                dependencyKind,
                property == "package" ? StringValue(value) : null,
                property == "path" ? StringValue(value) : null,
                inheritedProperty);
            if (property == "workspace" && !inheritedProperty ||
                property is "package" or "path" && StringValue(value) is null)
                metadata.UnresolvedValues++;
            if (inheritedProperty) metadata.UnresolvedValues++;
            if (section.StartsWith("target.", StringComparison.Ordinal)) metadata.UnresolvedValues++;
            return;
        }
        bool inherited = InlineBoolean(value, "workspace") == true;
        AddDependency(metadata,
            key,
            dependencyKind,
            InlineString(value, "package"),
            InlineString(value, "path"),
            inherited);
        if (inherited) metadata.UnresolvedValues++;
        if (section.StartsWith("target.", StringComparison.Ordinal)) metadata.UnresolvedValues++;
    }

    private static IEnumerable<string> Statements(string text, CargoManifestMetadata metadata)
    {
        string pending = string.Empty;
        foreach (string raw in text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n').Split('\n'))
        {
            string line = StripComment(raw).Trim();
            if (line.Length == 0) continue;
            pending = pending.Length == 0 ? line : pending + " " + line;
            if (BalancedValue(pending))
            {
                yield return pending;
                pending = string.Empty;
            }
        }
        if (pending.Length > 0) metadata.Malformed = true;
    }

    private static string StripComment(string line)
    {
        bool single = false;
        bool doubleQuote = false;
        bool escaped = false;
        for (int index = 0; index < line.Length; index++)
        {
            char current = line[index];
            if (doubleQuote && escaped) { escaped = false; continue; }
            if (doubleQuote && current == '\\') { escaped = true; continue; }
            if (!doubleQuote && current == '\'') single = !single;
            else if (!single && current == '"') doubleQuote = !doubleQuote;
            else if (!single && !doubleQuote && current == '#') return line[..index];
        }
        return line;
    }

    private static bool BalancedValue(string value)
    {
        int square = 0;
        int brace = 0;
        bool single = false;
        bool doubleQuote = false;
        bool escaped = false;
        foreach (char current in value)
        {
            if (doubleQuote && escaped) { escaped = false; continue; }
            if (doubleQuote && current == '\\') { escaped = true; continue; }
            if (!doubleQuote && current == '\'') single = !single;
            else if (!single && current == '"') doubleQuote = !doubleQuote;
            else if (!single && !doubleQuote && current == '[') square++;
            else if (!single && !doubleQuote && current == ']') square--;
            else if (!single && !doubleQuote && current == '{') brace++;
            else if (!single && !doubleQuote && current == '}') brace--;
        }
        return !single && !doubleQuote && square == 0 && brace == 0;
    }

    private static int FindEquals(string value)
    {
        bool single = false;
        bool doubleQuote = false;
        for (int index = 0; index < value.Length; index++)
        {
            if (!doubleQuote && value[index] == '\'') single = !single;
            else if (!single && value[index] == '"') doubleQuote = !doubleQuote;
            else if (!single && !doubleQuote && value[index] == '=') return index;
        }
        return -1;
    }

    private static string? DependencyKind(string section)
    {
        if (section is "dependencies" or "workspace.dependencies" ||
            section.EndsWith(".dependencies", StringComparison.Ordinal)) return "runtime";
        if (section is "dev-dependencies" or "workspace.dev-dependencies" ||
            section.EndsWith(".dev-dependencies", StringComparison.Ordinal)) return "development";
        if (section is "build-dependencies" or "workspace.build-dependencies" ||
            section.EndsWith(".build-dependencies", StringComparison.Ordinal)) return "build";
        return null;
    }

    private static bool TryDependencyProperty(string key, out string name, out string property)
    {
        int separator = key.LastIndexOf('.');
        if (separator <= 0 || separator == key.Length - 1)
        {
            name = string.Empty;
            property = string.Empty;
            return false;
        }

        property = key[(separator + 1)..].ToLowerInvariant();
        if (property is not ("version" or "path" or "git" or "branch" or "tag" or "rev" or
            "package" or "registry" or "optional" or "default-features" or "features" or "workspace"))
        {
            name = string.Empty;
            property = string.Empty;
            return false;
        }
        name = key[..separator];
        return true;
    }

    private static bool TryDependencyTable(
        string section,
        out string kind,
        out string name)
    {
        foreach ((string Marker, string Kind) candidate in new[]
        {
            (".dev-dependencies.", "development"),
            (".build-dependencies.", "build"),
            (".dependencies.", "runtime"),
        })
        {
            int marker = ("." + section).LastIndexOf(candidate.Marker, StringComparison.Ordinal);
            if (marker < 0) continue;
            string prefix = ("." + section)[..marker];
            if (prefix.Length > 0 && prefix != ".workspace" &&
                !prefix.StartsWith(".target.", StringComparison.Ordinal)) continue;
            name = section[(marker + candidate.Marker.Length - 1)..].Trim().Trim('\'', '"');
            kind = candidate.Kind;
            return name.Length > 0;
        }

        kind = string.Empty;
        name = string.Empty;
        return false;
    }

    private static void AddDependency(
        CargoManifestMetadata metadata,
        string name,
        string kind,
        string? packageName,
        string? path,
        bool inherited)
    {
        int index = metadata.Dependencies.FindIndex(dependency =>
            dependency.Name.Equals(name, StringComparison.Ordinal) && dependency.Kind == kind);
        if (index < 0)
        {
            metadata.Dependencies.Add(new CargoDependencyBuilder(
                name, kind, packageName, path, inherited));
            return;
        }

        CargoDependencyBuilder existing = metadata.Dependencies[index];
        metadata.Dependencies[index] = existing with
        {
            PackageName = packageName ?? existing.PackageName,
            Path = path ?? existing.Path,
            WorkspaceInherited = inherited || existing.WorkspaceInherited,
        };
    }

    private static string? StringValue(string value)
    {
        string trimmed = value.Trim();
        if (trimmed.Length < 2) return null;
        char quote = trimmed[0];
        return quote is '\'' or '"' && trimmed[^1] == quote ? trimmed[1..^1] : null;
    }

    private static List<string> StringArray(string value)
    {
        List<string> values = [];
        string text = value.Trim();
        if (!text.StartsWith('[') || !text.EndsWith(']')) return values;
        for (int index = 1; index < text.Length - 1; index++)
        {
            if (text[index] is not ('\'' or '"')) continue;
            char quote = text[index++];
            int start = index;
            while (index < text.Length - 1 && text[index] != quote)
            {
                if (quote == '"' && text[index] == '\\' && index + 1 < text.Length - 1) index++;
                index++;
            }
            if (index < text.Length) values.Add(text[start..index]);
        }
        return values;
    }

    private static string? InlineString(string value, string key)
    {
        int index = value.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            int equals = value.IndexOf('=', index + key.Length);
            if (equals >= 0 && value[(index + key.Length)..equals].All(char.IsWhiteSpace))
            {
                string tail = value[(equals + 1)..].TrimStart();
                if (tail.Length > 1 && tail[0] is '\'' or '"')
                {
                    int end = tail.IndexOf(tail[0], 1);
                    if (end > 0) return tail[1..end];
                }
            }
            index = value.IndexOf(key, index + key.Length, StringComparison.OrdinalIgnoreCase);
        }
        return null;
    }

    private static bool? InlineBoolean(string value, string key)
    {
        int index = value.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return null;
        int equals = value.IndexOf('=', index + key.Length);
        if (equals < 0) return null;
        string tail = value[(equals + 1)..].TrimStart();
        if (tail.StartsWith("true", StringComparison.OrdinalIgnoreCase)) return true;
        if (tail.StartsWith("false", StringComparison.OrdinalIgnoreCase)) return false;
        return null;
    }

    private static bool? BooleanValue(string value)
    {
        string trimmed = value.Trim();
        if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
        if (trimmed.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
        return null;
    }
}
