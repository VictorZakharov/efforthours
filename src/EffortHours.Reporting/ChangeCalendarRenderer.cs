using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Reporting;

/// <summary>Offline views of exact reconciled repository/day allocations, with reference ratios.</summary>
public static partial class ChangeCalendarRenderer
{
    public static string Render(ChangePortfolioComparisonReport report,
        IReadOnlyList<ChangePortfolioComparisonSeries> projects, bool html)
    {
        if (ContractValidation.Validate(report).Count != 0) throw new ArgumentException("Invalid source comparison.", nameof(report));
        if (report.Status != ChangePortfolioComparisonStatus.Complete)
        {
            string failures = string.Join("\n", report.Execution.Failures.Select(f =>
                $"Project {f.RepositoryId}: phase {f.Phase}, category {f.Category}, diagnostic {f.MessageDigest}."));
            return html ? "<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><title>Incomplete EHE calendar</title><h1>Incomplete EHE calendar</h1><p>No aggregate or graph is available. Review diagnostics and rerun with the same checkpoint.</p><pre>" + WebUtility.HtmlEncode(failures) + "</pre></html>\n"
                : "# Incomplete EHE calendar\n\nNo aggregate or graph is available. Review diagnostics and rerun with the same checkpoint.\n\n" + failures + "\n";
        }
        ChangePortfolioComparisonSeries portfolio = report.Series.Single(s => s.Kind == ChangePortfolioSeriesKind.Portfolio);
        ValidateProjects(report, projects, portfolio);
        return html ? Html(report, projects, portfolio) : Text(report, projects, portfolio);
    }

    private static void ValidateProjects(ChangePortfolioComparisonReport report,
        IReadOnlyList<ChangePortfolioComparisonSeries> projects, ChangePortfolioComparisonSeries portfolio)
    {
        if (!projects.Select(p => p.Id).ToHashSet(StringComparer.Ordinal).SetEquals(report.SourcePortfolio!.Aggregation!.Repositories.Select(r => r.RepositoryId)) ||
            projects.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != projects.Count ||
            projects.Any(p => p.Points.Count != report.Buckets.Count)) throw new ArgumentException("Invalid project projection.");
        for (int i = 0; i < report.Buckets.Count; i++)
            if (projects.Any(p => p.Points[i].BucketId != report.Buckets[i].Id) ||
                ContractValidation.Sum(projects.Select(p => p.Points[i].Effort)) != portfolio.Points[i].Effort)
                throw new ArgumentException("Project cells must reconcile exactly to the canonical calendar.");
    }

