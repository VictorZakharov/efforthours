namespace EffortHours.Analyzers.Sql;

internal static class SqlSemanticAnalyzer
{
    private const int MaximumMeasurement = 10_000;

    private static readonly HashSet<string> KnownStatementStarters = new(StringComparer.Ordinal)
    {
        "ALTER", "ANALYZE", "ATTACH", "BEGIN", "CALL", "COMMIT", "CREATE",
        "DECLARE", "DELETE", "DELIMITER", "DETACH", "DO", "DROP", "END",
        "EXEC", "EXECUTE", "EXPLAIN", "GRANT", "IF", "INSERT", "MERGE",
        "PRAGMA", "RELEASE", "REPLACE", "REVOKE", "ROLLBACK", "SAVEPOINT",
        "SELECT", "SET", "START", "TRUNCATE", "UPDATE", "USE", "VACUUM", "WITH",
    };

    public static SqlSemanticAnalysis Analyze(string text)
    {
        SqlTokenizationResult tokenization = SqlTokenizer.Tokenize(text);
        IReadOnlyList<SqlToken> tokens = tokenization.Tokens;
        SqlSemanticMetrics metrics = new();
        int firstSemanticLine = tokens.Count == 0 ? 1 : tokens[0].Line;

        for (int index = 0; index < tokens.Count; index++)
        {
            SqlToken token = tokens[index];
            if (token.Kind != SqlTokenKind.Word)
            {
                continue;
            }

            string word = token.Value;
            switch (word)
            {
                case "CREATE":
                case "ALTER":
                case "DROP":
                case "TRUNCATE":
                    CountDdl(tokens, index, metrics);
                    firstSemanticLine = Math.Min(firstSemanticLine, token.Line);
                    break;
                case "SELECT":
                    metrics.Queries = Increment(metrics.Queries);
                    firstSemanticLine = Math.Min(firstSemanticLine, token.Line);
                    break;
                case "INSERT":
                case "MERGE":
                    metrics.DataModificationStatements = Increment(metrics.DataModificationStatements);
                    firstSemanticLine = Math.Min(firstSemanticLine, token.Line);
                    break;
                case "UPDATE" when !PreviousWordIs(tokens, index, "ON"):
                case "DELETE" when !PreviousWordIs(tokens, index, "ON"):
                    metrics.DataModificationStatements = Increment(metrics.DataModificationStatements);
                    firstSemanticLine = Math.Min(firstSemanticLine, token.Line);
                    break;
                case "JOIN":
                    metrics.Joins = Increment(metrics.Joins);
                    break;
                case "WITH" when IsCte(tokens, index):
                    metrics.Ctes = Increment(metrics.Ctes);
                    break;
                case "OVER" when NextSymbolIs(tokens, index, "("):
                    metrics.WindowFunctions = Increment(metrics.WindowFunctions);
                    break;
                case "BEGIN" when NextWordIs(tokens, index, "TRAN", "TRANSACTION"):
                case "START" when NextWordIs(tokens, index, "TRANSACTION"):
                    metrics.Transactions = Increment(metrics.Transactions);
                    break;
            }

            if (IsSubquery(tokens, index))
            {
                metrics.Subqueries = Increment(metrics.Subqueries);
            }

            AddIntegrationSignals(tokens, index, metrics.IntegrationSignals);
        }

        (metrics.Statements, metrics.UnknownStatements, bool balanced) = CountStatements(tokens);
        SqlDialectAssessment dialect = SqlDialectDetector.Detect(tokens, tokenization);
        string parserConfidence = tokenization.Truncated || tokenization.UnterminatedConstruct || !balanced
            ? "low"
            : metrics.UnknownStatements > Math.Max(2, metrics.Statements / 4)
                ? "medium"
                : "high";
        return new SqlSemanticAnalysis
        {
            Metrics = metrics,
            Dialect = dialect,
            ParserConfidence = parserConfidence,
            Truncated = tokenization.Truncated,
            StructurallyBalanced = balanced,
            UnterminatedConstruct = tokenization.UnterminatedConstruct,
            FirstSemanticLine = firstSemanticLine,
        };
    }

