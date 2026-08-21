namespace EffortHours.Analysis;

/// <summary>
/// Exposes immutable repository identities without forcing content or length
/// reads. The path-set identity must change whenever an observable path is added
/// or removed. File content IDs must change whenever the file bytes change.
/// </summary>
public interface IRepositoryImmutableIdentityProvider
{
    public string? RepositoryPathSetIdentity { get; }

    public bool TryGetFileContentId(string path, out string contentId);
}
