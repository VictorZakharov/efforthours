using System.IO.Compression;
using System.Text;
using System.Text.Json;
using EffortHours.Change;
using EffortHours.Contracts;

namespace EffortHours.Cli;

internal sealed record ChangePortfolioResultCacheEntry
{
    public string FormatVersion { get; init; } = ChangePortfolioResultCacheStore.FormatVersion;

    public required string Key { get; init; }

    public required ChangePortfolioCachedResult Result { get; init; }
}

/// <summary>
/// Durable per-commit and per-day final-delta results beside repository evidence
/// checkpoints. One gzip-compressed, content-addressed file per key; corrupt,
/// mismatched, or unreadable entries are misses. Hits refresh the entry's
/// last-write time, and <see cref="Trim"/> evicts least recently used entries
/// beyond fixed entry and compressed-byte bounds. Write failures never fail a run.
/// </summary>
internal sealed class ChangePortfolioResultCacheStore
{
    public const string FormatVersion = "portfolio-result-cache-entry/1.0.0";
    public const string DirectoryName = "results";
    public const int DefaultMaximumEntries = 131_072;
    public const long DefaultMaximumBytes = 2L * 1024 * 1024 * 1024;
    public const long MaximumEntryBytes = 64L * 1024 * 1024;
    private const string Extension = ".json.gz";
    private static readonly System.Buffers.SearchValues<char> s_hex =
        System.Buffers.SearchValues.Create("0123456789abcdef");

    private readonly Lock _clockGate = new();
    private DateTime _lastStamp = DateTime.MinValue;
    private int _hits;
    private int _misses;
    private int _writes;
    private int _writeFailures;
    private int _rejected;

    public ChangePortfolioResultCacheStore(
        string checkpointDirectory,
        int maximumEntries = DefaultMaximumEntries,
        long maximumBytes = DefaultMaximumBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpointDirectory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        Root = Path.Combine(Path.GetFullPath(checkpointDirectory), DirectoryName);
        MaximumEntries = maximumEntries;
        MaximumBytes = maximumBytes;
    }

    public string Root { get; }

    public int MaximumEntries { get; }

    public long MaximumBytes { get; }

    public int Hits => Volatile.Read(ref _hits);

    public int Misses => Volatile.Read(ref _misses);

    public int Writes => Volatile.Read(ref _writes);

    public int WriteFailures => Volatile.Read(ref _writeFailures);

    /// <summary>Entries present but unusable (corrupt, foreign key, invalid result).</summary>
    public int Rejected => Volatile.Read(ref _rejected);

    public IChangePortfolioResultCache ForContext(string context) => new View(this, context);

    public ChangePortfolioResultCacheTrim Trim(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(Root))
            return new ChangePortfolioResultCacheTrim(0, 0, 0, 0);
        List<FileInfo> files = [];
        foreach (string path in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            FileInfo file = new(path);
            if (path.EndsWith(Extension, StringComparison.Ordinal))
                files.Add(file);
            else if (file.Name.Contains(".tmp-", StringComparison.Ordinal) &&
                file.LastWriteTimeUtc < DateTime.UtcNow.AddHours(-1))
                TryDelete(path); // abandoned by an interrupted writer
        }

        // Most recently used first; the path breaks ties deterministically.
        files.Sort((left, right) =>
        {
            int order = right.LastWriteTimeUtc.CompareTo(left.LastWriteTimeUtc);
            return order != 0 ? order : string.CompareOrdinal(left.FullName, right.FullName);
        });
        int kept = 0;
        long keptBytes = 0;
        int evicted = 0;
        long evictedBytes = 0;
        foreach (FileInfo file in files)
        {
            if (kept < MaximumEntries && keptBytes + file.Length <= MaximumBytes)
            {
                kept++;
                keptBytes += file.Length;
            }
            else if (TryDelete(file.FullName))
            {
                evicted++;
                evictedBytes += file.Length;
            }
        }

