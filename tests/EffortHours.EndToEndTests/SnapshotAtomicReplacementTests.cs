using System.Text;
using EffortHours.Change;

namespace EffortHours.EndToEndTests;

public sealed partial class SnapshotPortfolioCliTests
{
    [Fact]
    public async Task SnapshotAtomicReplacementRetainsCompleteFilesUntilHeldReaderReleases()
    {
        using GitFixture execution = await GitFixture.CreateAsync();
        string destination = Path.Combine(execution.RootPath, "published.json");
        string staged = destination + ".staged.tmp";
        byte[] original = Encoding.UTF8.GetBytes("original complete publication");
        byte[] replacement = Encoding.UTF8.GetBytes("replacement complete publication");
        await File.WriteAllBytesAsync(destination, original);
        await File.WriteAllBytesAsync(staged, replacement);
        using FileStream reader = new(destination, FileMode.Open, FileAccess.Read, FileShare.Read);
        int retries = 0;
        await SnapshotAtomicReplacement.ExecuteAsync(() => File.Move(staged, destination, overwrite: true), (_, _) =>
        {
            retries++;
            Assert.Equal(original, File.ReadAllBytes(destination));
            Assert.Equal(replacement, File.ReadAllBytes(staged));
            reader.Dispose();
            return Task.CompletedTask;
        }, OperatingSystem.IsWindows(), CancellationToken.None);
        if (OperatingSystem.IsWindows()) Assert.InRange(retries, 1, SnapshotAtomicReplacement.MaximumAttempts - 1);
        else Assert.Equal(0, retries);
        Assert.Equal(replacement, await File.ReadAllBytesAsync(destination));
        Assert.False(File.Exists(staged));
        if (!OperatingSystem.IsWindows())
        {
            byte[] stillOriginal = new byte[original.Length];
            reader.ReadExactly(stillOriginal);
            Assert.Equal(original, stillOriginal);
        }
    }

    [Theory]
    [InlineData("held-reader")]
    [InlineData("read-only")]
    public async Task SnapshotAtomicWritePreservesPublicationAndCleansStagingOnPermanentWindowsDenial(string denial)
    {
        if (!OperatingSystem.IsWindows()) return; // Windows sharing and read-only replacement rules are OS-specific.
        using GitFixture execution = await GitFixture.CreateAsync();
        string destination = Path.Combine(execution.RootPath, "published.json");
        byte[] original = Encoding.UTF8.GetBytes("original complete publication");
        await File.WriteAllBytesAsync(destination, original);
        using FileStream? reader = denial == "held-reader"
            ? new(destination, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
        if (denial == "read-only") File.SetAttributes(destination, FileAttributes.ReadOnly);
        try
        {
            Exception? failure = await Record.ExceptionAsync(() => SnapshotPortfolioStore.AtomicWriteAsync(
                destination, "replacement complete publication", CancellationToken.None));
            Assert.True(failure is IOException or UnauthorizedAccessException, failure?.ToString() ?? "Replacement unexpectedly succeeded.");
            Assert.Equal(original, await File.ReadAllBytesAsync(destination));
            Assert.Empty(Directory.EnumerateFiles(execution.RootPath, "*.tmp"));
            if (denial == "read-only") Assert.True(File.GetAttributes(destination).HasFlag(FileAttributes.ReadOnly));
        }
        finally
        {
            if (denial == "read-only") File.SetAttributes(destination, FileAttributes.Normal);
        }
    }
}
