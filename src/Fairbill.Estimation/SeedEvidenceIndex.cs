using Fairbill.Contracts.V1;

namespace Fairbill.Estimation;

internal sealed class SeedEvidenceIndex
{
    private static readonly HashSet<string> SourceSemanticKinds =
    [
        EvidenceKinds.ApiSurface,
        EvidenceKinds.BackgroundWork,
        EvidenceKinds.DataAccess,
        EvidenceKinds.DotNetTest,
        EvidenceKinds.EntryPoint,
        EvidenceKinds.Integration,
        EvidenceKinds.JavaScriptTest,
        EvidenceKinds.SecurityConfiguration,
        EvidenceKinds.UserInterface,
        EvidenceKinds.Validation,
    ];

    private static readonly HashSet<string> JavaScriptSourceExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".cjs", ".cts", ".js", ".jsx", ".mjs", ".mts", ".svelte", ".ts", ".tsx", ".vue",
        };

    private readonly Dictionary<string, SeedFileEvidence> _filesByPath;
    private readonly Dictionary<string, string> _canonicalPathByDuplicateKey;
    private readonly Dictionary<string, EvidenceFact> _factsById;
    private readonly Dictionary<string, IReadOnlyList<NormalizedEvidenceFact>> _normalizedByKind;

    public SeedEvidenceIndex(RepositoryEvidence evidence)
    {
        Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        Facts = [.. evidence.Facts.OrderBy(fact => fact.Id, StringComparer.Ordinal)];
        _factsById = Facts.ToDictionary(fact => fact.Id, StringComparer.Ordinal);
        Files = [.. Facts
            .Where(fact => fact.Kind == EvidenceKinds.File)
            .Select(CreateFileEvidence)
            .OrderBy(file => file.Path, StringComparer.Ordinal)];
        _filesByPath = Files.ToDictionary(file => file.Path, StringComparer.Ordinal);
        _canonicalPathByDuplicateKey = Files
            .Where(file => file.DuplicateKey is not null)
            .GroupBy(file => file.DuplicateKey!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(file => file.Path).Min(StringComparer.Ordinal)!,
                StringComparer.Ordinal);
        Scopes = CreateScopes();
        _normalizedByKind = SourceSemanticKinds.ToDictionary(
            kind => kind,
            CreateNormalizedFacts,
            StringComparer.Ordinal);
    }

    public RepositoryEvidence Evidence { get; }

    public IReadOnlyList<EvidenceFact> Facts { get; }

    public IReadOnlyList<SeedFileEvidence> Files { get; }

    public IReadOnlyList<SeedEstimationScope> Scopes { get; }

    public IReadOnlyList<EvidenceFact> FactsOfKind(string kind) =>
        [.. Facts.Where(fact => fact.Kind == kind)];

    public IReadOnlyList<NormalizedEvidenceFact> NormalizedFactsOfKind(string kind) =>
        _normalizedByKind.TryGetValue(kind, out IReadOnlyList<NormalizedEvidenceFact>? facts)
            ? facts
            : [.. FactsOfKind(kind).Select(fact => NormalizedEvidenceFact.Single(fact))];

    public EvidenceFact Fact(string id) =>
        _factsById.TryGetValue(id, out EvidenceFact? fact)
            ? fact
            : throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown evidence fact ID.");

    public SeedEstimationScope? FindScope(EvidenceFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        string ecosystem = EcosystemFor(fact);
        SeedEstimationScope? exact = Scopes.FirstOrDefault(scope =>
            scope.Ecosystem == ecosystem && scope.Scope == fact.Scope);
        if (exact is not null)
        {
            return exact;
        }

        string? path = fact.Locations.Count == 0 ? null : fact.Locations[0].Path;
        return path is null ? null : FindScopeForPath(path, ecosystem);
    }

    public SeedEstimationScope? FindScopeForPath(string path, string ecosystem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Scopes
            .Where(scope => scope.Ecosystem == ecosystem && IsWithin(path, scope.Directory))
            .OrderByDescending(scope => scope.Directory.Length)
            .ThenBy(scope => scope.Id, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public StructureNormalization GetStructureNormalization(SeedEstimationScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        SeedFileEvidence[] analyzed = [.. Files
            .Where(file => IsOwnedBy(file, scope))
            .Where(file => IsAnalyzedSource(file, scope.Ecosystem))];
        if (analyzed.Length == 0)
        {
            return new StructureNormalization(
                scope.IsTest ? 0m : 1m,
                0,
                0,
                0,
                HasTests: scope.IsTest,
                HasDuplicates: false);
        }

        SeedFileEvidence[] production = [.. analyzed.Where(file => !file.IsTest)];
        int canonicalProduction = production.Count(IsCanonical);
        bool hasDuplicates = canonicalProduction < production.Length;
        return new StructureNormalization(
            (decimal)canonicalProduction / analyzed.Length,
            analyzed.Length,
            production.Length,
            canonicalProduction,
            HasTests: production.Length < analyzed.Length,
            HasDuplicates: hasDuplicates);
    }

    public IReadOnlyList<EvidenceFact> ScopeEvidence(SeedEstimationScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        List<EvidenceFact> evidence = [scope.Fact];
        evidence.AddRange(Facts.Where(fact =>
            fact.Kind == EvidenceKinds.ProjectReference &&
            fact.Scope == scope.Scope));
        return [.. evidence
            .DistinctBy(fact => fact.Id, StringComparer.Ordinal)
            .OrderBy(fact => fact.Id, StringComparer.Ordinal)];
    }

    public IReadOnlyList<EvidenceFact> RepositoryAnchorEvidence()
    {
        EvidenceFact[] anchors = [.. Facts.Where(fact => fact.Kind is
            EvidenceKinds.RepositoryInventory or EvidenceKinds.DotNetProject or
            EvidenceKinds.JavaScriptWorkspace)];
        if (anchors.Length > 0)
        {
            return anchors;
        }

        return Facts.Count == 0 ? [] : [Facts[0]];
    }

    public bool IsCanonical(SeedFileEvidence file)
    {
        ArgumentNullException.ThrowIfNull(file);
        return file.DuplicateKey is null ||
            _canonicalPathByDuplicateKey[file.DuplicateKey] == file.Path;
    }

    public bool HasExactSourceDuplicates => Files
        .Where(file => file.IsMaintained && file.RoleFamily is "source" or "test")
        .Any(file => !IsCanonical(file));

    public static decimal Measurement(EvidenceFact fact, string name) =>
        fact.Measurements
            .Where(measurement => measurement.Name == name)
            .Sum(measurement => measurement.Value);

    public static decimal Measurement(NormalizedEvidenceFact fact, string name) =>
        fact.Measurements.GetValueOrDefault(name);

    public static string? TagValue(EvidenceFact fact, string prefix) =>
        fact.Tags.FirstOrDefault(tag => tag.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];

    public static string EcosystemFor(EvidenceFact fact)
    {
        if (fact.Id.StartsWith("dotnet:", StringComparison.Ordinal))
        {
            return "dotnet";
        }

        if (fact.Id.StartsWith("javascript:", StringComparison.Ordinal))
        {
            return "javascript";
        }

        return TagValue(fact, "ecosystem:") ?? "common";
    }

    private IReadOnlyList<SeedEstimationScope> CreateScopes()
    {
        List<SeedEstimationScope> scopes = [];
        foreach (EvidenceFact fact in Facts.Where(fact =>
            fact.Kind == EvidenceKinds.DotNetProject &&
            fact.Id.StartsWith("dotnet:project:", StringComparison.Ordinal)))
        {
            scopes.Add(CreateScope(
                fact,
                "dotnet",
                TagValue(fact, "project-role:") ?? "project",
                ProjectDirectory(fact.Scope)));
        }

        foreach (EvidenceFact fact in Facts.Where(fact =>
            fact.Kind == EvidenceKinds.JavaScriptPackage &&
            fact.Id.StartsWith("javascript:package:", StringComparison.Ordinal)))
        {
            scopes.Add(CreateScope(
                fact,
                "javascript",
                TagValue(fact, "package-role:") ?? "package",
                NormalizeDirectory(fact.Scope)));
        }

        foreach (EvidenceFact fact in Facts.Where(fact => fact.Kind == EvidenceKinds.SourceStructure))
        {
            string ecosystem = EcosystemFor(fact);
            if (scopes.Any(scope =>
                scope.Ecosystem == ecosystem && scope.Scope == fact.Scope))
            {
                continue;
            }

            string directory = ecosystem == "dotnet"
                ? ProjectDirectory(fact.Scope)
                : NormalizeDirectory(fact.Scope);
            scopes.Add(CreateScope(
                fact,
                ecosystem,
                "component",
                directory));
        }

        if (scopes.Count == 0)
        {
            foreach (EvidenceFact fact in Facts.Where(fact => fact.Kind == EvidenceKinds.Component))
            {
                string ecosystem = TagValue(fact, "ecosystem:") ?? "common";
                scopes.Add(CreateScope(
                    fact,
                    ecosystem,
                    "component",
                    ProjectDirectory(fact.Scope)));
            }
        }

        if (scopes.Count == 0)
        {
            IReadOnlyList<EvidenceFact> anchors = RepositoryAnchorEvidence();
            EvidenceFact? anchor = anchors.Count == 0 ? null : anchors[0];
            if (anchor is not null)
            {
                string ecosystem = Evidence.Repository.Ecosystems.Count == 0
                    ? "common"
                    : Evidence.Repository.Ecosystems[0];
                scopes.Add(CreateScope(anchor, ecosystem, "application", "."));
            }
        }

        return [.. scopes
            .DistinctBy(scope => scope.Id, StringComparer.Ordinal)
            .OrderBy(scope => scope.Id, StringComparer.Ordinal)];
    }

    private static SeedEstimationScope CreateScope(
        EvidenceFact fact,
        string ecosystem,
        string role,
        string directory)
    {
        bool isTest = role == "test";
        bool isRunnable = role is
            "application" or "cli" or "desktop" or "executable" or "full-stack-web" or
            "server" or "web" or "web-ui" or "worker";
        return new SeedEstimationScope
        {
            Id = $"{ecosystem}:{fact.Scope}",
            Scope = fact.Scope,
            Directory = directory,
            Ecosystem = ecosystem,
            Role = role,
            Fact = fact,
            IsTest = isTest,
            IsRunnable = isRunnable,
        };
    }

    private IReadOnlyList<NormalizedEvidenceFact> CreateNormalizedFacts(string kind)
    {
        List<(string Key, EvidenceFact Fact)> keyed = [];
        foreach (EvidenceFact fact in Facts.Where(fact => fact.Kind == kind))
        {
            string key = fact.Id;
            string? path = fact.Locations.Count == 0 ? null : fact.Locations[0].Path;
            if (path is not null &&
                _filesByPath.TryGetValue(path, out SeedFileEvidence? file) &&
                file.DuplicateKey is not null)
            {
                key = $"{kind}|{file.DuplicateKey}";
            }

            keyed.Add((key, fact));
        }

        return [.. keyed
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .Select(group => CreateNormalizedFact(group.Select(item => item.Fact)))
            .OrderBy(fact => fact.Key, StringComparer.Ordinal)];
    }

    private static NormalizedEvidenceFact CreateNormalizedFact(IEnumerable<EvidenceFact> candidates)
    {
        EvidenceFact[] facts = [.. candidates.OrderBy(
            FirstLocationOrId,
            StringComparer.Ordinal)];
        EvidenceFact canonical = facts[0];
        Dictionary<string, decimal> measurements = canonical.Measurements
            .GroupBy(measurement => measurement.Name, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(measurement => measurement.Value),
                StringComparer.Ordinal);
        return new NormalizedEvidenceFact
        {
            Key = canonical.Id,
            Kind = canonical.Kind,
            Scope = canonical.Scope,
            Summary = canonical.Summary,
            Facts = facts,
            Measurements = measurements,
            Tags = [.. facts
                .SelectMany(fact => fact.Tags)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)],
            HasExactDuplicates = facts.Length > 1,
        };
    }

    private static string FirstLocationOrId(EvidenceFact fact) =>
        fact.Locations.Count == 0 ? fact.Id : fact.Locations[0].Path;

    private SeedFileEvidence CreateFileEvidence(EvidenceFact fact)
    {
        string role = TagValue(fact, "role:") ?? "unknown";
        bool isTest = role == "test" || fact.Tags.Contains("classification:test", StringComparer.Ordinal);
        bool isExcluded = fact.Tags.Any(tag => tag is
            "classification:generated" or "classification:minified" or
            "classification:vendored" or "content:binary");
        string? sha256 = TagValue(fact, "sha256:");
        string? language = TagValue(fact, "language:");
        string roleFamily = isTest
            ? "test"
            : role == "source"
                ? "source"
                : role;
        string? duplicateKey = sha256 is null || isExcluded
            ? null
            : $"{roleFamily}|{language ?? "none"}|{sha256}";
        return new SeedFileEvidence
        {
            Path = fact.Scope,
            Role = role,
            RoleFamily = roleFamily,
            Language = language,
            Extension = TagValue(fact, "extension:") ?? Path.GetExtension(fact.Scope),
            Ecosystems = [.. fact.Tags
                .Where(tag => tag.StartsWith("ecosystem:", StringComparison.Ordinal))
                .Select(tag => tag[10..])
                .Order(StringComparer.Ordinal)],
            Sha256 = sha256,
            DuplicateKey = duplicateKey,
            IsTest = isTest,
            IsMaintained = !isExcluded,
            PhysicalLines = Measurement(fact, "physical-lines"),
            Fact = fact,
        };
    }

    private bool IsOwnedBy(SeedFileEvidence file, SeedEstimationScope scope)
    {
        if (file.Ecosystems.Count > 0 && !file.Ecosystems.Contains(scope.Ecosystem, StringComparer.Ordinal))
        {
            return false;
        }

        SeedEstimationScope? owner = FindScopeForPath(file.Path, scope.Ecosystem);
        return owner?.Id == scope.Id;
    }

    private static bool IsAnalyzedSource(SeedFileEvidence file, string ecosystem)
    {
        if (!file.IsMaintained || file.Role is not ("source" or "test"))
        {
            return false;
        }

        return ecosystem == "dotnet"
            ? file.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
            : ecosystem == "javascript" && JavaScriptSourceExtensions.Contains(file.Extension);
    }

    private static string ProjectDirectory(string scope)
    {
        string normalized = scope.Replace('\\', '/');
        int separator = normalized.LastIndexOf('/');
        return separator < 0 ? "." : normalized[..separator];
    }

    private static string NormalizeDirectory(string scope) =>
        string.IsNullOrEmpty(scope) ? "." : scope.TrimEnd('/');

    private static bool IsWithin(string path, string directory) =>
        directory == "." ||
        path.Equals(directory, StringComparison.Ordinal) ||
        path.StartsWith(directory + "/", StringComparison.Ordinal);
}

