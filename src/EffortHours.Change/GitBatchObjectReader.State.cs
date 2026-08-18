using System.ComponentModel;

namespace EffortHours.Change;

internal sealed partial class GitBatchObjectReader
{
    internal GitObjectReaderStatistics GetStatistics()
    {
        lock (_stateGate)
        {
            return new GitObjectReaderStatistics
            {
                Requests = _requests,
                CacheHits = _cacheHits,
                CacheEvictions = _cacheEvictions,
                UniqueObjects = _seenObjects.Count,
                RequestedBytes = _requestedBytes,
                CacheHitBytes = _cacheHitBytes,
                ReadBytes = _readBytes,
                UniqueObjectBytes = _uniqueObjectBytes,
                RetainedCacheBytes = _cacheBytes,
                PeakCachedBytes = _peakCacheBytes,
                ProcessCpuTime = ReadProcessCpuTime(),
                ProcessOccupiedTime = Duration(
                    Interlocked.Read(ref _processOccupiedTimestamp)),
                ProcessWaitTime = Duration(Interlocked.Read(ref _processWaitTimestamp)),
            };
        }
    }

    private TimeSpan ReadProcessCpuTime()
    {
        try
        {
            return _process.TotalProcessorTime;
        }
        catch (InvalidOperationException)
        {
            return TimeSpan.Zero;
        }
    }

    private static TimeSpan Duration(long timestamp) => TimeSpan.FromSeconds(
        (double)timestamp / System.Diagnostics.Stopwatch.Frequency);

    private bool TryGetCachedBlob(
        string objectId,
        bool recordMiss,
        out byte[] content)
    {
        lock (_stateGate)
        {
            EnsureUsable();
            if (_cache.TryGetValue(objectId, out byte[]? cached) && cached is not null)
            {
                content = cached;
                _requests++;
                _cacheHits++;
                RecordBlobLengthCore(objectId, content.Length, cacheHit: true);
                return true;
            }

            if (recordMiss)
            {
                _requests++;
            }

            content = null!;
            return false;
        }
    }

    private void EnsureUsable()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_faulted)
        {
            throw new InvalidOperationException(
                "The Git object reader is no longer usable after a stream failure.");
        }
    }

    private void Fault()
    {
        lock (_stateGate)
        {
            _faulted = true;
        }

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
        lock (_stateGate)
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
                    _cacheEvictions++;
                }
            }

            _cache.Add(objectId, content);
            _cacheOrder.Enqueue(objectId);
            _cacheBytes += content.Length;
            _peakCacheBytes = Math.Max(_peakCacheBytes, _cacheBytes);
        }
    }

    private void RecordBlobLength(string objectId, long length, bool cacheHit)
    {
        lock (_stateGate)
        {
            RecordBlobLengthCore(objectId, length, cacheHit);
        }
    }

    private void RecordBlobLengthCore(string objectId, long length, bool cacheHit)
    {
        _requestedBytes += length;
        if (cacheHit)
        {
            _cacheHitBytes += length;
        }
        else
        {
            _readBytes += length;
        }

        if (_seenObjects.Add(objectId))
        {
            _uniqueObjectBytes += length;
        }
    }
}
