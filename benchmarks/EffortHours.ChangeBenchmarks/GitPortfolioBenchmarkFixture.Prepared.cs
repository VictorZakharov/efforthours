using System.Text.Json;
using System.Text.Json.Serialization;
using EffortHours.Contracts.V1;

namespace EffortHours.ChangeBenchmarks;

internal sealed partial class GitPortfolioBenchmarkFixture
{
    private const string DescriptorFileName = "author-period-benchmark-fixture.json";
    private const string DescriptorSchemaVersion = "1.0.0";
    private static readonly JsonSerializerOptions DescriptorJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static async Task<GitPortfolioBenchmarkFixture> LoadAsync(
        string descriptorPath,
        CancellationToken cancellationToken)
    {
        string fullDescriptorPath = Path.GetFullPath(descriptorPath);
        if (!File.Exists(fullDescriptorPath))
        {
            throw new ArgumentException(
                $"Prepared fixture descriptor '{fullDescriptorPath}' does not exist.",
                nameof(descriptorPath));
        }

        PreparedFixtureDescriptor descriptor;
        try
        {
            string json = await File.ReadAllTextAsync(fullDescriptorPath, cancellationToken)
                .ConfigureAwait(false);
            descriptor = JsonSerializer.Deserialize<PreparedFixtureDescriptor>(
                    json,
                    DescriptorJsonOptions) ??
                throw new ArgumentException(
                    "Prepared fixture descriptor is empty.",
                    nameof(descriptorPath));
        }
        catch (JsonException exception)
        {
            throw new ArgumentException(
                "Prepared fixture descriptor is not valid JSON.",
                nameof(descriptorPath),
                exception);
        }

        ValidateDescriptor(descriptor, nameof(descriptorPath));
        string rootPath = Path.GetDirectoryName(fullDescriptorPath) ??
            throw new ArgumentException(
                "Prepared fixture descriptor has no containing directory.",
                nameof(descriptorPath));
        ChangeAuthorPeriodManifest manifest = descriptor.Manifest with
        {
            Repositories = [.. descriptor.Manifest.Repositories.Select(repository => repository with
            {
                RepositoryPath = ResolveRepositoryPath(rootPath, repository.RepositoryPath),
            })],
        };
        PortfolioBenchmarkRepository[] repositories = [.. manifest.Repositories.Select(repository =>
            new PortfolioBenchmarkRepository(
                repository.Id,
                repository.RepositoryPath,
                repository.Heads))];
        foreach (PortfolioBenchmarkRepository repository in repositories)
        {
            if (!Directory.Exists(repository.Path))
            {
                throw new ArgumentException(
                    $"Prepared fixture repository '{repository.Id}' is missing.",
                    nameof(descriptorPath));
            }

            foreach (ChangeAuthorPeriodManifestHead head in repository.Heads)
            {
                _ = await GitBenchmarkRepository.GitAsync(
                    repository.Path,
                    cancellationToken,
                    "cat-file",
                    "-e",
                    $"{head.ObjectId}^{{commit}}").ConfigureAwait(false);
            }
        }

        return new GitPortfolioBenchmarkFixture(
            rootPath,
            manifest,
            repositories,
            descriptor.HeadFileCount,
            descriptor.HeadDirectoryCount,
            descriptor.SharedObjectId,
            descriptor.FilesPerRepository,
            descriptor.ContextProjectsPerRepository,
            descriptor.LinesPerFile,
            descriptor.EditShape,
            descriptor.QualifyingCommitsPerRepository,
            keep: true,
            descriptorPath: fullDescriptorPath)
        {
            IsPreparedFixture = true,
        };
    }

    private async Task WriteDescriptorAsync(CancellationToken cancellationToken)
    {
        ChangeAuthorPeriodManifest portableManifest = Manifest with
        {
            Repositories = [.. Manifest.Repositories.Select(repository => repository with
            {
                RepositoryPath = Path.GetRelativePath(RootPath, repository.RepositoryPath)
                    .Replace(Path.DirectorySeparatorChar, '/'),
            })],
        };
        PreparedFixtureDescriptor descriptor = new()
        {
            SchemaVersion = DescriptorSchemaVersion,
            FilesPerRepository = FilesPerRepository,
            ContextProjectsPerRepository = ContextProjectsPerRepository,
            LinesPerFile = LinesPerFile,
            EditShape = EditShape,
            QualifyingCommitsPerRepository = QualifyingCommitsPerRepository,
            HeadFileCount = HeadFileCount,
            HeadDirectoryCount = HeadDirectoryCount,
            SharedObjectId = SharedObjectId,
            Manifest = portableManifest,
        };
        string descriptorPath = Path.Combine(RootPath, DescriptorFileName);
        await File.WriteAllTextAsync(
            descriptorPath,
            JsonSerializer.Serialize(descriptor, DescriptorJsonOptions),
            cancellationToken).ConfigureAwait(false);
        DescriptorPath = descriptorPath;
    }

