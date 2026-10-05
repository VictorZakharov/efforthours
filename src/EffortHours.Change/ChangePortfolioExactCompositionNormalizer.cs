using EffortHours.Contracts.V1;

namespace EffortHours.Change;

// This proves immutable endpoint equality, not semantic similarity or authorship.
internal static class ChangePortfolioExactCompositionNormalizer
{
    private const int MaximumChainLength = 256;
    private const int MaximumPaths = 16_384;
    private const int MaximumSteps = 1_000_000;

    public static void Mark(IReadOnlyList<ChangePortfolioItemDraft> drafts)
    {
        MarkRetainedRewrites(drafts);
        ChangePortfolioItemDraft[] commits = [.. drafts.Where(draft => !draft.Suppressed &&
            draft.Candidate.Report.Selection.Kind == ChangeSelectionKind.Commit &&
            draft.Candidate.Attribution.ParentCount <= 1 && draft.Effects.Count > 0)
            .OrderBy(draft => draft.Id, StringComparer.Ordinal)];
        Dictionary<string, ChangePortfolioItemDraft> byHead = commits
            .GroupBy(draft => draft.Candidate.Report.Selection.Head.ObjectId, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);
        Dictionary<string, ChangePortfolioItemDraft[]> byPatch = drafts
            .Where(draft => !draft.Suppressed && draft.Effects.Count > 0)
            .GroupBy(draft => draft.PatchDigest, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(draft => draft.Id,
                StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        HashSet<int> possiblePathCounts = [.. drafts.Select(draft => draft.Effects.Count)];
        HashSet<string> retained = new(StringComparer.Ordinal);
        int steps = 0;
        foreach (ChangePortfolioItemDraft end in commits)
        {
            if (end.Suppressed)
            {
                continue;
            }

            List<ChangePortfolioItemDraft> chain = [];
            Dictionary<string, ChangePortfolioPathEffect> effects = new(StringComparer.Ordinal);
            HashSet<string> visited = new(StringComparer.Ordinal);
            ChangePortfolioItemDraft? current = end;
            int netPathCount = 0;
            while (current is not null && !current.Suppressed &&
                chain.Count < MaximumChainLength && visited.Add(current.Id))
            {
                if (++steps > MaximumSteps)
                {
                    end.UncertaintyReasons.Add(
                        "Exact composition proof reached its operation bound; unproven representations remain in ordinary overlap reconciliation.");
                    return;
                }

                if (!Prepend(current, effects, ref netPathCount) || effects.Count > MaximumPaths)
                {
                    break;
                }

                chain.Add(current);
                if (chain.Count > 1 && netPathCount > 0 && possiblePathCounts.Contains(netPathCount))
                {
                    string digest = ChangePortfolioIdentity.PatchDigest(effects.Values
                        .Where(effect => effect.BaseState != effect.HeadState));
                    if (byPatch.TryGetValue(digest, out ChangePortfolioItemDraft[]? matches))
                    {
                        foreach (ChangePortfolioItemDraft match in matches.Where(match =>
                            !match.Suppressed && !visited.Contains(match.Id) && !retained.Contains(match.Id)))
                        {
                            if (IsRelated(match, chain, byHead))
                            {
                                continue;
                            }

                            match.ExactComposition = new ChangePortfolioExactComposition
                            {
                                ItemIds = [.. chain.AsEnumerable().Reverse().Select(draft => draft.Id)],
                                PatchDigest = digest,
                            };
                            match.UncertaintyReasons.Add(
                                "The exact represented endpoint delta equals a connected retained commit composition and receives zero normalized allocation. Retained commit dates are selection evidence, not recovered workdays.");
                            retained.UnionWith(chain.Select(draft => draft.Id));
                        }
                    }
                }

                byHead.TryGetValue(current.Candidate.Report.Selection.Base.ObjectId, out current);
            }
        }
    }

    private static void MarkRetainedRewrites(IReadOnlyList<ChangePortfolioItemDraft> drafts)
    {
        foreach (IGrouping<string, ChangePortfolioItemDraft> group in drafts.Where(draft =>
            !draft.Suppressed && draft.Effects.Count > 0 &&
            draft.Candidate.Report.Selection.Kind == ChangeSelectionKind.Commit &&
            draft.Candidate.Attribution.HeadIds is { Count: > 0 }).GroupBy(draft => draft.PatchDigest, StringComparer.Ordinal))
        {
            List<ChangePortfolioItemDraft> kept = [];
            HashSet<string> headSets = new(StringComparer.Ordinal);
            foreach (ChangePortfolioItemDraft item in group.OrderBy(draft => draft.Candidate.Attribution.Rewrite?.OriginalAuthorTimestamp ??
                draft.Candidate.Attribution.SelectedTimestamp)
                .ThenBy(draft => draft.Candidate.Attribution.Rewrite?.Role == "original" ? 0 : 1)
                .ThenBy(draft => draft.Id, StringComparer.Ordinal))
            {
                ChangePortfolioItemDraft? original = kept.FirstOrDefault(earlier =>
                    !earlier.Candidate.Attribution.HeadIds!.Intersect(item.Candidate.Attribution.HeadIds!,
                        StringComparer.Ordinal).Any());
                if (original is null)
                {
                    string heads = string.Join('\n', item.Candidate.Attribution.HeadIds!.Order(StringComparer.Ordinal));
                    if (kept.Count < 64 && headSets.Add(heads))
                    {
                        kept.Add(item);
                    }
                    continue;
                }

                item.DuplicateOfItemId = original.Id;
                item.UncertaintyReasons.Add(
                    "The exact represented patch repeats a retained representation on disjoint pinned heads and receives zero normalized allocation. This does not prove original workdays or sole authorship.");
            }
        }
    }

    private static bool IsRelated(
        ChangePortfolioItemDraft candidate,
        List<ChangePortfolioItemDraft> chain,
        Dictionary<string, ChangePortfolioItemDraft> byHead)
    {
        // A later reintroduction after a revert is not a rewritten representation.
        // Selected ancestry can omit a revert (another author or outside the interval).
        // Shared reachable heads therefore cannot certify composition equivalence.
        if (candidate.Candidate.Attribution.HeadIds is { Count: > 0 } heads)
        {
            if (chain.Any(member => member.Candidate.Attribution.HeadIds is not { Count: > 0 } memberHeads ||
                heads.Intersect(memberHeads, StringComparer.Ordinal).Any()))
            {
                return true;
            }
        }
        else if (chain.Any(member => member.Candidate.Attribution.HeadIds is { Count: > 0 }) ||
            candidate.Candidate.Report.Selection.Base.ObjectId != chain[^1].Candidate.Report.Selection.Base.ObjectId)
        {
            return true;
        }

        return Reaches(candidate, chain.Select(member => member.Id).ToHashSet(StringComparer.Ordinal)) ||
            Reaches(chain[^1], new HashSet<string>([candidate.Id], StringComparer.Ordinal));

        bool Reaches(ChangePortfolioItemDraft start, HashSet<string> targets)
        {
            ChangePortfolioItemDraft? current = start;
            for (int index = 0; index < MaximumChainLength && current is not null; index++)
            {
                if (targets.Contains(current.Id))
                {
                    return true;
                }

                byHead.TryGetValue(current.Candidate.Report.Selection.Base.ObjectId, out current);
            }

            return current is not null; // Incomplete bounded proof remains represented.
        }
    }

    private static bool Prepend(
        ChangePortfolioItemDraft earlier,
        Dictionary<string, ChangePortfolioPathEffect> effects,
        ref int netPathCount)
    {
        foreach ((string path, ChangePortfolioPathEffect effect) in earlier.Effects)
        {
            if (effects.TryGetValue(path, out ChangePortfolioPathEffect? later))
            {
                if (effect.HeadState != later.BaseState)
                {
                    return false;
                }

                if (later.BaseState != later.HeadState)
                {
                    netPathCount--;
                }

                effects[path] = later with { BaseState = effect.BaseState };
                if (effect.BaseState != later.HeadState)
                {
                    netPathCount++;
                }
            }
            else
            {
                effects.Add(path, effect);
                if (effect.BaseState != effect.HeadState)
                {
                    netPathCount++;
                }
            }
        }

        return true;
    }
}
