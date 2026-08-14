using System.ComponentModel;
using System.Diagnostics;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal interface IGitHeadReachabilityResolver
{
    public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ResolveAsync(
        string repositoryPath,
        IReadOnlyList<ChangeAuthorPeriodManifestHead> heads,
        IReadOnlyList<string> selectedObjectIds,
        CancellationToken cancellationToken);
}

internal sealed class GitHeadReachabilityResolver : IGitHeadReachabilityResolver
{
    private const int MaximumWalkedCommits = 1_000_000;
    private const int MaximumFrontierCommits = 100_000;

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> ResolveAsync(
        string repositoryPath,
        IReadOnlyList<ChangeAuthorPeriodManifestHead> heads,
        IReadOnlyList<string> selectedObjectIds,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GitHeadReachabilityAccumulator accumulator = new(heads, selectedObjectIds);
        if (accumulator.IsComplete)
        {
            return accumulator.Result();
        }

        List<string> arguments = ["rev-list", "--topo-order", "--parents"];
        arguments.AddRange(heads.OrderBy(head => head.Id, StringComparer.Ordinal).Select(head => head.ObjectId));
        arguments.Add("--");
        ProcessStartInfo startInfo = ExternalCommand.CreateStartInfo("git", repositoryPath, arguments);
        using Process process = new() { StartInfo = startInfo };
        try
        {
            Start(process);
            Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            int walked = 0;
            while (!accumulator.IsComplete)
            {
                string? line = await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line is null)
                {
                    break;
                }

                walked++;
                if (walked > MaximumWalkedCommits)
                {
                    throw new InvalidOperationException(
                        $"Manifest head reachability exceeded the {MaximumWalkedCommits}-commit traversal limit.");
                }

                accumulator.Consume(line);
                if (accumulator.PendingCount > MaximumFrontierCommits)
                {
                    throw new InvalidOperationException(
                        $"Manifest head reachability exceeded the {MaximumFrontierCommits}-commit frontier limit.");
                }
            }

            if (accumulator.IsComplete)
            {
                TryKill(process);
            }

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            _ = await stderr.ConfigureAwait(false);
            if (!accumulator.IsComplete)
            {
                throw new InvalidOperationException(
                    "Git history ended before every selected commit received manifest-head reachability.");
            }

            return accumulator.Result();
        }
        catch
        {
            TryKill(process);
            throw;
        }
    }

    private static void Start(Process process)
    {
        try
        {
            if (!process.Start())
            {
                throw new ExternalCommandException("git", null, "Could not start Git reachability traversal.");
            }
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            throw new ExternalCommandException(
                "git",
                null,
                "Could not start Git reachability traversal.",
                exception);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
        }
    }
}

internal sealed class GitHeadReachabilityAccumulator
{
    private readonly string[] _headIds;
    private readonly HashSet<string> _selected;
    private readonly Dictionary<string, uint> _pending = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<string>> _result = new(StringComparer.Ordinal);

    public GitHeadReachabilityAccumulator(
        IReadOnlyList<ChangeAuthorPeriodManifestHead> heads,
        IReadOnlyList<string> selectedObjectIds)
    {
        ArgumentNullException.ThrowIfNull(heads);
        ArgumentNullException.ThrowIfNull(selectedObjectIds);
        if (heads.Count is < 1 or > 32)
        {
            throw new ArgumentOutOfRangeException(nameof(heads));
        }

        ChangeAuthorPeriodManifestHead[] ordered = [.. heads.OrderBy(head => head.Id, StringComparer.Ordinal)];
        _headIds = [.. ordered.Select(head => head.Id)];
        for (int index = 0; index < ordered.Length; index++)
        {
            _pending[ordered[index].ObjectId] =
                _pending.GetValueOrDefault(ordered[index].ObjectId) | (1U << index);
        }

        _selected = [.. selectedObjectIds.Distinct(StringComparer.Ordinal)];
    }

    public bool IsComplete => _result.Count == _selected.Count;

    public int PendingCount => _pending.Count;

    public void Consume(string line)
    {
        string[] values = line.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (values.Length == 0 || values.Any(value => !IsObjectId(value)))
        {
            throw new InvalidOperationException("Git returned invalid manifest reachability data.");
        }

        string objectId = values[0].ToLowerInvariant();
        if (!_pending.Remove(objectId, out uint reachableHeads) || reachableHeads == 0)
        {
            throw new InvalidOperationException("Git returned inconsistent manifest reachability order.");
        }

        if (_selected.Contains(objectId))
        {
            _result.Add(
                objectId,
                [.. _headIds.Where((_, index) => (reachableHeads & (1U << index)) != 0)]);
        }

        foreach (string parent in values.Skip(1).Select(value => value.ToLowerInvariant()))
        {
            _pending[parent] = _pending.GetValueOrDefault(parent) | reachableHeads;
        }
    }

    public IReadOnlyDictionary<string, IReadOnlyList<string>> Result()
    {
        if (!IsComplete)
        {
            throw new InvalidOperationException("Manifest head reachability is incomplete.");
        }

        return _result;
    }

    private static bool IsObjectId(string value) =>
        value.Length is 40 or 64 && value.All(Uri.IsHexDigit);
}
