namespace EffortHours.Analyzers.Terraform;

internal static class TerraformSemanticAnalyzer
{
    private static readonly string[] CredentialNames =
    [
        "access_key", "api_key", "client_secret", "password", "private_key",
        "secret", "secret_key", "token",
    ];

    private static readonly string[] SecurityTerms =
    [
        "acl", "certificate", "encryption", "firewall", "iam", "ingress", "egress",
        "kms", "policy", "public_access", "role", "security_group", "secret", "vault",
    ];

    public static TerraformFileAnalysis Analyze(
        string text,
        TerraformArtifactAssessment artifact)
    {
        HclDocumentAnalysis document = HclSyntaxAnalyzer.Analyze(text);
        TerraformSemanticMetrics metrics = new();
        foreach (HclAttributeAnalysis attribute in document.Attributes)
        {
            CountAttribute(attribute, metrics, artifact, parentType: null);
        }

        foreach (HclBlockAnalysis block in document.Blocks)
        {
            CountBlock(block, metrics, artifact, parentType: null);
        }

        if (artifact.IsVariableValues)
        {
            metrics.InputAssignments = document.Attributes.Count;
        }

        return new TerraformFileAnalysis
        {
            Document = document,
            Artifact = artifact,
            Metrics = metrics,
        };
    }

    private static void CountBlock(
        HclBlockAnalysis block,
        TerraformSemanticMetrics metrics,
        TerraformArtifactAssessment artifact,
        string? parentType)
    {
        metrics.Blocks++;
        string type = block.Type.ToLowerInvariant();
        if (artifact.SupportsTerraformSemantics)
        {
            CountTerraformBlock(block, type, parentType, metrics);
        }

        if (type is "validation" or "precondition" or "postcondition" or "check")
            metrics.ValidationRules++;
        if (type == "run") metrics.TestRuns++;
        if (type == "assert") metrics.TestAssertions++;
        if (type == "dynamic") metrics.DynamicBlocks++;
        if (type == "lifecycle") metrics.LifecycleBlocks++;
        if (type is "provisioner" or "connection") metrics.Provisioners++;

        foreach (HclAttributeAnalysis attribute in block.Attributes)
        {
            CountAttribute(attribute, metrics, artifact, type);
        }

        foreach (HclBlockAnalysis child in block.Blocks)
        {
            CountBlock(child, metrics, artifact, type);
        }
    }

    private static void CountTerraformBlock(
        HclBlockAnalysis block,
        string type,
        string? parentType,
        TerraformSemanticMetrics metrics)
    {
        if (parentType is not null && !(type == "backend" && parentType == "terraform"))
            return;

        switch (type)
        {
            case "resource":
                metrics.Resources++;
                AddTypedFamily(block, metrics.ResourceTypes, metrics.ProviderFamilies, security: metrics);
                break;
            case "data":
                metrics.DataSources++;
                AddTypedFamily(block, metrics.DataSourceTypes, metrics.ProviderFamilies, security: metrics);
                break;
            case "module":
                metrics.ModuleCalls++;
                HclAttributeAnalysis? source = block.Attributes.FirstOrDefault(attribute =>
                    attribute.Name.Equals("source", StringComparison.OrdinalIgnoreCase));
                metrics.ModuleSources.Add(AssessModuleSource(source, block.Line));
                break;
            case "variable": metrics.Variables++; break;
            case "output": metrics.Outputs++; break;
            case "locals": metrics.LocalValues += block.Attributes.Count; break;
            case "provider":
                metrics.Providers++;
                AddLabelFamily(block, metrics.ProviderFamilies);
                break;
            case "terraform": metrics.TerraformBlocks++; break;
            case "backend" when parentType == "terraform":
                metrics.Backends++;
                if (block.Labels.Count > 0)
                    metrics.BackendTypes.Add(SafeFamily(block.Labels[0]));
                break;
        }
    }

