using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.JavaScript;

internal sealed class FrontendEvidenceAnalyzer(RepositoryTextReader textReader)
{
    private const long MaximumAssetBytes = 4 * 1024 * 1024;

    private readonly RepositoryTextReader _textReader = textReader;

    public async Task<FrontendAnalysisResult> AnalyzeAsync(
        RepositoryEvidence evidence,
        IReadOnlyList<JavaScriptPackageModel> packages,
        IReadOnlyList<AngularComponentMetadata> components,
        CancellationToken cancellationToken)
    {
        EvidenceFact[] assets = [.. evidence.Facts
            .Where(IsMaintainedWebAsset)
            .OrderBy(fact => fact.Scope, StringComparer.Ordinal)];
        Dictionary<string, EvidenceFact> assetsByPath = assets.ToDictionary(
            fact => fact.Scope,
            StringComparer.Ordinal);
        ComponentOwner[] owners = [.. components
            .OrderBy(component => component.SourcePath, StringComparer.Ordinal)
            .ThenBy(component => component.Line)
            .Select((component, index) => new ComponentOwner(
                component,
                JavaScriptEvidence.IdToken($"{component.SourcePath}:{component.Line}:{index}")))];
        Dictionary<string, List<ComponentOwner>> ownersByAsset = new(StringComparer.Ordinal);
        Dictionary<string, int> unresolvedByOwner = new(StringComparer.Ordinal);
        foreach (ComponentOwner owner in owners)
        {
            AddReferences(owner, owner.Component.TemplateReferences, "html", assetsByPath, ownersByAsset, unresolvedByOwner);
            AddReferences(owner, owner.Component.StyleReferences, "style", assetsByPath, ownersByAsset, unresolvedByOwner);
        }

        List<EvidenceFact> facts = [];
        List<Diagnostic> diagnostics = [];
        foreach (ComponentOwner owner in owners)
        {
            facts.AddRange(CreateComponentFacts(
                owner,
                unresolvedByOwner.GetValueOrDefault(owner.Token)));
            if (unresolvedByOwner.GetValueOrDefault(owner.Token) > 0)
            {
                diagnostics.Add(JavaScriptEvidence.Diagnostic(
                    "FB4202",
                    DiagnosticSeverity.Information,
                    "One or more static Angular component asset references did not resolve to an admitted maintained frontend asset and were excluded.",
                    owner.Component.SourcePath,
                    owner.Component.Line));
            }
        }

        foreach (EvidenceFact asset in assets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RepositoryTextReadResult read = await _textReader.ReadAsync(
                asset,
                MaximumAssetBytes,
                "FB4201",
                cancellationToken).ConfigureAwait(false);
            if (read.Diagnostic is not null)
            {
                diagnostics.Add(read.Diagnostic);
                continue;
            }

            ownersByAsset.TryGetValue(asset.Scope, out List<ComponentOwner>? assetOwners);
            facts.AddRange(CreateAssetFacts(asset, read.Text!, packages, assetOwners ?? []));
        }

        return new FrontendAnalysisResult(facts, diagnostics);
    }

    private static void AddReferences(
        ComponentOwner owner,
        IReadOnlyList<string> references,
        string expectedKind,
        Dictionary<string, EvidenceFact> assetsByPath,
        Dictionary<string, List<ComponentOwner>> ownersByAsset,
        Dictionary<string, int> unresolvedByOwner)
    {
        foreach (string reference in references.Distinct(StringComparer.Ordinal))
        {
            string? path = ResolveRelativePath(owner.Component.SourcePath, reference);
            if (path is null || !assetsByPath.TryGetValue(path, out EvidenceFact? asset) ||
                !MatchesKind(asset, expectedKind))
            {
                unresolvedByOwner[owner.Token] = unresolvedByOwner.GetValueOrDefault(owner.Token) + 1;
                continue;
            }

            if (!ownersByAsset.TryGetValue(path, out List<ComponentOwner>? assetOwners))
            {
                assetOwners = [];
                ownersByAsset.Add(path, assetOwners);
            }

            if (!assetOwners.Any(candidate => candidate.Token == owner.Token))
            {
                assetOwners.Add(owner);
            }
        }
    }

