namespace EffortHours.Calibration;

internal static class CalibrationUncertaintyGraphComponents
{
    public static IReadOnlyDictionary<string, int> FindCyclicComponents(
        IReadOnlyDictionary<string, HashSet<string>> outgoing)
    {
        List<string> finishOrder = [];
        HashSet<string> visited = new(StringComparer.Ordinal);
        foreach (string node in outgoing.Keys.Order(StringComparer.Ordinal))
        {
            AddFinishOrder(node, outgoing, visited, finishOrder);
        }

        Dictionary<string, HashSet<string>> reverse = outgoing.Keys.ToDictionary(
            node => node,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        foreach ((string source, HashSet<string> targets) in outgoing)
        {
            foreach (string target in targets)
            {
                reverse[target].Add(source);
            }
        }

        Dictionary<string, int> cyclicSizes = new(StringComparer.Ordinal);
        visited.Clear();
        for (int index = finishOrder.Count - 1; index >= 0; index--)
        {
            string start = finishOrder[index];
            if (visited.Contains(start))
            {
                continue;
            }

            string[] component = VisitComponent(start, reverse, visited);
            bool cyclic = component.Length > 1 || outgoing[start].Contains(start);
            if (cyclic)
            {
                foreach (string member in component)
                {
                    cyclicSizes.Add(member, component.Length);
                }
            }
        }

        return cyclicSizes;
    }

    private static void AddFinishOrder(
        string start,
        IReadOnlyDictionary<string, HashSet<string>> outgoing,
        HashSet<string> visited,
        List<string> finishOrder)
    {
        if (visited.Contains(start))
        {
            return;
        }

        Stack<(string Node, bool Expanded)> stack = new();
        stack.Push((start, false));
        while (stack.Count > 0)
        {
            (string node, bool expanded) = stack.Pop();
            if (expanded)
            {
                finishOrder.Add(node);
                continue;
            }

            if (!visited.Add(node))
            {
                continue;
            }

            stack.Push((node, true));
            foreach (string target in outgoing[node].OrderDescending(StringComparer.Ordinal))
            {
                if (!visited.Contains(target))
                {
                    stack.Push((target, false));
                }
            }
        }
    }

    private static string[] VisitComponent(
        string start,
        Dictionary<string, HashSet<string>> reverse,
        HashSet<string> visited)
    {
        List<string> component = [];
        Stack<string> stack = new();
        stack.Push(start);
        visited.Add(start);
        while (stack.Count > 0)
        {
            string node = stack.Pop();
            component.Add(node);
            foreach (string source in reverse[node].OrderDescending(StringComparer.Ordinal))
            {
                if (visited.Add(source))
                {
                    stack.Push(source);
                }
            }
        }

        return [.. component.Order(StringComparer.Ordinal)];
    }
}
