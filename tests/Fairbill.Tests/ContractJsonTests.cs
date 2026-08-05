using System.Text.Json.Nodes;
using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.Tests;

public sealed class ContractJsonTests
{
    [Fact]
    public void SerializeIsDeterministicAndRoundTrips()
    {
        RepositoryEvidence evidence = TestRepositoryEvidence.Create();

        string first = ContractJson.Serialize(evidence);
        string second = ContractJson.Serialize(evidence);
        RepositoryEvidence roundTrip = ContractJson.Deserialize<RepositoryEvidence>(first);

        Assert.Equal(first, second);
        Assert.Equal(first, ContractJson.Serialize(roundTrip));
        Assert.Equal(evidence.Repository.Name, roundTrip.Repository.Name);
        Assert.Equal(evidence.Facts.Select(fact => fact.Id), roundTrip.Facts.Select(fact => fact.Id));
        Assert.Contains("\"schemaVersion\": \"1.0.0\"", first, StringComparison.Ordinal);
        Assert.Contains("\"sourceKind\": \"observed\"", first, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryEvidenceSatisfiesPublishedSchema()
    {
        string json = ContractJson.Serialize(TestRepositoryEvidence.Create());

        SchemaValidationResult result = ContractSchemaValidator.Validate(
            SchemaNames.RepositoryEvidence,
            json);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Errors));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void RepositoryEvidenceSchemaRejectsUnknownProperties()
    {
        JsonObject json = JsonNode.Parse(ContractJson.Serialize(TestRepositoryEvidence.Create()))!
            .AsObject();
        json["unexpected"] = true;

        SchemaValidationResult result = ContractSchemaValidator.Validate(
            SchemaNames.RepositoryEvidence,
            json.ToJsonString());

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void DeserializeRejectsUnknownProperties()
    {
        JsonObject json = JsonNode.Parse(ContractJson.Serialize(TestRepositoryEvidence.Create()))!
            .AsObject();
        json["unexpected"] = true;

        Assert.Throws<System.Text.Json.JsonException>(
            () => ContractJson.Deserialize<RepositoryEvidence>(json.ToJsonString()));
    }
}