    private static IReadOnlyList<EvidenceFact> CreateComponentFacts(
        ComponentOwner owner,
        int unresolvedReferences)
    {
        AngularComponentMetadata component = owner.Component;
        FrontendMarkupMetrics markup = new();
        foreach (string template in component.InlineTemplates)
        {
            markup.Merge(FrontendMarkupAnalyzer.Analyze(template));
        }

        StylesheetMetrics styles = new();
        foreach (string style in component.InlineStyles)
        {
            styles.Merge(StylesheetAnalyzer.Analyze(style, "css"));
        }

        List<string> tags =
        [
            "technology:angular",
            "framework-flavor:angular",
            "angular-metadata:static-only",
            "semantic-analysis:bounded",
        ];
        if (component.DynamicProperties > 0)
        {
            tags.Add("angular-metadata:dynamic-values-excluded");
        }

        EvidenceFact uiFact = JavaScriptEvidence.Fact(
            $"javascript:angular-component:{owner.Token}",
            EvidenceKinds.UserInterface,
            component.PackageScope,
            $"Static Angular component metadata and represented frontend semantics in '{component.SourcePath}'.",
            EvidenceSourceKind.Inferred,
            "static Angular decorator token parsing with bounded markup and stylesheet scanning",
            [JavaScriptEvidence.Location(component.SourcePath, component.Line)],
            Measurements(
                markup,
                styles,
                components: 1,
                component.HasSelector ? 1 : 0,
                component.InlineTemplates.Count,
                component.InlineStyles.Count,
                component.TemplateReferences.Count,
                component.StyleReferences.Count,
                component.StaticProperties,
                component.DynamicProperties,
                unresolvedReferences),
            tags);
        EvidenceFact? accessibilityFact = FrontendAccessibilityAnalyzer.CreateFact(
            $"javascript:accessibility:angular-component:{owner.Token}",
            component.PackageScope,
            $"Explicit static accessibility semantics were detected in Angular component '{component.SourcePath}'.",
            "static Angular inline-template accessibility token scanning",
            markup,
            [JavaScriptEvidence.Location(component.SourcePath, component.Line)],
            ["technology:angular", "framework-flavor:angular"]);
        return accessibilityFact is null ? [uiFact] : [uiFact, accessibilityFact];
    }

    private static IReadOnlyList<EvidenceFact> CreateAssetFacts(
        EvidenceFact asset,
        string source,
        IReadOnlyList<JavaScriptPackageModel> packages,
        IReadOnlyList<ComponentOwner> owners)
    {
        string language = JavaScriptEvidence.FindTagValue(asset.Tags, "language:")!;
        bool isMarkup = language == "html";
        FrontendMarkupMetrics markup = isMarkup
            ? FrontendMarkupAnalyzer.Analyze(source)
            : new FrontendMarkupMetrics();
        StylesheetMetrics styles = isMarkup
            ? new StylesheetMetrics()
            : StylesheetAnalyzer.Analyze(source, language);
        ComponentOwner? soleOwner = owners.Count == 1 ? owners[0] : null;
        string scope = soleOwner?.Component.PackageScope ?? FindOwningPackage(asset.Scope, packages)?.Scope ?? ".";
        List<string> tags =
        [
            "ui-asset:maintained",
            isMarkup ? "ui-asset:template" : "ui-asset:style",
            $"language:{language}",
            "semantic-analysis:bounded",
        ];
        if (soleOwner is not null)
        {
            tags.Add("asset-ownership:angular-component");
            tags.Add($"component-owner:{soleOwner.Token}");
            tags.Add("technology:angular");
        }
        else if (owners.Count > 1)
        {
            tags.Add("asset-ownership:ambiguous-shared");
        }
        else
        {
            tags.Add("asset-ownership:package-or-generic");
        }

        decimal physicalLines = asset.Measurements
            .Where(measurement => measurement.Name == "physical-lines")
            .Sum(measurement => measurement.Value);
        EvidenceFact uiFact = JavaScriptEvidence.FactWithPrimaryLocation(
            $"javascript:ui-asset:{asset.Scope}",
            EvidenceKinds.UserInterface,
            scope,
            $"Bounded frontend semantic evidence for maintained asset '{asset.Scope}'.",
            EvidenceSourceKind.Measured,
            isMarkup
                ? "tolerant bounded HTML/template token scanning"
                : "tolerant bounded CSS-family structural scanning",
            JavaScriptEvidence.Location(asset.Scope),
            owners.Select(owner => JavaScriptEvidence.Location(owner.Component.SourcePath, owner.Component.Line)),
            [
                .. Measurements(markup, styles),
                JavaScriptEvidence.Measurement("files", 1, "files"),
                JavaScriptEvidence.Measurement("markup-files", isMarkup ? 1 : 0, "files"),
                JavaScriptEvidence.Measurement("style-files", isMarkup ? 0 : 1, "files"),
                JavaScriptEvidence.Measurement("physical-lines", physicalLines, "lines"),
            ],
            tags);
        EvidenceFact? accessibilityFact = FrontendAccessibilityAnalyzer.CreateFact(
            $"javascript:accessibility:{asset.Scope}",
            scope,
            $"Explicit static accessibility semantics were detected in maintained template '{asset.Scope}'.",
            "tolerant bounded HTML/template accessibility token scanning",
            markup,
            [
                JavaScriptEvidence.Location(asset.Scope),
                .. owners.Select(owner => JavaScriptEvidence.Location(
                    owner.Component.SourcePath,
                    owner.Component.Line)),
            ],
            tags.Where(tag => tag.StartsWith("technology:", StringComparison.Ordinal) ||
                tag.StartsWith("asset-ownership:", StringComparison.Ordinal)));
        return accessibilityFact is null ? [uiFact] : [uiFact, accessibilityFact];
    }

