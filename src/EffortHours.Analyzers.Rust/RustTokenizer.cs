namespace EffortHours.Analyzers.Rust;

internal sealed class RustTokenizer
{
    public const int MaximumTokens = 250_000;

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "as", "async", "await", "break", "const", "continue", "crate", "dyn", "else",
        "enum", "extern", "false", "fn", "for", "if", "impl", "in", "let", "loop",
        "macro", "match", "mod", "move", "mut", "pub", "ref", "return", "self",
        "Self", "static", "struct", "super", "trait", "true", "type", "union", "unsafe",
        "use", "where", "while", "yield", "abstract", "become", "box", "do", "final",
        "macro_rules", "override", "priv", "try", "typeof", "unsized", "virtual",
    };

    private static readonly string[] MultiCharacterOperators =
    [
        "<<=", ">>=", "..=", "...", "::", "->", "=>", "==", "!=", "<=", ">=", "&&",
        "||", "<<", ">>", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "..",
    ];

    private readonly List<RustToken> _tokens = [];
    private readonly Stack<char> _delimiters = [];
    private string _source = string.Empty;
    private int _index;
    private int _line = 1;
    private int _column = 1;
    private bool _truncated;
    private bool _unterminatedLiteral;
    private bool _unterminatedComment;
    private bool _invalidDelimiter;

    public static RustTokenization Tokenize(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new RustTokenizer().Run(source);
    }

    private RustTokenization Run(string source)
    {
        _source = source;
        while (_index < _source.Length && !_truncated)
        {
            char current = _source[_index];
            if (char.IsWhiteSpace(current)) ConsumeWhitespace();
            else if (current == '/' && Peek(1) == '/') ReadLineComment();
            else if (current == '/' && Peek(1) == '*') ReadBlockComment();
            else if (TryReadRawString()) { }
            else if (IsQuotedStringStart()) ReadQuotedString();
            else if (current == '\'') ReadApostrophe();
            else if (IsIdentifierStart(current)) ReadIdentifier();
            else if (char.IsDigit(current)) ReadNumber();
            else ReadOperator();
        }

        Add(RustTokenKind.End, string.Empty, _line, _column, allowAtLimit: true);
        return new RustTokenization(
            _tokens,
            _truncated,
            _unterminatedLiteral,
            _unterminatedComment,
            _delimiters.Count > 0 || _invalidDelimiter);
    }

    private void ConsumeWhitespace()
    {
        if (_source[_index] is '\r' or '\n') ConsumeNewLine();
        else Advance();
    }

    private void ReadLineComment()
    {
        int line = _line;
        int column = _column;
        int start = _index;
        bool documentation = Peek(2) is '/' or '!';
        while (_index < _source.Length && _source[_index] is not ('\r' or '\n')) Advance();
        if (documentation) Add(RustTokenKind.Documentation, _source[start.._index], line, column);
    }

    private void ReadBlockComment()
    {
        int line = _line;
        int column = _column;
        int start = _index;
        bool documentation = Peek(2) is '*' or '!';
        int depth = 0;
        while (_index < _source.Length)
        {
            if (_source[_index] == '/' && Peek(1) == '*')
            {
                depth++;
                Advance(2);
            }
            else if (_source[_index] == '*' && Peek(1) == '/')
            {
                depth--;
                Advance(2);
                if (depth == 0) break;
            }
            else if (_source[_index] is '\r' or '\n') ConsumeNewLine();
            else Advance();
        }

        if (depth != 0) _unterminatedComment = true;
        if (documentation) Add(RustTokenKind.Documentation, _source[start.._index], line, column);
    }

    private bool TryReadRawString()
    {
        int prefixLength = RawPrefixLength();
        if (prefixLength < 0) return false;
        int openingQuote = _index + prefixLength;
        while (openingQuote < _source.Length && _source[openingQuote] == '#') openingQuote++;
        if (openingQuote >= _source.Length || _source[openingQuote] != '"') return false;
        int line = _line;
        int column = _column;
        int start = _index;
        Advance(prefixLength);
        int hashes = 0;
        while (Peek() == '#') { hashes++; Advance(); }
        Advance();
        bool closed = false;
        while (_index < _source.Length)
        {
            if (Peek() == '"' && ClosingHashes(hashes))
            {
                Advance(1 + hashes);
                closed = true;
                break;
            }
            if (Peek() is '\r' or '\n') ConsumeNewLine();
            else Advance();
        }

        _unterminatedLiteral |= !closed;
        Add(RustTokenKind.String, _source[start.._index], line, column);
        return true;
    }

    private int RawPrefixLength()
    {
        if (Peek() == 'r' && (Peek(1) is '"' or '#')) return 1;
        if (Peek() is 'b' or 'c' && Peek(1) == 'r' && Peek(2) is '"' or '#') return 2;
        return -1;
    }

    private bool ClosingHashes(int hashes)
    {
        for (int offset = 1; offset <= hashes; offset++)
            if (Peek(offset) != '#') return false;
        return true;
    }

    private bool IsQuotedStringStart() =>
        Peek() == '"' || Peek() is 'b' or 'c' && Peek(1) == '"';

    private void ReadQuotedString()
    {
        int line = _line;
        int column = _column;
        int start = _index;
        if (Peek() is 'b' or 'c') Advance();
        Advance();
        bool closed = false;
        while (_index < _source.Length)
        {
            if (Peek() == '\\')
            {
                Advance();
                if (_index < _source.Length)
                {
                    if (Peek() is '\r' or '\n') ConsumeNewLine();
                    else Advance();
                }
            }
            else if (Peek() == '"')
            {
                Advance();
                closed = true;
                break;
            }
            else if (Peek() is '\r' or '\n')
            {
                break;
            }
            else Advance();
        }
        _unterminatedLiteral |= !closed;
        Add(RustTokenKind.String, _source[start.._index], line, column);
    }

    private void ReadApostrophe()
    {
        int line = _line;
        int column = _column;
        int start = _index;
        Advance();
        if (IsIdentifierStart(Peek()))
        {
            while (IsIdentifierPart(Peek())) Advance();
            if (Peek() != '\'')
            {
                Add(RustTokenKind.Lifetime, _source[start.._index], line, column);
                return;
            }
        }
        else if (Peek() == '\\')
        {
            Advance();
            if (_index < _source.Length) Advance();
        }
        else if (_index < _source.Length) Advance();

        bool closed = Peek() == '\'';
        if (closed) Advance();
        _unterminatedLiteral |= !closed;
        Add(RustTokenKind.Character, _source[start.._index], line, column);
    }

    private void ReadIdentifier()
    {
        int start = _index;
        int line = _line;
        int column = _column;
        Advance();
        if (_source[start] == 'r' && Peek() == '#' && IsIdentifierStart(Peek(1))) Advance();
        while (IsIdentifierPart(Peek())) Advance();
        string text = _source[start.._index];
        Add(Keywords.Contains(text) ? RustTokenKind.Keyword : RustTokenKind.Identifier, text, line, column);
    }

    private void ReadNumber()
    {
        int start = _index;
        int line = _line;
        int column = _column;
        while (char.IsAsciiLetterOrDigit(Peek()) || Peek() is '.' or '_') Advance();
        Add(RustTokenKind.Number, _source[start.._index], line, column);
    }

    private void ReadOperator()
    {
        int line = _line;
        int column = _column;
        string text = MultiCharacterOperators.FirstOrDefault(Matches) ?? Peek().ToString();
        Advance(text.Length);
        if (text.Length == 1 && text[0] is '(' or '[' or '{') _delimiters.Push(text[0]);
        else if (text.Length == 1 && text[0] is ')' or ']' or '}')
        {
            char expected = text[0] switch { ')' => '(', ']' => '[', _ => '{' };
            if (_delimiters.Count == 0 || _delimiters.Pop() != expected) _invalidDelimiter = true;
        }
        Add(RustTokenKind.Operator, text, line, column);
    }

    private bool Matches(string candidate) =>
        _index + candidate.Length <= _source.Length &&
        _source.AsSpan(_index, candidate.Length).SequenceEqual(candidate);

    private char Peek(int offset = 0) =>
        _index + offset < _source.Length ? _source[_index + offset] : '\0';

    private void ConsumeNewLine()
    {
        if (Peek() == '\r' && Peek(1) == '\n') _index++;
        _index++;
        _line++;
        _column = 1;
    }

    private void Advance(int count = 1)
    {
        _index += count;
        _column += count;
    }

    private void Add(
        RustTokenKind kind,
        string text,
        int line,
        int column,
        bool allowAtLimit = false)
    {
        if (!allowAtLimit && _tokens.Count >= MaximumTokens)
        {
            _truncated = true;
            return;
        }
        _tokens.Add(new RustToken(kind, text, line, column));
    }

    private static bool IsIdentifierStart(char character) =>
        character == '_' || char.IsLetter(character);

    private static bool IsIdentifierPart(char character) =>
        character == '_' || char.IsLetterOrDigit(character);
}
