namespace EffortHours.Analysis;

internal static class TerraformFileClassification
{
    public static string? DetectLanguage(string lowerName, string extension)
    {
        if (IsDependencyLock(lowerName) || IsStateOrPlan(lowerName, extension))
        {
            return null;
        }

        if (lowerName.EndsWith(".tf.json", StringComparison.Ordinal) ||
            lowerName.EndsWith(".tfvars.json", StringComparison.Ordinal))
        {
            return "terraform-json";
        }

        if (extension is ".tf" or ".tfvars" or ".tfbackend")
        {
            return "terraform";
        }

        if (extension == ".hcl" || lowerName is ".terraformrc" or "terraform.rc")
        {
            return "hcl";
        }

        return null;
    }

    public static bool IsProjectArtifact(string lowerName, string extension) =>
        DetectLanguage(lowerName, extension) is not null ||
        IsDependencyLock(lowerName) ||
        IsStateOrPlan(lowerName, extension);

    public static bool IsTest(string lowerName, string? language) =>
        language is "terraform" or "hcl" &&
        (lowerName.EndsWith(".tftest.hcl", StringComparison.Ordinal) ||
         lowerName.EndsWith(".tfmock.hcl", StringComparison.Ordinal));

    public static bool IsDependencyLock(string lowerName) =>
        lowerName == ".terraform.lock.hcl";

    public static bool IsStateOrPlan(string lowerName, string extension) =>
        lowerName.EndsWith(".tfstate", StringComparison.Ordinal) ||
        lowerName.EndsWith(".tfstate.backup", StringComparison.Ordinal) ||
        extension == ".tfplan" ||
        lowerName is "terraform.tfplan" or "tfplan";

    public static bool IsCliConfiguration(string lowerName) =>
        lowerName is ".terraformrc" or "terraform.rc";
}
