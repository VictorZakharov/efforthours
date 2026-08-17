using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.ChangeBenchmarks;

internal sealed record PortfolioBenchmarkRepository(
    string Id,
    string Path,
    IReadOnlyList<ChangeAuthorPeriodManifestHead> Heads);

internal sealed partial class GitPortfolioBenchmarkFixture : IDisposable
{
    public const string ContributorAName = "Benchmark Contributor A";
    public const string ContributorAEmail = "contributor-a@example.invalid";
    public const string ContributorBName = "Benchmark Contributor B";
    public const string ContributorBEmail = "contributor-b@example.invalid";

    private const int FanoutBranches = 4;
    private readonly bool _keep;

    private GitPortfolioBenchmarkFixture(
        string rootPath,
        ChangeAuthorPeriodManifest manifest,
        IReadOnlyList<PortfolioBenchmarkRepository> repositories,
        int headFileCount,
        int headDirectoryCount,
        string sharedObjectId,
        bool keep)
    {
        RootPath = rootPath;
        Manifest = manifest;
        Repositories = repositories;
        HeadFileCount = headFileCount;
        HeadDirectoryCount = headDirectoryCount;
        SharedObjectId = sharedObjectId;
        _keep = keep;
    }

    public string RootPath { get; }

    public ChangeAuthorPeriodManifest Manifest { get; }

    public IReadOnlyList<PortfolioBenchmarkRepository> Repositories { get; }

    public IReadOnlyDictionary<string, string> RepositoryPaths => Repositories.ToDictionary(
        repository => repository.Id,
        repository => repository.Path,
        StringComparer.Ordinal);

    public int HeadFileCount { get; }

    public int HeadDirectoryCount { get; }

    public string SharedObjectId { get; }

    public int HeadCount => Repositories.Sum(repository => repository.Heads.Count);

    public int ContributorCount => Manifest.Contributors.Count;

