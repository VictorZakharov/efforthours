namespace EffortHours.Analysis;

internal sealed class BoundedReadStream : Stream
{
    private readonly Stream _inner;
    private readonly long _maximumBytes;
    private long _bytesRead;

    public BoundedReadStream(Stream inner, long maximumBytes)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBytes);
        _inner = inner;
        _maximumBytes = maximumBytes;
    }

    public override bool CanRead => _inner.CanRead;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => _bytesRead;
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = _inner.Read(buffer, offset, AllowedCount(count));
        Record(read);
        return read;
    }

    public override int Read(Span<byte> buffer)
    {
        int read = _inner.Read(buffer[..AllowedCount(buffer.Length)]);
        Record(read);
        return read;
    }

    public override async ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        int read = await _inner.ReadAsync(
            buffer[..AllowedCount(buffer.Length)],
            cancellationToken).ConfigureAwait(false);
        Record(read);
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();

    private int AllowedCount(int requested)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(requested);
        if (requested == 0)
        {
            return 0;
        }

        long remaining = _maximumBytes - _bytesRead;
        return remaining < requested
            ? checked((int)(remaining + 1L))
            : requested;
    }

    private void Record(int count)
    {
        if (_bytesRead + count > _maximumBytes)
        {
            throw new InvalidDataException(
                $"The stream exceeded its {_maximumBytes}-byte read limit.");
        }

        _bytesRead += count;
    }
}
