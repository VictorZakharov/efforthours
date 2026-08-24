namespace EffortHours.Change;

public sealed class ChangePathAdmission
{
    private readonly Func<string, bool> _admits;

    internal ChangePathAdmission(string profileDigest, Func<string, bool> admits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileDigest);
        _admits = admits ?? throw new ArgumentNullException(nameof(admits));
        ProfileDigest = profileDigest;
    }

    public string ProfileDigest { get; }

    public bool Admits(string path)
    {
        ChangeSnapshotInventoryBuilder.ValidateRelativePath(path);
        return _admits(path);
    }
}
