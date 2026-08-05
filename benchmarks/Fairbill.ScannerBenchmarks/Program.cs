using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Fairbill.Analysis;
using Fairbill.Contracts;
using Fairbill.Contracts.V1;
using Fairbill.Core;

namespace Fairbill.ScannerBenchmarks;

public static class Program
{
    public static async Task<int> Main(string[] arguments)
    {
        BenchmarkOptions options;
        try
        {
            options = BenchmarkOptions.Parse(arguments);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine(
                "Usage: scanner-benchmark [--files <count>] [--lines-per-file <count>] [--dotnet|--javascript] [--warm-cache] [--keep]");
            return 2;
        }

        string rootPath = Path.Combine(
            Path.GetTempPath(),
            "fairbill-scanner-benchmarks",
            Guid.NewGuid().ToString("N"));
        string cachePath = rootPath + ".cache.json";
        Directory.CreateDirectory(rootPath);

        try
        {
            Stopwatch generationTimer = Stopwatch.StartNew();
            GenerateRepository(rootPath, options);
            generationTimer.Stop();

            long allocationsBefore = GC.GetTotalAllocatedBytes(precise: true);
            Stopwatch scanTimer = Stopwatch.StartNew();
            IRepositoryScanner scanner = options.AnalyzeDotNet || options.AnalyzeJavaScript
                ? new RepositoryAnalysisPipeline()
                : new RepositoryScanner();
            RepositoryEvidence evidence = await scanner.ScanAsync(rootPath);
            scanTimer.Stop();
            long scanAllocations = GC.GetTotalAllocatedBytes(precise: true) - allocationsBefore;

            Stopwatch serializationTimer = Stopwatch.StartNew();
            string json = ContractJson.Serialize(evidence);
            serializationTimer.Stop();

            decimal requestedLines = (decimal)options.Files * options.LinesPerFile;
            Console.WriteLine($"scanner={RepositoryScanner.AnalyzerVersion}");
            Console.WriteLine($"mode={GetMode(options)}");
            Console.WriteLine($"runtime={RuntimeInformation.FrameworkDescription}");
            Console.WriteLine($"os={RuntimeInformation.OSDescription}");
            Console.WriteLine($"architecture={RuntimeInformation.OSArchitecture}");
            Console.WriteLine($"logical-processors={Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)}");
            Console.WriteLine($"files={options.Files.ToString(CultureInfo.InvariantCulture)}");
            Console.WriteLine($"requested-lines={requestedLines.ToString(CultureInfo.InvariantCulture)}");
            Console.WriteLine($"generation-seconds={generationTimer.Elapsed.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}");
            Console.WriteLine($"scan-seconds={scanTimer.Elapsed.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}");
            Console.WriteLine($"serialization-seconds={serializationTimer.Elapsed.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}");
            Console.WriteLine($"scan-lines-per-second={(requestedLines / (decimal)scanTimer.Elapsed.TotalSeconds).ToString("F0", CultureInfo.InvariantCulture)}");
            Console.WriteLine($"scan-allocated-mib={(scanAllocations / 1024m / 1024m).ToString("F2", CultureInfo.InvariantCulture)}");
            Console.WriteLine($"json-mib={(Encoding.UTF8.GetByteCount(json) / 1024m / 1024m).ToString("F2", CultureInfo.InvariantCulture)}");
            Console.WriteLine($"facts={evidence.Facts.Count.ToString(CultureInfo.InvariantCulture)}");
            Console.WriteLine($"digest={evidence.Repository.SourceDigest}");

            if (options.MeasureWarmCache)
            {
                RepositoryScanOptions cacheOptions = new() { CachePath = cachePath };
                _ = await scanner.ScanAsync(rootPath, cacheOptions);
                long warmAllocationsBefore = GC.GetTotalAllocatedBytes(precise: true);
                Stopwatch warmTimer = Stopwatch.StartNew();
                RepositoryEvidence warmEvidence = await scanner.ScanAsync(rootPath, cacheOptions);
                warmTimer.Stop();
                long warmAllocations = GC.GetTotalAllocatedBytes(precise: true) - warmAllocationsBefore;
                if (warmEvidence.Repository.SourceDigest != evidence.Repository.SourceDigest)
                {
                    throw new InvalidOperationException("Warm-cache evidence digest differs from the full scan.");
                }

                Console.WriteLine($"warm-cache-seconds={warmTimer.Elapsed.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}");
                Console.WriteLine($"warm-cache-allocated-mib={(warmAllocations / 1024m / 1024m).ToString("F2", CultureInfo.InvariantCulture)}");
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Benchmark failed: {exception}");
            return 1;
        }
        finally
        {
            if (options.KeepRepository)
            {
                Console.WriteLine($"repository={rootPath}");
                if (File.Exists(cachePath))
                {
                    Console.WriteLine($"cache={cachePath}");
                }
            }
            else
            {
                try
                {
                    Directory.Delete(rootPath, recursive: true);
                    File.Delete(cachePath);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    Console.Error.WriteLine($"Benchmark cleanup failed: {exception.Message}");
                }
            }
        }
    }

    private static void GenerateRepository(string rootPath, BenchmarkOptions options)
    {
        if (options.AnalyzeJavaScript)
        {
            GenerateJavaScriptRepository(rootPath, options);
            return;
        }

        File.WriteAllText(
            Path.Combine(rootPath, "Benchmark.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        string sharedLines = string.Concat(
            Enumerable.Range(1, Math.Max(0, options.LinesPerFile - 1))
                .Select(line => $"// representative source line {line.ToString(CultureInfo.InvariantCulture)}\n"));

        Parallel.For(0, options.Files, index =>
        {
            string directory = Path.Combine(rootPath, "src", $"group-{index / 100:D5}");
            Directory.CreateDirectory(directory);
            string content = sharedLines +
                $"internal static class File{index:D7} {{ public const int Id = {index.ToString(CultureInfo.InvariantCulture)}; }}\n";
            File.WriteAllText(Path.Combine(directory, $"File{index:D7}.cs"), content);
        });
    }

    private static void GenerateJavaScriptRepository(string rootPath, BenchmarkOptions options)
    {
        File.WriteAllText(
            Path.Combine(rootPath, "package.json"),
            "{\"name\":\"fairbill-benchmark\",\"private\":true,\"devDependencies\":{\"typescript\":\"5.9.0\"}}\n");
        string sharedLines = string.Concat(
            Enumerable.Range(1, Math.Max(0, options.LinesPerFile - 1))
                .Select(line =>
                    $"export const value{line:D4} = (input) => input ?? {line.ToString(CultureInfo.InvariantCulture)};\n"));

        Parallel.For(0, options.Files, index =>
        {
            string directory = Path.Combine(rootPath, "src", $"group-{index / 100:D5}");
            Directory.CreateDirectory(directory);
            string content = sharedLines +
                $"export async function file{index:D7}(input) {{ return input ? {index.ToString(CultureInfo.InvariantCulture)} : 0; }}\n";
            string extension = index % 2 == 0 ? ".js" : ".ts";
            File.WriteAllText(Path.Combine(directory, $"file{index:D7}{extension}"), content);
        });
    }

    private static string GetMode(BenchmarkOptions options) => options switch
    {
        { AnalyzeDotNet: true } => "dotnet-static",
        { AnalyzeJavaScript: true } => "javascript-typescript-static",
        _ => "common",
    };

    private sealed record BenchmarkOptions(
        int Files,
        int LinesPerFile,
        bool KeepRepository,
        bool MeasureWarmCache,
        bool AnalyzeDotNet,
        bool AnalyzeJavaScript)
    {
        public static BenchmarkOptions Parse(string[] arguments)
        {
            int files = 1_000;
            int linesPerFile = 100;
            bool keep = false;
            bool measureWarmCache = false;
            bool analyzeDotNet = false;
            bool analyzeJavaScript = false;

            for (int index = 0; index < arguments.Length; index++)
            {
                switch (arguments[index])
                {
                    case "--files":
                        files = ReadPositiveInteger(arguments, ref index, "--files");
                        break;
                    case "--lines-per-file":
                        linesPerFile = ReadPositiveInteger(arguments, ref index, "--lines-per-file");
                        break;
                    case "--keep":
                        keep = true;
                        break;
                    case "--warm-cache":
                        measureWarmCache = true;
                        break;
                    case "--dotnet":
                        analyzeDotNet = true;
                        break;
                    case "--javascript":
                        analyzeJavaScript = true;
                        break;
                    default:
                        throw new ArgumentException($"Unknown option '{arguments[index]}'.");
                }
            }

            if (analyzeDotNet && analyzeJavaScript)
            {
                throw new ArgumentException("Options '--dotnet' and '--javascript' are mutually exclusive.");
            }

            return new BenchmarkOptions(
                files,
                linesPerFile,
                keep,
                measureWarmCache,
                analyzeDotNet,
                analyzeJavaScript);
        }

        private static int ReadPositiveInteger(
            string[] arguments,
            ref int index,
            string option)
        {
            if (index + 1 >= arguments.Length ||
                !int.TryParse(
                    arguments[++index],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int value) ||
                value <= 0)
            {
                throw new ArgumentException($"Option '{option}' requires a positive integer.");
            }

            return value;
        }
    }
}
