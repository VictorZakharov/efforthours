using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Python;

internal sealed record PythonFileAnalysis(
    EvidenceFact File,
    PythonPackageModel Package,
    PythonSyntaxAnalysis Syntax);

internal sealed record JupyterNotebookAnalysis(
    EvidenceFact File,
    PythonPackageModel Package,
    string Confidence,
    string DeclaredLanguage,
    int TotalCells,
    int CodeCells,
    int PythonCodeCells,
    int MarkdownCells,
    int UniqueMarkdownCells,
    int DuplicateMarkdownCells,
    int RawCells,
    int UnsupportedCodeCells,
    int UniqueCodeCells,
    int DuplicateCodeCells,
    int OutputCells,
    int ExecutionCountCells,
    int AttachmentCells,
    int MagicLines,
    int ShellEscapeLines,
    int MarkdownLines,
    int MarkdownHeadings,
    int MarkdownLinks,
    bool HasWidgetState,
    bool SafeguardReached,
    bool IsCanonical,
    string MaintainedProjectionDigest,
    PythonSyntaxAnalysis Syntax);
