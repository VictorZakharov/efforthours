using EffortHours.Contracts;
using EffortHours.Contracts.V1;

namespace EffortHours.RepositoryCalibration;

internal static class CandidateEstimateProjection
{
    public static EstimateReport Create(
        EstimateReport source,
        string estimatorVersion,
        IReadOnlyList<WorkItem> workItems,
        IReadOnlyList<string> appendedAssumptions)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(estimatorVersion);
        ArgumentNullException.ThrowIfNull(workItems);
        ArgumentNullException.ThrowIfNull(appendedAssumptions);

        EffortRange total = ContractValidation.Sum(workItems.Select(item => item.Hours));
        return source with
        {
            EstimatorVersion = estimatorVersion,
            TotalEffort = total,
            TotalCost = ProjectCost(source.RateCard, total),
            Categories = BuildCategories(workItems, total),
            WorkItems = workItems,
            Assumptions = [.. source.Assumptions, .. appendedAssumptions],
        };
    }

    private static CategoryEstimate[] BuildCategories(
        IReadOnlyList<WorkItem> workItems,
        EffortRange total)
    {
        CategoryEstimate[] categories =
        [
            .. workItems.GroupBy(item => item.Category)
                .OrderBy(group => group.Key)
                .Select(group => new CategoryEstimate
                {
                    Category = group.Key,
                    Hours = ContractValidation.Sum(group.Select(item => item.Hours)),
                }),
        ];
        if (categories.Length == 0)
        {
            return categories;
        }

        EffortRange beforeLast = ContractValidation.Sum(
            categories[..^1].Select(category => category.Hours));
        categories[^1] = categories[^1] with
        {
            Hours = new EffortRange
            {
                Low = total.Low - beforeLast.Low,
                Expected = total.Expected - beforeLast.Expected,
                High = total.High - beforeLast.High,
            },
        };
        return categories;
    }

    private static CostRange? ProjectCost(RateCard? rateCard, EffortRange hours) =>
        rateCard is null
            ? null
            : new CostRange
            {
                Low = RoundMoney(hours.Low * rateCard.HourlyRate),
                Expected = RoundMoney(hours.Expected * rateCard.HourlyRate),
                High = RoundMoney(hours.High * rateCard.HourlyRate),
                Currency = rateCard.Currency,
            };

    private static decimal RoundMoney(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
