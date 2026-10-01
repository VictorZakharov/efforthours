using System.Collections.Concurrent;
using System.Diagnostics;
using EffortHours.Analysis;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Change;

public sealed record SnapshotPortfolioRunOptions
{
    public required DateTimeOffset AsOf { get; init; }
    public required string ProducerVersion { get; init; }
    public bool Preflight { get; init; }
    public bool FetchMissing { get; init; }
    public int Concurrency { get; init; } = 1;
    public int MaximumArchiveBytes { get; init; } = 256 * 1024 * 1024;
    public RateCard? RateCard { get; init; }
    public SnapshotPortfolioReport? Previous { get; init; }
    public SnapshotPortfolioReport? Reproduce { get; init; }
    public string? PreviousEpochDigest { get; init; }
}

public sealed class SnapshotPortfolioRunner(SnapshotPortfolioStore store, IEstimator? estimator = null)
{
    private readonly IEstimator _estimator = estimator ?? new SeedEstimator();
    private readonly ConcurrentDictionary<string, SnapshotMeasurementReceipt> _receipts = new(StringComparer.Ordinal);
    private int _estimatorCalls;
    private int _receiptHits;
    private int _exports;
    private int _artifactRequests;
    private int _artifactHits;
    private int _artifactInvalidations;
    private int _artifactEvictions;
    private long _gitReadBytes;

    public async Task<SnapshotPortfolioReport> RunAsync(SnapshotPortfolioManifest manifest,
        SnapshotPortfolioLocalMap local, SnapshotPortfolioRunOptions options, CancellationToken cancellationToken)
    {
        SnapshotPortfolioValidation.Validate(manifest);
        if (options.Concurrency is < 1 or > 2) throw new InvalidDataException("Snapshot concurrency must be 1 or 2.");
        if (local.SchemaVersion != ContractVersions.V1 || local.Projects.Count != manifest.Projects.Count ||
            local.Projects.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != local.Projects.Count ||
            !local.Projects.Select(p => p.Id).ToHashSet(StringComparer.Ordinal).SetEquals(manifest.Projects.Select(p => p.Id)))
            throw new InvalidDataException("Local map must contain exactly the curated project IDs.");
        Stopwatch elapsed = Stopwatch.StartNew();
        Dictionary<string, SnapshotProjectLocator> locators = local.Projects.ToDictionary(p => p.Id, StringComparer.Ordinal);
        ConcurrentDictionary<string, SnapshotProjectResult> results = new(StringComparer.Ordinal);
        await Parallel.ForEachAsync(manifest.Projects, new ParallelOptions
        {
            MaxDegreeOfParallelism = options.Concurrency,
            CancellationToken = cancellationToken,
        }, async (project, token) =>
        {
            try { results[project.Id] = await MeasureProjectAsync(manifest, project, locators[project.Id], options, token).ConfigureAwait(false); }
            catch (Exception e) when (options.Preflight && e is ExternalCommandException or InvalidDataException)
            {
                results[project.Id] = SnapshotPortfolioDiagnostics.Unavailable(manifest, project, options.AsOf, "missing-history-or-head");
            }
        }).ConfigureAwait(false);
        using Process process = Process.GetCurrentProcess();
        SnapshotPortfolioReport report = new()
        {
            Status = options.Preflight ? "planned" : "complete",
            AsOf = options.AsOf,
            Year = manifest.Year,
            Timezone = manifest.Timezone,
            ManifestDigest = SnapshotMeasurementIdentity.Digest(manifest),
            MeasurementEpoch = SnapshotMeasurementIdentity.Digest(SnapshotMeasurementIdentity.Create(manifest.Profile)),
            PreviousEpochDigest = options.PreviousEpochDigest,
            SemanticDigest = "",
            Projects = [.. manifest.Projects.Select(p => results[p.Id])],
            Receipts = [.. _receipts.Values.OrderBy(r => r.Id, StringComparer.Ordinal)],
            Telemetry = new()
            {
                EstimatorCalls = _estimatorCalls,
                ReceiptHits = _receiptHits,
                ReceiptInvalidations = store.Invalidations,
                Exports = _exports,
                ArtifactRequests = _artifactRequests,
                ArtifactHits = _artifactHits,
                ArtifactInvalidations = _artifactInvalidations,
                ArtifactEvictions = _artifactEvictions,
                GitReadBytes = _gitReadBytes,
                PeakWorkingSetBytes = process.PeakWorkingSet64,
                ElapsedMilliseconds = elapsed.Elapsed.TotalMilliseconds,
                ConcurrencyLimit = options.Concurrency,
            },
        };
        report = report with { RateCard = options.RateCard, SharedSourceReviews = SnapshotPortfolioDiagnostics.SharedBodies(report.Projects, report.Receipts) };
        report = report with { SemanticDigest = SnapshotPortfolioValidation.ReportDigest(report) };
        if (!options.Preflight) SnapshotPortfolioValidation.Validate(report);
        return report;
    }

