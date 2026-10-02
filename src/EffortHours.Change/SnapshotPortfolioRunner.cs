using System.Collections.Concurrent;
using System.Diagnostics;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Estimation;

namespace EffortHours.Change;

public sealed record SnapshotPortfolioRunOptions
{
    public required DateTimeOffset AsOf { get; init; }
    public required string ProducerVersion { get; init; }
    public bool Preflight { get; init; }
    public bool FetchMissing { get; init; }
    public Action<string, string>? Progress { get; init; }
    public int Concurrency { get; init; } = 1;
    public int MaximumArchiveBytes { get; init; } = 256 * 1024 * 1024;
    public RateCard? RateCard { get; init; }
    public SnapshotPortfolioReport? Previous { get; init; }
    public SnapshotPortfolioReport? Reproduce { get; init; }
    public SnapshotPortfolioReport? EndpointReference { get; init; }
    public string? PreviousEpochDigest { get; init; }
}

public sealed partial class SnapshotPortfolioRunner(SnapshotPortfolioStore store, IEstimator? estimator = null)
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
    private int _inventoryReads;
    private int _areaPlanningCalls;
    private int _selectorCompilations;
    private int _planningReuseHits;

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
            catch (Exception e) when (options.Preflight && e is ExternalCommandException or InvalidDataException or SnapshotPlanningException)
            {
                results[project.Id] = SnapshotPortfolioDiagnostics.Unavailable(manifest, project, options.AsOf, e is SnapshotPlanningException failure ? failure.Category :
                    e is ExternalCommandException ? "missing-object-or-ref" : "invalid-area-definition");
            }
        }).ConfigureAwait(false);
        using Process process = Process.GetCurrentProcess();
        SnapshotPortfolioReport report = new()
        {
            Status = options.Preflight ? "planned" : "complete",
            AsOf = options.AsOf,
            Year = manifest.Year,
            Timezone = manifest.Timezone,
            CalendarPolicy = manifest.CalendarPolicy,
            ManifestDigest = SnapshotMeasurementIdentity.Digest(manifest),
            MeasurementEpoch = SnapshotMeasurementIdentity.Digest(SnapshotMeasurementIdentity.Create(manifest.Profile)),
            PreviousEpochDigest = options.PreviousEpochDigest,
            SemanticDigest = "",
            Projects = [.. manifest.Projects.Select(p => results[p.Id])],
            Receipts = [.. _receipts.Values.OrderBy(r => r.Id, StringComparer.Ordinal)],
            Telemetry = new()
            {
                InventoryReads = _inventoryReads,
                AreaPlanningCalls = _areaPlanningCalls,
                SelectorCompilations = _selectorCompilations,
                PlanningReuseHits = _planningReuseHits,
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
        if (!options.Preflight && options.EndpointReference is not null && manifest.CalendarPolicy == SnapshotPortfolioVersions.Daily)
            SnapshotDailyCalendar.ValidateReference(report, options.EndpointReference);
        return report;
    }

    private async Task<SnapshotProjectResult> MeasureProjectAsync(SnapshotPortfolioManifest manifest,
        SnapshotProjectDefinition project, SnapshotProjectLocator locator,
        SnapshotPortfolioRunOptions options, CancellationToken token)
    {
        options.Progress?.Invoke("history", project.Id);
        GitClient git = new();
        string root;
        string head;
        string selectedRef = options.Reproduce?.Projects.Single(p => p.Id == project.Id).HeadObjectId ?? project.Ref;
        if ((locator.RepositoryPath is null) == (locator.GitHubRepository is null))
            throw new InvalidDataException("Each project requires exactly one local or provider locator.");
        if (locator.GitHubRepository is not null)
        {
            ManagedRepositoryHead managed;
            try
            {
                managed = await new ManagedGitQueryPlanner().PrepareSnapshotHeadAsync(locator.GitHubRepository,
                selectedRef, options.FetchMissing, token).ConfigureAwait(false);
            }
            catch (InvalidOperationException e) { throw new SnapshotPlanningException("missing-object-or-ref", e.Message, inner: e); }
            root = managed.RepositoryPath;
            head = managed.ObjectId;
        }
        else
        {
            root = await git.ResolveRepositoryRootAsync(locator.RepositoryPath!, token).ConfigureAwait(false);
            head = await git.ResolveCommitAsync(root, selectedRef, token).ConfigureAwait(false);
            if (SnapshotPortfolioPaths.IsWithin(root, store.DirectoryPath))
                throw new InvalidDataException("Checkpoints must be outside measured source repositories.");
        }
        (IReadOnlyList<SnapshotHistoryCommit> history, bool shallow) = await git.ReadSnapshotHistoryAsync(root, head, token).ConfigureAwait(false);
        try { await git.ValidateArchivePolicyAsync(root, token).ConfigureAwait(false); }
        catch (InvalidDataException e) { throw new SnapshotPlanningException("invalid-archive", e.Message, inner: e); }
        if (shallow)
        {
            if (options.Preflight)
            {
                options.Progress?.Invoke("shallow-history: map gitHubRepository and run --fetch-missing, or explicitly git fetch --unshallow origin", project.Id);
                return SnapshotPortfolioDiagnostics.Unavailable(manifest, project, options.AsOf, "shallow-history", head, history[^1].CommittedAt, shallow: true);
            }
            throw new SnapshotPlanningException("shallow-history", "Shallow first-parent history cannot prove monthly selection. " +
                "Map this project to gitHubRepository and run with --fetch-missing to acquire history in the private managed cache, " +
                "or explicitly run git fetch --unshallow origin in your own clone. Preflight and ordinary runs never deepen source repositories.");
        }
        string areasDigest = SnapshotMeasurementIdentity.Digest(project.Areas);
        string? ownership = project.VendorManifest is null ? null : ReviewedVendorManifestValidation.ComputeDigest(project.VendorManifest);
        MeasurementIdentity identity = SnapshotMeasurementIdentity.Create(manifest.Profile, ownership);
        List<SnapshotPeriodResult> periods = [];
        IReadOnlyList<SnapshotPeriodResult> selections = SnapshotPortfolioSelection.Select(manifest.Year,
            TimeZoneInfo.FindSystemTimeZoneById(manifest.Timezone), options.AsOf, history, manifest.CalendarPolicy);
        bool daily = manifest.CalendarPolicy == SnapshotPortfolioVersions.Daily;
        if (daily) selections = SnapshotCalendarBenchmark.Apply(selections, TimeZoneInfo.FindSystemTimeZoneById(manifest.Timezone),
            await git.ReadSnapshotBenchmarkHistoryAsync(root, head, token).ConfigureAwait(false));
        string? latestId = selections.LastOrDefault(p => p.CommitObjectId is not null)?.Id;
        Dictionary<string, (IReadOnlyList<ChangeSnapshotFile> Files, bool Attributes)> inventories = new(StringComparer.Ordinal);
        Dictionary<string, IReadOnlyList<SnapshotAreaPlan>> plans = new(StringComparer.Ordinal);
        Dictionary<string, SnapshotAreaSelectors> selectorSets = new(StringComparer.Ordinal);
        foreach (SnapshotPeriodResult selected in selections)
        {
            token.ThrowIfCancellationRequested();
            if (selected.CommitObjectId is null) { periods.Add(selected); continue; }
            bool requestAreas = !daily && (project.AreaMeasurementMode != "latest-only" || selected.Id == latestId);
            SnapshotProjectDefinition selectedProject = project;
            string selectedAreasDigest = requestAreas ? areasDigest : "";
            options.Progress?.Invoke(options.Preflight ? "planning" : "measurement", project.Id);
            try
            {
                if (!daily && project.AreaMeasurementMode == "revision-bound")
                {
                    SnapshotAreaRevision? revision = project.AreaRevisions!.FirstOrDefault(r => r.CommitObjectId == selected.CommitObjectId) ?? throw new SnapshotPlanningException("invalid-area-definition", "Selected snapshot needs an exact reviewed area revision.");
                    selectedProject = project with { Areas = revision.Areas };
                    selectedAreasDigest = SnapshotMeasurementIdentity.Digest(revision.Areas);
                    if (selected.Id == latestId && selectedAreasDigest != areasDigest)
                        throw new SnapshotPlanningException("invalid-area-definition", "Latest revision must match current reviewed areas.");
                }
                if (!selectorSets.TryGetValue(selectedAreasDigest, out SnapshotAreaSelectors? selectors) && requestAreas)
                {
                    selectors = new(selectedProject.Areas, () => Interlocked.Increment(ref _selectorCompilations));
                    selectorSets.Add(selectedAreasDigest, selectors);
                }
                string bindingKey = BindingKey(selected.CommitObjectId, identity, selectedAreasDigest);
                SnapshotStoredBinding? binding = await store.LoadAsync<SnapshotStoredBinding>("bindings", bindingKey, token).ConfigureAwait(false);
                bool cached = binding is not null && binding.Key == bindingKey && await LoadBindingReceiptsAsync(binding, identity, token).ConfigureAwait(false);
                IReadOnlyList<SnapshotAreaPlan> areaPlan = [];
                if (!cached)
                {
                    // Trees without attribute controls can share archive work under distinct commit provenance.
                    if (!inventories.TryGetValue(selected.TreeObjectId!, out var cachedInventory))
                    {
                        await using IChangeSnapshot raw = await git.OpenSnapshotAsync(root, selected.CommitObjectId, token).ConfigureAwait(false);
                        cachedInventory = (raw.Files.ToArray(), raw.Files.Any(f => Path.GetFileName(f.Path) == ".gitattributes"));
                        if (inventories.Count == 12) inventories.Remove(inventories.Keys.First());
                        inventories.Add(selected.TreeObjectId!, cachedInventory);
                        Interlocked.Increment(ref _inventoryReads);
                    }
                    else Interlocked.Increment(ref _planningReuseHits);
                    IReadOnlyList<ChangeSnapshotFile> inventory = cachedInventory.Files;
                    if (inventory.Count > 100_000 || inventory.Select(f => f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count() != inventory.Count)
                        throw new SnapshotPlanningException("invalid-archive", "Snapshot file-count limit or portable path uniqueness was violated.");
                    if (inventory.Any(f => f.IsLink || f.IsSubmodule))
                        throw new SnapshotPlanningException("unsupported-source-entry", "Snapshot contains a link or submodule; archive policy rejects these entries.");
                    bool attributes = cachedInventory.Attributes;
                    string treeKey = BindingKey(selected.TreeObjectId! + (attributes ? selected.CommitObjectId : ""), identity, selectedAreasDigest);
                    binding = await store.LoadAsync<SnapshotStoredBinding>("trees", treeKey, token).ConfigureAwait(false);
                    cached = binding is not null && binding.Key == treeKey && await LoadBindingReceiptsAsync(binding, identity, token).ConfigureAwait(false);
                    if (!cached && !options.Preflight)
                    {
                        binding = await MeasureArchiveAsync(git, root, selected.CommitObjectId, inventory, selectedProject, identity, treeKey, selected.TreeObjectId!, requestAreas, selectors, options, token).ConfigureAwait(false);
                        await store.SaveAsync("trees", treeKey, binding, token).ConfigureAwait(false);
                    }
                    if (options.Preflight && !cached && requestAreas)
                    {
                        SnapshotProjectResult? prior = options.Previous?.Projects.FirstOrDefault(p => p.Id == project.Id);
                        string? priorWhole = prior?.Periods.LastOrDefault(p => p.WholeReceiptId is not null)?.WholeReceiptId;
                        if (prior?.AreasDigest != selectedAreasDigest || options.Previous?.Receipts.FirstOrDefault(r => r.Id == priorWhole)?.Measurement != identity)
                            prior = null;
                        string planKey = BindingKey(selected.TreeObjectId!, identity, selectedAreasDigest);
                        if (!plans.TryGetValue(planKey, out areaPlan!))
                        {
                            Interlocked.Increment(ref _areaPlanningCalls);
                            areaPlan = SnapshotPortfolioAreaPlanning.Plan(inventory, selectedProject, prior, attributes, selectors!, token);
                            plans.Add(planKey, areaPlan);
                        }
                        else Interlocked.Increment(ref _planningReuseHits);
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
                    periods.Add(selected with { CacheDisposition = "measurement-required", AreaPlans = areaPlan, AreaDefinitionDigest = !daily && project.AreaMeasurementMode == "revision-bound" ? selectedAreasDigest : null, AreaDisposition = daily ? "not-requested" : project.AreaMeasurementMode == "latest-only" ? requestAreas ? "requested" : "not-requested" : null });
                    continue;
                }
                SnapshotMeasurementReceipt whole = _receipts[binding!.WholeReceiptId];
                SnapshotPeriodResult? previous = options.Previous?.Projects.FirstOrDefault(p => p.Id == project.Id)?.Periods.FirstOrDefault(p => p.Id == selected.Id);
                periods.Add(selected with
                {
                    AreaDefinitionDigest = !daily && project.AreaMeasurementMode == "revision-bound" ? selectedAreasDigest : null,
                    AreaDisposition = daily ? "not-requested" : project.AreaMeasurementMode == "latest-only" ? requestAreas ? options.Preflight ? "requested" : "measured" : "not-requested" : null,
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
            catch (SnapshotPlanningException e) when (options.Preflight)
            {
                periods.Add(selected with { Status = "unavailable", Hours = null, PlanningIssue = e.Category, PlanningAreaId = e.AreaId });
            }
            catch (ExternalCommandException) when (options.Preflight)
            {
                periods.Add(selected with { Status = "unavailable", Hours = null, PlanningIssue = "missing-object-or-ref" });
            }
        }
        if (daily) periods = [.. SnapshotDailyCalendar.Differences(periods)];
        return new()
        {
            Id = project.Id,
            HeadObjectId = head,
            FirstAvailableCommitAt = history[^1].CommittedAt,
            ShallowHistory = shallow,
            AreasDigest = areasDigest,
            AreaMeasurementMode = project.AreaMeasurementMode,
            Periods = periods,
            MonthlyEndpoints = daily ? SnapshotDailyCalendar.Endpoints(manifest.Year, manifest.Timezone, options.AsOf, periods) : null,
            SelectedSnapshotCount = daily ? selections.Count(p => p.CommitObjectId is not null) : null,
            DistinctSnapshotCount = daily ? selections.Where(p => p.CommitObjectId is not null).Select(p => p.CommitObjectId).Distinct().Count() : null,
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

    public static string BindingKey(string objectId, MeasurementIdentity identity, string areasDigest) =>
        SnapshotMeasurementIdentity.Digest(new { objectId, identity, areasDigest });
}
