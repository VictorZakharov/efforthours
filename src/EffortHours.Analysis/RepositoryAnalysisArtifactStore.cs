using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EffortHours.Analysis;

/// <summary>Optional private persistence for the existing immutable artifact cache.</summary>
public interface IRepositoryAnalysisArtifactStore
{
    public bool TryLoad<T>(string key, out T value) where T : class;
    public void Save<T>(string key, T value) where T : class;
}

public sealed class PhysicalRepositoryAnalysisArtifactStore : IRepositoryAnalysisArtifactStore
{
    private const int MaximumEntryBytes = 1024 * 1024;
    private readonly string _directory;
    private readonly string _context;
    private readonly Lock _gate = new();
    // Least recently used first. Loads and saves move an entry to the end; trimming evicts from the front.
    private readonly Dictionary<string, (long Bytes, LinkedListNode<string> Node)> _retained = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _recency = new();
    // Entries whose file write time already reflects this process's use.
    private readonly HashSet<string> _current = new(StringComparer.Ordinal);
    private readonly Action<long>? _bytesChanged;
    private long _bytes;
    private int _invalidations;
    private int _evictions;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
    };

    /// <param name="bytesChanged">Receives retained-byte deltas for writes, replacements, and evictions.</param>
    public PhysicalRepositoryAnalysisArtifactStore(string directory, string semanticContext,
        Action<long>? bytesChanged = null)
    {
        _directory = Path.GetFullPath(directory);
        _context = semanticContext;
        _bytesChanged = bytesChanged;
        Directory.CreateDirectory(_directory);
        // File write times carry recency across runs; names break ties deterministically.
        foreach (FileInfo file in new DirectoryInfo(_directory).EnumerateFiles("*.json")
            .OrderBy(f => f.LastWriteTimeUtc).ThenBy(f => f.Name, StringComparer.Ordinal))
        {
            _retained[file.Name] = (file.Length, _recency.AddLast(file.Name));
            _bytes += file.Length;
        }
        Trim();
    }

    public int Invalidations => Volatile.Read(ref _invalidations);
    public int Evictions => _evictions;

    // Reads, parsing, and temporary writes run outside the gate so concurrent analysis workers do not
    // serialize on disk I/O; the gate covers retention bookkeeping and the atomic replacement only.
    public bool TryLoad<T>(string key, out T value) where T : class
    {
        value = null!;
        if (!Supported(typeof(T), key)) return false;
        string path = EntryPath<T>(key);
        string filename = Path.GetFileName(path);
        lock (_gate)
        {
            if (!_retained.ContainsKey(filename)) return false;
        }
        try
        {
            string text;
            using (FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (stream.Length > MaximumEntryBytes) throw new InvalidDataException("Artifact exceeds entry budget.");
                using StreamReader reader = new(stream, Encoding.UTF8);
                text = reader.ReadToEnd();
            }
            Envelope envelope = JsonSerializer.Deserialize<Envelope>(text)
                ?? throw new InvalidDataException("Empty artifact.");
            if (envelope.Key != _context + "\0" + key || envelope.Digest != Hash(envelope.Payload))
                throw new InvalidDataException("Artifact integrity/context mismatch.");
            value = JsonSerializer.Deserialize<T>(envelope.Payload, JsonOptions)
                ?? throw new InvalidDataException("Empty artifact payload.");
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (Exception e) when (e is JsonException or IOException or InvalidDataException or NotSupportedException)
        {
            Interlocked.Increment(ref _invalidations);
            return false;
        }
        bool refresh;
        lock (_gate) refresh = Touch(filename) && _current.Add(filename);
        if (refresh)
        {
            // Best-effort recency for later runs; a failure only affects eviction order.
            try { File.SetLastWriteTimeUtc(path, DateTime.UtcNow); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
        return true;
    }

    public void Save<T>(string key, T value) where T : class
    {
        if (!Supported(typeof(T), key)) return;
        string payload = JsonSerializer.Serialize(value, JsonOptions);
        string json = JsonSerializer.Serialize(new Envelope(_context + "\0" + key, Hash(payload), payload));
        int bytes = Encoding.UTF8.GetByteCount(json);
        if (bytes > MaximumEntryBytes) return;
        string path = EntryPath<T>(key);
        string filename = Path.GetFileName(path);
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, json, new UTF8Encoding(false));
            lock (_gate)
            {
                File.Move(temporary, path, overwrite: true);
                long prior = 0;
                if (_retained.TryGetValue(filename, out (long Bytes, LinkedListNode<string> Node) existing))
                {
                    prior = existing.Bytes;
                    _recency.Remove(existing.Node);
                }
                _retained[filename] = (bytes, _recency.AddLast(filename));
                _current.Add(filename);
                _bytes += bytes - prior;
                _bytesChanged?.Invoke(bytes - prior);
                Trim();
            }
        }
        finally { File.Delete(temporary); }
    }

    private void Trim()
    {
        while (_retained.Count > RepositoryAnalysisArtifactCache.DefaultMaximumEntries || _bytes > 128L * 1024 * 1024)
        {
            string oldest = _recency.First!.Value;
            long bytes = _retained[oldest].Bytes;
            File.Delete(Path.Combine(_directory, oldest));
            _recency.RemoveFirst();
            _retained.Remove(oldest);
            _current.Remove(oldest);
            _bytes -= bytes;
            _bytesChanged?.Invoke(-bytes);
            _evictions++;
        }
    }

    private bool Touch(string filename)
    {
        if (!_retained.TryGetValue(filename, out (long Bytes, LinkedListNode<string> Node) entry)) return false;
        _recency.Remove(entry.Node);
        _recency.AddLast(entry.Node);
        return true;
    }

    private string EntryPath<T>(string key) => Path.Combine(_directory,
        Hash(_context + "\0" + typeof(T).FullName + "\0" + key) + ".json");
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    // Explicitly admit data-only values; never persist syntax trees, runtime types, or project readers.
    private static bool Supported(Type type, string key) => type.FullName is
        "EffortHours.Analysis.RepositoryScanner+ScannedFile" or
        "EffortHours.Analyzers.DotNet.CSharpFileAnalysis" or
        "EffortHours.Analyzers.JavaScript.JavaScriptFileAnalysis" ||
        type == typeof(RepositoryAnalysisContribution) && key.StartsWith("dotnet-razor/", StringComparison.Ordinal);

    private sealed record Envelope(string Key, string Digest, string Payload);
}
