using System.Text.Json;
using System.Text.Json.Serialization;

namespace EffortHours.RepositoryCalibration;

internal static partial class RepositoryCalibrationReproducer
{
    public const string ToolVersion = "repository-calibration-reproducer/0.1.0";

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static readonly HttpClient HttpClient = CreateHttpClient();

    public static async Task RunAsync(
        ReproductionOptions options,
        TextWriter diagnostics,
        CancellationToken cancellationToken)
    {
        ValidateInputPaths(options);
        byte[] planBytes = await File.ReadAllBytesAsync(options.PlanPath, cancellationToken)
            .ConfigureAwait(false);
        SamplingPlan plan = JsonSerializer.Deserialize<SamplingPlan>(planBytes, JsonOptions)
            ?? throw new InvalidDataException("Sampling plan is empty.");
        ValidatePlan(plan);

        Directory.CreateDirectory(options.WorkspacePath);
        Directory.CreateDirectory(options.PacketDirectory);
        List<ReproductionFamily> reproduced = [];
        foreach (SamplingFamily family in plan.Families)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await diagnostics.WriteLineAsync($"Verifying {family.RepositoryName} at {family.SourceSnapshot.CommitSha}.")
                .ConfigureAwait(false);
            reproduced.Add(await ReproduceFamilyAsync(
                options,
                plan.SizeMetric,
                family,
                diagnostics,
                cancellationToken).ConfigureAwait(false));
        }

