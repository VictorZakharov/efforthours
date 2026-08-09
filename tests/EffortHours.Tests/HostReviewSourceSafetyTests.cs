using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;
using EffortHours.Review;

namespace EffortHours.Tests;

public sealed class HostReviewSourceSafetyTests
{
    [Fact]
    public async Task SelectedSourceEnforcesContainmentLinksSizeEncodingAndOutputBounds()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "Demo.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n");
        repository.WriteText("Program.cs", "namespace Demo; public static class Program { }\n");
        repository.WriteText("linked/Linked.cs", "namespace Linked; public sealed class Value { }\n");
        repository.WriteText("Huge.cs", "namespace Small;\n");
        repository.WriteBytes("Invalid.cs", [0xC3, 0x28]);
        repository.WriteText(
            "Long.cs",
            new string('x', HostReviewProtocol.MaximumSourceCharacters + 100) + "\n");

        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(repository)
            .ScanAsync(repository.RootPath);
        ReviewState state = CreateState(evidence);
        HostReviewSourceContext context = new(repository.RootPath, repository);

        repository.SetAttributes(
            "linked",
            FileAttributes.Directory | FileAttributes.ReparsePoint);
        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecuteSourceAsync(
            state,
            context,
            "linked/Linked.cs"));

        repository.WriteBytes(
            "Huge.cs",
            new byte[HostReviewProtocol.MaximumSourceBytes + 1]);
        InvalidOperationException sizeError = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ExecuteSourceAsync(state, context, "Huge.cs"));
        Assert.Contains("safety ceiling", sizeError.Message, StringComparison.Ordinal);

        InvalidOperationException encodingError = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ExecuteSourceAsync(state, context, "Invalid.cs"));
        Assert.Contains("not valid UTF-8", encodingError.Message, StringComparison.Ordinal);

        HostReviewQueryResult bounded = await ExecuteSourceAsync(
            state,
            context,
            "Long.cs");
        Assert.NotNull(bounded.SourceExcerpt);
        Assert.True(bounded.SourceExcerpt.ContentTruncated);
        Assert.Equal(
            HostReviewProtocol.MaximumSourceCharacters,
            bounded.SourceExcerpt.Lines.Sum(line => line.Text.Length));
        Assert.True(bounded.Omissions.SourceCharacterCount > 0);

        RepositoryEvidence escapedEvidence = evidence with
        {
            Facts =
            [
                .. evidence.Facts,
                new EvidenceFact
                {
                    Id = "file:../outside.cs",
                    Kind = EvidenceKinds.File,
                    Scope = "../outside.cs",
                    Summary = "Synthetic escaping path for containment validation.",
                    Provenance = new EvidenceProvenance
                    {
                        SourceKind = EvidenceSourceKind.Observed,
                        Analyzer = "memory-safety-test",
                        AnalyzerVersion = "1.0.0",
                        Method = "synthetic invalid path",
                    },
                    Locations = [new EvidenceLocation { Path = "../outside.cs" }],
                    Tags = ["sha256:" + new string('0', 64)],
                },
            ],
        };
        ReviewState escapedState = CreateState(escapedEvidence);
        InvalidOperationException containmentError = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ExecuteSourceAsync(escapedState, context, "../outside.cs"));
        Assert.Contains("escapes", containmentError.Message, StringComparison.Ordinal);
    }

    private static ReviewState CreateState(RepositoryEvidence evidence)
    {
        EstimateReport report = new SeedEstimator().Estimate(
            evidence,
            EstimationProfile.Implementation,
            rateCard: null);
        HostReviewPacket packet = HostReviewPacketBuilder.Build(report, evidence);
        return new ReviewState(report, evidence, packet.InputDigest);
    }

    private static Task<HostReviewQueryResult> ExecuteSourceAsync(
        ReviewState state,
        HostReviewSourceContext context,
        string path) => HostReviewQueryEngine.ExecuteAsync(
            state.Report,
            state.Evidence,
            new HostReviewQuery
            {
                Kind = HostReviewQueryKind.SelectedSource,
                Selector = path,
                Reason = "Exercise selected-source safety.",
                Profile = state.Report.Profile,
                InputDigest = state.InputDigest,
                StartLine = 1,
                LineCount = 1,
            },
            context);

    private sealed record ReviewState(
        EstimateReport Report,
        RepositoryEvidence Evidence,
        string InputDigest);
}
