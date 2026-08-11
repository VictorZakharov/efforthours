using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed class GitClient
{
    private readonly IExternalCommandRunner _commands;
    private readonly Func<string, string, CancellationToken, Task<IChangeSnapshot>> _snapshotFactory;

    public const string EmptyTreeObjectId = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";

    public GitClient()
        : this(
            new ExternalCommandRunner(),
            OpenGitSnapshotAsync)
    {
    }

    internal GitClient(
        IExternalCommandRunner commands,
        Func<string, string, CancellationToken, Task<IChangeSnapshot>> snapshotFactory)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _snapshotFactory = snapshotFactory ?? throw new ArgumentNullException(nameof(snapshotFactory));
    }

    public async Task<string> ResolveRepositoryRootAsync(
        string repositoryPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        string fullPath = Path.GetFullPath(repositoryPath);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException($"Repository directory was not found: {repositoryPath}");
        }

        ExternalCommandResult result = await _commands.RunAsync(
            "git",
            fullPath,
            ["rev-parse", "--show-toplevel"],
            cancellationToken).ConfigureAwait(false);
        string root = result.StandardOutput.Trim();
        if (root.Length == 0)
        {
            throw new ExternalCommandException("git", result.ExitCode, "Git did not return a repository root.");
        }

        return Path.GetFullPath(root);
    }

    public async Task<string> ResolveCommitAsync(
        string repositoryPath,
        string revision,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        ExternalCommandResult result = await _commands.RunAsync(
            "git",
            repositoryPath,
            ["rev-parse", "--verify", $"{revision}^{{commit}}"],
            cancellationToken).ConfigureAwait(false);
        return RequireObjectId(result.StandardOutput, revision);
    }

    public async Task<bool> CommitExistsAsync(
        string repositoryPath,
        string objectId,
        CancellationToken cancellationToken = default)
    {
        ExternalCommandResult result = await _commands.RunAsync(
            "git",
            repositoryPath,
            ["cat-file", "-e", $"{objectId}^{{commit}}"],
            cancellationToken,
            requireSuccess: false).ConfigureAwait(false);
        return result.ExitCode == 0;
    }

    public async Task<IReadOnlyList<string>> GetParentsAsync(
        string repositoryPath,
        string commitObjectId,
        CancellationToken cancellationToken = default)
    {
        ExternalCommandResult result = await _commands.RunAsync(
            "git",
            repositoryPath,
            ["rev-list", "--parents", "-n", "1", commitObjectId],
            cancellationToken).ConfigureAwait(false);
        string[] values = result.StandardOutput
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values.Length == 0 || !string.Equals(values[0], commitObjectId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ExternalCommandException("git", result.ExitCode, "Git returned invalid commit-parent data.");
        }

        return [.. values.Skip(1).Select(value => value.ToLowerInvariant())];
    }

    internal async Task<IReadOnlyList<GitCommitMetadata>> ListCommitMetadataAsync(
        string repositoryPath,
        string headObjectId,
        int maximumCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);
        ExternalCommandResult result = await _commands.RunAsync(
            "git",
            repositoryPath,
            [
                "log",
                "--reverse",
                "--topo-order",
                $"--max-count={maximumCount}",
                "--format=%H%x00%P%x00%an%x00%ae%x00%aI%x00%cn%x00%ce%x00%cI%x00%(trailers:key=Co-authored-by,valueonly,separator=%x1f)%x00",
                headObjectId,
            ],
            cancellationToken).ConfigureAwait(false);
        return GitCommitMetadataParser.Parse(result.StandardOutput);
    }

    public async Task EnsureAncestorAsync(
        string repositoryPath,
        string baseObjectId,
        string headObjectId,
        CancellationToken cancellationToken = default)
    {
        ExternalCommandResult result = await _commands.RunAsync(
            "git",
            repositoryPath,
            ["merge-base", "--is-ancestor", baseObjectId, headObjectId],
            cancellationToken,
            requireSuccess: false).ConfigureAwait(false);
        if (result.ExitCode == 0)
        {
            return;
        }

        if (result.ExitCode == 1)
        {
            throw new InvalidOperationException(
                "The selected range base is not an ancestor of its head. Use explicit base/head selection " +
                "only after confirming the intended comparison.");
        }

        throw new ExternalCommandException(
            "git",
            result.ExitCode,
            $"Git could not test range ancestry: {result.StandardError.Trim()}");
    }

    public Task<IReadOnlyList<string>> ListRangeCommitsAsync(
        string repositoryPath,
        string baseObjectId,
        string headObjectId,
        CancellationToken cancellationToken = default)
    {
        return ListRangeCommitsCoreAsync(
            repositoryPath,
            baseObjectId,
            headObjectId,
            maximumCount: null,
            cancellationToken);
    }

    public Task<IReadOnlyList<string>> ListRangeCommitsAsync(
        string repositoryPath,
        string baseObjectId,
        string headObjectId,
        int maximumCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);
        return ListRangeCommitsCoreAsync(
            repositoryPath,
            baseObjectId,
            headObjectId,
            maximumCount,
            cancellationToken);
    }

    private async Task<IReadOnlyList<string>> ListRangeCommitsCoreAsync(
        string repositoryPath,
        string baseObjectId,
        string headObjectId,
        int? maximumCount,
        CancellationToken cancellationToken)
    {
        List<string> arguments = ["rev-list", "--reverse", "--topo-order"];
        if (maximumCount is not null)
        {
            arguments.Add($"--max-count={maximumCount.Value}");
        }

        arguments.Add($"{baseObjectId}..{headObjectId}");
        ExternalCommandResult result = await _commands.RunAsync(
            "git",
            repositoryPath,
            arguments,
            cancellationToken).ConfigureAwait(false);
        return [.. result.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.ToLowerInvariant())];
    }

    public async Task<IChangeSnapshot> OpenSnapshotAsync(
        string repositoryPath,
        string objectId,
        CancellationToken cancellationToken = default) =>
        await _snapshotFactory(repositoryPath, objectId, cancellationToken).ConfigureAwait(false);

    public static ChangeSnapshotReference Reference(
        string selector,
        string objectId,
        ChangeSnapshotKind kind = ChangeSnapshotKind.GitCommit) => new()
        {
            Selector = selector,
            ObjectId = objectId,
            Kind = kind,
        };

    private static string RequireObjectId(string value, string revision)
    {
        string objectId = value.Trim().ToLowerInvariant();
        if (objectId.Length is not (40 or 64) || objectId.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidOperationException(
                $"Git returned an invalid object ID while resolving revision '{revision}'.");
        }

        return objectId;
    }

    private static async Task<IChangeSnapshot> OpenGitSnapshotAsync(
        string repositoryPath,
        string objectId,
        CancellationToken cancellationToken) =>
        await GitSnapshotFileSystem.CreateAsync(
            repositoryPath,
            objectId,
            cancellationToken).ConfigureAwait(false);
}
