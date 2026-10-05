namespace EffortHours.Analysis;

public sealed partial class RepositoryScanner
{
    private sealed class IncompleteTraversalProofException : Exception;

    private sealed record TraversalPlan(IReadOnlyList<string> Files, IReadOnlyList<ExcludedEntry> Exclusions);

    private static async Task TraverseWithImmutablePlanAsync(ScanState state, CancellationToken cancellationToken)
    {
        string? identity = (state.FileSystem as IRepositoryTraversalIdentityProvider)?.RepositoryTraversalIdentity;
        RepositoryAnalysisArtifactCache? cache = state.AnalysisArtifactCache;
        if (identity is null || cache is null || state.FileSystem is not IRepositoryImmutableIdentityProvider)
        {
            await TraverseAsync(state, cancellationToken).ConfigureAwait(false);
            return;
        }
        string key = $"common-traversal/{AnalyzerVersion}/{identity}/" +
            $"gitignore:{state.Options.RespectGitIgnore}/ehignore:{state.Options.RespectEffortHoursIgnore}";
        RepositoryAnalysisArtifactCache.RepositoryAnalysisArtifactRequest<TraversalPlan> request =
            cache.Request<TraversalPlan>(key, "common-traversal");
        if (request.IsOwner)
        {
            try
            {
                await TraverseAsync(state, cancellationToken).ConfigureAwait(false);
                // A failed read is not an immutable admission proof. Do not freeze
                // transient missing/unreadable objects or partial inventories.
                if (state.Diagnostics.Count > 0)
                {
                    request.Fail(new IncompleteTraversalProofException());
                    return;
                }
                TraversalPlan plan = new([.. state.Files.Select(file => file.RelativePath).Order(StringComparer.Ordinal)],
                    [.. state.Exclusions]);
                cache.Add(key, plan);
                request.Complete(plan);
                return;
            }
            catch (Exception exception) { request.Fail(exception); throw; }
        }
        TraversalPlan retained;
        try { retained = await request.Result.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch (IncompleteTraversalProofException)
        {
            await TraverseAsync(state, cancellationToken).ConfigureAwait(false);
            return;
        }
        state.Exclusions.AddRange(retained.Exclusions);
        await using FileInspectionPipeline pipeline = new(state, cancellationToken);
        foreach (string relativePath in retained.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fullPath = Path.Combine(state.RootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            RepositoryAnalysisArtifactCache.RepositoryAnalysisArtifactRequest<ScannedFile>? artifactRequest = null;
            try
            {
                if (TryReuseImmutableFile(state, fullPath, relativePath, pipeline,
                    out string? artifactKey, out artifactRequest)) continue;
                RepositoryFileMetadata metadata = state.FileSystem.GetFileMetadata(fullPath);
                await pipeline.EnqueueAsync(new FileInspectionWork(fullPath, relativePath, artifactKey,
                    metadata.LastWriteTimeUtcTicks, artifactRequest),
                    metadata.Length <= RepositoryAnalysisConcurrency.MaximumBufferedFileBytes).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                artifactRequest?.Fail(exception);
                state.Exclusions.Add(new ExcludedEntry(relativePath, "unreadable", false));
                state.AddUnreadableDiagnostic(relativePath, exception);
            }
            catch (Exception exception) { artifactRequest?.Fail(exception); throw; }
        }
        foreach (FileInspectionResult result in await pipeline.CompleteAsync().ConfigureAwait(false))
            ApplyInspectionResult(state, result);
    }
}
