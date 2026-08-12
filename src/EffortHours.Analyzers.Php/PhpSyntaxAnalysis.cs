namespace EffortHours.Analyzers.Php;

internal sealed record PhpSyntaxAnalysis(
    string Confidence,
    PhpImportContext Imports,
    PhpSourceMetrics Metrics,
    IReadOnlyList<PhpToken> Tokens);

internal sealed class PhpSourceMetrics
{
    public string Namespace { get; set; } = string.Empty;

    public int Imports { get; set; }

    public int InternalImports { get; set; }

    public int Functions { get; set; }

    public int Methods { get; set; }

    public int Types { get; set; }

    public int Classes { get; set; }

    public int Traits { get; set; }

    public int Interfaces { get; set; }

    public int Enums { get; set; }

    public int PublicSymbols { get; set; }

    public int Attributes { get; set; }

    public int AsyncUnits { get; set; }

    public int BranchPoints { get; set; }

    public int ExceptionPaths { get; set; }

    public int DocumentationComments { get; set; }

    public int DynamicIncludes { get; set; }

    public int MagicMethods { get; set; }

    public int ReflectionUsages { get; set; }

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

    public int FeatureTests { get; set; }

    public int EndToEndTests { get; set; }

    public HashSet<string> AttributesSeen { get; } = new(StringComparer.Ordinal);

    public HashSet<string> BaseTypesSeen { get; } = new(StringComparer.Ordinal);

    public HashSet<string> CallsSeen { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Technologies { get; } = new(StringComparer.Ordinal);
}

internal sealed record PhpTemplateMetrics(
    int Components,
    int Forms,
    int StructureUnits,
    int BindingUnits,
    int ControlFlowUnits)
{
    public bool Represented => Components + Forms + StructureUnits + BindingUnits + ControlFlowUnits > 0;
}
