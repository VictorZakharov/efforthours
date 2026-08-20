using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed class RuntimeConfigurationTests
{
    [Theory]
    [InlineData("src", "EffortHours.Cli", "efforthours.runtimeconfig.json")]
    [InlineData(
        "benchmarks",
        "EffortHours.ChangeBenchmarks",
        "EffortHours.ChangeBenchmarks.runtimeconfig.json")]
    public void PerformanceExecutablesUseServerGarbageCollection(
        string rootDirectory,
        string projectDirectory,
        string runtimeConfigurationFile)
    {
        string path = Path.Combine(
            FindRepositoryRoot(),
            rootDirectory,
            projectDirectory,
            "bin",
            Configuration,
            "net10.0",
            runtimeConfigurationFile);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        Assert.True(document.RootElement
            .GetProperty("runtimeOptions")
            .GetProperty("configProperties")
            .GetProperty("System.GC.Server")
            .GetBoolean());
    }

#if DEBUG
    private const string Configuration = "Debug";
#else
    private const string Configuration = "Release";
#endif

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EffortHours.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the EffortHours repository root.");
    }
}
