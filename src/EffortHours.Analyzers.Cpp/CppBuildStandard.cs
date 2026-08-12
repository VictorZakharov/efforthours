namespace EffortHours.Analyzers.Cpp;

internal static class CppBuildStandard
{
    public static string Normalize(string value)
    {
        string lower = value.Trim().ToLowerInvariant();
        int separator = lower.IndexOfAny(['=', ':']);
        if (separator >= 0) lower = lower[(separator + 1)..];
        if (lower.StartsWith("gnu++", StringComparison.Ordinal)) lower = "c++" + lower[5..];
        if (lower.StartsWith("gnu", StringComparison.Ordinal)) lower = "c" + lower[3..];
        if (lower.StartsWith("stdcpp", StringComparison.Ordinal)) lower = "c++" + lower[6..];
        return lower is "c99" or "c11" or "c17" or "c23" or
            "c++11" or "c++14" or "c++17" or "c++20" or "c++23" or "c++latest"
                ? lower
                : string.Empty;
    }
}
