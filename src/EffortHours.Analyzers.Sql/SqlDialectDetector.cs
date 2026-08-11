namespace EffortHours.Analyzers.Sql;

internal sealed record SqlDialectAssessment(
    string Dialect,
    string Confidence,
    IReadOnlyList<string> Signals);

internal static class SqlDialectDetector
{
    public static SqlDialectAssessment Detect(
        IReadOnlyList<SqlToken> tokens,
        SqlTokenizationResult tokenization)
    {
        Dictionary<string, int> scores = new(StringComparer.Ordinal)
        {
            ["postgresql"] = tokenization.DollarQuotedStringCount * 2,
            ["sql-server"] = tokenization.BracketIdentifierCount,
            ["mysql-mariadb"] = tokenization.BacktickIdentifierCount,
            ["sqlite"] = 0,
        };
        HashSet<string> signals = new(StringComparer.Ordinal);
        foreach (SqlToken token in tokens.Where(token => token.Kind == SqlTokenKind.Word))
        {
            switch (token.Value)
            {
                case "ILIKE":
                case "JSONB":
                case "PLPGSQL":
                case "SERIAL":
                case "BIGSERIAL":
                    scores["postgresql"]++;
                    signals.Add("postgresql-keyword");
                    break;
                case "GO":
                case "NVARCHAR":
                case "RAISERROR":
                case "OPENQUERY":
                case "TRY":
                case "CATCH":
                    scores["sql-server"]++;
                    signals.Add("sql-server-keyword");
                    break;
                case "AUTO_INCREMENT":
                case "DELIMITER":
                case "UNSIGNED":
                case "SQL_MODE":
                    scores["mysql-mariadb"]++;
                    signals.Add("mysql-mariadb-keyword");
                    break;
                case "PRAGMA":
                case "AUTOINCREMENT":
                case "VACUUM":
                    scores["sqlite"]++;
                    signals.Add("sqlite-keyword");
                    break;
                default:
                    if (token.Value.StartsWith("SQLITE_", StringComparison.Ordinal))
                    {
                        scores["sqlite"]++;
                        signals.Add("sqlite-keyword");
                    }

                    break;
            }
        }

        (string dialect, int score) = scores
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .First();
        int second = scores.Where(pair => pair.Key != dialect).Max(pair => pair.Value);
        if (score == 0)
        {
            return new SqlDialectAssessment("standard-or-unknown", "low", []);
        }

        if (score == second)
        {
            return new SqlDialectAssessment(
                "mixed-or-ambiguous",
                "low",
                [.. signals.Order(StringComparer.Ordinal)]);
        }

        string confidence = score >= 4 && score - second >= 2
            ? "high"
            : score >= 2
                ? "medium"
                : "low";
        return new SqlDialectAssessment(
            dialect,
            confidence,
            [.. signals.Order(StringComparer.Ordinal)]);
    }
}
