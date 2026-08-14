using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed partial class GitChangePlanner
{
    private Task<IChangeSnapshot> OpenSnapshotAsync(
        string repositoryPath,
        string objectId,
        GitSnapshotSession? snapshotSession,
        CancellationToken cancellationToken) => snapshotSession is null
            ? _git.OpenSnapshotAsync(repositoryPath, objectId, cancellationToken)
            : snapshotSession.OpenSnapshotAsync(objectId, cancellationToken);

    private static Diagnostic PinnedReferenceDiagnostic() => new()
    {
        Code = "FB5100",
        Severity = DiagnosticSeverity.Information,
        Message = "Moving selectors were resolved to immutable object IDs before analysis; selector metadata does not multiply effort.",
    };

    private static (string Base, string Head) ParseRange(string range)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(range);
        if (range.Contains("...", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Three-dot ranges are ambiguous for final-change estimation. Use an explicit <base>..<head> range.",
                nameof(range));
        }

        int separator = range.IndexOf("..", StringComparison.Ordinal);
        if (separator <= 0 || separator != range.LastIndexOf("..", StringComparison.Ordinal) ||
            separator + 2 >= range.Length)
        {
            throw new ArgumentException("Range must have the exact form <base>..<head>.", nameof(range));
        }

        return (range[..separator], range[(separator + 2)..]);
    }
}
