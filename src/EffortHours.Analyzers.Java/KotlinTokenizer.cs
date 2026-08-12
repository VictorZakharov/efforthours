namespace EffortHours.Analyzers.Java;

internal sealed class KotlinTokenizer
{
    public const int MaximumTokens = 250_000;

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "as", "break", "by", "catch", "class", "companion", "constructor", "continue",
        "crossinline", "data", "delegate", "do", "dynamic", "else", "enum", "expect",
        "external", "false", "field", "file", "finally", "for", "fun", "get", "if",
        "import", "in", "infix", "init", "inline", "inner", "interface", "internal",
        "is", "lateinit", "noinline", "null", "object", "open", "operator", "out",
        "override", "package", "param", "private", "property", "protected", "public",
        "receiver", "reified", "return", "sealed", "set", "setparam", "suspend", "tailrec",
        "this", "throw", "true", "try", "typealias", "typeof", "val", "value", "var",
        "vararg", "when", "where", "while",
    };

    private static readonly string[] MultiCharacterOperators =
    [
        "===", "!==", ">>>", "..<", "::", "->", "=>", "?.", "?:", "!!", "as?", "!is",
        "!in", "==", "!=", "<=", ">=", "&&", "||", "++", "--", "<<", ">>", "+=", "-=",
        "*=", "/=", "%=", "..",
    ];

    private readonly List<KotlinToken> _tokens = [];
    private readonly Stack<char> _delimiters = [];
    private string _source = string.Empty;
    private int _index;
    private int _line = 1;
    private int _column = 1;
    private bool _truncated;
    private bool _unterminatedLiteral;
    private bool _unterminatedComment;
    private bool _invalidDelimiter;
    private bool _hasShebang;

    public static KotlinTokenization Tokenize(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new KotlinTokenizer().Run(source);
    }

    private KotlinTokenization Run(string source)
    {
        _source = source;
        if (_source.StartsWith("#!", StringComparison.Ordinal))
        {
            _hasShebang = true;
            ReadLineComment();
        }

        while (_index < _source.Length && !_truncated)
        {
            char current = _source[_index];
            if (char.IsWhiteSpace(current)) ReadWhitespace();
            else if (current == '/' && Peek(1) == '/') ReadLineComment();
            else if (current == '/' && Peek(1) == '*') ReadBlockComment();
            else if (current == '"') ReadString();
            else if (current == '\'') ReadCharacter();
            else if (current == '`') ReadBacktickIdentifier();
            else if (IsIdentifierStart(current)) ReadIdentifier();
            else if (char.IsDigit(current)) ReadNumber();
            else ReadOperator();
        }

        Add(KotlinTokenKind.End, string.Empty, _line, _column, allowAtLimit: true);
        return new KotlinTokenization(
            _tokens,
            _truncated,
            _unterminatedLiteral,
            _unterminatedComment,
            _delimiters.Count > 0 || _invalidDelimiter,
            _hasShebang);
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
        int depth = 1;
        while (_index < _source.Length && depth > 0)
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
            }
            else if (_source[_index] is '\r' or '\n') ConsumeNewLine();
            else Advance();
        }

        _unterminatedComment |= depth > 0;
    }

    private void ReadString()
    {
        int line = _line;
        int column = _column;
        int start = _index;
        bool raw = Peek(1) == '"' && Peek(2) == '"';
        Advance(raw ? 3 : 1);
        bool closed = false;
        while (_index < _source.Length)
        {
            if (raw && _source[_index] == '"' && Peek(1) == '"' && Peek(2) == '"')
            {
                Advance(3);
                closed = true;
                break;
            }

            if (!raw && _source[_index] == '\\')
            {
                Advance();
                if (_index < _source.Length) Advance();
            }
            else if (!raw && _source[_index] == '"')
            {
                Advance();
                closed = true;
                break;
            }
            else if (!raw && _source[_index] is '\r' or '\n')
            {
                break;
            }
            else if (_source[_index] is '\r' or '\n') ConsumeNewLine();
            else Advance();
        }

        _unterminatedLiteral |= !closed;
        Add(KotlinTokenKind.String, _source[start.._index], line, column);
    }

    private void ReadCharacter()
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
            else if (_source[_index] is '\r' or '\n') break;
            else Advance();
        }

        _unterminatedLiteral |= !closed;
        Add(KotlinTokenKind.Character, _source[start.._index], line, column);
    }

    private void ReadBacktickIdentifier()
    {
        int line = _line;
        int column = _column;
        Advance();
        int start = _index;
        while (_index < _source.Length && _source[_index] is not ('`' or '\r' or '\n')) Advance();
        string text = _source[start.._index];
        bool closed = _index < _source.Length && _source[_index] == '`';
        if (closed) Advance();
        _unterminatedLiteral |= !closed;
        Add(KotlinTokenKind.Identifier, text, line, column);
    }

    private void ReadIdentifier()
    {
        int start = _index;
        int line = _line;
        int column = _column;
        while (_index < _source.Length && IsIdentifierPart(_source[_index])) Advance();
        string text = _source[start.._index];
        Add(Keywords.Contains(text) ? KotlinTokenKind.Keyword : KotlinTokenKind.Identifier, text, line, column);
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

        Add(KotlinTokenKind.Number, _source[start.._index], line, column);
    }

    private void ReadOperator()
    {
        int line = _line;
        int column = _column;
        string text = MultiCharacterOperators.FirstOrDefault(Matches) ?? _source[_index].ToString();
        Advance(text.Length);
        if (text.Length == 1 && text[0] is '(' or '[' or '{') _delimiters.Push(text[0]);
        if (text.Length == 1 && text[0] is ')' or ']' or '}')
        {
            char expected = text[0] switch { ')' => '(', ']' => '[', _ => '{' };
            if (_delimiters.Count == 0 || _delimiters.Pop() != expected) _invalidDelimiter = true;
        }

        Add(KotlinTokenKind.Operator, text, line, column);
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
        KotlinTokenKind kind,
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

        _tokens.Add(new KotlinToken(kind, text, line, column));
    }

    private static bool IsIdentifierStart(char character) => character == '_' || char.IsLetter(character);

    private static bool IsIdentifierPart(char character) => character == '_' || char.IsLetterOrDigit(character);
}
