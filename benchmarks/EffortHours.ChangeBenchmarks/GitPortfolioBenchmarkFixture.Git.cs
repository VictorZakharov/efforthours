using System.Globalization;

namespace EffortHours.ChangeBenchmarks;

internal sealed partial class GitPortfolioBenchmarkFixture
{
    internal const int MaximumActiveContextProjects = 80;

    private static async Task InitializeRepositoryAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await GitBenchmarkRepository.GitAsync(
            path,
            cancellationToken,
            "init",
            "--quiet",
            "--initial-branch=main").ConfigureAwait(false);
        await ConfigureRepositoryAsync(path, cancellationToken).ConfigureAwait(false);
    }

    private static async Task ConfigureRepositoryAsync(
        string path,
        CancellationToken cancellationToken)
    {
        await GitBenchmarkRepository.GitAsync(
            path,
            cancellationToken,
            "config",
            "user.name",
            "EffortHours Portfolio Benchmark").ConfigureAwait(false);
        await GitBenchmarkRepository.GitAsync(
            path,
            cancellationToken,
            "config",
            "user.email",
            "portfolio-benchmark@example.invalid").ConfigureAwait(false);
        await GitBenchmarkRepository.GitAsync(path, cancellationToken, "config", "gc.auto", "0")
            .ConfigureAwait(false);
        await GitBenchmarkRepository.GitAsync(
            path,
            cancellationToken,
            "config",
            "maintenance.auto",
            "false").ConfigureAwait(false);
    }

    private static void WriteBaseTree(
        string path,
        ChangeBenchmarkOptions options,
        CancellationToken cancellationToken)
    {
        GitBenchmarkRepository.WriteText(
            path,
            "Benchmark.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
                "<TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>\n");
        for (int index = 0; index < options.ContextProjects; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string projectPath = index < MaximumActiveContextProjects
                ? $"src/area-00/component-000000/Context-{index:D4}.csproj"
                : $"projects/unrelated-{index:D4}/Unrelated.csproj";
            GitBenchmarkRepository.WriteText(
                path,
                projectPath,
                "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>" +
                    "<TargetFramework>net10.0</TargetFramework>" +
                    $"<AssemblyName>Context{index:D4}</AssemblyName>" +
                    "</PropertyGroup></Project>\n");
        }

        for (int index = 0; index < options.Files; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GitBenchmarkRepository.WriteText(
                path,
                GitBenchmarkRepository.SourcePath(index, nested: true),
                GitBenchmarkRepository.SourceFile(index, options.LinesPerFile));
        }
    }

    private static async Task<string> CreateMergeFanoutAsync(
        string path,
        string baseObjectId,
        CancellationToken cancellationToken)
    {
        List<string> branches = [];
        for (int index = 0; index < FanoutBranches; index++)
        {
            string branch = $"fanout-{index:D2}";
            branches.Add(branch);
            await GitBenchmarkRepository.GitAsync(
                path,
                cancellationToken,
                "switch",
                "--quiet",
                "--create",
                branch,
                baseObjectId).ConfigureAwait(false);
            string changedPath = $"fanout/branch-{index:D2}.txt";
            GitBenchmarkRepository.WriteText(
                path,
                changedPath,
                $"synthetic merge-fanout branch {index:D2}\n");
            _ = await GitBenchmarkRepository.CommitAsync(
                path,
                $"fanout branch {index:D2}",
                cancellationToken,
                paths: [changedPath]).ConfigureAwait(false);
        }

        await GitBenchmarkRepository.GitAsync(
            path,
            cancellationToken,
            "switch",
            "--quiet",
            "main").ConfigureAwait(false);
        await GitBenchmarkRepository.GitAsync(
            path,
            cancellationToken,
            [
                "merge",
                "--quiet",
                "--no-ff",
                "-m",
                "merge synthetic fanout",
                .. branches,
            ]).ConfigureAwait(false);
        return await GitBenchmarkRepository.GitAsync(path, cancellationToken, "rev-parse", "HEAD")
            .ConfigureAwait(false);
    }

    private static async Task<string> SelectedCommitAsync(
        string path,
        ChangeBenchmarkOptions options,
        int finalValue,
        string message,
        string authorName,
        string authorEmail,
        string authorDate,
        CancellationToken cancellationToken)
    {
        string changedPath = GitBenchmarkRepository.SourcePath(0, nested: true);
        GitBenchmarkRepository.WriteText(
            path,
            changedPath,
            GitBenchmarkRepository.SourceFile(0, options.LinesPerFile, finalValue));
        return await GitBenchmarkRepository.CommitAsync(
            path,
            message,
            cancellationToken,
            authorName,
            authorEmail,
            authorDate,
            [changedPath]).ConfigureAwait(false);
    }

    private static async Task<string> SelectedSeriesAsync(
        string path,
        ChangeBenchmarkOptions options,
        int count,
        int firstValue,
        int firstMinute,
        string messagePrefix,
        string authorName,
        string authorEmail,
        CancellationToken cancellationToken)
    {
        string head = await GitBenchmarkRepository.GitAsync(
            path,
            cancellationToken,
            "rev-parse",
            "HEAD").ConfigureAwait(false);
        DateTimeOffset firstTimestamp = new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero)
            .AddMinutes(firstMinute);
        for (int index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            head = await SelectedCommitAsync(
                path,
                options,
                firstValue + index,
                $"{messagePrefix} {index + 1:D3}",
                authorName,
                authorEmail,
                firstTimestamp.AddSeconds(index).ToString("O", CultureInfo.InvariantCulture),
                cancellationToken).ConfigureAwait(false);
        }

        return head;
    }

    private static async Task<string> UnselectedHeadAsync(
        string path,
        string shared,
        string branch,
        CancellationToken cancellationToken)
    {
        await GitBenchmarkRepository.GitAsync(
            path,
            cancellationToken,
            "switch",
            "--quiet",
            "--create",
            branch,
            shared).ConfigureAwait(false);
        const string changedPath = "notes/unselected.md";
        GitBenchmarkRepository.WriteText(path, changedPath, "# Unselected head\n");
        return await GitBenchmarkRepository.CommitAsync(
            path,
            "unselected overlap head",
            cancellationToken,
            paths: [changedPath]).ConfigureAwait(false);
    }

    private static async Task CloneRepositoryAsync(
        string source,
        string target,
        CancellationToken cancellationToken)
    {
        string parent = Path.GetDirectoryName(target)!;
        await GitBenchmarkRepository.GitAsync(
            parent,
            cancellationToken,
            "clone",
            "--quiet",
            "--no-hardlinks",
            source,
            target).ConfigureAwait(false);
    }
}
