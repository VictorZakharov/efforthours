namespace EffortHours.Analyzers.Terraform;

internal static class HclSyntaxAnalyzer
{
    private const int MaximumDepth = 128;
    private const int MaximumCapturedValues = 32;

    public static HclDocumentAnalysis Analyze(string text)
    {
        HclTokenizationResult tokenization = HclTokenizer.Tokenize(text);
        Parser parser = new(tokenization.Tokens);
        HclBody body = parser.ParseBody(expectClosingBrace: false, depth: 0);
        int firstLine = body.Blocks.Select(block => block.Line)
            .Concat(body.Attributes.Select(attribute => attribute.Line))
            .DefaultIfEmpty(1)
            .Min();
        return new HclDocumentAnalysis
        {
            Blocks = body.Blocks,
            Attributes = body.Attributes,
            Truncated = tokenization.Truncated || parser.DepthExceeded,
            StructurallyBalanced = parser.StructurallyBalanced,
            UnterminatedConstruct = tokenization.UnterminatedConstruct,
            UnknownConstructs = parser.UnknownConstructs,
            CommentCount = tokenization.CommentCount,
            HeredocCount = tokenization.HeredocCount,
            FirstSemanticLine = firstLine,
        };
    }

    private sealed class Parser(IReadOnlyList<HclToken> tokens)
    {
        private readonly IReadOnlyList<HclToken> _tokens = tokens;
        private int _index;

        public int UnknownConstructs { get; private set; }

        public bool StructurallyBalanced { get; private set; } = true;

        public bool DepthExceeded { get; private set; }

        public HclBody ParseBody(bool expectClosingBrace, int depth)
        {
            if (depth > MaximumDepth)
            {
                DepthExceeded = true;
                StructurallyBalanced = false;
                return new HclBody([], []);
            }

            List<HclBlockAnalysis> blocks = [];
            List<HclAttributeAnalysis> attributes = [];
            while (_index < _tokens.Count)
            {
                SkipNewLines();
                if (_index >= _tokens.Count) break;
                if (IsSymbol(_tokens[_index], "}"))
                {
                    if (expectClosingBrace)
                    {
                        _index++;
                        return new HclBody(blocks, attributes);
                    }

                    StructurallyBalanced = false;
                    UnknownConstructs++;
                    _index++;
                    continue;
                }

                if (_tokens[_index].Kind != HclTokenKind.Identifier)
                {
                    UnknownConstructs++;
                    SkipUnknown();
                    continue;
                }

                int headerStart = _index;
                int delimiter = FindHeaderDelimiter(headerStart);
                if (delimiter < 0)
                {
                    UnknownConstructs++;
                    SkipUnknown();
                    continue;
                }

                if (IsSymbol(_tokens[delimiter], "="))
                {
                    attributes.Add(ParseAttribute(headerStart, delimiter));
                    continue;
                }

                blocks.Add(ParseBlock(headerStart, delimiter, depth));
            }

            if (expectClosingBrace) StructurallyBalanced = false;
            return new HclBody(blocks, attributes);
        }

        private HclBlockAnalysis ParseBlock(int headerStart, int openingBrace, int depth)
        {
            HclToken type = _tokens[headerStart];
            List<string> labels = [];
            for (int index = headerStart + 1; index < openingBrace; index++)
            {
                HclToken token = _tokens[index];
                if (token.Kind is HclTokenKind.String or HclTokenKind.Identifier)
                {
                    if (labels.Count < 4) labels.Add(token.Value);
                }
                else if (token.Kind != HclTokenKind.NewLine)
                {
                    UnknownConstructs++;
                }
            }

            _index = openingBrace + 1;
            HclBody body = ParseBody(expectClosingBrace: true, depth + 1);
            return new HclBlockAnalysis
            {
                Type = type.Value,
                Labels = labels,
                Line = type.Line,
                Attributes = body.Attributes,
                Blocks = body.Blocks,
            };
        }

        private HclAttributeAnalysis ParseAttribute(int headerStart, int equals)
        {
            HclToken name = _tokens[headerStart];
            if (equals != headerStart + 1) UnknownConstructs++;
            int expressionStart = equals + 1;
            int end = FindExpressionEnd(expressionStart);
            HclAttributeAnalysis analysis = AnalyzeExpression(name, expressionStart, end);
            _index = end;
            if (_index < _tokens.Count && _tokens[_index].Kind == HclTokenKind.NewLine) _index++;
            return analysis;
        }

