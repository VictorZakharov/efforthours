namespace EffortHours.RepositoryCalibration;

internal static class SourceSizeMetric
{
    public static bool IsEligible(string path, SamplingSizeMetric metric)
    {
        string normalized = path.Replace('\\', '/');
        string extension = Path.GetExtension(normalized);
        if (!metric.EligibleExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment =>
                metric.ExcludedPathSegments.Contains(segment, StringComparer.OrdinalIgnoreCase)))
        {
            return false;
        }

        return !metric.ExcludedPathSequences.Any(sequence =>
            ContainsSequence(segments, sequence.Split('/', StringSplitOptions.RemoveEmptyEntries)));
    }

    private static bool ContainsSequence(string[] path, string[] sequence)
    {
        if (sequence.Length == 0 || sequence.Length > path.Length)
        {
            return false;
        }

        for (int offset = 0; offset <= path.Length - sequence.Length; offset++)
        {
            bool matches = true;
            for (int index = 0; index < sequence.Length; index++)
            {
                if (!string.Equals(path[offset + index], sequence[index], StringComparison.OrdinalIgnoreCase))
                {
                    matches = false;
                    break;
                }
            }

            if (matches)
            {
                return true;
            }
        }

        return false;
    }
}
