using EffortHours.Change;
using EffortHours.Contracts.V1;

namespace EffortHours.Tests;

public sealed class PhpChangeTests
{
    [Fact]
    public async Task WhitespaceAndOrdinaryCommentOnlyPhpChangeHasZeroEffort()
    {
        const string before = "<?php\n// old note\nfinal class Service { public function run(): bool { return true; } }\n";
        const string after = "<?php\n# new note\nfinal  class Service\n{\n    public function run( ) : bool\n    {\n        return true ;\n    }\n}\n";

        ChangeEstimateReport report = await EstimateAsync(
            State(("src/Service.php", before)),
            State(("src/Service.php", after)));

        Assert.Equal(ChangePathClassification.FormattingOnly, Assert.Single(report.Evidence.Paths).Classification);
        Assert.Equal(0m, report.TotalEffort.Expected);
    }

    [Theory]
    [InlineData("<?php\nreturn 'before';\n", "<?php\nreturn 'after';\n")]
    [InlineData(
        "<?php\n/** Before contract. */\nfunction run(): void {}\n",
        "<?php\n/** After contract. */\nfunction run(): void {}\n")]
    [InlineData(
        "<?php\n$value = <<<TXT\nbefore\nTXT;\n",
        "<?php\n$value = <<<TXT\nafter\nTXT;\n")]
    public async Task LiteralsPhpDocAndHeredocChangesRemainMeaningful(string before, string after)
    {
        ChangeEstimateReport report = await EstimateAsync(
            State(("src/Value.php", before)),
            State(("src/Value.php", after)));

        Assert.True(Assert.Single(report.Evidence.Paths).Represented);
        Assert.True(report.TotalEffort.Expected > 0m);
    }

    [Fact]
    public async Task UnterminatedPhpLiteralFailsClosed()
    {
        ChangeEstimateReport report = await EstimateAsync(
            State(("src/Value.php", "<?php\n$value = 'one\n")),
            State(("src/Value.php", "<?php\n  $value = 'one\n")));

        Assert.True(Assert.Single(report.Evidence.Paths).Represented);
        Assert.True(report.TotalEffort.Expected > 0m);
    }

    [Fact]
    public async Task AddedComposerPhpSurfacesReachSemanticCategories()
    {
        ChangeEstimateReport report = await EstimateAsync(
            State(),
            State(
                ("composer.json", "{\"name\":\"acme/app\",\"require\":{\"laravel/framework\":\"^12.0\",\"phpunit/phpunit\":\"^12.0\"}}"),
                ("routes/api.php", "<?php\nuse Illuminate\\Support\\Facades\\Route;\nRoute::get('/health', fn () => true);\n"),
                ("database/migrations/CreateUsers.php", "<?php\nuse Illuminate\\Database\\Migrations\\Migration;\nuse Illuminate\\Support\\Facades\\Schema;\nfinal class CreateUsers extends Migration { public function up(): void { Schema::create('users', function ($table) {}); } }\n"),
                ("resources/views/health.blade.php", "<form>{{ $status }}</form>\n"),
                ("tests/Feature/HealthTest.php", "<?php\nuse PHPUnit\\Framework\\TestCase;\nfinal class HealthTest extends TestCase { public function testHealth(): void { self::assertTrue(true); } }\n")));

        Assert.Contains(report.Categories, item =>
            item.Category == EffortCategory.ProductionImplementation && item.Hours.Expected > 0m);
        Assert.Contains(report.Categories, item =>
            item.Category == EffortCategory.DataModelingPersistenceAndMigrations && item.Hours.Expected > 0m);
        Assert.Contains(report.Categories, item =>
            item.Category == EffortCategory.UiImplementationAndRepresentedUxDecisions && item.Hours.Expected > 0m);
        Assert.Contains(report.Categories, item =>
            item.Category == EffortCategory.IntegrationContractAndComponentTesting && item.Hours.Expected > 0m);
        Assert.Equal("change-seed/0.14.0+seed-rules/0.4.0", report.EstimatorVersion);
    }

    private static Task<ChangeEstimateReport> EstimateAsync(ChangeState before, ChangeState after) =>
        new ChangeEstimator().EstimateAsync(
            new ChangeEstimateInput
            {
                RepositoryName = "in-memory-php-change",
                Selection = new ChangeSelection
                {
                    Kind = ChangeSelectionKind.BaseHead,
                    Base = Reference("base", before.ObjectId),
                    Head = Reference("head", after.ObjectId),
                },
                OpenBaseAsync = before.OpenAsync,
                OpenHeadAsync = after.OpenAsync,
            },
            EstimationProfile.Implementation);

    private static ChangeSnapshotReference Reference(string selector, string objectId) => new()
    {
        Selector = selector,
        ObjectId = objectId,
        Kind = ChangeSnapshotKind.GitTree,
    };

    private static ChangeState State(params (string Path, string Content)[] files)
    {
        InMemoryChangeSnapshot snapshot = new(files);
        return new ChangeState(snapshot.ObjectId, InMemoryChangeSnapshot.Factory(files));
    }

    private sealed record ChangeState(
        string ObjectId,
        Func<CancellationToken, Task<IChangeSnapshot>> OpenAsync);
}
