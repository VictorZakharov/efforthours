using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed class E2eShardVerificationTests : ChangeCliTestSupport
{
    [Theory]
    [InlineData("valid")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("failed")]
    [InlineData("skipped")]
    [InlineData("head")]
    [InlineData("inventory")]
    [InlineData("count")]
    [InlineData("misplaced")]
    [InlineData("omitted")]
    public async Task AggregateGuardRequiresEveryCurrentHeadCaseExactlyOnce(string mutation)
    {
        string directory = Path.Combine(Path.GetTempPath(), "efforthours-shard-tests", Guid.NewGuid().ToString("N"));
        try
        {
            string[] inventory = [.. Enumerable.Range(0, 64).Select(index => $"EffortHours.EndToEndTests.SyntheticCases.Method{index}")];
            for (int index = 0; index < 4; index++)
            {
                if (mutation == "missing" && index == 0) continue;
                string[] cases = [.. inventory.Where(name => SHA256.HashData(Encoding.UTF8.GetBytes(name))[0] % 4 == index)];
                Assert.NotEmpty(cases);
                if (index == 0 && mutation == "omitted") cases = [.. cases.Skip(1)];
                if (index == 0 && mutation == "misplaced") cases = [.. cases, inventory.First(name => !cases.Contains(name, StringComparer.Ordinal))];
                string path = Path.Combine(directory, index.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Directory.CreateDirectory(path);
                await File.WriteAllTextAsync(Path.Combine(path, "receipt.json"), JsonSerializer.Serialize(new
                {
                    schemaVersion = "e2e-shard/1.0.0",
                    head = index == 0 && mutation == "head" ? "stale" : "synthetic",
                    index = index == 0 && mutation == "duplicate" ? 1 : index,
                    count = index == 0 && mutation == "count" ? 2 : 4,
                    inventory = index == 0 && mutation == "inventory" ? [.. inventory.Skip(1)] : inventory,
                    results = cases.Select((name, position) => new
                    {
                        name,
                        outcome = index == 0 && position == 0 && mutation is "failed" or "skipped"
                            ? mutation == "failed" ? "Failed" : "NotExecuted" : "Passed",
                        duration = "00:00:01",
                    }),
                }), Encoding.UTF8);
            }

            var start = StartInfo("pwsh", FindRepositoryRoot());
            foreach (string argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File",
                "eng/verify-e2e-shards.ps1", "-Directory", directory, "-Count", "4", "-Head", "synthetic" })
                start.ArgumentList.Add(argument);
            ProcessResult result = await RunAsync(start);
            if (mutation == "valid")
            {
                Assert.True(result.ExitCode == 0, result.StandardError);
                Assert.Contains("coverage of 64 E2E cases", result.StandardOutput, StringComparison.Ordinal);
            }
            else
            {
                Assert.NotEqual(0, result.ExitCode);
            }
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }
}
