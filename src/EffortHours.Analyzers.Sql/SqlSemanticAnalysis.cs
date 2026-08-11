namespace EffortHours.Analyzers.Sql;

internal sealed record SqlSemanticAnalysis
{
    public required SqlSemanticMetrics Metrics { get; init; }

    public required SqlDialectAssessment Dialect { get; init; }

    public required string ParserConfidence { get; init; }

    public required bool Truncated { get; init; }

    public required bool StructurallyBalanced { get; init; }

    public required bool UnterminatedConstruct { get; init; }

    public required int FirstSemanticLine { get; init; }
}

internal sealed class SqlSemanticMetrics
{
    public int Statements { get; set; }

    public int UnknownStatements { get; set; }

    public int DdlStatements { get; set; }

    public int Tables { get; set; }

    public int Views { get; set; }

    public int Indexes { get; set; }

    public int Constraints { get; set; }

    public int Sequences { get; set; }

    public int Types { get; set; }

    public int Functions { get; set; }

    public int Procedures { get; set; }

    public int Triggers { get; set; }

    public int Queries { get; set; }

    public int Joins { get; set; }

    public int Ctes { get; set; }

    public int Subqueries { get; set; }

    public int WindowFunctions { get; set; }

    public int Transactions { get; set; }

    public int DataModificationStatements { get; set; }

    public HashSet<string> IntegrationSignals { get; } = new(StringComparer.Ordinal);

    public int StoredPrograms => Functions + Procedures;

    public bool HasDataSemantics => DdlStatements > 0 || Queries > 0 ||
        DataModificationStatements > 0 || StoredPrograms > 0 || Triggers > 0;

    public int SchemaConfigurationUnits => Math.Min(
        2_000,
        Views + Indexes + Sequences + Types + (int)Math.Ceiling(Constraints / 3m));

    public int QueryComplexityUnits => Math.Min(
        2_000,
        Queries + DataModificationStatements +
        (int)Math.Ceiling((Joins + Ctes + Subqueries + WindowFunctions + Transactions) / 2m));
}
