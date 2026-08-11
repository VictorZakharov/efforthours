using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeCliTests
{
    [Fact]
    public async Task AuthorPeriodPortfolioIsDeterministicOfflineAndDoesNotMutateWorktree()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        await repository.GitAsync("config", "user.name", "Baseline Author");
        await repository.GitAsync("config", "user.email", "baseline@example.invalid");
        repository.WriteText("Demo.csproj", ProjectFile);
        _ = await repository.CommitAsync("base");
        await repository.GitAsync("config", "user.name", "Selected Contributor");
        await repository.GitAsync("config", "user.email", "selected@example.invalid");
        repository.WriteText(
            "Alpha.cs",
            "namespace Demo; public sealed class Alpha { public int Value => 1; }\n");
        _ = await repository.CommitAsync("alpha");
        repository.WriteText(
            "Beta.cs",
            "namespace Demo; public sealed class Beta { public bool Enabled => true; }\n");
        _ = await repository.CommitAsync("beta");
        repository.WriteText("untracked.txt", "keep me\n");
        string statusBefore = await repository.GitAsync("status", "--porcelain=v1");
        string[] arguments =
        [
            "change",
            "portfolio",
            repository.RootPath,
            "--author", "selected@example.invalid",
            "--since", "2020-01-01T00:00:00Z",
            "--until", "2030-01-01T00:00:00Z",
            "--timezone", "UTC",
            "--no-rate",
            "--compact",
        ];

        ProcessResult first = await RunCliAsync(arguments);
        ProcessResult second = await RunCliAsync(arguments);

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(string.Empty, first.StandardError);
        Assert.Equal(first.StandardOutput, second.StandardOutput);
        Assert.Equal(statusBefore, await repository.GitAsync("status", "--porcelain=v1"));
        Assert.DoesNotContain(repository.RootPath, first.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("public sealed class Alpha", first.StandardOutput, StringComparison.Ordinal);
        using JsonDocument report = JsonDocument.Parse(first.StandardOutput);
        Assert.Equal(
            "author-period",
            report.RootElement.GetProperty("selection").GetProperty("kind").GetString());
        JsonElement items = report.RootElement.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.All(items.EnumerateArray(), item =>
        {
            Assert.Equal("commit", item.GetProperty("selection").GetProperty("kind").GetString());
            Assert.Equal("direct-author", item.GetProperty("attribution").GetProperty("kind").GetString());
            Assert.True(item.GetProperty("allocatedExpectedHours").GetDecimal() >= 0m);
        });
        decimal allocated = items.EnumerateArray()
            .Sum(item => item.GetProperty("allocatedExpectedHours").GetDecimal());
        Assert.Equal(
            report.RootElement.GetProperty("totalEffort").GetProperty("expected").GetDecimal(),
            allocated);
        Assert.Contains(
            report.RootElement.GetProperty("diagnostics").EnumerateArray(),
            diagnostic => diagnostic.GetProperty("code").GetString() == "FB5310");
    }

    [Fact]
    public async Task AuthorPeriodPortfolioSelectsStructuredCoauthorTrailerWithoutEmittingMessage()
    {
        using GitFixture repository = await GitFixture.CreateAsync();
        repository.WriteText("Demo.csproj", ProjectFile);
        _ = await repository.CommitAsync("base");
        repository.WriteText(
            "Feature.cs",
            "namespace Demo; public sealed class Feature { public int Value => 1; }\n");
        const string message =
            "coauthored feature\n\nCo-authored-by: Selected Contributor <selected@example.invalid>";
        _ = await repository.CommitAsync(message);

        ProcessResult result = await RunCliAsync(
            "change",
            "portfolio",
            repository.RootPath,
            "--author", "selected@example.invalid",
            "--since", "2020-01-01T00:00:00Z",
            "--until", "2030-01-01T00:00:00Z",
            "--timezone", "UTC",
            "--no-rate",
            "--compact");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Assert.DoesNotContain("coauthored feature", result.StandardOutput, StringComparison.Ordinal);
        using JsonDocument report = JsonDocument.Parse(result.StandardOutput);
        JsonElement item = Assert.Single(report.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("coauthor", item.GetProperty("attribution").GetProperty("kind").GetString());
        Assert.Contains(
            item.GetProperty("attribution").GetProperty("ambiguityReasons").EnumerateArray(),
            reason => reason.GetString()!.Contains("shared-credit", StringComparison.Ordinal));
    }
}
