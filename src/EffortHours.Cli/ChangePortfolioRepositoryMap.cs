namespace EffortHours.Cli;

internal sealed class ChangePortfolioRepositoryMap
{
    private readonly Dictionary<string, string> _rootByRepositoryId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _repositoryIdByRoot = new(PathComparer);

    public void Add(string repositoryId, string repositoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryId);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        string root = Path.GetFullPath(repositoryPath);
        if (_rootByRepositoryId.TryGetValue(repositoryId, out string? priorRoot) &&
            !PathComparer.Equals(priorRoot, root))
        {
            throw new InvalidOperationException(
                $"Manifest repository ID '{repositoryId}' resolves to more than one local Git repository.");
        }

        if (_repositoryIdByRoot.TryGetValue(root, out string? priorId) && priorId != repositoryId)
        {
            throw new InvalidOperationException(
                $"Manifest repository IDs '{priorId}' and '{repositoryId}' resolve to the same local Git repository. " +
                "Use one repository ID so overlap normalization cannot be bypassed.");
        }

        _rootByRepositoryId[repositoryId] = root;
        _repositoryIdByRoot[root] = repositoryId;
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}
