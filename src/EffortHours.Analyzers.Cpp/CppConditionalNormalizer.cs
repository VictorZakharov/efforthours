using System.Text;

namespace EffortHours.Analyzers.Cpp;

internal static class CppConditionalNormalizer
{
    public static ConditionalSource Split(
        string source,
        CancellationToken cancellationToken = default)
    {
        string[] lines = source.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n').Split('\n');
        StringBuilder unconditional = new(source.Length);
        List<IReadOnlyList<string>> groups = [];
        for (int index = 0; index < lines.Length; index++)
        {
            if ((index & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
            string directive = DirectiveName(lines[index]);
            if (directive is not ("if" or "ifdef" or "ifndef"))
            {
                unconditional.AppendLine(lines[index]);
                continue;
            }

            int depth = 1;
            List<string> branches = [];
            StringBuilder branch = new();
            bool closed = false;
            for (index++; index < lines.Length; index++)
            {
                if ((index & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
                string nested = DirectiveName(lines[index]);
                if (nested is "if" or "ifdef" or "ifndef") depth++;
                if (nested == "endif")
                {
                    depth--;
                    if (depth == 0)
                    {
                        branches.Add(branch.ToString());
                        closed = true;
                        break;
                    }
                }
                if (depth == 1 && nested is "elif" or "elifdef" or "elifndef" or "else")
                {
                    branches.Add(branch.ToString());
                    branch.Clear();
                    continue;
                }
                branch.AppendLine(lines[index]);
            }
            if (closed) groups.Add(branches);
            else unconditional.Append(branch);
        }
        return new ConditionalSource(unconditional.ToString(), groups);
    }

    private static string DirectiveName(string line)
    {
        ReadOnlySpan<char> span = line.AsSpan().TrimStart();
        if (span.Length == 0 || span[0] != '#') return string.Empty;
        span = span[1..].TrimStart();
        int end = 0;
        while (end < span.Length && (char.IsAsciiLetter(span[end]) || span[end] == '_')) end++;
        return span[..end].ToString();
    }
}

internal sealed record ConditionalSource(
    string Unconditional,
    IReadOnlyList<IReadOnlyList<string>> Groups);
