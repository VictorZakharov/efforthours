using System.Text.Json;

namespace EffortHours.RepositoryCalibration;

internal static class ValidationBoundaryVerifier
{
    public const string ExpectedCandidateManifestDigest =
        "sha256:206b3955d53af9902996b588e9255ab9396e7b7624731a6d6e09896ce5026f23";
    public const string ExpectedPlanDigest =
        "sha256:c4c5f0026112b0d495e79e9ca7a5b3d03a763710db75360d02fd333afd282aa1";
    public const string ExpectedReproductionDigest =
        "sha256:0d36e4178a65a523fd4705e6bc353ec5365f565b3b27f52692d7bed2e47b5159";
    public const string ExpectedCustodyDigest =
        "sha256:09bbd23d6a6b5aca4094ce9651d9016dc15bbc6cb860e7f73d1c242a1fafdd50";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<VerifiedValidationBoundary> VerifyAsync(
        ValidationOpenOptions options,
        CancellationToken cancellationToken)
    {
        ValidatePaths(options);
        await ValidateOpeningCheckoutAsync(options, cancellationToken).ConfigureAwait(false);
        string candidateManifestDigest = await RequireDigestAsync(
            options.CandidateManifestPath,
            ExpectedCandidateManifestDigest,
            cancellationToken).ConfigureAwait(false);
        string planDigest = await RequireDigestAsync(
            options.PlanPath,
            ExpectedPlanDigest,
            cancellationToken).ConfigureAwait(false);
        string reproductionDigest = await RequireDigestAsync(
            options.ReproductionManifestPath,
            ExpectedReproductionDigest,
            cancellationToken).ConfigureAwait(false);
        string custodyDigest = await RequireDigestAsync(
            options.CustodyPath,
            ExpectedCustodyDigest,
            cancellationToken).ConfigureAwait(false);

        string candidateJson = await File.ReadAllTextAsync(
            options.CandidateManifestPath,
            cancellationToken).ConfigureAwait(false);
        using JsonDocument candidateDocument = JsonDocument.Parse(candidateJson);
        await ValidationCandidateVerifier.ValidateAsync(
            candidateDocument.RootElement,
            options.RepositoryRoot,
            cancellationToken).ConfigureAwait(false);

        SamplingPlan plan = await ReadAsync<SamplingPlan>(options.PlanPath, cancellationToken)
            .ConfigureAwait(false);
        ReproductionManifest reproduction = await ReadAsync<ReproductionManifest>(
            options.ReproductionManifestPath,
            cancellationToken).ConfigureAwait(false);
        HoldoutCustody custody = await ReadAsync<HoldoutCustody>(
            options.CustodyPath,
            cancellationToken).ConfigureAwait(false);
        ValidateSourceCustody(plan, reproduction, custody, candidateDocument.RootElement);

        return new VerifiedValidationBoundary
        {
            Plan = plan,
            Reproduction = reproduction,
            Custody = custody,
            CandidateManifestDigest = candidateManifestDigest,
            PlanDigest = planDigest,
            ReproductionDigest = reproductionDigest,
            CustodyDigest = custodyDigest,
            ValidationFamilies = [.. plan.Families
                .Where(family => family.Partition == "validation")
                .OrderBy(family => family.Id, StringComparer.Ordinal)],
            TestFamilies = [.. plan.Families
                .Where(family => family.Partition == "test")
                .OrderBy(family => family.Id, StringComparer.Ordinal)],
        };
    }

    private static void ValidatePaths(ValidationOpenOptions options)
    {
        string root = Path.GetFullPath(options.RepositoryRoot);
        if (Path.GetPathRoot(root) == root ||
            !File.Exists(Path.Combine(root, "EffortHours.slnx")))
        {
            throw new InvalidDataException("Repository root is not the EffortHours checkout.");
        }

        foreach (string path in new[]
                 {
                     options.PlanPath,
                     options.ReproductionManifestPath,
                     options.CustodyPath,
                     options.CandidateManifestPath,
                     options.CliPath,
                 })
        {
            RequireContainedPath(root, path);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Frozen validation input was not found.", path);
            }
        }

        foreach (string path in new[]
                 {
                     options.WorkspacePath,
                     options.PacketDirectory,
                     options.OutputPath,
                 })
        {
            RequireContainedPath(root, path);
        }

        if (Path.GetPathRoot(options.WorkspacePath) == options.WorkspacePath ||
            Path.GetPathRoot(options.PacketDirectory) == options.PacketDirectory)
        {
            throw new InvalidDataException("Validation output paths must not be filesystem roots.");
        }

        if (Directory.Exists(options.WorkspacePath) ||
            Directory.Exists(options.PacketDirectory) ||
            File.Exists(options.OutputPath))
        {
            throw new InvalidDataException(
                "Validation opening is one-shot; workspace, packet directory, and opening record must not exist.");
        }

