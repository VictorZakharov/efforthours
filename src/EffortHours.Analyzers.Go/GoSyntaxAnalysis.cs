namespace EffortHours.Analyzers.Go;

internal sealed record GoSyntaxAnalysis(
    string Confidence,
    GoSourceMetrics Metrics);

internal sealed class GoSourceMetrics
{
    public string PackageName { get; set; } = string.Empty;

    public int Imports { get; set; }

    public int InternalImports { get; set; }

    public int Functions { get; set; }

    public int Methods { get; set; }

    public int Types { get; set; }

    public int Interfaces { get; set; }

    public int PublicSymbols { get; set; }

    public int GenericDeclarations { get; set; }

    public int AsyncUnits { get; set; }

    public int BranchPoints { get; set; }

    public int ErrorPaths { get; set; }

    public int Goroutines { get; set; }

    public int ChannelUsages { get; set; }

    public int SynchronizationUsages { get; set; }

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

    public int ValidationRules { get; set; }

    public int TestCases { get; set; }

    public int Benchmarks { get; set; }

    public int Examples { get; set; }

    public int FuzzTests { get; set; }

    public int TableDrivenCases { get; set; }

    public int Assertions { get; set; }

    public int MockUsages { get; set; }

    public int BuildConstraints { get; set; }

    public int PlatformFiles { get; set; }

    public int EmbedDirectives { get; set; }

    public int CodeGenerationDirectives { get; set; }

    public int CgoFiles { get; set; }

    public int BlankImports { get; set; }

    public HashSet<string> ImportsSeen { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Technologies { get; } = new(StringComparer.Ordinal);
}
