using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EffortHours.EndToEndTests;

public sealed class CalibrationFixtureDependencyDispositionTests
{
    private const string SyntheticRoot = "calibration/mutations/public-synthetic";
    private const string DispositionDocument = $"{SyntheticRoot}/DEPENDENCY_ALERTS.md";
    private const string JavaScriptSuite07 = $"{SyntheticRoot}/0.7.0.suite.json";
    private const string JavaScriptSuite08 = $"{SyntheticRoot}/0.8.0.suite.json";
    private const string JavaScriptReport07 =
        $"{SyntheticRoot}/baseline-seed-rules-0.3.0-suite-0.7.0.json";
    private const string JavaScriptReport08 =
        $"{SyntheticRoot}/baseline-seed-rules-0.4.0-suite-0.8.0.json";
    private const string GoSuite = $"{SyntheticRoot}/go-0.1.0.suite.json";
    private const string GoReport = $"{SyntheticRoot}/baseline-seed-rules-0.4.0-go-0.1.0.json";

    private static readonly Disposition[] ExpectedDispositions =
    [
        new(
            8,
            "GHSA-5xrq-8626-4rwp",
            "vitest",
            "4.0.0",
            ">= 4.0.0, < 4.1.0",
            "4.1.0",
            "frontend-accessibility-tests",
            $"{SyntheticRoot}/fixtures/frontend-accessibility-tests/package.json",
            "sha256:1a06c175183b900cb925dccac007b67a4f4bb9a10ae7d03d5d8e2d88733cfe72",
            "sha256:fc10583062a35d1e42cb1b5bcfb6ec749b99f4959ec048529faf04b7da4dd2ec",
            $"{SyntheticRoot}/estimates/seed-rules-0.3.0/frontend-accessibility-tests.estimate.json",
            "sha256:8b3befce754223e508bf68f9c4a30fd3ddbc6aabb14633663f676addcf6ed902",
            "sha256:be1b2e3697081a31c589935f2b4a61c5cdb8a955dee667fbe1ed03bd35bd8b06",
            "seed-rules/0.3.0",
            [JavaScriptSuite07, JavaScriptSuite08],
            [JavaScriptReport07, JavaScriptReport08]),
        new(
            9,
            "GHSA-5xrq-8626-4rwp",
            "vitest",
            "4.0.0",
            ">= 4.0.0, < 4.1.0",
            "4.1.0",
            "frontend-component-tests",
            $"{SyntheticRoot}/fixtures/frontend-component-tests/package.json",
            "sha256:1a96ad5cec1c22d1b3312d526642af17fdf0c874db5099a4cd5e8c297953e0d1",
            "sha256:8b00299eb5e3b7d1c4fe1b0824984b862e25d2b9c46c9e1e90d4d8275c4fa1a9",
            $"{SyntheticRoot}/estimates/seed-rules-0.3.0/frontend-component-tests.estimate.json",
            "sha256:1a2d2889ee24c41b7e5d644b805e0d2580cc8b87234f1f7a95ccad85b4960b75",
            "sha256:2ff3648eebefe9c63e608e1eb78e3c8aff224aaf12fbaa1c99a45a7bfdc2850c",
            "seed-rules/0.3.0",
            [JavaScriptSuite07, JavaScriptSuite08],
            [JavaScriptReport07, JavaScriptReport08]),
        new(
            10,
            "GHSA-5xrq-8626-4rwp",
            "vitest",
            "4.0.0",
            ">= 4.0.0, < 4.1.0",
            "4.1.0",
            "javascript-workspace-graph",
            $"{SyntheticRoot}/fixtures/javascript-workspace-graph/packages/domain/package.json",
            "sha256:096b5f9ad0a922195b0cb89c3680c3501040f1943d1ab4084e305d0426cfc7e2",
            "sha256:ea5bc878f21fc63ba2a8ae7e6df830aa7856bed5094b088270f3407226b5311a",
            $"{SyntheticRoot}/estimates/seed-rules-0.3.0/javascript-workspace-graph.estimate.json",
            "sha256:702d67be579573bea0c8922c2ea474eb91705affa2f0f1b198d811364064b8b5",
            "sha256:52c4a6a4786ff680eba8ee7690eedaf51c34437b6f7757b57088144c554ce8b1",
            "seed-rules/0.3.0",
            [JavaScriptSuite07, JavaScriptSuite08],
            [JavaScriptReport07, JavaScriptReport08]),
        new(
            11,
            "GHSA-5xrq-8626-4rwp",
            "vitest",
            "4.0.0",
            ">= 4.0.0, < 4.1.0",
            "4.1.0",
            "mixed-boundary-graph",
            $"{SyntheticRoot}/fixtures/mixed-boundary-graph/web/packages/app/package.json",
            "sha256:9ac1f2fe3eb6910a267fddd1ca34e3551e3b9812cb5a4f75d6aafbd0434f00c4",
            "sha256:6cebdebdc779b09e46e2565981316501f478e4103d2f9f2d0e6e63cabe42c47e",
            $"{SyntheticRoot}/estimates/seed-rules-0.3.0/mixed-boundary-graph.estimate.json",
            "sha256:69e4dd7b25cbe9619560725cbb3181bb6c6b6c09936820d0818552a775696e80",
            "sha256:d073c804f8b9e570ed0fe1f873c96add18e5de6264ff9460f70e93be094e36bb",
            "seed-rules/0.3.0",
            [JavaScriptSuite07, JavaScriptSuite08],
            [JavaScriptReport07, JavaScriptReport08]),
        new(
            12,
            "GHSA-mh63-6h87-95cp",
            "github.com/golang-jwt/jwt/v5",
            "v5.2.0",
            ">= 5.0.0-rc.1, < 5.2.2",
            "v5.2.2",
            "go-security",
            $"{SyntheticRoot}/fixtures/go-security/go.mod",
            "sha256:3bc613a7d5f7d53966636dd3b64e64ed1dcd1964455ec7ee4f1f64fd3e549178",
            "sha256:8894a32546506d5d12ad7745e893ccf8c493d8a4eef0d768a0c1a42cd4414979",
            $"{SyntheticRoot}/estimates/seed-rules-0.4.0/go-security.estimate.json",
            "sha256:e8b1602876bdbe99f2349247e9a4c2b344bb5075bbc8f9ed3e95205faaaecc83",
            "sha256:c4cd55705c9c9517e8a18e712ff8f508fee61d26743926a7db040f6faa44ac90",
            "seed-rules/0.4.0",
            [GoSuite],
            [GoReport]),
    ];

