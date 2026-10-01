using System.Globalization;
using System.Text;
using EffortHours.Change;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Reporting;

namespace EffortHours.Cli;

internal static class SnapshotDashboardAdapterCommand
{
    public static async Task<int> ExecuteAsync(string[] args, TextWriter stdout, TextWriter stderr, CancellationToken token)
    {
        if (args.Any(a => a is "--help" or "-h"))
        {
            await stdout.WriteLineAsync("Usage: eh portfolio-adapter --input <complete-result.json> --studies <authored-studies.json> --output <asset.json>\n" +
                "Emits one deterministic numerical asset with pinned links and documentation tables; review stale studies before publication.").ConfigureAwait(false);
            return CliExitCodes.Success;
        }
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i++)
        {
            string key = args[i];
            if (key is not ("--input" or "--studies" or "--output") || i + 1 >= args.Length || !values.TryAdd(key, args[++i]))
                throw new InvalidDataException("Adapter requires unique --input, --studies, and --output options.");
        }
        if (values.Count != 3) throw new InvalidDataException("Adapter requires --input, --studies, and --output.");
        SnapshotPortfolioReport report = await SnapshotPortfolioCommand.ReadReportAsync(values["--input"], token).ConfigureAwait(false);
        SnapshotDashboardStudies studies = await SnapshotPortfolioCommand.ReadAsync<SnapshotDashboardStudies>(values["--studies"],
            "snapshot-dashboard-studies.schema.json", token).ConfigureAwait(false);
        if (studies.Projects.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != studies.Projects.Count ||
            !studies.Projects.Select(p => p.Id).ToHashSet(StringComparer.Ordinal).SetEquals(report.Projects.Select(p => p.Id)))
            throw new InvalidDataException("Missing authored studies or unexpected study project IDs.");
        List<SnapshotDashboardProjectAsset> projects = [];
        StringBuilder markdown = new("| Project | Period | Status | Expected EHE (h) |\n| --- | --- | --- | ---: |\n");
        foreach (SnapshotProjectResult project in report.Projects)
        {
            SnapshotDashboardStudy study = studies.Projects.Single(p => p.Id == project.Id);
            if (study.AreasDigest != project.AreasDigest || study.Areas.Select(a => a.Id).Distinct(StringComparer.Ordinal).Count() != study.Areas.Count)
                throw new InvalidDataException("Stale reviewed area boundaries: " + project.Id);
            if (!Uri.TryCreate(study.PublicRepositoryUrl, UriKind.Absolute, out Uri? uri) || uri.Scheme != "https" ||
                uri.Host != "github.com" || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
                uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Length != 2)
                throw new InvalidDataException("Authored public repository links require an explicit HTTPS GitHub repository URL.");
            List<SnapshotDashboardPeriodAsset> periods = [];
            foreach (SnapshotPeriodResult period in project.Periods)
            {
                List<SnapshotDashboardAreaAsset> areas = [];
                if (period.Areas.Count != 0 && !period.Areas.Select(a => a.Id).ToHashSet(StringComparer.Ordinal).SetEquals(study.Areas.Select(a => a.Id)))
                    throw new InvalidDataException("Missing authored area studies: " + project.Id);
                foreach (SnapshotAreaResult area in period.Areas)
                {
                    SnapshotDashboardAreaStudy areaStudy = study.Areas.Single(a => a.Id == area.Id);
                    bool unchangedReviewedInput = project.Periods.Any(old => old.CommitObjectId == areaStudy.ReviewedCommit &&
                        old.Areas.Any(prior => prior.Id == area.Id && prior.ReceiptId == area.ReceiptId));
                    bool latest = ReferenceEquals(period, project.Periods.LastOrDefault(p => p.WholeReceiptId is not null));
                    if ((area.ReviewStatus == "review-required" || latest) && areaStudy.ReviewedCommit != period.CommitObjectId && !unchangedReviewedInput)
                        throw new InvalidDataException("Changed scope requires a reviewed immutable binding: " + project.Id + "/" + area.Id);
                    if (areaStudy.Folder != ".") GitArchiveSnapshot.RequireSafePath(areaStudy.Folder);
                    SnapshotMeasurementReceipt areaReceipt = report.Receipts.Single(r => r.Id == area.ReceiptId);
                    if (!areaReceipt.DirectoryIds.Contains(SnapshotMeasurementIdentity.Digest(areaStudy.Folder), StringComparer.Ordinal))
                        throw new InvalidDataException("Authored folder link is absent from the selected area snapshot: " + project.Id + "/" + area.Id);
                    string folder = areaStudy.Folder == "." ? "" : "/" + string.Join('/', areaStudy.Folder.Split('/').Select(Uri.EscapeDataString));
                    areas.Add(new(area.Id, area.StandaloneExpectedHours, area.AllocatedExpectedHours,
                        study.PublicRepositoryUrl.TrimEnd('/') + "/tree/" + period.CommitObjectId + folder, area.ReceiptId));
                }
                SnapshotMeasurementReceipt? receipt = report.Receipts.FirstOrDefault(r => r.Id == period.WholeReceiptId);
                periods.Add(new()
                {
                    Id = period.Id,
                    Status = period.Status,
                    CommitObjectId = period.CommitObjectId,
                    Hours = period.Hours,
                    Categories = receipt is null ? [] : SnapshotCategoryGrouping.Aggregate(receipt.Categories),
                    Areas = areas,
                });
                markdown.Append(CultureInfo.InvariantCulture, $"| {project.Id} | {period.Id} | {period.Status} | {period.Hours?.Expected.ToString("0.00", CultureInfo.InvariantCulture) ?? "-"} |\n");
            }
            projects.Add(new() { Id = project.Id, Periods = periods });
        }
        markdown.Append("\nReplacement effort; experimental and uncalibrated. Standalone area measurements differ from allocated planning shares.\n");
        SnapshotDashboardAsset asset = new()
        {
            SourceSemanticDigest = report.SemanticDigest,
            MeasurementEpoch = report.MeasurementEpoch,
            DocumentationMarkdown = markdown.ToString(),
            Projects = projects,
        };
        string json = ContractJson.SerializeDocument(asset);
        SchemaValidationResult schema = ContractSchemaValidator.Validate("snapshot-dashboard-asset.schema.json", json);
        if (!schema.IsValid) throw new InvalidDataException("Dashboard asset failed schema validation.");
        await SnapshotPortfolioStore.AtomicWriteAsync(values["--output"], json, token).ConfigureAwait(false);
        await stderr.WriteLineAsync("Validated portfolio numerical asset and pinned links published.").ConfigureAwait(false);
        return CliExitCodes.Success;
    }
}
