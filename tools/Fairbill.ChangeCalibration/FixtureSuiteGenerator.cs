using System.Security.Cryptography;
using System.Text;
using Fairbill.Calibration;
using Fairbill.Change;
using Fairbill.Contracts;
using Fairbill.Contracts.V1;

namespace Fairbill.ChangeCalibration;

internal static class FixtureSuiteGenerator
{
    public const string Version = "change-fixture-generator/0.1.0";

    public static async Task GenerateAsync(
        string suitePath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suitePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        string json = await File.ReadAllTextAsync(suitePath, cancellationToken).ConfigureAwait(false);
        FixtureSuite suite = ContractJson.Deserialize<FixtureSuite>(json);
        Validate(suite);

        string fullOutputPath = Path.GetFullPath(outputPath);
        string reportPath = Path.Combine(fullOutputPath, "reports");
        string packetPath = Path.Combine(fullOutputPath, "blind-packets");
        Directory.CreateDirectory(reportPath);
        Directory.CreateDirectory(packetPath);

        List<GeneratedFixtureCase> generated = [];
        foreach (FixtureCase fixture in suite.Cases.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ChangeEstimateReport estimate = await EstimateAsync(fixture, cancellationToken)
                .ConfigureAwait(false);
            CalibrationAuthoringPacket packet = ChangeCalibrationAuthoring.Scaffold(
                estimate,
                fixture.RepositoryFamilyId,
                fixture.Id,
                fixture.CoverageTags,
                blind: true);
            string slug = Slug(fixture.Id);
            string relativeEstimate = $"reports/{slug}.change-estimate.json";
            string relativePacket = $"blind-packets/{slug}.authoring.json";
            string estimateJson = ContractJson.Serialize(estimate);
            string packetJson = ContractJson.Serialize(packet);
            EnsureSchema(SchemaNames.ChangeEstimateReport, estimateJson, fixture.Id);
            EnsureSchema(SchemaNames.CalibrationAuthoringPacket, packetJson, fixture.Id);
            await WriteAsync(
                Path.Combine(fullOutputPath, relativeEstimate.Replace('/', Path.DirectorySeparatorChar)),
                estimateJson,
                cancellationToken).ConfigureAwait(false);
            await WriteAsync(
                Path.Combine(fullOutputPath, relativePacket.Replace('/', Path.DirectorySeparatorChar)),
                packetJson,
                cancellationToken).ConfigureAwait(false);
            generated.Add(new GeneratedFixtureCase
            {
                Id = fixture.Id,
                RepositoryFamilyId = fixture.RepositoryFamilyId,
                Partition = fixture.Partition,
                Ecosystem = fixture.Ecosystem,
                SelectionKind = fixture.SelectionKind,
                EstimatePath = relativeEstimate,
                BlindPacketPath = relativePacket,
                EstimateDigest = CalibrationDigest.Compute(estimate),
                FinalDeltaDigest = ChangeCalibrationIdentity.ComputeFinalDeltaDigest(estimate),
                WorkItemCount = estimate.WorkItems.Count,
                CoverageTags = [.. fixture.CoverageTags.Order(StringComparer.Ordinal)],
            });
        }

        GeneratedFixtureIndex index = new()
        {
            SchemaVersion = ContractVersions.V1,
            GeneratorVersion = Version,
            SuiteId = suite.Id,
            SuiteVersion = suite.Version,
            SuiteDigest = Digest(ContractJson.SerializeCompact(suite)),
            Cases = generated,
        };
        await WriteAsync(
            Path.Combine(fullOutputPath, "index.json"),
            ContractJson.Serialize(index),
            cancellationToken).ConfigureAwait(false);
        await Console.Out.WriteLineAsync(
            $"Generated {generated.Count} Change calibration cases in {fullOutputPath}.").ConfigureAwait(false);
    }

