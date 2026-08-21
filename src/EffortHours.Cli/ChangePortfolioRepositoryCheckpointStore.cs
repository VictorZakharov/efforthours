using System.Text;
using System.Text.Json;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal sealed record ChangePortfolioRepositoryCheckpointLoad(
    IReadOnlyList<ChangePortfolioCandidate> Candidates,
    IReadOnlyList<Diagnostic> Diagnostics,
    GitAuthorPeriodManifestRepositoryScope Scope,
    long ReadBytes);

internal sealed record ChangePortfolioRepositoryCheckpoint
{
    public string FormatVersion { get; init; } =
        ChangePortfolioComparisonPolicies.RepositoryEvidenceCheckpointV2;

    public required string InputDigest { get; init; }

    public required string RepositoryId { get; init; }

    public required string EstimatorVersion { get; init; }

    public required EstimationProfile Profile { get; init; }

    public IReadOnlyList<ChangePortfolioRepositoryCheckpointItem> Items { get; init; } = [];

    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];

    public GitAuthorPeriodManifestRepositoryScope? Scope { get; init; }
}

internal sealed record ChangePortfolioRepositoryCheckpointItem
{
    public required string SelectorId { get; init; }

    public required ChangeEstimateReport Report { get; init; }

    public required ChangePortfolioAttribution Attribution { get; init; }
}

internal sealed class ChangePortfolioRepositoryCheckpointStore(string directory)
{
    private readonly string _directory = ResolveDirectory(directory);

