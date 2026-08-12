using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.Java;

internal sealed record KotlinFileAnalysis(
    EvidenceFact File,
    JavaProjectModel Project,
    KotlinSyntaxAnalysis Syntax);
