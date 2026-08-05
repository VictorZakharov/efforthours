using Fairbill.Contracts.V1;

namespace Fairbill.Analysis;

public interface IRepositoryScanner
{
    public Task<RepositoryEvidence> ScanAsync(
        string repositoryPath,
        RepositoryScanOptions? options = null,
        CancellationToken cancellationToken = default);
}
