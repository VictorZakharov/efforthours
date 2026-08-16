using EffortHours.Analysis;

namespace EffortHours.Analyzers.JavaScript;

internal sealed class JavaScriptSourceMetrics
{
    public int Files { get; set; }

    public int ParserBackedFiles { get; set; }

    public int LexerBackedFiles { get; set; }

    public int Imports { get; set; }

    public int DynamicImports { get; set; }

    public int Exports { get; set; }

    public int Functions { get; set; }

    public int Methods { get; set; }

    public int AsyncFunctions { get; set; }

    public int Classes { get; set; }

    public int Interfaces { get; set; }

    public int TypeAliases { get; set; }

    public int Enums { get; set; }

    public int BranchPoints { get; set; }

    public int StructuralParserBackedFiles { get; set; }

    public int StructuralDetectedCallables { get; set; }

    public List<CallableStructuralMetric> CallableStructuralMetrics { get; } = [];

    public int Calls { get; set; }

    public int Decorators { get; set; }

    public int ApiEndpoints { get; set; }

    public int ApiControllers { get; set; }

    public int GraphQlOperations { get; set; }

    public int UiComponents { get; set; }

    public int UiPages { get; set; }

    public bool HasExplicitUiFile { get; set; }

    public int JsxElements { get; set; }

    public int StateUsages { get; set; }

    public int EffectUsages { get; set; }

    public int FormUsages { get; set; }

    public int DataCalls { get; set; }

    public int Migrations { get; set; }

    public int IntegrationCalls { get; set; }

    public int SecurityUsages { get; set; }

    public int ValidationUsages { get; set; }

    public int BackgroundUsages { get; set; }

    public int TestCases { get; set; }

    public int TestSuites { get; set; }

    public int Assertions { get; set; }

    public int MockUsages { get; set; }

    public int ComponentTestUsages { get; set; }

    public int IntegrationTestUsages { get; set; }

    public int EndToEndTestUsages { get; set; }

    public int AccessibilityTestUsages { get; set; }

    public int? EntryPointLine { get; set; }

    public int? ApiLine { get; set; }

    public int? UiLine { get; set; }

    public int? DataLine { get; set; }

    public int? IntegrationLine { get; set; }

    public int? SecurityLine { get; set; }

    public int? ValidationLine { get; set; }

    public int? BackgroundLine { get; set; }

    public int? TestLine { get; set; }

    public HashSet<string> Technologies { get; } = new(StringComparer.Ordinal);

    public HashSet<string> TechnologyFamilies { get; } = new(StringComparer.Ordinal);

    public HashSet<string> ObservedTechnologies { get; } = new(StringComparer.Ordinal);

    public HashSet<string> ObservedTechnologyFamilies { get; } = new(StringComparer.Ordinal);

    public HashSet<string> HttpMethods { get; } = new(StringComparer.Ordinal);

    public void Merge(JavaScriptSourceMetrics other)
    {
        ArgumentNullException.ThrowIfNull(other);
        Files += other.Files;
        ParserBackedFiles += other.ParserBackedFiles;
        LexerBackedFiles += other.LexerBackedFiles;
        Imports += other.Imports;
        DynamicImports += other.DynamicImports;
        Exports += other.Exports;
        Functions += other.Functions;
        Methods += other.Methods;
        AsyncFunctions += other.AsyncFunctions;
        Classes += other.Classes;
        Interfaces += other.Interfaces;
        TypeAliases += other.TypeAliases;
        Enums += other.Enums;
        BranchPoints += other.BranchPoints;
        StructuralParserBackedFiles += other.StructuralParserBackedFiles;
        StructuralDetectedCallables += other.StructuralDetectedCallables;
        CallableStructuralMetrics.AddRange(other.CallableStructuralMetrics);
        Calls += other.Calls;
        Decorators += other.Decorators;
        ApiEndpoints += other.ApiEndpoints;
        ApiControllers += other.ApiControllers;
        GraphQlOperations += other.GraphQlOperations;
        UiComponents += other.UiComponents;
        UiPages += other.UiPages;
        HasExplicitUiFile |= other.HasExplicitUiFile;
        JsxElements += other.JsxElements;
        StateUsages += other.StateUsages;
        EffectUsages += other.EffectUsages;
        FormUsages += other.FormUsages;
        DataCalls += other.DataCalls;
        Migrations += other.Migrations;
        IntegrationCalls += other.IntegrationCalls;
        SecurityUsages += other.SecurityUsages;
        ValidationUsages += other.ValidationUsages;
        BackgroundUsages += other.BackgroundUsages;
        TestCases += other.TestCases;
        TestSuites += other.TestSuites;
        Assertions += other.Assertions;
        MockUsages += other.MockUsages;
        ComponentTestUsages += other.ComponentTestUsages;
        IntegrationTestUsages += other.IntegrationTestUsages;
        EndToEndTestUsages += other.EndToEndTestUsages;
        AccessibilityTestUsages += other.AccessibilityTestUsages;
        Technologies.UnionWith(other.Technologies);
        TechnologyFamilies.UnionWith(other.TechnologyFamilies);
        ObservedTechnologies.UnionWith(other.ObservedTechnologies);
        ObservedTechnologyFamilies.UnionWith(other.ObservedTechnologyFamilies);
        HttpMethods.UnionWith(other.HttpMethods);
    }
}

internal sealed record JavaScriptFileAnalysis(
    JavaScriptSourceMetrics Metrics,
    IReadOnlyList<EffortHours.Contracts.V1.EvidenceFact> Facts,
    IReadOnlyList<EffortHours.Contracts.V1.Diagnostic> Diagnostics,
    IReadOnlyList<AngularComponentMetadata> AngularComponents);
