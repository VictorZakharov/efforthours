using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    [Fact]
    public async Task CompleteRevertReportsBoundedReworkLikeShareAndSupportsExplanation()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("Demo.csproj", ProjectFile);
        string baseObjectId = await repository.CommitAsync("base");
        repository.WriteText(
            "Temporary.cs",
            "namespace Demo; public sealed class Temporary { public int Value => 1; }\n");
        _ = await repository.CommitAsync("temporary feature");
        File.Delete(Path.Combine(repository.RootPath, "Temporary.cs"));
        string headObjectId = await repository.CommitAsync("revert temporary feature");
        string reportPath = Path.Combine(repository.RootPath, "change-report.json");

        ProcessResult estimate = await RunCliAsync(
            "change",
            repository.RootPath,
            "--range",
            $"{baseObjectId}..{headObjectId}",
            "--no-rate",
            "--compact",
            "--output",
            reportPath);

        Assert.Equal(0, estimate.ExitCode);
        Assert.Equal(string.Empty, estimate.StandardError);
        using JsonDocument report = JsonDocument.Parse(await File.ReadAllTextAsync(reportPath));
        JsonElement normalization = report.RootElement
            .GetProperty("reconciliation")
            .GetProperty("normalization");
        Assert.Equal(1m, normalization.GetProperty("expectedGrossToFinalNormalizationShare").GetDecimal());
        decimal reworkLikeShare = normalization.GetProperty("expectedReworkLikeShare").GetDecimal();
        Assert.InRange(reworkLikeShare, 0.0001m, 1m);
        Assert.True(normalization.GetProperty("expectedRevertHours").GetDecimal() > 0m);
        string normalizationId = normalization.GetProperty("id").GetString()!;

        ProcessResult explanation = await RunCliAsync(
            "change",
            "explain",
            reportPath,
            "--item",
            normalizationId,
            "--compact");

        Assert.Equal(0, explanation.ExitCode);
        Assert.Equal(string.Empty, explanation.StandardError);
        using JsonDocument explanationJson = JsonDocument.Parse(explanation.StandardOutput);
        Assert.Equal(normalizationId, explanationJson.RootElement.GetProperty("requestedId").GetString());
        Assert.Equal(0, explanationJson.RootElement.GetProperty("workItems").GetArrayLength());
        Assert.True(explanationJson.RootElement.GetProperty("adjustments").GetArrayLength() > 0);
    }
}
