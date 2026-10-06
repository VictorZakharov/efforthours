using System.Globalization;
using EffortHours.Cli;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangePortfolioCliTests
{
    private static async Task AssertWorkdayAllocationAsync(string workspace, ChangePortfolioComparisonReport source)
    {
        string input = Path.Combine(workspace, "allocation-source.json");
        string declarations = Path.Combine(workspace, "workdays.json");
        string output = Path.Combine(workspace, "allocated.json");
        string original = ContractJson.Serialize(source);
        await File.WriteAllTextAsync(input, original, System.Text.Encoding.UTF8);
        ChangeWorkdayManifest manifest = new()
        {
            SourceSemanticDigest = source.Verification.SemanticDigest,
            Workdays = [.. Enumerable.Range(19, 5).Select(day => new ChangeDeclaredWorkday
            {
                RecordId = "external-" + day.ToString(CultureInfo.InvariantCulture),
                Date = "2026-01-" + day.ToString(CultureInfo.InvariantCulture),
                LoggedHours = day % 2 == 0 ? 4 : 12,
            })],
        };
        await File.WriteAllTextAsync(declarations, ContractJson.Serialize(manifest), System.Text.Encoding.UTF8);
        using StringWriter stdout = new(CultureInfo.InvariantCulture);
        using StringWriter stderr = new(CultureInfo.InvariantCulture);
        string[] args = ["change", "allocate-days", input, "--workdays", declarations,
            "--policy", ChangeWorkdayPolicies.EqualDeclaredDaysV1, "--compact", "--output", output];
        Assert.True(await new EffortHoursApplication().RunAsync(args, stdout, stderr) == 0, stderr.ToString());
        string json = await File.ReadAllTextAsync(output);
        SchemaValidationResult schema = ContractSchemaValidator.Validate(SchemaNames.ChangeWorkdayAllocationReport, json);
        Assert.True(schema.IsValid, string.Join(" ", schema.Errors));
        ChangeWorkdayAllocationReport allocated = ContractJson.Deserialize<ChangeWorkdayAllocationReport>(json);
        Assert.Empty(ContractValidation.Validate(allocated));
        Assert.Equal(source.SourcePortfolio!.TotalEffort, allocated.TotalEffort);
        Assert.Equal(40, allocated.TotalCapacityHours);
        Assert.All(allocated.Days, day =>
        {
            Assert.Equal(8, day.CapacityHours);
            Assert.Equal("allocated", day.Status);
            Assert.Equal(ChangeWorkdayPolicies.Unresolved, day.OriginalWorkdayStatus);
            Assert.True(day.AllocatedEffort.Expected > 0);
        });
        Assert.Equal(source.SourcePortfolio.TotalEffort.Expected, allocated.Days.Sum(day => day.AllocatedEffort.Expected));
        Assert.Equal(original, await File.ReadAllTextAsync(input));
        Assert.DoesNotContain(workspace, json, StringComparison.OrdinalIgnoreCase);
        // Existing output and source paths cannot be overwritten.
        Assert.NotEqual(0, await new EffortHoursApplication().RunAsync(args, stdout, stderr));
        Assert.Equal(json, await File.ReadAllTextAsync(output));
        Assert.NotEqual(0, await new EffortHoursApplication().RunAsync([.. args.Take(args.Length - 1), input], stdout, stderr));
        Assert.Equal(original, await File.ReadAllTextAsync(input));
        // Changing external durations never changes EHE or fixed reference capacity.
        manifest = manifest with { Workdays = [.. manifest.Workdays.Select(day => day with { LoggedHours = day.LoggedHours == 4 ? 12 : 4 })] };
        await File.WriteAllTextAsync(declarations, ContractJson.Serialize(manifest), System.Text.Encoding.UTF8);
        stdout.GetStringBuilder().Clear();
        Assert.Equal(0, await new EffortHoursApplication().RunAsync(args[..^2], stdout, stderr));
        ChangeWorkdayAllocationReport changedDurations = ContractJson.Deserialize<ChangeWorkdayAllocationReport>(stdout.ToString());
        Assert.Equal(allocated.TotalEffort, changedDurations.TotalEffort);
        Assert.Equal(allocated.TotalCapacityHours, changedDurations.TotalCapacityHours);
        Assert.Equal(allocated.Days.Select(day => day.AllocatedEffort), changedDurations.Days.Select(day => day.AllocatedEffort));
        // Wrong source binding is an error, never a zero or allocated aggregate.
        manifest = manifest with { SourceSemanticDigest = "sha256:" + new string('0', 64) };
        await File.WriteAllTextAsync(declarations, ContractJson.Serialize(manifest), System.Text.Encoding.UTF8);
        stdout.GetStringBuilder().Clear();
        Assert.NotEqual(0, await new EffortHoursApplication().RunAsync(args[..^2], stdout, stderr));
        Assert.Equal("", stdout.ToString());
    }
}
