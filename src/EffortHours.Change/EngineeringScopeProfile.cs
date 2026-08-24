using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed class EngineeringScopeProfile
{
    public const string ProfileName = "engineering";
    private const string ResourceName = "EffortHours.Change.ScopeProfiles.engineering.json";
    private const string OverrideEnvironmentVariable = "EFFORTHOURS_ENGINEERING_SCOPE_PROFILE";
    private readonly ScopeProfileDocument _document;

    private EngineeringScopeProfile(ScopeProfileDocument document, string source)
    {
        _document = Canonicalize(document);
        Contract = new ChangePortfolioScopeProfile
        {
            Id = ProfileName,
            Version = _document.SemanticVersion,
            Digest = Digest(_document),
            Source = source,
            ImportantExclusions = _document.ImportantExclusions,
        };
    }

    public ChangePortfolioScopeProfile Contract { get; }

    public static EngineeringScopeProfile Load()
    {
        string? explicitPath = Environment.GetEnvironmentVariable(OverrideEnvironmentVariable);
        return !string.IsNullOrWhiteSpace(explicitPath)
            ? Load(Path.GetFullPath(explicitPath), requireExists: true)
            : Load(ResolveDefaultOverridePath(), requireExists: false);
    }

    public static EngineeringScopeProfile LoadBundled() => Load(null, requireExists: false);

    internal static EngineeringScopeProfile Load(string? overridePath) =>
        Load(overridePath, requireExists: !string.IsNullOrWhiteSpace(overridePath));

    private static EngineeringScopeProfile Load(string? overridePath, bool requireExists)
    {
        ScopeProfileDocument bundled = ReadBundled();
        Validate(bundled, requireComplete: true);
        if (string.IsNullOrWhiteSpace(overridePath))
        {
            return new EngineeringScopeProfile(bundled, "bundled");
        }

        if (!File.Exists(overridePath))
        {
            if (requireExists)
            {
                throw new InvalidOperationException(
                    "The configured engineering scope override does not exist.");
            }

            return new EngineeringScopeProfile(bundled, "bundled");
        }

        ScopeProfileDocument custom = Read(File.ReadAllText(overridePath, Encoding.UTF8));
        if (custom.Mode is not ("extend" or "replace"))
        {
            throw new InvalidOperationException(
                "The engineering scope override mode must be 'extend' or 'replace'.");
        }

        Validate(custom, requireComplete: custom.Mode == "replace");
        ScopeProfileDocument effective = custom.Mode == "replace"
            ? custom
            : Merge(bundled, custom);
        Validate(effective, requireComplete: true);
        return new EngineeringScopeProfile(effective, "bundled+user-" + custom.Mode);
    }

    public bool ExcludesRepository(string repositoryIdentity, bool archived, bool mirror)
    {
        string canonical = CanonicalRepository(repositoryIdentity);
        return archived || mirror ||
            _document.RepositoryOverrides.GetValueOrDefault(canonical)?.ExcludeRepository == true;
    }

    public ChangePathAdmission CreateAdmission(string repositoryIdentity)
    {
        string canonical = CanonicalRepository(repositoryIdentity);
        ScopeRepositoryOverride? repository =
            _document.RepositoryOverrides.GetValueOrDefault(canonical);
        return new ChangePathAdmission(Contract.Digest, path => Admits(path, repository));
    }

    private bool Admits(string path, ScopeRepositoryOverride? repository)
    {
        string canonical = path.Replace('\\', '/');
        string fileName = Path.GetFileName(canonical);
        string extension = Path.GetExtension(canonical);
        string[] segments = canonical.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (repository?.ExcludeRepository == true ||
            repository?.ExcludePrefixes.Any(prefix =>
                canonical.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) == true ||
            _document.ExcludeDirectories.Any(directory =>
                segments.Contains(directory, StringComparer.OrdinalIgnoreCase)) ||
            _document.ExcludeFileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase) ||
            _document.ExcludeExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        return repository?.IncludePrefixes.Any(prefix =>
                canonical.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) == true ||
            _document.IncludePrefixes.Any(prefix =>
                canonical.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) ||
            _document.IncludeFileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase) ||
            _document.IncludeExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static string? ResolveDefaultOverridePath()
    {
        string applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return string.IsNullOrWhiteSpace(applicationData)
            ? null
            : Path.Combine(applicationData, "EffortHours", "scope-profiles", "engineering.json");
    }

    private static ScopeProfileDocument ReadBundled()
    {
        using Stream stream = typeof(EngineeringScopeProfile).Assembly.GetManifestResourceStream(ResourceName) ??
            throw new InvalidOperationException("The bundled engineering scope profile is missing.");
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return Read(reader.ReadToEnd());
    }

    private static ScopeProfileDocument Read(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ScopeProfileDocument>(json, JsonOptions) ??
                throw new InvalidOperationException("The engineering scope profile is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "The engineering scope profile is not valid strict JSON.", exception);
        }
    }

    private static void Validate(ScopeProfileDocument value, bool requireComplete)
    {
        if (value.SchemaVersion != "1.0.0" ||
            string.IsNullOrWhiteSpace(value.SemanticVersion) ||
            value.SemanticVersion.Length > 128 ||
            value.ImportantExclusions.Any(string.IsNullOrWhiteSpace) ||
            value.RepositoryOverrides.Keys.Any(key => !IsCanonicalRepository(key)) ||
            AllRules(value).Any(rule => string.IsNullOrWhiteSpace(rule) || rule.Contains('\\')))
        {
            throw new InvalidOperationException(
                "The engineering scope profile has an unsupported version or non-canonical rule.");
        }

        if (requireComplete &&
            (value.IncludeExtensions.Count == 0 || value.ImportantExclusions.Count == 0))
        {
            throw new InvalidOperationException(
                "A replacement engineering scope profile must define admissions and important exclusions.");
        }
    }

    private static IEnumerable<string> AllRules(ScopeProfileDocument value) =>
        value.IncludeExtensions.Concat(value.IncludeFileNames).Concat(value.IncludePrefixes)
            .Concat(value.ExcludeExtensions).Concat(value.ExcludeFileNames)
            .Concat(value.ExcludeDirectories).Concat(value.ImportantExclusions)
            .Concat(value.RepositoryOverrides.Values.SelectMany(item =>
                item.IncludePrefixes.Concat(item.ExcludePrefixes)));

    private static ScopeProfileDocument Merge(
        ScopeProfileDocument bundled,
        ScopeProfileDocument custom) => bundled with
        {
            SemanticVersion = custom.SemanticVersion,
            Mode = "replace",
            IncludeExtensions = Union(bundled.IncludeExtensions, custom.IncludeExtensions),
            IncludeFileNames = Union(bundled.IncludeFileNames, custom.IncludeFileNames),
            IncludePrefixes = Union(bundled.IncludePrefixes, custom.IncludePrefixes),
            ExcludeExtensions = Union(bundled.ExcludeExtensions, custom.ExcludeExtensions),
            ExcludeFileNames = Union(bundled.ExcludeFileNames, custom.ExcludeFileNames),
            ExcludeDirectories = Union(bundled.ExcludeDirectories, custom.ExcludeDirectories),
            ImportantExclusions = Union(
                bundled.ImportantExclusions,
                custom.ImportantExclusions),
            RepositoryOverrides = MergeRepositories(
                bundled.RepositoryOverrides,
                custom.RepositoryOverrides),
        };

    private static ScopeProfileDocument Canonicalize(ScopeProfileDocument value) => value with
    {
        Mode = "replace",
        IncludeExtensions = Canonical(value.IncludeExtensions),
        IncludeFileNames = Canonical(value.IncludeFileNames),
        IncludePrefixes = Canonical(value.IncludePrefixes),
        ExcludeExtensions = Canonical(value.ExcludeExtensions),
        ExcludeFileNames = Canonical(value.ExcludeFileNames),
        ExcludeDirectories = Canonical(value.ExcludeDirectories),
        ImportantExclusions = Canonical(value.ImportantExclusions),
        RepositoryOverrides = value.RepositoryOverrides
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(
                item => item.Key,
                item => item.Value with
                {
                    IncludePrefixes = Canonical(item.Value.IncludePrefixes),
                    ExcludePrefixes = Canonical(item.Value.ExcludePrefixes),
                },
                StringComparer.Ordinal),
    };

    private static IReadOnlyList<string> Canonical(IEnumerable<string> values) => [.. values
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Order(StringComparer.OrdinalIgnoreCase)
        .ThenBy(value => value, StringComparer.Ordinal)];

    private static Dictionary<string, ScopeRepositoryOverride> MergeRepositories(
        IReadOnlyDictionary<string, ScopeRepositoryOverride> bundled,
        IReadOnlyDictionary<string, ScopeRepositoryOverride> custom)
    {
        Dictionary<string, ScopeRepositoryOverride> merged = new(bundled, StringComparer.Ordinal);
        foreach ((string id, ScopeRepositoryOverride value) in custom)
        {
            ScopeRepositoryOverride prior = merged.GetValueOrDefault(id) ?? new();
            merged[id] = new ScopeRepositoryOverride
            {
                ExcludeRepository = prior.ExcludeRepository || value.ExcludeRepository,
                IncludePrefixes = Union(prior.IncludePrefixes, value.IncludePrefixes),
                ExcludePrefixes = Union(prior.ExcludePrefixes, value.ExcludePrefixes),
            };
        }

        return merged;
    }

    private static IReadOnlyList<string> Union(
        IEnumerable<string> first,
        IEnumerable<string> second) => [.. first.Concat(second)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ThenBy(value => value, StringComparer.Ordinal)];

    private static string CanonicalRepository(string value) => value.Trim().ToLowerInvariant();

    private static bool IsCanonicalRepository(string value)
    {
        string[] parts = value.Split('/');
        return value == CanonicalRepository(value) &&
            parts.Length == 2 &&
            parts.All(part => part.Length > 0 && part.All(character =>
                char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'));
    }

    private static string Digest(ScopeProfileDocument document)
    {
        string json = JsonSerializer.Serialize(document, JsonOptions);
        return "sha256:" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
    }

    private static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private sealed record ScopeProfileDocument
    {
        public string SchemaVersion { get; init; } = string.Empty;
        public string SemanticVersion { get; init; } = string.Empty;
        public string Mode { get; init; } = string.Empty;
        public IReadOnlyList<string> IncludeExtensions { get; init; } = [];
        public IReadOnlyList<string> IncludeFileNames { get; init; } = [];
        public IReadOnlyList<string> IncludePrefixes { get; init; } = [];
        public IReadOnlyList<string> ExcludeExtensions { get; init; } = [];
        public IReadOnlyList<string> ExcludeFileNames { get; init; } = [];
        public IReadOnlyList<string> ExcludeDirectories { get; init; } = [];
        public IReadOnlyList<string> ImportantExclusions { get; init; } = [];
        public IReadOnlyDictionary<string, ScopeRepositoryOverride> RepositoryOverrides { get; init; } =
            new Dictionary<string, ScopeRepositoryOverride>(StringComparer.Ordinal);
    }

    private sealed record ScopeRepositoryOverride
    {
        public bool ExcludeRepository { get; init; }
        public IReadOnlyList<string> IncludePrefixes { get; init; } = [];
        public IReadOnlyList<string> ExcludePrefixes { get; init; } = [];
    }
}