    public async Task<(RepositoryStateSnapshot Worktree, RepositoryStateSnapshot Git)>
        CaptureLightweightStateAsync(CancellationToken cancellationToken)
    {
        LightweightRepositoryState[] states = await Task.WhenAll(Repositories.Select(
            repository => CaptureLightweightRepositoryStateAsync(
                repository,
                cancellationToken))).ConfigureAwait(false);
        RepositoryStateSnapshot worktree = RepositoryStateSnapshot.Combine(states.Select(state => (
            state.Id,
            RepositoryStateSnapshot.FromText(state.Worktree))));
        RepositoryStateSnapshot git = RepositoryStateSnapshot.Combine(states.Select(state => (
            state.Id,
            RepositoryStateSnapshot.FromText(state.Git))));
        return (
            worktree with { FileCount = HeadFileCount },
            git);
    }

    private static async Task<LightweightRepositoryState> CaptureLightweightRepositoryStateAsync(
        PortfolioBenchmarkRepository repository,
        CancellationToken cancellationToken)
    {
        Task<string> head = GitBenchmarkRepository.GitAsync(
            repository.Path,
            cancellationToken,
            "rev-parse",
            "HEAD");
        Task<string> status = GitBenchmarkRepository.GitAsync(
            repository.Path,
            cancellationToken,
            "status",
            "--porcelain=v1",
            "--untracked-files=all");
        Task<string> refs = GitBenchmarkRepository.GitAsync(
            repository.Path,
            cancellationToken,
            "show-ref",
            "--head",
            "--dereference");
        Task<string> objects = GitBenchmarkRepository.GitAsync(
            repository.Path,
            cancellationToken,
            "count-objects",
            "-v");
        await Task.WhenAll(head, status, refs, objects).ConfigureAwait(false);
        return new LightweightRepositoryState(
            repository.Id,
            $"{await head.ConfigureAwait(false)}\0{await status.ConfigureAwait(false)}",
            $"{await refs.ConfigureAwait(false)}\0{await objects.ConfigureAwait(false)}");
    }

    private static void ValidateDescriptor(
        PreparedFixtureDescriptor descriptor,
        string parameterName)
    {
        if (!string.Equals(
                descriptor.SchemaVersion,
                DescriptorSchemaVersion,
                StringComparison.Ordinal) ||
            descriptor.FilesPerRepository <= 0 ||
            descriptor.ContextProjectsPerRepository < 0 ||
            descriptor.LinesPerFile <= 0 ||
            descriptor.QualifyingCommitsPerRepository < 3 ||
            descriptor.HeadFileCount <= 0 ||
            descriptor.HeadDirectoryCount <= 0 ||
            string.IsNullOrWhiteSpace(descriptor.SharedObjectId) ||
            descriptor.Manifest.Repositories.Count != 2 ||
            descriptor.Manifest.Contributors.Count != 3 ||
            descriptor.Manifest.Repositories.Any(repository => repository.Heads.Count != 4) ||
            !descriptor.Manifest.Contributors.Any(contributor =>
                string.Equals(contributor.Id, "contributor-empty", StringComparison.Ordinal)))
        {
            throw new ArgumentException(
                "Prepared fixture descriptor does not match the supported author-period benchmark shape.",
                parameterName);
        }
    }

    private static string ResolveRepositoryPath(string rootPath, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException(
                "Prepared fixture repository paths must be relative to the descriptor.");
        }

        string path = Path.GetFullPath(Path.Combine(
            rootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        string relative = Path.GetRelativePath(rootPath, path);
        if (Path.IsPathRooted(relative) ||
            relative.Equals("..", StringComparison.Ordinal) ||
            relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Prepared fixture repository paths must stay inside the descriptor directory.");
        }

        return path;
    }

    private sealed record PreparedFixtureDescriptor
    {
        public required string SchemaVersion { get; init; }

        public required int FilesPerRepository { get; init; }

        public required int ContextProjectsPerRepository { get; init; }

        public required int LinesPerFile { get; init; }

        public ChangeBenchmarkEditShape EditShape { get; init; }

        public required int QualifyingCommitsPerRepository { get; init; }

        public required int HeadFileCount { get; init; }

        public required int HeadDirectoryCount { get; init; }

        public required string SharedObjectId { get; init; }

        public required ChangeAuthorPeriodManifest Manifest { get; init; }
    }

    private sealed record LightweightRepositoryState(
        string Id,
        string Worktree,
        string Git);
}
