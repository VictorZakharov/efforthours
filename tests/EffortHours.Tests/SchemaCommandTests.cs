using EffortHours.Cli;

namespace EffortHours.Tests;

public sealed class SchemaCommandTests
{
    [Fact]
    public async Task ShowAcceptsBareSchemaStemListedWithFilenameSuffix()
    {
        using StringWriter listOutput = new();
        using StringWriter showOutput = new();
        using StringWriter error = new();
        EffortHoursApplication application = new();

        int listExit = await application.RunAsync(
            ["schema", "list"],
            listOutput,
            error,
            CancellationToken.None);
        int showExit = await application.RunAsync(
            ["schema", "show", "change-portfolio-capacity-manifest"],
            showOutput,
            error,
            CancellationToken.None);

        Assert.Equal(CliExitCodes.Success, listExit);
        Assert.Equal(CliExitCodes.Success, showExit);
        Assert.Contains(
            "change-portfolio-capacity-manifest.schema.json",
            listOutput.ToString(),
            StringComparison.Ordinal);
        Assert.Contains(
            "EffortHours change portfolio reference-capacity manifest v1",
            showOutput.ToString(),
            StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }
}
