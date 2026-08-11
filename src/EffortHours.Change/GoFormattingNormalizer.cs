using System.Globalization;
using System.Text;

namespace EffortHours.Change;

internal static class GoFormattingNormalizer
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "break", "case", "chan", "const", "continue", "default", "defer", "else",
        "fallthrough", "for", "func", "go", "goto", "if", "import", "interface",
        "map", "package", "range", "return", "select", "struct", "switch", "type", "var",
    };

    private static readonly string[] MultiCharacterOperators =
    [
        "<<=", ">>=", "&^=", "...", "==", "!=", "<=", ">=", ":=", "++", "--",
        "&&", "||", "<-", "<<", ">>", "&^", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=",
    ];

    public static bool TryCreateSignature(string source, out string signature)
    {
        ArgumentNullException.ThrowIfNull(source);
        StringBuilder result = new(source.Length);
        int index = 0;
        int delimiterDepth = 0;
        bool valid = true;
        bool semicolonEligible = false;
        bool preserveBlockComments = ContainsCgoImport(source);

        while (index < source.Length && valid)
        {
            char current = source[index];
            if (current is ' ' or '\t' or '\f')
            {
                index++;
            }
            else if (IsNewLine(source, index))
            {
                ConsumeNewLine(source, ref index);
                AppendImplicitSemicolon(result, ref semicolonEligible);
            }
            else if (current == '/' && Peek(source, index, 1) == '/')
            {
                int start = index;
                while (index < source.Length && !IsNewLine(source, index)) index++;
                string comment = source[start..index].Trim();
                if (IsCompilerDirective(comment)) AppendToken(result, "directive:" + comment);
            }
            else if (current == '/' && Peek(source, index, 1) == '*')
            {
                int start = index;
                index += 2;
                bool hasNewLine = false;
                bool closed = false;
                while (index < source.Length)
                {
                    if (source[index] == '*' && Peek(source, index, 1) == '/')
                    {
                        index += 2;
                        closed = true;
                        break;
                    }

                    if (IsNewLine(source, index))
                    {
                        hasNewLine = true;
                        ConsumeNewLine(source, ref index);
                    }
                    else
                    {
                        index++;
                    }
                }

                valid = closed;
                if (valid && preserveBlockComments) AppendToken(result, source[start..index]);
                if (hasNewLine) AppendImplicitSemicolon(result, ref semicolonEligible);
            }
            else if (current is '"' or '\'' or '`')
            {
                valid = ReadLiteral(source, ref index, current, out string literal);
                if (valid)
                {
                    AppendToken(result, literal);
                    semicolonEligible = true;
                }
            }
            else if (current == '_' || char.IsLetter(current))
            {
                int start = index++;
                while (index < source.Length &&
                    (source[index] == '_' || char.IsLetterOrDigit(source[index]))) index++;
                string token = source[start..index];
                AppendToken(result, token);
                semicolonEligible = IsSemicolonEligibleWord(token);
            }
            else if (char.IsDigit(current) || current == '.' && char.IsDigit(Peek(source, index, 1)))
            {
                int start = index++;
                while (index < source.Length &&
                    (char.IsAsciiLetterOrDigit(source[index]) || source[index] is '.' or '_')) index++;
                AppendToken(result, source[start..index]);
                semicolonEligible = true;
            }
            else
            {
                string token = MultiCharacterOperators.FirstOrDefault(candidate =>
                    Matches(source, index, candidate)) ?? current.ToString();
                index += token.Length;
                if (token is "(" or "[" or "{") delimiterDepth++;
                if (token is ")" or "]" or "}") delimiterDepth--;
                valid = delimiterDepth >= 0;
                AppendToken(result, token);
                if (token == ";") semicolonEligible = false;
                else semicolonEligible = token is "++" or "--" or ")" or "]" or "}";
            }
        }

        valid &= delimiterDepth == 0;
        if (valid) AppendImplicitSemicolon(result, ref semicolonEligible);
        signature = valid ? result.ToString() : string.Empty;
        return valid;
    }

    private static bool ReadLiteral(
        string source,
        ref int index,
        char quote,
        out string literal)
    {
        int start = index++;
        while (index < source.Length)
        {
            if (quote != '`' && source[index] == '\\')
            {
                index++;
                if (index < source.Length) index++;
            }
            else if (source[index] == quote)
            {
                index++;
                literal = source[start..index];
                return true;
            }
            else if (quote != '`' && IsNewLine(source, index))
            {
                break;
            }
            else
            {
                index++;
            }
        }

        literal = string.Empty;
        return false;
    }

    private static void AppendImplicitSemicolon(StringBuilder result, ref bool eligible)
    {
        if (!eligible) return;
        AppendToken(result, ";");
        eligible = false;
    }

    private static void AppendToken(StringBuilder result, string token)
    {
        result.Append(token.Length.ToString(CultureInfo.InvariantCulture));
        result.Append(':');
        result.Append(token);
        result.Append(';');
    }

    private static bool ContainsCgoImport(string source)
    {
        int index = 0;
        while (index < source.Length)
        {
            if (source[index] == '/' && Peek(source, index, 1) == '/')
            {
                SkipLineComment(source, ref index);
            }
            else if (source[index] == '/' && Peek(source, index, 1) == '*')
            {
                SkipBlockComment(source, ref index);
            }
            else if (source[index] is '"' or '\'' or '`')
            {
                if (!ReadLiteral(source, ref index, source[index], out _)) return false;
            }
            else if (source[index] == '_' || char.IsLetter(source[index]))
            {
                int start = index++;
                while (index < source.Length &&
                    (source[index] == '_' || char.IsLetterOrDigit(source[index]))) index++;
                if (source.AsSpan(start, index - start).SequenceEqual("import") &&
                    ImportContainsC(source, ref index)) return true;
            }
            else
            {
                index++;
            }
        }

        return false;
    }

    private static bool ImportContainsC(string source, ref int index)
    {
        SkipTrivia(source, ref index);
        if (index >= source.Length) return false;
        if (source[index] != '(') return ReadImportPath(source, ref index);

        index++;
        while (index < source.Length && source[index] != ')')
        {
            SkipTrivia(source, ref index);
            if (index >= source.Length || source[index] == ')') break;
            if (ReadImportPath(source, ref index)) return true;
            index++;
        }

        if (index < source.Length) index++;
        return false;
    }

    private static bool ReadImportPath(string source, ref int index)
    {
        SkipTrivia(source, ref index);
        if (index >= source.Length) return false;
        if (source[index] is '.' or '_')
        {
            index++;
            SkipTrivia(source, ref index);
        }
        else if (char.IsLetter(source[index]))
        {
            while (index < source.Length &&
                (source[index] == '_' || char.IsLetterOrDigit(source[index]))) index++;
            SkipTrivia(source, ref index);
        }

        if (index >= source.Length || source[index] is not ('"' or '`')) return false;
        return ReadLiteral(source, ref index, source[index], out string literal) &&
            literal is "\"C\"" or "`C`";
    }

    private static void SkipTrivia(string source, ref int index)
    {
        bool advanced;
        do
        {
            advanced = false;
            while (index < source.Length && char.IsWhiteSpace(source[index]))
            {
                index++;
                advanced = true;
            }

            if (index < source.Length && source[index] == '/' && Peek(source, index, 1) == '/')
            {
                SkipLineComment(source, ref index);
                advanced = true;
            }
            else if (index < source.Length && source[index] == '/' && Peek(source, index, 1) == '*')
            {
                SkipBlockComment(source, ref index);
                advanced = true;
            }
        }
        while (advanced);
    }

    private static void SkipLineComment(string source, ref int index)
    {
        index += 2;
        while (index < source.Length && !IsNewLine(source, index)) index++;
    }

    private static void SkipBlockComment(string source, ref int index)
    {
        index += 2;
        while (index < source.Length &&
            !(source[index] == '*' && Peek(source, index, 1) == '/')) index++;
        if (index < source.Length) index += 2;
    }

    private static bool IsSemicolonEligibleWord(string token) =>
        token is "break" or "continue" or "fallthrough" or "return" ||
        !Keywords.Contains(token);

    private static bool IsCompilerDirective(string comment) =>
        comment.StartsWith("//go:", StringComparison.Ordinal) ||
        comment.StartsWith("// +build", StringComparison.Ordinal);

    private static bool Matches(string source, int index, string candidate) =>
        index + candidate.Length <= source.Length &&
        source.AsSpan(index, candidate.Length).SequenceEqual(candidate);

    private static char Peek(string source, int index, int offset) =>
        index + offset < source.Length ? source[index + offset] : '\0';

    private static bool IsNewLine(string source, int index) =>
        index < source.Length && source[index] is '\r' or '\n';

    private static void ConsumeNewLine(string source, ref int index)
    {
        if (source[index] == '\r' && Peek(source, index, 1) == '\n') index += 2;
        else index++;
    }
}
