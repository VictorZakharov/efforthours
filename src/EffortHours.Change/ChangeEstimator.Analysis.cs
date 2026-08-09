using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;

namespace EffortHours.Change;

public sealed partial class ChangeEstimator
{
    private async Task<PairEstimate> AnalyzePairAsync(
        string repositoryName,
        ChangeSelection selection,
        IChangeSnapshot baseSnapshot,
        IChangeSnapshot headSnapshot,
        IReadOnlyList<Diagnostic> selectorDiagnostics,
        EstimationProfile profile,
        CancellationToken cancellationToken)
    {
        RepositoryEvidence baseEvidence = await new RepositoryAnalysisPipeline(baseSnapshot.FileSystem)
            .ScanAsync(baseSnapshot.RootPath, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        RepositoryEvidence headEvidence = await new RepositoryAnalysisPipeline(headSnapshot.FileSystem)
            .ScanAsync(headSnapshot.RootPath, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        baseEvidence = RenameRepository(baseEvidence, repositoryName);
        headEvidence = RenameRepository(headEvidence, repositoryName);
        EstimateReport baseEstimate = _repositoryEstimator.Estimate(baseEvidence, profile);
        EstimateReport headEstimate = _repositoryEstimator.Estimate(headEvidence, profile);
        List<Diagnostic> evidenceDiagnostics = [.. selectorDiagnostics];
        evidenceDiagnostics.AddRange(baseEvidence.Diagnostics);
        evidenceDiagnostics.AddRange(headEvidence.Diagnostics);
        ChangeEvidence evidence = await ChangeEvidenceBuilder.BuildAsync(
            selection,
            baseSnapshot,
            headSnapshot,
            baseEvidence,
            headEvidence,
            evidenceDiagnostics,
            cancellationToken).ConfigureAwait(false);
        int excludedCount = evidence.Paths.Count(path => !path.Represented);
        List<Diagnostic> normalizedDiagnostics = [.. evidence.Diagnostics];
        if (excludedCount > 0)
        {
            normalizedDiagnostics.Add(new Diagnostic
            {
                Code = "FB5200",
                Severity = DiagnosticSeverity.Information,
                Message = $"{excludedCount} final path change(s) were classified as mechanical or excluded body evidence and do not create represented implementation effort.",
                EvidenceIds = [.. evidence.Paths
                    .Where(path => !path.Represented)
                    .Select(path => path.Id)
                    .Order(StringComparer.Ordinal)],
            });
        }

        ChangePathEvidence[] unsupported = [.. evidence.Paths.Where(path =>
            path.Classification == ChangePathClassification.Unsupported)];
        if (unsupported.Length > 0)
        {
            normalizedDiagnostics.Add(new Diagnostic
            {
                Code = "FB5201",
                Severity = DiagnosticSeverity.Warning,
                Message = $"{unsupported.Length} changed link, submodule, or unsupported object path(s) were excluded and require review.",
                EvidenceIds = [.. unsupported.Select(path => path.Id).Order(StringComparer.Ordinal)],
            });
        }

        if (evidence.Paths.Count == 0)
        {
            normalizedDiagnostics.Add(new Diagnostic
            {
                Code = "FB5202",
                Severity = DiagnosticSeverity.Information,
                Message = "Base and head trees are identical; normalized Change EHE is zero.",
            });
        }

        evidence = evidence with
        {
            Diagnostics = [.. normalizedDiagnostics
                .Distinct()
                .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)],
        };
        IReadOnlyList<string> evidenceErrors = ContractValidation.Validate(evidence);
        if (evidenceErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "The change analyzer produced invalid evidence: " + string.Join(" ", evidenceErrors));
        }

        ChangeWorkItemResult workItems = ChangeWorkItemBuilder.Build(
            selection,
            evidence,
            baseEvidence,
            headEvidence,
            baseEstimate,
            headEstimate,
            profile);
        return new PairEstimate(
            evidence,
            workItems,
            baseEvidence,
            headEvidence,
            baseEstimate,
            headEstimate);
    }

    private static RepositoryEvidence RenameRepository(
        RepositoryEvidence evidence,
        string repositoryName) => evidence with
        {
            Repository = evidence.Repository with { Name = repositoryName },
        };

    private static string[] ValidateSelection(ChangeSelection selection)
    {
        ChangeEvidence placeholder = new()
        {
            Selection = selection,
            Repository = new RepositoryDescriptor { Name = "placeholder", Ecosystems = [] },
            BaseEvidenceDigest = "sha256:placeholder",
            HeadEvidenceDigest = "sha256:placeholder",
        };
        return [.. ContractValidation.Validate(placeholder)
            .Where(error => error.Contains("selection", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("base-head", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("commit", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("range", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("pull-request", StringComparison.OrdinalIgnoreCase))];
    }

    private static CostRange CalculateCost(EffortRange effort, RateCard rateCard) => new()
    {
        Low = RoundMoney(effort.Low * rateCard.HourlyRate),
        Expected = RoundMoney(effort.Expected * rateCard.HourlyRate),
        High = RoundMoney(effort.High * rateCard.HourlyRate),
        Currency = rateCard.Currency,
    };

    private static decimal RoundMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record PairEstimate(
        ChangeEvidence Evidence,
        ChangeWorkItemResult WorkItems,
        RepositoryEvidence BaseEvidence,
        RepositoryEvidence HeadEvidence,
        EstimateReport BaseEstimate,
        EstimateReport HeadEstimate);
}
