namespace EffortHours.ScannerBenchmarks;

internal sealed class BenchmarkTarget : IDisposable
{
    private BenchmarkTarget(
        string rootPath,
        string cachePath,
        bool ownsTarget,
        bool keepTarget,
        long requestedLines)
    {
        RootPath = rootPath;
        CachePath = cachePath;
        OwnsTarget = ownsTarget;
        KeepTarget = keepTarget;
        RequestedLines = requestedLines;
    }

    public string RootPath { get; }

    public string CachePath { get; }

    public bool OwnsTarget { get; }

    public bool KeepTarget { get; }

    public long RequestedLines { get; }

    public static BenchmarkTarget Create(BenchmarkOptions options)
    {
        if (options.Shape == BenchmarkShape.ExistingRepository)
        {
            string root = Path.GetFullPath(options.RepositoryPath!);
            if (!Directory.Exists(root))
            {
                throw new DirectoryNotFoundException($"Benchmark repository was not found: {root}");
            }

            return new BenchmarkTarget(
                root,
                Path.Combine(
                    Path.GetTempPath(),
                    $"efforthours-scanner-benchmark-{Guid.NewGuid():N}.cache.json"),
                ownsTarget: false,
                keepTarget: false,
                requestedLines: 0);
        }

        string rootPath = Path.Combine(
            Path.GetTempPath(),
            "efforthours-scanner-benchmarks",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);
        BenchmarkFixtureGenerator.Generate(rootPath, options);
        return new BenchmarkTarget(
            rootPath,
            rootPath + ".cache.json",
            ownsTarget: true,
            options.KeepRepository,
            (long)options.Files * options.LinesPerFile);
    }

    public void Dispose()
    {
        if (OwnsTarget && KeepTarget)
        {
            Console.WriteLine($"repository={RootPath}");
            if (File.Exists(CachePath))
            {
                Console.WriteLine($"cache={CachePath}");
            }

            return;
        }

        try
        {
            if (OwnsTarget && Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }

            File.Delete(CachePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Benchmark cleanup failed: {exception.Message}");
        }
    }
}
