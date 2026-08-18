using System.Text;
using EffortHours.Change;

namespace EffortHours.Tests;

public sealed class GitSnapshotDeltaBatchTests
{
    private const string Commit = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Before = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string After = "cccccccccccccccccccccccccccccccccccccccc";
    private const string EmptyFirst = "dddddddddddddddddddddddddddddddddddddddd";
    private const string EmptyMiddle = "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee";
    private const string EmptyLast = "ffffffffffffffffffffffffffffffffffffffff";
    private const string Zero = "0000000000000000000000000000000000000000";

    [Fact]
    public void ParsesNulDelimitedBatchDiffWithoutExposingQuotedPathSemantics()
    {
        byte[] output = Encoding.UTF8.GetBytes(
            $"\nCOMMIT:{Commit}\0" +
            $":100644 100644 {Before} {After} M\0src/Feature.cs\0" +
            $":100644 000000 {Before} {Zero} D\0src/Removed.cs\0");

        IReadOnlyDictionary<string, IReadOnlyList<GitSnapshotDeltaBatch.RawChangedFile>> parsed =
            GitSnapshotDeltaBatch.ParseDiff(output);

        IReadOnlyList<GitSnapshotDeltaBatch.RawChangedFile> files = Assert.Single(parsed).Value;
        Assert.Equal(2, files.Count);
        Assert.Equal("src/Feature.cs", files[0].Path);
        Assert.Equal("100644", files[0].NewMode);
        Assert.Equal(After, files[0].NewObjectId);
        Assert.Equal("src/Removed.cs", files[1].Path);
        Assert.Equal("000000", files[1].NewMode);
        Assert.Equal(Zero, files[1].NewObjectId);
    }

    [Fact]
    public void RejectsUnsafeBatchDiffPaths()
    {
        byte[] output = Encoding.UTF8.GetBytes(
            $"COMMIT:{Commit}\0" +
            $":100644 100644 {Before} {After} M\0../outside.cs\0");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            GitSnapshotDeltaBatch.ParseDiff(output));

        Assert.Contains("unsafe snapshot path", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PreservesExplicitEmptyCommitFramesAtEveryBatchPosition()
    {
        byte[] output = Encoding.UTF8.GetBytes(
            $"COMMIT:{EmptyFirst}\0" +
            $"COMMIT:{Commit}\0" +
            $":100644 100644 {Before} {After} M\0src/Feature.cs\0" +
            $"COMMIT:{EmptyMiddle}\0" +
            $"COMMIT:{EmptyLast}\0");

        IReadOnlyDictionary<string, IReadOnlyList<GitSnapshotDeltaBatch.RawChangedFile>> parsed =
            GitSnapshotDeltaBatch.ParseDiff(output);

        Assert.Equal(4, parsed.Count);
        Assert.Empty(parsed[EmptyFirst]);
        Assert.Single(parsed[Commit]);
        Assert.Empty(parsed[EmptyMiddle]);
        Assert.Empty(parsed[EmptyLast]);
    }

    [Fact]
    public void ParsesBlobLengthsAndRejectsNonBlobObjects()
    {
        IReadOnlyDictionary<string, long> lengths = GitSnapshotDeltaBatch.ParseBlobLengths(
            Encoding.ASCII.GetBytes($"{After} blob 1234\n"));

        Assert.Equal(1234L, lengths[After]);
        Assert.Throws<InvalidOperationException>(() =>
            GitSnapshotDeltaBatch.ParseBlobLengths(
                Encoding.ASCII.GetBytes($"{After} tree 1234\n")));
    }
}
