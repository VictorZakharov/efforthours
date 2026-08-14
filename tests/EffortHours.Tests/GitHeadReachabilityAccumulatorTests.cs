using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class GitHeadReachabilityAccumulatorTests
{
    [Fact]
    public void OneTopologicalWalkMapsSharedAndUniqueCommitsToEveryHead()
    {
        string firstHead = Id('a');
        string firstUnique = Id('b');
        string secondHead = Id('c');
        string secondUnique = Id('d');
        string shared = Id('e');
        string root = Id('f');
        GitHeadReachabilityAccumulator accumulator = new(
        [
            new ChangeAuthorPeriodManifestHead { Id = "open", ObjectId = secondHead },
            new ChangeAuthorPeriodManifestHead { Id = "default", ObjectId = firstHead },
        ],
        [shared, secondUnique, firstUnique]);

        accumulator.Consume($"{firstHead} {firstUnique}");
        accumulator.Consume($"{firstUnique} {shared}");
        accumulator.Consume($"{secondHead} {secondUnique}");
        accumulator.Consume($"{secondUnique} {shared}");
        accumulator.Consume($"{shared} {root}");

        IReadOnlyDictionary<string, IReadOnlyList<string>> result = accumulator.Result();
        Assert.Equal(["default"], result[firstUnique]);
        Assert.Equal(["open"], result[secondUnique]);
        Assert.Equal(["default", "open"], result[shared]);
    }

    [Fact]
    public async Task ReachabilityTraversalHonorsCancellationBeforeStartingGit()
    {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new GitHeadReachabilityResolver().ResolveAsync(
                "must-not-be-opened",
                [new ChangeAuthorPeriodManifestHead { Id = "default", ObjectId = Id('a') }],
                [Id('a')],
                cancellation.Token));
    }

    private static string Id(char value) => new(value, 40);
}