    private static IReadOnlyList<EvidenceMeasurement> Measurements(
        FrontendMarkupMetrics markup,
        StylesheetMetrics styles,
        int components = 0,
        int selectors = 0,
        int inlineTemplates = 0,
        int inlineStyles = 0,
        int templateReferences = 0,
        int styleReferences = 0,
        int staticProperties = 0,
        int dynamicProperties = 0,
        int unresolvedReferences = 0) =>
    [
        JavaScriptEvidence.Measurement("components", components, "components"),
        JavaScriptEvidence.Measurement("component-selectors", selectors, "selectors"),
        JavaScriptEvidence.Measurement("inline-template-blocks", inlineTemplates, "blocks"),
        JavaScriptEvidence.Measurement("inline-style-blocks", inlineStyles, "blocks"),
        JavaScriptEvidence.Measurement("external-template-references", templateReferences, "references"),
        JavaScriptEvidence.Measurement("external-style-references", styleReferences, "references"),
        JavaScriptEvidence.Measurement("static-metadata-properties", staticProperties, "properties"),
        JavaScriptEvidence.Measurement("dynamic-metadata-properties", dynamicProperties, "properties"),
        JavaScriptEvidence.Measurement("unresolved-asset-references", unresolvedReferences, "references"),
        JavaScriptEvidence.Measurement("elements", markup.Elements, "elements"),
        JavaScriptEvidence.Measurement("structural-elements", markup.StructuralElements, "elements"),
        JavaScriptEvidence.Measurement("custom-elements", markup.CustomElements, "elements"),
        JavaScriptEvidence.Measurement("forms", markup.Forms, "forms"),
        JavaScriptEvidence.Measurement("form-controls", markup.FormControls, "controls"),
        JavaScriptEvidence.Measurement("bindings", markup.Bindings, "bindings"),
        JavaScriptEvidence.Measurement("directives", markup.Directives, "directives"),
        JavaScriptEvidence.Measurement("template-structure-units", markup.TemplateStructureUnits, "units"),
        JavaScriptEvidence.Measurement("template-binding-units", markup.TemplateBindingUnits, "units"),
        JavaScriptEvidence.Measurement("rule-groups", styles.RuleGroups, "rules"),
        JavaScriptEvidence.Measurement("selectors", styles.Selectors, "selectors"),
        JavaScriptEvidence.Measurement("responsive-surfaces", styles.ResponsiveSurfaces, "surfaces"),
        JavaScriptEvidence.Measurement("design-tokens", styles.DesignTokens, "tokens"),
        JavaScriptEvidence.Measurement("animation-theme-surfaces", styles.AnimationThemeSurfaces, "surfaces"),
        JavaScriptEvidence.Measurement("style-structure-units", styles.StyleStructureUnits, "units"),
        JavaScriptEvidence.Measurement("design-token-units", styles.DesignTokenUnits, "units"),
    ];

    private static string? ResolveRelativePath(string sourcePath, string reference)
    {
        string value = reference.Trim().Replace('\\', '/');
        if (value.Length == 0 || value.StartsWith('/') || value.Contains(':') ||
            value.Contains('?') || value.Contains('#') || value.Contains('\0'))
        {
            return null;
        }

        List<string> segments = [.. sourcePath.Split('/').SkipLast(1)];
        foreach (string segment in value.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    return null;
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        return segments.Count == 0 ? null : string.Join('/', segments);
    }

    private static bool MatchesKind(EvidenceFact asset, string expectedKind)
    {
        string? language = JavaScriptEvidence.FindTagValue(asset.Tags, "language:");
        return expectedKind == "html"
            ? language == "html"
            : language is "css" or "scss" or "sass" or "less";
    }

    private static bool IsMaintainedWebAsset(EvidenceFact fact) =>
        fact.Kind == EvidenceKinds.File &&
        fact.Tags.Contains("role:source", StringComparer.Ordinal) &&
        !fact.Tags.Contains("classification:test", StringComparer.Ordinal) &&
        !fact.Tags.Contains("classification:generated", StringComparer.Ordinal) &&
        !fact.Tags.Contains("classification:minified", StringComparer.Ordinal) &&
        !fact.Tags.Contains("classification:vendored", StringComparer.Ordinal) &&
        !fact.Tags.Contains("content:binary", StringComparer.Ordinal) &&
        JavaScriptEvidence.FindTagValue(fact.Tags, "language:") is
            "css" or "scss" or "sass" or "less" or "html";

    private static JavaScriptPackageModel? FindOwningPackage(
        string path,
        IReadOnlyList<JavaScriptPackageModel> packages) => packages
            .Where(package => package.Scope == "." ||
                path.StartsWith(package.Scope + "/", StringComparison.Ordinal))
            .OrderByDescending(package => package.Scope.Length)
            .ThenBy(package => package.Scope, StringComparer.Ordinal)
            .FirstOrDefault();

    private sealed record ComponentOwner(AngularComponentMetadata Component, string Token);
}

internal sealed record FrontendAnalysisResult(
    IReadOnlyList<EvidenceFact> Facts,
    IReadOnlyList<Diagnostic> Diagnostics);
