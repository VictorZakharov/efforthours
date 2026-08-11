using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Go;

internal sealed record GoFileAnalysis(
    EvidenceFact File,
    GoModuleModel Module,
    GoSyntaxAnalysis Syntax)
{
    public string Directory => GoPath.Directory(File.Scope);
}
