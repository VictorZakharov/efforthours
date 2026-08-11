using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static class ChangePortfolioIdentity
{
    public static ChangePortfolioItemDraft CreateDraft(ChangePortfolioCandidate candidate)
    {
        ChangeEstimateReport report = candidate.Report;
        Dictionary<string, ChangePortfolioPathEffect> effects = new(StringComparer.Ordinal);
        Dictionary<string, string[]> effectPathsByEvidenceId = new(StringComparer.Ordinal);
        foreach (ChangePathEvidence path in report.Evidence.Paths.Where(path => path.Represented))
        {
            if (path.Status == ChangePathStatus.Moved && path.PreviousPath is not null)
            {
                MergeEffect(effects, path.PreviousPath, path.BaseObjectId, null);
                MergeEffect(effects, path.Path, null, path.HeadObjectId);
                effectPathsByEvidenceId[path.Id] = [path.PreviousPath, path.Path];
            }
            else
            {
                MergeEffect(effects, path.Path, path.BaseObjectId, path.HeadObjectId);
                effectPathsByEvidenceId[path.Id] = [path.Path];
            }
        }

        (Dictionary<(EffortCategory Category, string Path), EffortRange> pathHours,
            Dictionary<EffortCategory, EffortRange> pathlessHours) =
            CategoryContributions(report.WorkItems, effectPathsByEvidenceId);

        string itemId = StableId(
            "change-portfolio-item",
            candidate.RepositoryId,
            candidate.SelectorId,
            report.Selection.Base.ObjectId,
            report.Selection.Head.ObjectId);
        return new ChangePortfolioItemDraft
        {
            Candidate = candidate,
            Id = itemId,
            BaseContextId = BaseContextId(candidate.RepositoryId, report.Selection.Base.ObjectId),
            EvidenceDigest = Digest(ContractJson.SerializeCompact(report.Evidence)),
            PatchDigest = PatchDigest(effects.Values),
            Effects = effects,
            PathCategoryHours = pathHours,
            PathlessCategoryHours = pathlessHours,
        };
    }

    public static string BaseContextId(string repositoryId, string baseObjectId) =>
        StableId("change-portfolio-base", repositoryId, baseObjectId);

    public static string RepositoryGroupId(string repositoryId) =>
        StableId("change-portfolio-repository", repositoryId);

    public static string AdjustmentId(
        string repositoryId,
        ChangePortfolioAdjustmentKind kind,
        int index,
        IReadOnlyList<string> itemIds) => StableId(
            "change-portfolio-adjustment",
            repositoryId,
            kind.ToString(),
            index.ToString(System.Globalization.CultureInfo.InvariantCulture),
            string.Join('\n', itemIds.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)));

    public static string Digest(string value) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();

    private static string PatchDigest(IEnumerable<ChangePortfolioPathEffect> effects)
    {
        string material = string.Join(
            '\n',
            effects.OrderBy(effect => effect.Path, StringComparer.Ordinal).Select(effect =>
                $"{effect.Path}\0{effect.BaseState ?? "<absent>"}\0{effect.HeadState ?? "<absent>"}"));
        return Digest(material.Length == 0 ? "<zero-represented-delta>" : material);
    }

    private static (Dictionary<(EffortCategory Category, string Path), EffortRange> PathHours,
        Dictionary<EffortCategory, EffortRange> PathlessHours) CategoryContributions(
        IReadOnlyList<WorkItem> workItems,
        Dictionary<string, string[]> effectPathsByEvidenceId)
    {
        Dictionary<(EffortCategory Category, string Path), EffortRange> pathHours = [];
        Dictionary<EffortCategory, EffortRange> pathlessHours = [];
        foreach (WorkItem item in workItems)
        {
            string[] paths = [.. item.EvidenceIds
                .SelectMany(id => effectPathsByEvidenceId.TryGetValue(id, out string[]? mapped)
                    ? mapped
                    : [])
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)];
            if (paths.Length == 0)
            {
                Add(pathlessHours, item.Category, item.Hours);
                continue;
            }

            EffortRange[] shares = Split(item.Hours, paths.Length);
            for (int index = 0; index < paths.Length; index++)
            {
                Add(pathHours, (item.Category, paths[index]), shares[index]);
            }
        }

        return (pathHours, pathlessHours);
    }

    private static EffortRange[] Split(EffortRange value, int count)
    {
        EffortRange[] shares = new EffortRange[count];
        EffortRange used = new() { Low = 0m, Expected = 0m, High = 0m };
        for (int index = 0; index < count; index++)
        {
            bool last = index == count - 1;
            EffortRange share = last
                ? new EffortRange
                {
                    Low = value.Low - used.Low,
                    Expected = value.Expected - used.Expected,
                    High = value.High - used.High,
                }
                : new EffortRange
                {
                    Low = value.Low / count,
                    Expected = value.Expected / count,
                    High = value.High / count,
                };
            shares[index] = share;
            used = new EffortRange
            {
                Low = used.Low + share.Low,
                Expected = used.Expected + share.Expected,
                High = used.High + share.High,
            };
        }

        return shares;
    }

    private static void Add<TKey>(
        Dictionary<TKey, EffortRange> values,
        TKey key,
        EffortRange hours)
        where TKey : notnull
    {
        EffortRange current = values.GetValueOrDefault(key) ??
            new EffortRange { Low = 0m, Expected = 0m, High = 0m };
        values[key] = new EffortRange
        {
            Low = current.Low + hours.Low,
            Expected = current.Expected + hours.Expected,
            High = current.High + hours.High,
        };
    }

    private static string StableId(string prefix, params string[] values)
    {
        string digest = Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(string.Join('\n', values))))
            .ToLowerInvariant();
        return $"{prefix}:{digest[..20]}";
    }

    private static void MergeEffect(
        Dictionary<string, ChangePortfolioPathEffect> effects,
        string path,
        string? baseState,
        string? headState)
    {
        if (effects.TryGetValue(path, out ChangePortfolioPathEffect? existing))
        {
            effects[path] = existing with
            {
                BaseState = existing.BaseState ?? baseState,
                HeadState = headState ?? existing.HeadState,
            };
            return;
        }

        effects[path] = new ChangePortfolioPathEffect(path, baseState, headState);
    }
}
