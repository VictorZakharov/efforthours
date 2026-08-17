using System.Diagnostics;
using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed class CppCliTests
{
    [Fact]
    public async Task ScanAndEstimateProduceStaticCppEvidenceWithoutSourceDisclosureOrTargetMutation()
    {
        using CppRepository repository = new();
        repository.WriteText(
            "CMakeLists.txt",
            "set(CMAKE_CXX_STANDARD 20)\nfind_package(gRPC REQUIRED)\n" +
            "add_executable(app src/main.cpp)\ntarget_link_libraries(app PRIVATE gRPC)\n");
        repository.WriteText(
            "src/main.cpp",
            "#include <grpcpp/grpcpp.h>\nconst char* marker = \"synthetic-private-cpp-marker\";\n" +
            "struct Server { void route(); }; int main() { Server server; server.route(); return 0; }\n");
        Dictionary<string, byte[]> before = repository.ReadAllBytes();

        ProcessResult scan = await RunCliAsync("scan", repository.RootPath);
        ProcessResult repeatedScan = await RunCliAsync("scan", repository.RootPath);
        ProcessResult estimate = await RunCliAsync("estimate", repository.RootPath, "--no-rate", "--compact");

        Assert.Equal(0, scan.ExitCode);
        Assert.Equal(0, repeatedScan.ExitCode);
        Assert.Equal(0, estimate.ExitCode);
        Assert.Equal(string.Empty, scan.StandardError);
        Assert.Equal(string.Empty, repeatedScan.StandardError);
        Assert.Equal(string.Empty, estimate.StandardError);
        Assert.Equal(scan.StandardOutput, repeatedScan.StandardOutput);
        Assert.DoesNotContain("synthetic-private-cpp-marker", scan.StandardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(repository.RootPath, scan.StandardOutput, StringComparison.OrdinalIgnoreCase);
        using JsonDocument scanDocument = JsonDocument.Parse(scan.StandardOutput);
        Assert.Contains(
            scanDocument.RootElement.GetProperty("repository").GetProperty("ecosystems").EnumerateArray(),
            ecosystem => ecosystem.GetString() == "cpp");
        Assert.Contains(
            scanDocument.RootElement.GetProperty("facts").EnumerateArray(),
            fact => fact.GetProperty("kind").GetString() == "api-surface" &&
                fact.GetProperty("provenance").GetProperty("analyzer").GetString() ==
                    "efforthours.cpp-analyzer");

        using JsonDocument estimateDocument = JsonDocument.Parse(estimate.StandardOutput);
        Assert.Equal("seed-rules/0.4.0", estimateDocument.RootElement.GetProperty("estimatorVersion").GetString());
        Assert.Contains(
            estimateDocument.RootElement.GetProperty("categories").EnumerateArray(),
            category => category.GetProperty("category").GetString() == "production-implementation");
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), repository.ReadAllBytes().Keys.Order(StringComparer.Ordinal));
        Assert.All(before, pair => Assert.Equal(pair.Value, repository.ReadAllBytes()[pair.Key]));
    }

    [Fact]
    public async Task DirectoryPairChangeUsesCppNormalizationAndSemanticRoutingThroughTheCli()
    {
        using CppRepository before = new();
        using CppRepository after = new();
        before.WriteText("src/service.cpp", "const char* value() { return R\"tag(before)tag\"; }\n");
        after.WriteText("src/service.cpp", "const char * value( ) { return R\"tag(after)tag\" ; }\n");

        ProcessResult result = await RunCliAsync(
            "change",
            "--base-path",
            before.RootPath,
            "--head-path",
            after.RootPath,
            "--no-rate",
            "--compact");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardError);
        Assert.DoesNotContain(before.RootPath, result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(after.RootPath, result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
        Assert.Equal(
            "change-seed/0.18.2+seed-rules/0.4.0",
            document.RootElement.GetProperty("estimatorVersion").GetString());
        Assert.Contains(
            document.RootElement.GetProperty("categories").EnumerateArray(),
            category => category.GetProperty("category").GetString() == "production-implementation");
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
        foreach (string argument in arguments) startInfo.ArgumentList.Add(argument);

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
            if (File.Exists(Path.Combine(directory.FullName, "EffortHours.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Could not locate the repository root.");
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class CppRepository : IDisposable
    {
        public CppRepository()
        {
            RootPath = Path.Combine(Path.GetTempPath(), "efforthours-cpp-e2e", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(RootPath);
        }

        public string RootPath { get; }

        public void WriteText(string path, string content)
        {
            string fullPath = Path.Combine(RootPath, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }

        public Dictionary<string, byte[]> ReadAllBytes() => Directory
            .GetFiles(RootPath, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(RootPath, path).Replace('\\', '/'),
                File.ReadAllBytes,
                StringComparer.Ordinal);

        public void Dispose()
        {
            string resolved = Path.GetFullPath(RootPath);
            string expectedParent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "efforthours-cpp-e2e"));
            if (resolved.StartsWith(expectedParent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                Directory.Delete(resolved, recursive: true);
        }
    }
}
