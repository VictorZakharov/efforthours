using System.Security.Cryptography;
using System.Text;
using EffortHours.Analysis;
using EffortHours.Contracts.V1;

namespace EffortHours.Analyzers.DotNet;

internal sealed partial class DotNetProjectReader
{
    private async Task<DotNetProjectReadResult> ReadThroughCacheAsync(
        RepositoryEvidence evidence,
        CancellationToken cancellationToken)
    {
        string[] projectPaths = GetFilePaths(evidence, IsProjectFile);
        string[] solutionPaths = GetFilePaths(evidence, IsSolutionFile);
        string[] centralPackagePaths = GetFilePaths(
            evidence,
            path => Path.GetFileName(path).Equals(
                "Directory.Packages.props",
                StringComparison.OrdinalIgnoreCase));
        string? cacheKey = TryCreateCacheKey(
            projectPaths,
            solutionPaths,
            centralPackagePaths);
        RepositoryAnalysisArtifactCache? cache =
            (_fileSystem as IRepositoryAnalysisArtifactCacheProvider)?.AnalysisArtifactCache;
        if (cacheKey is null || cache is null)
        {
            return await ReadUncachedAsync(
                projectPaths,
                solutionPaths,
                centralPackagePaths,
                cancellationToken).ConfigureAwait(false);
        }

        return await cache.GetOrCreateAsync(
            cacheKey,
            async itemCancellationToken => (await ReadUncachedAsync(
                projectPaths,
                solutionPaths,
                centralPackagePaths,
                itemCancellationToken).ConfigureAwait(false)) with
            {
                ImmutableCacheKey = cacheKey,
            },
            cancellationToken).ConfigureAwait(false);
    }

    private string? TryCreateCacheKey(
        IReadOnlyList<string> projectPaths,
        IReadOnlyList<string> solutionPaths,
        IReadOnlyList<string> centralPackagePaths)
    {
        if (_fileSystem is not IRepositoryImmutableIdentityProvider identityProvider)
        {
            return null;
        }

        string? pathSetIdentity = identityProvider.RepositoryPathSetIdentity;
        if (string.IsNullOrWhiteSpace(pathSetIdentity))
        {
            return null;
        }

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData("efforthours:dotnet-project-context:1\0"u8);
        Append(hash, DotNetEvidence.AnalyzerVersion);
        Append(hash, pathSetIdentity);
        return AppendFiles(hash, identityProvider, "project", projectPaths) &&
            AppendFiles(hash, identityProvider, "solution", solutionPaths) &&
            AppendFiles(hash, identityProvider, "central-package", centralPackagePaths)
                ? $"dotnet-project-context/{DotNetEvidence.AnalyzerVersion}/" +
                    Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()
                : null;
    }

    private bool AppendFiles(
        IncrementalHash hash,
        IRepositoryImmutableIdentityProvider identityProvider,
        string kind,
        IEnumerable<string> relativePaths)
    {
        foreach (string relativePath in relativePaths)
        {
            if (!identityProvider.TryGetFileContentId(
                    ToFullPath(relativePath),
                    out string contentId) ||
                string.IsNullOrWhiteSpace(contentId))
            {
                return false;
            }

            Append(hash, kind);
            Append(hash, relativePath);
            Append(hash, contentId);
        }

        return true;
    }

    private static void Append(IncrementalHash hash, string value)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(value));
        hash.AppendData([0]);
    }
}
