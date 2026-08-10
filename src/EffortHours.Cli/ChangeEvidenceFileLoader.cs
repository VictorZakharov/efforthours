using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Cli;

internal static class ChangeEvidenceFileLoader
{
    public static async Task<RepositoryEvidence> LoadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Repository evidence file was not found: {path}", path);
        }

        string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(
            SchemaNames.RepositoryEvidence,
            json);
        if (!schema.IsValid)
        {
            throw new InvalidOperationException(
                "Repository evidence does not satisfy the v1 schema: " +
                string.Join(" ", schema.Errors));
        }

        RepositoryEvidence evidence = ContractJson.Deserialize<RepositoryEvidence>(json);
        IReadOnlyList<string> semanticErrors = ContractValidation.Validate(evidence);
        if (semanticErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "Repository evidence is semantically invalid: " +
                string.Join(" ", semanticErrors));
        }

        return evidence;
    }
}
