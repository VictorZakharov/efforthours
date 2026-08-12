namespace EffortHours.Analyzers.Docker;

internal static class ComposeAnalyzer
{
    private static readonly HashSet<string> KnownTopLevelKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "services", "networks", "volumes", "secrets", "configs", "name", "version", "include",
    };

    private static readonly HashSet<string> SecurityProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "cap_add", "cap_drop", "credential_spec", "group_add", "ipc", "isolation", "pid",
        "privileged", "read_only", "security_opt", "sysctls", "user", "userns_mode",
    };

    public static ComposeAnalysis Analyze(string source)
    {
        ComposeYamlScan scan = ComposeYamlScanner.Scan(source);
        MutableMetrics metrics = new(scan);
        Dictionary<string, ServiceBuild> builds = new(StringComparer.Ordinal);
        HashSet<string> services = new(StringComparer.Ordinal);
        HashSet<string> buildServices = new(StringComparer.Ordinal);
        HashSet<string> imageServices = new(StringComparer.Ordinal);
        HashSet<string> healthServices = new(StringComparer.Ordinal);
        HashSet<string> restartServices = new(StringComparer.Ordinal);

        foreach (ComposeYamlEntry entry in scan.Entries)
        {
            if (entry.Key is not null && entry.Key.StartsWith("x-", StringComparison.OrdinalIgnoreCase))
                metrics.Extensions++;

            if (entry.Parents.Count == 0)
            {
                AnalyzeRoot(entry, metrics);
                continue;
            }

            string root = entry.Parents[0];
            if (root.Equals("services", StringComparison.OrdinalIgnoreCase))
            {
                AnalyzeService(
                    entry,
                    metrics,
                    builds,
                    services,
                    buildServices,
                    imageServices,
                    healthServices,
                    restartServices);
            }
            else
            {
                AnalyzeTopLevelCollection(entry, root, metrics);
            }
        }

        metrics.Services = services.Count;
        metrics.BuildDefinitions = buildServices.Count;
        metrics.ImageReferences = imageServices.Count;
        metrics.HealthChecks = healthServices.Count;
        metrics.RestartPolicies = restartServices.Count;
        metrics.BuildReferences = [.. builds
            .Where(item => item.Value.HasBuild)
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => new ComposeBuildReference(
                item.Key,
                item.Value.Context,
                item.Value.Dockerfile,
                item.Value.Dynamic))];
        return metrics.ToAnalysis();
    }

    private static void AnalyzeRoot(ComposeYamlEntry entry, MutableMetrics metrics)
    {
        if (entry.Key is null) return;
        if (entry.Key.Equals("include", StringComparison.OrdinalIgnoreCase))
            metrics.Includes += Math.Max(1, InlineValueCount(entry.Value));
        else if (entry.Key.Equals("services", StringComparison.OrdinalIgnoreCase) &&
            entry.Value.Length > 0)
            metrics.DynamicValues++;
        else if (!KnownTopLevelKeys.Contains(entry.Key) &&
            !entry.Key.StartsWith("x-", StringComparison.OrdinalIgnoreCase))
            metrics.UnknownTopLevelKeys++;
    }

    private static void AnalyzeTopLevelCollection(
        ComposeYamlEntry entry,
        string root,
        MutableMetrics metrics)
    {
        int count = CollectionEntryCount(entry, root);
        if (root.Equals("networks", StringComparison.OrdinalIgnoreCase)) metrics.Networks += count;
        else if (root.Equals("secrets", StringComparison.OrdinalIgnoreCase)) metrics.Secrets += count;
        else if (root.Equals("configs", StringComparison.OrdinalIgnoreCase)) metrics.Configs += count;
        else if (root.Equals("include", StringComparison.OrdinalIgnoreCase)) metrics.Includes += Math.Max(1, count);
    }

    private static void AnalyzeService(
        ComposeYamlEntry entry,
        MutableMetrics metrics,
        Dictionary<string, ServiceBuild> builds,
        HashSet<string> services,
        HashSet<string> buildServices,
        HashSet<string> imageServices,
        HashSet<string> healthServices,
        HashSet<string> restartServices)
    {
        if (entry.Parents.Count == 1 && entry.Key is not null)
        {
            services.Add(entry.Key);
            if (entry.Value.Length > 0) metrics.DynamicValues++;
            return;
        }

        if (entry.Parents.Count < 2) return;
        string service = entry.Parents[1];
        services.Add(service);
        string? property = entry.Parents.Count == 2 ? entry.Key : entry.Parents[2];
        if (property is null) return;
        int values = ServiceValueCount(entry, property);

        if (property.Equals("build", StringComparison.OrdinalIgnoreCase))
        {
            buildServices.Add(service);
            ServiceBuild build = GetBuild(builds, service);
            build.HasBuild = true;
            if (entry.Parents.Count == 2 && entry.Key?.Equals("build", StringComparison.OrdinalIgnoreCase) == true &&
                entry.Value.Length > 0)
                SetLiteral(entry.Value, value => build.Context = value, build);
            else if (entry.Parents.Count >= 3 && entry.Key is not null)
            {
                if (entry.Key.Equals("context", StringComparison.OrdinalIgnoreCase))
                    SetLiteral(entry.Value, value => build.Context = value, build);
                else if (entry.Key.Equals("dockerfile", StringComparison.OrdinalIgnoreCase))
                    SetLiteral(entry.Value, value => build.Dockerfile = value, build);
                else if (entry.Key.Equals("dockerfile_inline", StringComparison.OrdinalIgnoreCase))
                    build.Dynamic = true;
            }
        }
        else if (property.Equals("image", StringComparison.OrdinalIgnoreCase)) imageServices.Add(service);
        else if (property.Equals("command", StringComparison.OrdinalIgnoreCase) ||
            property.Equals("entrypoint", StringComparison.OrdinalIgnoreCase))
            metrics.Commands += Math.Max(1, values);
        else if (property.Equals("ports", StringComparison.OrdinalIgnoreCase) ||
            property.Equals("expose", StringComparison.OrdinalIgnoreCase))
            metrics.Ports += values;
        else if (property.Equals("environment", StringComparison.OrdinalIgnoreCase)) metrics.EnvironmentEntries += values;
        else if (property.Equals("env_file", StringComparison.OrdinalIgnoreCase)) metrics.EnvironmentFiles += values;
        else if (property.Equals("volumes", StringComparison.OrdinalIgnoreCase)) metrics.VolumeMounts += values;
        else if (property.Equals("networks", StringComparison.OrdinalIgnoreCase)) metrics.Networks += values;
        else if (property.Equals("depends_on", StringComparison.OrdinalIgnoreCase) ||
            property.Equals("links", StringComparison.OrdinalIgnoreCase))
            metrics.Dependencies += values;
        else if (property.Equals("healthcheck", StringComparison.OrdinalIgnoreCase)) healthServices.Add(service);
        else if (property.Equals("profiles", StringComparison.OrdinalIgnoreCase)) metrics.Profiles += values;
        else if (property.Equals("secrets", StringComparison.OrdinalIgnoreCase)) metrics.Secrets += values;
        else if (property.Equals("configs", StringComparison.OrdinalIgnoreCase)) metrics.Configs += values;
        else if (property.Equals("deploy", StringComparison.OrdinalIgnoreCase))
            metrics.DeploySettings += Math.Max(values, entry.Parents.Count > 2 && entry.Key is not null ? 1 : 0);
        else if (property.Equals("restart", StringComparison.OrdinalIgnoreCase)) restartServices.Add(service);
        else if (SecurityProperties.Contains(property)) metrics.SecuritySettings += Math.Max(1, values);
        else if (property.Equals("extends", StringComparison.OrdinalIgnoreCase) ||
            property.Equals("provider", StringComparison.OrdinalIgnoreCase))
            metrics.DynamicValues++;
    }

    private static int ServiceValueCount(ComposeYamlEntry entry, string property)
    {
        if (entry.Parents.Count == 2 && entry.Key?.Equals(property, StringComparison.OrdinalIgnoreCase) == true)
            return InlineValueCount(entry.Value);
        if (entry.Parents.Count >= 3 && entry.Parents[2].Equals(property, StringComparison.OrdinalIgnoreCase))
            return entry.IsSequence || entry.Key is not null ? Math.Max(1, InlineValueCount(entry.Value)) : 0;
        return 0;
    }

    private static int CollectionEntryCount(ComposeYamlEntry entry, string root)
    {
        if (!entry.Parents[0].Equals(root, StringComparison.OrdinalIgnoreCase)) return 0;
        return entry.Parents.Count == 1 && entry.Key is not null || entry.IsSequence ? 1 : 0;
    }

    private static int InlineValueCount(string value)
    {
        string trimmed = value.Trim();
        if (trimmed.Length == 0) return 0;
        if ((trimmed.StartsWith('[') && trimmed.EndsWith(']')) ||
            (trimmed.StartsWith('{') && trimmed.EndsWith('}')))
            return trimmed.Length <= 2 ? 0 : CountTopLevelCommas(trimmed[1..^1]) + 1;
        return 1;
    }

    private static int CountTopLevelCommas(string value)
    {
        int count = 0;
        int depth = 0;
        char quote = '\0';
        bool escaped = false;
        foreach (char current in value)
        {
            if (quote == '"' && escaped) { escaped = false; continue; }
            if (quote == '"' && current == '\\') { escaped = true; continue; }
            if (quote != '\0')
            {
                if (current == quote) quote = '\0';
                continue;
            }

            if (current is '\'' or '"') quote = current;
            else if (current is '[' or '{') depth++;
            else if (current is ']' or '}') depth--;
            else if (current == ',' && depth == 0) count++;
        }

        return count;
    }

    private static ServiceBuild GetBuild(Dictionary<string, ServiceBuild> builds, string service)
    {
        if (!builds.TryGetValue(service, out ServiceBuild? build))
        {
            build = new ServiceBuild();
            builds.Add(service, build);
        }

        return build;
    }

    private static void SetLiteral(string raw, Action<string> assign, ServiceBuild build)
    {
        string value = raw.Trim();
        if (value.Length >= 2 && value[0] == value[^1] && value[0] is '\'' or '"')
            value = value[1..^1];
        if (value.Length == 0 || value.Contains("${", StringComparison.Ordinal) ||
            value[0] is '!' or '&' or '*' || value.StartsWith('[') || value.StartsWith('{'))
        {
            build.Dynamic = true;
            return;
        }

        assign(value);
    }

    private sealed class ServiceBuild
    {
        public bool HasBuild;
        public string? Context;
        public string? Dockerfile;
        public bool Dynamic;
    }

    private sealed class MutableMetrics(ComposeYamlScan scan)
    {
        public int Services;
        public int BuildDefinitions;
        public int ImageReferences;
        public int Commands;
        public int Ports;
        public int EnvironmentEntries;
        public int EnvironmentFiles;
        public int VolumeMounts;
        public int Networks;
        public int Dependencies;
        public int HealthChecks;
        public int Profiles;
        public int Secrets;
        public int Configs;
        public int DeploySettings;
        public int SecuritySettings;
        public int RestartPolicies;
        public int Extensions;
        public int Includes;
        public int DynamicValues = scan.DynamicValues;
        public int UnknownTopLevelKeys;
        public IReadOnlyList<ComposeBuildReference> BuildReferences = [];

        public ComposeAnalysis ToAnalysis()
        {
            int boundaryValues = Ports + EnvironmentEntries + EnvironmentFiles + VolumeMounts + Networks + Dependencies;
            decimal units = scan.Entries.Count == 0 ? 0.25m : 1m;
            units += 0.5m * Math.Min(24, Services);
            units += 0.25m * decimal.Ceiling(boundaryValues / 6m);
            if (BuildDefinitions > 0) units += 0.25m;
            if (HealthChecks > 0) units += 0.25m;
            if (Secrets + Configs + SecuritySettings > 0) units += 0.25m;
            if (DeploySettings + Profiles + RestartPolicies > 0) units += 0.25m;
            if (scan.AnchorsAliasesAndMerges + scan.Interpolations + scan.BlockScalars +
                DynamicValues + UnknownTopLevelKeys + scan.Safeguards > 0) units += 0.25m;
            string confidence = scan.Safeguards > 0 || scan.Documents > 1 ? "low" :
                scan.AnchorsAliasesAndMerges + scan.Interpolations + scan.BlockScalars +
                    DynamicValues + UnknownTopLevelKeys > 0 ? "medium" : "high";
            return new ComposeAnalysis
            {
                Documents = scan.Documents,
                Services = Services,
                BuildDefinitions = BuildDefinitions,
                ImageReferences = ImageReferences,
                Commands = Commands,
                Ports = Ports,
                EnvironmentEntries = EnvironmentEntries,
                EnvironmentFiles = EnvironmentFiles,
                VolumeMounts = VolumeMounts,
                Networks = Networks,
                Dependencies = Dependencies,
                HealthChecks = HealthChecks,
                Profiles = Profiles,
                Secrets = Secrets,
                Configs = Configs,
                DeploySettings = DeploySettings,
                SecuritySettings = SecuritySettings,
                RestartPolicies = RestartPolicies,
                Extensions = Extensions,
                Includes = Includes,
                AnchorsAliasesAndMerges = scan.AnchorsAliasesAndMerges,
                Interpolations = scan.Interpolations,
                BlockScalars = scan.BlockScalars,
                DynamicValues = DynamicValues,
                UnknownTopLevelKeys = UnknownTopLevelKeys,
                Safeguards = scan.Safeguards,
                Confidence = confidence,
                SemanticUnits = decimal.Min(24m, units),
                BuildReferences = BuildReferences,
            };
        }
    }
}