    public static async Task<GitPortfolioBenchmarkFixture> CreateAsync(
        ChangeBenchmarkOptions options,
        CancellationToken cancellationToken)
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "efforthours-change-portfolio-benchmark",
            Guid.NewGuid().ToString("N"));
        string repositoryAPath = Path.Combine(root, "repository-a");
        string repositoryBPath = Path.Combine(root, "repository-b");
        Directory.CreateDirectory(repositoryAPath);
        try
        {
            await InitializeRepositoryAsync(repositoryAPath, cancellationToken).ConfigureAwait(false);
            WriteBaseTree(repositoryAPath, options, cancellationToken);
            string baseObjectId = await GitBenchmarkRepository.CommitAsync(
                repositoryAPath,
                "base tree",
                cancellationToken).ConfigureAwait(false);
            _ = await CreateMergeFanoutAsync(
                repositoryAPath,
                baseObjectId,
                cancellationToken).ConfigureAwait(false);
            string shared = await SelectedCommitAsync(
                repositoryAPath,
                options,
                finalValue: 1,
                "repository-scoped shared-object change",
                ContributorAName,
                ContributorAEmail,
                "2026-08-10T00:00:00+00:00",
                cancellationToken).ConfigureAwait(false);
            string overlap = await UnselectedHeadAsync(
                repositoryAPath,
                shared,
                "fully-overlapping-head",
                cancellationToken).ConfigureAwait(false);
            await GitBenchmarkRepository.GitAsync(
                repositoryAPath,
                cancellationToken,
                "switch",
                "--quiet",
                "main").ConfigureAwait(false);
            int defaultAdditional = options.Commits / 2;
            int openAdditional = options.Commits - 1 - defaultAdditional;
            string defaultA = await SelectedSeriesAsync(
                repositoryAPath,
                options,
                defaultAdditional,
                firstValue: 2,
                firstMinute: 10,
                "repository a default selected change",
                ContributorBName,
                ContributorBEmail,
                cancellationToken).ConfigureAwait(false);
            await GitBenchmarkRepository.GitAsync(
                repositoryAPath,
                cancellationToken,
                "switch",
                "--quiet",
                "--create",
                "open-head",
                shared).ConfigureAwait(false);
            string openA = await SelectedSeriesAsync(
                repositoryAPath,
                options,
                openAdditional,
                firstValue: 10_000,
                firstMinute: 1_000,
                "repository a open selected change",
                ContributorAName,
                ContributorAEmail,
                cancellationToken).ConfigureAwait(false);

            await CloneRepositoryAsync(repositoryAPath, repositoryBPath, cancellationToken)
                .ConfigureAwait(false);
            await ConfigureRepositoryAsync(repositoryBPath, cancellationToken).ConfigureAwait(false);
            await GitBenchmarkRepository.GitAsync(
                repositoryBPath,
                cancellationToken,
                "switch",
                "--quiet",
                "--create",
                "repository-b-default",
                shared).ConfigureAwait(false);
            string defaultB = await SelectedSeriesAsync(
                repositoryBPath,
                options,
                defaultAdditional,
                firstValue: 20_000,
                firstMinute: 2_000,
                "repository b default selected change",
                ContributorBName,
                ContributorBEmail,
                cancellationToken).ConfigureAwait(false);
            await GitBenchmarkRepository.GitAsync(
                repositoryBPath,
                cancellationToken,
                "switch",
                "--quiet",
                "--create",
                "repository-b-open",
                shared).ConfigureAwait(false);
            string openB = await SelectedSeriesAsync(
                repositoryBPath,
                options,
                openAdditional,
                firstValue: 30_000,
                firstMinute: 3_000,
                "repository b open selected change",
                ContributorAName,
                ContributorAEmail,
                cancellationToken).ConfigureAwait(false);

            PortfolioBenchmarkRepository[] repositories =
            [
                Repository("repository-a", repositoryAPath, shared, overlap, defaultA, openA),
                Repository("repository-b", repositoryBPath, shared, overlap, defaultB, openB),
            ];
            ChangeAuthorPeriodManifest manifest = CreateManifest(repositories);
            (int filesA, int directoriesA) = await GitBenchmarkRepository.CountHeadTreeAsync(
                repositoryAPath,
                defaultA,
                cancellationToken).ConfigureAwait(false);
            (int filesB, int directoriesB) = await GitBenchmarkRepository.CountHeadTreeAsync(
                repositoryBPath,
                defaultB,
                cancellationToken).ConfigureAwait(false);
            return new GitPortfolioBenchmarkFixture(
                root,
                manifest,
                repositories,
                filesA + filesB,
                directoriesA + directoriesB,
                shared,
                options.KeepRepository);
        }
        catch
        {
            GitBenchmarkRepository.DeleteRepository(root);
            throw;
        }
    }

    public ChangeAuthorPeriodManifest ReorderedManifest() => Manifest with
    {
        Contributors = [.. Manifest.Contributors.Reverse().Select(contributor => contributor with
        {
            Aliases = [.. contributor.Aliases.Reverse()],
        })],
        Repositories = [.. Manifest.Repositories.Reverse().Select(repository => repository with
        {
            Heads = [.. repository.Heads.Reverse()],
        })],
    };

    public RepositoryStateSnapshot CaptureWorktrees() => RepositoryStateSnapshot.Combine(
        Repositories.Select(repository => (
            repository.Id,
            RepositoryStateSnapshot.Capture(repository.Path, ".git"))));

    public RepositoryStateSnapshot CaptureGitState() => RepositoryStateSnapshot.Combine(
        Repositories.Select(repository => (
            repository.Id,
            RepositoryStateSnapshot.Capture(Path.Combine(repository.Path, ".git")))));

    public void Dispose()
    {
        if (!_keep)
        {
            GitBenchmarkRepository.DeleteRepository(RootPath);
        }
    }

    private static PortfolioBenchmarkRepository Repository(
        string id,
        string path,
        string shared,
        string overlap,
        string defaultHead,
        string openHead) => new(
            id,
            path,
            [
                new ChangeAuthorPeriodManifestHead { Id = "open", ObjectId = openHead },
                new ChangeAuthorPeriodManifestHead { Id = "fully-overlapping", ObjectId = overlap },
                new ChangeAuthorPeriodManifestHead { Id = "default", ObjectId = defaultHead },
                new ChangeAuthorPeriodManifestHead { Id = "shared", ObjectId = shared },
            ]);

    private static ChangeAuthorPeriodManifest CreateManifest(
        IReadOnlyList<PortfolioBenchmarkRepository> repositories) => new()
        {
            Selection = new ChangeAuthorPeriodManifestSelection
            {
                SinceInclusive = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
                UntilExclusive = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
                TimeZone = "UTC",
                DateField = ChangePortfolioDateField.Author,
                MergePolicy = ChangePortfolioMergePolicy.Exclude,
                CoauthorPolicy = ChangePortfolioCoauthorPolicy.Include,
            },
            Contributors =
            [
                new ChangeAuthorPeriodManifestContributor
                {
                    Id = "contributor-b",
                    Aliases = [ContributorBEmail, ContributorBName],
                },
                new ChangeAuthorPeriodManifestContributor
                {
                    Id = "contributor-empty",
                    Aliases = ["empty-contributor@example.invalid", "Empty Contributor"],
                },
                new ChangeAuthorPeriodManifestContributor
                {
                    Id = "contributor-a",
                    Aliases = [ContributorAName, ContributorAEmail],
                },
            ],
            Repositories = [.. repositories.Select(repository =>
                new ChangeAuthorPeriodManifestRepository
                {
                    Id = repository.Id,
                    RepositoryPath = repository.Path,
                    Heads = repository.Heads,
                })],
        };
}
