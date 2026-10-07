using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Examples;

namespace EffortHours.EndToEndTests;

public sealed class ChangeHistoricalRefreshIntegrationTests : ChangeCliTestSupport
{
    private static string Example(string name) => Path.Combine(FindRepositoryRoot(), "examples", "historical-refresh", name);
    private static async Task<T> Read<T>(string name) => ContractJson.Deserialize<T>(await File.ReadAllTextAsync(Example(name), Encoding.UTF8));

    [Fact]
    public async Task PublishedOfflineFixtureReproducesCompleteReviewPlanAndCheckWithoutChangingInputs()
    {
        string[] names = ["comparison.json", "work-records.json", "declared-days.json", "entries.json", "review.json", "plan.json", "check-ready.json", "check-blocked.json"];
        string[] before = [.. names.Select(name => File.ReadAllText(Example(name), Encoding.UTF8))];
        var source = await Read<ChangePortfolioComparisonReport>(names[0]);
        Assert.Empty(ContractValidation.Validate(source));
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangePortfolioComparisonReport, before[0]).IsValid);
        string[] policy = ["--workdays", Example("declared-days.json"), "--workday-policy", ChangeWorkdayPolicies.EqualDeclaredDaysV1,
            "--entry-policy", ChangeDeclaredWorkdayReviewPolicies.EqualEntries];
        var reviewResult = await RunCliAsync(["change", "review-days", Example("comparison.json"), "--work-records", Example("work-records.json"), .. policy, "--compact"]);
        Assert.Equal(0, reviewResult.ExitCode);
        var review = ContractJson.Deserialize<ChangeWorkdayReviewReport>(reviewResult.StandardOutput);
        Assert.Empty(ContractValidation.Validate(review));
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeWorkdayReviewReport, reviewResult.StandardOutput).IsValid);
        Assert.Equal(ContractJson.SerializeCompact(await Read<ChangeWorkdayReviewReport>("review.json")), ContractJson.SerializeCompact(review));
        Assert.Equal(8m, review.ReferenceHoursPerDay);
        Assert.Equal(.28m, review.WorkdayResolution!.ExpectedMultiplierTotal);
        Assert.Equal(.28m, review.Days.SelectMany(day => day.Records).Sum(record => record.AllocatedMultiplierContribution));
        Assert.Equal(.07m, review.Days[0].Records.Single(record => record.RecordId == "entry-a").AllocatedMultiplierContribution);
        Assert.Equal(.07m, review.Days[0].Records.Single(record => record.RecordId == "entry-b").AllocatedMultiplierContribution);
        Assert.All(review.Days.SelectMany(day => day.Records).Where(record => record.Kind is "meeting" or "pto"), record => Assert.Null(record.AllocatedMultiplierContribution));
        Assert.Equal(0m, review.Days[1].SourceAttributedExpectedHours);
        Assert.Equal(ChangeWorkdayPolicies.Unresolved, review.Days[1].OriginalWorkdayStatus);
        Assert.Equal(.14m, review.Days[1].MatchedDailyMultiplier);
        var planned = await RunCliAsync(["change", "plan-refresh", Example("comparison.json"), "--work-records", Example("work-records.json"),
            "--entries", Example("entries.json"), .. policy, "--fields", "both", "--compact"]);
        Assert.Equal(0, planned.ExitCode);
        var plan = ContractJson.Deserialize<ChangeHistoricalRefreshPlan>(planned.StandardOutput);
        Assert.Empty(ContractValidation.Validate(plan));
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeHistoricalRefreshPlan, planned.StandardOutput).IsValid);
        Assert.Equal(ContractJson.SerializeCompact(await Read<ChangeHistoricalRefreshPlan>("plan.json")), ContractJson.SerializeCompact(plan));
        var ready = await RunCliAsync("change", "check-refresh", Example("plan.json"), "--entries", Example("entries.json"), "--compact");
        Assert.Equal(0, ready.ExitCode);
        var receipt = ContractJson.Deserialize<ChangeHistoricalRefreshCheck>(ready.StandardOutput);
        Assert.Empty(ContractValidation.Validate(receipt));
        Assert.Equal(ContractJson.SerializeCompact(await Read<ChangeHistoricalRefreshCheck>("check-ready.json")), ContractJson.SerializeCompact(receipt));
        var blocked = await Read<ChangeHistoricalRefreshCheck>("check-blocked.json");
        Assert.Empty(ContractValidation.Validate(blocked));
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeHistoricalRefreshCheck, before[7]).IsValid);
        Assert.Equal("blocked", blocked.Status);
        Assert.Throws<ArgumentException>(() => OfflineRefreshAdapter.Prepare(blocked, blocked.PlanDigest));
        Assert.Equal(before, names.Select(name => File.ReadAllText(Example(name), Encoding.UTF8)));
    }

    [Theory]
    [InlineData("concurrent", "blocked-concurrent-edit")]
    [InlineData("invoiced", "blocked-invoiced")]
    [InlineData("locked", "blocked-locked")]
    [InlineData("unknown-restriction", "blocked-unknown")]
    [InlineData("denied-note", "blocked-permission-denied")]
    [InlineData("unknown-note", "blocked-permission-unknown")]
    [InlineData("missing", "blocked-missing-record")]
    [InlineData("extra", "ready-to-set")]
    [InlineData("denied-ehe", "ready-to-set")]
    [InlineData("already-current", "already-current")]
    public async Task NativePreflightExitAndPersistedReceiptAgreeForEachIndependentCase(string change, string noteStatus)
    {
        using var temp = new RefreshDirectory();
        var input = await Read<ChangeHistoricalRefreshManifest>("entries.json");
        var plan = await Read<ChangeHistoricalRefreshPlan>("plan.json");
        var first = input.Entries[0];
        var snapshot = JsonNode.Parse(first.Original.GetRawText())!.AsObject();
        if (change == "concurrent") snapshot["description"] = "Concurrent user note";
        if (change == "already-current") snapshot["description"] = plan.Proposals.Single(row => row.RecordId == first.RecordId).ProposedDescription;
        first = first with
        {
            Original = JsonSerializer.SerializeToElement(snapshot),
            NotePermission = change == "denied-note" ? "denied" : change == "unknown-note" ? "unknown" : first.NotePermission,
            EhePermission = change == "denied-ehe" ? "denied" : first.EhePermission,
            Restriction = change is "invoiced" or "locked" ? change : change == "unknown-restriction" ? "unknown" : first.Restriction
        };
        var current = input with { Entries = [first, .. input.Entries.Skip(1)] };
        if (change == "missing") current = current with { Entries = [.. current.Entries.Skip(1)] };
        if (change == "extra") current = current with { Entries = [.. current.Entries, first with { RecordId = "extra" }] };
        string path = Path.Combine(temp.Path, "current.json"), output = Path.Combine(temp.Path, "receipt.json");
        string original = ContractJson.Serialize(current);
        await File.WriteAllTextAsync(path, original, new UTF8Encoding(false));
        string[] args = ["change", "check-refresh", Example("plan.json"), "--entries", path, "--compact", "--output", output];
        var result = await RunCliAsync(args);
        bool ready = change == "already-current";
        Assert.Equal(ready ? 0 : 3, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        var receipt = ContractJson.Deserialize<ChangeHistoricalRefreshCheck>(await File.ReadAllTextAsync(output, Encoding.UTF8));
        Assert.Empty(ContractValidation.Validate(receipt));
        Assert.True(ContractSchemaValidator.Validate(SchemaNames.ChangeHistoricalRefreshCheck, ContractJson.Serialize(receipt)).IsValid);
        Assert.Equal(ready ? "ready-for-confirmation" : "blocked", receipt.Status);
        Assert.Equal(noteStatus, receipt.Entries[0].NoteStatus);
        if (change == "concurrent") Assert.Equal("concurrent-edit", receipt.Entries[0].SnapshotStatus);
        if (change == "denied-ehe") Assert.Equal("blocked-permission-denied", receipt.Entries[0].EheStatus);
        if (change == "extra") Assert.Equal(["extra"], receipt.UnexpectedRecordIds);
        if (!ready) Assert.Throws<ArgumentException>(() => OfflineRefreshAdapter.Prepare(receipt, receipt.PlanDigest));
        Assert.Equal(original, await File.ReadAllTextAsync(path, Encoding.UTF8));
        string saved = await File.ReadAllTextAsync(output, Encoding.UTF8);
        Assert.Equal(1, (await RunCliAsync(args)).ExitCode);
        Assert.Equal(saved, await File.ReadAllTextAsync(output, Encoding.UTF8));
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("source-digest")]
    [InlineData("record-digest")]
    [InlineData("range")]
    [InlineData("oversized")]
    [InlineData("output-directory")]
    public async Task FailedPreflightHasExitOneAndNoNewReceipt(string failure)
    {
        using var temp = new RefreshDirectory();
        var input = await Read<ChangeHistoricalRefreshManifest>("entries.json");
        input = failure switch
        {
            "source-digest" => input with { SourceSemanticDigest = "sha256:" + new string('0', 64) },
            "record-digest" => input with { WorkRecordInputDigest = "sha256:" + new string('0', 64) },
            "range" => input with { UntilExclusiveDate = "2026-01-25" },
            _ => input,
        };
        string path = Path.Combine(temp.Path, "current.json"), output = Path.Combine(temp.Path, "receipt.json");
        string text = failure == "malformed" ? "{}" : failure == "oversized" ? new string(' ', 1048577) : ContractJson.Serialize(input);
        await File.WriteAllTextAsync(path, text, new UTF8Encoding(false));
        if (failure == "output-directory") Directory.CreateDirectory(output);
        var result = await RunCliAsync("change", "check-refresh", Example("plan.json"), "--entries", path, "--output", output);
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.StandardOutput);
        Assert.False(File.Exists(output));
        Assert.DoesNotContain(temp.Path, result.StandardError, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(text, await File.ReadAllTextAsync(path, Encoding.UTF8));
        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp-*"));
    }

    [Fact]
    public async Task ExampleConditionalAdapterRejectsLaterRacesAndSupportsRereadAfterInterruptedResponse()
    {
        var check = await Read<ChangeHistoricalRefreshCheck>("check-ready.json");
        Assert.Throws<ArgumentException>(() => OfflineRefreshAdapter.Prepare(check, "wrong-confirmation"));
        Assert.Throws<ArgumentException>(() => OfflineRefreshAdapter.Prepare(check with { PlanDigest = "sha256:" + new string('0', 64) }, check.PlanDigest));
        var handoff = OfflineRefreshAdapter.Prepare(check, check.PlanDigest);
        Assert.Equal(3, handoff.Notes.Count);
        Assert.Equal(.28m, handoff.Ehe.Sum(proposal => proposal.Contribution));
        var mutation = handoff.Notes[0];
        var entry = check.Current.Entries.Single(row => row.RecordId == mutation.RecordId);
        foreach (var change in new[] { entry with { NotePermission = "denied" }, entry with { Restriction = "invoiced" }, entry with { Restriction = "locked" } })
        {
            var denied = new OfflineRefreshAdapter.MemoryNoteStore(entry);
            denied.ObserveExternalChange(change);
            Assert.Equal("blocked-permission-or-restriction", denied.TrySetNote(mutation));
            Assert.Equal(change, denied.Read());
        }
        var store = new OfflineRefreshAdapter.MemoryNoteStore(entry);
        var raced = JsonNode.Parse(entry.Original.GetRawText())!.AsObject();
        raced["ticket"] = "User changed ticket after preflight";
        var fresh = entry with { Original = JsonSerializer.SerializeToElement(raced) };
        store.ObserveExternalChange(fresh);
        Assert.Equal("blocked-concurrent-edit", store.TrySetNote(mutation));
        Assert.Equal(fresh, store.Read());
        store = new(entry);
        Assert.Equal("applied", store.TrySetNote(mutation)); // Response could be lost after this point.
        Assert.Equal(mutation.TargetSnapshotDigest, ChangeHistoricalSnapshot.Digest(store.Read().Original));
        Assert.Equal("already-current", store.TrySetNote(mutation));
        Assert.Equal(mutation.Description, store.Read().Original.GetProperty("description").GetString());
        foreach (var field in entry.Original.EnumerateObject().Where(field => field.Name != "description"))
            Assert.Equal(ContractJson.SerializeCompact(field.Value), ContractJson.SerializeCompact(store.Read().Original.GetProperty(field.Name)));
        var current = check.Current with { Entries = [store.Read(), .. check.Current.Entries.Skip(1)] };
        var repeated = ChangeHistoricalRefreshPreflight.Check(check.Plan, current);
        Assert.Equal("already-current", repeated.Entries[0].NoteStatus);
        Assert.Equal(2, OfflineRefreshAdapter.Prepare(repeated, repeated.PlanDigest).Notes.Count);
        Assert.Equal(3, OfflineRefreshAdapter.Prepare(repeated, repeated.PlanDigest).Ehe.Count); // No inferred numeric mapping.
    }

    [Fact]
    public async Task WindowsPowerShellWrapperMustExplicitlyPreserveNativeBlockedExit()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var temp = new RefreshDirectory();
        var blocked = await Read<ChangeHistoricalRefreshCheck>("check-blocked.json");
        string current = Path.Combine(temp.Path, "current.json");
        await File.WriteAllTextAsync(current, ContractJson.Serialize(blocked.Current), new UTF8Encoding(false));
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        string assembly = Path.Combine(FindRepositoryRoot(), "src", "EffortHours.Cli", "bin", configuration, "net10.0", "efforthours.dll");
        string command = "& dotnet " + string.Join(" ", new[] { assembly, "change", "check-refresh", Example("plan.json"), "--entries", current, "--compact" }
            .Select(value => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'"));
        foreach (bool preserve in new[] { false, true })
        {
            var start = StartInfo("powershell.exe", temp.Path);
            foreach (string value in new[] { "-NoProfile", "-Command", command + (preserve ? "; exit $LASTEXITCODE" : "") }) start.ArgumentList.Add(value);
            var result = await RunAsync(start);
            Assert.Equal(preserve ? 3 : 1, result.ExitCode);
            Assert.Equal("blocked", ContractJson.Deserialize<ChangeHistoricalRefreshCheck>(result.StandardOutput).Status);
        }
        var help = await RunCliAsync("change", "check-refresh", "--help");
        Assert.Contains("Exit 1", help.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("Exit 3", help.StandardOutput, StringComparison.Ordinal);
        Assert.Equal(2, (await RunCliAsync("change", "check-refresh", Example("plan.json"))).ExitCode);
    }

    private sealed class RefreshDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "efforthours-refresh", Guid.NewGuid().ToString("N"));
        public RefreshDirectory() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}