    private static void CountDdl(
        IReadOnlyList<SqlToken> tokens,
        int index,
        SqlSemanticMetrics metrics)
    {
        int targetIndex = NextWordIndex(tokens, index + 1);
        if (targetIndex < 0)
        {
            return;
        }

        if (tokens[index].Value == "CREATE")
        {
            while (targetIndex >= 0 && tokens[targetIndex].Value is
                "OR" or "REPLACE" or "ALTER" or "TEMP" or "TEMPORARY" or "UNIQUE")
            {
                targetIndex = NextWordIndex(tokens, targetIndex + 1);
            }

            if (targetIndex >= 0 && tokens[targetIndex].Value == "MATERIALIZED")
            {
                int viewIndex = NextWordIndex(tokens, targetIndex + 1);
                if (viewIndex >= 0 && tokens[viewIndex].Value == "VIEW")
                {
                    targetIndex = viewIndex;
                }
            }
        }

        if (targetIndex < 0)
        {
            return;
        }

        bool recognized = true;
        switch (tokens[targetIndex].Value)
        {
            case "TABLE": metrics.Tables = Increment(metrics.Tables); break;
            case "VIEW": metrics.Views = Increment(metrics.Views); break;
            case "INDEX": metrics.Indexes = Increment(metrics.Indexes); break;
            case "SEQUENCE": metrics.Sequences = Increment(metrics.Sequences); break;
            case "TYPE":
            case "DOMAIN": metrics.Types = Increment(metrics.Types); break;
            case "FUNCTION": metrics.Functions = Increment(metrics.Functions); break;
            case "PROCEDURE":
            case "PROC": metrics.Procedures = Increment(metrics.Procedures); break;
            case "TRIGGER": metrics.Triggers = Increment(metrics.Triggers); break;
            default: recognized = false; break;
        }

        if (recognized)
        {
            metrics.DdlStatements = Increment(metrics.DdlStatements);
        }

        CountConstraint(tokens, index, metrics);
    }

    private static void CountConstraint(
        IReadOnlyList<SqlToken> tokens,
        int ddlIndex,
        SqlSemanticMetrics metrics)
    {
        int end = Math.Min(tokens.Count, ddlIndex + 2_000);
        int depth = 0;
        for (int index = ddlIndex; index < end; index++)
        {
            if (tokens[index].Kind == SqlTokenKind.Symbol)
            {
                if (tokens[index].Value == "(") depth++;
                if (tokens[index].Value == ")") depth--;
                if (tokens[index].Value == ";" && depth <= 0) break;
                continue;
            }

            string word = tokens[index].Value;
            if ((word == "PRIMARY" || word == "FOREIGN") && NextWordIs(tokens, index, "KEY") ||
                word == "CHECK" ||
                word == "UNIQUE" && !PreviousWordIs(tokens, index, "CREATE"))
            {
                metrics.Constraints = Increment(metrics.Constraints);
            }
        }
    }

    private static bool IsCte(IReadOnlyList<SqlToken> tokens, int index)
    {
        int limit = Math.Min(tokens.Count, index + 16);
        for (int candidate = index + 1; candidate < limit; candidate++)
        {
            if (tokens[candidate].Kind == SqlTokenKind.Word && tokens[candidate].Value == "AS")
            {
                return NextSymbolIs(tokens, candidate, "(");
            }

            if (tokens[candidate].Kind == SqlTokenKind.Symbol && tokens[candidate].Value == ";")
            {
                break;
            }
        }

        return false;
    }

    private static bool IsSubquery(IReadOnlyList<SqlToken> tokens, int index) =>
        tokens[index].Value is "SELECT" or "WITH" &&
        PreviousSymbolIs(tokens, index, "(");

