namespace EffortHours.ChangeBenchmarks;

internal sealed partial class GitPortfolioBenchmarkFixture
{
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
            GitBenchmarkRepository.WriteText(
                path,
                $"fanout/branch-{index:D2}.txt",
                $"synthetic merge-fanout branch {index:D2}\n");
            _ = await GitBenchmarkRepository.CommitAsync(
                path,
                $"fanout branch {index:D2}",
                cancellationToken).ConfigureAwait(false);
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
        GitBenchmarkRepository.WriteText(
            path,
            GitBenchmarkRepository.SourcePath(0, nested: true),
            GitBenchmarkRepository.SourceFile(0, options.LinesPerFile, finalValue));
        return await GitBenchmarkRepository.CommitAsync(
            path,
            message,
            cancellationToken,
            authorName,
            authorEmail,
            authorDate).ConfigureAwait(false);
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
        GitBenchmarkRepository.WriteText(path, "notes/unselected.md", "# Unselected head\n");
        return await GitBenchmarkRepository.CommitAsync(
            path,
            "unselected overlap head",
            cancellationToken).ConfigureAwait(false);
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