    private static void CountAttribute(
        HclAttributeAnalysis attribute,
        TerraformSemanticMetrics metrics,
        TerraformArtifactAssessment artifact,
        string? parentType)
    {
        metrics.Attributes++;
        metrics.Traversals += attribute.Traversals;
        metrics.FunctionCalls += attribute.FunctionCalls;
        metrics.ConditionalExpressions += attribute.Conditionals;
        metrics.ForExpressions += attribute.ForExpressions;
        metrics.TemplateExpressions += attribute.TemplateExpressions;
        string name = attribute.Name.ToLowerInvariant();
        if (name == "description") metrics.DescriptionAttributes++;
        if (name == "depends_on") metrics.DependencyExpressions++;
        if (name == "condition" &&
            parentType is "validation" or "precondition" or "postcondition" or "check")
        {
            metrics.ValidationRules++;
        }

        if (name == "sensitive" && attribute.LiteralBoolean == true)
            metrics.SensitiveInterfaces++;
        if (CredentialNames.Any(term => ContainsTerm(name, term)))
            metrics.CredentialLikeAttributes++;
        if (SecurityTerms.Any(term => ContainsTerm(name, term)))
            metrics.PolicyOrSecuritySurfaces++;

        if (artifact.SupportsTerraformSemantics && parentType == "required_providers")
        {
            foreach (string literal in attribute.StringLiterals.Where(value => value.Contains('/')))
            {
                metrics.ProviderFamilies.Add(SafeFamily(literal.Split('/')[^1]));
            }
        }
    }

    private static TerraformModuleSource AssessModuleSource(
        HclAttributeAnalysis? attribute,
        int line)
    {
        if (attribute is null)
            return new(null, "missing", line, true);
        if (attribute.LiteralString is null)
            return new(null, "dynamic", attribute.Line, true);

        string source = attribute.LiteralString.Trim();
        string kind = source.StartsWith("./", StringComparison.Ordinal) ||
            source.StartsWith("../", StringComparison.Ordinal)
                ? "local"
                : source.StartsWith("git::", StringComparison.OrdinalIgnoreCase) ||
                    source.StartsWith("git@", StringComparison.OrdinalIgnoreCase) ||
                    source.Contains(".git", StringComparison.OrdinalIgnoreCase)
                    ? "git"
                    : source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        source.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                        ? "http"
                        : source.StartsWith("s3::", StringComparison.OrdinalIgnoreCase) ||
                            source.StartsWith("gcs::", StringComparison.OrdinalIgnoreCase)
                            ? "object-storage"
                            : source.Split('/', StringSplitOptions.RemoveEmptyEntries).Length >= 3
                                ? "registry"
                                : "other-external";
        return new(source, kind, attribute.Line, false);
    }

    private static void AddTypedFamily(
        HclBlockAnalysis block,
        HashSet<string> types,
        HashSet<string> providers,
        TerraformSemanticMetrics security)
    {
        if (block.Labels.Count == 0) return;
        string type = SafeFamily(block.Labels[0]);
        types.Add(type);
        int separator = type.IndexOf('_');
        if (separator > 0) providers.Add(type[..separator]);
        if (SecurityTerms.Any(term => ContainsTerm(type, term)))
            security.PolicyOrSecuritySurfaces++;
    }

    private static void AddLabelFamily(HclBlockAnalysis block, HashSet<string> families)
    {
        if (block.Labels.Count > 0) families.Add(SafeFamily(block.Labels[0]));
    }

    private static string SafeFamily(string value)
    {
        string normalized = new([.. value.ToLowerInvariant()
            .Take(64)
            .Select(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'
                ? character
                : '-')]);
        normalized = normalized.Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
    }

    private static bool ContainsTerm(string value, string term) =>
        value.Equals(term, StringComparison.Ordinal) ||
        value.StartsWith(term + "_", StringComparison.Ordinal) ||
        value.EndsWith("_" + term, StringComparison.Ordinal) ||
        value.Contains("_" + term + "_", StringComparison.Ordinal);
}
