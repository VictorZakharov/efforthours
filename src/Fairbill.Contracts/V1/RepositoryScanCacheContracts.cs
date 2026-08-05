namespace Fairbill.Contracts.V1;

public sealed record RepositoryScanCache
{
    public string SchemaVersion { get; init; } = ContractVersions.V1;

    public required string AnalyzerVersion { get; init; }

    public required string RepositoryKey { get; init; }

    public IReadOnlyList<RepositoryScanCacheEntry> Files { get; init; } = [];
}

public sealed record RepositoryScanCacheEntry
{
    public required string Path { get; init; }

    public required long Length { get; init; }

    public required long LastWriteTimeUtcTicks { get; init; }

    public required string Sha256 { get; init; }

    public required long Bytes { get; init; }

    public required long Lines { get; init; }

    public required bool IsBinary { get; init; }

    public required string Role { get; init; }

    public string? Language { get; init; }

    public IReadOnlyList<string> Ecosystems { get; init; } = [];

    public required bool IsTest { get; init; }

    public required bool IsGenerated { get; init; }

    public required bool IsMinified { get; init; }

    public required bool IsVendored { get; init; }

    public required bool IsComponentManifest { get; init; }
}