        return new ChangePortfolioResultCacheTrim(kept, keptBytes, evicted, evictedBytes);
    }

    private async Task<ChangePortfolioCachedResult?> TryLoadAsync(string key, CancellationToken cancellationToken)
    {
        string path = PathFor(key);
        FileInfo file = new(path);
        if (!file.Exists)
        {
            Interlocked.Increment(ref _misses);
            return null;
        }

        try
        {
            if (file.Length is <= 0 or > MaximumEntryBytes)
                throw new InvalidDataException("Result cache entry size is outside its bound.");
            string json;
            await using (FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920, useAsync: true))
            await using (GZipStream gzip = new(stream, CompressionMode.Decompress))
            using (StreamReader reader = new(gzip, new UTF8Encoding(false, true)))
            {
                json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            }

            ChangePortfolioResultCacheEntry entry = ContractJson.Deserialize<ChangePortfolioResultCacheEntry>(json);
            if (entry.FormatVersion != FormatVersion || entry.Key != key || !entry.Result.IsValid())
                throw new InvalidDataException("Result cache entry does not match its key.");
            Touch(path);
            Interlocked.Increment(ref _hits);
            return entry.Result;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            JsonException or InvalidDataException or ArgumentException or InvalidOperationException or
            NotSupportedException)
        {
            Interlocked.Increment(ref _misses);
            Interlocked.Increment(ref _rejected);
            return null;
        }
    }

    private async Task StoreAsync(string key, ChangePortfolioCachedResult result, CancellationToken cancellationToken)
    {
        string path = PathFor(key);
        string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            if (!result.IsValid())
                throw new InvalidOperationException("Only one valid result may be cached per key.");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string json = ContractJson.SerializeDocument(
                new ChangePortfolioResultCacheEntry { Key = key, Result = result }, compact: true);
            await using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81_920, useAsync: true))
            await using (GZipStream gzip = new(stream, CompressionLevel.Fastest))
            await using (StreamWriter writer = new(gzip, new UTF8Encoding(false)))
            {
                await writer.WriteAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
            }

            if (new FileInfo(temporary).Length > MaximumEntryBytes)
                throw new InvalidOperationException("Result cache entry exceeds its bound.");
            File.Move(temporary, path, overwrite: true);
            Touch(path);
            Interlocked.Increment(ref _writes);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
            InvalidOperationException or ArgumentException or NotSupportedException)
        {
            Interlocked.Increment(ref _writeFailures);
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    private string PathFor(string key)
    {
        if (!key.StartsWith("sha256:", StringComparison.Ordinal) || key.Length != 71 ||
            key.AsSpan(7).ContainsAnyExcept(s_hex))
            throw new ArgumentException("Result cache keys must be lowercase SHA-256 digests.", nameof(key));
        string hex = key[7..];
        return Path.Combine(Root, hex[..2], hex + Extension);
    }

    // Strictly increasing stamps keep LRU order exact within one run even when the
    // file-system clock resolution is coarse.
    private void Touch(string path)
    {
        DateTime stamp;
        lock (_clockGate)
        {
            DateTime now = DateTime.UtcNow;
            _lastStamp = now > _lastStamp ? now : _lastStamp.AddTicks(1);
            stamp = _lastStamp;
        }

        try
        {
            File.SetLastWriteTimeUtc(path, stamp);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Recency is advisory; a missed touch only changes eviction order.
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            if (!File.Exists(path))
                return false;
            File.Delete(path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private sealed class View(ChangePortfolioResultCacheStore store, string context) : IChangePortfolioResultCache
    {
        public string Context { get; } = context;

        public Task<ChangePortfolioCachedResult?> TryLoadAsync(string key, CancellationToken cancellationToken) =>
            store.TryLoadAsync(key, cancellationToken);

        public Task StoreAsync(string key, ChangePortfolioCachedResult result, CancellationToken cancellationToken) =>
            store.StoreAsync(key, result, cancellationToken);
    }
}

internal sealed record ChangePortfolioResultCacheTrim(int Entries, long Bytes, int Evicted, long EvictedBytes);