    [Fact]
    public void AdvisoryAffectedDeclarationsRemainAnExactFrozenStaticFixtureAllowlist()
    {
        string root = FindRepositoryRoot();
        string fixtures = Path.Combine(root, SyntheticRoot, "fixtures");
        string dispositionDocument = File.ReadAllText(Path.Combine(root, DispositionDocument));

        Assert.Equal(5, ExpectedDispositions.Length);
        AssertExactVulnerableDeclarationInventory(root, fixtures);

        foreach (Disposition disposition in ExpectedDispositions)
        {
            AssertDocumented(dispositionDocument, disposition);
            AssertManifest(root, disposition);
            Assert.Equal(
                disposition.FixtureTreeDigest,
                DigestFixtureTree(Path.Combine(fixtures, disposition.CaseId)));
            AssertEstimate(root, disposition);

            foreach (string suite in disposition.Suites)
            {
                AssertSuite(root, suite, disposition);
            }

            foreach (string report in disposition.Reports)
            {
                AssertReport(root, report, disposition);
            }
        }
    }

    private static void AssertExactVulnerableDeclarationInventory(string root, string fixtures)
    {
        string[] actualVitest =
        [
            .. Directory.EnumerateFiles(fixtures, "package.json", SearchOption.AllDirectories)
                .Where(path => ReadJsonDependencyVersion(path, "vitest") == "4.0.0")
                .Select(path => RepositoryPath(root, path))
                .Order(StringComparer.Ordinal),
        ];
        string[] expectedVitest =
        [
            .. ExpectedDispositions
                .Where(item => item.Package == "vitest")
                .Select(item => item.ManifestPath)
                .Order(StringComparer.Ordinal),
        ];
        Assert.Equal(expectedVitest, actualVitest);

        string[] actualJwt =
        [
            .. Directory.EnumerateFiles(fixtures, "go.mod", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains(
                    "require github.com/golang-jwt/jwt/v5 v5.2.0",
                    StringComparison.Ordinal))
                .Select(path => RepositoryPath(root, path))
                .Order(StringComparer.Ordinal),
        ];
        Assert.Equal(
            ExpectedDispositions.Where(item => item.Package != "vitest")
                .Select(item => item.ManifestPath),
            actualJwt);
    }