    private async Task<SnapshotProjectResult> MeasureProjectAsync(SnapshotPortfolioManifest manifest,
        SnapshotProjectDefinition project, SnapshotProjectLocator locator,
        SnapshotPortfolioRunOptions options, CancellationToken token)
    {
        GitClient git = new();
        string root;
        string head;
        string selectedRef = options.Reproduce?.Projects.Single(p => p.Id == project.Id).HeadObjectId ?? project.Ref;
        if ((locator.RepositoryPath is null) == (locator.GitHubRepository is null))
            throw new InvalidDataException("Each project requires exactly one local or provider locator.");
        if (locator.GitHubRepository is not null)
        {
            ManagedRepositoryHead managed = await new ManagedGitQueryPlanner().PrepareHeadAsync(locator.GitHubRepository,
                selectedRef, options.FetchMissing, token).ConfigureAwait(false);
            root = managed.RepositoryPath;
            head = managed.ObjectId;
        }
        else
        {
            root = await git.ResolveRepositoryRootAsync(locator.RepositoryPath!, token).ConfigureAwait(false);
            head = await git.ResolveCommitAsync(root, selectedRef, token).ConfigureAwait(false);
            string relativeCache = Path.GetRelativePath(root, store.DirectoryPath);
            if (relativeCache != ".." && !relativeCache.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !Path.IsPathRooted(relativeCache))
                throw new InvalidDataException("Checkpoints must be outside measured source repositories.");
        }
        (IReadOnlyList<SnapshotHistoryCommit> history, bool shallow) = await git.ReadSnapshotHistoryAsync(root, head, token).ConfigureAwait(false);
        await git.ValidateArchivePolicyAsync(root, token).ConfigureAwait(false);
        if (shallow)
        {
            if (options.Preflight) return SnapshotPortfolioDiagnostics.Unavailable(manifest, project, options.AsOf,
                "shallow-history", head, history[^1].CommittedAt, shallow: true);
            throw new InvalidDataException("Shallow first-parent history cannot prove monthly selection; supply complete local history.");
        }
        string areasDigest = SnapshotMeasurementIdentity.Digest(project.Areas);
        string? ownership = project.VendorManifest is null ? null : ReviewedVendorManifestValidation.ComputeDigest(project.VendorManifest);
        MeasurementIdentity identity = SnapshotMeasurementIdentity.Create(manifest.Profile, ownership);
        List<SnapshotPeriodResult> periods = [];
        foreach (SnapshotPeriodResult selected in SnapshotPortfolioSelection.Select(manifest.Year,
            TimeZoneInfo.FindSystemTimeZoneById(manifest.Timezone), options.AsOf, history))
        {
            token.ThrowIfCancellationRequested();
            if (selected.CommitObjectId is null) { periods.Add(selected); continue; }
            string bindingKey = BindingKey(selected.CommitObjectId, identity, areasDigest);
            SnapshotStoredBinding? binding = await store.LoadAsync<SnapshotStoredBinding>("bindings", bindingKey, token).ConfigureAwait(false);
            bool cached = binding is not null && binding.Key == bindingKey && await LoadBindingReceiptsAsync(binding, identity, token).ConfigureAwait(false);
            IReadOnlyList<SnapshotAreaPlan> areaPlan = [];
            if (!cached)
            {
                // Trees without attribute controls can share archive work under distinct commit provenance.
                await using IChangeSnapshot raw = await git.OpenSnapshotAsync(root, selected.CommitObjectId, token).ConfigureAwait(false);
                bool attributes = raw.Files.Any(f => Path.GetFileName(f.Path) == ".gitattributes");
                string treeKey = BindingKey(selected.TreeObjectId! + (attributes ? selected.CommitObjectId : ""), identity, areasDigest);
                binding = await store.LoadAsync<SnapshotStoredBinding>("trees", treeKey, token).ConfigureAwait(false);
                cached = binding is not null && binding.Key == treeKey && await LoadBindingReceiptsAsync(binding, identity, token).ConfigureAwait(false);
                if (!cached && !options.Preflight)
                {
                    binding = await MeasureArchiveAsync(git, root, selected.CommitObjectId, raw.Files, project, identity, treeKey, options, token).ConfigureAwait(false);
                    await store.SaveAsync("trees", treeKey, binding, token).ConfigureAwait(false);
                }
                if (options.Preflight && !cached)
                {
                    SnapshotProjectResult? prior = options.Previous?.Projects.FirstOrDefault(p => p.Id == project.Id);
                    string? priorWhole = prior?.Periods.LastOrDefault(p => p.WholeReceiptId is not null)?.WholeReceiptId;
                    if (prior?.AreasDigest != areasDigest || options.Previous?.Receipts.FirstOrDefault(r => r.Id == priorWhole)?.Measurement != identity)
                        prior = null;
                    areaPlan = SnapshotPortfolioAreaPlanning.Plan(raw.Files, project, prior, attributes);
                }
                if (!options.Preflight)
                {
                    binding = binding! with { Key = bindingKey, Digest = "" };
                    binding = binding with { Digest = SnapshotPortfolioStore.BindingDigest(binding) };
                    await store.SaveAsync("bindings", bindingKey, binding, token).ConfigureAwait(false);
                }
            }
            if (options.Preflight && !cached)
            {
                periods.Add(selected with { CacheDisposition = "measurement-required", AreaPlans = areaPlan });
                continue;
            }
            SnapshotMeasurementReceipt whole = _receipts[binding!.WholeReceiptId];
            SnapshotPeriodResult? previous = options.Previous?.Projects.FirstOrDefault(p => p.Id == project.Id)?.Periods.FirstOrDefault(p => p.Id == selected.Id);
            periods.Add(selected with
            {
                Hours = whole.Hours,
                WholeReceiptId = whole.Id,
                Areas = [.. binding.Areas.Select(a => a with
                {
                    PreviousExpectedHours = previous?.Areas.FirstOrDefault(old => old.Id == a.Id)?.StandaloneExpectedHours,
                    ReviewStatus = previous?.Areas.FirstOrDefault(old => old.Id == a.Id)?.ReviewStatus == "review-required" ||
                        previous is not null && previous.Areas.FirstOrDefault(old => old.Id == a.Id)?.InputDigest != a.InputDigest
                        ? "review-required" : "reviewed-boundary",
                })],
                CacheDisposition = cached ? "receipt-hit" : "measured",
                PreviousExpectedHours = previous?.Hours?.Expected,
                AreaPlans = options.Preflight ? binding.Areas.Select(a => new SnapshotAreaPlan(a.Id, "receipt-hit")).ToArray() : [],
                TotalCost = options.RateCard is null ? null : new()
                {
                    Low = decimal.Round(whole.Hours.Low * options.RateCard.HourlyRate, 2),
                    Expected = decimal.Round(whole.Hours.Expected * options.RateCard.HourlyRate, 2),
                    High = decimal.Round(whole.Hours.High * options.RateCard.HourlyRate, 2),
                    Currency = options.RateCard.Currency,
                },
            });
        }
        return new()
        {
            Id = project.Id,
            HeadObjectId = head,
            FirstAvailableCommitAt = history[^1].CommittedAt,
            ShallowHistory = shallow,
            AreasDigest = areasDigest,
            Periods = periods,
        };
    }