internal sealed record SeedEstimationScope
{
    public required string Id { get; init; }

    public required string Scope { get; init; }

    public required string Directory { get; init; }

    public required string Ecosystem { get; init; }

    public required string Role { get; init; }

    public required EvidenceFact Fact { get; init; }

    public required bool IsTest { get; init; }

    public required bool IsRunnable { get; init; }

    public bool IsProduction => !IsTest;
}

internal sealed record SeedFileEvidence
{
    public required string Path { get; init; }

    public required string Role { get; init; }

    public required string RoleFamily { get; init; }

    public string? Language { get; init; }

    public required string Extension { get; init; }

    public IReadOnlyList<string> Ecosystems { get; init; } = [];

    public string? Sha256 { get; init; }

    public string? DuplicateKey { get; init; }

    public required bool IsTest { get; init; }

    public required bool IsMaintained { get; init; }

    public required decimal PhysicalLines { get; init; }

    public required EvidenceFact Fact { get; init; }
}

internal sealed record NormalizedEvidenceFact
{
    public required string Key { get; init; }

    public required string Kind { get; init; }

    public required string Scope { get; init; }

    public required string Summary { get; init; }

    public IReadOnlyList<EvidenceFact> Facts { get; init; } = [];

    public IReadOnlyDictionary<string, decimal> Measurements { get; init; } =
        new Dictionary<string, decimal>(StringComparer.Ordinal);

    public IReadOnlyList<string> Tags { get; init; } = [];

    public required bool HasExactDuplicates { get; init; }

    public static NormalizedEvidenceFact Single(EvidenceFact fact) => new()
    {
        Key = fact.Id,
        Kind = fact.Kind,
        Scope = fact.Scope,
        Summary = fact.Summary,
        Facts = [fact],
        Measurements = fact.Measurements
            .GroupBy(measurement => measurement.Name, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(measurement => measurement.Value),
                StringComparer.Ordinal),
        Tags = fact.Tags,
        HasExactDuplicates = false,
    };
}

internal sealed record StructureNormalization(
    decimal Factor,
    int AnalyzedFiles,
    int ProductionFiles,
    int CanonicalProductionFiles,
    bool HasTests,
    bool HasDuplicates);
