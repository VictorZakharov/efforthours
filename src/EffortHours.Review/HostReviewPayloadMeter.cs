using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Review;

internal static class HostReviewPayloadMeter
{
    public static HostReviewPayloadMeasurement MeasureDocument<T>(T document, string payloadText)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(payloadText);

        T decoded = ContractJson.Deserialize<T>(payloadText);
        string digest = Digest(document);
        if (digest != Digest(decoded))
        {
            throw new ArgumentException(
                "The measured payload text does not represent the supplied document.",
                nameof(payloadText));
        }

        return new HostReviewPayloadMeasurement
        {
            Digest = digest,
            Size = MeasureText(payloadText),
        };
    }

    public static HostReviewPayloadTotals MeasureText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        long characters = 0;
        foreach (Rune _ in value.EnumerateRunes())
        {
            characters++;
        }

        return new HostReviewPayloadTotals
        {
            Utf8Bytes = Encoding.UTF8.GetByteCount(value),
            CharacterCount = characters,
            ApproximateTokens = ApproximateTokens(characters),
        };
    }

    public static string Digest<T>(T document)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(document);
        byte[] bytes = Encoding.UTF8.GetBytes(ContractJson.SerializeCompact(document));
        return $"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
    }

    public static HostReviewPayloadTotals Add(params HostReviewPayloadTotals[] payloads)
    {
        long bytes = payloads.Sum(payload => payload.Utf8Bytes);
        long characters = payloads.Sum(payload => payload.CharacterCount);
        return new HostReviewPayloadTotals
        {
            Utf8Bytes = bytes,
            CharacterCount = characters,
            ApproximateTokens = ApproximateTokens(characters),
        };
    }

    public static long ApproximateTokens(long characters) =>
        (long)decimal.Ceiling(characters / 4m);
}
