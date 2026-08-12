using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Scripting;

internal static class ScriptRoleClassifier
{
    public static ScriptRole Classify(
        EvidenceFact file,
        ScriptLanguage language,
        IReadOnlyList<ScriptRole> invocationRoles,
        ScriptSyntaxAnalysis syntax)
    {
        string? commonRole = ScriptEvidence.TagValue(file.Tags, "role:");
        ScriptRole? pathRole = commonRole switch
        {
            "test" => ScriptRole.Test,
            "ci-configuration" => ScriptRole.Ci,
            "infrastructure" or "container-configuration" => ScriptRole.Infrastructure,
            "delivery" => ScriptRole.Delivery,
            "build-configuration" => ScriptRole.Build,
            _ => null,
        };
        if (pathRole is not null) return pathRole.Value;

        ScriptRole? invokedRole = invocationRoles
            .OrderBy(RolePriority)
            .Cast<ScriptRole?>()
            .FirstOrDefault();
        if (invokedRole is not null) return invokedRole.Value;

        string extension = Path.GetExtension(file.Scope).ToLowerInvariant();
        if (language == ScriptLanguage.PowerShell && extension == ".psm1")
            return ScriptRole.Module;
        if (syntax.Metrics.Functions + syntax.Metrics.Types > 0 &&
            syntax.Metrics.TopLevelCommands == 0)
            return ScriptRole.Module;
        return ScriptRole.Product;
    }

    public static bool HasConflictingAutomationRoles(IReadOnlyList<ScriptRole> roles) => roles
        .Where(role => role is not ScriptRole.Product and not ScriptRole.Module)
        .Distinct()
        .Skip(1)
        .Any();

    private static int RolePriority(ScriptRole role) => role switch
    {
        ScriptRole.Test => 0,
        ScriptRole.Ci => 1,
        ScriptRole.Infrastructure => 2,
        ScriptRole.Delivery => 3,
        ScriptRole.Build => 4,
        _ => 5,
    };
}
