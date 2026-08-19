namespace EffortHours.Change;

internal sealed class GitBatchBlobStream : Stream
{
    private readonly GitBatchObjectReader _owner;
    private readonly string _objectId;
    private readonly Stream _source;
    private readonly long _length;
    private readonly MemoryStream? _capture;
    private readonly long _acquiredTimestamp;
    private long _remaining;
    private bool _finished;
    private bool _disposed;

    public GitBatchBlobStream(
        GitBatchObjectReader owner,
        string objectId,
        Stream source,
        long length,
        long acquiredTimestamp)
    {
        _owner = owner;
        _objectId = objectId;
        _source = source;
        _length = length;
        _acquiredTimestamp = acquiredTimestamp;
        _remaining = length;
        _capture = length <= GitBatchObjectReader.MaximumCachedBlobBytes
            ? new MemoryStream((int)length)
            : null;
    }

    public override bool CanRead => !_disposed;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => _length;

    public override long Position
    {
        get => _length - _remaining;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count) =>
        Read(buffer.AsSpan(offset, count));

    public override int Read(Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_remaining == 0)
        {
            Finish();
            return 0;
        }

        if (buffer.Length == 0)
        {
            return 0;
        }

        int requested = (int)Math.Min(buffer.Length, _remaining);
        int read = _source.Read(buffer[..requested]);
        RecordRead(buffer[..read], read);
        if (_remaining == 0)
        {
            Finish();
        }

        return read;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_remaining == 0)
        {
            await FinishAsync(cancellationToken).ConfigureAwait(false);
            return 0;
        }

        if (buffer.Length == 0)
        {
            return 0;
        }

        int requested = (int)Math.Min(buffer.Length, _remaining);
        int read;
        try
        {
            read = await _source.ReadAsync(buffer[..requested], cancellationToken)
                .ConfigureAwait(false);
            RecordRead(buffer.Span[..read], read);
            if (_remaining == 0)
            {
                await FinishAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            Fail();
            throw;
        }

        return read;
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            try
            {
                Drain();
            }
            catch
            {
                Fail();
                throw;
            }
            finally
            {
                _disposed = true;
                _capture?.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            try
            {
                await DrainAsync().ConfigureAwait(false);
            }
            catch
            {
                Fail();
                throw;
            }
            finally
            {
                _disposed = true;
                _capture?.Dispose();
            }
        }

        await base.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private void RecordRead(ReadOnlySpan<byte> content, int count)
    {
        if (count == 0)
        {
            Fail();
            throw new EndOfStreamException("Git blob content ended before its declared length.");
        }

        _capture?.Write(content);
        _remaining -= count;
    }

    private void Drain()
    {
        byte[] buffer = new byte[64 * 1024];
        while (_remaining > 0)
        {
            int read = Read(buffer, 0, (int)Math.Min(buffer.Length, _remaining));
            if (read == 0)
            {
                break;
            }
        }

        Finish();
    }

    private async Task DrainAsync()
    {
        byte[] buffer = new byte[64 * 1024];
        while (_remaining > 0)
        {
            int read = await ReadAsync(
                buffer.AsMemory(0, (int)Math.Min(buffer.Length, _remaining)),
                CancellationToken.None).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }
        }

        await FinishAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private void Finish()
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        bool faulted = true;
        try
        {
            if (_source.ReadByte() != '\n')
            {
                throw new InvalidOperationException("Git blob output was not newline terminated.");
            }

            faulted = false;
        }
        finally
        {
            _owner.FinishStream(
                _objectId,
                faulted ? null : _capture?.ToArray(),
                faulted,
                _acquiredTimestamp);
        }
    }

    private async Task FinishAsync(CancellationToken cancellationToken)
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        bool faulted = true;
        try
        {
            await GitBatchObjectReader.ReadTerminatorAsync(_source, cancellationToken).ConfigureAwait(false);
            faulted = false;
        }
        finally
        {
            _owner.FinishStream(
                _objectId,
                faulted ? null : _capture?.ToArray(),
                faulted,
                _acquiredTimestamp);
        }
    }

    private void Fail()
    {
        if (_finished)
        {
            return;
        }

        _finished = true;
        _owner.FinishStream(
            _objectId,
            null,
            faulted: true,
            acquiredTimestamp: _acquiredTimestamp);
    }
}
