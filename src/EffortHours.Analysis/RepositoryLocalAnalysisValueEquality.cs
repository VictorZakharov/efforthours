using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;

namespace EffortHours.Analysis;

// Called only for repository-authored analyzer data objects, never target objects.
// Compare raw values, not JSON strings that can conflate invalid UTF-16. Unknown
// data shapes and excessive depth fail closed; collection order is significant.
internal static class RepositoryLocalAnalysisValueEquality
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> Properties = new();
    internal static bool Equal(object? left, object? right) => Equal(left, right, 0);
    private static bool Equal(object? left, object? right, int depth)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null || left.GetType() != right.GetType() || depth > 24) return false;
        Type type = left.GetType();
        if (type.IsPrimitive || type.IsEnum || left is string or decimal) return left.Equals(right);
        if (left is IEnumerable oldValues && right is IEnumerable newValues)
        {
            IEnumerator oldItems = oldValues.GetEnumerator(), newItems = newValues.GetEnumerator();
            try
            {
                int count = 0;
                while (oldItems.MoveNext())
                    if (++count > 100000 || !newItems.MoveNext() || !Equal(oldItems.Current, newItems.Current, depth + 1))
                        return false;
                return !newItems.MoveNext();
            }
            finally
            {
                (oldItems as IDisposable)?.Dispose();
                (newItems as IDisposable)?.Dispose();
            }
        }
        if (type.Namespace?.StartsWith("EffortHours.", StringComparison.Ordinal) != true) return false;
        PropertyInfo[] properties = Properties.GetOrAdd(type, type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance));
        return properties.Length > 0 && properties.All(property => property.GetIndexParameters().Length == 0 &&
            Equal(property.GetValue(left), property.GetValue(right), depth + 1));
    }
}
