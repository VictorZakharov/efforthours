using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal static class ReviewedVendorManifestLoader
{
    public static async Task<ReviewedVendorManifest> LoadAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length is < 1 or > ReviewedVendorManifest.MaximumBytes)
            throw new InvalidDataException("A reviewed vendor manifest must be between 1 byte and 1 MiB.");
        byte[] bytes = new byte[(int)stream.Length];
        await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
        string json = new UTF8Encoding(false, true).GetString(bytes);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(SchemaNames.ReviewedVendorManifest, json);
        if (!schema.IsValid) throw new InvalidDataException("The reviewed vendor manifest does not satisfy its schema.");
        ReviewedVendorManifest manifest = ContractJson.Deserialize<ReviewedVendorManifest>(json);
        IReadOnlyList<string> errors = ReviewedVendorManifestValidation.Validate(manifest);
        if (errors.Count > 0) throw new InvalidDataException(string.Join(" ", errors));
        return manifest;
    }
}
