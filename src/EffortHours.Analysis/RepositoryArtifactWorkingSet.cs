using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EffortHours.Analysis;

public sealed record RepositoryArtifactWorkingSetStatistics(
    int Hits, int Replacements, int Evictions, int Entries, int PeakEntries,
    long ChargedBytes, long PeakChargedBytes, int EntryLimit, long ByteLimit);

/// <summary>
/// Invocation-local current file artifacts. Slots replace obsolete versions;
/// reuse still requires the entire immutable analyzer/context key to match.
/// Retention has both an entry limit and a conservative serialized-size charge.
/// Values are never written to disk and serialization is only a size measurement.
/// </summary>
public sealed class RepositoryArtifactWorkingSet
{
    public const int DefaultMaximumEntries = 65_536;
    public const long DefaultMaximumChargedBytes = 512L * 1024 * 1024;
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly SortedSet<string> _ranks = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();
    private readonly int _maximumEntries;
    private readonly long _maximumBytes;
    private long _bytes;
    private long _peakBytes;
    private int _peakEntries;
    private int _hits;
    private int _replacements;
    private int _evictions;

    public RepositoryArtifactWorkingSet(int maximumEntries = DefaultMaximumEntries,
        long maximumChargedBytes = DefaultMaximumChargedBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumEntries);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumChargedBytes);
        _maximumEntries = maximumEntries;
        _maximumBytes = maximumChargedBytes;
    }

    public bool TryGet<T>(string slot, string key, out T value) where T : class
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(slot, out Entry? entry) && entry.Key == key)
            {
                if (entry.Value is not T typed)
                    throw new InvalidOperationException("Working-set artifact type mismatch.");
                _hits++;
                value = typed;
                return true;
            }
            value = null!;
            return false;
        }
    }

    public void Add<T>(string slot, string key, T value) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slot);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);
        lock (_gate)
        {
            if (_entries.TryGetValue(slot, out Entry? same) && same.Key == key) return;
        }
        long charge;
        try
        {
            using ChargeStream stream = new(_maximumBytes / 4);
            JsonSerializer.Serialize(stream, value);
            charge = checked(1024 + 4 * (stream.Count + 2L * (slot.Length + key.Length)));
        }
        catch (Exception exception) when (exception is ChargeExceededException or
            NotSupportedException or JsonException)
        {
            // Optional retention cannot make an otherwise supported analysis fail.
            return;
        }
        if (charge > _maximumBytes) return;
        string rank = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(slot))) + "\0" + slot;
        lock (_gate)
        {
            if (_entries.Remove(slot, out Entry? previous))
            {
                _bytes -= previous.Charge;
                _ranks.Remove(previous.Rank);
                _replacements++;
            }
            _entries.Add(slot, new(key, value, charge, rank));
            _ranks.Add(rank);
            _bytes += charge;
            while (_entries.Count > _maximumEntries || _bytes > _maximumBytes)
            {
                string largest = _ranks.Max!;
                string evictedSlot = largest[65..];
                Entry evicted = _entries[evictedSlot];
                _entries.Remove(evictedSlot);
                _ranks.Remove(largest);
                _bytes -= evicted.Charge;
                _evictions++;
            }
            _peakEntries = Math.Max(_peakEntries, _entries.Count);
            _peakBytes = Math.Max(_peakBytes, _bytes);
        }
    }

    public RepositoryArtifactWorkingSetStatistics GetStatistics()
    {
        lock (_gate)
            return new(_hits, _replacements, _evictions, _entries.Count, _peakEntries,
                _bytes, _peakBytes, _maximumEntries, _maximumBytes);
    }

    private sealed record Entry(string Key, object Value, long Charge, string Rank);
    private sealed class ChargeExceededException : Exception;
    private sealed class ChargeStream(long maximum) : Stream
    {
        public long Count { get; private set; }
        public override void Write(byte[] buffer, int offset, int count) => Advance(count);
        public override void Write(ReadOnlySpan<byte> buffer) => Advance(buffer.Length);
        private void Advance(int count)
        {
            Count += count;
            if (Count > maximum) throw new ChargeExceededException();
        }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => Count;
        public override long Position { get => Count; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
