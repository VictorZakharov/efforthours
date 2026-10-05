using System.Text;

namespace EffortHours.Core;

internal static partial class RepositoryEvidenceIncrementalDeriver
{
    // Scheduling only: digit runs in TS/SQL are a cheap likelihood hint. Reuse
    // still requires complete common/local/context/duplicate proofs, never this test.
    internal static bool IsSmallNumberedSourceEdit(byte[] before, byte[] after)
    {
        if (before.Length != after.Length || before.Length > 65536) return false;
        string left = Encoding.UTF8.GetString(before), right = Encoding.UTF8.GetString(after);
        int oldIndex = 0, newIndex = 0;
        while (oldIndex < left.Length && newIndex < right.Length)
        {
            if (char.IsAsciiDigit(left[oldIndex]) && char.IsAsciiDigit(right[newIndex]))
            {
                while (oldIndex < left.Length && char.IsAsciiDigit(left[oldIndex])) oldIndex++;
                while (newIndex < right.Length && char.IsAsciiDigit(right[newIndex])) newIndex++;
            }
            else if (left[oldIndex++] != right[newIndex++]) return false;
        }
        return oldIndex == left.Length && newIndex == right.Length;
    }
}
