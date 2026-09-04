using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public static partial class ChangePortfolioComparisonBuilder
{
    public static ChangePortfolioComparisonReport BuildIncomplete(
        ChangePortfolioComparisonBuildOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ChangePortfolioComparisonExecution execution = options.ExecutionOverride ??
            throw new ArgumentException(
                "Incomplete comparison output requires structured execution failures.",
                nameof(options));
        if (execution.Failures.Count == 0)
        {
            throw new ArgumentException(
                "Incomplete comparison output requires at least one failure.",
                nameof(options));
        }

        string manifestDigest = ChangeAuthorPeriodManifestIdentity.ComputeDigest(
            options.SourceManifest);
        ChangePortfolioSelection selection =
            ChangeAuthorPeriodManifestIdentity.CreateReportSelection(
                options.SourceManifest,
                manifestDigest);
        ChangePortfolioComparisonBucketPolicy bucketPolicy = new()
        {
            Kind = options.BucketKind,
            Policy = options.BucketPolicy,
            InputDigest = ChangePortfolioComparisonIdentity.ComputeBucketDigest(
                options.BucketManifest),
            ContributorNormalization = options.ContributorNormalization,
            CapacityCalendarPolicy = options.CapacityManifest?.CalendarPolicy,
            CapacityInputDigest = options.CapacityManifest is null
                ? null
                : ChangePortfolioComparisonIdentity.ComputeCapacityDigest(
                    options.CapacityManifest),
        };
        string semanticDigest = ChangePortfolioComparisonIdentity.ComputeTextDigest(
            string.Join(
                '\n',
                "incomplete-change-portfolio-comparison/1.0.0",
                manifestDigest,
                bucketPolicy.InputDigest,
                bucketPolicy.CapacityInputDigest ?? "no-capacity",
                bucketPolicy.ContributorNormalization.ToString(),
                options.SourceManifest.Selection.TimeZone,
                options.ScopeProfile?.Digest ?? "no-scope-profile",
                options.NativePeriod is null
                    ? "no-native-period"
                    : ContractJson.SerializeCompact(options.NativePeriod)));
        ChangePortfolioComparisonReport report = new()
        {
            Status = ChangePortfolioComparisonStatus.Incomplete,
            View = options.View,
            Title = options.Title,
            GeneratedAt = options.GeneratedAt.ToUniversalTime(),
            AsOf = options.AsOf?.ToUniversalTime(),
            Discovery = options.Discovery,
            ScopeProfile = options.ScopeProfile,
            ScopeSummary = options.ScopeSummary,
            NativePeriod = options.NativePeriod,
            CliVersion = options.CliVersion,
            EstimatorVersion = ChangePortfolioReconciler.Version,
            SourceChangeEstimatorVersion = ChangeEstimator.Version,
            Profile = options.Profile,
            Selection = selection,
            BucketPolicy = bucketPolicy,
            SourcePortfolio = null,
            Buckets = options.Buckets,
            Series = [],
            Execution = execution,
            Diagnostics =
            [
                new Diagnostic
                {
                    Code = "FB5332",
                    Severity = DiagnosticSeverity.Error,
                    Message = "The comparison is incomplete. Completed repository evidence remains resumable, but no aggregate EHE or trend is published and no failed cell is replaced with zero.",
                },
            ],
            Verification = new ChangePortfolioComparisonVerification
            {
                SemanticDigest = semanticDigest,
                SourcePortfolioDigest = null,
                BucketAllocationPolicy = ChangePortfolioComparisonIdentity.ContributorSeriesPolicy(
                    options.ContributorNormalization),
                CompleteAggregates = false,
                ExecutionOnlyPathsExcluded = true,
                RawAliasesExcluded = true,
                Note = "Incomplete output binds validated inputs and structured failures but deliberately omits portfolio and trend aggregates.",
            },
        };
        IReadOnlyList<string> errors = ContractValidation.Validate(report);
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "The incomplete comparison report is invalid: " + string.Join(" ", errors));
        }

        return report;
    }
}
