using System.Text.Json;
using EffortHours.Analysis;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Review;

namespace EffortHours.Cli;

internal sealed record RepositoryInputSelection
{
    public string? InputPath { get; init; }
    public string? GitHubRepository { get; init; }
    public string Revision { get; init; } = "HEAD";
    public bool FetchMissing { get; init; }
    public string? VendorManifestPath { get; init; }
    public bool IsRemote => GitHubRepository is not null;
}

internal sealed class RepositoryInputOptionsBuilder
{
    private string? _githubRepository;
    private string? _revision;
    private bool _fetchMissing;
    private string? _vendorManifest;

    public RepositoryInputOptionsBuilder(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        InputPath = arguments.Length > 0 && !arguments[0].StartsWith('-')
            ? arguments[0]
            : null;
        FirstOptionIndex = InputPath is null ? 0 : 1;
    }

    public string? InputPath { get; }
    public int FirstOptionIndex { get; }

    public bool TryConsume(string[] arguments, ref int index, out string? error)
    {
        error = null;
        switch (arguments[index])
        {
            case "--fetch-missing":
                if (_fetchMissing)
                {
                    error = "Option '--fetch-missing' cannot be repeated.";
                }

                _fetchMissing = true;
                return true;
            case "--vendor-manifest":
                return TryConsumeValue(arguments, ref index, "--vendor-manifest", ref _vendorManifest, out error);
            case "--repo":
                return TryConsumeValue(arguments, ref index, "--repo", ref _githubRepository, out error);
            case "--revision":
                return TryConsumeValue(arguments, ref index, "--revision", ref _revision, out error);
            default:
                return false;
        }
    }

    public bool TryBuild(out RepositoryInputSelection? selection, out string? error)
    {
        selection = null;
        error = null;
        if (InputPath is null && _githubRepository is null)
        {
            error = "Supply a repository or evidence path, or use --repo <owner/name>.";
            return false;
        }

        if (InputPath is not null && _githubRepository is not null)
        {
            error = "A positional repository or evidence path cannot be combined with --repo.";
            return false;
        }

        if (_githubRepository is null && (_revision is not null || _fetchMissing))
        {
            error = "Options '--revision' and '--fetch-missing' are valid only with --repo.";
            return false;
        }

        selection = new RepositoryInputSelection
        {
            InputPath = InputPath,
            VendorManifestPath = _vendorManifest,
            GitHubRepository = _githubRepository,
            Revision = _revision ?? "HEAD",
            FetchMissing = _fetchMissing,
        };
        return true;
    }

    private static bool TryConsumeValue(
        string[] arguments,
        ref int index,
        string option,
        ref string? target,
        out string? error)
    {
        error = null;
        if (target is not null)
        {
            error = $"Option '{option}' cannot be repeated.";
            return true;
        }

        if (index + 1 >= arguments.Length)
        {
            error = $"Option '{option}' requires a value.";
            return true;
        }

        target = arguments[++index];
        if (string.IsNullOrWhiteSpace(target))
        {
            error = $"Option '{option}' cannot be empty.";
        }

        return true;
    }
}

internal sealed class RepositoryInputContext : IAsyncDisposable
{
    private readonly IAsyncDisposable? _lease;

    public RepositoryInputContext(
        RepositoryEvidence evidence,
        HostReviewSourceContext? sourceContext,
        IAsyncDisposable? lease = null)
    {
        Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        SourceContext = sourceContext;
        _lease = lease;
    }

    public RepositoryEvidence Evidence { get; }
    public HostReviewSourceContext? SourceContext { get; }

    public async ValueTask DisposeAsync()
    {
        if (_lease is not null)
        {
            await _lease.DisposeAsync().ConfigureAwait(false);
        }
    }
}
