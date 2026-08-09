using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;
using EffortHours.Review;

namespace EffortHours.Tests;

public sealed class HostReviewTests
{
    [Fact]
    public void PacketIsDeterministicRateFreeAndSchemaValid()
    {
        RepositoryEvidence evidence = TestRepositoryEvidence.Create();
        EstimateReport report = new SeedEstimator().Estimate(
            evidence,
            EstimationProfile.Implementation,
            rateCard: null);

        HostReviewPacket first = HostReviewPacketBuilder.Build(report, evidence);
        HostReviewPacket second = HostReviewPacketBuilder.Build(report, evidence);
        string firstJson = new HostReviewJsonRenderer(compact: true).Render(first);
        string secondJson = new HostReviewJsonRenderer(compact: true).Render(second);

        Assert.Equal(firstJson, secondJson);
        Assert.Equal(HostReviewProtocolVersions.V1, first.ProtocolVersion);
        Assert.StartsWith("sha256:", first.InputDigest, StringComparison.Ordinal);
        Assert.False(first.PricingIncluded);
        Assert.True(first.LocalBaselineComplete);
        Assert.All(first.Categories, category => Assert.Null(category.Cost));
        Assert.All(first.Candidates, candidate => Assert.Null(candidate.Capability.Cost));
        Assert.InRange(first.Candidates.Count, 1, HostReviewProtocol.MaximumPacketCapabilities);
        Assert.InRange(first.EvidenceFacts.Count, 1, HostReviewProtocol.MaximumPacketEvidenceFacts);
        Assert.False(first.ReviewerModel.IsAvailable);
        Assert.Empty(ContractValidation.Validate(first));
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.HostReviewPacket,
            firstJson).IsValid);
    }

    [Fact]
    public void ModelMetadataDoesNotChangeReviewInputIdentity()
    {
        RepositoryEvidence evidence = TestRepositoryEvidence.Create();
        EstimateReport report = new SeedEstimator().Estimate(
            evidence,
            EstimationProfile.Recreation,
            rateCard: null);
        HostReviewPacket unavailable = HostReviewPacketBuilder.Build(report, evidence);
        HostReviewPacket disclosed = HostReviewPacketBuilder.Build(
            report,
            evidence,
            new HostReviewModelIdentity
            {
                IsAvailable = true,
                Provider = "example-provider",
                Model = "example-model",
                Version = "2026-08",
            });

        Assert.Equal(unavailable.InputDigest, disclosed.InputDigest);
        Assert.True(disclosed.ReviewerModel.IsAvailable);
        Assert.Equal("example-model", disclosed.ReviewerModel.Model);
    }

    [Fact]
    public async Task PacketAndQueryHonorPreCancelledTokens()
    {
        RepositoryEvidence evidence = TestRepositoryEvidence.Create();
        EstimateReport report = new SeedEstimator().Estimate(
            evidence,
            EstimationProfile.Implementation,
            rateCard: null);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => HostReviewPacketBuilder.Build(
            report,
            evidence,
            reviewerModel: null,
            cancellationToken: cancellation.Token));

        HostReviewPacket packet = HostReviewPacketBuilder.Build(report, evidence);
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            HostReviewQueryEngine.ExecuteAsync(
                report,
                evidence,
                Query(packet, HostReviewQueryKind.Evidence, evidence.Facts[0].Id),
                sourceContext: null,
                cancellationToken: cancellation.Token));
    }

    [Fact]
    public async Task CapabilityEvidenceAndScopeQueriesAreDigestBound()
    {
        RepositoryEvidence evidence = TestRepositoryEvidence.Create();
        EstimateReport report = new SeedEstimator().Estimate(
            evidence,
            EstimationProfile.Implementation,
            rateCard: null);
        HostReviewPacket packet = HostReviewPacketBuilder.Build(report, evidence);
        HostReviewCandidate selected = packet.Candidates[0];

        HostReviewQueryResult capability = await HostReviewQueryEngine.ExecuteAsync(
            report,
            evidence,
            Query(
                packet,
                HostReviewQueryKind.Capability,
                selected.Capability.Id));
        HostReviewQueryResult fact = await HostReviewQueryEngine.ExecuteAsync(
            report,
            evidence,
            Query(
                packet,
                HostReviewQueryKind.Evidence,
                selected.EvidenceIds[0]));
        HostReviewQueryResult scope = await HostReviewQueryEngine.ExecuteAsync(
            report,
            evidence,
            Query(
                packet,
                HostReviewQueryKind.Scope,
                selected.Capability.Scope) with
            {
                Offset = 0,
                Limit = 1,
            });

        Assert.Single(capability.Capabilities);
        Assert.Equal(selected.Capability.Id, capability.Capabilities[0].Capability.Id);
        Assert.Single(fact.EvidenceFacts);
        Assert.Equal(selected.EvidenceIds[0], fact.EvidenceFacts[0].Id);
        Assert.Single(scope.Capabilities);
        Assert.All(scope.Capabilities, candidate =>
            Assert.Equal(selected.Capability.Scope, candidate.Capability.Scope));
        Assert.DoesNotContain(
            capability.EvidenceFacts,
            evidenceFact => evidenceFact.Summary.Contains("namespace ", StringComparison.Ordinal));

        string json = new HostReviewJsonRenderer(compact: true).Render(capability);
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.HostReviewQueryResult,
            json).IsValid);

        HostReviewQuery wrongDigest = Query(
            packet,
            HostReviewQueryKind.Capability,
            selected.Capability.Id) with
        {
            InputDigest = "sha256:" + new string('0', 64),
        };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            HostReviewQueryEngine.ExecuteAsync(report, evidence, wrongDigest));
    }

    [Fact]
    public async Task SelectedSourceUsesOnlyAdmittedInMemoryFilesAndDetectsChanges()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "Demo.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n");
        repository.WriteText(
            "Program.cs",
            "namespace Demo;\npublic static class Program\n{\n    public static int Main() => 0;\n}\n");
        repository.WriteText(".gitignore", ".secret\n");
        repository.WriteText(".secret", "do-not-disclose\n");
        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);
        EstimateReport report = new SeedEstimator().Estimate(
            evidence,
            EstimationProfile.Implementation,
            rateCard: null);
        HostReviewPacket packet = HostReviewPacketBuilder.Build(report, evidence);
        HostReviewQuery sourceQuery = Query(
            packet,
            HostReviewQueryKind.SelectedSource,
            "Program.cs") with
        {
            StartLine = 2,
            LineCount = 2,
        };

        HostReviewQueryResult result = await HostReviewQueryEngine.ExecuteAsync(
            report,
            evidence,
            sourceQuery,
            new HostReviewSourceContext(repository.RootPath, repository));

        Assert.True(result.ContainsSourceExcerpt);
        Assert.NotNull(result.SourceExcerpt);
        Assert.Equal("Program.cs", result.SourceExcerpt.Path);
        Assert.Equal([2, 3], result.SourceExcerpt.Lines.Select(line => line.Line));
        Assert.DoesNotContain(repository.RootPath, ContractJson.Serialize(result), StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-disclose", ContractJson.Serialize(result), StringComparison.Ordinal);

        HostReviewQuery ignoredFile = sourceQuery with { Selector = ".secret" };
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            HostReviewQueryEngine.ExecuteAsync(
                report,
                evidence,
                ignoredFile,
                new HostReviewSourceContext(repository.RootPath, repository)));

        repository.WriteText("Program.cs", "namespace Changed;\n");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HostReviewQueryEngine.ExecuteAsync(
                report,
                evidence,
                sourceQuery,
                new HostReviewSourceContext(repository.RootPath, repository)));
    }

    [Fact]
    public void AdjustmentValidationChecksBaselineAndEvidenceWithoutApplying()
    {
        RepositoryEvidence evidence = TestRepositoryEvidence.Create();
        EstimateReport report = new SeedEstimator().Estimate(
            evidence,
            EstimationProfile.Implementation,
            rateCard: null);
        HostReviewPacket packet = HostReviewPacketBuilder.Build(report, evidence);
        HostReviewCandidate candidate = packet.Candidates[0];
        HostReviewAdjustmentLedger validLedger = new()
        {
            InputDigest = packet.InputDigest,
            ReviewerModel = new HostReviewModelIdentity
            {
                IsAvailable = true,
                Model = "review-model",
            },
            Adjustments =
            [
                new HostReviewAdjustment
                {
                    TargetId = candidate.Capability.Id,
                    Decision = HostReviewDecision.Affirm,
                    OriginalHours = candidate.Capability.Hours,
                    EvidenceIds = [candidate.EvidenceIds[0]],
                    Reason = "The observed boundary supports the local range.",
                },
            ],
        };

        HostReviewValidationReport valid = HostReviewAdjustmentValidator.Validate(
            packet,
            validLedger);
        Assert.True(valid.IsValid);
        Assert.False(valid.AdjustmentsApplied);
        Assert.Empty(valid.Errors);
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.HostReviewAdjustment,
            new HostReviewJsonRenderer(compact: true).Render(validLedger)).IsValid);
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.HostReviewValidation,
            new HostReviewJsonRenderer(compact: true).Render(valid)).IsValid);

        HostReviewAdjustmentLedger replacementLedger = validLedger with
        {
            Adjustments =
            [
                validLedger.Adjustments[0] with
                {
                    Decision = HostReviewDecision.Replace,
                    Replacement = new HostReviewReplacement
                    {
                        Category = candidate.Capability.Category,
                        Hours = candidate.Capability.Hours,
                        Confidence = candidate.Capability.Confidence,
                        Assumptions = candidate.Assumptions,
                        Exclusions = candidate.Exclusions,
                        UncertaintyReasons = candidate.UncertaintyReasons,
                    },
                },
            ],
        };
        Assert.True(HostReviewAdjustmentValidator.Validate(packet, replacementLedger).IsValid);
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.HostReviewAdjustment,
            new HostReviewJsonRenderer(compact: true).Render(replacementLedger)).IsValid);

        HostReviewAdjustmentLedger invalidLedger = validLedger with
        {
            Adjustments =
            [
                validLedger.Adjustments[0] with
                {
                    OriginalHours = new EffortRange
                    {
                        Low = 0m,
                        Expected = 0m,
                        High = 0m,
                    },
                    EvidenceIds = ["unrelated:evidence"],
                },
            ],
        };
        HostReviewValidationReport invalid = HostReviewAdjustmentValidator.Validate(
            packet,
            invalidLedger);

        Assert.False(invalid.IsValid);
        Assert.Contains(invalid.Errors, error => error.Contains("exact baseline", StringComparison.Ordinal));
        Assert.Contains(invalid.Errors, error => error.Contains("unrelated evidence", StringComparison.Ordinal));
        Assert.Equal(report.TotalEffort, packet.TotalEffort);
    }

    private static HostReviewQuery Query(
        HostReviewPacket packet,
        HostReviewQueryKind kind,
        string selector) => new()
        {
            Kind = kind,
            Selector = selector,
            Reason = "Resolve a material review uncertainty.",
            Profile = packet.Profile,
            InputDigest = packet.InputDigest,
        };
}
