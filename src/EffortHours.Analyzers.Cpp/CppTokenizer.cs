namespace EffortHours.Analyzers.Cpp;

internal sealed class CppTokenizer
{
    public const int MaximumTokens = 250_000;

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "alignas", "alignof", "and", "asm", "atomic_cancel", "atomic_commit", "atomic_noexcept",
        "auto", "bitand", "bitor", "bool", "break", "case", "catch", "char", "char8_t",
        "char16_t", "char32_t", "class", "compl", "concept", "const", "consteval", "constexpr",
        "constinit", "const_cast", "continue", "co_await", "co_return", "co_yield", "decltype",
        "default", "delete", "do", "double", "dynamic_cast", "else", "enum", "explicit", "export",
        "extern", "false", "float", "for", "friend", "goto", "if", "inline", "int", "long",
        "module", "mutable", "namespace", "new", "noexcept", "not", "not_eq", "nullptr", "operator",
        "or", "or_eq", "private", "protected", "public", "register", "reinterpret_cast", "requires",
        "return", "short", "signed", "sizeof", "static", "static_assert", "static_cast", "struct",
        "switch", "synchronized", "template", "this", "thread_local", "throw", "true", "try", "typedef",
        "typeid", "typename", "union", "unsigned", "using", "virtual", "void", "volatile", "wchar_t",
        "while", "xor", "xor_eq", "_Alignas", "_Alignof", "_Atomic", "_Bool", "_Complex", "_Generic",
        "_Imaginary", "_Noreturn", "_Static_assert", "_Thread_local", "__attribute__", "__declspec",
        "__asm", "__asm__", "__extension__", "__forceinline", "__inline", "__inline__",
    };

    private static readonly string[] MultiCharacterOperators =
    [
        "<=>", ">>=", "<<=", "->*", "...", "::", "->", ".*", "++", "--", "<<", ">>",
        "<=", ">=", "==", "!=", "&&", "||", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=",
        "##", "<:", ":>", "<%", "%>", "%:",
    ];

    private readonly List<CppToken> _tokens = [];
    private readonly Stack<char> _delimiters = [];
    private CancellationToken _cancellationToken;
    private string _source = string.Empty;
    private int _index;
    private int _line = 1;
    private int _column = 1;
    private bool _lineStart = true;
    private bool _truncated;
    private bool _unterminatedLiteral;
    private bool _unterminatedComment;
    private bool _invalidDelimiter;
    private bool _invalidLineSplice;

    public static CppTokenization Tokenize(
        string source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new CppTokenizer { _cancellationToken = cancellationToken }.Run(source);
    }

    private CppTokenization Run(string source)
    {
        _source = source;
        while (_index < _source.Length && !_truncated)
        {
            if ((_index & 4095) == 0) _cancellationToken.ThrowIfCancellationRequested();
            char current = Peek();
            if (current is ' ' or '\t' or '\v' or '\f') Advance();
            else if (current is '\r' or '\n') ConsumeNewLine();
            else if (current == '\\' && Peek(1) is '\r' or '\n') ConsumeLineSplice();
            else if (_lineStart && current == '#') ReadPreprocessor();
            else if (current == '/' && Peek(1) == '/') ReadLineComment();
            else if (current == '/' && Peek(1) == '*') ReadBlockComment();
            else if (TryReadRawString()) { }
            else if (IsQuotedStringStart()) ReadQuotedString();
            else if (IsIdentifierStart(current)) ReadIdentifier();
            else if (char.IsDigit(current) || current == '.' && char.IsDigit(Peek(1))) ReadNumber();
            else ReadOperator();
        }

        Add(CppTokenKind.End, string.Empty, _line, _column, allowAtLimit: true);
        return new CppTokenization(
            _tokens,
            _truncated,
            _unterminatedLiteral,
            _unterminatedComment,
            _delimiters.Count > 0 || _invalidDelimiter,
            _invalidLineSplice);
    }

    private void ReadPreprocessor()
    {
        int line = _line;
        int column = _column;
        int start = _index;
        bool continued;
        do
        {
            continued = false;
            while (_index < _source.Length && Peek() is not ('\r' or '\n')) Advance();
            int cursor = _index - 1;
            while (cursor >= start && _source[cursor] is ' ' or '\t') cursor--;
            if (cursor >= start && _source[cursor] == '\\')
            {
                continued = true;
                if (_index < _source.Length) ConsumeNewLine();
            }
        }
        while (continued && _index < _source.Length);
        Add(CppTokenKind.Preprocessor, _source[start.._index], line, column);
    }

    private void ReadLineComment()
    {
        int line = _line;
        int column = _column;
        int start = _index;
        bool documentation = Peek(2) is '/' or '!';
        while (_index < _source.Length && Peek() is not ('\r' or '\n')) Advance();
        if (documentation) Add(CppTokenKind.Documentation, _source[start.._index], line, column);
    }

    private void ReadBlockComment()
    {
        int line = _line;
        int column = _column;
        int start = _index;
        bool documentation = Peek(2) is '*' or '!';
        Advance(2);
        bool closed = false;
        while (_index < _source.Length)
        {
            if (Peek() == '*' && Peek(1) == '/')
            {
                Advance(2);
                closed = true;
                break;
            }
            if (Peek() is '\r' or '\n') ConsumeNewLine();
            else Advance();
        }
        _unterminatedComment |= !closed;
        if (documentation) Add(CppTokenKind.Documentation, _source[start.._index], line, column);
    }

    private bool TryReadRawString()
    {
        int prefix = RawStringPrefixLength();
        if (prefix < 0) return false;
        int line = _line;
        int column = _column;
        int start = _index;
        Advance(prefix);
        int delimiterStart = _index;
        while (_index < _source.Length && Peek() != '(' &&
            Peek() is not ('\r' or '\n') && _index - delimiterStart <= 16) Advance();
        if (Peek() != '(' || _index - delimiterStart > 16 ||
            !ValidRawDelimiter(_source[delimiterStart.._index]))
        {
            _index = start;
            _line = line;
            _column = column;
            _unterminatedLiteral = true;
            return false;
        }
        string delimiter = _source[delimiterStart.._index];
        Advance();
        string closing = ")" + delimiter + '"';
        bool closed = false;
        while (_index < _source.Length)
        {
            if (Matches(closing))
            {
                Advance(closing.Length);
                closed = true;
                break;
            }
            if (Peek() is '\r' or '\n') ConsumeNewLine();
            else Advance();
        }
        _unterminatedLiteral |= !closed;
        Add(CppTokenKind.String, _source[start.._index], line, column);
        return true;
    }

    private int RawStringPrefixLength()
    {
        if (Peek() == 'R' && Peek(1) == '"') return 2;
        if (Peek() == 'u' && Peek(1) == '8' && Peek(2) == 'R' && Peek(3) == '"') return 4;
        if (Peek() is 'u' or 'U' or 'L' && Peek(1) == 'R' && Peek(2) == '"') return 3;
        return -1;
    }

    private static bool ValidRawDelimiter(string delimiter) => delimiter.All(character =>
        character is >= (char)0x21 and <= (char)0x7e && character is not ('(' or ')' or '\\'));

    private bool IsQuotedStringStart()
    {
        if (Peek() is '"' or '\'') return true;
        if (Peek() == 'u' && Peek(1) == '8' && Peek(2) is '"' or '\'') return true;
        return Peek() is 'u' or 'U' or 'L' && Peek(1) is '"' or '\'';
    }

    private void ReadQuotedString()
    {
        int line = _line;
        int column = _column;
        int start = _index;
        if (Peek() == 'u' && Peek(1) == '8') Advance(2);
        else if (Peek() is 'u' or 'U' or 'L') Advance();
        char quote = Peek();
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
            else if (Peek() == quote)
            {
                Advance();
                closed = true;
                break;
            }
            else if (Peek() is '\r' or '\n') break;
            else Advance();
        }
        _unterminatedLiteral |= !closed;
        Add(quote == '\'' ? CppTokenKind.Character : CppTokenKind.String,
            _source[start.._index], line, column);
    }

    private void ReadIdentifier()
    {
        int start = _index;
        int line = _line;
        int column = _column;
        Advance();
        while (IsIdentifierPart(Peek())) Advance();
        string text = _source[start.._index];
        Add(Keywords.Contains(text) ? CppTokenKind.Keyword : CppTokenKind.Identifier, text, line, column);
    }

    private void ReadNumber()
    {
        int start = _index;
        int line = _line;
        int column = _column;
        Advance();
        while (char.IsAsciiLetterOrDigit(Peek()) || Peek() is '.' or '_' or '\'' ||
            Peek() is '+' or '-' && _index > start && _source[_index - 1] is 'e' or 'E' or 'p' or 'P')
            Advance();
        Add(CppTokenKind.Number, _source[start.._index], line, column);
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
        Add(CppTokenKind.Operator, text, line, column);
    }

    private void ConsumeLineSplice()
    {
        Advance();
        if (Peek() is not ('\r' or '\n'))
        {
            _invalidLineSplice = true;
            return;
        }
        ConsumeNewLine();
    }

    private void ConsumeNewLine()
    {
        if (Peek() == '\r' && Peek(1) == '\n') _index++;
        _index++;
        _line++;
        _column = 1;
        _lineStart = true;
    }

    private void Advance(int count = 1)
    {
        for (int offset = 0; offset < count; offset++)
        {
            if (_index >= _source.Length) return;
            if (!char.IsWhiteSpace(_source[_index])) _lineStart = false;
            _index++;
            _column++;
        }
    }

    private void Add(CppTokenKind kind, string text, int line, int column, bool allowAtLimit = false)
    {
        if (!allowAtLimit && _tokens.Count >= MaximumTokens)
        {
            _truncated = true;
            return;
        }
        _tokens.Add(new CppToken(kind, text, line, column));
    }

    private bool Matches(string candidate) =>
        _index + candidate.Length <= _source.Length &&
        _source.AsSpan(_index, candidate.Length).SequenceEqual(candidate);

    private char Peek(int offset = 0) =>
        _index + offset < _source.Length ? _source[_index + offset] : '\0';

    private static bool IsIdentifierStart(char character) =>
        character == '_' || char.IsLetter(character);

    private static bool IsIdentifierPart(char character) =>
        character == '_' || char.IsLetterOrDigit(character);
}
