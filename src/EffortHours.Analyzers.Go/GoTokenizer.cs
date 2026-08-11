namespace EffortHours.Analyzers.Go;

internal sealed class GoTokenizer
{
    public const int MaximumTokens = 250_000;

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

    private readonly List<GoToken> _tokens = [];
    private readonly List<GoDirective> _directives = [];
    private string _source = string.Empty;
    private int _index;
    private int _line = 1;
    private int _column = 1;
    private int _delimiterDepth;
    private bool _truncated;
    private bool _unterminatedLiteral;
    private bool _unterminatedComment;
    private bool _invalidDelimiter;

    public static GoTokenization Tokenize(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new GoTokenizer().Run(source);
    }

    private GoTokenization Run(string source)
    {
        _source = source;
        while (_index < _source.Length && !_truncated)
        {
            char current = _source[_index];
            if (current is ' ' or '\t' or '\f')
            {
                Advance();
            }
            else if (current is '\r' or '\n')
            {
                ReadNewLine();
            }
            else if (current == '/' && Peek(1) == '/')
            {
                ReadLineComment();
            }
            else if (current == '/' && Peek(1) == '*')
            {
                ReadBlockComment();
            }
            else if (current == '"' || current == '`')
            {
                ReadString(current);
            }
            else if (current == '\'')
            {
                ReadRune();
            }
            else if (IsIdentifierStart(current))
            {
                ReadIdentifier();
            }
            else if (char.IsDigit(current) || current == '.' && char.IsDigit(Peek(1)))
            {
                ReadNumber();
            }
            else
            {
                ReadOperator();
            }
        }

        Add(GoTokenKind.End, string.Empty, _line, _column, allowAtLimit: true);
        return new GoTokenization(
            _tokens,
            _directives,
            _truncated,
            _unterminatedLiteral,
            _unterminatedComment,
            _delimiterDepth != 0 || _invalidDelimiter);
    }

    private void ReadNewLine()
    {
        int line = _line;
        int column = _column;
        ConsumeNewLine();
        Add(GoTokenKind.NewLine, string.Empty, line, column);
    }

    private void ReadLineComment()
    {
        int line = _line;
        int start = _index;
        while (_index < _source.Length && _source[_index] is not ('\r' or '\n')) Advance();
        string comment = _source[start.._index].Trim();
        if (IsCompilerDirective(comment)) _directives.Add(new GoDirective(comment, line));
    }

    private void ReadBlockComment()
    {
        int startLine = _line;
        int start = _index;
        Advance(2);
        bool closed = false;
        while (_index < _source.Length)
        {
            if (_source[_index] == '*' && Peek(1) == '/')
            {
                Advance(2);
                closed = true;
                break;
            }

            if (_source[_index] is '\r' or '\n')
            {
                int line = _line;
                int column = _column;
                ConsumeNewLine();
                Add(GoTokenKind.NewLine, string.Empty, line, column);
            }
            else
            {
                Advance();
            }
        }

        _unterminatedComment |= !closed;
        string comment = _source[start.._index].Trim();
        if (comment.Contains("#cgo", StringComparison.Ordinal))
            _directives.Add(new GoDirective("cgo-preamble", startLine));
    }

    private void ReadString(char quote)
    {
        int line = _line;
        int column = _column;
        int start = _index;
        Advance();
        bool closed = false;
        while (_index < _source.Length)
        {
            char current = _source[_index];
            if (quote == '"' && current == '\\')
            {
                Advance();
                if (_index < _source.Length)
                {
                    if (_source[_index] is '\r' or '\n') break;
                    Advance();
                }
            }
            else if (current == quote)
            {
                Advance();
                closed = true;
                break;
            }
            else if (quote == '"' && current is '\r' or '\n')
            {
                break;
            }
            else if (current is '\r' or '\n')
            {
                ConsumeNewLine();
            }
            else
            {
                Advance();
            }
        }

        _unterminatedLiteral |= !closed;
        Add(GoTokenKind.String, _source[start.._index], line, column);
    }

    private void ReadRune()
    {
        int line = _line;
        int column = _column;
        int start = _index;
        Advance();
        bool closed = false;
        while (_index < _source.Length)
        {
            if (_source[_index] == '\\')
            {
                Advance();
                if (_index < _source.Length) Advance();
            }
            else if (_source[_index] == '\'')
            {
                Advance();
                closed = true;
                break;
            }
            else if (_source[_index] is '\r' or '\n')
            {
                break;
            }
            else
            {
                Advance();
            }
        }

        _unterminatedLiteral |= !closed;
        Add(GoTokenKind.Rune, _source[start.._index], line, column);
    }

    private void ReadIdentifier()
    {
        int start = _index;
        int line = _line;
        int column = _column;
        while (_index < _source.Length && IsIdentifierPart(_source[_index])) Advance();
        string text = _source[start.._index];
        Add(Keywords.Contains(text) ? GoTokenKind.Keyword : GoTokenKind.Identifier, text, line, column);
    }

    private void ReadNumber()
    {
        int start = _index;
        int line = _line;
        int column = _column;
        while (_index < _source.Length &&
            (char.IsAsciiLetterOrDigit(_source[_index]) || _source[_index] is '.' or '_')) Advance();
        Add(GoTokenKind.Number, _source[start.._index], line, column);
    }

    private void ReadOperator()
    {
        int line = _line;
        int column = _column;
        string text = MultiCharacterOperators.FirstOrDefault(candidate => Matches(candidate)) ??
            _source[_index].ToString();
        Advance(text.Length);
        if (text is "(" or "[" or "{") _delimiterDepth++;
        if (text is ")" or "]" or "}")
        {
            if (_delimiterDepth == 0) _invalidDelimiter = true;
            else _delimiterDepth--;
        }

        Add(GoTokenKind.Operator, text, line, column);
    }

    private bool Matches(string candidate) =>
        _index + candidate.Length <= _source.Length &&
        _source.AsSpan(_index, candidate.Length).SequenceEqual(candidate);

    private char Peek(int offset) =>
        _index + offset < _source.Length ? _source[_index + offset] : '\0';

    private void ConsumeNewLine()
    {
        if (_source[_index] == '\r' && Peek(1) == '\n') _index++;
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
        GoTokenKind kind,
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

        _tokens.Add(new GoToken(kind, text, line, column));
    }

    private static bool IsCompilerDirective(string comment) =>
        comment.StartsWith("//go:", StringComparison.Ordinal) ||
        comment.StartsWith("// +build", StringComparison.Ordinal);

    private static bool IsIdentifierStart(char character) =>
        character == '_' || char.IsLetter(character);

    private static bool IsIdentifierPart(char character) =>
        character == '_' || char.IsLetterOrDigit(character);
}
