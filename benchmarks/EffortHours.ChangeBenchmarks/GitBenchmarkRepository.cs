using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace EffortHours.ChangeBenchmarks;

internal sealed class GitBenchmarkRepository : IDisposable
{
    public const string SelectedAuthorName = "Selected Benchmark Author";
    public const string SelectedAuthorEmail = "selected-author@example.invalid";

    private const int AuthorPeriodFanoutBranches = 8;
    private readonly bool _keep;

    private GitBenchmarkRepository(
        string rootPath,
        string baseObjectId,
        string headObjectId,
        int headFileCount,
        int headDirectoryCount,
        bool keep)
    {
        RootPath = rootPath;
        BaseObjectId = baseObjectId;
        HeadObjectId = headObjectId;
        HeadFileCount = headFileCount;
        HeadDirectoryCount = headDirectoryCount;
        _keep = keep;
    }

    public string RootPath { get; }

    public string BaseObjectId { get; }

    public string HeadObjectId { get; }

    public int HeadFileCount { get; }

    public int HeadDirectoryCount { get; }

    public long EstimatedLegacyEntryComparisonsPerSnapshot =>
        (long)HeadDirectoryCount * (HeadDirectoryCount + HeadFileCount);

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
                WriteText(
                    root,
                    SourcePath(index, options.Mode == ChangeBenchmarkMode.AuthorPeriod),
                    SourceFile(index, options.LinesPerFile));
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
            else if (options.Mode == ChangeBenchmarkMode.LongRange)
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
            else
            {
                List<string> branches = [];
                for (int index = 0; index < AuthorPeriodFanoutBranches; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string branch = $"fanout-{index:D2}";
                    branches.Add(branch);
                    await GitAsync(
                        root,
                        cancellationToken,
                        "switch",
                        "--quiet",
                        "--create",
                        branch,
                        baseObjectId).ConfigureAwait(false);
                    WriteText(
                        root,
                        $"fanout/branch-{index:D2}.txt",
                        $"synthetic merge-fanout branch {index:D2}\n");
                    _ = await CommitAsync(root, $"fanout branch {index:D2}", cancellationToken)
                        .ConfigureAwait(false);
                }

                await GitAsync(root, cancellationToken, "switch", "--quiet", "main")
                    .ConfigureAwait(false);
                await GitAsync(
                    root,
                    cancellationToken,
                    [
                        "merge",
                        "--quiet",
                        "--no-ff",
                        "-m",
                        "merge synthetic fanout",
                        .. branches,
                    ]).ConfigureAwait(false);
                headObjectId = await GitAsync(root, cancellationToken, "rev-parse", "HEAD")
                    .ConfigureAwait(false);
                for (int index = 1; index <= options.Commits; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    const int sourceIndex = 0;
                    WriteText(
                        root,
                        SourcePath(sourceIndex, nested: true),
                        SourceFile(sourceIndex, options.LinesPerFile, finalValue: index));
                    headObjectId = await CommitAsync(
                        root,
                        $"selected author change {index:D2}",
                        cancellationToken,
                        SelectedAuthorName,
                        SelectedAuthorEmail,
                        $"2026-08-14T{index:D2}:00:00+00:00").ConfigureAwait(false);
                }
            }

            (int headFiles, int headDirectories) = await CountHeadTreeAsync(
                root,
                headObjectId,
                cancellationToken).ConfigureAwait(false);
            return new GitBenchmarkRepository(
                root,
                baseObjectId,
                headObjectId,
                headFiles,
                headDirectories,
                options.KeepRepository);
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

    internal static async Task<string> CommitAsync(
        string root,
        string message,
        CancellationToken cancellationToken,
        string? authorName = null,
        string? authorEmail = null,
        string? authorDate = null)
    {
        await GitAsync(root, cancellationToken, "add", "--all").ConfigureAwait(false);
        List<string> arguments = [];
        if (authorName is not null && authorEmail is not null)
        {
            arguments.AddRange(["-c", $"user.name={authorName}", "-c", $"user.email={authorEmail}"]);
        }

        arguments.AddRange(["commit", "--quiet"]);
        if (authorDate is not null)
        {
            arguments.Add($"--date={authorDate}");
        }

        arguments.AddRange(["-m", message]);
        await GitAsync(root, cancellationToken, [.. arguments]).ConfigureAwait(false);
        return await GitAsync(root, cancellationToken, "rev-parse", "HEAD").ConfigureAwait(false);
    }

    internal static async Task<(int Files, int Directories)> CountHeadTreeAsync(
        string root,
        string headObjectId,
        CancellationToken cancellationToken)
    {
        string output = await GitAsync(
            root,
            cancellationToken,
            "ls-tree",
            "-r",
            "--name-only",
            headObjectId).ConfigureAwait(false);
        string[] files = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        HashSet<string> directories = new(StringComparer.Ordinal) { string.Empty };
        foreach (string file in files)
        {
            string[] segments = file.Split('/');
            string directory = string.Empty;
            for (int index = 0; index < segments.Length - 1; index++)
            {
                directory = directory.Length == 0
                    ? segments[index]
                    : $"{directory}/{segments[index]}";
                directories.Add(directory);
            }
        }

        return (files.Length, directories.Count);
    }

    internal static async Task<string> GitAsync(
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

    internal static string SourceFile(int index, int lines, int finalValue = 0)
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

    internal static string SourcePath(int index, bool nested) => nested
        ? $"src/area-{index % 100:D2}/component-{index:D6}/Type{index:D6}.cs"
        : $"src/Type{index:D6}.cs";

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

    internal static void WriteText(string root, string relativePath, string content)
    {
        string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    internal static void DeleteRepository(string root)
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
