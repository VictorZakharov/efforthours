using System.Text.RegularExpressions;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public static partial class SnapshotPortfolioValidation
{
    public static void RequireId(string value)
    {
        if (value.Length is < 1 or > 80 || !MyRegex().IsMatch(value))
            throw new InvalidDataException("Public IDs require 1 to 80 lowercase letters, digits, dots, or hyphens.");
    }

    public static void Validate(SnapshotPortfolioManifest manifest)
    {
        if (manifest.SchemaVersion != ContractVersions.V1 || manifest.ProtocolVersion != SnapshotPortfolioVersions.Manifest ||
            manifest.SnapshotPolicy != SnapshotPortfolioVersions.Snapshot || manifest.BaselineConvention != "january-1-zero" ||
            manifest.Year is < 1970 or > 9998 || !Enum.IsDefined(manifest.Profile))
            throw new InvalidDataException("Unsupported snapshot portfolio version, year, profile, or policy.");
        if (manifest.Timezone != "UTC" && !TimeZoneInfo.TryConvertIanaIdToWindowsId(manifest.Timezone, out _))
            throw new InvalidDataException("Timezone must be an IANA ID or UTC.");
        _ = TimeZoneInfo.FindSystemTimeZoneById(manifest.Timezone);
        if (manifest.Projects.Count is < 1 or > 256 || manifest.Projects.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != manifest.Projects.Count)
            throw new InvalidDataException("Manifest requires 1 to 256 uniquely identified projects.");
        foreach (SnapshotProjectDefinition project in manifest.Projects)
        {
            RequireId(project.Id);
            if (string.IsNullOrWhiteSpace(project.Ref) || project.Ref.StartsWith('-') || project.Ref.Any(char.IsControl))
                throw new InvalidDataException("Each project requires an explicit safe Git ref.");
            SnapshotAreaPartition.ValidateDefinitions(project.Areas);
            if (project.VendorManifest is not null) _ = ReviewedVendorManifestValidation.ComputeDigest(project.VendorManifest);
        }
    }

    public static void Validate(SnapshotMeasurementReceipt receipt)
    {
        if (receipt.SchemaVersion != ContractVersions.V1 || receipt.ProtocolVersion != SnapshotPortfolioVersions.Receipt ||
            receipt.Maturity != "experimental-uncalibrated" || receipt.Id != ReceiptId(receipt) ||
            string.IsNullOrWhiteSpace(receipt.ProducerVersion) || receipt.SelectedFileCount < 1 || receipt.ContextFileCount < 0)
            throw new InvalidDataException("Measurement receipt is incomplete, corrupt, or unsupported.");
        RequireDigest(receipt.InputDigest);
        RequireDigest(receipt.EvidenceDigest);
        if (receipt.DirectoryIds.Distinct(StringComparer.Ordinal).Count() != receipt.DirectoryIds.Count ||
            receipt.MaintainedBodies.Select(b => b.Digest).Distinct(StringComparer.Ordinal).Count() != receipt.MaintainedBodies.Count)
            throw new InvalidDataException("Receipt content/folder fingerprints are duplicated.");
        foreach (string directory in receipt.DirectoryIds) RequireDigest(directory);
        foreach (SnapshotBodyFingerprint body in receipt.MaintainedBodies)
        {
            RequireDigest(body.Digest);
            if (body.Bytes < 0) throw new InvalidDataException("Negative body bytes.");
        }
        Validate(receipt.Measurement);
        RequireRange(receipt.Hours);
        if (receipt.Categories.Select(c => c.Category).Distinct().Count() != receipt.Categories.Count)
            throw new InvalidDataException("Receipt category totals are duplicated.");
        foreach (CategoryEstimate category in receipt.Categories) RequireRange(category.Hours);
        if (receipt.Categories.Sum(c => c.Hours.Low) != receipt.Hours.Low ||
            receipt.Categories.Sum(c => c.Hours.Expected) != receipt.Hours.Expected ||
            receipt.Categories.Sum(c => c.Hours.High) != receipt.Hours.High)
            throw new InvalidDataException("Receipt categories do not reconcile to whole measurement.");
    }

    public static void Validate(MeasurementIdentity measurement)
    {
        if (measurement.Contract != SnapshotPortfolioVersions.Receipt || measurement.Normalization != "repository-evidence/1.0.0" ||
            measurement.InventoryPolicy != "archive-relative-inventory/1.0.0" || measurement.IgnorePolicy != "git-and-efforthours-ignore/1.0.0" ||
            measurement.SnapshotPolicy != SnapshotPortfolioVersions.Snapshot || measurement.AreaPolicy != SnapshotPortfolioVersions.Areas ||
            measurement.AnalysisOptions != "default-static/1.0.0" || !Enum.IsDefined(measurement.Profile))
            throw new InvalidDataException("Unsupported measurement semantics.");
        RequireDigest(measurement.ModelDigest);
        RequireDigest(measurement.ImplementationDigest);
        if (measurement.OwnershipDigest is not null) RequireDigest(measurement.OwnershipDigest);
    }

    public static void Validate(SnapshotPortfolioReport report)
    {
        if (report.SchemaVersion != ContractVersions.V1 || report.ProtocolVersion != SnapshotPortfolioVersions.Report ||
            report.Status != "complete" || report.CategoryMapping != SnapshotPortfolioVersions.Categories ||
            report.SemanticDigest != ReportDigest(report) || report.Projects.Count is < 1 or > 256)
            throw new InvalidDataException("Portfolio result is incomplete, corrupt, or unsupported.");
        RequireDigest(report.MeasurementEpoch);
        RequireDigest(report.ManifestDigest);
        if (report.Year is < 1970 or > 9998) throw new InvalidDataException("Invalid portfolio year.");
        TimeZoneInfo timezone = TimeZoneInfo.FindSystemTimeZoneById(report.Timezone);
        IReadOnlyList<SnapshotPeriodResult> calendar = SnapshotPortfolioSelection.Select(report.Year, timezone, report.AsOf, []);
        Dictionary<string, SnapshotMeasurementReceipt> receipts = new(StringComparer.Ordinal);
        foreach (SnapshotMeasurementReceipt receipt in report.Receipts)
        {
            Validate(receipt);
            if (SnapshotMeasurementIdentity.Digest(receipt.Measurement with { OwnershipDigest = null }) != report.MeasurementEpoch)
                throw new InvalidDataException("Mixed measurement epochs require a separately versioned consumer contract.");
            if (!receipts.TryAdd(receipt.Id, receipt)) throw new InvalidDataException("Duplicate receipt IDs.");
        }
        if (report.Projects.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != report.Projects.Count)
            throw new InvalidDataException("Duplicate public project IDs.");
        foreach (SnapshotProjectResult project in report.Projects)
        {
            RequireId(project.Id);
            if (project.HeadObjectId is null || project.FirstAvailableCommitAt is null || project.ShallowHistory || project.PlanningIssue is not null)
                throw new InvalidDataException("Complete projects require full immutable first-parent provenance.");
            RequireObject(project.HeadObjectId);
            RequireDigest(project.AreasDigest);
            if (project.Periods.Count != 13 || project.Periods.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != 13)
                throw new InvalidDataException("Annual portfolio requires one baseline and twelve distinct months.");
            foreach (SnapshotPeriodResult period in project.Periods)
            {
                SnapshotPeriodResult? expectedPeriod = calendar.FirstOrDefault(p => p.Id == period.Id);
                if (expectedPeriod is null || expectedPeriod.Cutoff != period.Cutoff ||
                    expectedPeriod.Status == "future" && period.Status != "future" ||
                    expectedPeriod.Status == "baseline-zero" && period.Status != "baseline-zero")
                    throw new InvalidDataException("Portfolio calendar/cutoff identity is invalid.");
                if (period.Status is "future")
                {
                    if (period.Hours is not null || period.CommitObjectId is not null || period.Areas.Count != 0)
                        throw new InvalidDataException("Future periods must have null effort and no measurements.");
                    continue;
                }
                if (period.Status is "baseline-zero" or "assumed-zero")
                {
                    if (period.Hours != SnapshotPortfolioSelection.Zero || period.WholeReceiptId is not null || period.Areas.Count != 0)
                        throw new InvalidDataException("Explicit zero periods cannot contain measurements.");
                    continue;
                }
                if (period.Status is not ("complete" or "partial") || period.CommitObjectId is null || period.TreeObjectId is null ||
                    period.CommitAt is null || period.CommitAt >= period.Cutoff || period.WholeReceiptId is null ||
                    !receipts.TryGetValue(period.WholeReceiptId, out SnapshotMeasurementReceipt? whole) || period.Hours != whole.Hours)
                    throw new InvalidDataException("Snapshot provenance or whole-project receipt is invalid.");
                RequireObject(period.CommitObjectId);
                RequireObject(period.TreeObjectId);
                if (report.RateCard is null && period.TotalCost is not null || report.RateCard is not null &&
                    (period.TotalCost is null || period.TotalCost.Low != decimal.Round(whole.Hours.Low * report.RateCard.HourlyRate, 2) ||
                     period.TotalCost.Expected != decimal.Round(whole.Hours.Expected * report.RateCard.HourlyRate, 2) ||
                     period.TotalCost.High != decimal.Round(whole.Hours.High * report.RateCard.HourlyRate, 2) ||
                     period.TotalCost.Currency != report.RateCard.Currency))
                    throw new InvalidDataException("Pricing must be an independent exact projection of EHE.");
                if (period.Areas.Count == 0 || period.Areas.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() != period.Areas.Count ||
                    period.Areas.Sum(a => a.AllocatedExpectedHours) != whole.Hours.Expected ||
                    period.Areas.Sum(a => a.OwnedFileCount) != whole.SelectedFileCount)
                    throw new InvalidDataException("Area coverage/allocation does not reconcile.");
                foreach (SnapshotAreaResult area in period.Areas)
                {
                    RequireId(area.Id);
                    if (!receipts.TryGetValue(area.ReceiptId, out SnapshotMeasurementReceipt? standalone) ||
                        area.StandaloneExpectedHours != standalone.Hours.Expected || area.InputDigest != standalone.InputDigest ||
                        area.OwnedFileCount != standalone.SelectedFileCount || area.ContextFileCount != standalone.ContextFileCount ||
                        area.AllocatedExpectedHours < 0 || SnapshotMeasurementIdentity.Digest(standalone.Measurement) != SnapshotMeasurementIdentity.Digest(whole.Measurement))
                        throw new InvalidDataException("Standalone area receipt or compatibility is invalid.");
                }
                IReadOnlyList<decimal> expected = SnapshotAreaPartition.Allocate(whole.Hours.Expected,
                    [.. period.Areas.Select(a => a.StandaloneExpectedHours)]);
                if (!expected.SequenceEqual(period.Areas.Select(a => a.AllocatedExpectedHours)))
                    throw new InvalidDataException("Area allocations differ from deterministic largest remainders.");
            }
        }
    }

    public static string ReceiptId(SnapshotMeasurementReceipt receipt) => SnapshotMeasurementIdentity.Digest(receipt with { Id = "" });
    public static string ReportDigest(SnapshotPortfolioReport report) => SnapshotMeasurementIdentity.Digest(report with
    {
        SemanticDigest = "",
        Telemetry = new(),
        Projects = [.. report.Projects.Select(p => p with
        {
            Periods = [.. p.Periods.Select(period => period with
            {
                CacheDisposition = "not-requested",
                PreviousExpectedHours = null,
                Areas = [.. period.Areas.Select(a => a with { PreviousExpectedHours = null, ReviewStatus = "reviewed-boundary" })],
            })],
        })],
    });

    private static void RequireRange(EffortRange range)
    {
        if (range.Low < 0 || range.Expected < range.Low || range.High < range.Expected ||
            new[] { range.Low, range.Expected, range.High }.Any(h => h != decimal.Round(h, 2)))
            throw new InvalidDataException("Effort ranges must be ordered nonnegative cent-hours.");
    }
    private static void RequireDigest(string value)
    {
        if (!MyRegex1().IsMatch(value))
            throw new InvalidDataException("Invalid SHA-256 identity.");
    }
    private static void RequireObject(string value)
    {
        if (value.Length is not (40 or 64) || value.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidDataException("Invalid immutable Git object identity.");
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex MyRegex();
    [GeneratedRegex("^sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex MyRegex1();
}
