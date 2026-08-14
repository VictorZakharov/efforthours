using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class CandidateMeasurementInputBuilder
{
    public const string Version = "repository-candidate-benchmark-inputs/1.0.0";

    private static readonly (string Id, int Copies)[] Shapes =
    [
        ("small", 1),
        ("medium", 16),
        ("large", 128),
    ];

    public static async Task<IReadOnlyList<CandidateMeasurementInput>> BuildAsync(
        string templatePath,
        string workspacePath,
        CancellationToken cancellationToken)
    {
        string templateJson = await File.ReadAllTextAsync(templatePath, cancellationToken)
            .ConfigureAwait(false);
        RepositoryEvidence template = ContractJson.Deserialize<RepositoryEvidence>(templateJson);
        IReadOnlyList<string> errors = ContractValidation.Validate(template);
        if (errors.Count > 0 || !template.Repository.Ecosystems.Contains("dotnet", StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Candidate measurement template must be valid .NET repository evidence.");
        }

        string templateDigest = JsonArtifactDigest.Compute(templateJson);
        string inputDirectory = Path.Combine(workspacePath, "saved-evidence");
        Directory.CreateDirectory(inputDirectory);
        List<CandidateMeasurementInput> inputs = [];
        foreach ((string id, int copies) in Shapes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RepositoryEvidence evidence = Build(template, templateDigest, id, copies);
            string json = ContractJson.Serialize(evidence) + Environment.NewLine;
            string path = Path.Combine(inputDirectory, $"{id}.repository-evidence.json");
            await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
            inputs.Add(new CandidateMeasurementInput(
                id,
                copies,
                evidence.Facts.Count,
                JsonArtifactDigest.Compute(json),
                path));
        }

        return inputs;
    }

    private static RepositoryEvidence Build(
        RepositoryEvidence template,
        string templateDigest,
        string shape,
        int copies)
    {
        List<EvidenceFact> facts = [];
        for (int index = 0; index < copies; index++)
        {
            string module = $"modules/{index:D4}";
            foreach (EvidenceFact fact in template.Facts)
            {
                facts.Add(fact with
                {
                    Id = $"benchmark:{shape}:{index:D4}:{fact.Id}",
                    Scope = Prefix(module, fact.Scope),
                    Locations =
                    [
                        .. fact.Locations.Select(location => location with
                        {
                            Path = Prefix(module, location.Path),
                        }),
                    ],
                });
            }
        }

        string sourceDigest = JsonArtifactDigest.Compute(
            $"{Version}:{shape}:{copies}:{templateDigest}");
        return new RepositoryEvidence
        {
            Repository = new RepositoryDescriptor
            {
                Name = $"candidate-benchmark-{shape}",
                Scope = ".",
                Ecosystems = ["dotnet"],
                SourceDigest = sourceDigest,
            },
            Facts = facts,
        };
    }

    private static string Prefix(string prefix, string value) =>
        value is "." or "" ? prefix : $"{prefix}/{value.Replace('\\', '/')}";
}

internal sealed record CandidateMeasurementInput(
    string Id,
    int ModuleCopies,
    int FactCount,
    string Digest,
    string Path);
