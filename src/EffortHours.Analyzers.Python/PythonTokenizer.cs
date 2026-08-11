namespace EffortHours.Analyzers.Python;

internal sealed class PythonTokenizer
{
    public const int MaximumTokens = 250_000;

    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "and", "as", "assert", "async", "await", "break", "case", "class",
        "continue", "def", "del", "elif", "else", "except", "False", "finally",
        "for", "from", "global", "if", "import", "in", "is", "lambda", "match",
        "None", "nonlocal", "not", "or", "pass", "raise", "return", "True", "try",
        "while", "with", "yield",
    };

    private readonly List<PythonToken> _tokens = [];
    private readonly List<int> _indentation = [0];
    private string _source = string.Empty;
    private int _index;
    private int _line = 1;
    private int _column = 1;
    private int _delimiterDepth;
    private bool _atLineStart = true;
    private bool _truncated;
    private bool _unterminatedString;
    private bool _invalidIndentation;
    private bool _invalidDelimiter;

    public static PythonTokenization Tokenize(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new PythonTokenizer().Run(source);
    }

    private PythonTokenization Run(string source)
    {
        _source = source;
        while (_index < _source.Length && !_truncated)
        {
            if (_atLineStart && _delimiterDepth == 0)
            {
                ReadIndentation();
                if (_index >= _source.Length || _truncated)
                {
                    break;
                }
            }

            char current = _source[_index];
            if (current is ' ' or '\t' or '\f')
            {
                Advance();
            }
            else if (current == '#')
            {
                SkipComment();
            }
            else if (current is '\r' or '\n')
            {
                ReadNewLine();
            }
            else if (current == '\\' && IsNewLineAt(_index + 1))
            {
                Advance();
                ConsumeNewLine(emit: false);
                _atLineStart = false;
            }
            else if (TryStringStart(out int quoteIndex))
            {
                ReadString(quoteIndex);
            }
            else if (IsIdentifierStart(current))
            {
                ReadIdentifier();
            }
            else if (char.IsDigit(current))
            {
                ReadNumber();
            }
            else
            {
                ReadOperator();
            }
        }

        while (_indentation.Count > 1 && !_truncated)
        {
            _indentation.RemoveAt(_indentation.Count - 1);
            Add(PythonTokenKind.Dedent, string.Empty, _line, 1);
        }

        Add(PythonTokenKind.End, string.Empty, _line, _column, allowAtLimit: true);
        return new PythonTokenization(
            _tokens,
            _truncated,
            _unterminatedString,
            _invalidIndentation,
            _delimiterDepth != 0 || _invalidDelimiter);
    }

    private void ReadIndentation()
    {
        int start = _index;
        int width = 0;
        while (_index < _source.Length && _source[_index] is ' ' or '\t' or '\f')
        {
            width = _source[_index] == '\t' ? ((width / 8) + 1) * 8 : width + 1;
            Advance();
        }

        if (_index >= _source.Length || _source[_index] == '#' || IsNewLineAt(_index))
        {
            return;
        }

        int current = _indentation[^1];
        if (width > current)
        {
            _indentation.Add(width);
            Add(PythonTokenKind.Indent, width.ToString(System.Globalization.CultureInfo.InvariantCulture), _line, 1);
        }
        else if (width < current)
        {
            while (_indentation.Count > 1 && width < _indentation[^1])
            {
                _indentation.RemoveAt(_indentation.Count - 1);
                Add(PythonTokenKind.Dedent, string.Empty, _line, 1);
            }

            if (width != _indentation[^1])
            {
                _invalidIndentation = true;
            }
        }

        _atLineStart = false;
        _ = start;
    }

    private void SkipComment()
    {
        while (_index < _source.Length && !IsNewLineAt(_index))
        {
            Advance();
        }
    }

    private void ReadNewLine() => ConsumeNewLine(emit: _delimiterDepth == 0);

    private void ConsumeNewLine(bool emit)
    {
        int line = _line;
        int column = _column;
        if (_source[_index] == '\r')
        {
            Advance();
            if (_index < _source.Length && _source[_index] == '\n')
            {
                Advance();
            }
        }
        else
        {
            Advance();
        }

        _line++;
        _column = 1;
        _atLineStart = true;
        if (emit)
        {
            Add(PythonTokenKind.NewLine, string.Empty, line, column);
        }
    }

    private bool TryStringStart(out int quoteIndex)
    {
        quoteIndex = _index;
        if (_source[_index] is '\'' or '"')
        {
            return true;
        }

        int probe = _index;
        while (probe < _source.Length && probe - _index < 3 &&
            _source[probe] is 'r' or 'R' or 'b' or 'B' or 'u' or 'U' or 'f' or 'F')
        {
            probe++;
        }

        if (probe > _index && probe < _source.Length && _source[probe] is '\'' or '"')
        {
            quoteIndex = probe;
            return true;
        }

        return false;
    }

    private void ReadString(int quoteIndex)
    {
        int line = _line;
        int column = _column;
        int start = _index;
        while (_index < quoteIndex)
        {
            Advance();
        }

        char quote = _source[_index];
        bool triple = _index + 2 < _source.Length &&
            _source[_index + 1] == quote && _source[_index + 2] == quote;
        int delimiterLength = triple ? 3 : 1;
        for (int count = 0; count < delimiterLength; count++) Advance();

        bool closed = false;
        while (_index < _source.Length)
        {
            if (_source[_index] == '\\')
            {
                Advance();
                if (_index < _source.Length)
                {
                    if (IsNewLineAt(_index)) ConsumeNewLine(emit: false);
                    else Advance();
                }
                continue;
            }

            if (_source[_index] == quote &&
                (!triple || _index + 2 < _source.Length &&
                    _source[_index + 1] == quote && _source[_index + 2] == quote))
            {
                for (int count = 0; count < delimiterLength; count++) Advance();
                closed = true;
                break;
            }

            if (!triple && IsNewLineAt(_index))
            {
                break;
            }

            if (IsNewLineAt(_index)) ConsumeNewLine(emit: false);
            else Advance();
        }

        _unterminatedString |= !closed;
        _atLineStart = false;
        Add(PythonTokenKind.String, _source[start.._index], line, column);
    }

    private void ReadIdentifier()
    {
        int start = _index;
        int line = _line;
        int column = _column;
        while (_index < _source.Length && IsIdentifierPart(_source[_index])) Advance();
        string text = _source[start.._index];
        Add(Keywords.Contains(text) ? PythonTokenKind.Keyword : PythonTokenKind.Identifier, text, line, column);
    }

    private void ReadNumber()
    {
        int start = _index;
        int line = _line;
        int column = _column;
        while (_index < _source.Length &&
            (char.IsAsciiLetterOrDigit(_source[_index]) || _source[_index] is '.' or '_'))
        {
            Advance();
        }

        Add(PythonTokenKind.Number, _source[start.._index], line, column);
    }

    private void ReadOperator()
    {
        int line = _line;
        int column = _column;
        string text = _source[_index].ToString();
        if (_index + 1 < _source.Length)
        {
            string pair = _source.Substring(_index, 2);
            if (pair is "->" or ":=" or "==" or "!=" or "<=" or ">=" or
                "**" or "//" or "<<" or ">>" or "+=" or "-=" or "*=" or "/=" or "@=")
            {
                text = pair;
            }
        }

        for (int count = 0; count < text.Length; count++) Advance();
        if (text is "(" or "[" or "{") _delimiterDepth++;
        if (text is ")" or "]" or "}")
        {
            if (_delimiterDepth == 0) _invalidDelimiter = true;
            else _delimiterDepth--;
        }
        Add(PythonTokenKind.Operator, text, line, column);
    }

    private void Advance()
    {
        _index++;
        _column++;
    }

    private void Add(
        PythonTokenKind kind,
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

        _tokens.Add(new PythonToken(kind, text, line, column));
    }

    private bool IsNewLineAt(int index) => index < _source.Length && _source[index] is '\r' or '\n';

    private static bool IsIdentifierStart(char character) => character == '_' || char.IsLetter(character);

    private static bool IsIdentifierPart(char character) => character == '_' || char.IsLetterOrDigit(character);
}