        if (options.SourceCommit.Length != 40 ||
            options.SourceCommit.Any(character =>
                !char.IsAsciiDigit(character) && character is not (>= 'a' and <= 'f')))
        {
            throw new InvalidDataException("Validation opener source commit must be 40 lowercase hex characters.");
        }
    }

    private static async Task ValidateOpeningCheckoutAsync(
        ValidationOpenOptions options,
        CancellationToken cancellationToken)
    {
        string head = (await ExternalProcess.RunAsync(
                "git",
                ["-C", options.RepositoryRoot, "rev-parse", "HEAD"],
                cancellationToken)
            .ConfigureAwait(false)).Trim();
        if (!string.Equals(head, options.SourceCommit, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Validation opener source commit does not match the checked-out commit.");
        }
    }

    private static void ValidateSourceCustody(
        SamplingPlan plan,
        ReproductionManifest reproduction,
        HoldoutCustody custody,
        JsonElement candidateManifest)
    {
        if (plan.SamplingPlanVersion != "repository-sampling-plan/1.0.0" ||
            plan.Id != "efforthours-public-readiness" ||
            plan.Version != "0.1.0" ||
            plan.Profile != "implementation" ||
            plan.Families.Count != 33 ||
            reproduction.SchemaVersion != "repository-calibration-reproduction/1.0.0" ||
            reproduction.Families.Count != 33 ||
            reproduction.SamplingPlan.Digest != ExpectedPlanDigest ||
            custody.SchemaVersion != "repository-calibration-holdout-custody/1.0.0" ||
            custody.Status != "source-custody-only-labels-not-authored" ||
            custody.ReproductionManifest.Digest != ExpectedReproductionDigest ||
            custody.Families.Count != 18)
        {
            throw new InvalidDataException("Sampling, reproduction, or custody identity changed.");
        }

        SamplingFamily[] validation = [.. plan.Families.Where(family => family.Partition == "validation")];
        SamplingFamily[] test = [.. plan.Families.Where(family => family.Partition == "test")];
        if (validation.Length != 9 || test.Length != 9)
        {
            throw new InvalidDataException("Frozen holdout partition counts changed.");
        }

        ValidateMatrix(validation, "validation");
        ValidateMatrix(test, "test");
        foreach (SamplingFamily family in validation.Concat(test))
        {
            ReproductionFamily reproduced = reproduction.Families.Single(item => item.Id == family.Id);
            HoldoutFamily held = custody.Families.Single(item => item.Id == family.Id);
            if (reproduced.RepositoryName != family.RepositoryName ||
                reproduced.PrimaryStratum != family.PrimaryStratum ||
                reproduced.Partition != family.Partition ||
                reproduced.CommitSha != family.SourceSnapshot.CommitSha ||
                reproduced.GitTreeSha1 != family.SourceSnapshot.GitTreeSha1 ||
                reproduced.LicenseBlobSha1 != family.License.GitBlobSha1 ||
                reproduced.LicenseContentSha256 != family.License.ContentSha256 ||
                reproduced.VerificationStatus != "verified-commit-tree-blobs-license" ||
                reproduced.AnalysisStatus != "withheld-not-run" ||
                reproduced.Analysis is not null ||
                held.RepositoryName != reproduced.RepositoryName ||
                held.PrimaryStratum != reproduced.PrimaryStratum ||
                held.Partition != reproduced.Partition ||
                held.CommitSha != reproduced.CommitSha ||
                held.GitTreeSha1 != reproduced.GitTreeSha1 ||
                held.ArchiveSha256 != reproduced.ArchiveSha256 ||
                held.LicenseBlobSha1 != reproduced.LicenseBlobSha1 ||
                held.LicenseContentSha256 != reproduced.LicenseContentSha256 ||
                held.SourceVerificationStatus != reproduced.VerificationStatus ||
                held.AnalysisStatus != "withheld-not-run" ||
                held.LabelStatus != "not-authored" ||
                held.LabelDigest is not null)
            {
                throw new InvalidDataException($"Frozen custody mismatch for '{family.Id}'.");
            }
        }

        JsonElement excluded = candidateManifest.GetProperty("developmentBoundary")
            .GetProperty("excludedFamilies");
        RequireSameIds(excluded.GetProperty("validation"), validation.Select(family => family.Id));
        RequireSameIds(excluded.GetProperty("test"), test.Select(family => family.Id));
    }

    private static void ValidateMatrix(IReadOnlyList<SamplingFamily> families, string partition)
    {
        string[] strata =
        [
            "dotnet",
            "javascript-typescript",
            "mixed-dotnet-javascript-typescript",
        ];
        string[] bands = ["small", "medium", "large"];
        foreach (string stratum in strata)
        {
            string[] actual = [.. families
                .Where(family => family.PrimaryStratum == stratum)
                .Select(family => family.Size.Band)
                .Order(StringComparer.Ordinal)];
            if (!actual.SequenceEqual(bands.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            {
                throw new InvalidDataException(
                    $"Frozen {partition}/{stratum} size matrix is incomplete.");
            }
        }
    }

    private static void RequireSameIds(JsonElement array, IEnumerable<string> expected)
    {
        string[] actualIds = [.. array.EnumerateArray().Select(item => item.GetString()!)
            .Order(StringComparer.Ordinal)];
        string[] expectedIds = [.. expected.Order(StringComparer.Ordinal)];
        if (!actualIds.SequenceEqual(expectedIds, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Candidate manifest holdout identities do not match custody.");
        }
    }

    private static async Task<T> ReadAsync<T>(string path, CancellationToken cancellationToken) =>
        JsonSerializer.Deserialize<T>(
            await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false),
            JsonOptions) ?? throw new InvalidDataException($"'{path}' is empty.");

    internal static async Task<string> RequireDigestAsync(
        string path,
        string expected,
        CancellationToken cancellationToken)
    {
        string actual = await JsonArtifactDigest.ComputeFileAsync(path, cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Artifact digest mismatch for '{path}'.");
        }

        return actual;
    }

    internal static string RequireContainedPath(string root, string path)
    {
        string rootPath = Path.GetFullPath(root);
        string candidate = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(rootPath, path.Replace('/', Path.DirectorySeparatorChar)));
        string prefix = rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? rootPath
            : rootPath + Path.DirectorySeparatorChar;
        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!candidate.StartsWith(prefix, comparison))
        {
            throw new InvalidDataException($"Validation path '{path}' escapes the repository root.");
        }

        return candidate;
    }
}
