using System.Text;
using System.Text.Json.Nodes;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangePortfolioCliTests
{
    private static async Task AssertRefreshPreflightAsync(string workspace, ChangeHistoricalRefreshPlan plan, ChangeHistoricalRefreshManifest input)
    {
        string planPath = Path.Combine(workspace, "preflight-plan.json"), currentPath = Path.Combine(workspace, "preflight-current.json");
        await File.WriteAllTextAsync(planPath, ContractJson.Serialize(plan), new UTF8Encoding(false));
        await File.WriteAllTextAsync(currentPath, ContractJson.Serialize(input), new UTF8Encoding(false));
        string savedPlan = await File.ReadAllTextAsync(planPath, Encoding.UTF8);
        string[] args = ["change", "check-refresh", planPath, "--entries", currentPath, "--compact"];
        var ready = await RunCliAsync(args);
        Assert.True(ready.ExitCode == 0, ready.StandardError);
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeHistoricalRefreshCheck, ready.StandardOutput).IsValid);
        var check = ContractJson.Deserialize<ChangeHistoricalRefreshCheck>(ready.StandardOutput);
        Assert.Empty(ContractValidation.Validate(check));
        Assert.Equal("ready-for-confirmation", check.Status);
        Assert.Equal("unresolved-workday", plan.Proposals[0].EvidenceStatus);
        Assert.DoesNotContain(workspace, ready.StandardOutput, StringComparison.OrdinalIgnoreCase);
        string receipt = Path.Combine(workspace, "preflight-receipt.json");
        var written = await RunCliAsync([.. args, "--output", receipt]);
        Assert.Equal(0, written.ExitCode);
        Assert.Empty(written.StandardOutput);
        Assert.Equal(ready.StandardOutput + "\n", await File.ReadAllTextAsync(receipt, Encoding.UTF8));
        Assert.NotEqual(0, (await RunCliAsync([.. args, "--output", receipt])).ExitCode);
        Assert.Equal(ready.StandardOutput + "\n", await File.ReadAllTextAsync(receipt, Encoding.UTF8));
        Assert.NotEqual(0, (await RunCliAsync([.. args, "--output", currentPath])).ExitCode);
        Assert.Equal(ContractJson.Serialize(input), await File.ReadAllTextAsync(currentPath, Encoding.UTF8));
        var snapshot = JsonNode.Parse(input.Entries[0].Original.GetRawText())!.AsObject();
        snapshot["description"] = plan.Proposals[0].ProposedDescription;
        var current = input with { Entries = [input.Entries[0] with { Original = System.Text.Json.JsonSerializer.SerializeToElement(snapshot) }] };
        await File.WriteAllTextAsync(currentPath, ContractJson.Serialize(current), new UTF8Encoding(false));
        var repeated = await RunCliAsync(args);
        Assert.Equal(0, repeated.ExitCode);
        Assert.Equal("already-current", ContractJson.Deserialize<ChangeHistoricalRefreshCheck>(repeated.StandardOutput).Entries[0].NoteStatus);
        snapshot["ticket"] = "user-changed-ticket";
        current = current with { Entries = [current.Entries[0] with { Original = System.Text.Json.JsonSerializer.SerializeToElement(snapshot) }] };
        string changed = ContractJson.Serialize(current);
        await File.WriteAllTextAsync(currentPath, changed, new UTF8Encoding(false));
        var blocked = await RunCliAsync(args);
        Assert.Equal(3, blocked.ExitCode);
        Assert.Contains("blocked", blocked.StandardError, StringComparison.Ordinal);
        var blockedCheck = ContractJson.Deserialize<ChangeHistoricalRefreshCheck>(blocked.StandardOutput);
        Assert.Equal("blocked-concurrent-edit", blockedCheck.Entries[0].NoteStatus);
        Assert.Empty(ContractValidation.Validate(blockedCheck));
        Assert.Equal(changed, await File.ReadAllTextAsync(currentPath, Encoding.UTF8));
        Assert.Equal(savedPlan, await File.ReadAllTextAsync(planPath, Encoding.UTF8));
        await File.WriteAllTextAsync(currentPath, ContractJson.Serialize(current with { Entries = [] }), new UTF8Encoding(false));
        var deleted = await RunCliAsync(args);
        Assert.Equal(3, deleted.ExitCode);
        Assert.Equal("blocked-missing-record", ContractJson.Deserialize<ChangeHistoricalRefreshCheck>(deleted.StandardOutput).Entries[0].NoteStatus);
        await File.WriteAllTextAsync(currentPath, "{}", new UTF8Encoding(false));
        var invalid = await RunCliAsync(args);
        Assert.Equal(1, invalid.ExitCode);
        Assert.Empty(invalid.StandardOutput);
        Assert.DoesNotContain(workspace, invalid.StandardError, StringComparison.OrdinalIgnoreCase);
    }
}
