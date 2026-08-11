using System.Globalization;
using System.Text;

namespace EffortHours.Change;

internal static class PythonFormattingNormalizer
{
    public static bool TryCreateSignature(string source, out string signature)
    {
        ArgumentNullException.ThrowIfNull(source);
        StringBuilder result = new(source.Length);
        List<int> indentation = [0];
        int index = 0;
        int delimiterDepth = 0;
        bool lineStart = true;
        bool lineHasCode = false;
        bool valid = true;

        while (index < source.Length && valid)
        {
            if (lineStart && delimiterDepth == 0)
            {
                int width = 0;
                while (index < source.Length && source[index] is ' ' or '\t' or '\f')
                {
                    width = source[index] == '\t' ? ((width / 8) + 1) * 8 : width + 1;
                    index++;
                }

                if (index >= source.Length) break;
                if (source[index] == '#' || IsNewLine(source, index))
                {
                    lineStart = false;
                    continue;
                }

                if (width > indentation[^1])
                {
                    indentation.Add(width);
                    result.Append("I;");
                }
                else if (width < indentation[^1])
                {
                    while (indentation.Count > 1 && width < indentation[^1])
                    {
                        indentation.RemoveAt(indentation.Count - 1);
                        result.Append("D;");
                    }

                    valid = width == indentation[^1];
                }

                lineStart = false;
                continue;
            }

            char current = source[index];
            if (current is ' ' or '\t' or '\f')
            {
                index++;
            }
            else if (current == '#')
            {
                while (index < source.Length && !IsNewLine(source, index)) index++;
            }
            else if (IsNewLine(source, index))
            {
                ConsumeNewLine(source, ref index);
                if (delimiterDepth == 0 && lineHasCode) result.Append("N;");
                lineStart = true;
                if (delimiterDepth == 0) lineHasCode = false;
            }
            else if (current == '\\' && IsNewLine(source, index + 1))
            {
                index++;
                ConsumeNewLine(source, ref index);
                lineStart = false;
            }
            else if (TryStringStart(source, index, out int quoteIndex))
            {
                valid = ReadString(source, ref index, quoteIndex, result);
                lineHasCode = true;
            }
            else if (current == '_' || char.IsLetter(current))
            {
                int start = index++;
                while (index < source.Length &&
                    (source[index] == '_' || char.IsLetterOrDigit(source[index]))) index++;
                AppendToken(result, source[start..index]);
                lineHasCode = true;
            }
            else if (char.IsDigit(current))
            {
                int start = index++;
                while (index < source.Length &&
                    (char.IsAsciiLetterOrDigit(source[index]) || source[index] is '.' or '_')) index++;
                AppendToken(result, source[start..index]);
                lineHasCode = true;
            }
            else
            {
                if (current is '(' or '[' or '{') delimiterDepth++;
                if (current is ')' or ']' or '}') delimiterDepth--;
                valid = delimiterDepth >= 0;
                AppendToken(result, current.ToString());
                lineHasCode = true;
                index++;
            }
        }

        valid &= delimiterDepth == 0;
        while (indentation.Count > 1)
        {
            indentation.RemoveAt(indentation.Count - 1);
            result.Append("D;");
        }

        signature = valid ? result.ToString() : string.Empty;
        return valid;
    }

    private static bool ReadString(
        string source,
        ref int index,
        int quoteIndex,
        StringBuilder result)
    {
        int start = index;
        index = quoteIndex;
        char quote = source[index];
        bool triple = index + 2 < source.Length &&
            source[index + 1] == quote && source[index + 2] == quote;
        int length = triple ? 3 : 1;
        index += length;
        while (index < source.Length)
        {
            if (source[index] == '\\')
            {
                index = Math.Min(source.Length, index + 2);
                continue;
            }

            if (source[index] == quote &&
                (!triple || index + 2 < source.Length &&
                    source[index + 1] == quote && source[index + 2] == quote))
            {
                index += length;
                AppendToken(result, source[start..index]);
                return true;
            }

            if (!triple && IsNewLine(source, index)) return false;
            index++;
        }

        return false;
    }

    private static bool TryStringStart(string source, int index, out int quoteIndex)
    {
        quoteIndex = index;
        if (source[index] is '\'' or '"') return true;
        int probe = index;
        while (probe < source.Length && probe - index < 3 &&
            source[probe] is 'r' or 'R' or 'b' or 'B' or 'u' or 'U' or 'f' or 'F') probe++;
        if (probe > index && probe < source.Length && source[probe] is '\'' or '"')
        {
            quoteIndex = probe;
            return true;
        }

        return false;
    }

    private static void AppendToken(StringBuilder result, string token)
    {
        result.Append(token.Length.ToString(CultureInfo.InvariantCulture));
        result.Append(':');
        result.Append(token);
        result.Append(';');
    }

    private static bool IsNewLine(string source, int index) =>
        index < source.Length && source[index] is '\r' or '\n';

    private static void ConsumeNewLine(string source, ref int index)
    {
        if (source[index] == '\r' && index + 1 < source.Length && source[index + 1] == '\n')
            index += 2;
        else
            index++;
    }
}
