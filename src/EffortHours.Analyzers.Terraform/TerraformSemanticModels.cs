namespace EffortHours.Analyzers.Terraform;

internal sealed record TerraformFileAnalysis
{
    public required HclDocumentAnalysis Document { get; init; }

    public required TerraformArtifactAssessment Artifact { get; init; }

    public required TerraformSemanticMetrics Metrics { get; init; }
}

internal sealed class TerraformSemanticMetrics
{
    public int Blocks { get; set; }

    public int Attributes { get; set; }

    public int Resources { get; set; }

    public int DataSources { get; set; }

    public int ModuleCalls { get; set; }

    public int Variables { get; set; }

    public int Outputs { get; set; }

    public int LocalValues { get; set; }

    public int Providers { get; set; }

    public int Backends { get; set; }

    public int TerraformBlocks { get; set; }

    public int LifecycleBlocks { get; set; }

    public int DynamicBlocks { get; set; }

    public int Provisioners { get; set; }

    public int DependencyExpressions { get; set; }

    public int Traversals { get; set; }

    public int FunctionCalls { get; set; }

    public int ConditionalExpressions { get; set; }

    public int ForExpressions { get; set; }

    public int TemplateExpressions { get; set; }

    public int InputAssignments { get; set; }

    public int DescriptionAttributes { get; set; }

    public int ValidationRules { get; set; }

    public int TestRuns { get; set; }

    public int TestAssertions { get; set; }

    public int SensitiveInterfaces { get; set; }

    public int CredentialLikeAttributes { get; set; }

    public int PolicyOrSecuritySurfaces { get; set; }

    public HashSet<string> ResourceTypes { get; } = new(StringComparer.Ordinal);

    public HashSet<string> DataSourceTypes { get; } = new(StringComparer.Ordinal);

    public HashSet<string> ProviderFamilies { get; } = new(StringComparer.Ordinal);

    public HashSet<string> BackendTypes { get; } = new(StringComparer.Ordinal);

    public List<TerraformModuleSource> ModuleSources { get; } = [];

    public int ExpressionComplexityUnits => Math.Min(
        10_000,
        Traversals + FunctionCalls + ConditionalExpressions * 2 +
        ForExpressions * 2 + TemplateExpressions);

    public int InfrastructureUnits(int canonicalFiles)
    {
        int interfaces = Variables + Outputs + InputAssignments;
        int structural = LifecycleBlocks + DynamicBlocks + Provisioners + DependencyExpressions;
        int units = (canonicalFiles > 0 ? 1 : 0) +
            ResourceTypes.Count + Ceiling(Math.Max(0, Resources - ResourceTypes.Count), 5) +
            DataSourceTypes.Count + Ceiling(Math.Max(0, DataSources - DataSourceTypes.Count), 5) +
            Ceiling(ModuleCalls, 3) + Ceiling(interfaces, 5) + Ceiling(LocalValues, 8) +
            ProviderFamilies.Count + BackendTypes.Count + Ceiling(structural, 3) +
            Ceiling(ExpressionComplexityUnits, 12);
        return Math.Clamp(units, canonicalFiles > 0 ? 1 : 0, 2_000);
    }

    public void Add(TerraformSemanticMetrics other)
    {
        Blocks += other.Blocks;
        Attributes += other.Attributes;
        Resources += other.Resources;
        DataSources += other.DataSources;
        ModuleCalls += other.ModuleCalls;
        Variables += other.Variables;
        Outputs += other.Outputs;
        LocalValues += other.LocalValues;
        Providers += other.Providers;
        Backends += other.Backends;
        TerraformBlocks += other.TerraformBlocks;
        LifecycleBlocks += other.LifecycleBlocks;
        DynamicBlocks += other.DynamicBlocks;
        Provisioners += other.Provisioners;
        DependencyExpressions += other.DependencyExpressions;
        Traversals += other.Traversals;
        FunctionCalls += other.FunctionCalls;
        ConditionalExpressions += other.ConditionalExpressions;
        ForExpressions += other.ForExpressions;
        TemplateExpressions += other.TemplateExpressions;
        InputAssignments += other.InputAssignments;
        DescriptionAttributes += other.DescriptionAttributes;
        ValidationRules += other.ValidationRules;
        TestRuns += other.TestRuns;
        TestAssertions += other.TestAssertions;
        SensitiveInterfaces += other.SensitiveInterfaces;
        CredentialLikeAttributes += other.CredentialLikeAttributes;
        PolicyOrSecuritySurfaces += other.PolicyOrSecuritySurfaces;
        ResourceTypes.UnionWith(other.ResourceTypes);
        DataSourceTypes.UnionWith(other.DataSourceTypes);
        ProviderFamilies.UnionWith(other.ProviderFamilies);
        BackendTypes.UnionWith(other.BackendTypes);
        ModuleSources.AddRange(other.ModuleSources);
    }

    private static int Ceiling(int value, int divisor) =>
        value <= 0 ? 0 : (value + divisor - 1) / divisor;
}

internal sealed record TerraformModuleSource(
    string? Literal,
    string Kind,
    int Line,
    bool Dynamic);