    private static void AddIntegrationSignals(
        IReadOnlyList<SqlToken> tokens,
        int index,
        HashSet<string> signals)
    {
        string word = tokens[index].Value;
        if (word is "DBLINK" or "OPENQUERY" or "OPENROWSET" or "OPENDATASOURCE")
        {
            signals.Add(word.ToLowerInvariant());
        }
        else if (word == "FOREIGN" && NextWordIs(tokens, index, "TABLE", "SERVER", "DATA"))
        {
            signals.Add("foreign-data");
        }
        else if (word == "ATTACH" && NextWordIs(tokens, index, "DATABASE"))
        {
            signals.Add("attached-database");
        }
        else if (word == "FEDERATED" && PreviousWordIs(tokens, index, "ENGINE"))
        {
            signals.Add("federated-engine");
        }
        else if (word == "LINK" && PreviousWordIs(tokens, index, "DATABASE"))
        {
            signals.Add("database-link");
        }
    }

    private static (int Statements, int Unknown, bool Balanced) CountStatements(
        IReadOnlyList<SqlToken> tokens)
    {
        int statements = 0;
        int unknown = 0;
        int depth = 0;
        string? firstWord = null;
        foreach (SqlToken token in tokens)
        {
            if (token.Kind == SqlTokenKind.Symbol)
            {
                if (token.Value == "(") depth++;
                if (token.Value == ")") depth--;
                if (token.Value == ";" && depth == 0)
                {
                    CompleteStatement(ref firstWord, ref statements, ref unknown);
                }

                continue;
            }

            if (token.Kind == SqlTokenKind.Word && token.Value == "GO" && depth == 0)
            {
                CompleteStatement(ref firstWord, ref statements, ref unknown);
                continue;
            }

            if (firstWord is null && token.Kind == SqlTokenKind.Word)
            {
                firstWord = token.Value;
            }
        }

        CompleteStatement(ref firstWord, ref statements, ref unknown);
        return (statements, unknown, depth == 0);
    }

    private static void CompleteStatement(
        ref string? firstWord,
        ref int statements,
        ref int unknown)
    {
        if (firstWord is null)
        {
            return;
        }

        statements = Increment(statements);
        if (!KnownStatementStarters.Contains(firstWord))
        {
            unknown = Increment(unknown);
        }

        firstWord = null;
    }

    private static int NextWordIndex(IReadOnlyList<SqlToken> tokens, int index)
    {
        for (int candidate = index; candidate < tokens.Count && candidate < index + 12; candidate++)
        {
            if (tokens[candidate].Kind == SqlTokenKind.Word)
            {
                return candidate;
            }
        }

        return -1;
    }

    private static bool NextWordIs(
        IReadOnlyList<SqlToken> tokens,
        int index,
        params string[] values)
    {
        int next = NextWordIndex(tokens, index + 1);
        return next >= 0 && values.Contains(tokens[next].Value, StringComparer.Ordinal);
    }

    private static bool PreviousWordIs(
        IReadOnlyList<SqlToken> tokens,
        int index,
        string value)
    {
        for (int candidate = index - 1; candidate >= 0 && candidate >= index - 6; candidate--)
        {
            if (tokens[candidate].Kind == SqlTokenKind.Word)
            {
                return tokens[candidate].Value == value;
            }
        }

        return false;
    }

    private static bool NextSymbolIs(IReadOnlyList<SqlToken> tokens, int index, string value)
    {
        int candidate = index + 1;
        return candidate < tokens.Count &&
            tokens[candidate].Kind == SqlTokenKind.Symbol &&
            tokens[candidate].Value == value;
    }

    private static bool PreviousSymbolIs(IReadOnlyList<SqlToken> tokens, int index, string value)
    {
        int candidate = index - 1;
        return candidate >= 0 &&
            tokens[candidate].Kind == SqlTokenKind.Symbol &&
            tokens[candidate].Value == value;
    }

    private static int Increment(int value) => Math.Min(MaximumMeasurement, value + 1);
}
