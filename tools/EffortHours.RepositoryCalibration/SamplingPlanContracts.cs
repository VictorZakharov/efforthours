using System.Text.Json.Serialization;

namespace EffortHours.RepositoryCalibration;

internal sealed record SamplingPlan
{
    public required string SamplingPlanVersion { get; init; }

    public required string Id { get; init; }

    public required string Version { get; init; }

    public required string Profile { get; init; }

    public required SamplingSizeMetric SizeMetric { get; init; }

    public IReadOnlyList<string> RequiredShapeTags { get; init; } = [];

    public IReadOnlyList<SamplingFamily> Families { get; init; } = [];
}

internal sealed record SamplingSizeMetric
{
    public required string Id { get; init; }

    public IReadOnlyList<string> EligibleExtensions { get; init; } = [];

    public IReadOnlyList<string> ExcludedPathSegments { get; init; } = [];

    public IReadOnlyList<string> ExcludedPathSequences { get; init; } = [];
}

internal sealed record SamplingFamily
{
    public required string Id { get; init; }

    public required string RepositoryName { get; init; }

    public required string RepositoryUrl { get; init; }

    public required string PrimaryStratum { get; init; }

    public required string Partition { get; init; }

    public required string ProductShape { get; init; }

    public IReadOnlyList<string> ShapeTags { get; init; } = [];

    public required SamplingSourceSnapshot SourceSnapshot { get; init; }

    public required SamplingLicense License { get; init; }

    public required SamplingSize Size { get; init; }
}

internal sealed record SamplingSourceSnapshot
{
    public required string CommitSha { get; init; }

    public required string GitTreeSha1 { get; init; }

    public required bool TreeListingComplete { get; init; }

    public required string ArchiveUrl { get; init; }
}

internal sealed record SamplingLicense
{
    public required string Expression { get; init; }

    public required string Path { get; init; }

    public required string GitBlobSha1 { get; init; }

    public required string ContentSha256 { get; init; }

    public required bool RedistributionAllowed { get; init; }
}

internal sealed record SamplingSize
{
    public required string Metric { get; init; }

    public required int EligibleFiles { get; init; }

    public required long EligibleBytes { get; init; }

    public required string Band { get; init; }
}

internal sealed record GitCommitResponse
{
    public required GitCommitTree Tree { get; init; }
}

internal sealed record GitCommitTree
{
    public required string Sha { get; init; }
}

internal sealed record GitTreeResponse
{
    public required string Sha { get; init; }

    public required bool Truncated { get; init; }

    public IReadOnlyList<GitTreeEntry> Tree { get; init; } = [];
}

internal sealed record GitTreeEntry
{
    public required string Path { get; init; }

    public required string Mode { get; init; }

    public required string Type { get; init; }

    public required string Sha { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? Size { get; init; }
}

internal sealed record GitBlobResponse
{
    public required string Sha { get; init; }

    public required string Encoding { get; init; }

    public required string Content { get; init; }

    public required long Size { get; init; }
}
