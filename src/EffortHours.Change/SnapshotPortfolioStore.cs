using System.Text;
using System.Text.Json;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed class SnapshotPortfolioStore(string directory, long maximumBytes = 512L * 1024 * 1024)
{
    private static readonly System.Buffers.SearchValues<char> s_myChars = System.Buffers.SearchValues.Create("0123456789abcdef");

    public string DirectoryPath { get; } = Path.GetFullPath(directory);
    private readonly Dictionary<string, SnapshotMeasurementReceipt> _imported = new(StringComparer.Ordinal);
    private readonly Lock _writeGate = new();
    private long _reservedBytes;
    private int _invalidations;
    public int Invalidations => Volatile.Read(ref _invalidations);

    public async Task<FileStream> AcquireLockAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(DirectoryPath);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await Task.FromResult(new FileStream(Path.Combine(DirectoryPath, "run.lock"), FileMode.OpenOrCreate,
                FileAccess.ReadWrite, FileShare.None)).ConfigureAwait(false);
        }
        catch (IOException) { throw new InvalidOperationException("Another snapshot portfolio run holds the checkpoint lock."); }
    }

    public async Task<T?> LoadAsync<T>(string kind, string key, CancellationToken cancellationToken) where T : class
    {
        string path = EntryPath(kind, key);
        if (!File.Exists(path)) return null;
        try
        {
            if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new InvalidDataException("Checkpoint entry exceeds its byte budget.");
            string json = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            if (typeof(T) == typeof(SnapshotMeasurementReceipt) &&
                !ContractSchemaValidator.Validate("snapshot-measurement-receipt.schema.json", json).IsValid)
                throw new InvalidDataException("Checkpoint receipt failed schema validation.");
            T value = ContractJson.Deserialize<T>(json);
            if (value is SnapshotMeasurementReceipt receipt) SnapshotPortfolioValidation.Validate(receipt);
            if (value is SnapshotReceiptReference reference && (reference.ReceiptId is null ||
                reference.ReceiptId.Length != 71 || !reference.ReceiptId.StartsWith("sha256:", StringComparison.Ordinal) ||
                reference.ReceiptId.AsSpan(7).ContainsAnyExcept(s_myChars)))
                throw new InvalidDataException("Checkpoint measurement reference is incomplete or corrupt.");
            if (value is SnapshotStoredBinding binding && (binding.Areas is null || binding.WholeReceiptId is null || binding.Digest != BindingDigest(binding)))
                throw new InvalidDataException("Checkpoint binding failed integrity validation.");
            return value;
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidDataException)
        {
            Interlocked.Increment(ref _invalidations);
            return null;
        }
    }

    public async Task SaveAsync<T>(string kind, string key, T value, CancellationToken cancellationToken)
    {
        string json = ContractJson.SerializeDocument(value);
        long bytes = Encoding.UTF8.GetByteCount(json);
        string path = EntryPath(kind, key);
        lock (_writeGate)
        {
            long retained = Directory.Exists(DirectoryPath) ? Directory.EnumerateFiles(DirectoryPath, "*.json", SearchOption.AllDirectories)
                .Sum(p => new FileInfo(p).Length) : 0;
            long prior = File.Exists(path) ? new FileInfo(path).Length : 0;
            if (bytes > 32 * 1024 * 1024 || retained + _reservedBytes + bytes - prior > maximumBytes)
                throw new InvalidOperationException("Checkpoint byte budget exhausted; preserve receipts and choose a larger explicit budget.");
            _reservedBytes += bytes;
        }
        try { await AtomicWriteAsync(path, json, cancellationToken).ConfigureAwait(false); }
        finally { lock (_writeGate) _reservedBytes -= bytes; }
    }

    public async Task<SnapshotMeasurementReceipt?> ReceiptAsync(string id, CancellationToken cancellationToken)
    {
        if (_imported.TryGetValue(id, out SnapshotMeasurementReceipt? receipt)) return receipt;
        return await LoadAsync<SnapshotMeasurementReceipt>("receipts", id, cancellationToken).ConfigureAwait(false);
    }

    public async Task ImportAsync(SnapshotPortfolioReport report, CancellationToken cancellationToken)
    {
        SnapshotPortfolioValidation.Validate(report);
        foreach (SnapshotMeasurementReceipt receipt in report.Receipts)
        {
            _imported[receipt.Id] = receipt;
            await SaveAsync("receipts", receipt.Id, receipt, cancellationToken).ConfigureAwait(false);
            string key = MeasurementKey(receipt.InputDigest, receipt.Measurement, receipt.SelectedFileCount, receipt.ContextFileCount);
            await SaveAsync("measurements", key, new SnapshotReceiptReference(receipt.Id), cancellationToken).ConfigureAwait(false);
        }
    }

    public static string MeasurementKey(string inputDigest, MeasurementIdentity identity, int selected, int context) =>
        SnapshotMeasurementIdentity.Digest(new { inputDigest, identity, selected, context });
    public static string BindingDigest(SnapshotStoredBinding binding) => SnapshotMeasurementIdentity.Digest(binding with { Digest = "" });

    public static async Task AtomicWriteAsync(string path, string text, CancellationToken cancellationToken,
        int maximumBytes = 32 * 1024 * 1024)
    {
        if (Encoding.UTF8.GetByteCount(text) > maximumBytes) throw new InvalidOperationException("Output byte budget exceeded.");
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        string temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, text, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, full, overwrite: true);
        }
        finally { File.Delete(temporary); }
    }

    private string EntryPath(string kind, string key) => Path.Combine(DirectoryPath, kind,
        SnapshotMeasurementIdentity.Digest(key)[7..] + ".json");
}

public sealed record SnapshotReceiptReference(string ReceiptId);
public sealed record SnapshotStoredBinding(string Key, string Digest, string WholeReceiptId,
    IReadOnlyList<SnapshotAreaResult> Areas);