        private HclAttributeAnalysis AnalyzeExpression(HclToken name, int start, int end)
        {
            List<string> strings = [];
            List<string> identifiers = [];
            int traversals = 0;
            int functions = 0;
            int conditionals = 0;
            int forExpressions = 0;
            int templates = 0;
            for (int index = start; index < end; index++)
            {
                HclToken token = _tokens[index];
                if (token.Kind == HclTokenKind.String)
                {
                    if (strings.Count < MaximumCapturedValues) strings.Add(token.Value);
                    if (token.HasTemplate) templates++;
                }
                else if (token.Kind == HclTokenKind.Identifier)
                {
                    if (identifiers.Count < MaximumCapturedValues) identifiers.Add(token.Value);
                    if (token.Value == "for") forExpressions++;
                    if (NextNonNewLine(index + 1, end) is int next && IsSymbol(_tokens[next], "("))
                    {
                        functions++;
                    }
                }
                else if (IsSymbol(token, ".")) traversals++;
                else if (IsSymbol(token, "?")) conditionals++;
            }

            HclToken[] significant = [.. _tokens.Skip(start).Take(end - start)
                .Where(token => token.Kind != HclTokenKind.NewLine)];
            string? literalString = significant.Length == 1 && significant[0].Kind == HclTokenKind.String &&
                !significant[0].HasTemplate
                    ? significant[0].Value
                    : null;
            bool? literalBoolean = significant.Length == 1 &&
                significant[0].Kind == HclTokenKind.Identifier &&
                significant[0].Value is "true" or "false"
                    ? significant[0].Value == "true"
                    : null;
            return new HclAttributeAnalysis
            {
                Name = name.Value,
                Line = name.Line,
                LiteralString = literalString,
                LiteralBoolean = literalBoolean,
                StringLiterals = strings,
                Identifiers = identifiers,
                Tokens = significant.Length,
                Traversals = traversals,
                FunctionCalls = functions,
                Conditionals = conditionals,
                ForExpressions = forExpressions,
                TemplateExpressions = templates,
            };
        }

        private int FindHeaderDelimiter(int start)
        {
            for (int index = start + 1; index < _tokens.Count; index++)
            {
                HclToken token = _tokens[index];
                if (token.Kind == HclTokenKind.NewLine) return -1;
                if (IsSymbol(token, "=") || IsSymbol(token, "{")) return index;
                if (IsSymbol(token, "}")) return -1;
            }

            return -1;
        }

        private int FindExpressionEnd(int start)
        {
            int braces = 0;
            int brackets = 0;
            int parentheses = 0;
            for (int index = start; index < _tokens.Count; index++)
            {
                HclToken token = _tokens[index];
                if (token.Kind == HclTokenKind.NewLine && braces == 0 && brackets == 0 && parentheses == 0)
                    return index;
                if (token.Kind != HclTokenKind.Symbol) continue;
                switch (token.Value)
                {
                    case "{": braces++; break;
                    case "}":
                        if (braces == 0 && brackets == 0 && parentheses == 0) return index;
                        braces--;
                        if (braces < 0) StructurallyBalanced = false;
                        break;
                    case "[": brackets++; break;
                    case "]": brackets--; if (brackets < 0) StructurallyBalanced = false; break;
                    case "(": parentheses++; break;
                    case ")": parentheses--; if (parentheses < 0) StructurallyBalanced = false; break;
                }
            }

            if (braces != 0 || brackets != 0 || parentheses != 0) StructurallyBalanced = false;
            return _tokens.Count;
        }

        private int? NextNonNewLine(int start, int end)
        {
            for (int index = start; index < end; index++)
            {
                if (_tokens[index].Kind != HclTokenKind.NewLine) return index;
            }

            return null;
        }

        private void SkipNewLines()
        {
            while (_index < _tokens.Count && _tokens[_index].Kind == HclTokenKind.NewLine) _index++;
        }

        private void SkipUnknown()
        {
            while (_index < _tokens.Count)
            {
                HclToken token = _tokens[_index];
                if (token.Kind == HclTokenKind.NewLine || IsSymbol(token, "}")) break;
                _index++;
            }

            if (_index < _tokens.Count && _tokens[_index].Kind == HclTokenKind.NewLine) _index++;
        }

        private static bool IsSymbol(HclToken token, string value) =>
            token.Kind == HclTokenKind.Symbol && token.Value == value;
    }

    private sealed record HclBody(
        IReadOnlyList<HclBlockAnalysis> Blocks,
        IReadOnlyList<HclAttributeAnalysis> Attributes);
}
