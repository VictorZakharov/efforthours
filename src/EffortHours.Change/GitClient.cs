using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed partial class GitClient
{
    private readonly IExternalCommandRunner _commands;
    private readonly Func<string, string, CancellationToken, Task<IChangeSnapshot>>? _snapshotFactory;

    public const string EmptyTreeObjectId = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";

    public GitClient()
    {
        _commands = new ExternalCommandRunner();
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
            cancellationToken,
            requireSuccess: false).ConfigureAwait(false);
        string root = result.ExitCode == 0
            ? result.StandardOutput.Trim()
            : await ResolveBareRepositoryRootAsync(
                fullPath,
                result,
                cancellationToken).ConfigureAwait(false);
        if (root.Length == 0)
        {
            throw new ExternalCommandException("git", result.ExitCode, "Git did not return a repository root.");
        }

        return Path.GetFullPath(root);
    }

    private async Task<string> ResolveBareRepositoryRootAsync(
        string fullPath,
        ExternalCommandResult worktreeResult,
        CancellationToken cancellationToken)
    {
        ExternalCommandResult bare = await _commands.RunAsync(
            "git",
            fullPath,
            ["rev-parse", "--is-bare-repository"],
            cancellationToken,
            requireSuccess: false).ConfigureAwait(false);
        if (bare.ExitCode != 0 ||
            !bare.StandardOutput.Trim().Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            string message = string.IsNullOrWhiteSpace(worktreeResult.StandardError)
                ? "Git did not return a repository root."
                : $"'git' failed: {worktreeResult.StandardError.Trim()}";
            throw new ExternalCommandException("git", worktreeResult.ExitCode, message);
        }

        ExternalCommandResult gitDirectory = await _commands.RunAsync(
            "git",
            fullPath,
            ["rev-parse", "--absolute-git-dir"],
            cancellationToken).ConfigureAwait(false);
        return gitDirectory.StandardOutput.Trim();
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

    public async Task<string> ResolveMergeBaseAsync(
        string repositoryPath,
        string firstObjectId,
        string secondObjectId,
        CancellationToken cancellationToken = default)
    {
        ExternalCommandResult result = await _commands.RunAsync(
            "git",
            repositoryPath,
            ["merge-base", "--all", firstObjectId, secondObjectId],
            cancellationToken,
            requireSuccess: false).ConfigureAwait(false);
        if (result.ExitCode == 0)
        {
            string[] objectIds =
            [
                .. result.StandardOutput
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(value => RequireObjectId(value, "merge base"))
                    .Distinct(StringComparer.Ordinal),
            ];
            return objectIds.Length switch
            {
                1 => objectIds[0],
                0 => throw new InvalidOperationException("Git returned no merge base for the selected pull request."),
                _ => throw new InvalidOperationException(
                    "The selected pull request has multiple merge bases. Use explicit --base and --head " +
                    "only after confirming the intended comparison base."),
            };
        }

        if (result.ExitCode == 1)
        {
            throw new InvalidOperationException(
                "The selected pull-request base and head have no common ancestor in the local Git database.");
        }

        throw new ExternalCommandException(
            "git",
            result.ExitCode,
            $"Git could not resolve the pull-request merge base: {result.StandardError.Trim()}");
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

        string[] parents = [.. values.Skip(1).Select(value => value.ToLowerInvariant())];
        RememberFirstParent(
            repositoryPath,
            commitObjectId,
            parents.Length == 0 ? null : parents[0]);
        return parents;
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

}
