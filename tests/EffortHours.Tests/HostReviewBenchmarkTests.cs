using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Review;

namespace EffortHours.Tests;

public sealed class HostReviewBenchmarkTests
{
    [Fact]
    public void BenchmarkMeasuresPositiveImprovementAtEveryLevel()
    {
        (_, _, HostReviewPacket packet) = HostReviewMeasurementTestData.Create();
        HostReviewMeasurement compact = HostReviewMeasurementTestData.Measurement(
            packet,
            HostReviewMeasurementTestData.Ledger(packet, firstCandidateFactor: 0.8m),
            HostReviewContextMode.Compact,
            elapsedMilliseconds: 4_000,
            inputTokens: 1_000,
            outputTokens: 100,
            cost: 0.25m,
            inputComplete: true);
        HostReviewMeasurement source = HostReviewMeasurementTestData.Measurement(
            packet,
            HostReviewMeasurementTestData.Ledger(packet, firstCandidateFactor: 0.6m),
            HostReviewContextMode.BroaderSource,
            session: "source-session",
            elapsedMilliseconds: 20_000,
            inputTokens: 8_000,
            outputTokens: 150,
            cost: 2.00m,
            additionalBytes: 30_000,
            additionalCharacters: 28_000,
            inputComplete: true);

        HostReviewBenchmarkReport report = HostReviewBenchmarkEngine.Build([compact, source]);

        Assert.Equal(1, report.SubjectCount);
        Assert.Equal(2, report.MeasurementCount);
        Assert.True(report.CapabilityItems.ExpectedAbsoluteErrorReductionHours > 0m);
        Assert.True(report.Categories.ExpectedAbsoluteErrorReductionHours > 0m);
        Assert.True(report.RepositoryTotals.ExpectedAbsoluteErrorReductionHours > 0m);
        Assert.Equal(0.125m, report.Subjects[0].Telemetry.MonetaryCostRatio);
        Assert.Equal(0.2m, report.Subjects[0].Telemetry.ElapsedTimeRatio);
        Assert.NotNull(report.Subjects[0].Telemetry.ObservedInputByteRatio);
        Assert.False(report.BudgetDecision.DefaultBudgetSelected);

        string json = new HostReviewJsonRenderer(compact: true).Render(report);
        Assert.Empty(ContractValidation.Validate(report));
        Assert.True(ContractSchemaValidator.Validate(
            SchemaNames.HostReviewBenchmark,
            json).IsValid);
    }

    [Fact]
    public void BenchmarkReportsNegativeImprovementWhenCompactReviewMovesAway()
    {
        (_, _, HostReviewPacket packet) = HostReviewMeasurementTestData.Create();
        HostReviewMeasurement compact = HostReviewMeasurementTestData.Measurement(
            packet,
            HostReviewMeasurementTestData.Ledger(packet, firstCandidateFactor: 1.4m),
            HostReviewContextMode.Compact);
        HostReviewMeasurement source = HostReviewMeasurementTestData.Measurement(
            packet,
            HostReviewMeasurementTestData.Ledger(packet, firstCandidateFactor: 0.6m),
            HostReviewContextMode.BroaderSource,
            session: "source-session",
            additionalBytes: 2_000,
            additionalCharacters: 1_800);

        HostReviewBenchmarkReport report = HostReviewBenchmarkEngine.Build([compact, source]);

        Assert.True(report.CapabilityItems.ExpectedAbsoluteErrorReductionHours < 0m);
        Assert.True(report.Categories.ExpectedAbsoluteErrorReductionHours < 0m);
        Assert.True(report.RepositoryTotals.ExpectedAbsoluteErrorReductionHours < 0m);
    }

