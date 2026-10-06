using System.Globalization;
using System.Text;
using EffortHours.Cli;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangePortfolioCliTests
{
    private static async Task AssertDeclaredWorkdayReviewAsync(string workspace, string sourcePath, string recordsPath,
        ChangeWorkRecordManifest records, ChangeHistoricalRefreshManifest refresh)
    {
        string datesPath = Path.Combine(workspace, "declared-workdays.json"), entriesPath = Path.Combine(workspace, "declared-refresh.json");
        ChangeWorkdayManifest dates = new()
        {
            SourceSemanticDigest = records.SourceSemanticDigest,
            Workdays = [.. records.Records.Where(record => record.Kind == "implementation").Select(record => new ChangeDeclaredWorkday
            { RecordId = record.RecordId, Date = record.Date })],
        };
        string datesText = ContractJson.Serialize(dates);
        await File.WriteAllTextAsync(datesPath, datesText, new UTF8Encoding(false));
        string sourceText = await File.ReadAllTextAsync(sourcePath, Encoding.UTF8), recordsText = await File.ReadAllTextAsync(recordsPath, Encoding.UTF8);
        using StringWriter stdout = new(CultureInfo.InvariantCulture), stderr = new(CultureInfo.InvariantCulture);
        string[] policy = ["--workdays", datesPath, "--workday-policy", ChangeWorkdayPolicies.EqualDeclaredDaysV1,
            "--entry-policy", ChangeDeclaredWorkdayReviewPolicies.EqualEntries];
        string[] args = ["change", "review-days", sourcePath, "--work-records", recordsPath, .. policy, "--compact"];
        Assert.True(await new EffortHoursApplication().RunAsync(args, stdout, stderr) == 0, stderr.ToString());
        string json = stdout.ToString();
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeWorkdayReviewReport, json).IsValid);
        ChangeWorkdayReviewReport review = ContractJson.Deserialize<ChangeWorkdayReviewReport>(json);
        Assert.Empty(ContractValidation.Validate(review));
        ChangeWorkdayReviewDay lost = review.Days.Single(day => day.Date == "2026-01-23");
        Assert.Equal(0, lost.SourceAttributedExpectedHours);
        Assert.Equal("no-retained-change", lost.RetainedEvidenceStatus);
        Assert.Equal("external-work-record", lost.WorkdayEvidenceBasis);
        Assert.True(lost.AllocatedExpectedHours > 0);
        Assert.True(Assert.Single(lost.Records).AllocatedMultiplierContribution > 0);
        Assert.Equal(review.WorkdayResolution!.ExpectedMultiplierTotal, review.Days.SelectMany(day => day.Records).Sum(record => record.AllocatedMultiplierContribution));
        Assert.DoesNotContain(workspace, json, StringComparison.OrdinalIgnoreCase);
        refresh = refresh with
        {
            Entries = [.. records.Records.Where(record => record.Kind == "implementation").Select(record => refresh.Entries[0] with
            { RecordId = record.RecordId, EhePermission = "allowed" })],
        };
        string entriesText = ContractJson.Serialize(refresh);
        await File.WriteAllTextAsync(entriesPath, entriesText, new UTF8Encoding(false));
        stdout.GetStringBuilder().Clear();
        string[] refreshArgs = ["change", "plan-refresh", sourcePath, "--work-records", recordsPath, "--entries", entriesPath,
            .. policy, "--fields", "both", "--compact"];
        Assert.True(await new EffortHoursApplication().RunAsync(refreshArgs, stdout, stderr) == 0, stderr.ToString());
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeHistoricalRefreshPlan, stdout.ToString()).IsValid);
        ChangeHistoricalRefreshPlan plan = ContractJson.Deserialize<ChangeHistoricalRefreshPlan>(stdout.ToString());
        Assert.Empty(ContractValidation.Validate(plan));
        Assert.True(plan.DryRun);
        Assert.Equal(review.WorkdayResolution.ExpectedMultiplierTotal, plan.Proposals.Sum(proposal => proposal.ProposedMultiplierContribution));
        Assert.All(plan.Proposals, proposal => Assert.Contains(review.WorkdayResolution.Allocation.WorkdayInputDigest, proposal.ProposedDescription, StringComparison.Ordinal));
        Assert.Equal(entriesText, await File.ReadAllTextAsync(entriesPath, Encoding.UTF8));
        Assert.Equal(datesText, await File.ReadAllTextAsync(datesPath, Encoding.UTF8));
        Assert.Equal(sourceText, await File.ReadAllTextAsync(sourcePath, Encoding.UTF8));
        Assert.Equal(recordsText, await File.ReadAllTextAsync(recordsPath, Encoding.UTF8));
        stdout.GetStringBuilder().Clear();
        Assert.NotEqual(0, await new EffortHoursApplication().RunAsync([.. args, "--output", datesPath], stdout, stderr));
        Assert.Equal(datesText, await File.ReadAllTextAsync(datesPath, Encoding.UTF8));
        Assert.Empty(stdout.ToString());
        Assert.NotEqual(0, await new EffortHoursApplication().RunAsync(["change", "review-days", sourcePath, "--work-records", recordsPath,
            "--workdays", datesPath], stdout, stderr));
        Assert.Empty(stdout.ToString());
        Assert.NotEqual(0, await new EffortHoursApplication().RunAsync(["change", "plan-refresh", sourcePath, "--work-records", recordsPath,
            "--entries", entriesPath, "--workday-policy", ChangeWorkdayPolicies.EqualDeclaredDaysV1], stdout, stderr));
        Assert.Empty(stdout.ToString());
        await File.WriteAllTextAsync(entriesPath, ContractJson.Serialize(refresh with { Entries = [refresh.Entries[1]] }), new UTF8Encoding(false));
        Assert.NotEqual(0, await new EffortHoursApplication().RunAsync(refreshArgs, stdout, stderr));
        Assert.Empty(stdout.ToString());
        Assert.Equal(0, await new EffortHoursApplication().RunAsync([.. refreshArgs[..^2], "notes", "--compact"], stdout, stderr));
        Assert.Single(ContractJson.Deserialize<ChangeHistoricalRefreshPlan>(stdout.ToString()).Proposals);
    }
}
