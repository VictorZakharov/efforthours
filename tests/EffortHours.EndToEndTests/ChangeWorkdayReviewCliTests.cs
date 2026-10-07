using System.Globalization;
using System.Text;
using System.Text.Json;
using EffortHours.Cli;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangePortfolioCliTests
{
    private static async Task AssertWorkdayReviewAsync(string workspace, ChangePortfolioComparisonReport source)
    {
        string input = Path.Combine(workspace, "review-source.json"), records = Path.Combine(workspace, "records.json"), output = Path.Combine(workspace, "review.json");
        string original = ContractJson.Serialize(source);
        await File.WriteAllTextAsync(input, original, new UTF8Encoding(false));
        ChangeWorkRecordManifest manifest = new()
        {
            SourceSemanticDigest = source.Verification.SemanticDigest,
            Records = [new() { RecordId = "implementation", Date = "2026-01-19", Kind = "implementation", LoggedHours = 4m,
                RepositoryIds = [.. source.Selection.AuthorPeriodManifest!.Repositories.Select(repository => repository.Id)] },
                new() { RecordId = "missing-history", Date = "2026-01-23", Kind = "implementation", LoggedHours = 12m,
                    RepositoryIds = [.. source.Selection.AuthorPeriodManifest.Repositories.Select(repository => repository.Id)] },
                new() { RecordId = "meeting", Date = "2026-01-20", Kind = "meeting", LoggedHours = 8m }],
        };
        await File.WriteAllTextAsync(records, ContractJson.Serialize(manifest), new UTF8Encoding(false));
        string recordText = await File.ReadAllTextAsync(records);
        using StringWriter stdout = new(CultureInfo.InvariantCulture), stderr = new(CultureInfo.InvariantCulture);
        string[] args = ["change", "review-days", input, "--work-records", records, "--entry-policy", ChangeWorkdayReviewPolicies.EqualEntries, "--compact", "--output", output];
        Assert.True(await new EffortHoursApplication().RunAsync(args, stdout, stderr) == 0, stderr.ToString());
        string json = await File.ReadAllTextAsync(output);
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeWorkdayReviewReport, json).IsValid);
        ChangeWorkdayReviewReport review = ContractJson.Deserialize<ChangeWorkdayReviewReport>(json);
        Assert.Empty(ContractValidation.Validate(review));
        Assert.Equal("unresolved", review.Status);
        Assert.Equal("unresolved-workday", review.Days[4].Status);
        Assert.Equal("no-retained-change", review.Days[4].RetainedEvidenceStatus);
        Assert.Null(Assert.Single(review.Days[4].Records).AllocatedMultiplierContribution);
        Assert.Equal(original, await File.ReadAllTextAsync(input));
        Assert.Equal(recordText, await File.ReadAllTextAsync(records));
        Assert.DoesNotContain(workspace, json, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(0, await new EffortHoursApplication().RunAsync(args, stdout, stderr));
        Assert.Equal(json, await File.ReadAllTextAsync(output));
        Assert.NotEqual(0, await new EffortHoursApplication().RunAsync([.. args[..^1], input], stdout, stderr));
        Assert.Equal(original, await File.ReadAllTextAsync(input));
        stdout.GetStringBuilder().Clear();
        Assert.Equal(0, await new EffortHoursApplication().RunAsync([.. args[..^3], "--format", "markdown"], stdout, stderr));
        Assert.Contains("unresolved-workday", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("unavailable", stdout.ToString(), StringComparison.Ordinal);
        ChangeHistoricalRefreshManifest refresh = new()
        {
            SourceSemanticDigest = review.SourceSemanticDigest,
            WorkRecordInputDigest = review.WorkRecordInputDigest,
            SinceInclusiveDate = "2026-01-19",
            UntilExclusiveDate = "2026-01-24",
            Entries = [new() { RecordId = "missing-history", NotePermission = "allowed", EhePermission = "denied", Restriction = "none",
                Original = JsonSerializer.SerializeToElement(new { description = "Original implementation task", hours = 12, task = "original-task", project = "original-project", ticket = "T-1", billing = "preserved" }) }],
        };
        string entries = Path.Combine(workspace, "refresh-input.json");
        await File.WriteAllTextAsync(entries, ContractJson.Serialize(refresh), new UTF8Encoding(false));
        string entriesText = await File.ReadAllTextAsync(entries, Encoding.UTF8);
        stdout.GetStringBuilder().Clear();
        string[] refreshArgs = ["change", "plan-refresh", input, "--work-records", records, "--entries", entries, "--compact"];
        Assert.Equal(0, await new EffortHoursApplication().RunAsync(refreshArgs, stdout, stderr));
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeHistoricalRefreshPlan, stdout.ToString()).IsValid);
        ChangeHistoricalRefreshPlan plan = ContractJson.Deserialize<ChangeHistoricalRefreshPlan>(stdout.ToString());
        Assert.Empty(ContractValidation.Validate(plan));
        await AssertRefreshPreflightAsync(workspace, plan, refresh);
        Assert.StartsWith("Original implementation task", plan.Proposals[0].ProposedDescription, StringComparison.Ordinal);
        Assert.Equal("not-requested", plan.Proposals[0].EheStatus);
        Assert.Equal(entriesText, await File.ReadAllTextAsync(entries, Encoding.UTF8));
        Assert.Equal(original, await File.ReadAllTextAsync(input, Encoding.UTF8));
        Assert.Equal(recordText, await File.ReadAllTextAsync(records, Encoding.UTF8));
        stdout.GetStringBuilder().Clear();
        Assert.NotEqual(0, await new EffortHoursApplication().RunAsync([.. refreshArgs, "--output", entries], stdout, stderr));
        Assert.Equal(entriesText, await File.ReadAllTextAsync(entries, Encoding.UTF8));
        Assert.Empty(stdout.ToString());
        await AssertDeclaredWorkdayReviewAsync(workspace, input, records, manifest, refresh);
        manifest = manifest with { SourceSemanticDigest = "sha256:" + new string('0', 64) };
        await File.WriteAllTextAsync(records, ContractJson.Serialize(manifest), new UTF8Encoding(false));
        stdout.GetStringBuilder().Clear();
        Assert.NotEqual(0, await new EffortHoursApplication().RunAsync(args[..^2], stdout, stderr));
        Assert.Empty(stdout.ToString());
    }
}
