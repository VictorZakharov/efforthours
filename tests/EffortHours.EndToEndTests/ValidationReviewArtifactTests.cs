using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed class ValidationReviewArtifactTests
{
    private const string ReviewPath =
        "calibration/corpora/public-readiness/1.3.0.validation-review-plan.json";
    private const string OpeningPath =
        "calibration/corpora/public-readiness/1.3.0/1.3.0.validation-opening.json";
    private const string ReviewDigest =
        "sha256:f68e1e590d60547e657f551a883291f153b45d31022187e7dd064ceef55b9cd1";

    [Fact]
    public void ValidationReviewFreezesTheCompleteBlindTeacherCohort()
    {
        string root = FindRepositoryRoot();
        string json = File.ReadAllText(Path.Combine(root, ReviewPath));
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement plan = document.RootElement;

        Assert.Equal(ReviewDigest, Digest(json));
        Assert.Equal("efforthours-public-readiness-validation", String(plan, "id"));
        Assert.Equal("1.3.0", String(plan, "version"));
        Assert.Equal("ehe-work-item", String(plan.GetProperty("rubric"), "id"));
        Assert.Equal("1.1.0", String(plan.GetProperty("rubric"), "version"));

        JsonElement[] records = [.. plan.GetProperty("records").EnumerateArray()];
        Assert.Equal(9, records.Length);
        Assert.Equal(9, records.Select(record => String(record.GetProperty("repository"), "id"))
            .Distinct(StringComparer.Ordinal).Count());
        Assert.All(records, record =>
        {
            Assert.Equal("validation", String(record, "partition"));
            Assert.Equal("seed-rules/0.4.0", String(record, "sourceEstimatorVersion"));
            JsonElement review = record.GetProperty("review");
            Assert.Equal("teacher-estimate", String(review, "status"));
            Assert.Equal("2026-08-15", String(review, "completedOn"));
            JsonElement reviewer = Assert.Single(review.GetProperty("reviewers").EnumerateArray());
            Assert.Equal("teacher:openai-codex-gpt-5-2026-08", String(reviewer, "id"));
            Assert.Equal("host-ai", String(reviewer, "kind"));
            Assert.Equal("teacher", String(reviewer, "role"));
        });

        (decimal Low, decimal Expected, decimal High, int Excluded, int AboveEight, int Count) =
            Totals(records);
        Assert.Equal(2747, Count);
        Assert.Equal(111, Excluded);
        Assert.Equal(538, AboveEight);
        Assert.Equal(37809.00m, Low);
        Assert.Equal(46045.50m, Expected);
        Assert.Equal(56110.25m, High);

        Dictionary<string, decimal> expectedByRepository = records.ToDictionary(
            record => String(record.GetProperty("repository"), "name"),
            record => record.GetProperty("capabilities").EnumerateArray()
                .SelectMany(capability => capability.GetProperty("targets").EnumerateArray())
                .Sum(target => target.GetProperty("hours").GetProperty("expected").GetDecimal()),
            StringComparer.Ordinal);
        Assert.Equal(536.75m, expectedByRepository["sindresorhus/ky"]);
        Assert.Equal(966.75m, expectedByRepository["axios/axios"]);
        Assert.Equal(14644.75m, expectedByRepository["nrwl/nx"]);
        Assert.Equal(274.00m, expectedByRepository["Cysharp/ConsoleAppFramework"]);
        Assert.Equal(1009.25m, expectedByRepository["spectreconsole/spectre.console"]);
        Assert.Equal(13535.00m, expectedByRepository["dotnet/efcore"]);
        Assert.Equal(440.50m, expectedByRepository["jasontaylordev/CleanArchitecture"]);
        Assert.Equal(699.25m, expectedByRepository["ElectronNET/Electron.NET"]);
        Assert.Equal(13939.25m, expectedByRepository["OrchardCMS/OrchardCore"]);
    }

    [Fact]
    public void ValidationOpeningAndReviewDigestsPreserveThePreCandidateBoundary()
    {
        string root = FindRepositoryRoot();
        string opening = File.ReadAllText(Path.Combine(root, OpeningPath));
        using JsonDocument document = JsonDocument.Parse(opening);
        JsonElement manifest = document.RootElement;

        Assert.Equal(
            "sha256:ff7ff99ff97104b01f6306751f60d05b399bbc334c9d436537f2d23e267a12b5",
            Digest(opening));
        Assert.Equal(
            "strict-blind-validation-packets-generated-candidate-values-unavailable",
            String(manifest, "status"));
        Assert.Empty(manifest.GetProperty("failures").EnumerateArray());
        Assert.Empty(manifest.GetProperty("contaminatedFamilies").EnumerateArray());
        JsonElement boundary = manifest.GetProperty("boundary");
        Assert.False(boundary.GetProperty("validationCandidateOutputsGenerated").GetBoolean());
        Assert.False(boundary.GetProperty("validationLabelsAuthored").GetBoolean());
        Assert.Equal("not-performed", String(boundary, "testSourceAccess"));
        Assert.False(boundary.GetProperty("testCandidateOutputsGenerated").GetBoolean());
        Assert.False(boundary.GetProperty("testLabelsAuthored").GetBoolean());
    }

    private static (decimal Low, decimal Expected, decimal High, int Excluded, int AboveEight, int Count)
        Totals(IEnumerable<JsonElement> records)
    {
        decimal low = 0m;
        decimal expected = 0m;
        decimal high = 0m;
        int excluded = 0;
        int aboveEight = 0;
        int count = 0;
        foreach (JsonElement target in records
                     .SelectMany(record => record.GetProperty("capabilities").EnumerateArray())
                     .SelectMany(capability => capability.GetProperty("targets").EnumerateArray()))
        {
            JsonElement hours = target.GetProperty("hours");
            decimal targetLow = hours.GetProperty("low").GetDecimal();
            decimal targetExpected = hours.GetProperty("expected").GetDecimal();
            decimal targetHigh = hours.GetProperty("high").GetDecimal();
            Assert.True(targetLow <= targetExpected);
            Assert.True(targetExpected <= targetHigh);
            if (targetExpected == 0m)
            {
                excluded++;
                Assert.Equal(0m, targetLow);
                Assert.Equal(0m, targetHigh);
                Assert.StartsWith(
                    "Explicit rubric-qualified exclusion:",
                    String(target, "sizeException"),
                    StringComparison.Ordinal);
            }
            else
            {
                Assert.True(targetExpected >= 0.5m);
            }

            if (targetExpected > 8m)
            {
                aboveEight++;
                Assert.False(string.IsNullOrWhiteSpace(String(target, "sizeException")));
            }

            low += targetLow;
            expected += targetExpected;
            high += targetHigh;
            count++;
        }

        return (low, expected, high, excluded, aboveEight, count);
    }

    private static string String(JsonElement element, string property) =>
        element.GetProperty(property).GetString()!;

    private static string Digest(string content)
    {
        string normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
        return $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant()}";
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "EffortHours.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
