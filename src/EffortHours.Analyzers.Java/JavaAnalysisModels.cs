using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Java;

internal sealed record JavaFileAnalysis(
    EvidenceFact File,
    JavaProjectModel Project,
    JavaSyntaxAnalysis Syntax)
{
    public string Directory => JavaPath.Directory(File.Scope);
}
