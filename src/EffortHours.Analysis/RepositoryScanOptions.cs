using EffortHours.Contracts.V1;

namespace EffortHours.Analysis;

public sealed record RepositoryScanOptions
{
    public bool RespectGitIgnore { get; init; } = true;

    public bool RespectEffortHoursIgnore { get; init; } = true;

    public string? CachePath { get; init; }

    public ReviewedVendorManifest? VendorManifest { get; init; }

    /// <summary>
    /// Repository-relative files admitted only as analyzer context (snapshot area scans carry ancestor Unreal
    /// descriptors this way). Their file facts are tagged <c>scope:context-only</c> and analyzers must not value them.
    /// </summary>
    public IReadOnlySet<string>? ContextOnlyPaths { get; init; }

    public int FileReadBufferSize { get; init; } = 64 * 1024;

    public int TextSampleSize { get; init; } = 8 * 1024;
}