    private static void AssertDocumented(string document, Disposition disposition)
    {
        Assert.Contains($"dependabot/{disposition.AlertNumber}", document, StringComparison.Ordinal);
        Assert.Contains(disposition.Advisory, document, StringComparison.Ordinal);
        Assert.Contains(disposition.ManifestPath[(SyntheticRoot.Length + 1)..], document, StringComparison.Ordinal);
        Assert.Contains($"Declared `{disposition.DeclaredVersion}`", document, StringComparison.Ordinal);
        Assert.Contains($"vulnerable `{disposition.VulnerableRange}`", document, StringComparison.Ordinal);
        Assert.Contains($"first patched `{disposition.FirstPatchedVersion}`", document, StringComparison.Ordinal);
        Assert.Contains(disposition.SourceDigest, document, StringComparison.Ordinal);
    }

    private static void AssertManifest(string root, Disposition disposition)
    {
        string manifest = Path.Combine(root, disposition.ManifestPath);
        Assert.True(File.Exists(manifest), $"Missing disposition manifest: {disposition.ManifestPath}");

        if (disposition.Package == "vitest")
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifest));
            Assert.True(document.RootElement.GetProperty("private").GetBoolean());
            Assert.Equal(disposition.DeclaredVersion, ReadJsonDependencyVersion(manifest, disposition.Package));
        }
        else
        {
            Assert.Contains(
                $"require {disposition.Package} {disposition.DeclaredVersion}",
                File.ReadAllText(manifest),
                StringComparison.Ordinal);
        }
    }

    private static void AssertEstimate(string root, Disposition disposition)
    {
        string estimatePath = Path.Combine(root, disposition.EstimatePath);
        string estimateJson = File.ReadAllText(estimatePath);
        Assert.Equal(disposition.EstimateFileDigest, Digest(estimateJson));

        using JsonDocument estimate = JsonDocument.Parse(estimateJson);
        Assert.Equal(disposition.EstimatorVersion, estimate.RootElement.GetProperty("estimatorVersion").GetString());
        Assert.Equal(
            disposition.SourceDigest,
            estimate.RootElement.GetProperty("repository").GetProperty("sourceDigest").GetString());
    }

    private static void AssertSuite(string root, string relativePath, Disposition disposition)
    {
        using JsonDocument suite = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, relativePath)));
        JsonElement item = Assert.Single(
            suite.RootElement.GetProperty("cases").EnumerateArray(),
            candidate => candidate.GetProperty("id").GetString() == disposition.CaseId);
        Assert.Equal(disposition.SourceDigest, item.GetProperty("sourceDigest").GetString());
    }

    private static void AssertReport(string root, string relativePath, Disposition disposition)
    {
        using JsonDocument report = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, relativePath)));
        JsonElement item = Assert.Single(
            report.RootElement.GetProperty("cases").EnumerateArray(),
            candidate => candidate.GetProperty("caseId").GetString() == disposition.CaseId);
        Assert.Equal(
            disposition.ReportCandidateDigest,
            item.GetProperty("candidateEstimateDigest").GetString());
    }

    private static string? ReadJsonDependencyVersion(string path, string package)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (string group in new[] { "dependencies", "devDependencies" })
        {
            if (document.RootElement.TryGetProperty(group, out JsonElement dependencies) &&
                dependencies.TryGetProperty(package, out JsonElement version))
            {
                return version.GetString();
            }
        }

        return null;
    }

    private static string DigestFixtureTree(string fixtureRoot)
    {
        StringBuilder input = new();
        foreach (string path in Directory.EnumerateFiles(fixtureRoot, "*", SearchOption.AllDirectories)
                     .OrderBy(path => RepositoryPath(fixtureRoot, path), StringComparer.Ordinal))
        {
            input.Append(RepositoryPath(fixtureRoot, path));
            input.Append('\0');
            input.Append(Normalize(File.ReadAllText(path)));
            input.Append('\0');
        }

        return Digest(input.ToString());
    }

    private static string Digest(string content) =>
        $"sha256:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(content)))).ToLowerInvariant()}";

    private static string Normalize(string content) =>
        content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private static string RepositoryPath(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "EffortHours.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private sealed record Disposition(
        int AlertNumber,
        string Advisory,
        string Package,
        string DeclaredVersion,
        string VulnerableRange,
        string FirstPatchedVersion,
        string CaseId,
        string ManifestPath,
        string FixtureTreeDigest,
        string SourceDigest,
        string EstimatePath,
        string EstimateFileDigest,
        string ReportCandidateDigest,
        string EstimatorVersion,
        string[] Suites,
        string[] Reports);
}