    private static async Task<ChangeEstimateReport> EstimateAsync(
        FixtureCase fixture,
        CancellationToken cancellationToken)
    {
        Dictionary<string, FixtureState> states = fixture.States.ToDictionary(
            state => state.Id,
            StringComparer.Ordinal);
        Dictionary<string, string> objectIds = states.ToDictionary(
            pair => pair.Key,
            pair => new MemoryChangeSnapshot(pair.Value.Files).ObjectId,
            StringComparer.Ordinal);
        Func<CancellationToken, Task<IChangeSnapshot>> open(string stateId) => token =>
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult<IChangeSnapshot>(new MemoryChangeSnapshot(states[stateId].Files));
        };
        ChangeSelection selection = CreateSelection(fixture, objectIds);
        ChangeComponentInput[] components = [.. fixture.Components.Select(component =>
            new ChangeComponentInput
            {
                Selector = component.Selector,
                BaseObjectId = objectIds[component.BaseStateId],
                HeadObjectId = objectIds[component.HeadStateId],
                OpenBaseAsync = open(component.BaseStateId),
                OpenHeadAsync = open(component.HeadStateId),
            })];
        return await new ChangeEstimator().EstimateAsync(
            new ChangeEstimateInput
            {
                RepositoryName = fixture.RepositoryFamilyId,
                Selection = selection,
                OpenBaseAsync = open(fixture.BaseStateId),
                OpenHeadAsync = open(fixture.HeadStateId),
                Components = components,
            },
            EstimationProfile.Implementation,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static ChangeSelection CreateSelection(
        FixtureCase fixture,
        Dictionary<string, string> objectIds)
    {
        string baseSelector = $"fixture:{fixture.Id}:{fixture.BaseStateId}";
        string headSelector = $"fixture:{fixture.Id}:{fixture.HeadStateId}";
        return new ChangeSelection
        {
            Kind = fixture.SelectionKind,
            Base = Reference(baseSelector, objectIds[fixture.BaseStateId]),
            Head = Reference(headSelector, objectIds[fixture.HeadStateId]),
            Commit = fixture.SelectionKind == ChangeSelectionKind.Commit ? headSelector : null,
            Parent = fixture.SelectionKind == ChangeSelectionKind.Commit ? baseSelector : null,
            Range = fixture.SelectionKind == ChangeSelectionKind.Range
                ? $"{baseSelector}..{headSelector}"
                : null,
        };
    }

    private static ChangeSnapshotReference Reference(string selector, string objectId) => new()
    {
        Selector = selector,
        ObjectId = objectId,
        Kind = ChangeSnapshotKind.Evidence,
    };

    private static void Validate(FixtureSuite suite)
    {
        List<string> errors = [];
        Required(suite.SchemaVersion, "schemaVersion", errors);
        Required(suite.Id, "id", errors);
        Required(suite.Version, "version", errors);
        Required(suite.Description, "description", errors);
        if (suite.SchemaVersion != ContractVersions.V1)
        {
            errors.Add($"Unsupported fixture schema version '{suite.SchemaVersion}'.");
        }

        if (suite.Cases.Count == 0)
        {
            errors.Add("Fixture suite must contain at least one case.");
        }

        AddDuplicates(suite.Cases.Select(item => item.Id), "case ID", errors);
        Dictionary<string, CalibrationPartition> familyPartitions = new(StringComparer.Ordinal);
        HashSet<string> slugs = new(StringComparer.Ordinal);
        foreach (FixtureCase fixture in suite.Cases)
        {
            ValidateCase(fixture, familyPartitions, slugs, errors);
        }

        if (errors.Count > 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        }
    }

    private static void ValidateCase(
        FixtureCase fixture,
        Dictionary<string, CalibrationPartition> familyPartitions,
        HashSet<string> slugs,
        List<string> errors)
    {
        Required(fixture.Id, "case.id", errors);
        Required(fixture.RepositoryFamilyId, $"{fixture.Id}.repositoryFamilyId", errors);
        Required(fixture.Ecosystem, $"{fixture.Id}.ecosystem", errors);
        Required(fixture.BaseStateId, $"{fixture.Id}.baseStateId", errors);
        Required(fixture.HeadStateId, $"{fixture.Id}.headStateId", errors);
        if (fixture.SelectionKind == ChangeSelectionKind.PullRequest)
        {
            errors.Add($"{fixture.Id}: synthetic pull-request fixtures are not supported.");
        }

        if (!slugs.Add(Slug(fixture.Id)))
        {
            errors.Add($"{fixture.Id}: output slug collides with another case.");
        }

        if (familyPartitions.TryGetValue(fixture.RepositoryFamilyId, out CalibrationPartition partition) &&
            partition != fixture.Partition)
        {
            errors.Add($"{fixture.Id}: repository family crosses calibration partitions.");
        }
        else
        {
            familyPartitions[fixture.RepositoryFamilyId] = fixture.Partition;
        }

        AddDuplicates(fixture.CoverageTags, $"{fixture.Id} coverage tag", errors);
        if (fixture.CoverageTags.Count == 0 || fixture.CoverageTags.Any(tag => !IsLowerKebab(tag)))
        {
            errors.Add($"{fixture.Id}: coverage tags must be non-empty lower-kebab values.");
        }

        AddDuplicates(fixture.States.Select(state => state.Id), $"{fixture.Id} state ID", errors);
        HashSet<string> stateIds = fixture.States.Select(state => state.Id).ToHashSet(StringComparer.Ordinal);
        if (!stateIds.Contains(fixture.BaseStateId) || !stateIds.Contains(fixture.HeadStateId))
        {
            errors.Add($"{fixture.Id}: base and head state IDs must exist.");
        }

        foreach (FixtureState state in fixture.States)
        {
            Required(state.Id, $"{fixture.Id} state.id", errors);
            foreach (string path in state.Files.Keys)
            {
                if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) ||
                    path.Replace('\\', '/').Split('/').Any(segment => segment is "" or "." or ".."))
                {
                    errors.Add($"{fixture.Id}/{state.Id}: invalid relative file path '{path}'.");
                }
            }
        }

