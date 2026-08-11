namespace EffortHours.Analyzers.Java;

internal sealed class JavaTokenizer
{
    public const int MaximumTokens = 250_000;

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "assert", "boolean", "break", "byte", "case", "catch", "char",
        "class", "const", "continue", "default", "do", "double", "else", "enum",
        "exports", "extends", "final", "finally", "float", "for", "goto", "if",
        "implements", "import", "instanceof", "int", "interface", "long", "module",
        "native", "new", "non-sealed", "open", "opens", "package", "permits", "private",
        "protected", "provides", "public", "record", "requires", "return", "sealed",
        "short", "static", "strictfp", "super", "switch", "synchronized", "this",
        "throw", "throws", "to", "transient", "transitive", "try", "uses", "var",
        "void", "volatile", "while", "with", "yield",
    };

    private static readonly string[] MultiCharacterOperators =
    [
        ">>>=", "<<=", ">>=", "...", "::", "->", "==", "!=", "<=", ">=", "++", "--",
        "&&", "||", "<<", ">>>", ">>", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=",
    ];

    private readonly List<JavaToken> _tokens = [];
    private readonly Stack<char> _delimiters = [];
    private string _source = string.Empty;
    private int _index;
    private int _line = 1;
    private int _column = 1;
    private bool _truncated;
    private bool _unterminatedLiteral;
    private bool _unterminatedComment;
    private bool _invalidDelimiter;

    public static JavaTokenization Tokenize(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new JavaTokenizer().Run(source);
    }

    private JavaTokenization Run(string source)
    {
        _source = source;
        while (_index < _source.Length && !_truncated)
        {
            char current = _source[_index];
            if (char.IsWhiteSpace(current)) ReadWhitespace();
            else if (current == '/' && Peek(1) == '/') ReadLineComment();
            else if (current == '/' && Peek(1) == '*') ReadBlockComment();
            else if (current == '"') ReadString();
            else if (current == '\'') ReadCharacter();
            else if (IsIdentifierStart(current)) ReadIdentifier();
            else if (char.IsDigit(current) || current == '.' && char.IsDigit(Peek(1))) ReadNumber();
            else ReadOperator();
        }

        Add(JavaTokenKind.End, string.Empty, _line, _column, allowAtLimit: true);
        return new JavaTokenization(
            _tokens,
            _truncated,
            _unterminatedLiteral,
            _unterminatedComment,
            _delimiters.Count > 0 || _invalidDelimiter,
            source.Contains("\\u", StringComparison.Ordinal));
    }

    private void ReadWhitespace()
    {
        while (_index < _source.Length && char.IsWhiteSpace(_source[_index]))
        {
            if (_source[_index] is '\r' or '\n') ConsumeNewLine();
            else Advance();
        }
    }

    private void ReadLineComment()
    {
        while (_index < _source.Length && _source[_index] is not ('\r' or '\n')) Advance();
    }

    private void ReadBlockComment()
    {
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

            if (_source[_index] is '\r' or '\n') ConsumeNewLine();
            else Advance();
        }

        _unterminatedComment |= !closed;
    }

    private void ReadString()
    {
        int line = _line;
        int column = _column;
        int start = _index;
        bool textBlock = Peek(1) == '"' && Peek(2) == '"';
        Advance(textBlock ? 3 : 1);
        bool closed = false;
        while (_index < _source.Length)
        {
            if (_source[_index] == '\\')
            {
                Advance();
                if (_index < _source.Length)
                {
                    if (_source[_index] is '\r' or '\n') ConsumeNewLine();
                    else Advance();
                }
            }
            else if (textBlock && _source[_index] == '"' && Peek(1) == '"' && Peek(2) == '"')
            {
                Advance(3);
                closed = true;
                break;
            }
            else if (!textBlock && _source[_index] == '"')
            {
                Advance();
                closed = true;
                break;
            }
            else if (!textBlock && _source[_index] is '\r' or '\n')
            {
                break;
            }
            else if (_source[_index] is '\r' or '\n') ConsumeNewLine();
            else Advance();
        }

        _unterminatedLiteral |= !closed;
        Add(JavaTokenKind.String, _source[start.._index], line, column);
    }

    private void ReadCharacter()
    {
        int line = _line;
        int column = _column;
        int start = _index++;
        _column++;
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
            else if (_source[_index] is '\r' or '\n') break;
            else Advance();
        }

        _unterminatedLiteral |= !closed;
        Add(JavaTokenKind.Character, _source[start.._index], line, column);
    }

    private void ReadIdentifier()
    {
        int start = _index;
        int line = _line;
        int column = _column;
        while (_index < _source.Length && IsIdentifierPart(_source[_index])) Advance();
        string text = _source[start.._index];
        Add(Keywords.Contains(text) ? JavaTokenKind.Keyword : JavaTokenKind.Identifier, text, line, column);
    }

    private void ReadNumber()
    {
        int start = _index;
        int line = _line;
        int column = _column;
        while (_index < _source.Length &&
            (char.IsAsciiLetterOrDigit(_source[_index]) || _source[_index] is '.' or '_' or '+' or '-'))
        {
            if (_source[_index] is '+' or '-' && _index > start &&
                _source[_index - 1] is not ('e' or 'E' or 'p' or 'P')) break;
            Advance();
        }

        Add(JavaTokenKind.Number, _source[start.._index], line, column);
    }

    private void ReadOperator()
    {
        int line = _line;
        int column = _column;
        string text = MultiCharacterOperators.FirstOrDefault(candidate => Matches(candidate)) ??
            _source[_index].ToString();
        Advance(text.Length);
        if (text.Length == 1 && text[0] is '(' or '[' or '{') _delimiters.Push(text[0]);
        if (text.Length == 1 && text[0] is ')' or ']' or '}')
        {
            char expected = text[0] switch { ')' => '(', ']' => '[', _ => '{' };
            if (_delimiters.Count == 0 || _delimiters.Pop() != expected) _invalidDelimiter = true;
        }

        Add(JavaTokenKind.Operator, text, line, column);
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
        JavaTokenKind kind,
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

        _tokens.Add(new JavaToken(kind, text, line, column));
    }

    private static bool IsIdentifierStart(char character) =>
        character is '_' or '$' || char.IsLetter(character);

    private static bool IsIdentifierPart(char character) =>
        character is '_' or '$' || char.IsLetterOrDigit(character);
}
