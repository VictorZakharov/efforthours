using System.Text.Json;
using Json.Schema;

namespace EffortHours.EndToEndTests;

public sealed class ChangeLogicalDecompositionArtifactTests
{
    [Fact]
    public void StageAReviewUsesGranularLogicalTasksAndExactFrozenTotals()
    {
        string root = FindRepositoryRoot();
        string path = Path.Combine(
            root,
            "calibration",
            "changes",
            "stage-a-logical-review",
            "0.1.0.decomposition.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement artifact = document.RootElement;
        string schemaPath = Path.Combine(
            Path.GetDirectoryName(path)!,
            "change-logical-decomposition-1.0.0.schema.json");
        JsonSchema schema = JsonSchema.FromText(File.ReadAllText(schemaPath));
        EvaluationResults schemaResult = schema.Evaluate(
            artifact,
            new EvaluationOptions { RequireFormatValidation = true });
        Assert.True(schemaResult.IsValid, "The Stage A logical audit must satisfy its versioned schema.");

        Assert.Equal(
            "change-logical-decomposition/1.0.0",
            artifact.GetProperty("schemaVersion").GetString());
        Assert.Equal("change-ehe-work-item/1.1.0", artifact.GetProperty("rubric").GetString());
        JsonElement review = artifact.GetProperty("review");
        Assert.Equal("teacher-estimate", review.GetProperty("status").GetString());
        Assert.Equal("host-ai", review.GetProperty("reviewerKind").GetString());
        Assert.False(string.IsNullOrWhiteSpace(review.GetProperty("modelId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(review.GetProperty("modelVersion").GetString()));
        Assert.Contains("candidate values were visible", review.GetProperty("inputBoundary").GetString());

        JsonElement[] records = [.. artifact.GetProperty("records").EnumerateArray()];
        Assert.Equal(5, records.Length);
        Assert.Equal(5, records.Select(RepositoryId).Distinct(StringComparer.Ordinal).Count());

        HashSet<string> taskIds = new(StringComparer.Ordinal);
        decimal aggregate = 0m;
        int aggregateTargetCount = 0;
        foreach (JsonElement record in records)
        {
            string corpusPath = Path.Combine(
                root,
                record.GetProperty("sourceCorpus").GetString()!.Replace('/', Path.DirectorySeparatorChar));
            using JsonDocument corpus = JsonDocument.Parse(File.ReadAllText(corpusPath));
            JsonElement frozenRecord = Assert.Single(
                corpus.RootElement.GetProperty("records").EnumerateArray(),
                candidate => candidate.GetProperty("id").GetString() ==
                    record.GetProperty("recordId").GetString());
            HashSet<string> targetIds = [.. frozenRecord.GetProperty("targets")
                .EnumerateArray()
                .Select(target => target.GetProperty("id").GetString()!)];
            decimal frozenTotal = frozenRecord.GetProperty("targets")
                .EnumerateArray()
                .Sum(target => target.GetProperty("hours").GetProperty("expected").GetDecimal());
            decimal declaredTotal = record.GetProperty("teacherExpectedHours").GetDecimal();
            Assert.Equal(frozenTotal, declaredTotal);

            JsonElement[] tasks = [.. record.GetProperty("tasks").EnumerateArray()];
            Assert.NotEmpty(tasks);
            Assert.Equal(
                tasks.Length,
                tasks.Select(task => task.GetProperty("title").GetString())
                    .Distinct(StringComparer.Ordinal)
                    .Count());
            Assert.Equal(
                tasks.Length,
                tasks.Select(task => task.GetProperty("rationale").GetString())
                    .Distinct(StringComparer.Ordinal)
                    .Count());

            decimal taskTotal = 0m;
            HashSet<string> usedParentIds = new(StringComparer.Ordinal);
            foreach (JsonElement task in tasks)
            {
                string taskId = task.GetProperty("id").GetString()!;
                Assert.True(taskIds.Add(taskId), $"Duplicate logical task ID: {taskId}");
                string parentTargetId = task.GetProperty("parentTargetId").GetString()!;
                Assert.Contains(parentTargetId, targetIds);
                usedParentIds.Add(parentTargetId);
                Assert.False(string.IsNullOrWhiteSpace(task.GetProperty("category").GetString()));
                Assert.True(task.GetProperty("title").GetString()!.Length >= 20);
                Assert.True(task.GetProperty("rationale").GetString()!.Length >= 40);
                decimal expected = task.GetProperty("expectedHours").GetDecimal();
                Assert.InRange(expected, 0.5m, 1.5m);
                taskTotal += expected;
            }

            Assert.Equal(declaredTotal, taskTotal);
            Assert.True(usedParentIds.SetEquals(targetIds));
            aggregate += taskTotal;
            aggregateTargetCount += targetIds.Count;
        }

        Assert.Equal(38m, aggregate);
        Assert.Equal(28, aggregateTargetCount);
        Assert.Equal(45, taskIds.Count);
    }

    private static string RepositoryId(JsonElement record) =>
        record.GetProperty("repositoryId").GetString()!;

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "EffortHours.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the EffortHours repository root.");
    }
}
