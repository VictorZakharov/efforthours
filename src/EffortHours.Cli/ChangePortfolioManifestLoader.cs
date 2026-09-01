using System.Text.Json;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal sealed record ResolvedChangePortfolioManifestItem(
    ChangePortfolioManifestItem Item,
    string? RepositoryPath);

internal static class ChangePortfolioManifestLoader
{
    public static async Task<IReadOnlyList<ResolvedChangePortfolioManifestItem>> LoadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        string json = await File.ReadAllTextAsync(fullPath, cancellationToken).ConfigureAwait(false);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.ChangePortfolioManifest,
            json);
        if (!schema.IsValid)
        {
            throw new JsonException(
                "The change portfolio manifest does not satisfy its JSON Schema: " +
                string.Join(" ", schema.Errors));
        }

        ChangePortfolioManifest manifest = ContractJson.Deserialize<ChangePortfolioManifest>(json);
        IReadOnlyList<string> errors = ContractValidation.Validate(manifest);
        if (errors.Count > 0)
        {
            throw new JsonException(
                "The change portfolio manifest is semantically invalid: " + string.Join(" ", errors));
        }

        string directory = Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        return [.. manifest.Items.Select(item => new ResolvedChangePortfolioManifestItem(
            item,
            item.RepositoryPath is null ? null : Path.GetFullPath(item.RepositoryPath, directory)))];
    }
}
