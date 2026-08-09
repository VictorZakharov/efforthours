using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace EffortHours.Analysis;

internal sealed record FileInspection(
    long Bytes,
    long Lines,
    string Sha256,
    bool IsBinary,
    string SampleText)
{
    public static async Task<FileInspection> CreateAsync(
        IRepositoryFileSystem fileSystem,
        string path,
        RepositoryScanOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(options);

        if (options.FileReadBufferSize < 4 * 1024)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The file read buffer must be at least 4096 bytes.");
        }

        if (options.TextSampleSize < 256)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "The text sample must be at least 256 bytes.");
        }

        byte[] readBuffer = ArrayPool<byte>.Shared.Rent(options.FileReadBufferSize);
        byte[] sampleBuffer = ArrayPool<byte>.Shared.Rent(options.TextSampleSize);
        int sampleLength = 0;
        long bytes = 0;
        long lineFeeds = 0;
        byte previousByte = 0;
        byte lastByte = 0;

        try
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using Stream stream = fileSystem.OpenRead(path, options.FileReadBufferSize);

            while (true)
            {
                int count = await stream.ReadAsync(
                    readBuffer.AsMemory(0, options.FileReadBufferSize),
                    cancellationToken).ConfigureAwait(false);
                if (count == 0)
                {
                    break;
                }

                hash.AppendData(readBuffer, 0, count);
                if (sampleLength < options.TextSampleSize)
                {
                    int sampleCount = Math.Min(count, options.TextSampleSize - sampleLength);
                    readBuffer.AsSpan(0, sampleCount).CopyTo(sampleBuffer.AsSpan(sampleLength));
                    sampleLength += sampleCount;
                }

                for (int index = 0; index < count; index++)
                {
                    byte currentByte = readBuffer[index];
                    if (currentByte == (byte)'\n')
                    {
                        lineFeeds++;
                    }

                    previousByte = lastByte;
                    lastByte = currentByte;
                }

                bytes += count;
            }

            ReadOnlySpan<byte> sample = sampleBuffer.AsSpan(0, sampleLength);
            TextEncodingKind encoding = DetectEncoding(sample);
            bool isBinary = IsBinaryContent(path, sample, encoding);
            bool endsWithLineFeed = EndsWithLineFeed(encoding, previousByte, lastByte, bytes);
            long lines = isBinary || bytes == 0
                ? 0
                : lineFeeds + (endsWithLineFeed ? 0 : 1);
            string sampleText = isBinary ? string.Empty : DecodeSample(sample, encoding);

            return new FileInspection(
                bytes,
                lines,
                Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
                isBinary,
                sampleText);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(readBuffer);
            ArrayPool<byte>.Shared.Return(sampleBuffer);
        }
    }

    private static TextEncodingKind DetectEncoding(ReadOnlySpan<byte> sample)
    {
        if (sample.Length >= 4 &&
            sample[0] == 0xff &&
            sample[1] == 0xfe &&
            sample[2] == 0x00 &&
            sample[3] == 0x00)
        {
            return TextEncodingKind.Utf32LittleEndian;
        }

        if (sample.Length >= 4 &&
            sample[0] == 0x00 &&
            sample[1] == 0x00 &&
            sample[2] == 0xfe &&
            sample[3] == 0xff)
        {
            return TextEncodingKind.Utf32BigEndian;
        }

        if (sample.Length >= 2 && sample[0] == 0xff && sample[1] == 0xfe)
        {
            return TextEncodingKind.Utf16LittleEndian;
        }

        if (sample.Length >= 2 && sample[0] == 0xfe && sample[1] == 0xff)
        {
            return TextEncodingKind.Utf16BigEndian;
        }

        return TextEncodingKind.Utf8OrSingleByte;
    }

    private static bool IsBinaryContent(
        string path,
        ReadOnlySpan<byte> sample,
        TextEncodingKind encoding)
    {
        if (FileClassifier.HasKnownBinaryExtension(path))
        {
            return true;
        }

        if (encoding is not TextEncodingKind.Utf8OrSingleByte)
        {
            return false;
        }

        int suspiciousControls = 0;
        foreach (byte value in sample)
        {
            if (value == 0)
            {
                return true;
            }

            if (value < 0x20 && value is not (0x09 or 0x0a or 0x0c or 0x0d))
            {
                suspiciousControls++;
            }
        }

        return sample.Length > 0 && suspiciousControls * 100 / sample.Length > 5;
    }

    private static bool EndsWithLineFeed(
        TextEncodingKind encoding,
        byte previousByte,
        byte lastByte,
        long bytes)
    {
        if (bytes == 0)
        {
            return false;
        }

        return encoding switch
        {
            TextEncodingKind.Utf16LittleEndian => previousByte == 0x0a && lastByte == 0x00,
            TextEncodingKind.Utf16BigEndian => previousByte == 0x00 && lastByte == 0x0a,
            _ => lastByte == 0x0a,
        };
    }

    private static string DecodeSample(ReadOnlySpan<byte> sample, TextEncodingKind encoding)
    {
        byte[] bytes = sample.ToArray();
        return encoding switch
        {
            TextEncodingKind.Utf16LittleEndian => Encoding.Unicode.GetString(bytes),
            TextEncodingKind.Utf16BigEndian => Encoding.BigEndianUnicode.GetString(bytes),
            TextEncodingKind.Utf32LittleEndian => Encoding.UTF32.GetString(bytes),
            TextEncodingKind.Utf32BigEndian => new UTF32Encoding(true, true).GetString(bytes),
            _ => Encoding.UTF8.GetString(bytes),
        };
    }

    private enum TextEncodingKind
    {
        Utf8OrSingleByte,
        Utf16LittleEndian,
        Utf16BigEndian,
        Utf32LittleEndian,
        Utf32BigEndian,
    }
}
