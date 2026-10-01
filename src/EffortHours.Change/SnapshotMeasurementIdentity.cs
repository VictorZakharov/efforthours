using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Estimation;

namespace EffortHours.Change;

public static class SnapshotMeasurementIdentity
{
    public static string ImplementationDigest { get; } = ReadImplementationDigest();

    public static MeasurementIdentity Create(EstimationProfile profile, string? ownershipDigest = null) => new()
    {
        Profile = profile,
        ModelDigest = Digest(SeedRuleCatalog.ReadJson()),
        ImplementationDigest = ImplementationDigest,
        OwnershipDigest = ownershipDigest,
    };

    public static string Digest<T>(T value) => Hash(Encoding.UTF8.GetBytes(ContractJson.SerializeCompact(value)));

    public static string Hash(ReadOnlySpan<byte> value) =>
        "sha256:" + Convert.ToHexStringLower(SHA256.HashData(value));

    private static string ReadImplementationDigest()
    {
        using Stream stream = typeof(SnapshotMeasurementIdentity).Assembly.GetManifestResourceStream(
            "EffortHours.Change.MeasurementImplementation.txt")
            ?? throw new InvalidOperationException("The build has no measurement compatibility fingerprint.");
        using StreamReader reader = new(stream, Encoding.UTF8);
        string[] hashes = reader.ReadToEnd().Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        Array.Sort(hashes, StringComparer.Ordinal);
        return Hash(Encoding.UTF8.GetBytes(string.Join('\n', hashes)));
    }

    public static string ProducerVersion => typeof(SnapshotMeasurementIdentity).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
}
