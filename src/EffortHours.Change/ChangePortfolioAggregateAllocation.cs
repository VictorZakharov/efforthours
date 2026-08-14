using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal sealed record ChangePortfolioAggregateAllocationInput(
    string Id,
    decimal ExpectedWeight,
    decimal HighFallbackWeight);

internal static class ChangePortfolioAggregateAllocation
{
    public static IReadOnlyDictionary<string, EffortRange> Allocate(
        EffortRange total,
        IReadOnlyList<ChangePortfolioAggregateAllocationInput> inputs)
    {
        if (inputs.Count == 0)
        {
            if (total.Low != 0m || total.Expected != 0m || total.High != 0m)
            {
                throw new InvalidOperationException(
                    "A non-zero portfolio range cannot be allocated without attribution groups.");
            }

            return new Dictionary<string, EffortRange>(StringComparer.Ordinal);
        }

        decimal expectedWeight = inputs.Sum(input => input.ExpectedWeight);
        if (expectedWeight != total.Expected)
        {
            throw new InvalidOperationException(
                "Attribution-group expected weights do not reconcile to repository-normalized effort.");
        }

        decimal[] primaryWeights = [.. inputs.Select(input => input.ExpectedWeight)];
        decimal[] highWeights = expectedWeight > 0m
            ? primaryWeights
            : [.. inputs.Select(input => input.HighFallbackWeight)];
        decimal[] low = AllocatePoint(total.Low, primaryWeights, inputs);
        decimal[] high = AllocatePoint(total.High, highWeights, inputs);
        Dictionary<string, EffortRange> allocations = new(StringComparer.Ordinal);
        for (int index = 0; index < inputs.Count; index++)
        {
            EffortRange range = new()
            {
                Low = low[index],
                Expected = inputs[index].ExpectedWeight,
                High = high[index],
            };
            if (range.Low > range.Expected || range.Expected > range.High)
            {
                throw new InvalidOperationException(
                    $"Attribution range '{inputs[index].Id}' is not ordered low <= expected <= high.");
            }

            allocations.Add(inputs[index].Id, range);
        }

        return allocations;
    }

    private static decimal[] AllocatePoint(
        decimal total,
        decimal[] weights,
        IReadOnlyList<ChangePortfolioAggregateAllocationInput> inputs)
    {
        decimal[] result = new decimal[inputs.Count];
        if (total == 0m)
        {
            return result;
        }

        decimal weightTotal = weights.Sum();
        int[] eligible = weightTotal > 0m
            ? [.. Enumerable.Range(0, inputs.Count).Where(index => weights[index] > 0m)]
            : [.. Enumerable.Range(0, inputs.Count)];
        decimal[] remainders = new decimal[inputs.Count];
        foreach (int index in eligible)
        {
            decimal raw = weightTotal > 0m
                ? total * weights[index] / weightTotal
                : total / eligible.Length;
            result[index] = decimal.Floor(raw * 100m) / 100m;
            remainders[index] = raw - result[index];
        }

        decimal residual = total - result.Sum();
        foreach (int index in eligible
            .OrderByDescending(index => remainders[index])
            .ThenBy(index => inputs[index].Id, StringComparer.Ordinal))
        {
            if (residual <= 0m)
            {
                break;
            }

            decimal increment = Math.Min(0.01m, residual);
            result[index] += increment;
            residual -= increment;
        }

        if (residual > 0m)
        {
            result[eligible[0]] += residual;
        }

        return result;
    }
}
