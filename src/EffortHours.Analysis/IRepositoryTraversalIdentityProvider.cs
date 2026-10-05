namespace EffortHours.Analysis;

/// <summary>
/// Optional immutable traversal proof: binds every path and traversal attribute,
/// all ignore-file content and readability. Providers must change the identity
/// when any of these inputs change. Missing proof uses ordinary traversal.
/// </summary>
public interface IRepositoryTraversalIdentityProvider
{
    public string? RepositoryTraversalIdentity { get; }
}
