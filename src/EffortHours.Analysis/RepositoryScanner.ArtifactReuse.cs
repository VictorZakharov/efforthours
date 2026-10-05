namespace EffortHours.Analysis;

public sealed partial class RepositoryScanner
{
    // Immutable identity is sufficient to request a verified common artifact.
    // Length metadata is needed only on a miss, before bounded content inspection.
    private static bool TryReuseImmutableFile(ScanState state, string fullPath,
        string relativePath, FileInspectionPipeline pipeline, out string? key,
        out RepositoryAnalysisArtifactCache.RepositoryAnalysisArtifactRequest<ScannedFile>? request)
    {
        key = null;
        request = null;
        if (state.AnalysisArtifactCache is null ||
            state.FileSystem is not IRepositoryImmutableIdentityProvider immutable ||
            !immutable.TryGetFileContentId(fullPath, out string contentId)) return false;
        key = $"common-scanned-file/{AnalyzerVersion}/sample-{state.Options.TextSampleSize}/" +
            $"{contentId}/{relativePath}";
        if (state.AnalysisArtifactCache.TryGetCompleted(key, "common:" + relativePath, out ScannedFile completed))
        {
            state.Files.Add(completed);
            return true;
        }
        request = state.AnalysisArtifactCache.Request<ScannedFile>(key, "common:" + relativePath);
        if (request.IsOwner) return false;
        if (request.Result.IsCompletedSuccessfully)
            state.Files.Add(request.Result.GetAwaiter().GetResult());
        else
            pipeline.TrackPending(new FileInspectionWork(fullPath, relativePath, key, 0, request),
                request.Result);
        return true;
    }
}
