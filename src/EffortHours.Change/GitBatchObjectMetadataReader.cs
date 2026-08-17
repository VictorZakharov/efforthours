using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace EffortHours.Change;

internal sealed record GitObjectMetadataReaderStatistics(
    int Requests,
    int CacheHits,
    int UniqueObjects,
    int CacheEvictions,
    int PeakCachedLengths);

/// <summary>
/// Resolves immutable Git object lengths lazily through one repository-scoped
/// batch-check process. Full-tree inventory discovery therefore need not ask Git
/// to inflate metadata for every blob before analysis scope is known.
/// </summary>
internal sealed class GitBatchObjectMetadataReader : IDisposable
{
    internal const int MaximumCachedLengths = 16_384;

    private readonly Dictionary<string, long> _lengths = new(StringComparer.Ordinal);
    private readonly Queue<string> _order = new();
    private readonly HashSet<string> _seenObjects = new(StringComparer.Ordinal);
    private readonly Process _process;
    private readonly Task<string> _stderr;
    private readonly Lock _gate = new();
    private int _requests;
    private int _cacheHits;
    private int _cacheEvictions;
    private int _peakCachedLengths;
    private bool _disposed;

    public GitBatchObjectMetadataReader(string repositoryPath)
    {
        ProcessStartInfo startInfo = ExternalCommand.CreateStartInfo(
            "git",
            repositoryPath,
            ["cat-file", "--batch-check"]);
        _process = new Process { StartInfo = startInfo };
        try
        {
            if (!_process.Start())
            {
                throw new ExternalCommandException(
                    "git",
                    null,
                    "Could not start Git object metadata reader.");
            }
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            throw new ExternalCommandException(
                "git",
                null,
                $"Could not start Git object metadata reader: {exception.Message}",
                exception);
        }

        _stderr = _process.StandardError.ReadToEndAsync();
    }

    public long GetBlobLength(string objectId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectId);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _requests++;
            _seenObjects.Add(objectId);
            if (_lengths.TryGetValue(objectId, out long cached))
            {
                _cacheHits++;
                return cached;
            }

            _process.StandardInput.WriteLine(objectId);
            _process.StandardInput.Flush();
            string? header = _process.StandardOutput.ReadLine();
            long length = ParseBlobLength(header, objectId);
            while (_lengths.Count >= MaximumCachedLengths)
            {
                _lengths.Remove(_order.Dequeue());
                _cacheEvictions++;
            }

            _lengths.Add(objectId, length);
            _order.Enqueue(objectId);
            _peakCachedLengths = Math.Max(_peakCachedLengths, _lengths.Count);
            return length;
        }
    }

    public GitObjectMetadataReaderStatistics GetStatistics()
    {
        lock (_gate)
        {
            return new GitObjectMetadataReaderStatistics(
                _requests,
                _cacheHits,
                _seenObjects.Count,
                _cacheEvictions,
                _peakCachedLengths);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _process.StandardInput.Dispose();
        }

        if (!_process.WaitForExit(5_000))
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
        }

        string stderr = _stderr.GetAwaiter().GetResult().Trim();
        int exitCode = _process.HasExited ? _process.ExitCode : -1;
        _process.Dispose();
        if (exitCode != 0)
        {
            throw new ExternalCommandException(
                "git",
                exitCode,
                stderr.Length == 0
                    ? "Git object metadata reader failed."
                    : stderr);
        }
    }

    private static long ParseBlobLength(string? header, string requestedObjectId)
    {
        string[] fields = (header ?? string.Empty).Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (fields.Length != 3 ||
            !string.Equals(fields[0], requestedObjectId, StringComparison.OrdinalIgnoreCase) ||
            fields[1] != "blob" ||
            !long.TryParse(
                fields[2],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long length) ||
            length < 0)
        {
            throw new InvalidOperationException(
                "Git returned malformed metadata for a requested snapshot blob.");
        }

        return length;
    }
}
