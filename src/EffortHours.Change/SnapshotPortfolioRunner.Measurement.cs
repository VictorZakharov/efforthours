using EffortHours.Analysis;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;

namespace EffortHours.Change;

public sealed partial class SnapshotPortfolioRunner
{
    private async Task<SnapshotStoredBinding> MeasureArchiveAsync(GitClient git, string root, string commit, IReadOnlyList<ChangeSnapshotFile> inventory,
        SnapshotProjectDefinition project, MeasurementIdentity identity, string bindingKey,
        string treeObjectId, bool requestAreas, SnapshotAreaSelectors? selectors, SnapshotPortfolioRunOptions options, CancellationToken token)
    {
        GitArchiveSnapshot archive;
        try
        {
            archive = await git.OpenArchiveAsync(root, commit, maximumBytes: options.MaximumArchiveBytes,
            cancellationToken: token).ConfigureAwait(false);
        }
        catch (Exception e) when (e is InvalidDataException or IOException or ExternalCommandException)
        { throw new SnapshotPlanningException("invalid-archive", e.Message, inner: e); }
        Interlocked.Increment(ref _exports);
        Interlocked.Add(ref _gitReadBytes, archive.Files.Sum(f => f.Value.LongLength));
        // Persist data-only artifacts under all configuration bytes and the complete path set.
        string context = SnapshotMeasurementIdentity.Digest(new
        {
            identity,
            Paths = archive.Files.Keys.Order(StringComparer.Ordinal),
            Controls = archive.Files.Where(p => IsContextControl(p.Key)).OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new { p.Key, Digest = SnapshotMeasurementIdentity.Hash(p.Value) }),
        });
        PhysicalRepositoryAnalysisArtifactStore persistent = new(Path.Combine(store.DirectoryPath, "artifacts", project.Id), context);
        RepositoryAnalysisArtifactCache artifacts = new(store: persistent);
        archive = new GitArchiveSnapshot(archive.Files, artifacts);
        SnapshotMeasurementReceipt whole = await MeasureAsync(archive, archive.Files.Count, 0, identity,
            project.VendorManifest, options, token).ConfigureAwait(false);
        string wholeKey = BindingKey(commit, identity, "");
        SnapshotStoredBinding wholeBinding = new(wholeKey, "", whole.Id, []);
        wholeBinding = wholeBinding with { Digest = SnapshotPortfolioStore.BindingDigest(wholeBinding) };
        await store.SaveAsync("bindings", wholeKey, wholeBinding, token).ConfigureAwait(false);
        bool attributes = inventory.Any(f => Path.GetFileName(f.Path) == ".gitattributes");
        string wholeTreeKey = BindingKey(treeObjectId + (attributes ? commit : ""), identity, "");
        wholeBinding = wholeBinding with { Key = wholeTreeKey, Digest = "" };
        wholeBinding = wholeBinding with { Digest = SnapshotPortfolioStore.BindingDigest(wholeBinding) };
        await store.SaveAsync("trees", wholeTreeKey, wholeBinding, token).ConfigureAwait(false);
        IReadOnlyList<SnapshotAreaInput> inputs = requestAreas ? SnapshotAreaPartition.Partition(archive, project.Areas, cancellationToken: token, selectors: selectors) : [];
        List<SnapshotMeasurementReceipt> measurements = [];
        foreach (SnapshotAreaInput area in inputs)
        {
            ReviewedVendorManifest? vendor = project.VendorManifest is null ? null : project.VendorManifest with
            {
                Files = [.. project.VendorManifest.Files.Where(f => area.Snapshot.Files.ContainsKey(f.Path))],
            };
            // Empty ownership subsets are omitted from scanner input but remain in measurement identity.
            if (vendor?.Files.Count == 0) vendor = null;
            measurements.Add(await MeasureAsync(area.Snapshot, area.OwnedFiles, area.ContextFiles, identity, vendor, options, token).ConfigureAwait(false));
        }
        IReadOnlyList<decimal> allocated = requestAreas ? SnapshotAreaPartition.Allocate(whole.Hours.Expected, [.. measurements.Select(m => m.Hours.Expected)]) : [];
        List<SnapshotAreaResult> results = [];
        for (int i = 0; i < inputs.Count; i++) results.Add(new()
        {
            Id = inputs[i].Id,
            ReceiptId = measurements[i].Id,
            StandaloneExpectedHours = measurements[i].Hours.Expected,
            AllocatedExpectedHours = allocated[i],
            OwnedFileCount = inputs[i].OwnedFiles,
            ContextFileCount = inputs[i].ContextFiles,
            InputDigest = measurements[i].InputDigest,
            InventoryDigest = SnapshotPortfolioAreaPlanning.Digest(inventory, inputs[i].Snapshot.Files.Keys),
        });
        RepositoryAnalysisArtifactCacheStatistics stats = artifacts.GetStatistics();
        Interlocked.Add(ref _artifactRequests, stats.Requests);
        Interlocked.Add(ref _artifactHits, stats.Hits);
        Interlocked.Add(ref _artifactInvalidations, persistent.Invalidations);
        Interlocked.Add(ref _artifactEvictions, persistent.Evictions);
        SnapshotStoredBinding binding = new(bindingKey, "", whole.Id, results);
        return binding with { Digest = SnapshotPortfolioStore.BindingDigest(binding) };
    }

    private async Task<SnapshotMeasurementReceipt> MeasureAsync(GitArchiveSnapshot snapshot, int selected, int context,
        MeasurementIdentity identity, ReviewedVendorManifest? vendor, SnapshotPortfolioRunOptions options, CancellationToken token)
    {
        string key = SnapshotPortfolioStore.MeasurementKey(snapshot.InputDigest, identity, selected, context);
        SnapshotReceiptReference? reference = await store.LoadAsync<SnapshotReceiptReference>("measurements", key, token).ConfigureAwait(false);
        SnapshotMeasurementReceipt? cached = reference is null ? null : await store.ReceiptAsync(reference.ReceiptId, token).ConfigureAwait(false);
        if (cached is not null && cached.InputDigest == snapshot.InputDigest && cached.Measurement == identity &&
            cached.SelectedFileCount == selected && cached.ContextFileCount == context)
        {
            _receipts[cached.Id] = cached;
            Interlocked.Increment(ref _receiptHits);
            return cached;
        }
        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(snapshot, analysisArtifactCache: snapshot.AnalysisArtifactCache).ScanAsync(snapshot.RootPath,
            new RepositoryScanOptions { VendorManifest = vendor }, token).ConfigureAwait(false);
        EstimateReport estimate = _estimator.Estimate(evidence, identity.Profile, rateCard: null);
        Interlocked.Increment(ref _estimatorCalls);
        SnapshotMeasurementReceipt receipt = new()
        {
            Id = "",
            InputDigest = snapshot.InputDigest,
            Measurement = identity,
            ProducerVersion = options.ProducerVersion,
            Hours = estimate.TotalEffort,
            Categories = estimate.Categories,
            SelectedFileCount = selected,
            ContextFileCount = context,
            EvidenceDigest = evidence.Repository.SourceDigest!,
            DirectoryIds = SnapshotPortfolioDiagnostics.DirectoryIds(snapshot.Files.Keys),
            MaintainedBodies = SnapshotPortfolioDiagnostics.Bodies(snapshot, evidence),
        };
        receipt = receipt with { Id = SnapshotPortfolioValidation.ReceiptId(receipt) };
        SnapshotPortfolioValidation.Validate(receipt);
        await store.SaveAsync("receipts", receipt.Id, receipt, token).ConfigureAwait(false);
        await store.SaveAsync("measurements", key, new SnapshotReceiptReference(receipt.Id), token).ConfigureAwait(false);
        _receipts[receipt.Id] = receipt;
        return receipt;
    }

    private static bool IsContextControl(string path) => Path.GetExtension(path).ToLowerInvariant() is
        ".json" or ".csproj" or ".props" or ".targets" or ".sln" or ".slnx" or ".config" ||
        Path.GetFileName(path) is ".gitignore" or ".efforthoursignore" or ".gitattributes";
}
