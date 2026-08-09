using EffortHours.Contracts.V1;

namespace EffortHours.Analysis;

public interface IRepositoryScanner
{
    public Task<RepositoryEvidence> ScanAsync(
        string repositoryPath,
        RepositoryScanOptions? options = null,
        CancellationToken cancellationToken = default);
}
