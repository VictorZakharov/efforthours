using EffortHours.Analysis;

namespace EffortHours.Tests;

public sealed class IgnoreRuleTests
{
    [Theory]
    [InlineData("*.tmp", "", "scratch.tmp", false, true)]
    [InlineData("*.tmp", "", "src/scratch.tmp", false, true)]
    [InlineData("*.tmp", "", "src/scratch.tmp/child", false, false)]
    [InlineData("/root.txt", "", "root.txt", false, true)]
    [InlineData("/root.txt", "", "nested/root.txt", false, false)]
    [InlineData("src/*.cs", "", "src/app.cs", false, true)]
    [InlineData("src/*.cs", "", "src/nested/app.cs", false, false)]
    [InlineData("src/**/generated?.[Cc][Ss]", "", "src/generated1.cs", false, true)]
    [InlineData("src/**/generated?.[Cc][Ss]", "", "src/a/b/generatedX.CS", false, true)]
    [InlineData("[!a-c].txt", "", "d.txt", false, true)]
    [InlineData("[!a-c].txt", "", "b.txt", false, false)]
    [InlineData("file\\?.txt", "", "file?.txt", false, true)]
    [InlineData("private/", "src/web", "src/web/private", true, true)]
    [InlineData("private/", "src/web", "src/web/private", false, false)]
    [InlineData("private/", "src/web", "src/private", true, false)]
    public void SupportedGlobSubsetMatchesDeterministically(
        string source,
        string basePath,
        string path,
        bool isDirectory,
        bool expected)
    {
        Assert.True(IgnoreRule.TryCreate(source, basePath, out IgnoreRule? rule));

        Assert.Equal(expected, rule!.Matches(path, isDirectory));
    }

    [Fact]
    public void InvalidAndOversizedPatternsFailWithBoundedReasons()
    {
        Assert.False(IgnoreRule.TryCreate(
            "[z-a]",
            string.Empty,
            out IgnoreRule? invalid,
            out IgnoreRuleCreationFailure invalidFailure));
        Assert.Null(invalid);
        Assert.Equal(IgnoreRuleCreationFailure.InvalidCharacterClass, invalidFailure);

        Assert.False(IgnoreRule.TryCreate(
            new string('x', IgnoreGlobPattern.MaximumPatternCharacters + 1),
            string.Empty,
            out IgnoreRule? oversized,
            out IgnoreRuleCreationFailure oversizedFailure));
        Assert.Null(oversized);
        Assert.Equal(IgnoreRuleCreationFailure.PatternTooLong, oversizedFailure);
    }

    [Fact]
    public async Task IdenticalConcurrentRuleWorkIsStableAcrossFiftyRepetitions()
    {
        string[] patterns =
        [
            "*.tmp",
            "src/**/generated?.[Cc][Ss]",
            "[!a-c].txt",
            "!keep.tmp",
            "docs/",
        ];
        string[] paths =
        [
            "scratch.tmp",
            "src/a/b/generatedX.CS",
            "d.txt",
            "keep.tmp",
            "docs",
            "src/product.cs",
        ];
        string expected = Evaluate(patterns, paths);

        string[] actual = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ =>
            Task.Run(() => Evaluate(patterns, paths))));

        Assert.All(actual, value => Assert.Equal(expected, value));
    }

    private static string Evaluate(IReadOnlyList<string> patterns, IReadOnlyList<string> paths)
    {
        IgnoreRule[] rules = [.. patterns.Select(pattern =>
        {
            Assert.True(IgnoreRule.TryCreate(pattern, string.Empty, out IgnoreRule? rule));
            return rule!;
        })];
        return string.Join(
            '|',
            paths.Select(path => $"{path}:{IgnoreRule.IsIgnored(rules, path, path == "docs")}"));
    }
}
