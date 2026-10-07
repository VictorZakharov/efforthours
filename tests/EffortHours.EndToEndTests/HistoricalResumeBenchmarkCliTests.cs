using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed partial class ChangeBenchmarkCliTests
{
    [Fact]
    public async Task HistoricalInterruptionReopensAtomicSidecarsAndFetchesOnly236Of440Candidates()
    {
        string cache = Path.Combine(Path.GetTempPath(), "efforthours-resume-benchmark", Guid.NewGuid().ToString("N"));
        try
        {
            ProcessResult result = await RunBenchmarkAsync("--historical-resume", "0", cache);
            Assert.Equal(0, result.ExitCode);
            using JsonDocument document = JsonDocument.Parse(result.StandardOutput);
            var rows = document.RootElement.GetProperty("results").EnumerateArray().ToArray();
            Assert.Equal(8, rows.Length);
            Assert.Equal(43, rows[0].GetProperty("providerQueries").GetInt32());
            Assert.Equal(6, rows[1].GetProperty("providerQueries").GetInt32());
            for (int index = 0; index < 6; index += 2)
                Assert.Equal(rows[index].GetProperty("selectionDigest").GetString(), rows[index + 1].GetProperty("selectionDigest").GetString());
            JsonElement pending = rows[6].GetProperty("metadata"), resumed = rows[7].GetProperty("metadata");
            Assert.Equal(204, pending.GetProperty("CompletedCount").GetInt32());
            Assert.Equal(236, pending.GetProperty("PendingCount").GetInt32());
            Assert.False(pending.GetProperty("MetadataComplete").GetBoolean());
            Assert.True(pending.GetProperty("InventoryComplete").GetBoolean());
            Assert.Equal(204, resumed.GetProperty("CacheHitCount").GetInt32());
            Assert.Equal(236, resumed.GetProperty("CacheWriteCount").GetInt32());
            Assert.InRange(resumed.GetProperty("BatchCount").GetInt32(), 20, 23);
            Assert.Equal(236, rows[7].GetProperty("metadataRequestedCandidates").GetInt32());
            Assert.True(resumed.GetProperty("MetadataComplete").GetBoolean());
            Assert.Equal(rows[0].GetProperty("selectionDigest").GetString(), rows[7].GetProperty("selectionDigest").GetString());
            Assert.Equal(440, Directory.GetFiles(cache, "*.json").Length);
            Assert.Empty(Directory.GetFiles(cache, "*.tmp-*"));
            Assert.All(rows, row =>
            {
                Assert.InRange(row.GetProperty("peakAdapterConcurrency").GetInt32(), 1, 4);
                Assert.False(row.GetProperty("acquisitionExecuted").GetBoolean());
            });
            Assert.DoesNotContain(cache, result.StandardOutput, StringComparison.OrdinalIgnoreCase);
        }
        finally { if (Directory.Exists(cache)) Directory.Delete(cache, recursive: true); }
    }
}
