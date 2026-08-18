using EffortHours.Contracts;
using EffortHours.Contracts.V1;
using EffortHours.Core;
using EffortHours.Estimation;

namespace EffortHours.Change;

public sealed record ChangeEstimateInput
{
    public required string RepositoryName { get; init; }

    public required ChangeSelection Selection { get; init; }

    public required Func<CancellationToken, Task<IChangeSnapshot>> OpenBaseAsync { get; init; }

    public required Func<CancellationToken, Task<IChangeSnapshot>> OpenHeadAsync { get; init; }

    public IReadOnlyList<ChangeComponentInput> Components { get; init; } = [];

    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = [];
}

public sealed partial class ChangeEstimator
{
    public const string Version = "change-seed/0.18.2+seed-rules/0.4.0";
    public const int FullSnapshotAnalysisFileLimit = 1_024;

    private readonly IEstimator _repositoryEstimator;
    private readonly Lock _repositoryEstimatorGate = new();

    public ChangeEstimator()
        : this(new SeedEstimator())
    {
    }

    public ChangeEstimator(IEstimator repositoryEstimator)
    {
        _repositoryEstimator = repositoryEstimator ?? throw new ArgumentNullException(nameof(repositoryEstimator));
    }

    public async Task<ChangeEstimateReport> EstimateAsync(
        GitChangePlan plan,
        EstimationProfile profile,
        RateCard? rateCard = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ChangeEstimateInput input = plan.SnapshotSession is null
            ? CreateInput(plan)
            : CreateStandaloneInput(plan, plan.SnapshotSession);
        return await EstimateAsync(
            input,
            profile,
            rateCard,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<ChangeEstimateReport> EstimateAsync(
        ChangeEstimateInput input,
        EstimationProfile profile,
        RateCard? rateCard = null,
        CancellationToken cancellationToken = default) =>
        EstimateCoreAsync(
            input,
            profile,
            rateCard,
            new SnapshotAnalysisCache(),
            "single-estimate",
            executionTelemetry: null,
            cancellationToken);

    private async Task<ChangeEstimateReport> EstimateCoreAsync(
        ChangeEstimateInput input,
        EstimationProfile profile,
        RateCard? rateCard,
        SnapshotAnalysisCache snapshotAnalyses,
        string cacheNamespace,
        ChangePortfolioExecutionTelemetry? executionTelemetry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.RepositoryName);
        cancellationToken.ThrowIfCancellationRequested();
        string[] selectionErrors = ValidateSelection(input.Selection);
        if (selectionErrors.Length > 0)
        {
            throw new ArgumentException(
                "Change selection is invalid: " + string.Join(" ", selectionErrors),
                nameof(input));
        }

        PairEstimate normalized;
        (IChangeSnapshot baseSnapshot, IChangeSnapshot headSnapshot) = await OpenSnapshotsAsync(
            input.OpenBaseAsync,
            input.OpenHeadAsync,
            executionTelemetry,
            cancellationToken).ConfigureAwait(false);
        await using (baseSnapshot)
        await using (headSnapshot)
        {
            normalized = await AnalyzePairAsync(
                input.RepositoryName,
                input.Selection,
                baseSnapshot,
                headSnapshot,
                input.Diagnostics,
                profile,
                snapshotAnalyses,
                cacheNamespace,
                executionTelemetry,
                cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<ChangeComponentInput> componentInputs = input.Components.Count == 0
            ?
            [
                new ChangeComponentInput
                {
                    Kind = ChangeComponentKind.FinalDelta,
                    Selector = "final-delta",
                    BaseObjectId = input.Selection.Base.ObjectId,
                    HeadObjectId = input.Selection.Head.ObjectId,
                    OpenBaseAsync = input.OpenBaseAsync,
                    OpenHeadAsync = input.OpenHeadAsync,
                },
            ]
            : input.Components;
        List<ChangeComponentDraft> components = [];
        foreach (ChangeComponentInput component in componentInputs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PairEstimate isolated;
            if (string.Equals(
                    component.BaseObjectId,
                    input.Selection.Base.ObjectId,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    component.HeadObjectId,
                    input.Selection.Head.ObjectId,
                    StringComparison.OrdinalIgnoreCase))
            {
                isolated = normalized;
            }
            else
            {
                ChangeSelection componentSelection = new()
                {
                    Kind = ChangeSelectionKind.Commit,
                    Base = new ChangeSnapshotReference
                    {
                        Selector = component.BaseObjectId,
                        ObjectId = component.BaseObjectId,
                        Kind = component.BaseObjectId == GitClient.EmptyTreeObjectId
                            ? ChangeSnapshotKind.EmptyTree
                            : ChangeSnapshotKind.GitCommit,
                    },
                    Head = new ChangeSnapshotReference
                    {
                        Selector = component.HeadObjectId,
                        ObjectId = component.HeadObjectId,
                        Kind = ChangeSnapshotKind.GitCommit,
                    },
                    Commit = component.Selector,
                };
                (IChangeSnapshot componentBase, IChangeSnapshot componentHead) = await OpenSnapshotsAsync(
                    component.OpenBaseAsync,
                    component.OpenHeadAsync,
                    executionTelemetry,
                    cancellationToken).ConfigureAwait(false);
                await using (componentBase)
                await using (componentHead)
                {
                    isolated = await AnalyzePairAsync(
                        input.RepositoryName,
                        componentSelection,
                        componentBase,
                        componentHead,
                        [],
                        profile,
                        snapshotAnalyses,
                        cacheNamespace,
                        executionTelemetry,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            components.Add(new ChangeComponentDraft(
                component,
                isolated.WorkItems.TotalEffort,
                isolated.WorkItems.Categories,
                isolated.Evidence.Paths));
        }

        ChangeReconciliation reconciliation;
        using (executionTelemetry?.Measure(ChangePortfolioExecutionPhases.Reconciliation))
        {
            reconciliation = ChangeReconciler.Reconcile(
                normalized.Evidence.Selection,
                normalized.WorkItems.TotalEffort,
                normalized.WorkItems.Categories,
                normalized.Evidence.Paths,
                components);
        }
        List<Diagnostic> diagnostics = [.. normalized.Evidence.Diagnostics];
        diagnostics.Add(new Diagnostic
        {
            Code = "FB5000",
            Severity = DiagnosticSeverity.Warning,
            Message = "Change EHE uses transparent but uncalibrated change-seed and repository seed priors. " +
                "It is experimental and requires review before consequential use.",
        });
        diagnostics.Add(new Diagnostic
        {
            Code = "FB5001",
            Severity = DiagnosticSeverity.Information,
            Message = "Normalized Change EHE values the immutable final base-to-head artifact delta. " +
                "Component activity, commit count, timestamps, authors, and intermediate churn do not multiply it.",
        });
        if (reconciliation.Adjustments.Count > 0)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = "FB5002",
                Severity = DiagnosticSeverity.Information,
                Message = $"{reconciliation.Adjustments.Count} explicit reconciliation adjustment(s) bridge " +
                    "isolated components to the normalized final delta.",
                EvidenceIds = [.. reconciliation.Adjustments
                    .SelectMany(adjustment => adjustment.EvidenceIds)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)],
            });
        }

        bool hasKnownIssues = normalized.BaseEvidence.Diagnostics
            .Concat(normalized.HeadEvidence.Diagnostics)
            .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        EffortRange total = normalized.WorkItems.TotalEffort;
        ChangeEstimateReport report = new()
        {
            EstimatorVersion = Version,
            SourceEstimatorVersion = normalized.HeadEstimate.EstimatorVersion,
            Repository = normalized.Evidence.Repository,
            Selection = normalized.Evidence.Selection,
            Evidence = normalized.Evidence,
            Profile = profile,
            Baseline = normalized.HeadEstimate.Baseline,
            TotalEffort = total,
            RateCard = rateCard,
            TotalCost = rateCard is null ? null : CalculateCost(total, rateCard),
            Categories = normalized.WorkItems.Categories,
            WorkItems = normalized.WorkItems.WorkItems,
            ProfessionalizationGap = [],
            Reconciliation = reconciliation,
            Diagnostics = [.. diagnostics
                .Distinct()
                .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)],
            Assumptions =
            [
                "Change EHE is counterfactual non-AI replacement effort represented by the final artifact delta, not actual labor or a timesheet.",
                "Base and head are immutable snapshots; intermediate history is excluded from normalized effort.",
                "A clear specification exists at the level promised by the selected profile.",
                "Discovered tests are assumed to pass on the static path and the described delta is estimated as working.",
                "Formatting-only, exact movement, conventional generated, vendored, minified, binary, lockfile, build-output, and exact-copy bodies are excluded when classified; only safely isolated explicit custom-code regions inside generated files can contribute effort.",
                "Deletion is never negative EHE; supported removal and simplification work is bounded by capability context rather than deleted volume.",
                "Low and high values are preliminary dependent planning bounds, not calibrated probability intervals.",
                "Range normalization shares, when present, are expected-point structural diagnostics, not historical rework, actual labor, productivity scores, or effort multipliers.",
                "Professionalization gaps are separate and are not added to represented Change EHE or cost.",
            ],
            Verification = new VerificationSummary
            {
                Mode = VerificationMode.StaticAssumed,
                WorkingState = hasKnownIssues ? WorkingState.KnownIssues : WorkingState.AssumedWorking,
                TestsAssumedPassing = true,
                Note = hasKnownIssues
                    ? "One or both immutable snapshots contain analyzer errors; the report still values the materially described working delta."
                    : "Neither immutable snapshot was built or executed.",
            },
        };

        IReadOnlyList<string> reportErrors = ContractValidation.Validate(report);
        if (reportErrors.Count > 0)
        {
            throw new InvalidOperationException(
                "The change estimator produced an invalid report: " + string.Join(" ", reportErrors));
        }

        return report;
    }

    private static async Task<(IChangeSnapshot Base, IChangeSnapshot Head)> OpenSnapshotsAsync(
        Func<CancellationToken, Task<IChangeSnapshot>> openBaseAsync,
        Func<CancellationToken, Task<IChangeSnapshot>> openHeadAsync,
        ChangePortfolioExecutionTelemetry? executionTelemetry,
        CancellationToken cancellationToken)
    {
        using IDisposable? measurement = executionTelemetry?.Measure(
            ChangePortfolioExecutionPhases.SnapshotAndDiffConstruction);
        IChangeSnapshot baseSnapshot = await openBaseAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IChangeSnapshot headSnapshot = await openHeadAsync(cancellationToken).ConfigureAwait(false);
            return (baseSnapshot, headSnapshot);
        }
        catch
        {
            await baseSnapshot.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static ChangeEstimateInput CreateInput(GitChangePlan plan)
    {
        string repositoryName = Path.GetFileName(Path.TrimEndingDirectorySeparator(plan.RepositoryPath));
        return new ChangeEstimateInput
        {
            RepositoryName = string.IsNullOrWhiteSpace(repositoryName)
                ? plan.RepositoryPath
                : repositoryName,
            Selection = plan.Selection,
            OpenBaseAsync = plan.OpenBaseAsync,
            OpenHeadAsync = plan.OpenHeadAsync,
            Components = plan.Components,
            Diagnostics = plan.Diagnostics,
        };
    }

    private static ChangeEstimateInput CreateStandaloneInput(
        GitChangePlan plan,
        GitSnapshotSession snapshotSession)
    {
        ChangeEstimateInput input = CreateInput(plan);
        return input with
        {
            OpenBaseAsync = cancellationToken => snapshotSession.OpenStandaloneSnapshotAsync(
                plan.Selection.Base.ObjectId,
                cancellationToken),
            OpenHeadAsync = cancellationToken => snapshotSession.OpenStandaloneSnapshotAsync(
                plan.Selection.Head.ObjectId,
                cancellationToken),
            Components = [.. plan.Components.Select(component => component with
            {
                OpenBaseAsync = cancellationToken => snapshotSession.OpenStandaloneSnapshotAsync(
                    component.BaseObjectId,
                    cancellationToken),
                OpenHeadAsync = cancellationToken => snapshotSession.OpenStandaloneSnapshotAsync(
                    component.HeadObjectId,
                    cancellationToken),
            })],
        };
    }
}
