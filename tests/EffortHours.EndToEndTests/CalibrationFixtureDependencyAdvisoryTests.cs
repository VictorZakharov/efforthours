namespace EffortHours.EndToEndTests;

public sealed partial class CalibrationFixtureDependencyDispositionTests
{
    [Fact]
    public void AdditionalVitestAdvisoriesReuseTheExactFrozenFixtureBoundary()
    {
        string root = FindRepositoryRoot();
        string document = File.ReadAllText(Path.Combine(root, DispositionDocument));
        Disposition[] fixtures = [.. ExpectedDispositions.Where(item => item.Package == "vitest")];
        Assert.Equal(4, fixtures.Length);

        foreach (Disposition fixture in fixtures)
        {
            Disposition current = fixture with
            {
                AlertNumber = fixture.CaseId switch
                {
                    "frontend-accessibility-tests" => 13,
                    "javascript-workspace-graph" => 14,
                    "mixed-boundary-graph" => 15,
                    "frontend-component-tests" => 16,
                    _ => throw new InvalidOperationException("Unexpected advisory fixture."),
                },
                Advisory = "GHSA-82fw-gwwq-j7x9",
                VulnerableRange = ">= 2.1.0, < 4.1.11",
                FirstPatchedVersion = "4.1.11",
            };
            AssertDocumented(document, current);
            AssertManifest(root, current);
        }
    }

    [Fact]
    public void AngularAdvisoryRetainsItsExactFrozenTreeAndFourHistoricalReports()
    {
        string root = FindRepositoryRoot();
        string fixtures = Path.Combine(root, SyntheticRoot, "fixtures");
        Disposition angular = new(
            17,
            "GHSA-hh8m-fm6v-7cvg",
            "@angular/core",
            "21.0.0",
            ">= 21.0.0, < 21.2.20",
            "21.2.20",
            "frontend-angular",
            $"{SyntheticRoot}/fixtures/frontend-angular/package.json",
            "sha256:c0138d8c3c88978e1c21753b37c6735dfa62c0f93b7c6ee3206ace2127943068",
            "sha256:d30ffc9741750bf6fe887a2e4fe2697c5f548fa93c82ad2acc608900bb816038",
            $"{SyntheticRoot}/estimates/seed-rules-0.3.0/frontend-angular.estimate.json",
            "sha256:8ac8d7cddbe18b749fe1141b99bfc9a24dc610e242cf8b769567c1a73d934427",
            "sha256:6e26fef6dba9f1f0648c80d0b2bb7c728c6b7d463807a270bb581fe74fbf1ef2",
            "seed-rules/0.3.0",
            [$"{SyntheticRoot}/0.5.0.suite.json", $"{SyntheticRoot}/0.6.0.suite.json", JavaScriptSuite07, JavaScriptSuite08],
            [$"{SyntheticRoot}/baseline-seed-rules-0.3.0-suite-0.5.0.json",
             $"{SyntheticRoot}/baseline-seed-rules-0.3.0-suite-0.6.0.json", JavaScriptReport07, JavaScriptReport08]);

        string[] actual = [.. Directory.EnumerateFiles(fixtures, "package.json", SearchOption.AllDirectories)
            .Where(path => ReadJsonDependencyVersion(path, angular.Package) == angular.DeclaredVersion)
            .Select(path => RepositoryPath(root, path)).Order(StringComparer.Ordinal)];
        Assert.Equal([angular.ManifestPath], actual);
        AssertDocumented(File.ReadAllText(Path.Combine(root, DispositionDocument)), angular);
        AssertManifest(root, angular);
        Assert.Equal(angular.FixtureTreeDigest, DigestFixtureTree(Path.Combine(fixtures, angular.CaseId)));
        AssertEstimate(root, angular);
        foreach (string suite in angular.Suites) AssertSuite(root, suite, angular);
        foreach (string report in angular.Reports) AssertReport(root, report, angular);
    }
}
