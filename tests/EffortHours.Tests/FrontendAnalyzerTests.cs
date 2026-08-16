using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Tests;

public sealed class FrontendAnalyzerTests
{
    [Fact]
    public async Task AngularStaticMetadataMapsInlineAndExternalSemanticsWithoutSourceDisclosure()
    {
        InMemoryRepository repository = AngularRepository();

        RepositoryEvidence evidence = await ScanAsync(repository);
        string json = ContractJson.Serialize(evidence);

        EvidenceFact component = SingleFact(evidence, "javascript:angular-component:");
        Assert.Equal("0.5.2", component.Provenance.AnalyzerVersion);
        Assert.Equal(1m, Measurement(component, "components"));
        Assert.True(Measurement(component, "template-structure-units") > 0m);
        Assert.True(Measurement(component, "template-binding-units") > 0m);
        Assert.True(Measurement(component, "responsive-surfaces") > 0m);
        Assert.Contains("technology:angular", component.Tags);
        Assert.Contains("angular-metadata:static-only", component.Tags);

        EvidenceFact template = Fact(evidence, "javascript:ui-asset:src/card.html");
        Assert.Equal("src/card.html", template.Locations[0].Path);
        Assert.Contains(template.Locations, location => location.Path == "src/card.component.ts");
        Assert.Contains("asset-ownership:angular-component", template.Tags);
        Assert.True(Measurement(template, "forms") > 0m);
        Assert.True(Measurement(template, "bindings") > 0m);
        Assert.True(Measurement(template, "directives") > 0m);

        EvidenceFact styles = Fact(evidence, "javascript:ui-asset:src/card.scss");
        Assert.Equal("src/card.scss", styles.Locations[0].Path);
        Assert.True(Measurement(styles, "design-token-units") > 0m);
        Assert.True(Measurement(styles, "responsive-surfaces") > 0m);
        Assert.True(Measurement(styles, "animation-theme-surfaces") > 0m);
        Assert.DoesNotContain("private-template-marker", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-style-marker", json, StringComparison.Ordinal);
        Assert.Empty(ContractValidation.Validate(evidence));
    }

    [Fact]
    public async Task StandaloneHtmlAndEveryCssFamilyProduceBoundedSemanticEvidence()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "index.html",
            "<main><form><input required><custom-card></custom-card></form></main>");
        repository.WriteText(
            "styles.css",
            ":root { --space: 1rem; } @media (width > 40rem) { .grid, .list { display: grid; } }");
        repository.WriteText("theme.scss", "$accent: red; [data-theme='dark'] { color: $accent; }");
        repository.WriteText("tokens.less", "@accent: red; .button { transition: color .2s; }");
        repository.WriteText("layout.sass", "$gap: 1rem\n.grid\n  display: grid\n");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.Contains("javascript", evidence.Repository.Ecosystems);
        Assert.Equal(5, evidence.Facts.Count(fact => fact.Id.StartsWith(
            "javascript:ui-asset:",
            StringComparison.Ordinal)));
        Assert.True(Measurement(Fact(evidence, "javascript:ui-asset:index.html"), "template-structure-units") > 0m);
        Assert.True(Measurement(Fact(evidence, "javascript:ui-asset:styles.css"), "responsive-surfaces") > 0m);
        Assert.True(Measurement(Fact(evidence, "javascript:ui-asset:theme.scss"), "design-token-units") > 0m);
        Assert.True(Measurement(Fact(evidence, "javascript:ui-asset:tokens.less"), "animation-theme-surfaces") > 0m);
        Assert.True(Measurement(Fact(evidence, "javascript:ui-asset:layout.sass"), "style-structure-units") > 0m);
    }

    [Fact]
    public async Task FrameworkDependenciesAndDecoratorNamesAloneDoNotInventUi()
    {
        InMemoryRepository dependencyOnly = new();
        dependencyOnly.WriteText(
            "package.json",
            "{ \"dependencies\": { \"react\": \"19.0.0\" } }");
        dependencyOnly.WriteText("src/math.ts", "export const add = (a: number, b: number) => a + b;");
        RepositoryEvidence dependencyEvidence = await ScanAsync(dependencyOnly);
        Assert.DoesNotContain(dependencyEvidence.Facts, fact => fact.Kind == EvidenceKinds.UserInterface);

        InMemoryRepository decoratorOnly = new();
        decoratorOnly.WriteText(
            "package.json",
            "{ \"dependencies\": { \"@angular/core\": \"21.0.0\" } }");
        decoratorOnly.WriteText("src/model.ts", "@Component({ value: 1 }) export class DomainComponent {}");
        RepositoryEvidence decoratorEvidence = await ScanAsync(decoratorOnly);
        Assert.DoesNotContain(decoratorEvidence.Facts, fact => fact.Kind == EvidenceKinds.UserInterface);

        InMemoryRepository explicitSyntax = new();
        explicitSyntax.WriteText(
            "package.json",
            "{ \"dependencies\": { \"react\": \"19.0.0\" } }");
        explicitSyntax.WriteText("src/card.jsx", "export const Card = () => <article>Card</article>;");
        explicitSyntax.WriteText("src/badge.tsx", "export const Badge = () => <strong>Badge</strong>;");
        explicitSyntax.WriteText("src/counter.vue", "<script setup>const count = 1;</script><template>{{ count }}</template>");
        explicitSyntax.WriteText("src/status.svelte", "<strong>Status</strong>");
        RepositoryEvidence syntaxEvidence = await ScanAsync(explicitSyntax);

        Assert.Contains(syntaxEvidence.Facts, fact => fact.Id == "javascript:ui:src/card.jsx" &&
            fact.Tags.Contains("syntax:acornima-jsx-ast", StringComparer.Ordinal) &&
            fact.Tags.Contains("technology:react", StringComparer.Ordinal));
        Assert.Contains(syntaxEvidence.Facts, fact => fact.Id == "javascript:ui:src/badge.tsx" &&
            fact.Tags.Contains("syntax:typescript-token-stream", StringComparer.Ordinal) &&
            fact.Tags.Contains("technology:react", StringComparer.Ordinal));
        Assert.Contains(Fact(syntaxEvidence, "javascript:ui:src/counter.vue").Tags, tag => tag == "technology:vue");
        Assert.Contains(Fact(syntaxEvidence, "javascript:ui:src/status.svelte").Tags, tag => tag == "technology:svelte");
    }

    [Fact]
    public async Task AngularDynamicMetadataAndEscapingAssetReferencesFailClosed()
    {
        InMemoryRepository repository = new();
        repository.WriteText("outside.html", "<main>Generic asset</main>");
        repository.WriteText(
            "src/guard.component.ts",
            """
            import { Component as AngularComponent } from '@angular/core';
            @AngularComponent({
              selector: 'app-guard',
              template: buildTemplate(),
              templateUrl: '../../outside.html',
              styles: buildStyles()
            })
            export class GuardComponent {}
            """);

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact component = SingleFact(evidence, "javascript:angular-component:");
        EvidenceFact outside = Fact(evidence, "javascript:ui-asset:outside.html");

        Assert.Equal(2m, Measurement(component, "dynamic-metadata-properties"));
        Assert.Equal(1m, Measurement(component, "unresolved-asset-references"));
        Assert.Equal(0m, Measurement(component, "template-structure-units"));
        Assert.Contains("angular-metadata:dynamic-values-excluded", component.Tags);
        Assert.Contains("asset-ownership:package-or-generic", outside.Tags);
        Assert.DoesNotContain(outside.Tags, tag => tag.StartsWith("component-owner:", StringComparison.Ordinal));
        Assert.Contains(evidence.Diagnostics, diagnostic => diagnostic.Code == "FB4202");
    }

    [Fact]
    public async Task OnlyMaintainedProductionFrontendAssetsAreAnalyzed()
    {
        InMemoryRepository repository = new();
        repository.WriteText("package.json", "{ \"name\": \"asset-boundaries\" }");
        repository.WriteText("src/page.html", "<main>Product</main>");
        repository.WriteText("tests/form.html", "<form><input></form>");
        repository.WriteText("docs/example.html", "<form><input></form>");
        repository.WriteText("src/page.generated.html", "<!-- generated by fixture --><form></form>");
        repository.WriteText("public/site.min.css", ".a{color:red}.b{color:blue}");
        repository.WriteText("vendor/theme.css", ".theme { color: red; }");
        repository.WriteText("dist/bundle.css", ".bundle { animation: spin 1s; }");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact[] semanticAssets = [.. evidence.Facts.Where(fact =>
            fact.Id.StartsWith("javascript:ui-asset:", StringComparison.Ordinal))];

        Assert.Single(semanticAssets);
        Assert.Equal("javascript:ui-asset:src/page.html", semanticAssets[0].Id);
    }

    [Fact]
    public async Task SharedAngularAssetRetainsGenericOwnershipWithoutDoubleCounting()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "package.json",
            "{ \"dependencies\": { \"@angular/core\": \"21.0.0\" } }");
        repository.WriteText("src/shared.css", ".shared { display: grid; }");
        repository.WriteText(
            "src/a.ts",
            "import { Component } from '@angular/core'; @Component({styleUrl:'./shared.css'}) export class A {}");
        repository.WriteText(
            "src/b.ts",
            "import { Component } from '@angular/core'; @Component({styleUrl:'./shared.css'}) export class B {}");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact asset = Fact(evidence, "javascript:ui-asset:src/shared.css");

        Assert.Contains("asset-ownership:ambiguous-shared", asset.Tags);
        Assert.DoesNotContain(asset.Tags, tag => tag.StartsWith("component-owner:", StringComparison.Ordinal));
        Assert.Equal("src/shared.css", asset.Locations[0].Path);
        Assert.Equal(3, asset.Locations.Count);
    }

    [Fact]
    public async Task ExplicitAccessibilityMarkupProducesBoundedEvidenceAndRepresentedEffort()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "index.html",
            "<main role='main'><label for='name'>Name</label><input id='name' aria-describedby='help'><img alt='Account'><button tabindex='0' (keydown.enter)='save()'>Save</button><output aria-live='polite'>private-a11y-marker</output></main>");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact accessibility = Fact(evidence, "javascript:accessibility:index.html");
        string json = ContractJson.Serialize(evidence);

        Assert.Equal("0.5.2", accessibility.Provenance.AnalyzerVersion);
        Assert.True(Measurement(accessibility, "accessibility-attributes") >= 3m);
        Assert.True(Measurement(accessibility, "labels") >= 2m);
        Assert.Equal(1m, Measurement(accessibility, "alternative-texts"));
        Assert.Equal(1m, Measurement(accessibility, "keyboard-interactions"));
        Assert.Equal(1m, Measurement(accessibility, "live-regions"));
        Assert.Equal(1m, Measurement(accessibility, "focus-controls"));
        Assert.Contains("accessibility-conformance:not-proven", accessibility.Tags);
        Assert.DoesNotContain("private-a11y-marker", json, StringComparison.Ordinal);

        EstimateReport report = new SeedEstimator().Estimate(
            evidence,
            EstimationProfile.Implementation);
        Assert.True(Category(report, EffortCategory.SecurityAndAccessibility).Expected > 0m);
        Assert.DoesNotContain(report.Diagnostics, diagnostic => diagnostic.Code == "FB1001");
    }

    [Fact]
    public async Task StructuralMarkupAloneDoesNotInventAccessibilityEvidence()
    {
        InMemoryRepository repository = new();
        repository.WriteText("index.html", "<main><section>Summary</section></main>");

        RepositoryEvidence evidence = await ScanAsync(repository);

        Assert.DoesNotContain(evidence.Facts, fact => fact.Kind == EvidenceKinds.Accessibility);
    }

    [Fact]
    public async Task AccessibilityFocusedComponentTestsRetainDepthAndProvenance()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "package.json",
            "{ \"devDependencies\": { \"vitest\": \"4.0.0\", \"@testing-library/react\": \"16.0.0\", \"jest-axe\": \"10.0.0\" } }");
        repository.WriteText(
            "tests/card.test.jsx",
            "import { render, screen } from '@testing-library/react'; import { axe } from 'jest-axe'; test('accessible card', async () => { render(Card()); screen.getByRole('article'); expect(await axe(document.body)).toHaveNoViolations(); });");

        RepositoryEvidence evidence = await ScanAsync(repository);
        EvidenceFact test = Fact(evidence, "javascript:test:tests/card.test.jsx");

        Assert.Contains("test-type:component", test.Tags);
        Assert.Contains("test-focus:accessibility", test.Tags);
        Assert.Contains("technology:jest-axe", test.Tags);
        Assert.True(Measurement(test, "accessibility-checks") >= 2m);
    }

    [Fact]
    public async Task SemanticUiEffortIsFormattingAndExactCopyInvariantButBehaviorSensitive()
    {
        EstimateReport baseline = await EstimateAsync(
            "<main><section>Summary</section></main>",
            ".panel { display: block; }");
        EstimateReport formatted = await EstimateAsync(
            """
            <main>

              <section>
                Summary
              </section>
            </main>
            """,
            """
            .panel
            {
              display: block;
            }
            """);
        EstimateReport richer = await EstimateAsync(
            "<main><form *ngIf='ready' (submit)='save()'><input [(ngModel)]='name'><button>Save</button></form></main>",
            ":root { --space: 1rem; } @media (width > 40rem) { .panel, .grid { display: grid; animation: enter .2s; } }");
        EstimateReport copied = await EstimateAsync(
            "<main><section>Summary</section></main>",
            ".panel { display: block; }",
            copyStyle: true);

        Assert.Equal(baseline.TotalEffort, formatted.TotalEffort);
        Assert.Equal(baseline.TotalEffort, copied.TotalEffort);
        EffortRange baselineUi = Category(baseline, EffortCategory.UiImplementationAndRepresentedUxDecisions);
        EffortRange richerUi = Category(richer, EffortCategory.UiImplementationAndRepresentedUxDecisions);
        Assert.True(
            richerUi.Expected > baselineUi.Expected,
            $"Expected richer UI effort above baseline; baseline={baselineUi}, richer={richerUi}.");
        foreach (EffortCategory category in baseline.Categories.Select(item => item.Category)
            .Union(richer.Categories.Select(item => item.Category))
            .Where(category => category != EffortCategory.UiImplementationAndRepresentedUxDecisions))
        {
            Assert.Equal(Category(baseline, category), Category(richer, category));
        }
    }

    private static InMemoryRepository AngularRepository()
    {
        InMemoryRepository repository = new();
        repository.WriteText(
            "package.json",
            "{ \"name\": \"angular-fixture\", \"dependencies\": { \"@angular/core\": \"21.0.0\" } }");
        repository.WriteText(
            "src/card.component.ts",
            """
            import { Component } from '@angular/core';
            @Component({
              selector: 'app-card',
              template: `<section *ngIf="ready"><span>{{ label }}</span><span>private-template-marker</span></section>`,
              templateUrl: './card.html',
              styles: [`:host { display: block; } @media (width > 30rem) { :host { display: grid; } }`],
              styleUrl: './card.scss'
            })
            export class CardComponent {}
            """);
        repository.WriteText(
            "src/card.html",
            "<form (ngSubmit)='save()'><input [(ngModel)]='name'><button>Save</button></form>");
        repository.WriteText(
            "src/card.scss",
            "$accent: red; :root { --space: 1rem; } [data-theme='dark'] { color: $accent; } @media (width > 40rem) { .card { animation: enter .2s; } } /* private-style-marker */");
        return repository;
    }

    private static async Task<RepositoryEvidence> ScanAsync(InMemoryRepository repository) =>
        await new RepositoryAnalysisPipeline(repository).ScanAsync(repository.RootPath);

    private static async Task<EstimateReport> EstimateAsync(
        string markup,
        string styles,
        bool copyStyle = false)
    {
        InMemoryRepository repository = new();
        repository.WriteText("index.html", markup);
        repository.WriteText("styles.css", styles);
        if (copyStyle)
        {
            repository.WriteText("copy.css", styles);
        }

        RepositoryEvidence evidence = await ScanAsync(repository);
        return new SeedEstimator().Estimate(evidence, EstimationProfile.Implementation);
    }

    private static EvidenceFact Fact(RepositoryEvidence evidence, string id) =>
        Assert.Single(evidence.Facts, fact => fact.Id == id);

    private static EvidenceFact SingleFact(RepositoryEvidence evidence, string idPrefix) =>
        Assert.Single(evidence.Facts, fact => fact.Id.StartsWith(idPrefix, StringComparison.Ordinal));

    private static decimal Measurement(EvidenceFact fact, string name) =>
        Assert.Single(fact.Measurements, measurement => measurement.Name == name).Value;

    private static EffortRange Category(EstimateReport report, EffortCategory category) =>
        report.Categories.SingleOrDefault(item => item.Category == category)?.Hours ?? new EffortRange
        {
            Low = 0m,
            Expected = 0m,
            High = 0m,
        };
}
