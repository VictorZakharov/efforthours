using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private static void ValidateNativePeriod(
        ChangePortfolioComparisonReport report,
        List<string> errors)
    {
        ChangePortfolioNativePeriod? native = report.NativePeriod;
        if (native is null)
        {
            return;
        }

        if (native.Protocol != ChangePortfolioComparisonPolicies.NativePeriodReportV1 ||
            !Enum.IsDefined(native.Kind) ||
            !Enum.IsDefined(native.Breakdown) ||
            native.CapacityHoursPerDay <= 0m)
        {
            errors.Add("The native named-period contract is invalid.");
        }

        if (report.AsOf is null || report.Discovery is null ||
            report.ScopeProfile is null || report.ScopeSummary is null ||
            report.BucketPolicy.ContributorNormalization !=
                ChangePortfolioContributorNormalization.Isolated)
        {
            errors.Add(
                "A native named-period report requires provider discovery, scope metadata, and isolated contributor series.");
        }

        bool daily = native.Breakdown == ChangePortfolioNativeBreakdown.CalendarDay;
        if (daily !=
                (report.BucketPolicy.Kind == ChangePortfolioBucketPolicyKind.CalendarDay &&
                 report.BucketPolicy.Policy == ChangePortfolioComparisonPolicies.CalendarDayV1) ||
            !daily &&
                (report.BucketPolicy.Kind != ChangePortfolioBucketPolicyKind.Custom ||
                 report.BucketPolicy.Policy !=
                    ChangePortfolioComparisonPolicies.NamedPeriodTotalV1 ||
                 report.Buckets.Count != 1))
        {
            errors.Add("Native named-period buckets do not match the requested breakdown.");
        }

        bool current = native.Kind is ChangePortfolioNativePeriodKind.ThisWeek or
            ChangePortfolioNativePeriodKind.ThisMonth;
        if (current && report.AsOf != report.Selection.AuthorPeriodManifest?.UntilExclusive)
        {
            errors.Add("A current native named period must end at its asOf instant.");
        }

        ChangePortfolioContributorSelection selection = native.ContributorSelection;
        ValidateContributorSelection(report, selection, errors);
    }

    private static void ValidateContributorSelection(
        ChangePortfolioComparisonReport report,
        ChangePortfolioContributorSelection selection,
        List<string> errors)
    {
        if (selection.Protocol != ChangePortfolioComparisonPolicies.ContributorSampleV1 ||
            !Enum.IsDefined(selection.Mode) ||
            selection.RequestedSampleSize is < 0 or > 63 ||
            selection.EligiblePopulationCount is < 0 or > 100_000)
        {
            errors.Add("The native contributor-selection provenance is invalid.");
        }

        ValidateDigest(
            selection.InputDigest,
            "nativePeriod.contributorSelection.inputDigest",
            errors);
        if (selection.SampleSeed is { Length: > 256 } ||
            string.IsNullOrWhiteSpace(selection.SampleSeed) &&
                selection.SampleSeed is not null)
        {
            errors.Add("The contributor sample seed must contain 1 to 256 characters.");
        }

        string[] ids =
        [
            .. selection.SampledContributorIds,
            .. selection.IncludedContributorIds,
        ];
        foreach (string id in ids)
        {
            ValidatePublicId(id, "nativePeriod.contributorSelection.contributorId", errors);
        }

        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
        {
            errors.Add("Native sampled and included contributor IDs must be unique.");
        }

        if (!selection.Complete)
        {
            return;
        }

        IReadOnlyList<string> reportIds =
            report.Selection.AuthorPeriodManifest?.ContributorIds ?? [];
        if (!ids.Order(StringComparer.Ordinal)
            .SequenceEqual(reportIds.Order(StringComparer.Ordinal), StringComparer.Ordinal))
        {
            errors.Add(
                "Complete native contributor-selection IDs must match the report selection.");
        }

        if (selection.Mode == ChangePortfolioContributorSelectionMode.SingleContributor)
        {
            if (selection.SampleSeed is not null ||
                selection.RequestedSampleSize != 0 ||
                selection.EligiblePopulationCount != 1 ||
                selection.SampledContributorIds.Count != 0 ||
                selection.IncludedContributorIds.Count != 1)
            {
                errors.Add("A complete single-contributor selection has invalid provenance.");
            }

            return;
        }

        if (selection.SampleSeed is null ||
            selection.RequestedSampleSize < 1 ||
            selection.SampledContributorIds.Count != selection.RequestedSampleSize ||
            selection.EligiblePopulationCount is null ||
            selection.EligiblePopulationCount < selection.SampledContributorIds.Count)
        {
            errors.Add("A complete team contributor sample has invalid provenance.");
        }
    }
}
