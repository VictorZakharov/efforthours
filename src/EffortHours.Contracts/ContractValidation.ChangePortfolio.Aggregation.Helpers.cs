using EffortHours.Contracts.V1;

namespace EffortHours.Contracts;

public static partial class ContractValidation
{
    private const char PortfolioSetSeparator = '\u001f';

    private static EffortRange PortfolioZero() => new() { Low = 0m, Expected = 0m, High = 0m };

    private static string PortfolioSetKey(IEnumerable<string> ids) =>
        string.Join(PortfolioSetSeparator, ids.Order(StringComparer.Ordinal));

    private static string[] PortfolioContributorIds(ChangePortfolioItemEstimate item) =>
        item.Attribution.ContributorMatches?.Select(match => match.ContributorId).ToArray() ?? [];

    private static IReadOnlyList<string> PortfolioHeadIds(ChangePortfolioItemEstimate item) =>
        item.Attribution.HeadIds ?? [];

    private static Dictionary<string, ChangePortfolioItemEstimate> PortfolioItemIndex(
        IEnumerable<ChangePortfolioItemEstimate> items)
    {
        Dictionary<string, ChangePortfolioItemEstimate> result = new(StringComparer.Ordinal);
        foreach (ChangePortfolioItemEstimate item in items)
        {
            result.TryAdd(item.Id, item);
        }

        return result;
    }

    private static Dictionary<string, ChangePortfolioAdjustment> PortfolioAdjustmentIndex(
        IEnumerable<ChangePortfolioAdjustment> adjustments)
    {
        Dictionary<string, ChangePortfolioAdjustment> result = new(StringComparer.Ordinal);
        foreach (ChangePortfolioAdjustment adjustment in adjustments)
        {
            result.TryAdd(adjustment.Id, adjustment);
        }

        return result;
    }

    private static int PortfolioMatchCount(
        IEnumerable<ChangePortfolioItemEstimate> items,
        ChangePortfolioContributorMatchKind kind) => items.Sum(item =>
            item.Attribution.ContributorMatches?.Count(match => match.Kind == kind) ?? 0);

    private static int PortfolioContributorMatchCount(
        IEnumerable<ChangePortfolioItemEstimate> items,
        string contributorId,
        ChangePortfolioContributorMatchKind kind) => items.Sum(item =>
            item.Attribution.ContributorMatches?.Count(match =>
                match.ContributorId == contributorId && match.Kind == kind) ?? 0);

    private static SignedEffortRange PortfolioDifference(EffortRange normalized, EffortRange isolated) => new()
    {
        Low = normalized.Low - isolated.Low,
        Expected = normalized.Expected - isolated.Expected,
        High = normalized.High - isolated.High,
    };

    private static bool PortfolioCanonical(IEnumerable<string> values) =>
        values.SequenceEqual(values.Order(StringComparer.Ordinal), StringComparer.Ordinal);

    private static void ValidatePortfolioDelta(
        SignedEffortRange actual,
        EffortRange normalized,
        EffortRange isolated,
        string path,
        List<string> errors)
    {
        if (actual != PortfolioDifference(normalized, isolated))
        {
            errors.Add($"{path} does not reconcile isolated and normalized effort exactly.");
        }
    }

    private static void ValidatePortfolioCounts(
        IEnumerable<int> counts,
        string path,
        List<string> errors)
    {
        if (counts.Any(count => count < 0))
        {
            errors.Add($"{path} contains a negative count.");
        }
    }

    private static void ValidatePortfolioReferences(
        IReadOnlyList<string> references,
        HashSet<string> available,
        string path,
        List<string> errors)
    {
        RequireUniqueText(references, path, errors);
        if (references.Any(reference => !available.Contains(reference)) ||
            !PortfolioCanonical(references))
        {
            errors.Add($"{path} contains an invalid or non-canonical reference.");
        }
    }
}
