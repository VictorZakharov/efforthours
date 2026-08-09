using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Review;

namespace EffortHours.Tests;

public sealed class HostReviewMeasurementTests
{
    [Fact]
    public async Task MeasurementRecordsExactPayloadsQueriesAndReportedTelemetry()
    {
        (RepositoryEvidence evidence, EstimateReport report, HostReviewPacket packet) =
            HostReviewMeasurementTestData.Create();
        HostReviewAdjustmentLedger ledger = HostReviewMeasurementTestData.Ledger(
            packet,
            firstCandidateFactor: 0.8m);
        HostReviewQueryResult query = await HostReviewQueryEngine.ExecuteAsync(
            report,
            evidence,
            HostReviewMeasurementTestData.Query(packet, packet.Candidates[0]));
        string packetJson = ContractJson.Serialize(packet) + "\n";
        string ledgerJson = ContractJson.SerializeCompact(ledger);
        string queryJson = ContractJson.SerializeCompact(query);

        HostReviewMeasurement measurement = HostReviewMeasurementBuilder.Build(
            packet,
            packetJson,
            ledger,
            ledgerJson,
            new HostReviewMeasurementOptions
            {
                SubjectId = "opaque-subject",
                SessionId = "compact-session",
                Context = HostReviewContextMode.Compact,
                Queries =
                [
                    new HostReviewQueryPayloadInput
                    {
                        Result = query,
                        PayloadText = queryJson,
                    },
                ],
                ElapsedMilliseconds = 12_345,
                ElapsedBasis = "stopwatch around host review",
                ProviderInputTokens = 2_000,
                ProviderOutputTokens = 300,
                ProviderCachedInputTokens = 500,
                TokenBasis = "provider usage response",
                CostAmount = 0.42m,
                CostCurrency = "usd",
                CostBasis = "provider invoice",
                AdditionalInputBytes = 40,
                AdditionalInputCharacters = 32,
                AdditionalInputBasis = "system rubric size",
                AdditionalInputSizeReported = true,
                ObservedInputComplete = true,
                ConditionNotes = ["Compact review was recorded before broader inspection."],
            });

        Assert.Equal(Encoding.UTF8.GetByteCount(packetJson), measurement.PacketPayload.Size.Utf8Bytes);
        Assert.Equal(Encoding.UTF8.GetByteCount(queryJson), measurement.Queries[0].Payload.Size.Utf8Bytes);
        Assert.Equal(12_345, measurement.Elapsed.Milliseconds);
        Assert.Equal(2_000, measurement.ProviderTokens.InputTokens);
        Assert.Equal(0.42m, measurement.Cost.Amount);
        Assert.Equal("USD", measurement.Cost.Currency);
        Assert.True(measurement.ObservedInputComplete);
        Assert.True(measurement.AdditionalInput.SizeReported);
        Assert.Equal(packet.Candidates.Count, measurement.Decisions.Count);
        Assert.Equal(packet.TotalEffort, measurement.BaselineTotal);
        Assert.NotEqual(packet.TotalEffort, measurement.ReviewedTotal);

        string json = new HostReviewJsonRenderer(compact: true).Render(measurement);
        Assert.Empty(ContractValidation.Validate(measurement));
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.HostReviewMeasurement,
            json).IsValid);
    }

    [Fact]
    public void MeasurementSerializationOmitsRepositoryPromptsReasonsAndSource()
    {
        (_, _, HostReviewPacket packet) = HostReviewMeasurementTestData.Create();
        HostReviewAdjustmentLedger ledger = HostReviewMeasurementTestData.Ledger(packet);
        HostReviewMeasurement measurement = HostReviewMeasurementTestData.Measurement(
            packet,
            ledger,
            HostReviewContextMode.Compact);

        string json = new HostReviewJsonRenderer(compact: true).Render(measurement);

        Assert.DoesNotContain(packet.Repository.Name, json, StringComparison.Ordinal);
        Assert.DoesNotContain(packet.Repository.SourceDigest!, json, StringComparison.Ordinal);
        Assert.DoesNotContain(ledger.Adjustments[0].Reason, json, StringComparison.Ordinal);
        Assert.DoesNotContain(ledger.Adjustments[0].EvidenceIds[0], json, StringComparison.Ordinal);
        Assert.False(measurement.Privacy.RepositoryIdentityCopied);
        Assert.False(measurement.Privacy.SourceTextCopied);
        Assert.True(measurement.Privacy.CallerSuppliedTextRetained);
    }

    [Fact]
    public void MissingTelemetryIsExplicitlyUnavailable()
    {
        (_, _, HostReviewPacket packet) = HostReviewMeasurementTestData.Create();
        HostReviewMeasurement measurement = HostReviewMeasurementTestData.Measurement(
            packet,
            HostReviewMeasurementTestData.Ledger(packet),
            HostReviewContextMode.Compact);

        Assert.Null(measurement.ProviderTokens.InputTokens);
        Assert.NotEmpty(measurement.ProviderTokens.UnavailableReason!);
        Assert.Null(measurement.Elapsed.Milliseconds);
        Assert.NotEmpty(measurement.Elapsed.UnavailableReason!);
        Assert.Null(measurement.Cost.Amount);
        Assert.NotEmpty(measurement.Cost.UnavailableReason!);
        Assert.False(measurement.ObservedInputComplete);
        Assert.False(measurement.AdditionalInput.SizeReported);
    }

    [Fact]
    public void MeasurementRequiresCompleteCandidateDecisions()
    {
        (_, _, HostReviewPacket packet) = HostReviewMeasurementTestData.Create();
        HostReviewAdjustmentLedger incomplete = HostReviewMeasurementTestData.Ledger(packet) with
        {
            Adjustments = [HostReviewMeasurementTestData.Ledger(packet).Adjustments[0]],
        };

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            HostReviewMeasurementTestData.Measurement(
                packet,
                incomplete,
                HostReviewContextMode.Compact));

        Assert.Contains("every packet candidate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MeasurementRejectsUnmarkedAdditionalInputTelemetry()
    {
        (_, _, HostReviewPacket packet) = HostReviewMeasurementTestData.Create();
        HostReviewAdjustmentLedger ledger = HostReviewMeasurementTestData.Ledger(packet);

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            HostReviewMeasurementBuilder.Build(
                packet,
                ContractJson.SerializeCompact(packet),
                ledger,
                ContractJson.SerializeCompact(ledger),
                new HostReviewMeasurementOptions
                {
                    SubjectId = "subject",
                    SessionId = "source-session",
                    Context = HostReviewContextMode.BroaderSource,
                    AdditionalInputBytes = 100,
                    AdditionalInputCharacters = 90,
                }));

        Assert.Contains("caller-reported", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MeasurementValidationRejectsSourceExcerptInCompactContext()
    {
        (_, _, HostReviewPacket packet) = HostReviewMeasurementTestData.Create();
        HostReviewMeasurement measurement = HostReviewMeasurementTestData.Measurement(
            packet,
            HostReviewMeasurementTestData.Ledger(packet),
            HostReviewContextMode.Compact);
        HostReviewMeasurement invalid = measurement with
        {
            Queries =
            [
                new HostReviewQueryMeasurement
                {
                    Kind = HostReviewQueryKind.SelectedSource,
                    ContainsSourceExcerpt = true,
                    Payload = measurement.PacketPayload,
                },
            ],
        };

        IReadOnlyList<string> errors = ContractValidation.Validate(invalid);

        Assert.Contains(errors, error => error.Contains(
            "compact measurement cannot contain",
            StringComparison.Ordinal));
    }

    [Fact]
    public void MeasurementRejectsCompleteSourceAccountingWithoutAdditionalSize()
    {
        (_, _, HostReviewPacket packet) = HostReviewMeasurementTestData.Create();
        HostReviewAdjustmentLedger ledger = HostReviewMeasurementTestData.Ledger(packet);

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            HostReviewMeasurementBuilder.Build(
                packet,
                ContractJson.SerializeCompact(packet),
                ledger,
                ContractJson.SerializeCompact(ledger),
                new HostReviewMeasurementOptions
                {
                    SubjectId = "subject",
                    SessionId = "source-session",
                    Context = HostReviewContextMode.BroaderSource,
                    ObservedInputComplete = true,
                }));

        Assert.Contains("additional-context sizes", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CategoryReplacementReconcilesWithoutChangingBaseline()
    {
        (_, _, HostReviewPacket packet) = HostReviewMeasurementTestData.Create();
        EffortCategory movedCategory = packet.Candidates[0].Capability.Category ==
            EffortCategory.Documentation
            ? EffortCategory.ProductionImplementation
            : EffortCategory.Documentation;
        HostReviewMeasurement measurement = HostReviewMeasurementTestData.Measurement(
            packet,
            HostReviewMeasurementTestData.Ledger(
                packet,
                firstCandidateFactor: 0.75m,
                replacementCategory: movedCategory),
            HostReviewContextMode.BroaderSource,
            additionalBytes: 1_000,
            additionalCharacters: 900);

        Assert.Equal(packet.TotalEffort, measurement.BaselineTotal);
        Assert.Equal(
            measurement.ReviewedTotal,
            Sum(measurement.ReviewedCategories.Select(category => category.Hours)));
        Assert.Contains(measurement.ReviewedCategories, category => category.Category == movedCategory);
    }

    [Fact]
    public void MeasurementHonorsPreCancelledToken()
    {
        (_, _, HostReviewPacket packet) = HostReviewMeasurementTestData.Create();
        HostReviewAdjustmentLedger ledger = HostReviewMeasurementTestData.Ledger(packet);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => HostReviewMeasurementBuilder.Build(
            packet,
            ContractJson.SerializeCompact(packet),
            ledger,
            ContractJson.SerializeCompact(ledger),
            new HostReviewMeasurementOptions
            {
                SubjectId = "subject",
                SessionId = "session",
                Context = HostReviewContextMode.Compact,
            },
            cancellation.Token));
    }

    private static EffortRange Sum(IEnumerable<EffortRange> ranges) => new()
    {
        Low = ranges.Sum(range => range.Low),
        Expected = ranges.Sum(range => range.Expected),
        High = ranges.Sum(range => range.High),
    };
}
