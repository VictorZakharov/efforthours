using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Python;

internal sealed record PythonFileAnalysis(
    EvidenceFact File,
    PythonPackageModel Package,
    PythonSyntaxAnalysis Syntax);
