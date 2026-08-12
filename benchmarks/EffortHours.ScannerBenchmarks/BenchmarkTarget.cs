using System.Globalization;

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

internal static class BenchmarkFixtureGenerator
{
    public static void Generate(string rootPath, BenchmarkOptions options)
    {
        if (options.Shape is BenchmarkShape.DotNet or BenchmarkShape.Common or BenchmarkShape.Mixed)
        {
            File.WriteAllText(
                Path.Combine(rootPath, "Benchmark.csproj"),
                "<Project Sdk=\"Microsoft.NET.Sdk\" />\n");
        }

        if (options.Shape is BenchmarkShape.JavaScript or BenchmarkShape.Mixed)
        {
            File.WriteAllText(
                Path.Combine(rootPath, "package.json"),
                "{\"name\":\"efforthours-benchmark\",\"private\":true," +
                "\"devDependencies\":{\"typescript\":\"5.9.0\"}}\n");
        }

        if (options.Shape == BenchmarkShape.Python)
        {
            File.WriteAllText(
                Path.Combine(rootPath, "pyproject.toml"),
                "[project]\nname = \"efforthours-benchmark\"\nversion = \"1.0.0\"\n");
        }

        if (options.Shape == BenchmarkShape.Go)
        {
            File.WriteAllText(
                Path.Combine(rootPath, "go.mod"),
                "module example.com/efforthours-benchmark\n\ngo 1.24\n");
        }

        if (options.Shape == BenchmarkShape.Java)
        {
            File.WriteAllText(
                Path.Combine(rootPath, "pom.xml"),
                "<project><modelVersion>4.0.0</modelVersion>" +
                "<groupId>example.com</groupId><artifactId>efforthours-benchmark</artifactId>" +
                "<version>1.0.0</version></project>\n");
        }

        if (options.Shape == BenchmarkShape.Kotlin)
        {
            File.WriteAllText(
                Path.Combine(rootPath, "build.gradle.kts"),
                "plugins { kotlin(\"jvm\") version \"2.2.0\" }\n");
        }

        string csharpLines = string.Concat(
            Enumerable.Range(1, Math.Max(0, options.LinesPerFile - 1))
                .Select(line => $"// representative source line {line.ToString(CultureInfo.InvariantCulture)}\n"));
        string scriptLines = string.Concat(
            Enumerable.Range(1, Math.Max(0, options.LinesPerFile - 1))
                .Select(line =>
                    $"export const value{line:D4} = (input) => input ?? " +
                    $"{line.ToString(CultureInfo.InvariantCulture)};\n"));
        string pythonLines = string.Concat(
            Enumerable.Range(1, Math.Max(0, options.LinesPerFile - 2))
                .Select(line =>
                    $"value_{line:D4} = {line.ToString(CultureInfo.InvariantCulture)}\n"));
        string goLines = string.Concat(
            Enumerable.Range(1, Math.Max(0, options.LinesPerFile - 2))
                .Select(line =>
                    $"var value{line:D4} = {line.ToString(CultureInfo.InvariantCulture)}\n"));
        string javaLines = string.Concat(
            Enumerable.Range(1, Math.Max(0, options.LinesPerFile - 3))
                .Select(line =>
                    $"    private final int value{line:D4} = {line.ToString(CultureInfo.InvariantCulture)};\n"));
        string kotlinLines = string.Concat(
            Enumerable.Range(1, Math.Max(0, options.LinesPerFile - 4))
                .Select(line =>
                    $"    private val value{line:D4}: Int = {line.ToString(CultureInfo.InvariantCulture)}\n"));

        Parallel.For(0, options.Files, index =>
        {
            BenchmarkSourceKind kind = SourceKind(options.Shape, index);
            string directory = Path.Combine(
                rootPath,
                kind switch
                {
                    BenchmarkSourceKind.CSharp => "src",
                    BenchmarkSourceKind.Python => "python",
                    BenchmarkSourceKind.Go => "go",
                    BenchmarkSourceKind.Java => Path.Combine("src", "main", "java", "benchmark"),
                    BenchmarkSourceKind.Kotlin => Path.Combine("src", "main", "kotlin", "benchmark"),
                    _ => "web",
                },
                $"group-{index / 100:D5}");
            Directory.CreateDirectory(directory);
            (string fileName, string content) = kind switch
            {
                BenchmarkSourceKind.CSharp => (
                    $"File{index:D7}.cs",
                    csharpLines +
                    $"internal static class File{index:D7} {{ public const int Id = " +
                    $"{index.ToString(CultureInfo.InvariantCulture)}; }}\n"),
                BenchmarkSourceKind.JavaScript => (
                    $"file{index:D7}.js",
                    scriptLines +
                    $"export async function file{index:D7}(input) {{ return input ? " +
                    $"{index.ToString(CultureInfo.InvariantCulture)} : 0; }}\n"),
                BenchmarkSourceKind.TypeScript => (
                    $"file{index:D7}.ts",
                    scriptLines +
                    $"export async function file{index:D7}(input: number): Promise<number> {{ return input ? " +
                    $"{index.ToString(CultureInfo.InvariantCulture)} : 0; }}\n"),
                BenchmarkSourceKind.Python => (
                    $"file{index:D7}.py",
                    pythonLines +
                    $"async def file_{index:D7}(value):\n    return value if value else " +
                    $"{index.ToString(CultureInfo.InvariantCulture)}\n"),
                BenchmarkSourceKind.Go => (
                    $"file{index:D7}.go",
                    "package benchmark\n" + goLines +
                    $"func File{index:D7}[T any](value T) T {{ return value }}\n"),
                BenchmarkSourceKind.Java => (
                    $"File{index:D7}.java",
                    "package benchmark;\n" +
                    $"public final class File{index:D7}<T> {{\n" + javaLines +
                    "    public T value(T input) { return input; }\n}\n"),
                BenchmarkSourceKind.Kotlin => (
                    $"File{index:D7}.kt",
                    "package benchmark\n" +
                    $"data class File{index:D7}<T>(val input: T) {{\n" + kotlinLines +
                    "    suspend fun value(): T = input\n}\n"),
                _ => throw new InvalidOperationException($"Unsupported benchmark source kind '{kind}'."),
            };
            File.WriteAllText(Path.Combine(directory, fileName), content);
        });
    }

    private static BenchmarkSourceKind SourceKind(BenchmarkShape shape, int index) => shape switch
    {
        BenchmarkShape.Common or BenchmarkShape.DotNet => BenchmarkSourceKind.CSharp,
        BenchmarkShape.JavaScript => index % 2 == 0
            ? BenchmarkSourceKind.JavaScript
            : BenchmarkSourceKind.TypeScript,
        BenchmarkShape.Python => BenchmarkSourceKind.Python,
        BenchmarkShape.Go => BenchmarkSourceKind.Go,
        BenchmarkShape.Java => BenchmarkSourceKind.Java,
        BenchmarkShape.Kotlin => BenchmarkSourceKind.Kotlin,
        BenchmarkShape.Mixed => (index % 3) switch
        {
            0 => BenchmarkSourceKind.CSharp,
            1 => BenchmarkSourceKind.JavaScript,
            _ => BenchmarkSourceKind.TypeScript,
        },
        _ => throw new InvalidOperationException($"Unsupported generated benchmark shape '{shape}'."),
    };

    private enum BenchmarkSourceKind
    {
        CSharp,
        JavaScript,
        TypeScript,
        Python,
        Go,
        Java,
        Kotlin,
    }
}
