using System.Diagnostics;
using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed class SqlCliTests
{
    [Fact]
    public async Task ScanAndEstimateProduceStaticSqlEvidenceWithoutSourceDisclosure()
    {
        using SqlRepository repository = new();
        repository.WriteText(
            "migrations/V001__orders.sql",
            """
            -- synthetic-private-sql-marker
            CREATE TABLE orders (
                id BIGSERIAL PRIMARY KEY,
                payload JSONB NOT NULL
            );
            CREATE INDEX ix_orders_payload ON orders USING GIN(payload);
            SELECT id FROM orders WHERE payload::text ILIKE 'paid';
            """);

        ProcessResult scan = await RunCliAsync("scan", repository.RootPath);
        ProcessResult estimate = await RunCliAsync(
            "estimate",
            repository.RootPath,
            "--no-rate",
            "--compact");

        Assert.Equal(0, scan.ExitCode);
        Assert.Equal(0, estimate.ExitCode);
        Assert.Equal(string.Empty, scan.StandardError);
        Assert.Equal(string.Empty, estimate.StandardError);
        Assert.DoesNotContain("synthetic-private-sql-marker", scan.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(repository.RootPath, scan.StandardOutput, StringComparison.OrdinalIgnoreCase);
        using JsonDocument scanDocument = JsonDocument.Parse(scan.StandardOutput);
        Assert.Contains(
            scanDocument.RootElement.GetProperty("repository").GetProperty("ecosystems").EnumerateArray(),
            ecosystem => ecosystem.GetString() == "sql");
        JsonElement sql = Assert.Single(
            scanDocument.RootElement.GetProperty("facts").EnumerateArray(),
            fact => fact.GetProperty("kind").GetString() == "sql-artifact");
        Assert.Contains(
            sql.GetProperty("tags").EnumerateArray(),
            tag => tag.GetString() == "dialect:postgresql");
        Assert.Contains(
            sql.GetProperty("tags").EnumerateArray(),
            tag => tag.GetString() == "sql-role:migration");

        using JsonDocument estimateDocument = JsonDocument.Parse(estimate.StandardOutput);
        Assert.Equal(
            "seed-rules/0.4.0",
            estimateDocument.RootElement.GetProperty("estimatorVersion").GetString());
        Assert.Contains(
            estimateDocument.RootElement.GetProperty("categories").EnumerateArray(),
            category => category.GetProperty("category").GetString() ==
                "data-modeling-persistence-and-migrations");
    }

    private static async Task<ProcessResult> RunCliAsync(params string[] arguments)
    {
        string repositoryRoot = FindRepositoryRoot();
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Could not determine the test build configuration.");
        string cliAssembly = Path.Combine(
            repositoryRoot,
            "src",
            "EffortHours.Cli",
            "bin",
            configuration,
            "net10.0",
            "efforthours.dll");
        ProcessStartInfo startInfo = new("dotnet")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["DOTNET_NOLOGO"] = "1";
        startInfo.ArgumentList.Add(cliAssembly);
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)!;
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);
        return new ProcessResult(
            process.ExitCode,
            (await stdout).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd(),
            (await stderr).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd());
    }

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

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class SqlRepository : IDisposable
    {
        public SqlRepository()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                "efforthours-sql-e2e",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public void WriteText(string relativePath, string content)
        {
            string path = Path.Combine(
                RootPath,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
        }

        public void Dispose() => Directory.Delete(RootPath, recursive: true);
    }
}
