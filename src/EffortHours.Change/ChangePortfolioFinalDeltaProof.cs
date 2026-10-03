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
            byte[] bytes = Encoding.UTF8.GetBytes(ContractJson.SerializeCompact(candidate with { FinalDelta = null }));
            hash.AppendData(Encoding.UTF8.GetBytes(bytes.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":"));
            hash.AppendData(bytes);
        }
        return "sha256:" + Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    public static bool TryCompose(IEnumerable<ChangePortfolioCandidate> candidates,
        out Dictionary<string, ChangePortfolioPathEffect> effects)
    {
        effects = new(StringComparer.Ordinal);
        foreach (ChangePortfolioCandidate candidate in candidates)
        {
            foreach (ChangePathEvidence path in candidate.Report.Evidence.Paths)
            {
                if (effects.Count > 16_384)
                    return false;
                if (path.Status == ChangePathStatus.Moved && path.PreviousPath is not null)
                {
                    if (!Append(effects, path.PreviousPath, path.BaseObjectId, null) ||
                        !Append(effects, path.Path, null, path.HeadObjectId))
                        return false;
                }
                else if (!Append(effects, path.Path, path.BaseObjectId, path.HeadObjectId))
                    return false;
            }
        }
        return effects.Count <= 16_384;
    }

    public static bool MatchesInventories(IReadOnlyDictionary<string, ChangePortfolioPathEffect> effects,
        IChangeSnapshot before, IChangeSnapshot after, ChangePathAdmission? admission)
    {
        var left = before.Files.Where(file => admission is null || admission.Admits(file.Path))
            .ToDictionary(file => file.Path, StringComparer.Ordinal);
        var right = after.Files.Where(file => admission is null || admission.Admits(file.Path))
            .ToDictionary(file => file.Path, StringComparer.Ordinal);
        foreach (string path in left.Keys.Concat(right.Keys).Concat(effects.Keys).Distinct(StringComparer.Ordinal))
        {
            left.TryGetValue(path, out ChangeSnapshotFile? baseFile);
            right.TryGetValue(path, out ChangeSnapshotFile? headFile);
            if (effects.TryGetValue(path, out ChangePortfolioPathEffect? effect))
            {
                if (effect.BaseState != baseFile?.ObjectId || effect.HeadState != headFile?.ObjectId ||
                    baseFile is { IsLink: true } or { IsSubmodule: true } ||
                    headFile is { IsLink: true } or { IsSubmodule: true } ||
                    (baseFile is not null && headFile is not null && baseFile.Mode != headFile.Mode))
                    return false;
            }
            else if (baseFile?.ObjectId != headFile?.ObjectId || baseFile?.Mode != headFile?.Mode)
                return false;
        }
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
            if (ordered.Length < 2 || !TryCompose(ordered, out var effects) ||
                !TryCompose([new ChangePortfolioCandidate
                {
                    RepositoryId = ordered[0].RepositoryId, SelectorId = "endpoint", Report = receipt.Report,
                    Attribution = ordered[0].Attribution,
                }], out var endpointEffects) ||
                receipt.RawDeltaDigest != ChangePortfolioIdentity.PatchDigest(effects.Values.Where(effect => effect.BaseState != effect.HeadState)) ||
                receipt.RawDeltaDigest != ChangePortfolioIdentity.PatchDigest(endpointEffects.Values.Where(effect => effect.BaseState != effect.HeadState)) ||
                receipt.Report.Selection.Base.ObjectId != ordered[0].Report.Selection.Base.ObjectId ||
                receipt.Report.Selection.Head.ObjectId != ordered[^1].Report.Selection.Head.ObjectId ||
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
