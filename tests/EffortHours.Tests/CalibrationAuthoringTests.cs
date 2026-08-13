using EffortHours.Calibration;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class CalibrationTests
{
    [Fact]
    public void BlindAuthoringScaffoldHidesCandidateValuesAndExplanationsInMemory()
    {
        CalibrationAuthoringPacket packet = CalibrationAuthoring.Scaffold(
            CreateCandidate(),
            blind: true);
        string json = ContractJson.SerializeCompact(packet);
        CalibrationAuthoringPacket roundTrip =
            ContractJson.Deserialize<CalibrationAuthoringPacket>(json);

        Assert.Empty(ContractValidation.Validate(packet));
        Assert.Equal(CalibrationCandidateVisibility.Blind, packet.CandidateVisibility);
        Assert.Null(packet.Candidate.TotalHours);
        Assert.Empty(packet.Candidate.Categories);
        Assert.Empty(packet.ProfessionalizationGapWorkItemIds);
        Assert.All(packet.Targets, target =>
        {
            Assert.Empty(target.SourceWorkItemIds);
            Assert.Null(target.Candidate.Hours);
            Assert.Null(target.Candidate.Confidence);
            Assert.Null(target.Candidate.Reason);
        });
        Assert.Equal(json, ContractJson.SerializeCompact(roundTrip));
        Assert.Contains("\"hours\":null", json, StringComparison.Ordinal);
        Assert.Contains("\"reason\":null", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Implement the represented behavior", json, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthoringScaffoldAcceptsFrozenRepositoryFamilyIdentity()
    {
        CalibrationAuthoringPacket packet = CalibrationAuthoring.Scaffold(
            CreateCandidate(),
            blind: true,
            repositoryFamilyId: "repository:github.com/example/project",
            repositoryName: "example/project");

        Assert.Equal("repository:github.com/example/project", packet.Repository.Id);
        Assert.Equal("example/project", packet.Repository.Name);
    }

    [Fact]
    public void AuthoringScaffoldGroupsCandidatePartitionsByCapability()
    {
        EstimateReport estimate = CreateCandidate();
        WorkItem source = estimate.WorkItems[0];
        WorkItem first = source with
        {
            Id = "work:implementation:part-0001",
            Title = "Implement behavior (part 1 of 2)",
            Hours = Hours(1m, 2m, 3m),
        };
        WorkItem second = source with
        {
            Id = "work:implementation:part-0002",
            Title = "Implement behavior (part 2 of 2)",
            EvidenceIds = ["evidence:implementation:second"],
            Hours = Hours(1m, 2m, 3m),
        };
        estimate = estimate with { WorkItems = [first, second, estimate.WorkItems[1]] };

        CalibrationAuthoringPacket packet = CalibrationAuthoring.Scaffold(estimate);

        Assert.Equal(2, packet.Targets.Count);
        CalibrationAuthoringTarget target = Assert.Single(
            packet.Targets,
            item => item.SourceCapabilityId == "work:implementation");
        Assert.Equal("Implement behavior", target.Title);
        Assert.Equal(
            ["work:implementation:part-0001", "work:implementation:part-0002"],
            target.SourceWorkItemIds);
        Assert.Equal(
            ["evidence:implementation", "evidence:implementation:second"],
            target.EvidenceIds);
        Assert.Equal(Hours(2m, 4m, 6m), target.Candidate.Hours);
        Assert.Equal(0.8m, target.Candidate.Confidence);
    }

    [Fact]
    public void BlindAuthoringValidationRejectsCandidateReason()
    {
        CalibrationAuthoringPacket packet = CalibrationAuthoring.Scaffold(
            CreateCandidate(),
            blind: true);
        CalibrationAuthoringTarget target = packet.Targets[0];
        packet = packet with
        {
            Targets =
            [
                target with
                {
                    Candidate = target.Candidate with { Reason = "Leaked candidate explanation." },
                },
                .. packet.Targets.Skip(1),
            ],
        };

        Assert.Contains(
            ContractValidation.Validate(packet),
            error => error.Contains("must hide candidate hours, confidence, and reason", StringComparison.Ordinal));
    }

    [Fact]
    public void BlindAuthoringValidationRejectsSourcePartitionLineage()
    {
        CalibrationAuthoringPacket packet = CalibrationAuthoring.Scaffold(
            CreateCandidate(),
            blind: true);
        CalibrationAuthoringTarget target = packet.Targets[0];
        packet = packet with
        {
            Targets =
            [
                target with { SourceWorkItemIds = ["work:candidate-partition:part-0001"] },
                .. packet.Targets.Skip(1),
            ],
        };

        Assert.Contains(
            ContractValidation.Validate(packet),
            error => error.Contains("sourceWorkItemIds must be hidden in blind mode", StringComparison.Ordinal));
    }
}
