using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static class ChangePortfolioFinalDeltaProof
{
    public static string InputDigest(IEnumerable<ChangePortfolioCandidate> candidates)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(ChangePortfolioFinalDelta.Policy));
        foreach (ChangePortfolioCandidate candidate in candidates.OrderBy(candidate => candidate.SelectorId, StringComparer.Ordinal))
        {
            byte[] bytes = Encoding.UTF8.GetBytes(ContractJson.SerializeCompact(candidate with { FinalDelta = null, FinalDeltaRejection = null }));
            hash.AppendData(Encoding.UTF8.GetBytes(bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":"));
            hash.AppendData(bytes);
        }
        return "sha256:" + Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    public static bool TryCompose(IEnumerable<ChangePortfolioCandidate> candidates,
        out Dictionary<string, ChangePortfolioPathEffect> effects) =>
        TryCompose(candidates, out effects, out _);

    public static bool TryCompose(IEnumerable<ChangePortfolioCandidate> candidates,
        out Dictionary<string, ChangePortfolioPathEffect> effects, out string? rejectedPath)
    {
        ChangePortfolioCandidate[] ordered = [.. candidates.OrderBy(candidate => candidate.Attribution.SelectedTimestamp)
            .ThenBy(candidate => ChangePortfolioIdentity.CreateDraft(candidate).Id, StringComparer.Ordinal)];
        effects = new(StringComparer.Ordinal);
        bool complete = true;
        int steps = 0;
        foreach (ChangePortfolioCandidate candidate in ordered)
        {
            foreach (ChangePathEvidence path in candidate.Report.Evidence.Paths)
            {
                if (++steps > 1_000_000 || effects.Count > 16_384) { complete = false; break; }
                if (path.Status == ChangePathStatus.Moved && path.PreviousPath is not null)
                    complete = Append(effects, path.PreviousPath, path.BaseObjectId, null) &&
                        Append(effects, path.Path, null, path.HeadObjectId);
                else complete = Append(effects, path.Path, path.BaseObjectId, path.HeadObjectId);
                if (!complete) break;
            }
            if (!complete) break;
        }
        rejectedPath = null;
        return complete && effects.Count <= 16_384 ||
            ChangePortfolioRawEffectGraph.TryCompose(ordered, out effects, out rejectedPath);
    }

    public static bool SuppressionPreservesRawEffects(IReadOnlyList<ChangePortfolioItemDraft> drafts)
    {
        var byId = drafts.ToDictionary(draft => draft.Id, StringComparer.Ordinal);
        foreach (ChangePortfolioItemDraft draft in drafts.Where(draft => draft.Suppressed))
        {
            string[] retainedIds = draft.DuplicateOfItemId is { } duplicate ? [duplicate] :
                [.. draft.ExactComposition!.ItemIds];
            if (!TryCompose(retainedIds.Select(id => byId[id].Candidate), out var retained) ||
                !TryCompose([draft.Candidate], out var suppressed) ||
                ChangePortfolioIdentity.PatchDigest(retained.Values.Where(effect => effect.BaseState != effect.HeadState)) !=
                ChangePortfolioIdentity.PatchDigest(suppressed.Values.Where(effect => effect.BaseState != effect.HeadState)))
                return false;
        }
        return true;
    }

    public static bool MatchesInventories(IReadOnlyDictionary<string, ChangePortfolioPathEffect> effects,
        IChangeSnapshot before, IChangeSnapshot after, ChangePathAdmission? admission) =>
        MatchesInventories(effects, before, after, admission, out _, out _);

    public static bool MatchesInventories(IReadOnlyDictionary<string, ChangePortfolioPathEffect> effects,
        IChangeSnapshot before, IChangeSnapshot after, ChangePathAdmission? admission,
        out string code, out string? rejectedPath)
    {
        code = "inventory-mismatch";
        rejectedPath = null;
        var left = before.Files.Where(file => admission is null || admission.Admits(file.Path))
            .ToDictionary(file => file.Path, StringComparer.Ordinal);
        var right = after.Files.Where(file => admission is null || admission.Admits(file.Path))
            .ToDictionary(file => file.Path, StringComparer.Ordinal);
        foreach (string path in left.Keys.Concat(right.Keys).Concat(effects.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            rejectedPath = path;
            left.TryGetValue(path, out ChangeSnapshotFile? baseFile);
            right.TryGetValue(path, out ChangeSnapshotFile? headFile);
            if (effects.TryGetValue(path, out ChangePortfolioPathEffect? effect))
            {
                if (baseFile is { IsLink: true } or { IsSubmodule: true } ||
                    headFile is { IsLink: true } or { IsSubmodule: true } ||
                    (baseFile is not null && headFile is not null && baseFile.Mode != headFile.Mode))
                {
                    code = "unsupported-mode";
                    return false;
                }
                if (effect.BaseState != baseFile?.ObjectId || effect.HeadState != headFile?.ObjectId)
                    return false;
            }
            else if (baseFile?.ObjectId != headFile?.ObjectId || baseFile?.Mode != headFile?.Mode)
                return false;
        }
        rejectedPath = null;
        return true;
    }

    public static ChangePortfolioFinalDelta? Find(IReadOnlyList<ChangePortfolioItemDraft> drafts,
        IReadOnlyList<ChangePortfolioItemDraft> active)
    {
        string[] selectors = [.. active.Select(draft => draft.Candidate.SelectorId).Order(StringComparer.Ordinal)];
        ChangePortfolioFinalDelta[] receipts = [.. drafts.Select(draft => draft.Candidate.FinalDelta)
            .OfType<ChangePortfolioFinalDelta>()];
        foreach (ChangePortfolioFinalDelta receipt in receipts)
        {
            ChangePortfolioCandidate[] ordered = [.. active.OrderBy(draft => draft.Candidate.Attribution.SelectedTimestamp)
                .ThenBy(draft => draft.Id, StringComparer.Ordinal).Select(draft => draft.Candidate)];
            if (ordered.Length < 2 || !SuppressionPreservesRawEffects(drafts) || !TryCompose(ordered, out var effects) ||
                !TryCompose([new ChangePortfolioCandidate
                {
                    RepositoryId = ordered[0].RepositoryId, SelectorId = "endpoint", Report = receipt.Report,
                    Attribution = ordered[0].Attribution,
                }], out var endpointEffects) ||
                receipt.RawDeltaDigest != ChangePortfolioIdentity.PatchDigest(effects.Values.Where(effect => effect.BaseState != effect.HeadState)) ||
                receipt.RawDeltaDigest != ChangePortfolioIdentity.PatchDigest(endpointEffects.Values.Where(effect => effect.BaseState != effect.HeadState)) ||
                !ordered.Any(candidate => candidate.Report.Selection.Base.ObjectId == receipt.Report.Selection.Base.ObjectId) ||
                !ordered.Any(candidate => candidate.Report.Selection.Head.ObjectId == receipt.Report.Selection.Head.ObjectId) ||
                !receipt.SelectorIds.SequenceEqual(selectors, StringComparer.Ordinal) ||
                receipt.InputDigest != InputDigest(active.Select(draft => draft.Candidate)) ||
                ContractValidation.Validate(receipt.Report).Count > 0 ||
                receipt.Report.EstimatorVersion != ChangeEstimator.Version ||
                receipt.Report.Selection.Kind != ChangeSelectionKind.BaseHead ||
                receipt.Report.Profile != active[0].Candidate.Report.Profile)
                throw new InvalidOperationException("Final-delta receipt does not match the selected canonical evidence.");
        }
        if (receipts.Length > 1)
            throw new InvalidOperationException("A normalized repository group has multiple final-delta receipts.");
        return receipts.SingleOrDefault();
    }

    private static bool Append(Dictionary<string, ChangePortfolioPathEffect> effects,
        string path, string? before, string? after)
    {
        if (effects.TryGetValue(path, out ChangePortfolioPathEffect? previous))
        {
            if (previous.HeadState != before)
                return false;
            effects[path] = previous with { HeadState = after };
        }
        else effects.Add(path, new(path, before, after));
        return true;
    }
}
