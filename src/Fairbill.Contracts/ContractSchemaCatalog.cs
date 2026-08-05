using System.Reflection;
using System.Text;
using Fairbill.Contracts.V1;

namespace Fairbill.Contracts;

public static class ContractSchemaCatalog
{
    private const string ResourcePrefix = "Fairbill.Schemas.V1.";

    public static IReadOnlyList<string> Names { get; } =
    [
        SchemaNames.Diagnostic,
        SchemaNames.EstimateReport,
        SchemaNames.RateCard,
        SchemaNames.RepositoryEvidence,
        SchemaNames.RepositoryScanCache,
        SchemaNames.WorkItem,
    ];

    public static string Read(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!Names.Contains(name, StringComparer.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown Fairbill schema name.");
        }

        Assembly assembly = typeof(ContractSchemaCatalog).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(ResourcePrefix + name)
            ?? throw new InvalidOperationException($"Embedded schema '{name}' was not found.");
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}
