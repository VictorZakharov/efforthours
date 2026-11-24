using EffortHours.Analysis;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;

namespace EffortHours.Change;

public sealed partial class SnapshotPortfolioRunner
{
    private async Task<SnapshotStoredBinding> MeasureArchiveAsync(GitClient git, string root, string commit, IReadOnlyList<ChangeSnapshotFile> inventory,
        SnapshotProjectDefinition project, MeasurementIdentity identity, string bindingKey,
        string treeObjectId, bool requestAreas, SnapshotAreaSelectors? selectors, ProjectArtifacts projectArtifacts,
        SnapshotPortfolioRunOptions options, CancellationToken token)
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
        archive = archive.WithArtifactCache(projectArtifacts.Cache);
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
            new RepositoryScanOptions { VendorManifest = vendor, ContextOnlyPaths = snapshot.ContextOnlyPaths }, token).ConfigureAwait(false);
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

    // One artifact cache per project spans every selected snapshot and its areas. Artifact keys already carry
    // content, analyzer version, path, project/package scope, and test role, as in Change repository sessions;
    // persisted entries additionally bind the complete measurement identity (implementation, model, ownership).
    internal sealed class ProjectArtifacts(string directory, MeasurementIdentity identity, SnapshotPortfolioStore store)
    {
        private readonly Lazy<(RepositoryAnalysisArtifactCache Cache, PhysicalRepositoryAnalysisArtifactStore Store)> _value = new(() =>
        {
            PhysicalRepositoryAnalysisArtifactStore persistent = new(directory,
                SnapshotMeasurementIdentity.Digest(new { identity, ArtifactContext = "snapshot-file-artifacts/2" }), store.AccountRetainedBytes);
            return (new RepositoryAnalysisArtifactCache(store: persistent), persistent);
        });

        public RepositoryAnalysisArtifactCache Cache => _value.Value.Cache;

        public void AddTelemetry(SnapshotPortfolioRunner runner)
        {
            if (!_value.IsValueCreated) return;
            RepositoryAnalysisArtifactCacheStatistics stats = _value.Value.Cache.GetStatistics();
            Interlocked.Add(ref runner._artifactRequests, stats.Requests);
            Interlocked.Add(ref runner._artifactHits, stats.Hits);
            Interlocked.Add(ref runner._artifactInvalidations, _value.Value.Store.Invalidations);
            Interlocked.Add(ref runner._artifactEvictions, _value.Value.Store.Evictions);
        }
    }
}
