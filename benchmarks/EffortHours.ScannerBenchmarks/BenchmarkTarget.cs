using System.Globalization;
using System.Text.Json;

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
    private static readonly string[] BenchmarkMarkdownSource = ["# Benchmark notebook\n"];
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

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

        if (options.Shape is BenchmarkShape.Python or BenchmarkShape.Jupyter)
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

        if (options.Shape == BenchmarkShape.Php)
        {
            File.WriteAllText(
                Path.Combine(rootPath, "composer.json"),
                "{\"name\":\"efforthours/benchmark\",\"autoload\":{\"psr-4\":{\"Benchmark\\\\\":\"src/\"}}}\n");
        }

        if (options.Shape == BenchmarkShape.Rust)
        {
            File.WriteAllText(
                Path.Combine(rootPath, "Cargo.toml"),
                "[package]\nname = \"efforthours-benchmark\"\nversion = \"1.0.0\"\nedition = \"2024\"\n");
        }

        if (options.Shape is BenchmarkShape.C or BenchmarkShape.Cpp or BenchmarkShape.Mixed)
        {
            File.WriteAllText(
                Path.Combine(rootPath, "CMakeLists.txt"),
                "cmake_minimum_required(VERSION 3.25)\n" +
                "project(efforthours_benchmark LANGUAGES C CXX)\n");
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
        string shellLines = string.Concat(
            Enumerable.Range(1, Math.Max(0, options.LinesPerFile - 5))
                .Select(line =>
                    $"  value_{line:D4}={line.ToString(CultureInfo.InvariantCulture)}\n"));
        string powerShellLines = string.Concat(
            Enumerable.Range(1, Math.Max(0, options.LinesPerFile - 5))
                .Select(line =>
                    $"  $value{line:D4} = {line.ToString(CultureInfo.InvariantCulture)}\n"));
        string phpLines = string.Concat(
            Enumerable.Range(1, Math.Max(0, options.LinesPerFile - 5))
                .Select(line =>
                    $"    private int $value{line:D4} = {line.ToString(CultureInfo.InvariantCulture)};\n"));
        string rustLines = string.Concat(
            Enumerable.Range(1, Math.Max(0, options.LinesPerFile - 4))
                .Select(line =>
                    $"    pub value_{line:D4}: usize,\n"));
        string cLines = string.Concat(
            Enumerable.Range(1, Math.Max(0, options.LinesPerFile - 4))
                .Select(line => $"    int value_{line:D4};\n"));
        string cppLines = string.Concat(
            Enumerable.Range(1, Math.Max(0, options.LinesPerFile - 4))
                .Select(line => $"    int value_{line:D4};\n"));
        string terraformLines = string.Concat(
            Enumerable.Range(1, Math.Max(0, options.LinesPerFile - 2))
                .Select(line =>
                    $"  value_{line:D4} = {line.ToString(CultureInfo.InvariantCulture)}\n"));

        Parallel.For(0, options.Files, index =>
        {
            BenchmarkSourceKind kind = SourceKind(options.Shape, index);
            string directory = Path.Combine(
                rootPath,
                kind switch
                {
                    BenchmarkSourceKind.CSharp => "src",
                    BenchmarkSourceKind.Python => "python",
                    BenchmarkSourceKind.Jupyter => "notebooks",
                    BenchmarkSourceKind.Go => "go",
                    BenchmarkSourceKind.Java => Path.Combine("src", "main", "java", "benchmark"),
                    BenchmarkSourceKind.Kotlin => Path.Combine("src", "main", "kotlin", "benchmark"),
                    BenchmarkSourceKind.Shell => "scripts",
                    BenchmarkSourceKind.PowerShell => "powershell",
                    BenchmarkSourceKind.Php => "src",
                    BenchmarkSourceKind.Rust => "src",
                    BenchmarkSourceKind.C => "c",
                    BenchmarkSourceKind.Cpp => "cpp",
                    BenchmarkSourceKind.Terraform => "infrastructure",
                    BenchmarkSourceKind.Dockerfile or BenchmarkSourceKind.Compose => "containers",
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
                BenchmarkSourceKind.Jupyter => (
                    $"analysis{index:D7}.ipynb",
                    JupyterContent(index, options.LinesPerFile)),
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
                BenchmarkSourceKind.Shell => (
                    $"file{index:D7}.sh",
                    "#!/bin/sh\n" + $"file_{index:D7}() {{\n" + shellLines +
                    "  if test \"$1\"; then printf '%s\\n' \"$1\"; fi\n}\n"),
                BenchmarkSourceKind.PowerShell => (
                    $"File{index:D7}.ps1",
                    $"function Invoke-File{index:D7} {{\n  param([string] $Value)\n" +
                    powerShellLines +
                    "  if ($Value) { Write-Output $Value }\n}\n"),
                BenchmarkSourceKind.Php => (
                    $"File{index:D7}.php",
                    "<?php\nnamespace Benchmark;\n" +
                    $"final class File{index:D7} {{\n" + phpLines +
                    "    public function value(int $input): int { return $input; }\n}\n"),
                BenchmarkSourceKind.Rust => (
                    $"file_{index:D7}.rs",
                    $"pub struct File{index:D7}<'a, T> {{\n" + rustLines +
                    "    pub input: &'a T,\n}\n" +
                    $"impl<'a, T> File{index:D7}<'a, T> {{ pub async fn value(&self) -> &T {{ self.input }} }}\n"),
                BenchmarkSourceKind.C => (
                    $"file_{index:D7}.c",
                    $"struct file_{index:D7} {{\n" + cLines + "};\n" +
                    $"int file_{index:D7}_value(int input) {{ return input ? input : {index.ToString(CultureInfo.InvariantCulture)}; }}\n"),
                BenchmarkSourceKind.Cpp => (
                    $"file_{index:D7}.cpp",
                    $"template<class T> struct File{index:D7} {{\n" + cppLines +
                    "    T value(T input) const { return input; }\n};\n"),
                BenchmarkSourceKind.Terraform => (
                    $"file{index:D7}.tf",
                    $"resource \"benchmark_item\" \"item_{index:D7}\" {{\n" +
                    terraformLines + "}\n"),
                BenchmarkSourceKind.Dockerfile => (
                    $"Dockerfile.{index:D7}",
                    DockerfileContent(index, options.LinesPerFile)),
                BenchmarkSourceKind.Compose => (
                    $"compose.{index:D7}.yml",
                    ComposeContent(index, options.LinesPerFile)),
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
        BenchmarkShape.Jupyter => BenchmarkSourceKind.Jupyter,
        BenchmarkShape.Go => BenchmarkSourceKind.Go,
        BenchmarkShape.Java => BenchmarkSourceKind.Java,
        BenchmarkShape.Kotlin => BenchmarkSourceKind.Kotlin,
        BenchmarkShape.Shell => BenchmarkSourceKind.Shell,
        BenchmarkShape.PowerShell => BenchmarkSourceKind.PowerShell,
        BenchmarkShape.Php => BenchmarkSourceKind.Php,
        BenchmarkShape.Rust => BenchmarkSourceKind.Rust,
        BenchmarkShape.C => BenchmarkSourceKind.C,
        BenchmarkShape.Cpp => BenchmarkSourceKind.Cpp,
        BenchmarkShape.Terraform => BenchmarkSourceKind.Terraform,
        BenchmarkShape.Docker => index % 2 == 0
            ? BenchmarkSourceKind.Dockerfile
            : BenchmarkSourceKind.Compose,
        BenchmarkShape.Mixed => (index % 5) switch
        {
            0 => BenchmarkSourceKind.CSharp,
            1 => BenchmarkSourceKind.JavaScript,
            2 => BenchmarkSourceKind.TypeScript,
            3 => BenchmarkSourceKind.C,
            _ => BenchmarkSourceKind.Cpp,
        },
        _ => throw new InvalidOperationException($"Unsupported generated benchmark shape '{shape}'."),
    };

    private static string DockerfileContent(int index, int lines)
    {
        string id = index.ToString(CultureInfo.InvariantCulture);
        if (lines == 1) return $"FROM benchmark/image:{id}\n";
        return $"FROM scratch AS build\nARG FILE_ID={id}\n" + string.Concat(
            Enumerable.Range(1, Math.Max(0, lines - 2))
                .Select(line => $"RUN printf '%s' {line.ToString(CultureInfo.InvariantCulture)} > /tmp/value-{line:D4}\n"));
    }

    private static string ComposeContent(int index, int lines)
    {
        string id = index.ToString(CultureInfo.InvariantCulture);
        if (lines == 1) return $"name: benchmark-{id}\n";
        return $"name: benchmark-{id}\nservices:\n" + string.Concat(
            Enumerable.Range(1, Math.Max(0, lines - 2))
                .Select(line => $"  service-{line:D4}: {{ image: benchmark/image:{id}-{line:D4} }}\n"));
    }

    private static string JupyterContent(int index, int lines)
    {
        int sourceLines = Math.Max(1, lines - 18);
        string[] source = [.. Enumerable.Range(1, sourceLines).Select(line =>
            line == 1
                ? "import pandas as pd\n"
                : line == sourceLines
                    ? $"frame_{index:D7} = pd.DataFrame({{\"value\": [{index.ToString(CultureInfo.InvariantCulture)}]}})\n"
                    : $"value_{line:D4} = {line.ToString(CultureInfo.InvariantCulture)}\n")];
        return JsonSerializer.Serialize(
            new
            {
                cells = new object[]
                {
                    new
                    {
                        cell_type = "markdown",
                        metadata = new { },
                        source = BenchmarkMarkdownSource,
                    },
                    new
                    {
                        cell_type = "code",
                        metadata = new { },
                        source,
                        execution_count = (int?)null,
                        outputs = Array.Empty<object>(),
                    },
                },
                metadata = new
                {
                    kernelspec = new { name = "python3", language = "python" },
                    language_info = new { name = "python" },
                },
                nbformat = 4,
                nbformat_minor = 5,
            },
            IndentedJson);
    }

    private enum BenchmarkSourceKind
    {
        CSharp,
        JavaScript,
        TypeScript,
        Python,
        Jupyter,
        Go,
        Java,
        Kotlin,
        Shell,
        PowerShell,
        Php,
        Rust,
        C,
        Cpp,
        Terraform,
        Dockerfile,
        Compose,
    }
}
