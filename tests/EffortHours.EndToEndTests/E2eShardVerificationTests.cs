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
            Directory.CreateDirectory(directory);
            IReadOnlyDictionary<string, int> assignment = await AssignAsync(directory, inventory, 4, weightsPath: null);
            for (int index = 0; index < 4; index++)
            {
                if (mutation == "missing" && index == 0) continue;
                string[] cases = [.. inventory.Where(name => assignment[name] == index)];
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

    [Fact]
    public async Task ShardAssignmentSeparatesHeavySerialMethodsDeterministically()
    {
        string directory = Path.Combine(Path.GetTempPath(), "efforthours-shard-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            const string Synthetic = "EffortHours.EndToEndTests.SyntheticCases.";
            const string Other = "EffortHours.EndToEndTests.OtherCases.";
            string weights = Path.Combine(directory, "weights.json");
            await File.WriteAllTextAsync(weights, JsonSerializer.Serialize(new
            {
                schemaVersion = "e2e-shard-weights/1.0.0",
                methods = new Dictionary<string, int>
                {
                    [Synthetic + "HeavyA"] = 120,
                    [Synthetic + "HeavyB"] = 100,
                    [Other + "HeavyC"] = 90,
                },
            }), Encoding.UTF8);
            string[] inventory =
            [
                Synthetic + "HeavyA",
                Synthetic + "HeavyB(value: 1)",
                Synthetic + "HeavyB(value: 2)",
                Other + "HeavyC",
                .. Enumerable.Range(0, 30).Select(index => (index % 2 == 0 ? Synthetic : Other) + $"Light{index}"),
            ];

            IReadOnlyDictionary<string, int> first = await AssignAsync(directory, inventory, 3, weights);
            IReadOnlyDictionary<string, int> second = await AssignAsync(directory, [.. inventory.Reverse()], 3, weights);

            Assert.Equal(first.OrderBy(pair => pair.Key, StringComparer.Ordinal), second.OrderBy(pair => pair.Key, StringComparer.Ordinal));
            Assert.Equal(33, first.Count);
            Assert.Equal(3, new[] { first[Synthetic + "HeavyA"], first[Synthetic + "HeavyB"], first[Other + "HeavyC"] }.Distinct().Count());
            Assert.Equal([0, 1, 2], first.Values.Distinct().Order());

            await File.WriteAllTextAsync(weights, "{\"schemaVersion\":\"e2e-shard-weights/0.0.0\",\"methods\":{}}", Encoding.UTF8);
            await Assert.ThrowsAsync<InvalidOperationException>(() => AssignAsync(directory, inventory, 3, weights));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    private static async Task<IReadOnlyDictionary<string, int>> AssignAsync(
        string directory, string[] inventory, int count, string? weightsPath)
    {
        string cases = Path.Combine(directory, $"cases-{Guid.NewGuid():N}.txt");
        await File.WriteAllLinesAsync(cases, inventory, Encoding.UTF8);
        string script = Path.Combine(directory, "assign.ps1");
        await File.WriteAllTextAsync(script, """
            param([string] $Root, [string] $Cases, [int] $Count, [string] $Weights)
            . (Join-Path $Root "eng/e2e-shard-common.ps1")
            $arguments = @{ Cases = @(Get-Content -LiteralPath $Cases -Encoding utf8); Count = $Count }
            if ($Weights) { $arguments.WeightsPath = $Weights }
            $assignment = Get-E2eShardAssignment @arguments
            foreach ($method in $assignment.Keys) { "$method=$($assignment[$method])" }
            """, Encoding.UTF8);
        string root = FindRepositoryRoot();
        var start = StartInfo("pwsh", root);
        foreach (string argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File", script, root, cases,
            count.ToString(System.Globalization.CultureInfo.InvariantCulture), weightsPath ?? "" })
            start.ArgumentList.Add(argument);
        ProcessResult result = await RunAsync(start);
        if (result.ExitCode != 0) throw new InvalidOperationException(result.StandardError);
        return result.StandardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('=', 2))
            .ToDictionary(parts => parts[0], parts => int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture), StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("pending")]
    [InlineData("missing")]
    [InlineData("failed")]
    [InlineData("duplicate")]
    [InlineData("cancelled")]
    public async Task CoordinatorRequiresSuccessfulUniqueCurrentAttemptPeers(string mutation)
    {
        string directory = Path.Combine(Path.GetTempPath(), "efforthours-peer-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            var jobs = new List<object>();
            for (int index = 1; index < 3; index++)
            {
                if (index == 1 && mutation == "missing") continue;
                var job = new
                {
                    name = $"E2E shard (ubuntu-latest, {index})",
                    status = index == 1 && mutation == "pending" ? "in_progress" : "completed",
                    conclusion = index == 1 && mutation is "failed" or "cancelled"
                        ? mutation == "failed" ? "failure" : "cancelled" : "success",
                };
                jobs.Add(job);
                if (index == 1 && mutation == "duplicate") jobs.Add(job);
            }
            string data = Path.Combine(directory, "jobs.json");
            await File.WriteAllTextAsync(data, JsonSerializer.Serialize(jobs), Encoding.UTF8);
            string script = Path.Combine(directory, "check.ps1");
            await File.WriteAllTextAsync(script, """
                param([string] $Root, [string] $Data)
                . (Join-Path $Root "eng/e2e-shard-common.ps1")
                $jobs = @(Get-Content -LiteralPath $Data -Raw -Encoding utf8 | ConvertFrom-Json)
                Test-E2ePeersCompleted -Jobs $jobs -OperatingSystem "ubuntu-latest" -Count 3
                """, Encoding.UTF8);
            string root = FindRepositoryRoot();
            var start = StartInfo("pwsh", root);
            foreach (string argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File", script, root, data })
                start.ArgumentList.Add(argument);
            ProcessResult result = await RunAsync(start);
            if (mutation is "failed" or "duplicate" or "cancelled") Assert.NotEqual(0, result.ExitCode);
            else
            {
                Assert.True(result.ExitCode == 0, result.StandardError);
                Assert.Equal(mutation == "valid" ? "True" : "False", result.StandardOutput.Trim());
            }
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SdkSelectionHonorsGlobalJsonAndReportsSetupFallback(bool compatible)
    {
        string directory = Path.Combine(Path.GetTempPath(), "efforthours-sdk-tests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            string root = FindRepositoryRoot();
            string global = compatible ? await File.ReadAllTextAsync(Path.Combine(root, "global.json"), Encoding.UTF8)
                : "{\"sdk\":{\"version\":\"10.999.999\",\"rollForward\":\"disable\"}}";
            await File.WriteAllTextAsync(Path.Combine(directory, "global.json"), global, Encoding.UTF8);
            string output = Path.Combine(directory, "outputs.txt");
            var start = StartInfo("pwsh", directory);
            start.Environment["GITHUB_OUTPUT"] = output;
            foreach (string argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File", Path.Combine(root, "eng", "select-ci-sdk.ps1") })
                start.ArgumentList.Add(argument);
            ProcessResult result = await RunAsync(start);
            Assert.True(result.ExitCode == 0, result.StandardError);
            string outputs = await File.ReadAllTextAsync(output, Encoding.UTF8);
            Assert.Contains($"available={compatible.ToString().ToLowerInvariant()}", outputs, StringComparison.Ordinal);
            Assert.Contains("nuget_cache_path=", outputs, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

}
