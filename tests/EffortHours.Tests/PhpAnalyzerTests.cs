using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class PhpAnalyzerTests
{
    private const string Analyzer = "efforthours.php-analyzer";

    [Fact]
    public async Task ComposerPhpFrameworkTemplatesAndTestsProduceTraceableEvidenceAndEffort()
    {
        InMemoryRepository repository = RichRepository();

        RepositoryEvidence evidence = await ScanAsync(repository);
        EstimateReport estimate = new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);
        EvidenceFact source = AnalyzerFact(evidence, EvidenceKinds.SourceStructure, ".");

        Assert.Contains("analysis-status:analyzed", Language(evidence).Tags);
        Assert.Contains("analysis-depth:token-backed", Language(evidence).Tags);
        Assert.Equal("0.1.0", source.Provenance.AnalyzerVersion);
        Assert.True(Measurement(source, "types") >= 3m);
        Assert.True(Measurement(source, "methods") >= 3m);
        Assert.True(Measurement(source, "public-symbols") >= 3m);
        Assert.True(Measurement(source, "imports") >= 8m);
        Assert.True(Measurement(source, "branch-points") >= 1m);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ApiSurface &&
            fact.Provenance.Analyzer == Analyzer && fact.Tags.Contains("technology:laravel"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.DataAccess &&
            fact.Provenance.Analyzer == Analyzer);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.Integration &&
            fact.Provenance.Analyzer == Analyzer && fact.Tags.Contains("technology:guzzle"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.SecurityConfiguration &&
            fact.Provenance.Analyzer == Analyzer);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.BackgroundWork &&
            fact.Provenance.Analyzer == Analyzer);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.Validation &&
            fact.Provenance.Analyzer == Analyzer);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.UserInterface &&
            fact.Provenance.Analyzer == Analyzer && Measurement(fact, "forms") == 1m);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.EcosystemTest &&
            fact.Provenance.Analyzer == Analyzer && fact.Tags.Contains("test-type:integration"));
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ProjectReference &&
            fact.Provenance.Analyzer == Analyzer && fact.Scope == "." &&
            fact.Tags.Contains("target-scope:packages/domain"));
        EvidenceFact build = AnalyzerFact(evidence, EvidenceKinds.BuildConfiguration, ".");
        Assert.Equal(2m, Measurement(build, "autoload-mappings"));
        Assert.Equal(1m, Measurement(build, "path-repositories"));
        Assert.Equal(1m, Measurement(build, "bin-entries"));
        Assert.Contains(estimate.Categories, item => item.Category == EffortCategory.ProductionImplementation);
        Assert.Contains(estimate.Categories, item => item.Category == EffortCategory.UiImplementationAndRepresentedUxDecisions);
        Assert.Contains(estimate.Categories, item => item.Category == EffortCategory.DataModelingPersistenceAndMigrations);
        Assert.Contains(estimate.Categories, item => item.Category == EffortCategory.ExternalIntegrationsAndProtocols);
        Assert.Contains(estimate.Categories, item => item.Category == EffortCategory.SecurityAndAccessibility);
        Assert.Contains(estimate.Categories, item => item.Category == EffortCategory.IntegrationContractAndComponentTesting);
        Assert.DoesNotContain(estimate.Diagnostics, diagnostic => diagnostic.Code is "FB1001" or "FB1002");
        Assert.Empty(ContractValidation.Validate(evidence));
        Assert.Empty(ContractValidation.Validate(estimate));
    }

    [Fact]
    public async Task FrameworkNamesWithoutQualifiedImportsDoNotCreateSemanticEvidence()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "app.php",
            "<?php\nclass Route { public static function get($path) {} }\nRoute::get('/local');\n");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.DoesNotContain(evidence.Facts, fact => fact.Kind == EvidenceKinds.ApiSurface &&
            fact.Provenance.Analyzer == Analyzer);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure &&
            fact.Provenance.Analyzer == Analyzer);
    }

    [Fact]
    public async Task ExactDuplicatePhpBodiesDoNotIncreaseEstimatedEffort()
    {
        const string source = "<?php\nnamespace App;\nclass Service { public function run(): bool { return true; } }\n";
        EstimateReport single = await EstimateAsync(("src/Service.php", source));
        EstimateReport duplicate = await EstimateAsync(("src/Service.php", source), ("src/Copy.php", source));

        Assert.Equal(single.TotalEffort.Expected, duplicate.TotalEffort.Expected);
        Assert.Equal(
            Category(single, EffortCategory.ProductionImplementation).Expected,
            Category(duplicate, EffortCategory.ProductionImplementation).Expected);
        Assert.Contains(duplicate.Diagnostics, diagnostic => diagnostic.Code == "FB1003");
    }

    [Fact]
    public async Task NestedComposerPackagesUseDeepestOwnershipAndLocalReferences()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "composer.json",
            """
            {"name":"acme/app","require":{"acme/domain":"*"},"repositories":[{"type":"path","url":"packages/*"}],"autoload":{"psr-4":{"App\\":"src/"}}}
            """);
        repository.WriteText(
            "packages/domain/composer.json",
            """
            {"name":"acme/domain","autoload":{"psr-4":{"Domain\\":"src/"}}}
            """);
        repository.WriteText("src/UseDomain.php", "<?php\nnamespace App;\nuse Domain\\Order;\nclass UseDomain { public function load(): Order {} }\n");
        repository.WriteText("packages/domain/src/Order.php", "<?php\nnamespace Domain;\nclass Order {}\n");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.SourceStructure &&
            fact.Scope == "packages/domain" && fact.Provenance.Analyzer == Analyzer);
        Assert.Contains(evidence.Facts, fact => fact.Kind == EvidenceKinds.ProjectReference &&
            fact.Scope == "." && fact.Tags.Contains("target-scope:packages/domain"));
    }

    [Fact]
    public async Task MixedPhpFrontendAndSqlRepositoryKeepsAnalyzerOwnershipSeparate()
    {
        InMemoryRepository repository = new();
        repository.WriteText("composer.json", "{\"name\":\"acme/mixed\",\"autoload\":{\"psr-4\":{\"App\\\\\":\"src/\"}}}");
        repository.WriteText(
            "src/Order.php",
            "<?php\nnamespace App;\nfinal class Order { public function id(): int { return 1; } }\n");
        repository.WriteText("resources/views/order.blade.php", "<article>{{ $order }}</article>\n");
        repository.WriteText("web/order.js", "export function showOrder(order) { return order.id; }\n");
        repository.WriteText("database/schema.sql", "create table orders (id integer primary key);\n");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == Analyzer &&
            fact.Locations.Any(location => location.Path == "src/Order.php"));
        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == "efforthours.javascript-analyzer" &&
            fact.Locations.Any(location => location.Path == "web/order.js"));
        Assert.Contains(evidence.Facts, fact => fact.Provenance.Analyzer == "efforthours.sql-analyzer" &&
            fact.Locations.Any(location => location.Path == "database/schema.sql"));
        Assert.DoesNotContain(evidence.Facts.Where(fact => fact.Provenance.Analyzer == Analyzer)
            .SelectMany(fact => fact.Locations), location =>
                location.Path is "web/order.js" or "database/schema.sql");
        Assert.Empty(ContractValidation.Validate(evidence));
    }

    private static InMemoryRepository RichRepository()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "composer.json",
            """
            {
              "name":"acme/store",
              "type":"project",
              "require":{
                "laravel/framework":"^12.0",
                "guzzlehttp/guzzle":"^7.0",
                "acme/domain":"*"
              },
              "require-dev":{"phpunit/phpunit":"^12.0","pestphp/pest":"^4.0"},
              "autoload":{"psr-4":{"App\\":"app/"},"files":["app/helpers.php"]},
              "autoload-dev":{"psr-4":{"Tests\\":"tests/"}},
              "scripts":{"test":"phpunit"},
              "bin":["bin/store"],
              "repositories":[{"type":"path","url":"packages/*"}]
            }
            """);
        repository.WriteText(
            "packages/domain/composer.json",
            """
            {"name":"acme/domain","autoload":{"psr-4":{"Domain\\":"src/"}}}
            """);
        repository.WriteText("packages/domain/src/Order.php", "<?php\nnamespace Domain;\nfinal class Order {}\n");
        repository.WriteText(
            "routes/api.php",
            """
            <?php
            use Illuminate\Support\Facades\Route;
            use Domain\Order;
            Route::get('/orders', function (): Order { return new Order(); });
            """);
        repository.WriteText(
            "app/Models/User.php",
            """
            <?php
            namespace App\Models;
            use Illuminate\Database\Eloquent\Model;
            final class User extends Model {
                public function active(): bool { return $this->enabled && !$this->blocked; }
            }
            """);
        repository.WriteText(
            "app/Services/Checkout.php",
            """
            <?php
            namespace App\Services;
            use GuzzleHttp\Client;
            use Illuminate\Support\Facades\Hash;
            use Illuminate\Support\Facades\Validator;
            final class Checkout {
                public function run(array $input): void {
                    $client = new Client();
                    $client->request('POST', '/payments');
                    Hash::make('secret');
                    Validator::make($input, []);
                }
            }
            """);
        repository.WriteText(
            "app/Jobs/SendReceipt.php",
            """
            <?php
            namespace App\Jobs;
            use Illuminate\Contracts\Queue\ShouldQueue;
            final class SendReceipt implements ShouldQueue { public function handle(): void {} }
            """);
        repository.WriteText(
            "resources/views/orders.blade.php",
            """
            <x-layout>
              @if($orders)
                <form method="post"><input name="query">{{ $orders }}</form>
              @endif
            </x-layout>
            """);
        repository.WriteText(
            "tests/Feature/OrdersTest.php",
            """
            <?php
            namespace Tests\Feature;
            use PHPUnit\Framework\TestCase;
            final class OrdersTest extends TestCase {
                public function testOrders(): void { self::assertTrue(true); }
            }
            """);
        return repository;
    }

    private static Task<RepositoryEvidence> ScanAsync(InMemoryRepository repository) =>
        new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);

    private static async Task<EstimateReport> EstimateAsync(params (string Path, string Content)[] files)
    {
        InMemoryRepository repository = new();
        foreach ((string path, string content) in files) repository.WriteText(path, content);
        return new SeedEstimator().Estimate(
            await ScanAsync(repository),
            EstimationProfile.Implementation);
    }

    private static EvidenceFact AnalyzerFact(RepositoryEvidence evidence, string kind, string scope) =>
        Assert.Single(evidence.Facts, fact => fact.Kind == kind && fact.Scope == scope &&
            fact.Provenance.Analyzer == Analyzer);

    private static EvidenceFact Language(RepositoryEvidence evidence) =>
        Assert.Single(evidence.Facts, fact => fact.Kind == EvidenceKinds.Language &&
            fact.Tags.Contains("language:php", StringComparer.Ordinal));

    private static decimal Measurement(EvidenceFact fact, string name) =>
        Assert.Single(fact.Measurements, measurement => measurement.Name == name).Value;

    private static EffortRange Category(EstimateReport report, EffortCategory category) =>
        Assert.Single(report.Categories, item => item.Category == category).Hours;
}
