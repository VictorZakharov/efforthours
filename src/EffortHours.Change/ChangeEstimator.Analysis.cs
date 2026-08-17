using EffortHours.Analysis;
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
        SnapshotAnalysisCache snapshotAnalyses,
        string cacheNamespace,
        ChangePortfolioExecutionTelemetry? executionTelemetry,
        CancellationToken cancellationToken)
    {
        ValidateSnapshotReference("base", selection.Base, baseSnapshot);
        ValidateSnapshotReference("head", selection.Head, headSnapshot);
        ChangeAnalysisScope? analysisScope;
        using (executionTelemetry?.Measure(ChangePortfolioExecutionPhases.SnapshotAndDiffConstruction))
        {
            analysisScope = ChangeAnalysisScope.Create(baseSnapshot, headSnapshot);
        }
        SnapshotAnalysis baseAnalysis = await AnalyzeSnapshotAsync(
            repositoryName,
            baseSnapshot,
            profile,
            snapshotAnalyses,
            cacheNamespace,
            analysisScope,
            executionTelemetry,
            cancellationToken)
            .ConfigureAwait(false);
        SnapshotAnalysis headAnalysis = await AnalyzeSnapshotAsync(
            repositoryName,
            headSnapshot,
            profile,
            snapshotAnalyses,
            cacheNamespace,
            analysisScope,
            executionTelemetry,
            cancellationToken)
            .ConfigureAwait(false);
        RepositoryEvidence baseEvidence = baseAnalysis.Evidence;
        RepositoryEvidence headEvidence = headAnalysis.Evidence;
        EstimateReport baseEstimate = baseAnalysis.Estimate;
        EstimateReport headEstimate = headAnalysis.Estimate;
        List<Diagnostic> evidenceDiagnostics = [.. selectorDiagnostics];
        evidenceDiagnostics.AddRange(baseEvidence.Diagnostics);
        evidenceDiagnostics.AddRange(headEvidence.Diagnostics);
        ChangeEvidence evidence;
        using (executionTelemetry?.Measure(ChangePortfolioExecutionPhases.SnapshotAndDiffConstruction))
        {
            evidence = await ChangeEvidenceBuilder.BuildAsync(
                selection,
                baseSnapshot,
                headSnapshot,
                baseEvidence,
                headEvidence,
                evidenceDiagnostics,
                cancellationToken).ConfigureAwait(false);
        }
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

        ChangePathEvidence[] generatedCustomization = [.. evidence.Paths.Where(path =>
            path.Tags.Contains(
                "normalization:generated-customization-represented",
                StringComparer.Ordinal))];
        if (generatedCustomization.Length > 0)
        {
            normalizedDiagnostics.Add(new Diagnostic
            {
                Code = "FB5203",
                Severity = DiagnosticSeverity.Information,
                Message = $"{generatedCustomization.Length} generated path change(s) contain supported, " +
                    "explicit custom-code regions. Only those regions contribute represented effort.",
                EvidenceIds = [.. generatedCustomization
                    .Select(path => path.Id)
                    .Order(StringComparer.Ordinal)],
            });
        }

        ChangePathEvidence[] ambiguousCustomization = [.. evidence.Paths.Where(path =>
            path.Tags.Contains(
                "normalization:generated-customization-ambiguous",
                StringComparer.Ordinal))];
        if (ambiguousCustomization.Length > 0)
        {
            normalizedDiagnostics.Add(new Diagnostic
            {
                Code = "FB5204",
                Severity = DiagnosticSeverity.Warning,
                Message = $"{ambiguousCustomization.Length} generated path change(s) contain ambiguous " +
                    "custom-code markers. Their bodies are excluded and require review.",
                EvidenceIds = [.. ambiguousCustomization
                    .Select(path => path.Id)
                    .Order(StringComparer.Ordinal)],
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

        ChangeWorkItemResult workItems;
        using (executionTelemetry?.Measure(ChangePortfolioExecutionPhases.Reconciliation))
        {
            workItems = ChangeWorkItemBuilder.Build(
                selection,
                evidence,
                baseEvidence,
                headEvidence,
                baseEstimate,
                headEstimate,
                profile);
        }
        return new PairEstimate(
            evidence,
            workItems,
            baseEvidence,
            headEvidence,
            baseEstimate,
            headEstimate);
    }

    private async Task<SnapshotAnalysis> AnalyzeSnapshotAsync(
        string repositoryName,
        IChangeSnapshot snapshot,
        EstimationProfile profile,
        SnapshotAnalysisCache snapshotAnalyses,
        string cacheNamespace,
        ChangeAnalysisScope? analysisScope,
        ChangePortfolioExecutionTelemetry? executionTelemetry,
        CancellationToken cancellationToken)
    {
        string analysisScopeId = analysisScope?.Id ?? "full-snapshot";
        if (snapshotAnalyses.TryGet(
            cacheNamespace,
            snapshot.ObjectId,
            analysisScopeId,
            out SnapshotAnalysis cached))
        {
            return cached;
        }

        SnapshotAnalysis analysis;
        using (executionTelemetry?.Measure(ChangePortfolioExecutionPhases.StaticAnalysis))
        {
            RepositoryEvidence evidence = await ReadEvidenceAsync(snapshot, analysisScope, cancellationToken)
                .ConfigureAwait(false);
            evidence = RenameRepository(evidence, repositoryName);
            EstimateReport estimate;
            lock (_repositoryEstimatorGate)
            {
                estimate = _repositoryEstimator.Estimate(evidence, profile);
            }

            analysis = new(evidence, estimate);
        }
        snapshotAnalyses.Add(cacheNamespace, snapshot.ObjectId, analysisScopeId, analysis);
        return analysis;
    }

    private static async Task<RepositoryEvidence> ReadEvidenceAsync(
        IChangeSnapshot snapshot,
        ChangeAnalysisScope? analysisScope,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (snapshot is IRepositoryEvidenceChangeSnapshot analyzedSnapshot)
        {
            return analyzedSnapshot.Evidence;
        }

        IRepositoryFileSystem fileSystem = analysisScope is null
            ? snapshot.FileSystem
            : new ScopedRepositoryFileSystem(
                snapshot.FileSystem,
                snapshot.RootPath,
                analysisScope.Paths);
        RepositoryAnalysisArtifactCache? analysisArtifactCache =
            (fileSystem as IRepositoryAnalysisArtifactCacheProvider)?.AnalysisArtifactCache;
        RepositoryEvidence evidence = await new RepositoryAnalysisPipeline(
            fileSystem,
            cacheStore: null,
            analysisArtifactCache)
            .ScanAsync(snapshot.RootPath, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (analysisScope is null)
        {
            return evidence;
        }

        Diagnostic scopeDiagnostic = new()
        {
            Code = "FB5205",
            Severity = DiagnosticSeverity.Information,
            Message = $"Large immutable Git snapshot analysis parsed {analysisScope.ChangedPathCount} changed " +
                $"path(s), {analysisScope.ContextPathCount} relevant unchanged context artifact(s), and " +
                $"{analysisScope.RepresentativePathCount} ecosystem representative(s). " +
                $"The snapshot contained {analysisScope.AvailableContextPathCount} context artifact(s); " +
                "analysis retained " +
                $"the content-addressed identity of all {analysisScope.FullPathCount} paths.",
        };
        return evidence with
        {
            Repository = evidence.Repository with
            {
                SourceDigest = snapshot is GitSnapshotFileSystem gitSnapshot
                    ? gitSnapshot.InventoryDigest
                    : ChangeAnalysisScope.ComputeInventoryDigest(snapshot.Files),
            },
            Diagnostics = [.. evidence.Diagnostics
                .Append(scopeDiagnostic)
                .Distinct()
                .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)],
        };
    }

    private static void ValidateSnapshotReference(
        string label,
        ChangeSnapshotReference reference,
        IChangeSnapshot snapshot)
    {
        if (!string.Equals(reference.ObjectId, snapshot.ObjectId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The opened {label} snapshot identity does not match the pinned selection.");
        }

        if (snapshot is IRepositoryEvidenceChangeSnapshot analyzedSnapshot &&
            !string.Equals(
                analyzedSnapshot.Evidence.Repository.SourceDigest,
                snapshot.ObjectId,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The opened {label} snapshot evidence does not match its content-derived identity.");
        }
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

    private sealed record SnapshotAnalysis(
        RepositoryEvidence Evidence,
        EstimateReport Estimate);
}
