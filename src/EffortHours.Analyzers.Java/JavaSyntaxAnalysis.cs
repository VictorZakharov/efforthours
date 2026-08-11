namespace EffortHours.Analyzers.Java;

internal sealed record JavaSyntaxAnalysis(
    string Confidence,
    JavaImportContext Imports,
    JavaSourceMetrics Metrics);

internal sealed class JavaSourceMetrics
{
    public string PackageName { get; set; } = string.Empty;

    public string ModuleName { get; set; } = string.Empty;

    public int Imports { get; set; }

    public int InternalImports { get; set; }

    public int Types { get; set; }

    public int Classes { get; set; }

    public int Records { get; set; }

    public int Interfaces { get; set; }

    public int Enums { get; set; }

    public int Methods { get; set; }

    public int Constructors { get; set; }

    public int PublicSymbols { get; set; }

    public int Annotations { get; set; }

    public int GenericDeclarations { get; set; }

    public int AsyncUnits { get; set; }

    public int BranchPoints { get; set; }

    public int ExceptionPaths { get; set; }

    public int ConcurrencyUsages { get; set; }

    public int ModuleRequires { get; set; }

    public int ModuleExports { get; set; }

    public int ModuleServices { get; set; }

    public int EntryPoints { get; set; }

    public int ApiEndpoints { get; set; }

    public int ApiTypes { get; set; }

    public int CliCommands { get; set; }

    public int DataModels { get; set; }

    public int DataCalls { get; set; }

    public int Migrations { get; set; }

    public int IntegrationCalls { get; set; }

    public int MessagingHandlers { get; set; }

    public int SecurityUsages { get; set; }

    public int BackgroundUsages { get; set; }

    public int ValidationRules { get; set; }

    public int TestCases { get; set; }

    public int ParameterizedCases { get; set; }

    public int Assertions { get; set; }

    public int MockUsages { get; set; }

    public int IntegrationTests { get; set; }

    public int ComponentTests { get; set; }

    public int EndToEndTests { get; set; }

    public HashSet<string> AnnotationsSeen { get; } = new(StringComparer.Ordinal);

    public HashSet<string> BaseTypesSeen { get; } = new(StringComparer.Ordinal);

    public HashSet<string> DeclaredPackages { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Technologies { get; } = new(StringComparer.Ordinal);
}