    public async Task<ChangePortfolioRepositoryCheckpointLoad?> TryLoadAsync(
        string repositoryId,
        string inputDigest,
        EstimationProfile profile,
        CancellationToken cancellationToken)
    {
        string path = FilePath(repositoryId, inputDigest);
        FileInfo file = new(path);
        if (!file.Exists || file.Length <= 0 ||
            file.Length > ChangePortfolioLimits.MaximumCheckpointBytesPerRepository)
        {
            return null;
        }

        try
        {
            string json = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken)
                .ConfigureAwait(false);
            ChangePortfolioRepositoryCheckpoint checkpoint =
                ContractJson.Deserialize<ChangePortfolioRepositoryCheckpoint>(json);
            if (checkpoint.FormatVersion !=
                    ChangePortfolioComparisonPolicies.RepositoryEvidenceCheckpointV2 ||
                checkpoint.InputDigest != inputDigest ||
                checkpoint.RepositoryId != repositoryId ||
                checkpoint.EstimatorVersion != ChangeEstimator.Version ||
                checkpoint.Profile != profile ||
                checkpoint.Scope is null ||
                checkpoint.Scope.RepositoryId != repositoryId ||
                checkpoint.Scope.SelectedChangeCount != checkpoint.Items.Count ||
                !ValidScope(checkpoint.Scope))
            {
                return null;
            }

            foreach (ChangePortfolioRepositoryCheckpointItem item in checkpoint.Items)
            {
                if (ContractValidation.Validate(item.Report).Count > 0 ||
                    string.IsNullOrWhiteSpace(item.SelectorId))
                {
                    return null;
                }
            }

            return new ChangePortfolioRepositoryCheckpointLoad(
                [.. checkpoint.Items.Select(item => new ChangePortfolioCandidate
                {
                    RepositoryId = repositoryId,
                    SelectorId = item.SelectorId,
                    Report = item.Report,
                    Attribution = item.Attribution,
                })],
                checkpoint.Diagnostics,
                checkpoint.Scope,
                file.Length);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or
                ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    public async Task<long> WriteAsync(
        string repositoryId,
        string inputDigest,
        EstimationProfile profile,
        IReadOnlyList<ChangePortfolioCandidate> candidates,
        IReadOnlyList<Diagnostic> diagnostics,
        GitAuthorPeriodManifestRepositoryScope scope,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_directory);
        string path = FilePath(repositoryId, inputDigest);
        string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        ChangePortfolioRepositoryCheckpoint checkpoint = new()
        {
            InputDigest = inputDigest,
            RepositoryId = repositoryId,
            EstimatorVersion = ChangeEstimator.Version,
            Profile = profile,
            Items = [.. candidates.Select(candidate => new ChangePortfolioRepositoryCheckpointItem
            {
                SelectorId = candidate.SelectorId,
                Report = candidate.Report,
                Attribution = candidate.Attribution,
            })],
            Diagnostics = diagnostics,
            Scope = scope,
        };
        string json = ContractJson.SerializeDocument(checkpoint);
        long bytes = Encoding.UTF8.GetByteCount(json);
        if (bytes > ChangePortfolioLimits.MaximumCheckpointBytesPerRepository)
        {
            throw new InvalidOperationException(
                $"Repository '{repositoryId}' checkpoint requires {bytes} bytes; the declared " +
                $"per-repository limit is {ChangePortfolioLimits.MaximumCheckpointBytesPerRepository} bytes.");
        }

        try
        {
            await File.WriteAllTextAsync(
                temporary,
                json,
                new UTF8Encoding(false),
                cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
            return bytes;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private string FilePath(string repositoryId, string inputDigest)
    {
        string digest = inputDigest["sha256:".Length..];
        return Path.Combine(_directory, repositoryId + "-" + digest + ".json");
    }

    private static bool ValidScope(GitAuthorPeriodManifestRepositoryScope scope)
    {
        int selected = scope.SelectedChangeCount ?? -1;
        return scope.HeadCount is > 0 and <=
                ChangeAuthorPeriodManifestLimits.MaximumHeadsPerRepository &&
            scope.CandidateCount >= selected &&
            scope.CandidateCount <=
                ChangeAuthorPeriodManifestLimits.EmergencyMaximumIdentityCandidatesPerRepository &&
            !scope.CandidateCountIsLowerBound &&
            selected is >= 0 and <= ChangeAuthorPeriodManifestLimits.MaximumSelectedCommits &&
            scope.SharedContributorChangeCount is >= 0 &&
            scope.SharedContributorChangeCount.Value <= selected &&
            scope.ProjectedSnapshotRequests == selected * 2L &&
            scope.SelectionChunkCount == ChunkCount(
                scope.CandidateCount,
                ChangeAuthorPeriodManifestLimits.SelectionChunkSize) &&
            scope.AnalysisChunkCount == ChunkCount(
                selected,
                ChangeAuthorPeriodManifestLimits.AnalysisChunkSize) &&
            scope.ChargedCandidateLedgerBytes is >= 0 and <=
                ChangeAuthorPeriodManifestLimits.MaximumCandidateLedgerBytesPerRepository &&
            scope.MaximumCandidateLedgerBytes ==
                ChangeAuthorPeriodManifestLimits.MaximumCandidateLedgerBytesPerRepository &&
            scope.BlockingResource is null &&
            scope.Contributors.Count is > 0 and <=
                ChangeAuthorPeriodManifestLimits.MaximumContributors &&
            scope.Contributors.Select(contributor => contributor.ContributorId)
                .Distinct(StringComparer.Ordinal).Count() == scope.Contributors.Count &&
            scope.Contributors.All(contributor =>
                contributor.CandidateCount >= 0 &&
                contributor.DirectAuthorCandidateCount >= 0 &&
                contributor.CoauthorCandidateCount >= 0 &&
                contributor.CandidateCount == contributor.DirectAuthorCandidateCount +
                    contributor.CoauthorCandidateCount &&
                contributor.SelectedChangeCount is >= 0 &&
                contributor.SelectedChangeCount.Value <= selected);
    }

    private static int ChunkCount(int count, int chunkSize) => count == 0
        ? 0
        : ((count - 1) / chunkSize) + 1;

    private static string ResolveDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        string fullPath = Path.GetFullPath(directory);
        string root = Path.GetPathRoot(fullPath) ?? string.Empty;
        if (string.Equals(
            Path.TrimEndingDirectorySeparator(fullPath),
            Path.TrimEndingDirectorySeparator(root),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new IOException("The checkpoint directory cannot be a filesystem root.");
        }

        return fullPath;
    }
}
