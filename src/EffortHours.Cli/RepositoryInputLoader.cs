using System.Text.Json;
using EffortHours.Analysis;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Review;

namespace EffortHours.Cli;

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
        if (selection.VendorManifestPath is not null)
        {
            if (!selection.IsRemote && !Directory.Exists(selection.InputPath))
                throw new InvalidDataException("--vendor-manifest requires a repository scan; saved evidence cannot be reclassified. Rescan its source.");
            scanOptions = (scanOptions ?? new RepositoryScanOptions()) with
            {
                VendorManifest = await ReviewedVendorManifestLoader.LoadAsync(selection.VendorManifestPath, cancellationToken).ConfigureAwait(false),
            };
        }
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
