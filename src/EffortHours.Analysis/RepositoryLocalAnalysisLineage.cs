using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EffortHours.Contracts.V1;

namespace EffortHours.Analysis;

internal static class RepositoryLocalAnalysisLineage
{
    internal static bool SupportedPath(string path) => Path.GetExtension(path).ToLowerInvariant() is
        ".cs" or ".ts" or ".tsx" or ".sql";

    internal static string? ContextIdentity(IRepositoryFileSystem fs, string root, RepositoryEvidence evidence,
        CancellationToken token)
    {
        root = fs.GetFullPath(root);
        if (!fs.DirectoryExists(root) || (fs.GetAttributes(root) & FileAttributes.ReparsePoint) != 0 ||
            fs is not IRepositoryImmutableIdentityProvider identity ||
            string.IsNullOrWhiteSpace(identity.RepositoryPathSetIdentity)) return null;
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(hash, "local-analysis-context/1");
        Append(hash, identity.RepositoryPathSetIdentity);
        foreach (EvidenceFact file in evidence.Facts.Where(file => file.Kind == EvidenceKinds.File &&
            !SupportedPath(file.Scope)).OrderBy(file => file.Scope, StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            string path = Path.Combine(root, file.Scope.Replace('/', Path.DirectorySeparatorChar));
            if (!identity.TryGetFileContentId(path, out string id)) return null;
            Append(hash, file.Scope);
            Append(hash, id);
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    internal static async Task StoreAsync<T>(IRepositoryFileSystem fs, string root, EvidenceFact file,
        string? context, T analysis, CancellationToken token) where T : class
    {
        decimal? bytes = file.Measurements.FirstOrDefault(item => item.Name == "bytes")?.Value;
        if (context is null || bytes is null or < 0 or > 65536 ||
            fs is not IRepositoryVersionedAnalysisProvider { VersionedAnalysisCache: { } cache } provider ||
            fs is not IRepositoryImmutableIdentityProvider identity) return;
        string path = Path.Combine(root, file.Scope.Replace('/', Path.DirectorySeparatorChar));
        if (!provider.TryGetPreviousFileVersion(path, out RepositoryFileVersion previous) ||
            !identity.TryGetFileContentId(path, out string id) || previous.ContentId == id) return;
        string? digest = file.Tags.FirstOrDefault(tag => tag.StartsWith("sha256:", StringComparison.Ordinal));
        if (digest is null) return;
        LocalState<T> state = new(context, digest, analysis);
        long charge;
        try { charge = 1024L + 4L * JsonSerializer.SerializeToUtf8Bytes(state).Length; }
        catch (NotSupportedException) { return; }
        _ = await cache.GetOrCreateAsync(Key<T>(id, file.Scope),
            _ => Task.FromResult(new RepositoryVersionedAnalysisArtifact<LocalState<T>>(state, charge)), token)
            .ConfigureAwait(false);
    }

    internal static bool TryGetPrevious<T>(IRepositoryFileSystem fs, string root, EvidenceFact file,
        string context, out T analysis) where T : class
    {
        analysis = null!;
        string path = Path.Combine(root, file.Scope.Replace('/', Path.DirectorySeparatorChar));
        if (fs is not IRepositoryVersionedAnalysisProvider { VersionedAnalysisCache: { } cache } provider ||
            !provider.TryGetPreviousFileVersion(path, out RepositoryFileVersion previous) ||
            !cache.TryGetCompleted(Key<T>(previous.ContentId, file.Scope), out LocalState<T> state) ||
            state.Context != context || !file.Tags.Contains(state.Digest, StringComparer.Ordinal)) return false;
        analysis = state.Analysis;
        return true;
    }

    private static string Key<T>(string id, string path) => "local-analysis/1/" + typeof(T).FullName + "/" +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\0', id, path))));
    private static void Append(IncrementalHash hash, string value)
    {
        hash.AppendData(Encoding.UTF8.GetBytes(value));
        hash.AppendData([0]);
    }
    private sealed record LocalState<T>(string Context, string Digest, T Analysis);
}
