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
    private readonly SortedDictionary<string, long> _retained = new(StringComparer.Ordinal);
    private long _bytes;
    private int _invalidations;
    private int _evictions;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true,
        PreferredObjectCreationHandling = JsonObjectCreationHandling.Populate,
    };

    public PhysicalRepositoryAnalysisArtifactStore(string directory, string semanticContext)
    {
        _directory = Path.GetFullPath(directory);
        _context = semanticContext;
        Directory.CreateDirectory(_directory);
        foreach (string path in Directory.EnumerateFiles(_directory, "*.json"))
        {
            FileInfo file = new(path);
            _retained[Path.GetFileName(path)] = file.Length;
            _bytes += file.Length;
        }
        Trim();
    }

    public int Invalidations => _invalidations;
    public int Evictions => _evictions;

    public bool TryLoad<T>(string key, out T value) where T : class
    {
        value = null!;
        if (!Supported(typeof(T), key)) return false;
        string path = EntryPath<T>(key);
        lock (_gate)
        {
            if (!File.Exists(path)) return false;
            try
            {
                if (new FileInfo(path).Length > MaximumEntryBytes) throw new InvalidDataException("Artifact exceeds entry budget.");
                Envelope envelope = JsonSerializer.Deserialize<Envelope>(File.ReadAllText(path, Encoding.UTF8))
                    ?? throw new InvalidDataException("Empty artifact.");
                if (envelope.Key != _context + "\0" + key || envelope.Digest != Hash(envelope.Payload))
                    throw new InvalidDataException("Artifact integrity/context mismatch.");
                value = JsonSerializer.Deserialize<T>(envelope.Payload, JsonOptions)
                    ?? throw new InvalidDataException("Empty artifact payload.");
                return true;
            }
            catch (Exception e) when (e is JsonException or IOException or InvalidDataException or NotSupportedException)
            {
                _invalidations++;
                return false;
            }
        }
    }

    public void Save<T>(string key, T value) where T : class
    {
        if (!Supported(typeof(T), key)) return;
        string payload = JsonSerializer.Serialize(value, JsonOptions);
        string json = JsonSerializer.Serialize(new Envelope(_context + "\0" + key, Hash(payload), payload));
        int bytes = Encoding.UTF8.GetByteCount(json);
        if (bytes > MaximumEntryBytes) return;
        string path = EntryPath<T>(key);
        lock (_gate)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                File.Move(temporary, path, overwrite: true);
                string filename = Path.GetFileName(path);
                if (_retained.TryGetValue(filename, out long prior)) _bytes -= prior;
                _retained[filename] = bytes;
                _bytes += bytes;
                Trim();
            }
            finally { File.Delete(temporary); }
        }
    }

    private void Trim()
    {
        while (_retained.Count > RepositoryAnalysisArtifactCache.DefaultMaximumEntries || _bytes > 128L * 1024 * 1024)
        {
            KeyValuePair<string, long> last = _retained.Last();
            File.Delete(Path.Combine(_directory, last.Key));
            _retained.Remove(last.Key);
            _bytes -= last.Value;
            _evictions++;
        }
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
