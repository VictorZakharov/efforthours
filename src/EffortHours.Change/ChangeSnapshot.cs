using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed record ChangeSnapshotFile
{
    public required string Path { get; init; }

    public required string ObjectId { get; init; }

    public required long Length { get; init; }

    public required string Mode { get; init; }

    public bool IsLink => string.Equals(Mode, "120000", StringComparison.Ordinal);

    public bool IsSubmodule => string.Equals(Mode, "160000", StringComparison.Ordinal);
}

public interface IChangeSnapshot : IAsyncDisposable
{
    public string ObjectId { get; }

    public string RootPath { get; }

    public IRepositoryFileSystem FileSystem { get; }

    public IReadOnlyList<ChangeSnapshotFile> Files { get; }

    public ValueTask<byte[]> ReadAllBytesAsync(
        string relativePath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A change snapshot whose repository analysis was completed while the snapshot
/// identity was established. Consumers can reuse the frozen evidence instead of
/// traversing the backing source again.
/// </summary>
public interface IRepositoryEvidenceChangeSnapshot : IChangeSnapshot
{
    public RepositoryEvidence Evidence { get; }

    /// <summary>
    /// Indicates whether source bodies can be read for content-level normalization.
    /// Serialized repository evidence carries hashes and measurements, but no bodies.
    /// </summary>
    public bool SupportsSourceReads { get; }
}

public sealed record ChangeComponentInput
{
    public ChangeComponentKind Kind { get; init; } = ChangeComponentKind.Commit;

    public required string Selector { get; init; }

    public required string BaseObjectId { get; init; }

    public required string HeadObjectId { get; init; }

    public required Func<CancellationToken, Task<IChangeSnapshot>> OpenBaseAsync { get; init; }

    public required Func<CancellationToken, Task<IChangeSnapshot>> OpenHeadAsync { get; init; }
}