    private static string Text(ChangePortfolioComparisonReport report,
        IReadOnlyList<ChangePortfolioComparisonSeries> projects, ChangePortfolioComparisonSeries portfolio)
    {
        StringBuilder text = new();
        text.AppendLine("# Daily Change EHE calendar").AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"Timezone: {report.Selection.AuthorPeriodManifest!.TimeZone}. Expected EHE: {N(portfolio.TotalEffort.Expected)} hours " +
            $"(planning range {N(portfolio.TotalEffort.Low)}–{N(portfolio.TotalEffort.High)}). Reference ratio: {N(portfolio.TotalEffort.Expected / portfolio.TotalCapacityHours!.Value)}×.");
        text.AppendLine(CultureInfo.InvariantCulture, $"Reference: {N(portfolio.Points[0].CapacityHours!.Value)} hours per calendar day, including idle days. Total denominator: {N(portfolio.TotalCapacityHours.Value)} hours.").AppendLine();
        text.AppendLine("| Date | Expected EHE hours | Low | High | × EHE | Selected changes |").AppendLine("| --- | ---: | ---: | ---: | ---: | ---: |");
        for (int i = 0; i < report.Buckets.Count; i++)
        {
            ChangePortfolioComparisonPoint p = portfolio.Points[i];
            text.AppendLine(CultureInfo.InvariantCulture, $"| {report.Buckets[i].Label} | {N(p.Effort.Expected)} | {N(p.Effort.Low)} | {N(p.Effort.High)} | {N(p.Effort.Expected / p.CapacityHours!.Value)} | {p.SelectedChangeCount} |");
        }
        text.AppendLine().AppendLine("## Projects").AppendLine();
        foreach (ChangePortfolioComparisonSeries project in projects)
            text.AppendLine(CultureInfo.InvariantCulture, $"- {project.Id}: {N(project.TotalEffort.Expected)} expected EHE hours.");
        text.AppendLine().AppendLine(Measurement(report)).AppendLine(Limits).AppendLine();
        text.AppendLine("Source semantic digest: " + report.Verification.SemanticDigest);
        text.AppendLine("Estimator: " + report.EstimatorVersion);
        return text.ToString().ReplaceLineEndings("\n");
    }

    private static string Html(ChangePortfolioComparisonReport report,
        IReadOnlyList<ChangePortfolioComparisonSeries> projects, ChangePortfolioComparisonSeries portfolio)
    {
        string data = JsonSerializer.Serialize(new
        {
            dates = report.Buckets.Select(b => b.Label),
            capacity = portfolio.Points.Select(p => p.CapacityHours),
            projects = projects.Select(p => new
            {
                id = p.Id,
                points = p.Points.Select(v => new
                { low = v.Effort.Low, expected = v.Effort.Expected, high = v.Effort.High, changes = v.SelectedChangeCount })
            }),
        });
        string script = "const data=" + data + ";\n" + Script;
        string hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(script)));
        StringBuilder page = new();
        page.Append("<!doctype html>\n<html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        page.Append("<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'unsafe-inline'; script-src 'sha256-")
            .Append(hash).Append("'; base-uri 'none'; form-action 'none'\"><title>Daily Change EHE calendar</title><style>")
            .Append(Styles).Append("</style></head><body><main>");
        page.Append("<header><p class=\"eyebrow\">EFFORTHOURS · EXPERIMENTAL</p><h1>Daily Change EHE calendar</h1><p>")
            .Append(WebUtility.HtmlEncode(report.Selection.AuthorPeriodManifest!.TimeZone)).Append(" · ")
            .Append(WebUtility.HtmlEncode(report.Buckets[0].Label + " to " + report.Buckets[^1].Label)).Append("</p></header>");
        page.Append("<section class=\"summary\"><div><strong id=\"total\">").Append(N(portfolio.TotalEffort.Expected))
            .Append(" hours</strong><p id=\"range\">Planning range ").Append(N(portfolio.TotalEffort.Low)).Append('–')
            .Append(N(portfolio.TotalEffort.High)).Append(" hours</p></div><div class=\"units\"><button id=\"hours\" aria-pressed=\"true\">Hours</button>")
            .Append("<button id=\"ratio\" aria-pressed=\"false\">× EHE</button></div></section>");
        page.Append("<p>Engineering represented by selected committed changes. Select a date to explore.</p><div class=\"layout\"><section aria-label=\"Daily heatmap\">")
            .Append("<div class=\"scroll\"><div id=\"calendar\"></div></div><p class=\"legend\">Less <span>▪ ▪ ▪ ▪ ▪</span> More · color is relative to selected projects</p>")
            .Append("<div id=\"detail\" aria-live=\"polite\">Select a date for its planning range and projects.</div></section>")
            .Append("<aside><h2>Show on calendar</h2><button id=\"all\">All</button> <button id=\"none\">None</button><div id=\"projects\"></div></aside></div>");
        page.Append("<p class=\"assumption\">× EHE = expected Change EHE ÷ ").Append(N(portfolio.Points[0].CapacityHours!.Value))
            .Append(" reference hours per calendar day, including idle days. The overall ratio uses total EHE ÷ total reference hours; selecting projects does not multiply the denominator.</p>");
        page.Append("<details><summary>Daily values (also available without JavaScript)</summary><table><thead><tr><th>Date</th><th>Expected hours</th><th>Low</th><th>High</th><th>× EHE</th></tr></thead><tbody id=\"values\">");
        for (int i = 0; i < report.Buckets.Count; i++)
        {
            ChangePortfolioComparisonPoint point = portfolio.Points[i];
            page.Append("<tr><th>").Append(WebUtility.HtmlEncode(report.Buckets[i].Label)).Append("</th><td>")
                .Append(N(point.Effort.Expected)).Append("</td><td>").Append(N(point.Effort.Low)).Append("</td><td>")
                .Append(N(point.Effort.High)).Append("</td><td>").Append(N(point.Effort.Expected / point.CapacityHours!.Value)).Append("</td></tr>");
        }
        page.Append("</tbody></table></details><noscript><p>Enable JavaScript for project filters and the interactive calendar. The complete daily table above remains available.</p></noscript><footer><p>")
            .Append(WebUtility.HtmlEncode(Limits)).Append("</p><p>Source semantic digest: ")
            .Append(WebUtility.HtmlEncode(report.Verification.SemanticDigest)).Append("<br>Estimator: ")
            .Append(WebUtility.HtmlEncode(report.EstimatorVersion)).Append("</p><p>").Append(WebUtility.HtmlEncode(Measurement(report)))
            .Append("</p></footer></main><script>").Append(script).Append("</script></body></html>\n");
        return page.ToString();
    }

    private static string N(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);
    private static string Measurement(ChangePortfolioComparisonReport report) =>
        "Measurement: " + report.Verification.BucketAllocationPolicy + "; date field: " +
        report.Selection.AuthorPeriodManifest!.DateField.ToString().ToLowerInvariant() + ". " +
        (report.SourcePortfolio?.DailyNormalization is null
            ? "Dates are allocations from one joint range."
            : "Each local day is normalized independently; other requested dates do not change its estimate. Values differ from signed replacement-stock growth.");
    private const string Limits = "Experimental and uncalibrated. Change EHE represents counterfactual replacement effort, not actual labor, productivity, an AI skill score, or compensation. " +
        "Coverage includes only selected repositories and changes reachable from pinned heads. Identity and dates select work; they never value it. " +
        "Project values share the declared measurement policy. Cross-project copied code may remain represented separately; selecting fewer projects is a presentation filter. " +
        "Merges are excluded and valid coauthors included. Planning bounds are not probability intervals. No source, raw aliases, or local paths are embedded.";
}
