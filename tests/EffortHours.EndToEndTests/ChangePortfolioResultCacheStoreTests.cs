using EffortHours.Change;
using EffortHours.Cli;

namespace EffortHours.EndToEndTests;

public sealed class ChangePortfolioResultCacheStoreTests
{
    [Fact]
    public async Task TrimEvictsLeastRecentlyUsedEntriesBeyondCountAndByteBounds()
    {
        string directory = Path.Combine(Path.GetTempPath(), "eh-result-cache-" + Guid.NewGuid().ToString("N"));
        try
        {
            ChangePortfolioResultCacheStore store = new(directory, maximumEntries: 2);
            IChangePortfolioResultCache cache = store.ForContext("context");
            string[] keys = [Key('1'), Key('2'), Key('3')];
            foreach (string key in keys)
                await cache.StoreAsync(key, Rejection(key), CancellationToken.None);
            Assert.NotNull(await cache.TryLoadAsync(keys[0], CancellationToken.None)); // most recent now

            ChangePortfolioResultCacheTrim trim = store.Trim(CancellationToken.None);

            Assert.Equal((2, 1), (trim.Entries, trim.Evicted));
            Assert.NotNull(await cache.TryLoadAsync(keys[0], CancellationToken.None));
            Assert.Null(await cache.TryLoadAsync(keys[1], CancellationToken.None));
            Assert.NotNull(await cache.TryLoadAsync(keys[2], CancellationToken.None));
            Assert.Equal((3, 0), (store.Writes, store.WriteFailures));

            ChangePortfolioResultCacheStore tiny = new(directory, maximumBytes: trim.Bytes / 2);
            Assert.Equal((1, 1), (tiny.Trim(CancellationToken.None).Entries, tiny.Trim(CancellationToken.None).Entries));
            Assert.NotNull(await tiny.ForContext("context").TryLoadAsync(keys[2], CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task EntriesAreBoundToTheirKey()
    {
        string directory = Path.Combine(Path.GetTempPath(), "eh-result-cache-" + Guid.NewGuid().ToString("N"));
        try
        {
            ChangePortfolioResultCacheStore store = new(directory);
            IChangePortfolioResultCache cache = store.ForContext("context");
            await cache.StoreAsync(Key('a'), Rejection(Key('a')), CancellationToken.None);
            string source = Directory.EnumerateFiles(store.Root, "*.json.gz", SearchOption.AllDirectories).Single();
            string target = Path.Combine(store.Root, "bb", new string('b', 64) + ".json.gz");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target);

            Assert.Null(await cache.TryLoadAsync(Key('b'), CancellationToken.None));
            Assert.Equal(1, store.Rejected);
            Assert.NotNull(await cache.TryLoadAsync(Key('a'), CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => cache.TryLoadAsync("../escape", CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static string Key(char value) => "sha256:" + new string(value, 64);

    private static ChangePortfolioCachedResult Rejection(string digest) => new()
    {
        FinalDeltaRejection = new ChangePortfolioFinalDeltaRejection
        {
            Code = "composition-unproven",
            InputDigest = digest,
        },
    };
}
