using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed partial class GitChangePlanner
{
    internal GitChangePlan PlanPinnedCommit(
        string repositoryPath,
        GitCommitMetadata commit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        ArgumentNullException.ThrowIfNull(commit);
        GitSnapshotSession snapshotSession = _git.GetSnapshotSession(repositoryPath);
        snapshotSession.PrimeCommitMetadata(commit);
        List<Diagnostic> diagnostics = [PinnedReferenceDiagnostic()];
        string baseObjectId;
        string baseSelector;
        ChangeSnapshotKind baseKind;
        string? selectedParent = null;
        if (commit.ParentObjectIds.Count == 0)
        {
            baseObjectId = GitClient.EmptyTreeObjectId;
            baseSelector = "<empty-tree>";
            baseKind = ChangeSnapshotKind.EmptyTree;
            diagnostics.Add(new Diagnostic
            {
                Code = "FB5101",
                Severity = DiagnosticSeverity.Information,
                Message = "The selected root commit is compared with Git's empty tree.",
            });
        }
        else
        {
            baseObjectId = commit.ParentObjectIds[0];
            baseSelector = commit.ParentObjectIds.Count == 1
                ? $"{commit.ObjectId}^1"
                : baseObjectId;
            baseKind = ChangeSnapshotKind.GitCommit;
            if (commit.ParentObjectIds.Count > 1)
            {
                selectedParent = baseObjectId;
                diagnostics.Add(new Diagnostic
                {
                    Code = "FB5102",
                    Severity = DiagnosticSeverity.Information,
                    Message = "The merge commit is valued relative to the explicitly selected parent only.",
                });
            }
        }

        ChangeSelection selection = new()
        {
            Kind = ChangeSelectionKind.Commit,
            Base = GitClient.Reference(baseSelector, baseObjectId, baseKind),
            Head = GitClient.Reference(commit.ObjectId, commit.ObjectId),
            Commit = commit.ObjectId,
            Parent = selectedParent,
        };
        ChangeComponentInput component = Component(
            repositoryPath,
            commit.ObjectId,
            baseObjectId,
            commit.ObjectId,
            snapshotSession: snapshotSession);
        return CreatePlan(
            repositoryPath,
            selection,
            [component],
            diagnostics,
            snapshotSession);
    }
}
