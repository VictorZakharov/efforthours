using System.Buffers;

namespace EffortHours.Analysis;

internal enum IgnoreRuleCreationFailure
{
    None,
    PatternTooLong,
    InvalidCharacterClass,
}

/// <summary>
/// Matches the bounded gitignore subset admitted by <see cref="IgnoreRule"/>
/// without constructing runtime regular expressions from repository input.
/// </summary>
internal sealed class IgnoreGlobPattern
{
    internal const int MaximumPatternCharacters = 4_096;

    private const int StackStateLimit = 256;
    private readonly Token[] _tokens;

    private IgnoreGlobPattern(Token[] tokens)
    {
        _tokens = tokens;
    }

    public static bool TryCreate(
        string pattern,
        bool matchFromSegmentBoundary,
        out IgnoreGlobPattern? matcher,
        out IgnoreRuleCreationFailure failure)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        matcher = null;
        failure = IgnoreRuleCreationFailure.None;
        if (pattern.Length > MaximumPatternCharacters)
        {
            failure = IgnoreRuleCreationFailure.PatternTooLong;
            return false;
        }

        List<Token> tokens = [];
        if (matchFromSegmentBoundary)
        {
            tokens.Add(Token.DirectoryPrefix());
        }

        for (int index = 0; index < pattern.Length; index++)
        {
            char character = pattern[index];
            switch (character)
            {
                case '*':
                    if (index + 1 < pattern.Length && pattern[index + 1] == '*')
                    {
                        index++;
                        if (index + 1 < pattern.Length && pattern[index + 1] == '/')
                        {
                            index++;
                            tokens.Add(Token.DirectoryPrefix());
                        }
                        else
                        {
                            tokens.Add(Token.RecursiveStar());
                        }
                    }
                    else
                    {
                        tokens.Add(Token.Star());
                    }

                    break;

                case '?':
                    tokens.Add(Token.AnyCharacter());
                    break;

                case '[':
                    int closingBracket = pattern.IndexOf(']', index + 1);
                    if (closingBracket <= index + 1)
                    {
                        tokens.Add(Token.Literal('['));
                        break;
                    }

                    string characterClass = pattern[(index + 1)..closingBracket];
                    bool negatedClass = characterClass[0] is '!' or '^';
                    if (negatedClass)
                    {
                        characterClass = characterClass[1..];
                    }

                    if (characterClass.Length == 0)
                    {
                        AddLiterals(tokens, pattern.AsSpan(index, closingBracket - index + 1));
                        index = closingBracket;
                        break;
                    }

                    if (!TryCreateCharacterClass(characterClass, negatedClass, out CharacterClass? value))
                    {
                        failure = IgnoreRuleCreationFailure.InvalidCharacterClass;
                        return false;
                    }

                    tokens.Add(Token.CharacterClass(value!));
                    index = closingBracket;
                    break;

                case '\\' when index + 1 < pattern.Length:
                    index++;
                    tokens.Add(Token.Literal(pattern[index]));
                    break;

                default:
                    tokens.Add(Token.Literal(character));
                    break;
            }
        }

        matcher = new IgnoreGlobPattern([.. tokens]);
        return true;
    }

    public bool IsMatch(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        int stateCount = _tokens.Length + 1;
        bool[]? rentedCurrent = null;
        bool[]? rentedNext = null;
        Span<bool> current = stateCount <= StackStateLimit
            ? stackalloc bool[stateCount]
            : (rentedCurrent = ArrayPool<bool>.Shared.Rent(stateCount)).AsSpan(0, stateCount);
        Span<bool> next = stateCount <= StackStateLimit
            ? stackalloc bool[stateCount]
            : (rentedNext = ArrayPool<bool>.Shared.Rent(stateCount)).AsSpan(0, stateCount);

        try
        {
            current.Clear();
            next.Clear();
            current[0] = true;
            ExpandEmptyTransitions(current);
            foreach (char character in value)
            {
                next.Clear();
                for (int index = 0; index < _tokens.Length; index++)
                {
                    if (!current[index])
                    {
                        continue;
                    }

                    Token token = _tokens[index];
                    switch (token.Kind)
                    {
                        case TokenKind.Literal when token.LiteralValue == character:
                        case TokenKind.AnyCharacter when character != '/':
                            next[index + 1] = true;
                            break;

                        case TokenKind.CharacterClass when
                            character != '/' && token.CharacterClassValue!.Matches(character):
                            next[index + 1] = true;
                            break;

                        case TokenKind.Star when character != '/':
                        case TokenKind.RecursiveStar:
                            next[index] = true;
                            break;

                        case TokenKind.DirectoryPrefix:
                            next[index] = true;
                            if (character == '/')
                            {
                                next[index + 1] = true;
                            }

                            break;
                    }
                }

                ExpandEmptyTransitions(next);
                Span<bool> swap = current;
                current = next;
                next = swap;
            }

            return current[^1];
        }
        finally
        {
            if (rentedCurrent is not null)
            {
                ArrayPool<bool>.Shared.Return(rentedCurrent, clearArray: true);
            }

            if (rentedNext is not null)
            {
                ArrayPool<bool>.Shared.Return(rentedNext, clearArray: true);
            }
        }
    }

    private void ExpandEmptyTransitions(Span<bool> states)
    {
        for (int index = 0; index < _tokens.Length; index++)
        {
            if (states[index] && _tokens[index].Kind is
                TokenKind.Star or TokenKind.RecursiveStar or TokenKind.DirectoryPrefix)
            {
                states[index + 1] = true;
            }
        }
    }

    private static bool TryCreateCharacterClass(
        string value,
        bool negated,
        out CharacterClass? characterClass)
    {
        List<CharacterRange> ranges = [];
        for (int index = 0; index < value.Length;)
        {
            if (index + 2 < value.Length && value[index + 1] == '-')
            {
                char start = value[index];
                char end = value[index + 2];
                if (start > end)
                {
                    characterClass = null;
                    return false;
                }

                ranges.Add(new CharacterRange(start, end));
                index += 3;
            }
            else
            {
                ranges.Add(new CharacterRange(value[index], value[index]));
                index++;
            }
        }

        characterClass = new CharacterClass(negated, [.. ranges]);
        return true;
    }

    private static void AddLiterals(List<Token> tokens, ReadOnlySpan<char> value)
    {
        foreach (char character in value)
        {
            tokens.Add(Token.Literal(character));
        }
    }

    private enum TokenKind
    {
        Literal,
        AnyCharacter,
        Star,
        RecursiveStar,
        DirectoryPrefix,
        CharacterClass,
    }

    private readonly record struct Token(
        TokenKind Kind,
        char LiteralValue = default,
        CharacterClass? CharacterClassValue = null)
    {
        public static Token Literal(char value) => new(TokenKind.Literal, value);

        public static Token AnyCharacter() => new(TokenKind.AnyCharacter);

        public static Token Star() => new(TokenKind.Star);

        public static Token RecursiveStar() => new(TokenKind.RecursiveStar);

        public static Token DirectoryPrefix() => new(TokenKind.DirectoryPrefix);

        public static Token CharacterClass(CharacterClass value) =>
            new(TokenKind.CharacterClass, CharacterClassValue: value);
    }

    private sealed record CharacterClass(bool Negated, IReadOnlyList<CharacterRange> Ranges)
    {
        public bool Matches(char value)
        {
            bool matched = Ranges.Any(range => value >= range.Start && value <= range.End);
            return Negated ? !matched : matched;
        }
    }

    private readonly record struct CharacterRange(char Start, char End);
}
