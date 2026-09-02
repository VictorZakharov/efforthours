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
    public bool IsRemote => GitHubRepository is not null;
}

internal sealed class RepositoryInputOptionsBuilder
{
    private string? _githubRepository;
    private string? _revision;
    private bool _fetchMissing;

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

internal sealed class RepositoryInputLoader
{
    private readonly IRepositoryScanner _localScanner;
    private readonly Func<IRepositoryFileSystem, IRepositoryScanner> _scannerFactory;
    private readonly ManagedGitQueryPlanner _managedPlanner;
    private readonly GitClient _git;

    public RepositoryInputLoader(IRepositoryScanner localScanner)
        : this(
            localScanner,
            fileSystem => new RepositoryAnalysisPipeline(fileSystem),
            new ManagedGitQueryPlanner(),
            new GitClient())
    {
    }

    internal RepositoryInputLoader(
        IRepositoryScanner localScanner,
        Func<IRepositoryFileSystem, IRepositoryScanner> scannerFactory,
        ManagedGitQueryPlanner managedPlanner,
        GitClient git)
    {
        _localScanner = localScanner ?? throw new ArgumentNullException(nameof(localScanner));
        _scannerFactory = scannerFactory ?? throw new ArgumentNullException(nameof(scannerFactory));
        _managedPlanner = managedPlanner ?? throw new ArgumentNullException(nameof(managedPlanner));
        _git = git ?? throw new ArgumentNullException(nameof(git));
    }

    public async Task<RepositoryInputContext> LoadAsync(
        RepositoryInputSelection selection,
        bool allowEvidenceFile,
        RepositoryScanOptions? scanOptions,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (selection.IsRemote)
        {
            return await LoadRemoteAsync(selection, scanOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        string inputPath = selection.InputPath!;
        if (Directory.Exists(inputPath))
        {
            string root = Path.GetFullPath(inputPath);
            RepositoryEvidence evidence = await _localScanner.ScanAsync(
                root,
                scanOptions,
                cancellationToken).ConfigureAwait(false);
            return new RepositoryInputContext(
                evidence,
                new HostReviewSourceContext(root, PhysicalRepositoryFileSystem.Instance));
        }

        if (allowEvidenceFile && File.Exists(inputPath))
        {
            RepositoryEvidence evidence = await LoadEvidenceFileAsync(inputPath, cancellationToken)
                .ConfigureAwait(false);
            return new RepositoryInputContext(evidence, sourceContext: null);
        }

        string description = allowEvidenceFile ? "Repository or evidence path" : "Repository directory";
        throw new FileNotFoundException($"{description} was not found: {inputPath}");
    }

    private async Task<RepositoryInputContext> LoadRemoteAsync(
        RepositoryInputSelection selection,
        RepositoryScanOptions? scanOptions,
        CancellationToken cancellationToken)
    {
        ManagedRepositoryHead head = await _managedPlanner.PrepareHeadAsync(
            selection.GitHubRepository!,
            selection.Revision,
            selection.FetchMissing,
            cancellationToken).ConfigureAwait(false);
        IChangeSnapshot snapshot = await _git.OpenSnapshotAsync(
            head.RepositoryPath,
            head.ObjectId,
            cancellationToken).ConfigureAwait(false);
        try
        {
            IRepositoryScanner scanner = _scannerFactory(snapshot.FileSystem);
            RepositoryEvidence scanned = await scanner.ScanAsync(
                snapshot.RootPath,
                scanOptions,
                cancellationToken).ConfigureAwait(false);
            string repositoryName = selection.GitHubRepository!
                .Split('/', StringSplitOptions.RemoveEmptyEntries)[^1]
                .ToLowerInvariant();
            RepositoryEvidence evidence = RemoteRepositoryEvidence.Normalize(
                scanned,
                repositoryName,
                new Diagnostic
                {
                    Code = "FB5108",
                    Severity = DiagnosticSeverity.Information,
                    Message = RemoteDiagnosticMessage(head),
                });
            return new RepositoryInputContext(
                evidence,
                new HostReviewSourceContext(snapshot.RootPath, snapshot.FileSystem),
                snapshot);
        }
        catch
        {
            await snapshot.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<RepositoryEvidence> LoadEvidenceFileAsync(
        string inputPath,
        CancellationToken cancellationToken)
    {
        string json = await File.ReadAllTextAsync(inputPath, cancellationToken).ConfigureAwait(false);
        SchemaValidationResult schemaResult = ContractSchemaValidator.Validate(
            SchemaNames.RepositoryEvidence,
            json);
        if (!schemaResult.IsValid)
        {
            throw new InvalidDataException(
                "Evidence does not satisfy the repository evidence schema:\n- " +
                string.Join("\n- ", schemaResult.Errors));
        }

        RepositoryEvidence evidence;
        try
        {
            evidence = ContractJson.Deserialize<RepositoryEvidence>(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Could not deserialize evidence: {exception.Message}", exception);
        }

        IReadOnlyList<string> semanticErrors = ContractValidation.Validate(evidence);
        if (semanticErrors.Count > 0)
        {
            throw new InvalidDataException(
                "Evidence is semantically invalid:\n- " + string.Join("\n- ", semanticErrors));
        }

        return evidence;
    }

    private static string RemoteDiagnosticMessage(ManagedRepositoryHead head) => head.Fetched
        ? "Checkout-free --fetch-missing repository analysis populated the private EffortHours bare object cache with the exact provider-resolved immutable commit; no checkout, user ref, FETCH_HEAD, index, or worktree was created or changed."
        : head.ProviderResolved
            ? "Checkout-free --fetch-missing repository analysis refreshed the immutable provider identity and reused its objects from the private EffortHours bare cache; no checkout, user ref, FETCH_HEAD, index, or worktree was created or changed."
            : "Checkout-free repository analysis reused a provider-resolved immutable commit from the private EffortHours bare cache without provider or network access.";
}
