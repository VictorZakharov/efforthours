using System.Text;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.EndToEndTests;

public sealed partial class SnapshotPortfolioCliTests : ChangeCliTestSupport
{
    private const string VendorSource = "public class Vendored { public int A(int x) => x + 1; public int B(int x) => x * 2; " +
        "public int C(int x) => x - 3; public string D => \"vendor\"; }\n";

    [Fact]
    public async Task ExcludedPathsAreNeverAdmittedAtAnySnapshotAreaOrDayAndAreRecordedOnWholeReceipts()
    {
        using GitFixture vendored = await SnapshotFixtureAsync();
        vendored.WriteText("Packs/Forest/Vendored.cs", VendorSource);
        vendored.WriteText("Packs/Forest/README.md", "# Third-party pack\n");
        _ = await SnapshotCommitAtAsync(vendored, "add pack", "2026-02-10T12:00:00Z");
        using GitFixture authored = await SnapshotFixtureAsync();
        _ = await SnapshotCommitAtAsync(authored, "same authored state", "2026-02-10T12:00:00Z", allowEmpty: true);

        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, vendored.RootPath);
        string authoredRoot = Path.Combine(execution.RootPath, "authored");
        Directory.CreateDirectory(authoredRoot);
        (string authoredManifest, string authoredLocal, string authoredCheckpoint) = WriteSnapshotInputs(authoredRoot, authored.RootPath);
        ProcessResult plain = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(plain.ExitCode == 0, plain.StandardError);
        Assert.DoesNotContain("exclusion", plain.StandardOutput, StringComparison.OrdinalIgnoreCase);
        SnapshotPortfolioReport unexcluded = ContractJson.Deserialize<SnapshotPortfolioReport>(plain.StandardOutput);

