using System.Globalization;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

internal static partial class ChangeWorkItemBuilder
{
    private sealed class BudgetBridge
    {
        private sealed class Row
        {
            public decimal Positive, Negative, Growth, Modification, Removal, Redistribution;
        }

        private readonly Dictionary<EffortCategory, Row> _rows = [];
        private Row Get(EffortCategory category)
        {
            if (!_rows.TryGetValue(category, out Row? row)) _rows[category] = row = new();
            return row;
        }

        public BudgetBridge(Dictionary<string, Capability> before, Dictionary<string, Capability> after)
        {
            foreach (string id in before.Keys.Union(after.Keys, StringComparer.Ordinal))
            {
                before.TryGetValue(id, out Capability? left);
                after.TryGetValue(id, out Capability? right);
                if (left is not null && right is not null && left.Category == right.Category)
                {
                    decimal net = right.Hours.Expected - left.Hours.Expected;
                    Get(left.Category).Positive += Math.Max(0m, net);
                    Get(left.Category).Negative += Math.Max(0m, -net);
                }
                else
                {
                    if (left is not null) Get(left.Category).Negative += left.Hours.Expected;
                    if (right is not null) Get(right.Category).Positive += right.Hours.Expected;
                }
            }
        }

        public void Record(EffortCategory category, string rule, decimal hours, decimal growth,
            CapabilityRolePartition[] partitions)
        {
            Row row = Get(category);
            if (rule == "capability-removal") row.Removal += hours;
            else
            {
                row.Growth += growth;
                row.Modification += hours - growth;
            }
            row.Redistribution -= hours;
            foreach (CapabilityRolePartition partition in partitions)
                Get(partition.Category).Redistribution += partition.Hours.Expected;
        }

        public Diagnostic[] Diagnostics(WorkItem[] items) => [.. _rows.Keys.Union(items.Select(item => item.Category))
            .Order().Select(category =>
            {
                Row row = Get(category);
                WorkItem[] work = [.. items.Where(item => item.Category == category)];
                decimal fallback = work.Where(item => item.Estimator.Id == "change-rule:maintained-artifact-fallback")
                    .Sum(item => item.Hours.Expected);
                decimal changeLevel = work.Where(item => item.Estimator.Id.StartsWith("change-rule:change-", StringComparison.Ordinal))
                    .Sum(item => item.Hours.Expected);
                decimal expected = work.Sum(item => item.Hours.Expected);
                decimal omitted = row.Positive - row.Growth;
                decimal signed = row.Positive - row.Negative;
                decimal explained = signed + row.Negative - omitted + row.Modification + row.Removal +
                    row.Redistribution + fallback + changeLevel;
                if (explained != expected)
                    throw new InvalidOperationException("The Change capability budget bridge does not reconcile.");
                static string Number(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
                return new Diagnostic
                {
                    Code = "FB5210", Severity = DiagnosticSeverity.Information,
                    Message = $"change-capability-budget-bridge/1.0.0: category={category}; " +
                        $"signedStock={Number(signed)}; negativeStockCredit={Number(row.Negative)}; " +
                        $"growthNotRetained={Number(omitted)}; retainedGrowth={Number(row.Growth)}; " +
                        $"modificationExcess={Number(row.Modification)}; removalWork={Number(row.Removal)}; " +
                        $"roleRedistribution={Number(row.Redistribution)}; fallback={Number(fallback)}; " +
                        $"changeLevel={Number(changeLevel)}; changeExpected={Number(expected)}.",
                    EvidenceIds = [.. work.SelectMany(item => item.EvidenceIds).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
                };
            })];
    }
}
