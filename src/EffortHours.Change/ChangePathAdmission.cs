using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts;

namespace EffortHours.Change;

public sealed class ChangePathAdmission
{
    private readonly Func<string, bool> _admits;

    internal ChangePathAdmission(string profileDigest, Func<string, bool> admits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileDigest);
        _admits = admits ?? throw new ArgumentNullException(nameof(admits));
        ProfileDigest = profileDigest;
    }

    public string ProfileDigest { get; }

    public bool Admits(string path)
    {
        ChangeSnapshotInventoryBuilder.ValidateRelativePath(path);
        return _admits(path);
    }

    /// <summary>
    /// Composes reviewed path exclusion with an optional scope admission. Without exclusion the original
    /// admission (or none) is returned unchanged, so unexcluded repositories keep every digest and cache key.
    /// </summary>
    public static ChangePathAdmission? WithExclusion(ChangePathAdmission? scope, IReadOnlyList<string>? exclude)
    {
        RepositoryPathExclusion? exclusion = RepositoryPathExclusion.Create(exclude);
        if (exclusion is null) return scope;
        string digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            "efforthours:path-admission-exclusion:1\n" + (scope?.ProfileDigest ?? "all") + "\n" + exclusion.Digest)));
        return new ChangePathAdmission(digest, path => !exclusion.Excludes(path) && (scope?._admits(path) ?? true))
        {
            Exclusion = exclusion,
        };
    }

    /// <summary>The reviewed exclusion this admission applies, if any.</summary>
    public RepositoryPathExclusion? Exclusion { get; private init; }

    /// <summary>Fails loudly when exclusion leaves a pinned head with no admitted file, rather than estimating zero.</summary>
    public static async Task RequireAdmittedHeadsAsync(GitClient git, string repositoryPath,
        IEnumerable<string> headObjectIds, ChangePathAdmission? admission, CancellationToken cancellationToken)
    {
        if (admission?.Exclusion is not { } exclusion) return;
        foreach (string head in headObjectIds)
        {
            await using IChangeSnapshot snapshot = await git.OpenSnapshotAsync(repositoryPath, head, cancellationToken)
                .ConfigureAwait(false);
            if (!snapshot.Files.Any(file => !exclusion.Excludes(file.Path)))
                throw new InvalidDataException(
                    "Path exclusion admits no file at a pinned head; narrow 'exclude' rather than estimating zero.");
        }
    }
}
