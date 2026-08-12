namespace EffortHours.Analyzers.Terraform;

internal sealed record TerraformArtifactAssessment(
    string Role,
    string Dialect,
    bool IsTest,
    bool IsVariableValues,
    bool IsCliConfiguration,
    bool SupportsTerraformSemantics);

internal static class TerraformArtifactClassifier
{
    public static TerraformArtifactAssessment Classify(string path)
    {
        string lowerName = Path.GetFileName(path).ToLowerInvariant();
        if (lowerName.EndsWith(".tftest.hcl", StringComparison.Ordinal) ||
            lowerName.EndsWith(".tfmock.hcl", StringComparison.Ordinal))
        {
            return new("test", "terraform-test", true, false, false, true);
        }

        if (lowerName.EndsWith(".tfvars", StringComparison.Ordinal) ||
            lowerName.EndsWith(".auto.tfvars", StringComparison.Ordinal) ||
            lowerName.EndsWith(".tfbackend", StringComparison.Ordinal))
        {
            return new("variable-values", "terraform-values", false, true, false, true);
        }

        if (lowerName is ".terraformrc" or "terraform.rc")
        {
            return new("cli-configuration", "terraform-cli", false, false, true, true);
        }

        if (lowerName == "terragrunt.hcl")
        {
            return new("terragrunt", "terragrunt-hcl", false, false, false, false);
        }

        if (lowerName.EndsWith(".pkr.hcl", StringComparison.Ordinal))
        {
            return new("packer", "packer-hcl", false, false, false, false);
        }

        if (lowerName.EndsWith(".nomad.hcl", StringComparison.Ordinal))
        {
            return new("nomad", "nomad-hcl", false, false, false, false);
        }

        if (lowerName.EndsWith(".tf", StringComparison.Ordinal))
        {
            return new("configuration", "terraform-hcl", false, false, false, true);
        }

        return new("generic-hcl", "generic-hcl", false, false, false, false);
    }
}
