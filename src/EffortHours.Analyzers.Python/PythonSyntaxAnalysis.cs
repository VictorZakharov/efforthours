namespace EffortHours.Analyzers.Python;

internal sealed record PythonSyntaxAnalysis(
    string Confidence,
    PythonSourceMetrics Metrics);

internal sealed class PythonSourceMetrics
{
    public int Imports { get; set; }

    public int InternalImports { get; set; }

    public int Functions { get; set; }

    public int Methods { get; set; }

    public int Classes { get; set; }

    public int PublicSymbols { get; set; }

    public int AsyncUnits { get; set; }

    public int BranchPoints { get; set; }

    public int Decorators { get; set; }

    public int TypeAnnotations { get; set; }

    public int Calls { get; set; }

    public int EntryPoints { get; set; }

    public int ApiEndpoints { get; set; }

    public int ApiTypes { get; set; }

    public int CliCommands { get; set; }

    public int DataModels { get; set; }

    public int DataCalls { get; set; }

    public int Migrations { get; set; }

    public int IntegrationCalls { get; set; }

    public int SecurityUsages { get; set; }

    public int BackgroundUsages { get; set; }

    public int ValidationTypes { get; set; }

    public int ValidationRules { get; set; }

    public int TestCases { get; set; }

    public int ParameterizedCases { get; set; }

    public int Assertions { get; set; }

    public int MockUsages { get; set; }

    public int DataAnalysisCalls { get; set; }

    public int VisualizationCalls { get; set; }

    public HashSet<string> ImportsSeen { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Technologies { get; } = new(StringComparer.Ordinal);
}
