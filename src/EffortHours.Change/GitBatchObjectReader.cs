using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace EffortHours.Change;

internal sealed class GitBatchObjectReader : IAsyncDisposable
{
    internal const int MaximumCachedBlobBytes = 1024 * 1024;
    private const long MaximumCacheBytes = 64L * 1024 * 1024;

    private readonly Dictionary<string, byte[]> _cache = new(StringComparer.Ordinal);
    private readonly Queue<string> _cacheOrder = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Process _process;
    private readonly Task<string> _stderr;
    private long _cacheBytes;
    private bool _faulted;
    private bool _disposed;

    public GitBatchObjectReader(string repositoryPath)
    {
        ProcessStartInfo startInfo = ExternalCommand.CreateStartInfo(
            "git",
            repositoryPath,
            ["cat-file", "--batch"]);
        _process = new Process { StartInfo = startInfo };
        try
        {
            if (!_process.Start())
            {
                throw new ExternalCommandException("git", null, "Could not start Git object reader.");
            }
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            throw new ExternalCommandException(
                "git",
                null,
                $"Could not start Git object reader: {exception.Message}",
                exception);
        }

        _stderr = _process.StandardError.ReadToEndAsync();
    }

    public Stream OpenBlob(string objectId)
    {
        _gate.Wait();
        try
        {
            EnsureUsable();
            if (_cache.TryGetValue(objectId, out byte[]? cached))
            {
                _gate.Release();
                return new MemoryStream(cached, writable: false);
            }

            _process.StandardInput.WriteLine(objectId);
            _process.StandardInput.Flush();
            string header = ReadHeader(_process.StandardOutput.BaseStream);
            long length = ParseBlobLength(header, objectId);
            return new GitBatchBlobStream(this, objectId, _process.StandardOutput.BaseStream, length);
        }
        catch
        {
            Fault();
            _gate.Release();
            throw;
        }
    }

    public async ValueTask<byte[]> ReadBlobAsync(
        string objectId,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureUsable();
            if (_cache.TryGetValue(objectId, out byte[]? cached))
            {
                return cached;
            }

            await _process.StandardInput.WriteLineAsync(objectId.AsMemory(), cancellationToken)
                .ConfigureAwait(false);
            await _process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
            string header = await ReadHeaderAsync(
                _process.StandardOutput.BaseStream,
                cancellationToken).ConfigureAwait(false);
            long length = ParseBlobLength(header, objectId);
            if (length > int.MaxValue)
            {
                throw new InvalidOperationException(
                    $"Git blob '{objectId}' is too large to materialize as one byte array.");
            }

            byte[] content = GC.AllocateUninitializedArray<byte>((int)length);
            await _process.StandardOutput.BaseStream.ReadExactlyAsync(content, cancellationToken)
                .ConfigureAwait(false);
            await ReadTerminatorAsync(_process.StandardOutput.BaseStream, cancellationToken)
                .ConfigureAwait(false);
            Cache(objectId, content);
            return content;
        }
        catch
        {
            Fault();
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _disposed = true;
            try
            {
                _process.StandardInput.Dispose();
            }
            catch (InvalidOperationException)
            {
            }

            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));
            try
            {
                await _process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Fault();
                await _process.WaitForExitAsync().ConfigureAwait(false);
            }

            _ = await _stderr.ConfigureAwait(false);
            _process.Dispose();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    internal void FinishStream(string objectId, byte[]? content, bool faulted)
    {
        if (faulted)
        {
            Fault();
        }
        else if (content is not null)
        {
            Cache(objectId, content);
        }

        _gate.Release();
    }

    private static long ParseBlobLength(string header, string requestedObjectId)
    {
        string[] fields = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length == 2 && fields[1] == "missing")
        {
            throw new InvalidOperationException($"Git object '{requestedObjectId}' is missing locally.");
        }

        if (fields.Length != 3 ||
            !string.Equals(fields[0], requestedObjectId, StringComparison.OrdinalIgnoreCase) ||
            fields[1] != "blob" ||
            !long.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out long length) ||
            length < 0)
        {
            throw new InvalidOperationException(
                $"Git returned an invalid blob header for '{requestedObjectId}'.");
        }

        return length;
    }

    private static string ReadHeader(Stream stream)
    {
        byte[] single = new byte[1];
        List<byte> bytes = [];
        while (true)
        {
            int read = stream.Read(single, 0, 1);
            if (read == 0)
            {
                throw new EndOfStreamException("Git object reader ended before returning a header.");
            }

            if (single[0] == '\n')
            {
                return Encoding.ASCII.GetString([.. bytes]);
            }

            if (bytes.Count >= 256)
            {
                throw new InvalidOperationException("Git object header exceeded its safety bound.");
            }

            bytes.Add(single[0]);
        }
    }

    private static async Task<string> ReadHeaderAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        byte[] single = new byte[1];
        List<byte> bytes = [];
        while (true)
        {
            int read = await stream.ReadAsync(single, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                throw new EndOfStreamException("Git object reader ended before returning a header.");
            }

            if (single[0] == '\n')
            {
                return Encoding.ASCII.GetString([.. bytes]);
            }

            if (bytes.Count >= 256)
            {
                throw new InvalidOperationException("Git object header exceeded its safety bound.");
            }

            bytes.Add(single[0]);
        }
    }

    internal static async Task ReadTerminatorAsync(
        Stream stream,
        CancellationToken cancellationToken)
    {
        byte[] terminator = new byte[1];
        await stream.ReadExactlyAsync(terminator, cancellationToken).ConfigureAwait(false);
        if (terminator[0] != '\n')
        {
            throw new InvalidOperationException("Git blob output was not newline terminated.");
        }
    }

    private void EnsureUsable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_faulted)
        {
            throw new InvalidOperationException("The Git object reader is no longer usable after a stream failure.");
        }
    }

    private void Fault()
    {
        _faulted = true;
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
        }
    }

    private void Cache(string objectId, byte[] content)
    {
        if (content.Length > MaximumCachedBlobBytes || _cache.ContainsKey(objectId))
        {
            return;
        }

        while (_cacheBytes + content.Length > MaximumCacheBytes && _cacheOrder.Count > 0)
        {
            string evicted = _cacheOrder.Dequeue();
            if (_cache.Remove(evicted, out byte[]? value))
            {
                _cacheBytes -= value.Length;
            }
        }

        _cache.Add(objectId, content);
        _cacheOrder.Enqueue(objectId);
        _cacheBytes += content.Length;
    }
}
