using EffortHours.Analysis;
using EffortHours.Contracts.V1;
using EffortHours.Core;

namespace EffortHours.Change;

/// <summary>
/// Resolves directory pairs or serialized repository-evidence pairs into the
/// provider-neutral Change EHE input contract without consulting Git or GitHub.
/// </summary>
public sealed class NonGitChangePlanner
{
    private readonly IRepositoryFileSystem _fileSystem;
    private readonly IRepositoryScanner _scanner;

    public NonGitChangePlanner()
        : this(PhysicalRepositoryFileSystem.Instance)
    {
    }

    public NonGitChangePlanner(IRepositoryFileSystem fileSystem)
        : this(
            fileSystem,
            new RepositoryAnalysisPipeline(
                fileSystem ?? throw new ArgumentNullException(nameof(fileSystem))))
    {
    }

    internal NonGitChangePlanner(
        IRepositoryFileSystem fileSystem,
        IRepositoryScanner scanner)
    {
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
    }

    public async Task<ChangeEstimateInput> PlanDirectoriesAsync(
        string baseDirectory,
        string headDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(headDirectory);
        string baseRoot = _fileSystem.GetFullPath(baseDirectory);
        string headRoot = _fileSystem.GetFullPath(headDirectory);

        RepositoryEvidence baseEvidence = await _scanner.ScanAsync(
            baseRoot,
            new RepositoryScanOptions { CachePath = null },
            cancellationToken).ConfigureAwait(false);
        RepositoryEvidence headEvidence = await _scanner.ScanAsync(
            headRoot,
            new RepositoryScanOptions { CachePath = null },
            cancellationToken).ConfigureAwait(false);
        ChangeSnapshotInventory baseInventory = ChangeSnapshotInventoryBuilder.Build(baseEvidence);
        ChangeSnapshotInventory headInventory = ChangeSnapshotInventoryBuilder.Build(headEvidence);

        return CreateInput(
            baseEvidence.Repository.Name,
            headEvidence.Repository.Name,
            ChangeSnapshotKind.Directory,
            baseInventory,
            headInventory,
            cancellationToken => OpenDirectoryAsync(
                baseRoot,
                baseEvidence,
                baseInventory,
                cancellationToken),
            cancellationToken => OpenDirectoryAsync(
                headRoot,
                headEvidence,
                headInventory,
                cancellationToken),
            new Diagnostic
            {
                Code = "FB5110",
                Severity = DiagnosticSeverity.Information,
                Message = "Directory snapshots were statically scanned, bounded to admitted files, and " +
                    "pinned to content-derived identities without consulting Git history.",
            });
    }

    public static ChangeEstimateInput PlanEvidence(
        RepositoryEvidence baseEvidence,
        RepositoryEvidence headEvidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseEvidence);
        ArgumentNullException.ThrowIfNull(headEvidence);
        cancellationToken.ThrowIfCancellationRequested();
        ChangeSnapshotInventory baseInventory = ChangeSnapshotInventoryBuilder.Build(baseEvidence);
        ChangeSnapshotInventory headInventory = ChangeSnapshotInventoryBuilder.Build(headEvidence);

        return CreateInput(
            baseEvidence.Repository.Name,
            headEvidence.Repository.Name,
            ChangeSnapshotKind.Evidence,
            baseInventory,
            headInventory,
            cancellationToken => OpenEvidenceAsync(baseEvidence, baseInventory, cancellationToken),
            cancellationToken => OpenEvidenceAsync(headEvidence, headInventory, cancellationToken),
            new Diagnostic
            {
                Code = "FB5111",
                Severity = DiagnosticSeverity.Warning,
                Message = "Serialized evidence snapshots do not contain source bodies. Modified maintained " +
                    "paths that otherwise qualify as represented retain one conservative edit region because " +
                    "formatting-only and detailed edit-region analysis are unavailable.",
            });
    }

    private static ChangeEstimateInput CreateInput(
        string baseName,
        string headName,
        ChangeSnapshotKind snapshotKind,
        ChangeSnapshotInventory baseInventory,
        ChangeSnapshotInventory headInventory,
        Func<CancellationToken, Task<IChangeSnapshot>> openBaseAsync,
        Func<CancellationToken, Task<IChangeSnapshot>> openHeadAsync,
        Diagnostic diagnostic) => new()
        {
            RepositoryName = headName,
            Selection = new ChangeSelection
            {
                Kind = ChangeSelectionKind.BaseHead,
                Base = Reference(snapshotKind, baseName, baseInventory.ObjectId),
                Head = Reference(snapshotKind, headName, headInventory.ObjectId),
            },
            OpenBaseAsync = openBaseAsync,
            OpenHeadAsync = openHeadAsync,
            Diagnostics = [diagnostic],
        };

    private static ChangeSnapshotReference Reference(
        ChangeSnapshotKind kind,
        string repositoryName,
        string objectId) => new()
        {
            Selector = $"{Kebab(kind)}:{repositoryName}",
            ObjectId = objectId,
            Kind = kind,
        };

    private Task<IChangeSnapshot> OpenDirectoryAsync(
        string rootPath,
        RepositoryEvidence evidence,
        ChangeSnapshotInventory inventory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IChangeSnapshot>(new DirectoryChangeSnapshot(
            rootPath,
            _fileSystem,
            evidence,
            inventory));
    }

    private static Task<IChangeSnapshot> OpenEvidenceAsync(
        RepositoryEvidence evidence,
        ChangeSnapshotInventory inventory,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IChangeSnapshot>(new EvidenceChangeSnapshot(evidence, inventory));
    }

    private static string Kebab<T>(T value) where T : struct, Enum =>
        string.Concat(value.ToString().Select((character, index) =>
            char.IsUpper(character) && index > 0
                ? $"-{char.ToLowerInvariant(character)}"
                : char.ToLowerInvariant(character).ToString()));
}
