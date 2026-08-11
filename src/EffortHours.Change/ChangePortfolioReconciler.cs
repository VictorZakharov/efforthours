using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed class ChangePortfolioReconciler
{
    public const string Version = "change-portfolio/0.1.0+change-seed/0.7.0+seed-rules/0.3.0";

    public static ChangePortfolioReport Reconcile(
        ChangePortfolioSelection selection,
        IReadOnlyList<ChangePortfolioCandidate> candidates,
        EstimationProfile profile,
        RateCard? rateCard = null,
        IReadOnlyList<Diagnostic>? planningDiagnostics = null)
    {
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(candidates);
        ValidateInputs(selection, candidates, profile);
        ChangePortfolioItemDraft[] drafts = [.. candidates
            .Select(ChangePortfolioIdentity.CreateDraft)];
        ChangePortfolioGroupResult[] results = [.. drafts
            .GroupBy(draft => draft.Candidate.RepositoryId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => ChangePortfolioGroupNormalizer.Normalize(
                selection,
                group.Key,
                [.. group.OrderBy(draft => draft.Id, StringComparer.Ordinal)]))];
        ChangePortfolioAdjustment[] adjustments = [.. results
            .SelectMany(result => result.Adjustments)
            .OrderBy(adjustment => adjustment.Id, StringComparer.Ordinal)];
        EffortRange isolated = ContractValidation.Sum(
            drafts.Select(draft => draft.Candidate.Report.TotalEffort));
        EffortRange normalized = ContractValidation.Sum(
            results.Select(result => result.Group.NormalizedEffort));
        CategoryEstimate[] categories = AggregateCategories(results.Select(result => result.Group));
        ChangePortfolioItemEstimate[] items = [.. drafts
            .OrderBy(draft => draft.Candidate.RepositoryId, StringComparer.Ordinal)
            .ThenBy(draft => draft.Candidate.Attribution.SelectedTimestamp)
            .ThenBy(draft => draft.Id, StringComparer.Ordinal)
            .Select(draft => Item(draft, rateCard))];
        bool hasAmbiguity = results.Any(result => result.Group.UncertaintyReasons.Count > 0) ||
            items.Any(item => item.UncertaintyReasons.Count > 0);
        bool hasKnownIssues = candidates.Any(candidate =>
            candidate.Report.Verification.WorkingState == WorkingState.KnownIssues ||
            candidate.Report.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        List<Diagnostic> diagnostics =
        [
            .. (planningDiagnostics ?? []),
            new Diagnostic
            {
                Code = "FB5300",
                Severity = DiagnosticSeverity.Warning,
                Message = "Change portfolio reconciliation is experimental. It composes reviewed Change-report contracts but is not empirically calibrated or suitable for employee ranking.",
            },
            new Diagnostic
            {
                Code = "FB5301",
                Severity = DiagnosticSeverity.Information,
                Message = "Identity, timestamps, commit count, PR activity, and selector order choose rows only; they never multiply or rank Equivalent Human Effort.",
            },
        ];
        if (adjustments.Length > 0)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = "FB5302",
                Severity = DiagnosticSeverity.Information,
                Message = $"{adjustments.Length} explicit portfolio adjustment(s) reconcile isolated rows with repository-normalized effort.",
            });
        }

        if (hasAmbiguity)
        {
            diagnostics.Add(new Diagnostic
            {
                Code = "FB5303",
                Severity = DiagnosticSeverity.Warning,
                Message = "Shared-credit, overlapping-path, interleaving, or ordering ambiguity remains visible in item and repository-group uncertainty reasons.",
            });
        }

        ChangePortfolioReport report = new()
        {
            EstimatorVersion = Version,
            SourceChangeEstimatorVersion = candidates[0].Report.EstimatorVersion,
            Selection = selection,
            Profile = profile,
            Baseline = candidates[0].Report.Baseline,
            IsolatedEffort = isolated,
            TotalEffort = normalized,
            RateCard = rateCard,
            TotalCost = rateCard is null ? null : Cost(normalized, rateCard),
            Categories = categories,
            RepositoryGroups = [.. results.Select(result => result.Group)],
            Items = items,
            Adjustments = adjustments,
            Diagnostics = [.. diagnostics
                .Distinct()
                .OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal)],
            Assumptions =
            [
                "Portfolio Change EHE is repository-attributed counterfactual replacement effort, not actual labor, productivity, personal value, or proof of sole authorship.",
                "Each isolated row is a canonical immutable Change estimate; portfolio normalization never uses churn, duration, contributor count, message size, or activity as effort signals.",
                "Exact represented patch duplicates are counted once within one repository and never deduplicated across different repositories.",
                "Independent disjoint changes remain additive. Overlapping path/category contributions use a deterministic order-independent maximum unless an author-period selection supplies an exact chronological object chain.",
                "Author periods use inclusive start and exclusive end instants. Identity aliases and author/committer timestamps select commits only.",
                "Co-authorship and first-parent merge inclusion are explicit policies; pair work, requirements, design, reviews, mentoring, incidents, debugging, coordination, and uncommitted work are not recoverable from repository history.",
                "Allocations sum exactly to normalized expected EHE but are structural attribution, not timesheet entries, performance grades, compensation advice, or causal credit.",
                "Low and high values remain dependent planning bounds rather than calibrated probabilities.",
            ],
            Verification = new VerificationSummary
            {
                Mode = VerificationMode.StaticAssumed,
                WorkingState = hasKnownIssues ? WorkingState.KnownIssues : WorkingState.AssumedWorking,
                TestsAssumedPassing = true,
                Note = hasKnownIssues
                    ? "At least one selected immutable change contains analyzer errors; the portfolio retains its materially described delta and explicit uncertainty."
                    : "Selected immutable snapshots were statically analyzed; target code was not built or executed.",
            },
        };
        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "The change portfolio reconciler produced an invalid report: " + string.Join(" ", errors));
        }

        return report;
    }

    private static void ValidateInputs(
        ChangePortfolioSelection selection,
        IReadOnlyList<ChangePortfolioCandidate> candidates,
        EstimationProfile profile)
    {
        if (candidates.Count is < 1 or > 128)
        {
            throw new ArgumentException("A change portfolio requires between 1 and 128 selected changes.", nameof(candidates));
        }

        IReadOnlyList<string> selectionErrors = ContractValidation.Validate(selection);
        if (selectionErrors.Count > 0)
        {
            throw new ArgumentException(
                "Change portfolio selection is invalid: " + string.Join(" ", selectionErrors),
                nameof(selection));
        }

        HashSet<string> selectorIds = new(StringComparer.Ordinal);
        string expectedEstimator = candidates[0].Report.EstimatorVersion;
        EstimationBaseline expectedBaseline = candidates[0].Report.Baseline;
        foreach (ChangePortfolioCandidate candidate in candidates)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(candidate.RepositoryId);
            ArgumentException.ThrowIfNullOrWhiteSpace(candidate.SelectorId);
            IReadOnlyList<string> reportErrors = ContractValidation.Validate(candidate.Report);
            if (reportErrors.Count > 0)
            {
                throw new ArgumentException(
                    $"Portfolio candidate '{candidate.SelectorId}' has an invalid Change report: " +
                    string.Join(" ", reportErrors),
                    nameof(candidates));
            }

            if (!selectorIds.Add(candidate.SelectorId))
            {
                throw new ArgumentException(
                    $"Portfolio selector ID '{candidate.SelectorId}' is duplicated.",
                    nameof(candidates));
            }

            if (candidate.Report.Profile != profile ||
                candidate.Report.EstimatorVersion != expectedEstimator ||
                candidate.Report.Baseline != expectedBaseline)
            {
                throw new ArgumentException(
                    "All portfolio candidates must use one profile, Change estimator, and worker baseline.",
                    nameof(candidates));
            }

            bool validKind = selection.Kind switch
            {
                ChangePortfolioSelectionKind.PullRequests =>
                    candidate.Report.Selection.Kind == ChangeSelectionKind.PullRequest &&
                    candidate.Attribution.Kind == ChangePortfolioAttributionKind.PullRequest,
                ChangePortfolioSelectionKind.AuthorPeriod =>
                    candidate.Report.Selection.Kind == ChangeSelectionKind.Commit &&
                    candidate.Attribution.Kind is ChangePortfolioAttributionKind.DirectAuthor or
                        ChangePortfolioAttributionKind.Coauthor,
                _ => false,
            };
            if (!validKind)
            {
                throw new ArgumentException(
                    $"Portfolio candidate '{candidate.SelectorId}' does not match selection kind '{selection.Kind}'.",
                    nameof(candidates));
            }
        }
    }

    private static ChangePortfolioItemEstimate Item(
        ChangePortfolioItemDraft draft,
        RateCard? rateCard) => new()
        {
            Id = draft.Id,
            SelectorId = draft.Candidate.SelectorId,
            RepositoryId = draft.Candidate.RepositoryId,
            BaseContextId = draft.BaseContextId,
            Selection = draft.Candidate.Report.Selection,
            EvidenceDigest = draft.EvidenceDigest,
            PatchDigest = draft.PatchDigest,
            RepresentedPathCount = draft.Effects.Count,
            IsolatedEffort = draft.Candidate.Report.TotalEffort,
            AllocatedExpectedHours = draft.AllocatedExpectedHours,
            AllocatedExpectedCost = rateCard is null
                ? null
                : RoundMoney(draft.AllocatedExpectedHours * rateCard.HourlyRate),
            Categories = draft.Candidate.Report.Categories,
            Attribution = draft.Candidate.Attribution,
            DuplicateOfItemId = draft.DuplicateOfItemId,
            UncertaintyReasons = [.. draft.UncertaintyReasons.Order(StringComparer.Ordinal)],
        };

    private static CategoryEstimate[] AggregateCategories(
        IEnumerable<ChangePortfolioRepositoryGroup> groups) => [.. groups
            .SelectMany(group => group.Categories)
            .GroupBy(category => category.Category)
            .OrderBy(group => group.Key)
            .Select(group => new CategoryEstimate
            {
                Category = group.Key,
                Hours = ContractValidation.Sum(group.Select(category => category.Hours)),
            })];

    private static CostRange Cost(EffortRange effort, RateCard rateCard) => new()
    {
        Currency = rateCard.Currency,
        Low = RoundMoney(effort.Low * rateCard.HourlyRate),
        Expected = RoundMoney(effort.Expected * rateCard.HourlyRate),
        High = RoundMoney(effort.High * rateCard.HourlyRate),
    };

    private static decimal RoundMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
