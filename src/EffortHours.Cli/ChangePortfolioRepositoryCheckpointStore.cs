using System.Text;
using System.Text.Json;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal sealed record ChangePortfolioRepositoryCheckpointLoad(
    IReadOnlyList<ChangePortfolioCandidate> Candidates,
    IReadOnlyList<Diagnostic> Diagnostics);

internal sealed record ChangePortfolioRepositoryCheckpoint
{
    public string FormatVersion { get; init; } = "repository-evidence-checkpoint/1.0.0";

    public required string InputDigest { get; init; }

    public required string RepositoryId { get; init; }

    public required string EstimatorVersion { get; init; }

    public required EstimationProfile Profile { get; init; }

    public IReadOnlyList<ChangePortfolioRepositoryCheckpointItem> Items { get; init; } = [];

    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];
}

internal sealed record ChangePortfolioRepositoryCheckpointItem
{
    public required string SelectorId { get; init; }

    public required ChangeEstimateReport Report { get; init; }

    public required ChangePortfolioAttribution Attribution { get; init; }
}

internal sealed class ChangePortfolioRepositoryCheckpointStore(string directory)
{
    private const long MaximumCheckpointBytes = 512L * 1024 * 1024;

    private readonly string _directory = ResolveDirectory(directory);

    public async Task<ChangePortfolioRepositoryCheckpointLoad?> TryLoadAsync(
        string repositoryId,
        string inputDigest,
        EstimationProfile profile,
        CancellationToken cancellationToken)
    {
        string path = FilePath(repositoryId, inputDigest);
        FileInfo file = new(path);
        if (!file.Exists || file.Length <= 0 || file.Length > MaximumCheckpointBytes)
        {
            return null;
        }

        try
        {
            string json = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken)
                .ConfigureAwait(false);
            ChangePortfolioRepositoryCheckpoint checkpoint =
                ContractJson.Deserialize<ChangePortfolioRepositoryCheckpoint>(json);
            if (checkpoint.FormatVersion != "repository-evidence-checkpoint/1.0.0" ||
                checkpoint.InputDigest != inputDigest ||
                checkpoint.RepositoryId != repositoryId ||
                checkpoint.EstimatorVersion != ChangeEstimator.Version ||
                checkpoint.Profile != profile)
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
                checkpoint.Diagnostics);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or
                ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    public async Task WriteAsync(
        string repositoryId,
        string inputDigest,
        EstimationProfile profile,
        IReadOnlyList<ChangePortfolioCandidate> candidates,
        IReadOnlyList<Diagnostic> diagnostics,
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
        };
        string json = ContractJson.SerializeDocument(checkpoint);
        try
        {
            await File.WriteAllTextAsync(
                temporary,
                json,
                new UTF8Encoding(false),
                cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
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
