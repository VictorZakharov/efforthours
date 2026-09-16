using System.Security.Cryptography;
using System.Text;
using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static class ReviewedVendorManifestValidation
{
    public static IReadOnlyList<string> Validate(ReviewedVendorManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        List<string> errors = [];
        if (manifest.SchemaVersion != ContractVersions.V1 || manifest.ProtocolVersion != ReviewedVendorManifest.Protocol)
            errors.Add("Unsupported reviewed vendor manifest version.");
        if (manifest.Files is null || manifest.Files.Count is < 1 or > ReviewedVendorManifest.MaximumEntries)
        {
            errors.Add("A reviewed vendor manifest requires between 1 and 4096 files.");
            return errors;
        }
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach (ReviewedVendorEntry entry in manifest.Files)
        {
            if (entry is null) { errors.Add("A vendor entry cannot be null."); continue; }
            if (!IsPath(entry.Path) || !paths.Add(entry.Path)) errors.Add("Vendor paths must be unique normalized relative file paths.");
            if (entry.Sha256 is not { Length: 64 } || entry.Sha256.Any(value => !char.IsAsciiDigit(value) && value is not (>= 'a' and <= 'f')))
                errors.Add("Vendor content hashes must be lowercase SHA-256 values.");
            if (entry.Classification != "third-party-body") errors.Add("Only reviewed whole third-party bodies may be excluded.");
            if (!IsText(entry.Library, 256) || !IsText(entry.Provenance, 1024) || !IsText(entry.Rationale, 2048))
                errors.Add("Vendor entries require bounded library, provenance, and rationale fields without control characters.");
        }
        if (errors.Count == 0 && Encoding.UTF8.GetByteCount(ContractJson.SerializeCompact(manifest)) > ReviewedVendorManifest.MaximumBytes)
            errors.Add("The reviewed vendor manifest exceeds the 1-MiB bound.");
        return errors;
    }

    public static string ComputeDigest(ReviewedVendorManifest manifest)
    {
        IReadOnlyList<string> errors = Validate(manifest);
        if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors), nameof(manifest));
        ReviewedVendorManifest canonical = manifest with { Files = [.. manifest.Files.OrderBy(entry => entry.Path, StringComparer.Ordinal)] };
        return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ContractJson.SerializeCompact(canonical)))).ToLowerInvariant();
    }

    private static bool IsText(string? value, int maximum) => !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximum && !value.Any(char.IsControl);

    private static bool IsPath(string? value) => IsText(value, 1024) && !value!.StartsWith('/') &&
        !value.Contains('\\') && !value.Contains(':') && !value.Contains('*') && !value.Contains('?') &&
        value.Split('/').All(segment => segment.Length > 0 && segment is not ("." or ".."));
}
