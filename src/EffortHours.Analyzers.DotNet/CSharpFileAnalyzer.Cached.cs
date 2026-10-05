using EffortHours.Analysis;

namespace EffortHours.Analyzers.DotNet;

internal sealed partial class CSharpFileAnalyzer
{
    internal bool TryGetCompleted(string path, string digest, string projectScope, bool isTest,
        out CSharpFileAnalysis analysis)
    {
        analysis = null!;
        if (_fileSystem is not IRepositoryImmutableIdentityProvider immutable ||
            (_fileSystem as IRepositoryAnalysisArtifactCacheProvider)?.AnalysisArtifactCache is not { } cache ||
            !immutable.TryGetFileContentId(ToFullPath(path), out string contentId)) return false;
        return cache.TryGetCompleted(AnalysisArtifactKey(contentId, digest, path, projectScope, isTest),
            "dotnet-csharp:" + path, out analysis);
    }
}
