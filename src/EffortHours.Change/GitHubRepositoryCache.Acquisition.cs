namespace EffortHours.Change;

internal sealed partial class GitHubRepositoryCache
{
    // This monitor runs under the repository cache lock. It observes object-store
    // growth, including incoming packs, rather than claiming wire-byte accounting.
    private async Task FetchObservedAsync(string path, string identity, IReadOnlyList<DiscoveredHead> missing,
        IReadOnlyList<string> negotiationTips, long baseline, Action<long>? observeGrowth, CancellationToken token)
    {
        using CancellationTokenSource fetchCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        Task fetch = _git.FetchManagedObjectsAsync(path, _fetchSource(identity),
            [.. missing.Select(head => head.FetchRef)], negotiationTips, fetchCancellation.Token);
        try
        {
            while (observeGrowth is not null && !fetch.IsCompleted)
            {
                using CancellationTokenSource tickCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
                Task tick = Task.Delay(TimeSpan.FromSeconds(5), tickCancellation.Token);
                if (await Task.WhenAny(fetch, tick).ConfigureAwait(false) == fetch)
                {
                    tickCancellation.Cancel();
                    break;
                }
                await tick.ConfigureAwait(false);
                GitObjectStorage storage = await MeasureAsync(path, token).ConfigureAwait(false);
                string pack = Path.Combine(path, "objects", "pack");
                long incoming = Directory.Exists(pack) ? Directory.EnumerateFiles(pack, "tmp_pack_*").Sum(Length) : 0;
                observeGrowth(Math.Max(0, checked(storage.Bytes + incoming - baseline)));
            }
            await fetch.ConfigureAwait(false);
        }
        catch
        {
            fetchCancellation.Cancel();
            try { await fetch.ConfigureAwait(false); }
            catch (Exception) { /* Drain the child process before releasing the cache lock. */ }
            throw;
        }
    }

    private static long Length(string file)
    {
        try { return new FileInfo(file).Length; }
        catch (FileNotFoundException) { return 0; }
    }
}
