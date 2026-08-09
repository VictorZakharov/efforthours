using System.Collections.Frozen;
using EffortHours.Contracts.V1;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace EffortHours.Analyzers.DotNet;

internal static class CSharpDataEvidenceAnalyzer
{
    private static readonly FrozenSet<string> StrongDataInvocationNames = new[]
    {
        "ExecuteScalar", "ExecuteSql", "ExecuteSqlRaw", "FromSql", "FromSqlRaw",
        "SaveChanges", "SaveChangesAsync",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ContextualDataInvocationNames = new[]
    {
        "Execute", "ExecuteAsync", "Query", "QueryAsync",
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> DataPrimitiveNames = new[]
    {
        "DbCommand", "DbConnection", "DbContext", "DbDataReader", "DbSet", "IDbCommand",
        "IDbConnection", "IDataReader", "IQueryable", "NpgsqlConnection", "SqlCommand",
        "SqlConnection",
    }.ToFrozenSet(StringComparer.Ordinal);

    public static void AddFact(
        List<EvidenceFact> facts,
        string path,
        string projectScope,
        IReadOnlyList<SyntaxNode> nodes,
        IReadOnlyList<BaseTypeDeclarationSyntax> types)
    {
        BaseTypeDeclarationSyntax[] contexts =
        [
            .. types.Where(type => BaseTypeNames(type).Contains("DbContext", StringComparer.Ordinal)),
        ];
        int dbSets = nodes.OfType<PropertyDeclarationSyntax>()
            .Count(property => GetSimpleName(property.Type) == "DbSet");
        BaseTypeDeclarationSyntax[] migrations =
        [
            .. types.Where(type =>
                BaseTypeNames(type).Contains("Migration", StringComparer.Ordinal) ||
                path.Contains("/Migrations/", StringComparison.OrdinalIgnoreCase)),
        ];
        int entityConfigurations = types.Count(type =>
            BaseTypeNames(type).Contains("IEntityTypeConfiguration", StringComparer.Ordinal));
        bool hasDataPrimitives = nodes.OfType<SimpleNameSyntax>()
            .Select(name => name.Identifier.ValueText)
            .Any(DataPrimitiveNames.Contains);
        InvocationExpressionSyntax[] dataCalls =
        [
            .. nodes.OfType<InvocationExpressionSyntax>()
                .Where(invocation => IsDataCall(invocation, hasDataPrimitives)),
        ];
        int repositoryTypes = hasDataPrimitives
            ? types.Count(type => GetDeclaredTypeName(type)
                .EndsWith("Repository", StringComparison.Ordinal))
            : 0;
        if (contexts.Length == 0 && dbSets == 0 && migrations.Length == 0 &&
            entityConfigurations == 0 && repositoryTypes == 0 && dataCalls.Length == 0)
        {
            return;
        }

        List<EvidenceLocation> locations =
        [
            .. contexts.Select(context => Location(path, context, GetDeclaredTypeName(context))),
            .. migrations.Select(migration => Location(path, migration, GetDeclaredTypeName(migration))),
            .. dataCalls.Select(call => Location(path, call, GetInvocationName(call))),
        ];
        facts.Add(DotNetEvidence.Fact(
            $"dotnet:data:{path}",
            EvidenceKinds.DataAccess,
            projectScope,
            $"Data access or persistence structure detected in '{path}'.",
            EvidenceSourceKind.Inferred,
            "Roslyn data-type, persistence-path, and context-qualified invocation classification",
            NormalizeLocations(locations),
            [
                DotNetEvidence.Measurement("db-contexts", contexts.Length, "types"),
                DotNetEvidence.Measurement("db-sets", dbSets, "properties"),
                DotNetEvidence.Measurement("migrations", migrations.Length, "types"),
                DotNetEvidence.Measurement("entity-configurations", entityConfigurations, "types"),
                DotNetEvidence.Measurement("repository-types", repositoryTypes, "types"),
                DotNetEvidence.Measurement("data-calls", dataCalls.Length, "calls"),
            ]));
    }

    private static bool IsDataCall(
        InvocationExpressionSyntax invocation,
        bool hasDataPrimitives)
    {
        string invocationName = GetInvocationName(invocation);
        if (StrongDataInvocationNames.Contains(invocationName))
        {
            return true;
        }

        return ContextualDataInvocationNames.Contains(invocationName) &&
            (hasDataPrimitives || HasDataReceiverHint(invocation));
    }

    private static bool HasDataReceiverHint(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax member)
        {
            return false;
        }

        return member.Expression.DescendantNodesAndSelf()
            .OfType<SimpleNameSyntax>()
            .Select(name => name.Identifier.ValueText)
            .Any(name => name.Equals("db", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("database", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("sql", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("Db", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("Database", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("Repository", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("sql", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> BaseTypeNames(BaseTypeDeclarationSyntax type) =>
        type.BaseList?.Types.Select(baseType => GetSimpleName(baseType.Type)) ?? [];

    private static string GetInvocationName(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
            _ => string.Empty,
        };

    private static string GetSimpleName(SyntaxNode node) =>
        node.DescendantNodesAndSelf()
            .OfType<SimpleNameSyntax>()
            .LastOrDefault()?
            .Identifier.ValueText ?? string.Empty;

    private static string GetDeclaredTypeName(BaseTypeDeclarationSyntax type) => type switch
    {
        TypeDeclarationSyntax declaration => declaration.Identifier.ValueText,
        EnumDeclarationSyntax declaration => declaration.Identifier.ValueText,
        _ => string.Empty,
    };

    private static EvidenceLocation Location(
        string path,
        SyntaxNode node,
        string? symbol = null) => DotNetEvidence.Location(
            path,
            node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
            symbol);

    private static EvidenceLocation[] NormalizeLocations(IEnumerable<EvidenceLocation> locations) =>
        [
            .. locations
                .Distinct()
                .OrderBy(location => location.Line ?? int.MaxValue)
                .ThenBy(location => location.Symbol, StringComparer.Ordinal)
                .Take(50),
        ];
}