    private async Task<bool> LoadBindingReceiptsAsync(SnapshotStoredBinding binding, MeasurementIdentity identity, CancellationToken token)
    {
        foreach (string id in new[] { binding.WholeReceiptId }.Concat(binding.Areas.Select(a => a.ReceiptId)))
        {
            SnapshotMeasurementReceipt? receipt = await store.ReceiptAsync(id, token).ConfigureAwait(false);
            if (receipt is null || receipt.Id != id || receipt.Measurement != identity) return false;
            _receipts[id] = receipt;
        }
        Interlocked.Increment(ref _receiptHits);
        return true;
    }

    private async Task<SnapshotStoredBinding> MeasureArchiveAsync(GitClient git, string root, string commit, IReadOnlyList<ChangeSnapshotFile> inventory,
        SnapshotProjectDefinition project, MeasurementIdentity identity, string bindingKey,
        SnapshotPortfolioRunOptions options, CancellationToken token)
    {
        GitArchiveSnapshot archive = await git.OpenArchiveAsync(root, commit, maximumBytes: options.MaximumArchiveBytes,
            cancellationToken: token).ConfigureAwait(false);
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
        IReadOnlyList<SnapshotAreaInput> inputs = SnapshotAreaPartition.Partition(archive, project.Areas);
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
        IReadOnlyList<decimal> allocated = SnapshotAreaPartition.Allocate(whole.Hours.Expected, [.. measurements.Select(m => m.Hours.Expected)]);
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

    public static string BindingKey(string objectId, MeasurementIdentity identity, string areasDigest) =>
        SnapshotMeasurementIdentity.Digest(new { objectId, identity, areasDigest });
    private static bool IsContextControl(string path) => Path.GetExtension(path).ToLowerInvariant() is
        ".json" or ".csproj" or ".props" or ".targets" or ".sln" or ".slnx" or ".config" ||
        Path.GetFileName(path) is ".gitignore" or ".efforthoursignore" or ".gitattributes";
}
