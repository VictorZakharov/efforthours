using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Scripting;

internal enum ScriptLanguage
{
    Shell,
    PowerShell,
}

internal enum ScriptRole
{
    Product,
    Module,
    Test,
    Build,
    Ci,
    Delivery,
    Infrastructure,
}

internal enum ScriptTokenKind
{
    Word,
    String,
    Variable,
    Operator,
    NewLine,
    HereDocument,
}

internal sealed record ScriptToken(
    ScriptTokenKind Kind,
    string Text,
    int Line);

internal sealed record ScriptTokenizationResult(
    IReadOnlyList<ScriptToken> Tokens,
    bool Complete,
    bool Truncated);

internal sealed record ScriptSyntaxAnalysis(
    string Confidence,
    string Dialect,
    ScriptSourceMetrics Metrics);

internal sealed record ScriptFileAnalysis(
    EvidenceFact File,
    ScriptLanguage Language,
    ScriptRole Role,
    ScriptSyntaxAnalysis Syntax);

internal sealed class ScriptSourceMetrics
{
    public int Functions { get; set; }

    public int Methods { get; set; }

    public int Types { get; set; }

    public int PublicSymbols { get; set; }

    public int Parameters { get; set; }

    public int Conditionals { get; set; }

    public int Loops { get; set; }

    public int BranchPoints { get; set; }

    public int Pipelines { get; set; }

    public int ErrorHandlers { get; set; }

    public int ExternalCommands { get; set; }

    public int CmdletCalls { get; set; }

    public int FileOperations { get; set; }

    public int NetworkOperations { get; set; }

    public int ProcessOperations { get; set; }

    public int ModuleOperations { get; set; }

    public int AsyncUnits { get; set; }

    public int SourcedFiles { get; set; }

    public int DynamicExpansions { get; set; }

    public int TestCases { get; set; }

    public int Assertions { get; set; }

    public int MockUsages { get; set; }

    public int CredentialCandidates { get; set; }

    public int TopLevelCommands { get; set; }

    public int RequiresDirectives { get; set; }

    public int HereDocuments { get; set; }

    public bool HasShebang { get; set; }

    public bool HasDynamicInvocation { get; set; }

    public bool HasUnresolvedSourcing { get; set; }

    public HashSet<string> Technologies { get; } = new(StringComparer.Ordinal);
}
