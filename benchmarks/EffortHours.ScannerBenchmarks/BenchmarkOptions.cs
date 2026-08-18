using System.Globalization;

namespace EffortHours.ScannerBenchmarks;

internal enum BenchmarkShape
{
    Common,
    DotNet,
    JavaScript,
    Frontend,
    Sql,
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
    Docker,
    Mixed,
    ExistingRepository,
}

internal sealed record BenchmarkOptions(
    int Files,
    int LinesPerFile,
    int? FileAnalysisWorkers,
    bool KeepRepository,
    bool MeasureWarmCache,
    BenchmarkShape Shape,
    string? RepositoryPath)
{
    public const string Usage =
        "Usage: scanner-benchmark [--files <count>] [--lines-per-file <count>] " +
        "[--file-analysis-workers <count>] " +
        "[--dotnet|--javascript|--frontend|--sql|--python|--jupyter|--go|--java|--kotlin|--shell|--powershell|--php|--rust|--c|--cpp|--terraform|--docker|--mixed] [--warm-cache] [--keep] " +
        "or scanner-benchmark --repository <path> [--warm-cache]";

    public string Mode => Shape switch
    {
        BenchmarkShape.Common => "common",
        BenchmarkShape.DotNet => "dotnet-static",
        BenchmarkShape.JavaScript => "javascript-typescript-static",
        BenchmarkShape.Frontend => "frontend-assets-static",
        BenchmarkShape.Sql => "sql-static",
        BenchmarkShape.Python => "python-static",
        BenchmarkShape.Jupyter => "jupyter-notebook-static",
        BenchmarkShape.Go => "go-static",
        BenchmarkShape.Java => "java-static",
        BenchmarkShape.Kotlin => "kotlin-static",
        BenchmarkShape.Shell => "shell-static",
        BenchmarkShape.PowerShell => "powershell-static",
        BenchmarkShape.Php => "php-composer-static",
        BenchmarkShape.Rust => "rust-cargo-static",
        BenchmarkShape.C => "c-static",
        BenchmarkShape.Cpp => "cpp-static",
        BenchmarkShape.Terraform => "terraform-hcl-static",
        BenchmarkShape.Docker => "docker-compose-static",
        BenchmarkShape.Mixed => "mixed-static",
        BenchmarkShape.ExistingRepository => "repository-static",
        _ => throw new InvalidOperationException($"Unsupported benchmark shape '{Shape}'."),
    };

    public static BenchmarkOptions Parse(string[] arguments)
    {
        int files = 1_000;
        int linesPerFile = 100;
        bool filesProvided = false;
        bool linesProvided = false;
        int? fileAnalysisWorkers = null;
        bool keep = false;
        bool warmCache = false;
        BenchmarkShape? shape = null;
        string? repositoryPath = null;

        for (int index = 0; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case "--files":
                    files = ReadPositiveInteger(arguments, ref index, "--files");
                    filesProvided = true;
                    break;
                case "--lines-per-file":
                    linesPerFile = ReadPositiveInteger(arguments, ref index, "--lines-per-file");
                    linesProvided = true;
                    break;
                case "--file-analysis-workers":
                    fileAnalysisWorkers = ReadPositiveInteger(
                        arguments,
                        ref index,
                        "--file-analysis-workers");
                    break;
                case "--keep":
                    keep = true;
                    break;
                case "--warm-cache":
                    warmCache = true;
                    break;
                case "--dotnet":
                    shape = SelectShape(shape, BenchmarkShape.DotNet);
                    break;
                case "--javascript":
                    shape = SelectShape(shape, BenchmarkShape.JavaScript);
                    break;
                case "--frontend":
                    shape = SelectShape(shape, BenchmarkShape.Frontend);
                    break;
                case "--sql":
                    shape = SelectShape(shape, BenchmarkShape.Sql);
                    break;
                case "--python":
                    shape = SelectShape(shape, BenchmarkShape.Python);
                    break;
                case "--jupyter":
                    shape = SelectShape(shape, BenchmarkShape.Jupyter);
                    break;
                case "--go":
                    shape = SelectShape(shape, BenchmarkShape.Go);
                    break;
                case "--java":
                    shape = SelectShape(shape, BenchmarkShape.Java);
                    break;
                case "--kotlin":
                    shape = SelectShape(shape, BenchmarkShape.Kotlin);
                    break;
                case "--shell":
                    shape = SelectShape(shape, BenchmarkShape.Shell);
                    break;
                case "--powershell":
                    shape = SelectShape(shape, BenchmarkShape.PowerShell);
                    break;
                case "--php":
                    shape = SelectShape(shape, BenchmarkShape.Php);
                    break;
                case "--rust":
                    shape = SelectShape(shape, BenchmarkShape.Rust);
                    break;
                case "--c":
                    shape = SelectShape(shape, BenchmarkShape.C);
                    break;
                case "--cpp":
                    shape = SelectShape(shape, BenchmarkShape.Cpp);
                    break;
                case "--terraform":
                    shape = SelectShape(shape, BenchmarkShape.Terraform);
                    break;
                case "--docker":
                    shape = SelectShape(shape, BenchmarkShape.Docker);
                    break;
                case "--mixed":
                    shape = SelectShape(shape, BenchmarkShape.Mixed);
                    break;
                case "--repository":
                    repositoryPath = ReadValue(arguments, ref index, "--repository");
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{arguments[index]}'.");
            }
        }

        if (repositoryPath is not null &&
            (shape is not null || filesProvided || linesProvided || keep))
        {
            throw new ArgumentException(
                "Option '--repository' cannot be combined with generated-fixture shape, size, or keep options.");
        }

        if (fileAnalysisWorkers > Math.Max(1, Environment.ProcessorCount))
        {
            throw new ArgumentException(
                "Option '--file-analysis-workers' cannot exceed the available logical processors.");
        }

        return new BenchmarkOptions(
            files,
            linesPerFile,
            fileAnalysisWorkers,
            keep,
            warmCache,
            repositoryPath is null ? shape ?? BenchmarkShape.Common : BenchmarkShape.ExistingRepository,
            repositoryPath);
    }

    private static BenchmarkShape SelectShape(BenchmarkShape? current, BenchmarkShape selected)
    {
        if (current is not null)
        {
            throw new ArgumentException("Generated-fixture shape options are mutually exclusive.");
        }

        return selected;
    }

    private static string ReadValue(string[] arguments, ref int index, string option)
    {
        if (index + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[++index]))
        {
            throw new ArgumentException($"Option '{option}' requires a value.");
        }

        return arguments[index];
    }

    private static int ReadPositiveInteger(string[] arguments, ref int index, string option)
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