        SnapshotPortfolioManifest definition = ContractJson.Deserialize<SnapshotPortfolioManifest>(await File.ReadAllTextAsync(manifest, Encoding.UTF8));
        definition = definition with { Projects = [definition.Projects[0] with { Exclude = ["Packs/**"] }] };
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(definition), Encoding.UTF8);
        ProcessResult measured = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(measured.ExitCode == 0, measured.StandardError);
        SchemaValidationResult schema = ContractSchemaValidator.Validate("snapshot-portfolio-report.schema.json", measured.StandardOutput);
        Assert.True(schema.IsValid, string.Join(Environment.NewLine, schema.Errors));
        SnapshotPortfolioReport excluded = ContractJson.Deserialize<SnapshotPortfolioReport>(measured.StandardOutput);
        ProcessResult reference = await SnapshotRunAsync(authoredManifest, authoredLocal, authoredCheckpoint);
        Assert.True(reference.ExitCode == 0, reference.StandardError);
        SnapshotPortfolioReport own = ContractJson.Deserialize<SnapshotPortfolioReport>(reference.StandardOutput);

        Assert.Equal(unexcluded.MeasurementEpoch, excluded.MeasurementEpoch);
        Assert.NotEqual(unexcluded.ManifestDigest, excluded.ManifestDigest);
        Assert.NotEqual(unexcluded.SemanticDigest, excluded.SemanticDigest);
        SnapshotPeriodResult february = excluded.Projects[0].Periods.Single(p => p.Id == "2026-02");
        SnapshotPeriodResult january = excluded.Projects[0].Periods.Single(p => p.Id == "2026-01");
        SnapshotMeasurementReceipt whole = excluded.Receipts.Single(r => r.Id == february.WholeReceiptId);
        Assert.NotNull(whole.Measurement.ExclusionDigest);
        Assert.Equal(["Packs/**"], whole.Exclusion!.Exclude);
        Assert.Equal(2, whole.Exclusion.ExcludedFileCount);
        Assert.Equal(Encoding.UTF8.GetByteCount(VendorSource) + Encoding.UTF8.GetByteCount("# Third-party pack\n"), whole.Exclusion.ExcludedBytes);
        // January predates the pack: the same exclusion applies and records zero excluded files.
        Assert.Equal(0, excluded.Receipts.Single(r => r.Id == january.WholeReceiptId).Exclusion!.ExcludedFileCount);
        Assert.All(excluded.Receipts.Where(r => r.Exclusion is null), r => Assert.NotNull(r.Measurement.ExclusionDigest));

        SnapshotPeriodResult ownFebruary = own.Projects[0].Periods.Single(p => p.Id == "2026-02");
        SnapshotMeasurementReceipt ownWhole = own.Receipts.Single(r => r.Id == ownFebruary.WholeReceiptId);
        Assert.Equal(ownWhole.Hours, whole.Hours);
        Assert.Equal(ownWhole.InputDigest, whole.InputDigest);
        Assert.Equal(ownWhole.SelectedFileCount, whole.SelectedFileCount);
        Assert.Equal(ownFebruary.Areas.Select(a => (a.Id, a.InputDigest, a.OwnedFileCount, a.AllocatedExpectedHours)),
            february.Areas.Select(a => (a.Id, a.InputDigest, a.OwnedFileCount, a.AllocatedExpectedHours)));
        Assert.Equal(whole.SelectedFileCount, february.Areas.Sum(a => a.OwnedFileCount));
        Assert.Equal(february.Hours!.Expected, february.Areas.Sum(a => a.AllocatedExpectedHours));
        Assert.True(unexcluded.Projects[0].Periods.Single(p => p.Id == "2026-02").Hours!.Expected > february.Hours.Expected);

        ProcessResult warm = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.True(warm.ExitCode == 0, warm.StandardError);
        SnapshotPortfolioReport reused = ContractJson.Deserialize<SnapshotPortfolioReport>(warm.StandardOutput);
        Assert.Equal(0, reused.Telemetry.EstimatorCalls);
        Assert.Equal(0, reused.Telemetry.Exports);
        Assert.Equal(excluded.SemanticDigest, reused.SemanticDigest);

        ProcessResult daily = await SnapshotRunAsync(manifest, local, checkpoint, "--calendar", "daily");
        Assert.True(daily.ExitCode == 0, daily.StandardError);
        SnapshotPortfolioReport days = ContractJson.Deserialize<SnapshotPortfolioReport>(daily.StandardOutput);
        Assert.Equal(february.Hours, days.Projects[0].MonthlyEndpoints!.Single(p => p.Id == "2026-02").Hours);
        Assert.All(days.Receipts, r => Assert.Equal(whole.Measurement.ExclusionDigest, r.Measurement.ExclusionDigest));
        Assert.DoesNotContain(vendored.RootPath, daily.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExcludingEveryArchivedFileFailsLoudlyInsteadOfEstimatingZero()
    {
        using GitFixture repository = await SnapshotFixtureAsync();
        using GitFixture execution = await GitFixture.CreateAsync();
        (string manifest, string local, string checkpoint) = WriteSnapshotInputs(execution.RootPath, repository.RootPath);
        SnapshotPortfolioManifest definition = ContractJson.Deserialize<SnapshotPortfolioManifest>(await File.ReadAllTextAsync(manifest, Encoding.UTF8));
        SnapshotPortfolioManifest everything = definition with { Projects = [definition.Projects[0] with
        {
            Exclude = ["src", "*.cs", "*.csproj", "*.md", ".gitignore", ".hidden"],
        }] };
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(everything), Encoding.UTF8);
        ProcessResult plan = await SnapshotRunAsync(manifest, local, checkpoint, "--preflight");
        Assert.True(plan.ExitCode == 0, plan.StandardError);
        Assert.Contains(ContractJson.Deserialize<SnapshotPortfolioReport>(plan.StandardOutput).Projects[0].Periods,
            p => p.PlanningIssue == "excluded-all-files");
        ProcessResult failed = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.NotEqual(0, failed.ExitCode);
        Assert.Contains("excluded-all-files", failed.StandardOutput, StringComparison.Ordinal);

        SnapshotPortfolioManifest unsafeGlob = definition with { Projects = [definition.Projects[0] with { Exclude = ["../outside/**"] }] };
        await File.WriteAllTextAsync(manifest, ContractJson.SerializeDocument(unsafeGlob), Encoding.UTF8);
        ProcessResult rejected = await SnapshotRunAsync(manifest, local, checkpoint);
        Assert.Equal(3, rejected.ExitCode);
        Assert.Contains("Exclusion globs", rejected.StandardError, StringComparison.Ordinal);
    }
}
