using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

/// <summary>
/// A durable, content-addressed store of per-commit estimates and per-day
/// final-delta outcomes. Keys bind every input that determines one result, so a
/// hit is exactly the value a cold calculation would produce; misses are always
/// recomputed. Stores own their size bounds and eviction.
/// </summary>
public interface IChangePortfolioResultCache
{
    /// <summary>Digest of the execution identity shared by every key, such as the
    /// build, repository scope, and admission policy.</summary>
    public string Context { get; }

    public Task<ChangePortfolioCachedResult?> TryLoadAsync(string key, CancellationToken cancellationToken);

    public Task StoreAsync(string key, ChangePortfolioCachedResult result, CancellationToken cancellationToken);
}

/// <summary>Exactly one of the three members is set.</summary>
public sealed record ChangePortfolioCachedResult
{
    public ChangeEstimateReport? Report { get; init; }

    public ChangePortfolioFinalDelta? FinalDelta { get; init; }

    public ChangePortfolioFinalDeltaRejection? FinalDeltaRejection { get; init; }

    public bool IsValid() =>
        (Report is null ? 0 : 1) + (FinalDelta is null ? 0 : 1) + (FinalDeltaRejection is null ? 0 : 1) == 1 &&
        (Report is null || ContractValidation.Validate(Report).Count == 0) &&
        (FinalDelta is null || ContractValidation.Validate(FinalDelta.Report).Count == 0) &&
        (FinalDeltaRejection is null || FinalDeltaRejection.IsValid());
}

public static class ChangePortfolioResultCacheKeys
{
    public const string Protocol = "portfolio-result-cache/1.0.0";

    /// <summary>Binds one immutable commit estimate: selection object IDs, plan
    /// components/diagnostics, repository identity, path admission, profile, and
    /// estimator version. Local paths are excluded; object IDs bind content.</summary>
    public static string ForCommit(
        IChangePortfolioResultCache cache,
        GitChangePlan plan,
        EstimationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(plan);
        return Digest(new
        {
            Protocol,
            Kind = "commit",
            cache.Context,
            ChangeEstimator.Version,
            Profile = profile,
            RepositoryName = ReportRepositoryName(plan),
            Admission = plan.PathAdmission?.ProfileDigest,
            plan.Selection,
            Components = plan.Components.Select(component => new
            {
                component.Kind,
                component.Selector,
                component.BaseObjectId,
                component.HeadObjectId,
            }),
            plan.Diagnostics,
        });
    }

    /// <summary>Binds one repository-day final-delta outcome to the exact group
    /// members: selector, complete report bytes, attribution, and replay evidence.</summary>
    public static string ForFinalDelta(
        IChangePortfolioResultCache cache,
        string repositoryId,
        IEnumerable<ChangePortfolioCandidate> group,
        ChangePathAdmission? pathAdmission,
        EstimationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(group);
        return Digest(new
        {
            Protocol,
            Kind = "final-delta",
            cache.Context,
            ChangeEstimator.Version,
            ChangePortfolioFinalDelta.Policy,
            Profile = profile,
            RepositoryId = repositoryId,
            Admission = pathAdmission?.ProfileDigest,
            Members = group.OrderBy(candidate => candidate.SelectorId, StringComparer.Ordinal).Select(candidate => new
            {
                candidate.RepositoryId,
                candidate.SelectorId,
                Report = TextDigest(ContractJson.SerializeCompact(candidate.Report)),
                candidate.Attribution,
                candidate.ReplayEvidence,
            }),
        });
    }

    // Mirrors the report name chosen for a plan without a session, so the key
    // binds the name the report carries.
    private static string ReportRepositoryName(GitChangePlan plan)
    {
        string name = plan.RepositoryName ??
            Path.GetFileName(Path.TrimEndingDirectorySeparator(plan.RepositoryPath));
        return string.IsNullOrWhiteSpace(name) ? plan.RepositoryPath : name;
    }

    private static string Digest<T>(T value) => TextDigest(ContractJson.SerializeCompact(value));

    private static string TextDigest(string value) => "sha256:" +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

/// <summary>Per-call hit accounting; operational only, never semantic.</summary>
public sealed class ChangePortfolioResultCacheCounters
{
    private int _commitRequests;
    private int _commitHits;
    private int _finalDeltaRequests;
    private int _finalDeltaHits;

    public int CommitRequests => Volatile.Read(ref _commitRequests);

    public int CommitHits => Volatile.Read(ref _commitHits);

    public int FinalDeltaRequests => Volatile.Read(ref _finalDeltaRequests);

    public int FinalDeltaHits => Volatile.Read(ref _finalDeltaHits);

    internal void Commit(bool hit)
    {
        Interlocked.Increment(ref _commitRequests);
        if (hit) Interlocked.Increment(ref _commitHits);
    }

    internal void FinalDelta(bool hit)
    {
        Interlocked.Increment(ref _finalDeltaRequests);
        if (hit) Interlocked.Increment(ref _finalDeltaHits);
    }
}