        ReproductionManifest manifest = new()
        {
            Id = plan.Id,
            Version = "0.2.0",
            SamplingPlan = new ReproductionPlanReference
            {
                Id = plan.Id,
                Version = plan.Version,
                Digest = Sha256(planBytes),
            },
            Profile = plan.Profile,
            Families = reproduced,
        };
        string json = JsonSerializer.Serialize(manifest, JsonOptions) + Environment.NewLine;
        Directory.CreateDirectory(Path.GetDirectoryName(options.OutputPath)!);
        await File.WriteAllTextAsync(options.OutputPath, json, cancellationToken).ConfigureAwait(false);
        await HoldoutCustodyWriter.WriteAsync(
            manifest,
            Sha256(System.Text.Encoding.UTF8.GetBytes(json)),
            options.CustodyPath,
            cancellationToken).ConfigureAwait(false);
        await diagnostics.WriteLineAsync(
            $"Wrote {reproduced.Count} verified families; " +
            $"{reproduced.Count(item => item.Analysis is not null)} development packets and " +
            "holdout source custody were published.")
            .ConfigureAwait(false);
    }

    private static async Task<ReproductionFamily> ReproduceFamilyAsync(
        ReproductionOptions options,
        SamplingSizeMetric metric,
        SamplingFamily family,
        TextWriter diagnostics,
        CancellationToken cancellationToken)
    {
        GitTreeResponse tree = await LoadAndVerifyTreeAsync(
            options.GitHubCliPath,
            family,
            cancellationToken).ConfigureAwait(false);
        ValidateSizeMetric(metric, family, tree.Tree);

        string slug = Slug(family.RepositoryName);
        string archiveDirectory = Path.Combine(options.WorkspacePath, "archives");
        string snapshotDirectory = Path.Combine(options.WorkspacePath, "snapshots");
        Directory.CreateDirectory(archiveDirectory);
        Directory.CreateDirectory(snapshotDirectory);
        string archivePath = Path.Combine(
            archiveDirectory,
            $"{slug}-{family.SourceSnapshot.CommitSha}.zip");
        await DownloadArchiveAsync(family.SourceSnapshot.ArchiveUrl, archivePath, cancellationToken)
            .ConfigureAwait(false);
        string archiveSha256 = await Sha256FileAsync(archivePath, cancellationToken).ConfigureAwait(false);
        string snapshotPath = Path.Combine(snapshotDirectory, slug, family.SourceSnapshot.CommitSha);
        await SnapshotArchiveVerifier.VerifyAndExtractAsync(
            archivePath,
            snapshotPath,
            tree.Tree,
            (entry, token) => LoadExactBlobAsync(
                options.GitHubCliPath,
                family.RepositoryName,
                entry,
                token),
            cancellationToken).ConfigureAwait(false);
        await VerifyLicenseAsync(snapshotPath, family, tree.Tree, cancellationToken).ConfigureAwait(false);

        DevelopmentAnalysis? analysis = null;
        string analysisStatus = "withheld-not-run";
        if (string.Equals(family.Partition, "development", StringComparison.Ordinal))
        {
            await diagnostics.WriteLineAsync($"Generating blind development packet for {family.RepositoryName}.")
                .ConfigureAwait(false);
            analysis = await AnalyzeDevelopmentAsync(
                options,
                family,
                snapshotPath,
                slug,
                cancellationToken).ConfigureAwait(false);
            analysisStatus = "generated-blind-development";
        }

        GitTreeEntry[] blobs = [.. tree.Tree.Where(item => item.Type == "blob")];
        return new ReproductionFamily
        {
            Id = family.Id,
            RepositoryName = family.RepositoryName,
            PrimaryStratum = family.PrimaryStratum,
            Partition = family.Partition,
            CommitSha = family.SourceSnapshot.CommitSha,
            GitTreeSha1 = family.SourceSnapshot.GitTreeSha1,
            ArchiveSha256 = archiveSha256,
            ArchiveBytes = new FileInfo(archivePath).Length,
            VerifiedBlobCount = blobs.Length,
            VerifiedBlobBytes = blobs.Sum(item => item.Size ?? 0L),
            SubmoduleCount = tree.Tree.Count(item => item.Type == "commit"),
            LicenseBlobSha1 = family.License.GitBlobSha1,
            LicenseContentSha256 = family.License.ContentSha256,
            VerificationStatus = "verified-commit-tree-blobs-license",
            AnalysisStatus = analysisStatus,
            Analysis = analysis,
        };
    }

    private static void ValidateSizeMetric(
        SamplingSizeMetric metric,
        SamplingFamily family,
        IReadOnlyList<GitTreeEntry> tree)
    {
        GitTreeEntry[] eligible = [.. tree.Where(item =>
            item.Type == "blob" && SourceSizeMetric.IsEligible(item.Path, metric))];
        int files = eligible.Length;
        long bytes = eligible.Sum(item => item.Size ?? 0L);
        if (family.Size.Metric != metric.Id ||
            files != family.Size.EligibleFiles ||
            bytes != family.Size.EligibleBytes)
        {
            throw new InvalidDataException(
                $"Frozen source-size measurement does not reproduce for {family.RepositoryName}.");
        }
    }

    private static void ValidateInputPaths(ReproductionOptions options)
    {
        if (!File.Exists(options.PlanPath))
        {
            throw new FileNotFoundException("Sampling plan was not found.", options.PlanPath);
        }

        if (!File.Exists(options.CliPath))
        {
            throw new FileNotFoundException("EffortHours CLI assembly was not found.", options.CliPath);
        }

        string workspace = Path.GetFullPath(options.WorkspacePath);
        if (Path.GetPathRoot(workspace) == workspace)
        {
            throw new InvalidDataException("Workspace must not be a filesystem root.");
        }
    }

    private static void ValidatePlan(SamplingPlan plan)
    {
        if (plan.SamplingPlanVersion != "repository-sampling-plan/1.0.0" ||
            plan.Id != "efforthours-public-readiness" ||
            plan.Version != "0.1.0" ||
            plan.Profile != "implementation" ||
            plan.Families.Count != 33 ||
            plan.Families.Any(family =>
                !family.SourceSnapshot.TreeListingComplete ||
                !family.License.RedistributionAllowed))
        {
            throw new InvalidDataException("Sampling plan does not match the frozen public-readiness boundary.");
        }
    }

    private static string Slug(string repositoryName) => string.Concat(
        repositoryName.ToLowerInvariant().Select(character =>
            char.IsAsciiLetterOrDigit(character) ? character : '-')).Trim('-');

    private static JsonSerializerOptions CreateJsonOptions() => new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

}
