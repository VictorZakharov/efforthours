using System.Security.Cryptography;
using System.Text;
using Fairbill.Contracts.V1;

namespace Fairbill.Change;

internal static class ChangeEvidenceBuilder
{
    private static readonly HashSet<string> BuildOutputDirectories = new(
        ["obj", "bin", "dist", "out", "target", "artifacts", ".next", ".nuxt", ".svelte-kit"],
        StringComparer.OrdinalIgnoreCase);

    public static async Task<ChangeEvidence> BuildAsync(
        ChangeSelection selection,
        IChangeSnapshot baseSnapshot,
        IChangeSnapshot headSnapshot,
        RepositoryEvidence baseEvidence,
        RepositoryEvidence headEvidence,
        IReadOnlyList<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        Dictionary<string, ChangeSnapshotFile> baseFiles = baseSnapshot.Files
            .ToDictionary(file => file.Path, StringComparer.Ordinal);
        Dictionary<string, ChangeSnapshotFile> headFiles = headSnapshot.Files
            .ToDictionary(file => file.Path, StringComparer.Ordinal);
        Dictionary<string, EvidenceFact> baseFileFacts = FileFacts(baseEvidence);
        Dictionary<string, EvidenceFact> headFileFacts = FileFacts(headEvidence);
        int unchanged = baseFiles.Keys.Intersect(headFiles.Keys, StringComparer.Ordinal)
            .Count(path => SameObject(baseFiles[path], headFiles[path]));

        List<PathCandidate> modified = [.. baseFiles.Keys
            .Intersect(headFiles.Keys, StringComparer.Ordinal)
            .Where(path => !SameObject(baseFiles[path], headFiles[path]))
            .Order(StringComparer.Ordinal)
            .Select(path => new PathCandidate(
                ChangePathStatus.Modified,
                path,
                null,
                baseFiles[path],
                headFiles[path]))];
        List<PathCandidate> removed = [.. baseFiles.Keys
            .Except(headFiles.Keys, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(path => new PathCandidate(
                ChangePathStatus.Removed,
                path,
                null,
                baseFiles[path],
                null))];
        List<PathCandidate> added = [.. headFiles.Keys
            .Except(baseFiles.Keys, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(path => new PathCandidate(
                ChangePathStatus.Added,
                path,
                null,
                null,
                headFiles[path]))];

        List<PathCandidate> moved = PairExactMoves(removed, added);
        HashSet<string> baseObjectIds = baseFiles.Values
            .Where(file => !file.IsLink && !file.IsSubmodule)
            .Select(file => file.ObjectId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> headObjectIds = headFiles.Values
            .Where(file => !file.IsLink && !file.IsSubmodule)
            .Select(file => file.ObjectId)
            .ToHashSet(StringComparer.Ordinal);

        List<ChangePathEvidence> paths = [];
        foreach (PathCandidate candidate in moved.Concat(modified).Concat(removed).Concat(added))
        {
            cancellationToken.ThrowIfCancellationRequested();
            EvidenceFact? baseFact = candidate.Base is null
                ? null
                : baseFileFacts.GetValueOrDefault(candidate.PreviousPath ?? candidate.Path);
            EvidenceFact? headFact = candidate.Head is null
                ? null
                : headFileFacts.GetValueOrDefault(candidate.Path);
            ChangePathClassification classification = Classify(
                candidate,
                baseFact,
                headFact,
                baseObjectIds,
                headObjectIds);
            int editRegions = classification == ChangePathClassification.Represented ? 1 : 0;
            string reason = ClassificationReason(classification, candidate.Status);
            if (candidate.Status == ChangePathStatus.Modified &&
                classification == ChangePathClassification.Represented)
            {
                if (candidate.Base!.Length > ContentChangeAnalyzer.MaximumTextBytes ||
                    candidate.Head!.Length > ContentChangeAnalyzer.MaximumTextBytes)
                {
                    reason += " Detailed edit-region comparison was bounded because at least one blob " +
                        "exceeds the eight-megabyte content-diff limit.";
                }
                else
                {
                    byte[] baseContent = await baseSnapshot.ReadAllBytesAsync(
                        candidate.Path,
                        cancellationToken).ConfigureAwait(false);
                    byte[] headContent = await headSnapshot.ReadAllBytesAsync(
                        candidate.Path,
                        cancellationToken).ConfigureAwait(false);
                    ContentChangeResult content = ContentChangeAnalyzer.Analyze(
                        baseContent,
                        headContent,
                        candidate.Path);
                    if (content.FormattingOnly)
                    {
                        classification = ChangePathClassification.FormattingOnly;
                        editRegions = 0;
                        reason = ClassificationReason(classification, candidate.Status);
                    }
                    else
                    {
                        editRegions = content.EditRegions;
                    }
                }
            }

            bool represented = classification == ChangePathClassification.Represented;
            string[] tags = [.. (baseFact?.Tags ?? [])
                .Concat(headFact?.Tags ?? [])
                .Append($"status:{Kebab(candidate.Status)}")
                .Append($"classification:{Kebab(classification)}")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)];
            paths.Add(new ChangePathEvidence
            {
                Id = PathEvidenceId(selection, candidate),
                Status = candidate.Status,
                Path = candidate.Path,
                PreviousPath = candidate.PreviousPath,
                BaseObjectId = candidate.Base?.ObjectId,
                HeadObjectId = candidate.Head?.ObjectId,
                BaseBytes = candidate.Base?.Length,
                HeadBytes = candidate.Head?.Length,
                EditRegions = editRegions,
                Classification = classification,
                Represented = represented,
                Reason = reason,
                Tags = tags,
            });
        }

        paths.Sort(static (left, right) =>
        {
            int status = left.Status.CompareTo(right.Status);
            return status != 0
                ? status
                : StringComparer.Ordinal.Compare(left.Path, right.Path);
        });
        return new ChangeEvidence
        {
            Selection = selection,
            Repository = headEvidence.Repository,
            BaseEvidenceDigest = baseEvidence.Repository.SourceDigest ?? "sha256:unavailable",
            HeadEvidenceDigest = headEvidence.Repository.SourceDigest ?? "sha256:unavailable",
            UnchangedContextPathCount = unchanged,
            Paths = paths,
            Diagnostics = diagnostics,
        };
    }

    private static List<PathCandidate> PairExactMoves(
        List<PathCandidate> removed,
        List<PathCandidate> added)
    {
        Dictionary<string, Queue<PathCandidate>> addedByObject = added
            .Where(candidate => candidate.Head is { IsLink: false, IsSubmodule: false })
            .GroupBy(candidate => candidate.Head!.ObjectId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new Queue<PathCandidate>(group.OrderBy(candidate => candidate.Path, StringComparer.Ordinal)),
                StringComparer.Ordinal);
        List<PathCandidate> moved = [];
        List<PathCandidate> pairedRemoved = [];
        List<PathCandidate> pairedAdded = [];
        foreach (PathCandidate removedCandidate in removed)
        {
            if (removedCandidate.Base is null || removedCandidate.Base.IsLink || removedCandidate.Base.IsSubmodule ||
                !addedByObject.TryGetValue(removedCandidate.Base.ObjectId, out Queue<PathCandidate>? matches) ||
                matches.Count == 0)
            {
                continue;
            }

            PathCandidate addedCandidate = matches.Dequeue();
            pairedRemoved.Add(removedCandidate);
            pairedAdded.Add(addedCandidate);
            moved.Add(new PathCandidate(
                ChangePathStatus.Moved,
                addedCandidate.Path,
                removedCandidate.Path,
                removedCandidate.Base,
                addedCandidate.Head));
        }

        foreach (PathCandidate candidate in pairedRemoved)
        {
            removed.Remove(candidate);
        }

        foreach (PathCandidate candidate in pairedAdded)
        {
            added.Remove(candidate);
        }

        return moved;
    }

    private static ChangePathClassification Classify(
        PathCandidate candidate,
        EvidenceFact? baseFact,
        EvidenceFact? headFact,
        HashSet<string> baseObjectIds,
        HashSet<string> headObjectIds)
    {
        if (candidate.Status == ChangePathStatus.Moved)
        {
            return ChangePathClassification.ExactMove;
        }

        ChangeSnapshotFile? file = candidate.Head ?? candidate.Base;
        if (file is null ||
            candidate.Base is { IsLink: true } or { IsSubmodule: true } ||
            candidate.Head is { IsLink: true } or { IsSubmodule: true })
        {
            return ChangePathClassification.Unsupported;
        }

        string[] tags = [.. (baseFact?.Tags ?? []).Concat(headFact?.Tags ?? [])];
        if (tags.Contains("classification:generated", StringComparer.Ordinal))
        {
            return ChangePathClassification.Generated;
        }

        if (tags.Contains("classification:vendored", StringComparer.Ordinal))
        {
            return ChangePathClassification.Vendored;
        }

        if (tags.Contains("classification:minified", StringComparer.Ordinal))
        {
            return ChangePathClassification.Minified;
        }

        if (tags.Contains("content:binary", StringComparer.Ordinal))
        {
            return ChangePathClassification.Binary;
        }

        if (tags.Contains("role:dependency-lock", StringComparer.Ordinal))
        {
            return ChangePathClassification.Lockfile;
        }

        if (IsBuildOutput(candidate.Path) ||
            (candidate.PreviousPath is not null && IsBuildOutput(candidate.PreviousPath)))
        {
            return ChangePathClassification.BuildOutput;
        }

        if (candidate.Status == ChangePathStatus.Added && baseObjectIds.Contains(file.ObjectId))
        {
            return ChangePathClassification.ExactDuplicate;
        }

        if (candidate.Status == ChangePathStatus.Removed && headObjectIds.Contains(file.ObjectId))
        {
            return ChangePathClassification.ExactDuplicate;
        }

        return ChangePathClassification.Represented;
    }

    private static bool SameObject(ChangeSnapshotFile left, ChangeSnapshotFile right) =>
        string.Equals(left.ObjectId, right.ObjectId, StringComparison.Ordinal) &&
        string.Equals(left.Mode, right.Mode, StringComparison.Ordinal);

    private static bool IsBuildOutput(string path) =>
        path.Split('/').Any(BuildOutputDirectories.Contains);

    private static Dictionary<string, EvidenceFact> FileFacts(RepositoryEvidence evidence) =>
        evidence.Facts
            .Where(fact => fact.Kind == EvidenceKinds.File && fact.Id.StartsWith("file:", StringComparison.Ordinal))
            .ToDictionary(fact => fact.Id[5..], StringComparer.Ordinal);

    private static string ClassificationReason(
        ChangePathClassification classification,
        ChangePathStatus status) => classification switch
        {
            ChangePathClassification.Represented =>
                $"The final {Kebab(status)} artifact is maintained, non-mechanical change evidence.",
            ChangePathClassification.FormattingOnly =>
                "Base and head differ only after conservative whitespace normalization; body effort is excluded.",
            ChangePathClassification.ExactMove =>
                "The same immutable blob moved paths; mechanical movement does not create body implementation effort.",
            ChangePathClassification.Generated =>
                "The scanner classified the artifact as generated; unsupported generated-body effort is excluded.",
            ChangePathClassification.Vendored =>
                "The scanner classified the artifact as vendored; copied body effort is excluded.",
            ChangePathClassification.Minified =>
                "The scanner classified the artifact as minified; derived body effort is excluded.",
            ChangePathClassification.Binary =>
                "Binary body changes are not valued as hand-written implementation by the current change model.",
            ChangePathClassification.Lockfile =>
                "Dependency-lock changes are treated as mechanical resolution output in the current model.",
            ChangePathClassification.BuildOutput =>
                "Build or framework output is excluded from represented change effort.",
            ChangePathClassification.ExactDuplicate =>
                "The same immutable blob remains elsewhere in context; copied body effort is excluded.",
            ChangePathClassification.Unsupported =>
                "Links, submodules, or unsupported object forms are excluded and reported for review.",
            _ => throw new ArgumentOutOfRangeException(nameof(classification)),
        };

    private static string PathEvidenceId(ChangeSelection selection, PathCandidate candidate)
    {
        string identity = string.Join(
            '\n',
            selection.Base.ObjectId,
            selection.Head.ObjectId,
            candidate.Status,
            candidate.PreviousPath ?? string.Empty,
            candidate.Path,
            candidate.Base?.ObjectId ?? string.Empty,
            candidate.Head?.ObjectId ?? string.Empty);
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))
            .ToLowerInvariant();
        return $"change:path:{digest[..24]}";
    }

    private static string Kebab<T>(T value) where T : struct, Enum =>
        string.Concat(value.ToString().Select((character, index) =>
            char.IsUpper(character) && index > 0 ? $"-{char.ToLowerInvariant(character)}" :
            char.ToLowerInvariant(character).ToString()));

    private sealed record PathCandidate(
        ChangePathStatus Status,
        string Path,
        string? PreviousPath,
        ChangeSnapshotFile? Base,
        ChangeSnapshotFile? Head);
}