        AddDuplicates(fixture.Components.Select(component => component.Selector),
            $"{fixture.Id} component selector", errors);
        foreach (FixtureComponent component in fixture.Components)
        {
            if (!stateIds.Contains(component.BaseStateId) || !stateIds.Contains(component.HeadStateId))
            {
                errors.Add($"{fixture.Id}/{component.Selector}: component state ID does not exist.");
            }
        }

        if (fixture.SelectionKind == ChangeSelectionKind.Range && fixture.Components.Count == 0)
        {
            errors.Add($"{fixture.Id}: range fixtures require component changes.");
        }
    }

    private static void AddDuplicates(
        IEnumerable<string> values,
        string label,
        List<string> errors)
    {
        foreach (string duplicate in values
            .GroupBy(value => value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key))
        {
            errors.Add($"Duplicate {label} '{duplicate}'.");
        }
    }

    private static bool IsLowerKebab(string value) =>
        value.Length > 0 &&
        value[0] != '-' &&
        value[^1] != '-' &&
        value.All(character => char.IsAsciiLetterLower(character) || char.IsDigit(character) || character == '-');

    private static void Required(string value, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"Fixture {label} is required.");
        }
    }

    private static string Slug(string id)
    {
        StringBuilder builder = new(id.Length);
        foreach (char character in id)
        {
            builder.Append(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'
                ? char.ToLowerInvariant(character)
                : '-');
        }

        return builder.ToString().Trim('-');
    }

    private static string Digest(string value) =>
        $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()}";

    private static void EnsureSchema(string schemaName, string json, string caseId)
    {
        SchemaValidationResult result = ContractSchemaValidator.Validate(schemaName, json);
        if (!result.IsValid)
        {
            throw new InvalidDataException(
                $"Generated case '{caseId}' failed schema '{schemaName}': " +
                string.Join(" ", result.Errors));
        }
    }

    private static Task WriteAsync(string path, string content, CancellationToken cancellationToken) =>
        File.WriteAllTextAsync(path, content + Environment.NewLine, new UTF8Encoding(false), cancellationToken);
}
