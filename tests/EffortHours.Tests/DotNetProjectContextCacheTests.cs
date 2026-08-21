using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Analyzers.DotNet;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class DotNetProjectContextCacheTests
{
    private const string Project =
        "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
        "<TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n";

    [Fact]
    public async Task ImmutableProjectContextIsReusedAcrossSourceOnlySnapshots()
    {
        RepositoryAnalysisArtifactCache cache = new();
        ReadCounter reads = new();
        ImmutableContextRepository before = new(
            new Dictionary<string, string>
            {
                ["App.csproj"] = Project,
                ["Program.cs"] = "public sealed class Before;\n",
            },
            cache,
            reads);
        ImmutableContextRepository after = new(
            new Dictionary<string, string>
            {
                ["App.csproj"] = Project,
                ["Program.cs"] = "public sealed class After;\n",
            },
            cache,
            reads);
        RepositoryEvidence evidence = Evidence("App.csproj");

        RepositoryAnalysisContribution first = await new DotNetRepositoryAnalyzer(before)
            .AnalyzeAsync(before.RootPath, evidence);
        RepositoryAnalysisContribution second = await new DotNetRepositoryAnalyzer(after)
            .AnalyzeAsync(after.RootPath, evidence);

        Assert.Equal(Serialize(first), Serialize(second));
        Assert.Equal(1, reads["App.csproj"]);
        RepositoryAnalysisArtifactCacheStatistics statistics = cache.GetStatistics();
        Assert.Equal(4, statistics.Requests);
        Assert.Equal(2, statistics.Hits);
        Assert.Equal(2, statistics.UniqueKeys);
    }

    [Fact]
    public async Task PathAdditionInvalidatesCachedProjectReferenceResolution()
    {
        RepositoryAnalysisArtifactCache cache = new();
        ReadCounter reads = new();
        const string referencingProject =
            "<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup>" +
            "<ProjectReference Include=\"Library.csproj\" />" +
            "</ItemGroup></Project>\n";
        ImmutableContextRepository missing = new(
            new Dictionary<string, string> { ["App.csproj"] = referencingProject },
            cache,
            reads);
        ImmutableContextRepository resolved = new(
            new Dictionary<string, string>
            {
                ["App.csproj"] = referencingProject,
                ["Library.csproj"] = Project,
            },
            cache,
            reads);
        RepositoryEvidence evidence = Evidence("App.csproj");

        RepositoryAnalysisContribution first = await new DotNetRepositoryAnalyzer(missing)
            .AnalyzeAsync(missing.RootPath, evidence);
        RepositoryAnalysisContribution second = await new DotNetRepositoryAnalyzer(resolved)
            .AnalyzeAsync(resolved.RootPath, evidence);

        Assert.Contains(first.Diagnostics, diagnostic => diagnostic.Code == "FB3003");
        Assert.DoesNotContain(second.Diagnostics, diagnostic => diagnostic.Code == "FB3003");
        Assert.Contains(
            Assert.Single(second.Facts, fact => fact.Kind == EvidenceKinds.ProjectReference).Tags,
            tag => tag == "reference:resolved");
        Assert.Equal(2, reads["App.csproj"]);
        Assert.Equal(4, cache.GetStatistics().UniqueKeys);
    }

    [Fact]
    public async Task MissingImmutablePathIdentityUsesColdAnalysis()
    {
        RepositoryAnalysisArtifactCache cache = new();
        ReadCounter reads = new();
        ImmutableContextRepository repository = new(
            new Dictionary<string, string> { ["App.csproj"] = Project },
            cache,
            reads,
            exposePathSetIdentity: false);
        RepositoryEvidence evidence = Evidence("App.csproj");
        DotNetRepositoryAnalyzer analyzer = new(repository);

        RepositoryAnalysisContribution first = await analyzer.AnalyzeAsync(
            repository.RootPath,
            evidence);
        RepositoryAnalysisContribution second = await analyzer.AnalyzeAsync(
            repository.RootPath,
            evidence);

        Assert.Equal(Serialize(first), Serialize(second));
        Assert.Equal(2, reads["App.csproj"]);
        Assert.Equal(0, cache.GetStatistics().Requests);
    }

    private static RepositoryEvidence Evidence(params string[] paths) => new()
    {
        Repository = new RepositoryDescriptor
        {
            Name = "immutable-context-fixture",
            Scope = ".",
            Ecosystems = ["dotnet"],
            SourceDigest = "sha256:fixture",
        },
        Facts = [.. paths.Select(FileFact)],
    };

    private static EvidenceFact FileFact(string path) => new()
    {
        Id = $"file:{path}",
        Kind = EvidenceKinds.File,
        Scope = path,
        Summary = "Immutable fixture file.",
        Provenance = new EvidenceProvenance
        {
            SourceKind = EvidenceSourceKind.Observed,
            Analyzer = "test-fixture",
            AnalyzerVersion = "1.0.0",
            Method = "synthetic immutable context",
        },
    };

    private static string Serialize(RepositoryAnalysisContribution contribution) =>
        ContractJson.Serialize(new RepositoryEvidence
        {
            Repository = Evidence().Repository,
            Facts = contribution.Facts,
            Diagnostics = contribution.Diagnostics,
        });

    private sealed class ReadCounter
    {
        private readonly ConcurrentDictionary<string, int> _counts =
            new(StringComparer.Ordinal);

        public int this[string path] => _counts.GetValueOrDefault(path);

        public void Increment(string path) => _counts.AddOrUpdate(path, 1, (_, count) => count + 1);
    }

    private sealed class ImmutableContextRepository :
        IRepositoryFileSystem,
        IRepositoryAnalysisArtifactCacheProvider,
        IRepositoryImmutableIdentityProvider
    {
        private readonly RepositoryAnalysisArtifactCache _cache;
        private readonly Dictionary<string, string> _contentIds;
        private readonly InMemoryRepository _inner = new();
        private readonly ReadCounter _reads;

        public ImmutableContextRepository(
            IReadOnlyDictionary<string, string> files,
            RepositoryAnalysisArtifactCache cache,
            ReadCounter reads,
            bool exposePathSetIdentity = true)
        {
            _cache = cache;
            _reads = reads;
            _contentIds = files.ToDictionary(
                pair => pair.Key,
                pair => Sha256(pair.Value),
                StringComparer.Ordinal);
            foreach ((string path, string content) in files)
            {
                _inner.WriteText(path, content);
            }

            RepositoryPathSetIdentity = exposePathSetIdentity
                ? Sha256(string.Join('\n', files.Keys.Order(StringComparer.Ordinal)))
                : null;
        }

        public string RootPath => _inner.RootPath;

        public RepositoryAnalysisArtifactCache? AnalysisArtifactCache => _cache;

        public string? RepositoryPathSetIdentity { get; }

        public bool TryGetFileContentId(string path, out string contentId) =>
            _contentIds.TryGetValue(RelativePath(path), out contentId!);

        public string GetFullPath(string path) => _inner.GetFullPath(path);

        public bool DirectoryExists(string path) => _inner.DirectoryExists(path);

        public bool FileExists(string path) => _inner.FileExists(path);

        public FileAttributes GetAttributes(string path) => _inner.GetAttributes(path);

        public string[] GetFileSystemEntries(string directoryPath) =>
            _inner.GetFileSystemEntries(directoryPath);

        public RepositoryFileMetadata GetFileMetadata(string path)
        {
            RepositoryFileMetadata metadata = _inner.GetFileMetadata(path);
            return metadata with { ContentId = _contentIds[RelativePath(path)] };
        }

        public Stream OpenRead(string path, int bufferSize)
        {
            _reads.Increment(RelativePath(path));
            return _inner.OpenRead(path, bufferSize);
        }

        public ValueTask<byte[]> ReadAllBytesAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            _inner.ReadAllBytesAsync(path, cancellationToken);

        public ValueTask<string[]> ReadAllLinesAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            _reads.Increment(RelativePath(path));
            return _inner.ReadAllLinesAsync(path, cancellationToken);
        }

        private string RelativePath(string path) => Path.GetRelativePath(
            RootPath,
            _inner.GetFullPath(path)).Replace('\\', '/');

        private static string Sha256(string value) => Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
