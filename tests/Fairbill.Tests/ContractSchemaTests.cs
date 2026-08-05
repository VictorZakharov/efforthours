using System.Text.Json;
using Fairbill.Contracts;

namespace Fairbill.Tests;

public sealed class ContractSchemaTests
{
    [Fact]
    public void CatalogContainsAllV1SchemasWithUniqueIds()
    {
        Assert.Equal(6, ContractSchemaCatalog.Names.Count);
        HashSet<string> ids = new(StringComparer.Ordinal);

        foreach (string name in ContractSchemaCatalog.Names)
        {
            using JsonDocument schema = JsonDocument.Parse(ContractSchemaCatalog.Read(name));
            string id = schema.RootElement.GetProperty("$id").GetString()!;
            string draft = schema.RootElement.GetProperty("$schema").GetString()!;

            Assert.True(ids.Add(id), $"Duplicate schema ID: {id}");
            Assert.Equal("https://json-schema.org/draft/2020-12/schema", draft);
        }
    }
}
