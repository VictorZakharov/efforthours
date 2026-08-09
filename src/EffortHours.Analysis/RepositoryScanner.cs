using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts.V1;

namespace EffortHours.Analysis;

public sealed class RepositoryScanner : IRepositoryScanner
{
    public const string AnalyzerName = "efforthours.common-scanner";
    public const string AnalyzerVersion = "0.2.1";

    private const int AggregateLocationLimit = 50;

    private readonly IRepositoryFileSystem _fileSystem;
    private readonly IRepositoryScanCacheStore _cacheStore;

    private static readonly FrozenDictionary<string, string> DefaultExcludedDirectories =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".git"] = "version-control-metadata",
            [".hg"] = "version-control-metadata",
            [".svn"] = "version-control-metadata",
            ["node_modules"] = "vendored-dependencies",
            ["bower_components"] = "vendored-dependencies",
            ["vendor"] = "vendored-dependencies",
            ["obj"] = "build-output",
            ["dist"] = "build-output",
            ["out"] = "build-output",
            ["target"] = "build-output",
            ["artifacts"] = "build-output",
            [".next"] = "framework-output",
            [".nuxt"] = "framework-output",
            [".svelte-kit"] = "framework-output",
            [".packages"] = "tool-cache",
            [".efforthours"] = "tool-cache",
            [".pnpm-store"] = "tool-cache",
            [".vs"] = "tool-state",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public RepositoryScanner()
        : this(
            PhysicalRepositoryFileSystem.Instance,
            PhysicalRepositoryScanCacheStore.Instance)
    {
    }

    public RepositoryScanner(
        IRepositoryFileSystem fileSystem,
        IRepositoryScanCacheStore? cacheStore = null)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _cacheStore = cacheStore ?? PhysicalRepositoryScanCacheStore.Instance;
    }

    public async Task<RepositoryEvidence> ScanAsync(
        string repositoryPath,
        RepositoryScanOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        options ??= new RepositoryScanOptions();
        if (options.FileReadBufferSize < 4 * 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The file read buffer must be at least 4096 bytes.");
        }

        if (options.TextSampleSize < 256)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The text sample must be at least 256 bytes.");
        }

        string rootPath = _fileSystem.GetFullPath(repositoryPath);
        if (!_fileSystem.DirectoryExists(rootPath))
        {
            throw new DirectoryNotFoundException($"Repository directory was not found: {repositoryPath}");
        }

        if ((_fileSystem.GetAttributes(rootPath) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException(
                "The repository root cannot be a symbolic link or filesystem reparse point.");
        }

        string repositoryKey = ComputeRepositoryKey(rootPath);
        string? cachePath = ResolveCachePath(rootPath, options.CachePath);
        RepositoryScanCache? cache = cachePath is null
            ? null
            : await _cacheStore.LoadAsync(
                cachePath,
                repositoryKey,
                cancellationToken).ConfigureAwait(false);
        ScanState state = new(rootPath, options, cache, _fileSystem);
        await TraverseAsync(state, cancellationToken).ConfigureAwait(false);
        if (cachePath is not null)
        {
            await _cacheStore.SaveAsync(
                cachePath,
                CreateCache(repositoryKey, state.Files),
                cancellationToken).ConfigureAwait(false);
        }

        return BuildEvidence(rootPath, state);
    }

    private static async Task TraverseAsync(ScanState state, CancellationToken cancellationToken)
    {
        Stack<DirectoryFrame> pending = new();
        pending.Push(new DirectoryFrame(state.RootPath, string.Empty, []));

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DirectoryFrame frame = pending.Pop();
            if (frame.RelativePath.Length > 0)
            {
                try
                {
                    FileAttributes currentAttributes = state.FileSystem.GetAttributes(frame.FullPath);
                    if ((currentAttributes & FileAttributes.ReparsePoint) != 0)
                    {
                        state.Exclusions.Add(new ExcludedEntry(
                            frame.RelativePath,
                            "filesystem-link",
                            true));
                        continue;
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    state.Exclusions.Add(new ExcludedEntry(frame.RelativePath, "unreadable", true));
                    state.AddUnreadableDiagnostic(frame.RelativePath, exception);
                    continue;
                }
            }

            IReadOnlyList<IgnoreRule> rules = await LoadIgnoreRulesAsync(
                state,
                frame,
                cancellationToken).ConfigureAwait(false);

            string[] entries;
            try
            {
                entries = state.FileSystem.GetFileSystemEntries(frame.FullPath);
                Array.Sort(entries, CompareEntries);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                state.AddUnreadableDiagnostic(frame.RelativePath, exception);
                continue;
            }

            List<DirectoryFrame> childDirectories = [];
            foreach (string entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string relativePath = NormalizeRelativePath(state.RootPath, entry);
                FileAttributes attributes;
                try
                {
                    attributes = state.FileSystem.GetAttributes(entry);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    state.Exclusions.Add(new ExcludedEntry(relativePath, "unreadable", false));
                    state.AddUnreadableDiagnostic(relativePath, exception);
                    continue;
                }

                bool isDirectory = (attributes & FileAttributes.Directory) != 0;
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    state.Exclusions.Add(new ExcludedEntry(relativePath, "filesystem-link", isDirectory));
                    continue;
                }

                if (isDirectory)
                {
                    string? defaultReason = GetDefaultExclusionReason(relativePath);
                    if (defaultReason is not null)
                    {
                        state.Exclusions.Add(new ExcludedEntry(relativePath, defaultReason, true));
                        continue;
                    }

                    if (IgnoreRule.IsIgnored(rules, relativePath, true))
                    {
                        state.Exclusions.Add(new ExcludedEntry(relativePath, "ignore-rule", true));
                        continue;
                    }

                    childDirectories.Add(new DirectoryFrame(entry, relativePath, rules));
                    continue;
                }

                if (IgnoreRule.IsIgnored(rules, relativePath, false))
                {
                    state.Exclusions.Add(new ExcludedEntry(relativePath, "ignore-rule", false));
                    continue;
                }

                try
                {
                    RepositoryFileMetadata metadata = state.FileSystem.GetFileMetadata(entry);
                    long metadataLength = metadata.Length;
                    long lastWriteTimeUtcTicks = metadata.LastWriteTimeUtcTicks;
                    if (state.TryGetCachedFile(
                        relativePath,
                        metadataLength,
                        lastWriteTimeUtcTicks,
                        out ScannedFile cachedFile))
                    {
                        state.Files.Add(cachedFile);
                        continue;
                    }

                    FileInspection inspection = await FileInspection.CreateAsync(
                        state.FileSystem,
                        entry,
                        state.Options,
                        cancellationToken).ConfigureAwait(false);
                    RepositoryFileMetadata refreshedMetadata = state.FileSystem.GetFileMetadata(entry);
                    state.Files.Add(new ScannedFile(
                        relativePath,
                        inspection,
                        FileClassifier.Classify(relativePath, inspection),
                        inspection.Bytes,
                        refreshedMetadata.Exists
                            ? refreshedMetadata.LastWriteTimeUtcTicks
                            : lastWriteTimeUtcTicks));
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    state.Exclusions.Add(new ExcludedEntry(relativePath, "unreadable", false));
                    state.AddUnreadableDiagnostic(relativePath, exception);
                }
            }

            for (int index = childDirectories.Count - 1; index >= 0; index--)
            {
                pending.Push(childDirectories[index]);
            }
        }
    }

    private static async Task<IReadOnlyList<IgnoreRule>> LoadIgnoreRulesAsync(
        ScanState state,
        DirectoryFrame frame,
        CancellationToken cancellationToken)
    {
        List<IgnoreRule>? rules = null;
        if (state.Options.RespectGitIgnore)
        {
            rules = await AddRulesFromFileAsync(
                state,
                frame,
                ".gitignore",
                rules,
                cancellationToken).ConfigureAwait(false);
        }

        if (state.Options.RespectEffortHoursIgnore)
        {
            rules = await AddRulesFromFileAsync(
                state,
                frame,
                ".efforthoursignore",
                rules,
                cancellationToken).ConfigureAwait(false);
        }

        return rules ?? frame.InheritedRules;
    }

    private static async Task<List<IgnoreRule>?> AddRulesFromFileAsync(
        ScanState state,
        DirectoryFrame frame,
        string fileName,
        List<IgnoreRule>? rules,
        CancellationToken cancellationToken)
    {
        string ignorePath = Path.Combine(frame.FullPath, fileName);
        if (!state.FileSystem.FileExists(ignorePath))
        {
            return rules;
        }

        try
        {
            string[] lines = await state.FileSystem.ReadAllLinesAsync(
                ignorePath,
                cancellationToken).ConfigureAwait(false);
            foreach (string line in lines)
            {
                if (!IgnoreRule.TryCreate(line, frame.RelativePath, out IgnoreRule? rule))
                {
                    continue;
                }

                rules ??= [.. frame.InheritedRules];
                rules.Add(rule!);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            string relativePath = NormalizeRelativePath(state.RootPath, ignorePath);
            state.AddUnreadableDiagnostic(relativePath, exception);
        }

        return rules;
    }

    private static RepositoryEvidence BuildEvidence(string rootPath, ScanState state)
    {
        List<ScannedFile> files = [.. state.Files.OrderBy(file => file.RelativePath, StringComparer.Ordinal)];
        List<ExcludedEntry> exclusions =
        [
            .. state.Exclusions
                .Distinct()
                .OrderBy(exclusion => exclusion.RelativePath, StringComparer.Ordinal),
        ];
        string[] ecosystems = [.. files
            .SelectMany(file => file.Classification.Ecosystems)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(ecosystem => ecosystem, StringComparer.Ordinal)];

        List<EvidenceFact> facts = [CreateInventoryFact(files, exclusions)];
        facts.AddRange(files.Select(CreateFileFact));
        facts.AddRange(CreateLanguageFacts(files));
        facts.AddRange(CreateComponentFacts(files));
        AddAggregateFacts(files, facts);
        facts.AddRange(exclusions.Select(CreateExclusionFact));
        facts.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));

        List<Diagnostic> diagnostics =
        [
            .. state.Diagnostics.OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(
                    diagnostic => diagnostic.Locations.Count == 0
                        ? string.Empty
                        : diagnostic.Locations[0].Path,
                    StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal),
        ];
        if (ecosystems.Length == 0)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = "FB2000",
                Severity = DiagnosticSeverity.Information,
                Message = "No .NET, JavaScript, or TypeScript ecosystem markers were detected.",
            });
        }

        string trimmedRootPath = rootPath.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        string repositoryName = Path.GetFileName(trimmedRootPath);
        if (string.IsNullOrWhiteSpace(repositoryName))
        {
            repositoryName = trimmedRootPath;
        }
        return new RepositoryEvidence
        {
            Repository = new RepositoryDescriptor
            {
                Name = repositoryName,
                Scope = ".",
                Ecosystems = ecosystems,
                SourceDigest = ComputeSourceDigest(files),
            },
            Facts = facts,
            Diagnostics = diagnostics,
        };
    }

    private static EvidenceFact CreateInventoryFact(
        IReadOnlyList<ScannedFile> files,
        IReadOnlyList<ExcludedEntry> exclusions)
    {
        IReadOnlyList<ScannedFile> maintainedSource = [.. files.Where(file => file.Classification.Role == "source")];
        IReadOnlyList<ScannedFile> tests = [.. files.Where(file => file.Classification.IsTest && !file.Classification.IsGenerated)];

        return new EvidenceFact
        {
            Id = "inventory:repository",
            Kind = EvidenceKinds.RepositoryInventory,
            Scope = ".",
            Summary = "Deterministic metadata inventory of the analyzed repository scope.",
            Provenance = MeasuredProvenance("safe traversal and streamed file inspection"),
            Measurements =
            [
                Measurement("included-files", files.Count, "files"),
                Measurement("included-bytes", SumBytes(files), "bytes"),
                Measurement("text-lines", SumLines(files), "lines"),
                Measurement("maintained-source-files", maintainedSource.Count, "files"),
                Measurement("maintained-source-lines", SumLines(maintainedSource), "lines"),
                Measurement("test-files", tests.Count, "files"),
                Measurement("test-lines", SumLines(tests), "lines"),
                Measurement("generated-files", files.Count(file => file.Classification.IsGenerated), "files"),
                Measurement("minified-files", files.Count(file => file.Classification.IsMinified), "files"),
                Measurement("vendored-files", files.Count(file => file.Classification.IsVendored), "files"),
                Measurement("binary-files", files.Count(file => file.Inspection.IsBinary), "files"),
                Measurement("excluded-entries", exclusions.Count, "entries"),
            ],
            Tags = ["history:not-inspected", "source-excerpts:not-emitted"],
        };
    }

    private static EvidenceFact CreateFileFact(ScannedFile file)
    {
        List<EvidenceMeasurement> measurements =
        [
            Measurement("bytes", file.Inspection.Bytes, "bytes"),
        ];
        if (!file.Inspection.IsBinary)
        {
            measurements.Add(Measurement("physical-lines", file.Inspection.Lines, "lines"));
        }

        List<string> tags =
        [
            $"role:{file.Classification.Role}",
            file.Inspection.IsBinary ? "content:binary" : "content:text",
            $"sha256:{file.Inspection.Sha256}",
        ];
        string extension = Path.GetExtension(file.RelativePath).ToLowerInvariant();
        if (extension.Length > 0)
        {
            tags.Add($"extension:{extension}");
        }

        if (file.Classification.Language is not null)
        {
            tags.Add($"language:{file.Classification.Language}");
        }

        tags.AddRange(file.Classification.Ecosystems.Select(ecosystem => $"ecosystem:{ecosystem}"));
        if (file.Classification.IsTest)
        {
            tags.Add("classification:test");
        }

        if (file.Classification.IsGenerated)
        {
            tags.Add("classification:generated");
        }

        if (file.Classification.IsMinified)
        {
            tags.Add("classification:minified");
        }

        if (file.Classification.IsVendored)
        {
            tags.Add("classification:vendored");
        }

        return new EvidenceFact
        {
            Id = $"file:{file.RelativePath}",
            Kind = EvidenceKinds.File,
            Scope = file.RelativePath,
            Summary = $"{ToDisplayName(file.Classification.Role)} file '{file.RelativePath}'.",
            Provenance = MeasuredProvenance("streamed hashing, measurement, and classification"),
            Locations = [new EvidenceLocation { Path = file.RelativePath }],
            Measurements = measurements,
            Tags = NormalizeTags(tags),
        };
    }

    private static IEnumerable<EvidenceFact> CreateLanguageFacts(IReadOnlyList<ScannedFile> files)
    {
        return files
            .Where(file => file.Classification.Language is not null)
            .GroupBy(file => file.Classification.Language!, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => CreateAggregateFact(
                $"language:{group.Key}",
                EvidenceKinds.Language,
                $"{ToDisplayName(group.Key)} source and test files.",
                group,
                ["classification:language", $"language:{group.Key}"],
                EvidenceSourceKind.Measured,
                "extension and artifact classification"));
    }

    private static IEnumerable<EvidenceFact> CreateComponentFacts(IReadOnlyList<ScannedFile> files)
    {
        ScannedFile[] manifests = [.. files.Where(file => file.Classification.IsComponentManifest && !file.Classification.IsTest)];
        foreach (ScannedFile manifest in manifests)
        {
            yield return new EvidenceFact
            {
                Id = $"component:{manifest.RelativePath}",
                Kind = EvidenceKinds.Component,
                Scope = manifest.RelativePath,
                Summary = $"Component declared by '{manifest.RelativePath}'.",
                Provenance = ObservedProvenance("project or package manifest classification"),
                Locations = [new EvidenceLocation { Path = manifest.RelativePath }],
                Measurements = [Measurement("manifest-lines", manifest.Inspection.Lines, "lines")],
                Tags = NormalizeTags(
                    manifest.Classification.Ecosystems.Select(ecosystem => $"ecosystem:{ecosystem}")),
            };
        }

        if (manifests.Length == 0)
        {
            ScannedFile[] sourceFiles = [.. files.Where(file => file.Classification.Role == "source")];
            if (sourceFiles.Length > 0)
            {
                yield return CreateAggregateFact(
                    "component:repository",
                    EvidenceKinds.Component,
                    "A repository-level component is inferred from maintained source files.",
                    sourceFiles,
                    ["component:inferred"],
                    EvidenceSourceKind.Inferred,
                    "source inventory without a recognized component manifest");
            }
        }
    }

    private static void AddAggregateFacts(
        IReadOnlyList<ScannedFile> files,
        ICollection<EvidenceFact> facts)
    {
        AddAggregateFact(
            facts,
            files.Where(file => file.Classification.IsTest && !file.Classification.IsGenerated),
            "tests:repository",
            EvidenceKinds.TestSuite,
            "Repository test artifacts detected by path and naming conventions.",
            ["classification:test-suite"]);
        AddAggregateFact(
            facts,
            files.Where(file => file.Classification.Role == "documentation"),
            "documentation:repository",
            EvidenceKinds.Documentation,
            "Maintained repository documentation.",
            ["classification:documentation"]);
        AddAggregateFact(
            facts,
            files.Where(file => file.Classification.Role is
                "project" or "solution" or "package-manifest" or "dependency-lock" or "build-configuration"),
            "build:repository",
            EvidenceKinds.BuildConfiguration,
            "Build, solution, project, package, and dependency configuration.",
            ["classification:build"]);
        AddAggregateFact(
            facts,
            files.Where(file => file.Classification.Role == "ci-configuration"),
            "ci:repository",
            EvidenceKinds.CiConfiguration,
            "Continuous-integration configuration.",
            ["classification:ci"]);
        AddAggregateFact(
            facts,
            files.Where(file => file.Classification.Role == "container-configuration"),
            "containers:repository",
            EvidenceKinds.ContainerConfiguration,
            "Container build and orchestration configuration.",
            ["classification:containers"]);
        AddAggregateFact(
            facts,
            files.Where(file => file.Classification.Role == "infrastructure"),
            "infrastructure:repository",
            EvidenceKinds.Infrastructure,
            "Infrastructure-as-code artifacts.",
            ["classification:infrastructure"]);
        AddAggregateFact(
            facts,
            files.Where(file => file.Classification.Role == "coverage"),
            "coverage:repository",
            EvidenceKinds.Coverage,
            "Coverage configuration or existing coverage artifacts.",
            ["classification:coverage"]);
        AddAggregateFact(
            facts,
            files.Where(file => file.Classification.IsGenerated),
            "excluded-content:generated",
            EvidenceKinds.ExcludedContent,
            "Generated files retained as metadata but excluded from maintained-source totals.",
            ["classification:generated"]);
        AddAggregateFact(
            facts,
            files.Where(file => file.Classification.IsMinified),
            "excluded-content:minified",
            EvidenceKinds.ExcludedContent,
            "Minified or bundled files retained as metadata but excluded from maintained-source totals.",
            ["classification:minified"]);
        AddAggregateFact(
            facts,
            files.Where(file => file.Classification.IsVendored),
            "excluded-content:vendored",
            EvidenceKinds.ExcludedContent,
            "Vendored files retained as metadata but excluded from maintained-source totals.",
            ["classification:vendored"]);
        AddAggregateFact(
            facts,
            files.Where(file => file.Inspection.IsBinary),
            "excluded-content:binary",
            EvidenceKinds.ExcludedContent,
            "Binary files retained as metadata but excluded from maintained-source totals.",
            ["classification:binary"]);
    }

    private static void AddAggregateFact(
        ICollection<EvidenceFact> facts,
        IEnumerable<ScannedFile> candidates,
        string id,
        string kind,
        string summary,
        IReadOnlyList<string> tags)
    {
        ScannedFile[] files = [.. candidates];
        if (files.Length == 0)
        {
            return;
        }

        facts.Add(CreateAggregateFact(
            id,
            kind,
            summary,
            files,
            tags,
            EvidenceSourceKind.Observed,
            "path, filename, extension, and content-shape classification"));
    }

    private static EvidenceFact CreateAggregateFact(
        string id,
        string kind,
        string summary,
        IEnumerable<ScannedFile> candidates,
        IEnumerable<string> tags,
        EvidenceSourceKind sourceKind,
        string method)
    {
        ScannedFile[] files = [.. candidates.OrderBy(file => file.RelativePath, StringComparer.Ordinal)];
        List<string> normalizedTags = [.. tags];
        if (files.Length > AggregateLocationLimit)
        {
            normalizedTags.Add("locations:truncated");
        }

        return new EvidenceFact
        {
            Id = id,
            Kind = kind,
            Scope = ".",
            Summary = summary,
            Provenance = new EvidenceProvenance
            {
                SourceKind = sourceKind,
                Analyzer = AnalyzerName,
                AnalyzerVersion = AnalyzerVersion,
                Method = method,
            },
            Locations =
            [
                .. files.Take(AggregateLocationLimit)
                    .Select(file => new EvidenceLocation { Path = file.RelativePath }),
            ],
            Measurements =
            [
                Measurement("files", files.Length, "files"),
                Measurement("bytes", SumBytes(files), "bytes"),
                Measurement("physical-lines", SumLines(files), "lines"),
            ],
            Tags = NormalizeTags(normalizedTags),
        };
    }

    private static EvidenceFact CreateExclusionFact(ExcludedEntry exclusion) => new()
    {
        Id = $"excluded:{exclusion.RelativePath}",
        Kind = EvidenceKinds.ExcludedContent,
        Scope = exclusion.RelativePath,
        Summary = $"Excluded {(exclusion.IsDirectory ? "directory" : "file")} '{exclusion.RelativePath}'.",
        Provenance = ObservedProvenance("scope and filesystem safety rules"),
        Locations = [new EvidenceLocation { Path = exclusion.RelativePath }],
        Tags =
        [
            $"reason:{exclusion.Reason}",
            exclusion.IsDirectory ? "entry:directory" : "entry:file",
        ],
    };

    private static EvidenceProvenance MeasuredProvenance(string method) => new()
    {
        SourceKind = EvidenceSourceKind.Measured,
        Analyzer = AnalyzerName,
        AnalyzerVersion = AnalyzerVersion,
        Method = method,
    };

    private static EvidenceProvenance ObservedProvenance(string method) => new()
    {
        SourceKind = EvidenceSourceKind.Observed,
        Analyzer = AnalyzerName,
        AnalyzerVersion = AnalyzerVersion,
        Method = method,
    };

    private static EvidenceMeasurement Measurement(string name, decimal value, string unit) => new()
    {
        Name = name,
        Value = value,
        Unit = unit,
    };

    private static decimal SumBytes(IEnumerable<ScannedFile> files) =>
        files.Aggregate(0m, (total, file) => total + file.Inspection.Bytes);

    private static decimal SumLines(IEnumerable<ScannedFile> files) =>
        files.Aggregate(0m, (total, file) => total + file.Inspection.Lines);

    private static string[] NormalizeTags(IEnumerable<string> tags) =>
        [.. tags.Distinct(StringComparer.Ordinal).OrderBy(tag => tag, StringComparer.Ordinal)];

    private static string ComputeSourceDigest(IReadOnlyList<ScannedFile> files)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (ScannedFile file in files)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(file.RelativePath));
            hash.AppendData([0]);
            hash.AppendData(Encoding.ASCII.GetBytes(file.Inspection.Sha256));
            hash.AppendData([(byte)'\n']);
        }

        return $"sha256:{Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()}";
    }

    private static string ComputeRepositoryKey(string rootPath)
    {
        string canonicalPath = Path.TrimEndingDirectorySeparator(rootPath);
        if (OperatingSystem.IsWindows())
        {
            canonicalPath = canonicalPath.ToUpperInvariant();
        }

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPath));
        return $"sha256:{Convert.ToHexString(digest).ToLowerInvariant()}";
    }

    private static string? ResolveCachePath(string rootPath, string? requestedCachePath)
    {
        if (requestedCachePath is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(requestedCachePath))
        {
            throw new ArgumentException("The cache path cannot be empty.", nameof(requestedCachePath));
        }

        string fullCachePath = Path.GetFullPath(requestedCachePath);
        string relativePath = Path.GetRelativePath(rootPath, fullCachePath);
        bool isInsideRepository = relativePath == "." ||
            (!Path.IsPathRooted(relativePath) &&
             relativePath != ".." &&
             !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
             !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal));
        if (isInsideRepository)
        {
            throw new ArgumentException(
                "The scan cache must be outside the analyzed repository so scanning remains read-only.",
                nameof(requestedCachePath));
        }

        return fullCachePath;
    }

    private static RepositoryScanCache CreateCache(
        string repositoryKey,
        IEnumerable<ScannedFile> files) => new()
        {
            AnalyzerVersion = AnalyzerVersion,
            RepositoryKey = repositoryKey,
            Files =
        [
            .. files.OrderBy(file => file.RelativePath, StringComparer.Ordinal)
                .Select(file => new RepositoryScanCacheEntry
                {
                    Path = file.RelativePath,
                    Length = file.MetadataLength,
                    LastWriteTimeUtcTicks = file.LastWriteTimeUtcTicks,
                    Sha256 = file.Inspection.Sha256,
                    Bytes = file.Inspection.Bytes,
                    Lines = file.Inspection.Lines,
                    IsBinary = file.Inspection.IsBinary,
                    Role = file.Classification.Role,
                    Language = file.Classification.Language,
                    Ecosystems = file.Classification.Ecosystems,
                    IsTest = file.Classification.IsTest,
                    IsGenerated = file.Classification.IsGenerated,
                    IsMinified = file.Classification.IsMinified,
                    IsVendored = file.Classification.IsVendored,
                    IsComponentManifest = file.Classification.IsComponentManifest,
                }),
        ],
        };

    private static string? GetDefaultExclusionReason(string relativePath)
    {
        string fileName = Path.GetFileName(relativePath);
        if (DefaultExcludedDirectories.TryGetValue(fileName, out string? reason))
        {
            return reason;
        }

        string lowerPath = relativePath.ToLowerInvariant();
        if (lowerPath is ".yarn/cache" or ".nuget/packages")
        {
            return "tool-cache";
        }

        return null;
    }

    private static string NormalizeRelativePath(string rootPath, string path) =>
        Path.GetRelativePath(rootPath, path).Replace('\\', '/');

    private static int CompareEntries(string left, string right) =>
        StringComparer.Ordinal.Compare(Path.GetFileName(left), Path.GetFileName(right));

    private static string ToDisplayName(string value) => value.Replace('-', ' ');

    private sealed class ScanState(
        string rootPath,
        RepositoryScanOptions options,
        RepositoryScanCache? cache,
        IRepositoryFileSystem fileSystem)
    {
        private readonly Dictionary<string, RepositoryScanCacheEntry> _cachedFiles =
            cache?.Files.ToDictionary(entry => entry.Path, StringComparer.Ordinal) ??
            new Dictionary<string, RepositoryScanCacheEntry>(StringComparer.Ordinal);

        public string RootPath { get; } = rootPath;

        public RepositoryScanOptions Options { get; } = options;

        public IRepositoryFileSystem FileSystem { get; } = fileSystem;

        public List<ScannedFile> Files { get; } = [];

        public List<ExcludedEntry> Exclusions { get; } = [];

        public List<Diagnostic> Diagnostics { get; } = [];

        public bool TryGetCachedFile(
            string relativePath,
            long length,
            long lastWriteTimeUtcTicks,
            out ScannedFile file)
        {
            if (!_cachedFiles.TryGetValue(relativePath, out RepositoryScanCacheEntry? entry) ||
                entry.Length != length ||
                entry.LastWriteTimeUtcTicks != lastWriteTimeUtcTicks)
            {
                file = null!;
                return false;
            }

            file = new ScannedFile(
                relativePath,
                new FileInspection(
                    entry.Bytes,
                    entry.Lines,
                    entry.Sha256,
                    entry.IsBinary,
                    string.Empty),
                new FileClassification(
                    entry.Role,
                    entry.Language,
                    entry.Ecosystems,
                    entry.IsTest,
                    entry.IsGenerated,
                    entry.IsMinified,
                    entry.IsVendored,
                    entry.IsComponentManifest),
                entry.Length,
                entry.LastWriteTimeUtcTicks);
            return true;
        }

        public void AddUnreadableDiagnostic(string relativePath, Exception exception)
        {
            string displayPath = relativePath.Length == 0 ? "." : relativePath;
            Diagnostics.Add(new Diagnostic
            {
                Code = "FB2001",
                Severity = DiagnosticSeverity.Warning,
                Message = $"Could not inspect '{displayPath}': {DescribeReadFailure(exception)}",
                Locations = [new EvidenceLocation { Path = displayPath }],
            });
        }

        private static string DescribeReadFailure(Exception exception) => exception switch
        {
            UnauthorizedAccessException => "access was denied.",
            IOException => "the filesystem entry could not be read.",
            _ => "the entry could not be inspected.",
        };
    }

    private sealed record DirectoryFrame(
        string FullPath,
        string RelativePath,
        IReadOnlyList<IgnoreRule> InheritedRules);

    private sealed record ScannedFile(
        string RelativePath,
        FileInspection Inspection,
        FileClassification Classification,
        long MetadataLength,
        long LastWriteTimeUtcTicks);

    private sealed record ExcludedEntry(string RelativePath, string Reason, bool IsDirectory);
}