    [Fact]
    public void BenchmarkAggregatesSeveralOpaqueSubjectsDeterministically()
    {
        (_, _, HostReviewPacket packet) = HostReviewMeasurementTestData.Create();
        HostReviewMeasurement[] measurements =
        [
            HostReviewMeasurementTestData.Measurement(
                packet,
                HostReviewMeasurementTestData.Ledger(packet, 0.9m),
                HostReviewContextMode.Compact,
                subject: "subject-b",
                session: "b-compact"),
            HostReviewMeasurementTestData.Measurement(
                packet,
                HostReviewMeasurementTestData.Ledger(packet, 0.7m),
                HostReviewContextMode.BroaderSource,
                subject: "subject-b",
                session: "b-source",
                additionalBytes: 1_000,
                additionalCharacters: 900),
            HostReviewMeasurementTestData.Measurement(
                packet,
                HostReviewMeasurementTestData.Ledger(packet, 0.8m),
                HostReviewContextMode.Compact,
                subject: "subject-a",
                session: "a-compact"),
            HostReviewMeasurementTestData.Measurement(
                packet,
                HostReviewMeasurementTestData.Ledger(packet, 0.6m),
                HostReviewContextMode.BroaderSource,
                subject: "subject-a",
                session: "a-source",
                additionalBytes: 1_500,
                additionalCharacters: 1_300),
        ];

        HostReviewBenchmarkReport first = HostReviewBenchmarkEngine.Build(measurements);
        HostReviewBenchmarkReport second = HostReviewBenchmarkEngine.Build(
            [.. measurements.Reverse()]);

        Assert.Equal(2, first.SubjectCount);
        Assert.Equal(["subject-a", "subject-b"], first.Subjects.Select(subject => subject.SubjectId));
        Assert.Equal(
            ContractJson.SerializeCompact(first),
            ContractJson.SerializeCompact(second));
        Assert.Equal(packet.Candidates.Count * 2, first.CapabilityItems.BaselineAgreement.Expected.SampleCount);
        Assert.Equal(2, first.RepositoryTotals.BaselineAgreement.Expected.SampleCount);
    }

    [Fact]
    public void BenchmarkRequiresOneMatchingContextPair()
    {
        (_, _, HostReviewPacket packet) = HostReviewMeasurementTestData.Create();
        HostReviewMeasurement compact = HostReviewMeasurementTestData.Measurement(
            packet,
            HostReviewMeasurementTestData.Ledger(packet),
            HostReviewContextMode.Compact);

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            HostReviewBenchmarkEngine.Build([compact]));

        Assert.Contains("exactly one", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BenchmarkKeepsUnavailableRatiosNullAndExplained()
    {
        (_, _, HostReviewPacket packet) = HostReviewMeasurementTestData.Create();
        HostReviewMeasurement compact = HostReviewMeasurementTestData.Measurement(
            packet,
            HostReviewMeasurementTestData.Ledger(packet),
            HostReviewContextMode.Compact);
        HostReviewMeasurement source = HostReviewMeasurementTestData.Measurement(
            packet,
            HostReviewMeasurementTestData.Ledger(packet),
            HostReviewContextMode.BroaderSource,
            session: "source-session");

        HostReviewTelemetryComparison telemetry =
            HostReviewBenchmarkEngine.Build([compact, source]).Subjects[0].Telemetry;

        Assert.Null(telemetry.ProviderInputTokenRatio);
        Assert.Null(telemetry.ElapsedTimeRatio);
        Assert.Null(telemetry.MonetaryCostRatio);
        Assert.Null(telemetry.ObservedInputByteRatio);
        Assert.Null(telemetry.ApproximateInputTokenRatio);
        Assert.False(telemetry.BroaderSource.ObservedInputComplete);
        Assert.Equal(4, telemetry.Diagnostics.Count);
    }

    [Fact]
    public void BenchmarkDoesNotCompareDifferentCostCurrencies()
    {
        (_, _, HostReviewPacket packet) = HostReviewMeasurementTestData.Create();
        HostReviewMeasurement compact = HostReviewMeasurementTestData.Measurement(
            packet,
            HostReviewMeasurementTestData.Ledger(packet),
            HostReviewContextMode.Compact,
            cost: 0.25m,
            currency: "USD");
        HostReviewMeasurement source = HostReviewMeasurementTestData.Measurement(
            packet,
            HostReviewMeasurementTestData.Ledger(packet),
            HostReviewContextMode.BroaderSource,
            session: "source-session",
            cost: 0.50m,
            currency: "EUR",
            additionalBytes: 2_000,
            additionalCharacters: 1_800);

        HostReviewTelemetryComparison telemetry =
            HostReviewBenchmarkEngine.Build([compact, source]).Subjects[0].Telemetry;

        Assert.Null(telemetry.MonetaryCostRatio);
        Assert.Contains(telemetry.Diagnostics, diagnostic =>
            diagnostic.Contains("currencies", StringComparison.Ordinal));
    }

    [Fact]
    public void BenchmarkHonorsPreCancelledToken()
    {
        (_, _, HostReviewPacket packet) = HostReviewMeasurementTestData.Create();
        HostReviewMeasurement compact = HostReviewMeasurementTestData.Measurement(
            packet,
            HostReviewMeasurementTestData.Ledger(packet),
            HostReviewContextMode.Compact);
        HostReviewMeasurement source = HostReviewMeasurementTestData.Measurement(
            packet,
            HostReviewMeasurementTestData.Ledger(packet),
            HostReviewContextMode.BroaderSource,
            session: "source-session");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            HostReviewBenchmarkEngine.Build([compact, source], cancellation.Token));
    }
}
