using System.Text.RegularExpressions;
using EffortHours.Contracts.V1;

namespace EffortHours.Change;

public sealed record SnapshotAreaInput(string Id, GitArchiveSnapshot Snapshot, int OwnedFiles, int ContextFiles);

public static class SnapshotAreaPartition
{
    public static IReadOnlyList<SnapshotAreaInput> Partition(GitArchiveSnapshot snapshot,
        IReadOnlyList<SnapshotAreaDefinition> definitions, Action? selectorCompiled = null, SnapshotAreaSelectors? selectors = null, CancellationToken cancellationToken = default)
    {
        ValidateDefinitions(definitions);
        if (selectors is not null && selectors.DefinitionDigest != SnapshotMeasurementIdentity.Digest(definitions))
            throw new SnapshotPlanningException("invalid-area-definition", "Compiled selectors belong to a different reviewed definition.");
        List<List<string>> owned = [.. definitions.Select(_ => new List<string>())];
        Regex[][] patterns = (selectors ?? new SnapshotAreaSelectors(definitions, selectorCompiled)).Patterns;
        for (int i = 0; i < patterns.Length; i++)
            foreach (Regex pattern in patterns[i])
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!snapshot.Files.Keys.Any(path => { cancellationToken.ThrowIfCancellationRequested(); return pattern.IsMatch(path); }))
                    throw new SnapshotPlanningException("unmatched-selector", "A reviewed area selector no longer matches archived files; review the boundary.", definitions[i].Id);
            }
        foreach (string path in snapshot.Files.Keys.Order(StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            int owner = Array.FindIndex(patterns, patternsForArea => patternsForArea.Any(p => p.IsMatch(path)));
            if (owner < 0) throw new InvalidDataException("An archived file has no reviewed area owner.");
            owned[owner].Add(path);
        }
        List<SnapshotAreaInput> result = [];
        for (int i = 0; i < owned.Count; i++)
        {
            if (owned[i].Count == 0) throw new SnapshotPlanningException("empty-owned-area", "A reviewed area owns no archived files; review the boundary.", definitions[i].Id);
            HashSet<string> included = new(owned[i], StringComparer.Ordinal);
            foreach (string path in owned[i])
            {
                string parent = path.Contains('/') ? path[..path.LastIndexOf('/')] : "";
                while (true)
                {
                    foreach (string control in new[] { ".gitignore", ".efforthoursignore" })
                    {
                        string candidate = parent.Length == 0 ? control : parent + "/" + control;
                        if (snapshot.Files.ContainsKey(candidate)) included.Add(candidate);
                    }
                    if (parent.Length == 0) break;
                    parent = parent.Contains('/') ? parent[..parent.LastIndexOf('/')] : "";
                }
            }
            result.Add(new(definitions[i].Id, snapshot.Select(included), owned[i].Count, included.Count - owned[i].Count));
        }
        return result;
    }

    public static void ValidateDefinitions(IReadOnlyList<SnapshotAreaDefinition> definitions)
    {
        if (definitions.Count is < 1 or > 256 || definitions[^1].Include.Count != 1 || definitions[^1].Include[0] != "**")
            throw new InvalidDataException("Areas require an ordered exhaustive final '**' catch-all (maximum 256 areas).");
        if (definitions.Select(d => d.Id).Distinct(StringComparer.Ordinal).Count() != definitions.Count)
            throw new InvalidDataException("Area IDs must be unique.");
        foreach (SnapshotAreaDefinition definition in definitions)
        {
            SnapshotPortfolioValidation.RequireId(definition.Id);
            if (definition.Include.Count is < 1 or > 64) throw new InvalidDataException("Each area requires 1 to 64 include selectors.");
            foreach (string selector in definition.Include)
            {
                GitArchiveSnapshot.RequireSafePath(selector);
                if (selector.Contains('[') || selector.Contains(']') || selector.Contains('!'))
                    throw new InvalidDataException("Area selectors support relative paths, '*', '**', and '?' only.");
            }
        }
    }

    internal static Regex Compile(string selector)
    {
        string expression = Regex.Escape(selector)
            .Replace(@"\*\*/", "(?:.*/)?", StringComparison.Ordinal)
            .Replace(@"\*\*", ".*", StringComparison.Ordinal)
            .Replace(@"\*", "[^/]*", StringComparison.Ordinal)
            .Replace(@"\?", "[^/]", StringComparison.Ordinal);
        if (!selector.Contains('*') && !selector.Contains('?')) expression += "(?:/.*)?";
        return new("^" + expression + "$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    }

    public static IReadOnlyList<decimal> Allocate(decimal total, IReadOnlyList<decimal> weights)
    {
        if (total < 0 || total != decimal.Round(total, 2) || weights.Any(w => w < 0))
            throw new InvalidDataException("Allocation requires nonnegative hours and a cent-hour project total.");
        decimal sum = weights.Sum();
        if (total > 0 && sum == 0) throw new InvalidDataException("Positive project effort requires positive standalone area weights.");
        if (sum == 0) return [.. weights.Select(_ => 0m)];
        decimal[] exact = [.. weights.Select(w => total * 100m * w / sum)];
        decimal[] cents = [.. exact.Select(decimal.Floor)];
        int missing = decimal.ToInt32(total * 100m - cents.Sum());
        foreach (int index in Enumerable.Range(0, weights.Count).OrderByDescending(i => exact[i] - cents[i]).ThenBy(i => i).Take(missing))
            cents[index]++;
        return [.. cents.Select(c => c / 100m)];
    }
}
