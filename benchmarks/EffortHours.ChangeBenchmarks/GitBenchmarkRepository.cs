using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace EffortHours.ChangeBenchmarks;

internal sealed class GitBenchmarkRepository : IDisposable
{
    private readonly bool _keep;

    private GitBenchmarkRepository(
        string rootPath,
        string baseObjectId,
        string headObjectId,
        bool keep)
    {
        RootPath = rootPath;
        BaseObjectId = baseObjectId;
        HeadObjectId = headObjectId;
        _keep = keep;
    }

    public string RootPath { get; }

    public string BaseObjectId { get; }

    public string HeadObjectId { get; }

    public static async Task<GitBenchmarkRepository> CreateAsync(
        ChangeBenchmarkOptions options,
        CancellationToken cancellationToken)
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "efforthours-change-benchmark",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await GitAsync(root, cancellationToken, "init", "--quiet", "--initial-branch=main")
                .ConfigureAwait(false);
            await GitAsync(root, cancellationToken, "config", "user.name", "EffortHours Benchmark")
                .ConfigureAwait(false);
            await GitAsync(
                root,
                cancellationToken,
                "config",
                "user.email",
                "efforthours-benchmark@example.invalid").ConfigureAwait(false);
            await GitAsync(root, cancellationToken, "config", "gc.auto", "0").ConfigureAwait(false);
            await GitAsync(root, cancellationToken, "config", "maintenance.auto", "false")
                .ConfigureAwait(false);

            WriteText(
                root,
                "Benchmark.csproj",
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
                "<TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n");
            for (int index = 0; index < options.Files; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WriteText(root, $"src/Type{index:D6}.cs", SourceFile(index, options.LinesPerFile));
            }

            string baseObjectId = await CommitAsync(root, "base tree", cancellationToken)
                .ConfigureAwait(false);
            string headObjectId;
            if (options.Mode == ChangeBenchmarkMode.LargeTree)
            {
                WriteText(
                    root,
                    "src/Type000000.cs",
                    SourceFile(0, options.LinesPerFile, finalValue: 1));
                headObjectId = await CommitAsync(root, "final tree delta", cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                headObjectId = baseObjectId;
                for (int index = 1; index <= options.Commits; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    WriteText(
                        root,
                        "src/RangeChange.cs",
                        RangeSourceFile(index, options.LinesPerFile));
                    headObjectId = await CommitAsync(
                        root,
                        $"range component {index.ToString(CultureInfo.InvariantCulture)}",
                        cancellationToken).ConfigureAwait(false);
                }
            }

            return new GitBenchmarkRepository(root, baseObjectId, headObjectId, options.KeepRepository);
        }
        catch
        {
            DeleteRepository(root);
            throw;
        }
    }

    public void Dispose()
    {
        if (!_keep)
        {
            DeleteRepository(RootPath);
        }
    }

    private static async Task<string> CommitAsync(
        string root,
        string message,
        CancellationToken cancellationToken)
    {
        await GitAsync(root, cancellationToken, "add", "--all").ConfigureAwait(false);
        await GitAsync(root, cancellationToken, "commit", "--quiet", "-m", message)
            .ConfigureAwait(false);
        return await GitAsync(root, cancellationToken, "rev-parse", "HEAD").ConfigureAwait(false);
    }

    private static async Task<string> GitAsync(
        string root,
        CancellationToken cancellationToken,
        params string[] arguments)
    {
        ProcessStartInfo startInfo = new("git")
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start Git for the Change benchmark.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        string output = (await stdout.ConfigureAwait(false)).Trim();
        string error = (await stderr.ConfigureAwait(false)).Trim();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {string.Join(' ', arguments)} failed with exit {process.ExitCode}: {error}");
        }

        return output;
    }

    private static string SourceFile(int index, int lines, int finalValue = 0)
    {
        StringBuilder content = new();
        content.AppendLine("namespace EffortHours.Benchmark;");
        content.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"public static class Type{index:D6}"));
        content.AppendLine("{");
        int memberCount = Math.Max(1, lines - 4);
        for (int line = 0; line < memberCount; line++)
        {
            int value = line == 0 ? finalValue : line;
            content.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"    public static int Value{line:D4} => {value};"));
        }

        content.AppendLine("}");
        return content.ToString();
    }

    private static string RangeSourceFile(int revision, int lines)
    {
        StringBuilder content = new();
        content.AppendLine("namespace EffortHours.Benchmark;");
        content.AppendLine("public static class RangeChange");
        content.AppendLine("{");
        content.AppendLine(string.Create(
            CultureInfo.InvariantCulture,
            $"    public static int Revision => {revision};"));
        for (int line = 4; line < lines; line++)
        {
            content.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"    public static int Stable{line:D4} => {line};"));
        }

        content.AppendLine("}");
        return content.ToString();
    }

    private static void WriteText(string root, string relativePath, string content)
    {
        string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    private static void DeleteRepository(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(path, FileAttributes.Normal);
        }

        Directory.Delete(root, recursive: true);
    }
}
