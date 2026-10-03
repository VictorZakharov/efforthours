using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed partial class ChangeMarginalityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParsedExistingCapabilityRetainsLargeExpansionWithoutChargingDuplicateCopies(bool tests)
    {
        (string Path, string Content)[] initial = ExpansionFiles(tests, 1);
        (string Path, string Content)[] expanded = ExpansionFiles(tests, 66);
        ChangeEstimateReport small = await EstimateAsync(State(initial), State(ExpansionFiles(tests, 2)));
        ChangeEstimateReport large = await EstimateAsync(State(initial), State(expanded));
        EffortCategory category = tests ? EffortCategory.IntegrationContractAndComponentTesting : EffortCategory.ProductionImplementation;
        decimal Expected(ChangeEstimateReport report) => report.Categories.Where(item => item.Category == category).Sum(item => item.Hours.Expected);
        Assert.True(Expected(large) > 8m, $"Supported expansion was capped: {Expected(large)}");
        Assert.True(Expected(large) > Expected(small));
        Assert.True(Expected(large) < 65m * Expected(small));
        Assert.All(large.WorkItems, item => Assert.InRange(item.Hours.Expected, 0.01m, 1.5m));
        ChangeEstimateReport copies = await EstimateAsync(State(initial), State([.. expanded,
            .. expanded.Skip(2).Where(file => file.Path.EndsWith(".cs", StringComparison.Ordinal)).Select(file =>
                (file.Path[..file.Path.LastIndexOf('/')] + "/copies/" + file.Path[(file.Path.LastIndexOf('/') + 1)..], file.Content))]));
        Assert.Equal(large.TotalEffort, copies.TotalEffort);
        Assert.Equal(large.Categories.Select(CategoryHours), copies.Categories.Select(CategoryHours));
    }

    private static (string Path, string Content)[] ExpansionFiles(bool tests, int count)
    {
        string project = tests
            ? "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><IsTestProject>true</IsTestProject></PropertyGroup><ItemGroup><PackageReference Include=\"xunit\" Version=\"2.9.3\" /></ItemGroup></Project>"
            : "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>";
        string root = tests ? "integration-tests" : "src";
        return [(root + "/Feature.csproj", project), .. Enumerable.Range(0, count).Select(index =>
            (root + "/Feature" + index + ".cs", tests
                ? "using Xunit; namespace Demo; public class Feature" + index + " { private TestServer server = new(); " + string.Join(" ", Enumerable.Range(0, 8).Select(test =>
                    "[Fact] public void Case" + test + "() { Assert.Equal(" + (index * 8 + test) + ", Service.Read(" + index + ", " + test + ")); }")) + " }"
                : "namespace Demo; public class Feature" + index + " { " + string.Join(" ", Enumerable.Range(0, 8).Select(method =>
                    "public int Read" + method + "(int value) { if (value > " + index + ") return value + " + method + "; return value - " + index + "; }")) + " }"))];
    }
}
