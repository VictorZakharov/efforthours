using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class ChangeLogicalMarginalityTests
{
    private const string ModifiedPath = "src/engine/operation.cs";

    [Theory]
    [InlineData(EffortCategory.ProductionImplementation)]
    [InlineData(EffortCategory.UnitTesting)]
    [InlineData(EffortCategory.SecurityAndAccessibility)]
    public void RepeatedRepositoryPartitionsShareOneLogicalChangeBudget(EffortCategory category)
    {
        ChangeWorkItemResult twoPartitions = BuildExistingCapabilityChange(category, 2);
        ChangeWorkItemResult fourPartitions = BuildExistingCapabilityChange(category, 4);

        EffortRange twoPartitionHours = CategoryHours(twoPartitions, category);
        EffortRange fourPartitionHours = CategoryHours(fourPartitions, category);

        Assert.Equal(twoPartitionHours, fourPartitionHours);
        Assert.InRange(twoPartitionHours.Expected, 0.5m, 4m);
    }

    [Fact]
    public void DistinctAddedCapabilitiesRemainAdditive()
    {
        CapabilityFixture first = new(
            "first-capability",
            EffortCategory.ProductionImplementation,
            "src/engine/first.cs",
            2m,
            1);
        CapabilityFixture second = new(
            "second-capability",
            EffortCategory.ProductionImplementation,
            "src/engine/second.cs",
            2m,
            1);

        ChangeWorkItemResult result = Build(
            [],
            [first, second],
            [Path(first.Path, ChangePathStatus.Added), Path(second.Path, ChangePathStatus.Added)]);

        WorkItem[] capabilityItems =
        [
            .. result.WorkItems.Where(item =>
                item.Category == EffortCategory.ProductionImplementation &&
                item.Estimator.Id == "change-rule:capability-marginal"),
        ];
        Assert.Equal(2, capabilityItems.Length);
        Assert.Equal(4m, capabilityItems.Sum(item => item.Hours.Expected));
    }

    [Fact]
    public void DistinctModifiedCapabilitiesOnOneArtifactRemainAdditive()
    {
        CapabilityFixture firstBefore = new(
            "first-modified-capability",
            EffortCategory.ProductionImplementation,
            ModifiedPath,
            4m,
            1);
        CapabilityFixture firstAfter = firstBefore with { PartitionCount = 3 };
        CapabilityFixture secondBefore = new(
            "second-modified-capability",
            EffortCategory.ProductionImplementation,
            ModifiedPath,
            4m,
            1);
        CapabilityFixture secondAfter = secondBefore with { PartitionCount = 3 };

        ChangeWorkItemResult oneCapability = Build(
            [firstBefore],
            [firstAfter],
            [Path(ModifiedPath, ChangePathStatus.Modified)]);
        ChangeWorkItemResult twoCapabilities = Build(
            [firstBefore, secondBefore],
            [firstAfter, secondAfter],
            [Path(ModifiedPath, ChangePathStatus.Modified)]);

        decimal oneExpected = CategoryHours(
            oneCapability,
            EffortCategory.ProductionImplementation).Expected;
        decimal twoExpected = CategoryHours(
            twoCapabilities,
            EffortCategory.ProductionImplementation).Expected;
        Assert.Equal(oneExpected * 2m, twoExpected);
    }

    [Fact]
    public void NewlyDetectedCapabilityOnModifiedProductionArtifactReceivesMeaningfulEffort()
    {
        CapabilityFixture detected = new(
            "detected-capability",
            EffortCategory.ProductionImplementation,
            ModifiedPath,
            0.25m,
            1,
            MapsPath: false);

        ChangeWorkItemResult result = Build(
            [],
            [detected],
            [Path(ModifiedPath, ChangePathStatus.Modified)]);

        EffortRange production = CategoryHours(result, EffortCategory.ProductionImplementation);
        Assert.InRange(production.Expected, 0.5m, 4m);
    }

    private static ChangeWorkItemResult BuildExistingCapabilityChange(
        EffortCategory category,
        int headPartitionCount)
    {
        string path = category == EffortCategory.UnitTesting
            ? "tests/engine/operation-tests.cs"
            : ModifiedPath;
        CapabilityFixture before = new("logical-capability", category, path, 4m, 1);
        CapabilityFixture after = new("logical-capability", category, path, 4m, headPartitionCount);
        return Build(
            [before],
            [after],
            [Path(path, ChangePathStatus.Modified, category == EffortCategory.UnitTesting ? "role:test" : "role:source")]);
    }

    private static ChangeWorkItemResult Build(
        IReadOnlyList<CapabilityFixture> before,
        IReadOnlyList<CapabilityFixture> after,
        IReadOnlyList<ChangePathEvidence> paths)
    {
        ChangeSelection selection = Selection();
        RepositoryEvidence baseEvidence = Evidence("base", before.Concat(after), measurement: 1m);
        RepositoryEvidence headEvidence = Evidence("head", before.Concat(after), measurement: 2m);
        return ChangeWorkItemBuilder.Build(
            selection,
            new ChangeEvidence
            {
                Selection = selection,
                Repository = Repository("head"),
                BaseEvidenceDigest = "sha256:base",
                HeadEvidenceDigest = "sha256:head",
                Paths = paths,
            },
            baseEvidence,
            headEvidence,
            Report("base", before),
            Report("head", after),
            EstimationProfile.Implementation);
    }

    private static RepositoryEvidence Evidence(
        string identity,
        IEnumerable<CapabilityFixture> capabilities,
        decimal measurement) => new()
        {
            Repository = Repository(identity),
            Facts =
            [
                .. capabilities
                    .DistinctBy(capability => capability.Id)
                    .Select(capability => Fact(capability, measurement)),
            ],
        };

    private static EvidenceFact Fact(CapabilityFixture capability, decimal measurement) => new()
    {
        Id = EvidenceId(capability.Id),
        Kind = EvidenceKinds.SourceStructure,
        Scope = ".",
        Summary = "Synthetic logical capability evidence.",
        Provenance = new EvidenceProvenance
        {
            SourceKind = EvidenceSourceKind.Measured,
            Analyzer = "logical-marginality-fixture",
            AnalyzerVersion = "1.0.0",
            Method = "in-memory synthetic evidence",
        },
        Locations = capability.MapsPath
            ? [new EvidenceLocation { Path = capability.Path }]
            : [],
        Measurements =
        [
            new EvidenceMeasurement
            {
                Name = "logical-change",
                Value = measurement,
                Unit = "capabilities",
            },
        ],
        Tags = ["complexity:moderate"],
    };

    private static EstimateReport Report(
        string identity,
        IReadOnlyList<CapabilityFixture> capabilities)
    {
        WorkItem[] items = [.. capabilities.SelectMany(Items)];
        return new EstimateReport
        {
            EstimatorVersion = "synthetic-repository-estimator/1.0.0",
            Repository = Repository(identity),
            Profile = EstimationProfile.Implementation,
            Baseline = Baseline(),
            TotalEffort = Sum(items.Select(item => item.Hours)),
            Categories =
            [
                .. items
                    .GroupBy(item => item.Category)
                    .Select(group => new CategoryEstimate
                    {
                        Category = group.Key,
                        Hours = Sum(group.Select(item => item.Hours)),
                    }),
            ],
            WorkItems = items,
            Verification = new VerificationSummary
            {
                Mode = VerificationMode.StaticAssumed,
                WorkingState = WorkingState.AssumedWorking,
                TestsAssumedPassing = true,
            },
        };
    }

    private static IEnumerable<WorkItem> Items(CapabilityFixture capability)
    {
        for (int index = 0; index < capability.PartitionCount; index++)
        {
            yield return new WorkItem
            {
                Id = $"work:synthetic:{capability.Id}:part-{index + 1:D4}",
                Category = capability.Category,
                Title = capability.PartitionCount == 1
                    ? $"Implement {capability.Id}"
                    : $"Implement {capability.Id} (part {index + 1} of {capability.PartitionCount})",
                Scope = ".",
                EvidenceIds = [EvidenceId(capability.Id)],
                Complexity = ComplexityLevel.Moderate,
                Hours = Range(capability.ExpectedPerPartition),
                Confidence = 0.75m,
                Reason = "Synthetic repository capability partition.",
                Estimator = new EstimatorReference
                {
                    Id = "seed-rule:synthetic",
                    Version = "synthetic-repository-estimator/1.0.0",
                    Kind = EstimatorKind.Rule,
                },
                Profiles = [EstimationProfile.Implementation],
            };
        }
    }

    private static ChangePathEvidence Path(
        string path,
        ChangePathStatus status,
        string role = "role:source") => new()
        {
            Id = $"change:path:{path}",
            Status = status,
            Path = path,
            BaseObjectId = status == ChangePathStatus.Added ? null : "base-blob",
            HeadObjectId = "head-blob",
            EditRegions = 64,
            Classification = ChangePathClassification.Represented,
            Represented = true,
            Reason = "Synthetic meaningful final change.",
            Tags = ["classification:represented", role],
        };

    private static EffortRange CategoryHours(ChangeWorkItemResult result, EffortCategory category) =>
        Assert.Single(result.Categories, candidate => candidate.Category == category).Hours;

    private static EffortRange Sum(IEnumerable<EffortRange> ranges)
    {
        EffortRange[] values = [.. ranges];
        return new EffortRange
        {
            Low = values.Sum(range => range.Low),
            Expected = values.Sum(range => range.Expected),
            High = values.Sum(range => range.High),
        };
    }

    private static EffortRange Range(decimal expected) => new()
    {
        Low = expected * 0.5m,
        Expected = expected,
        High = expected * 1.5m,
    };

    private static ChangeSelection Selection() => new()
    {
        Kind = ChangeSelectionKind.BaseHead,
        Base = Reference("base"),
        Head = Reference("head"),
    };

    private static ChangeSnapshotReference Reference(string identity) => new()
    {
        Selector = identity,
        ObjectId = identity,
        Kind = ChangeSnapshotKind.Evidence,
    };

    private static RepositoryDescriptor Repository(string identity) => new()
    {
        Name = $"synthetic-{identity}",
        Ecosystems = ["dotnet"],
        SourceDigest = $"sha256:{identity}",
    };

    private static EstimationBaseline Baseline() => new()
    {
        Id = "synthetic-baseline",
        WorkerProfile = "Synthetic senior contractor",
        TechnologyBaselineYear = 2026,
        BusinessDomainFamiliar = false,
        UsesAi = false,
        Description = "Synthetic test baseline.",
    };

    private static string EvidenceId(string capabilityId) => $"evidence:{capabilityId}";

    private sealed record CapabilityFixture(
        string Id,
        EffortCategory Category,
        string Path,
        decimal ExpectedPerPartition,
        int PartitionCount,
        bool MapsPath = true);
}
