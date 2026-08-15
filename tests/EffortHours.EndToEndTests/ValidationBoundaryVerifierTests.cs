using EffortHours.RepositoryCalibration;

namespace EffortHours.EndToEndTests;

public sealed class ValidationBoundaryVerifierTests
{
    [Fact]
    public async Task FrozenBoundaryAuthorizesExactlyNineValidationFamilies()
    {
        string root = FindRepositoryRoot();
        ValidationOpenOptions options = Options(root, CandidateManifest(root));

        VerifiedValidationBoundary boundary = await ValidationBoundaryVerifier.VerifyAsync(
            options,
            CancellationToken.None);

        Assert.Equal(ValidationBoundaryVerifier.ExpectedCandidateManifestDigest,
            boundary.CandidateManifestDigest);
        Assert.Equal(ValidationBoundaryVerifier.ExpectedPlanDigest, boundary.PlanDigest);
        Assert.Equal(ValidationBoundaryVerifier.ExpectedReproductionDigest,
            boundary.ReproductionDigest);
        Assert.Equal(ValidationBoundaryVerifier.ExpectedCustodyDigest, boundary.CustodyDigest);
        Assert.Equal(9, boundary.ValidationFamilies.Count);
        Assert.Equal(9, boundary.TestFamilies.Count);
        Assert.All(boundary.ValidationFamilies, family => Assert.Equal("validation", family.Partition));
        Assert.All(boundary.TestFamilies, family => Assert.Equal("test", family.Partition));
        Assert.Equal(
            ["dotnet", "javascript-typescript", "mixed-dotnet-javascript-typescript"],
            boundary.ValidationFamilies.Select(family => family.PrimaryStratum)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task ChangedManifestFailsBeforeAnyValidationOutputIsCreated()
    {
        string root = FindRepositoryRoot();
        string temporary = Path.Combine(
            root,
            "artifacts",
            "validation-boundary-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            string manifest = Path.Combine(temporary, "candidate-manifest.json");
            string original = await File.ReadAllTextAsync(
                CandidateManifest(root),
                CancellationToken.None);
            await File.WriteAllTextAsync(
                manifest,
                original.Replace(
                    "\"validationAuthorized\": true",
                    "\"validationAuthorized\": false",
                    StringComparison.Ordinal),
                CancellationToken.None);
            ValidationOpenOptions options = Options(root, manifest);

            InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
                ValidationBoundaryVerifier.VerifyAsync(
                    options,
                    CancellationToken.None));

            Assert.Contains("digest mismatch", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(Directory.Exists(options.WorkspacePath));
            Assert.False(Directory.Exists(options.PacketDirectory));
            Assert.False(File.Exists(options.OutputPath));
        }
        finally
        {
            Directory.Delete(temporary, recursive: true);
        }
    }

    [Fact]
    public void ValidationOpenOptionsRequireEveryFrozenBoundaryInput()
    {
        Assert.False(ValidationOpenOptions.TryParse(
            ["--plan", "plan.json"],
            out ValidationOpenOptions? options,
            out string? error));
        Assert.Null(options);
        Assert.Contains("--repository-root", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExistingOutputRejectsTheOneShotOpeningBeforeAccess()
    {
        string root = FindRepositoryRoot();
        ValidationOpenOptions options = Options(root, CandidateManifest(root));
        Directory.CreateDirectory(options.PacketDirectory);
        try
        {
            InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
                ValidationBoundaryVerifier.VerifyAsync(options, CancellationToken.None));

            Assert.Contains("one-shot", exception.Message, StringComparison.Ordinal);
            Assert.False(Directory.Exists(options.WorkspacePath));
            Assert.False(File.Exists(options.OutputPath));
        }
        finally
        {
            Directory.Delete(
                Directory.GetParent(options.PacketDirectory)!.FullName,
                recursive: true);
        }
    }

    private static ValidationOpenOptions Options(string root, string manifestPath)
    {
        string outputRoot = Path.Combine(
            root,
            "artifacts",
            "validation-boundary-tests",
            Guid.NewGuid().ToString("N"));
        return new ValidationOpenOptions
        {
            RepositoryRoot = root,
            PlanPath = Path.Combine(
                root,
                "calibration",
                "corpora",
                "public-readiness",
                "0.1.0.sampling-plan.json"),
            ReproductionManifestPath = Path.Combine(
                root,
                "calibration",
                "corpora",
                "public-readiness",
                "0.2.0.reproduction-manifest.json"),
            CustodyPath = Path.Combine(
                root,
                "calibration",
                "corpora",
                "public-readiness",
                "0.2.0.holdout-custody.json"),
            CandidateManifestPath = manifestPath,
            SourceCommit = ExternalProcess.RunAsync(
                    "git",
                    ["-C", root, "rev-parse", "HEAD"],
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult()
                .Trim(),
            WorkspacePath = Path.Combine(outputRoot, "workspace"),
            CliPath = typeof(ValidationBoundaryVerifier).Assembly.Location,
            PacketDirectory = Path.Combine(outputRoot, "packets"),
            OutputPath = Path.Combine(outputRoot, "opening.json"),
        };
    }

    private static string CandidateManifest(string root) => Path.Combine(
        root,
        "calibration",
        "corpora",
        "public-readiness",
        "1.2.0",
        "1.2.0.candidate-manifest.json");

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "EffortHours.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
