namespace Fairbill.Analysis;

public sealed record RepositoryScanOptions
{
    public bool RespectGitIgnore { get; init; } = true;

    public bool RespectFairbillIgnore { get; init; } = true;

    public string? CachePath { get; init; }

    public int FileReadBufferSize { get; init; } = 64 * 1024;

    public int TextSampleSize { get; init; } = 8 * 1024;
}
