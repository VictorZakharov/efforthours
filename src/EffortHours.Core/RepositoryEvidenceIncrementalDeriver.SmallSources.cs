using System.Security.Cryptography;
using EffortHours.Analysis;
using EffortHours.Analyzers.JavaScript;
using EffortHours.Analyzers.Sql;
using EffortHours.Contracts.V1;

namespace EffortHours.Core;

internal static partial class RepositoryEvidenceIncrementalDeriver
{
    public static async Task<RepositoryEvidence?> TryDeriveAsync(RepositoryEvidence previous,
        IRepositoryFileSystem fs, string root, IReadOnlyList<string> changedPaths, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(changedPaths);
        token.ThrowIfCancellationRequested();
        if (changedPaths.Count == 0 || changedPaths.Count == 1 &&
            Path.GetExtension(changedPaths[0]).Equals(".cs", StringComparison.OrdinalIgnoreCase))
            return await TryDeriveSingleCSharpAsync(previous, fs, root, changedPaths, token).ConfigureAwait(false);
        if (changedPaths.Count > 8 || changedPaths.Distinct(StringComparer.Ordinal).Count() != changedPaths.Count ||
            changedPaths.Any(path => !RepositoryLocalAnalysisLineage.SupportedPath(path))) return null;
        string? context = RepositoryLocalAnalysisLineage.ContextIdentity(fs, root, previous, token);
        if (context is null) return null;
        HashSet<string> paths = new(changedPaths, StringComparer.Ordinal);
        Dictionary<string, EvidenceFact> files = previous.Facts.Where(fact =>
            fact.Kind == EvidenceKinds.File && paths.Contains(fact.Scope)).ToDictionary(fact => fact.Scope, StringComparer.Ordinal);
        if (files.Count != changedPaths.Count) return null;
        Dictionary<string, EvidenceFact> replacements = new(StringComparer.Ordinal);
        HashSet<string> oldHashes = new(StringComparer.Ordinal), newHashes = new(StringComparer.Ordinal);
        foreach (string path in changedPaths)
        {
            token.ThrowIfCancellationRequested();
            EvidenceFact old = files[path];
            if (old.Tags.Any(tag => tag is "classification:generated" or "classification:minified" or
                "classification:vendored" or "content:binary") || !TryGetByteLength(old, out long length) ||
                length > 65536 || !TryGetSha256(old, out string oldHash) || !oldHashes.Add("sha256:" + oldHash)) return null;
            string fullPath = fs.GetFullPath(Path.Combine(root, path.Replace('/', Path.DirectorySeparatorChar)));
            string relative = Path.GetRelativePath(fs.GetFullPath(root), fullPath);
            if (Path.IsPathRooted(relative) || relative == ".." ||
                relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                !fs.FileExists(fullPath) || (fs.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0 ||
                fs.GetFileMetadata(fullPath).Length != length) return null;
            byte[] bytes = await fs.ReadAllBytesAsync(fullPath, token).ConfigureAwait(false);
            if (bytes.LongLength != length) return null;
            EvidenceFact current;
            using (await RepositoryAnalysisConcurrency.AcquireFileAnalysisAsync(
                RepositoryAnalysisWorkKind.CommonFileInspection, token).ConfigureAwait(false))
                current = RepositoryScanner.InspectImmutableFileFact(path, bytes, token);
            if (!RepositoryEvidenceValueEquality.Fact(old, current with { Tags = old.Tags }) ||
                !old.Tags.Where(tag => !tag.StartsWith("sha256:", StringComparison.Ordinal))
                    .SequenceEqual(current.Tags.Where(tag => !tag.StartsWith("sha256:", StringComparison.Ordinal)))) return null;
            string newHash = "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!newHashes.Add(newHash)) return null;
            bool equivalent;
            if (Path.GetExtension(path).Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                using IDisposable lease = await RepositoryAnalysisConcurrency.AcquireFileAnalysisAsync(
                    RepositoryAnalysisWorkKind.SemanticFileAnalysis, token).ConfigureAwait(false);
                equivalent = await EffortHours.Analyzers.DotNet.CSharpEvidenceLineage.TryAdvanceExactEvidenceAsync(
                    fs, fullPath, path, bytes, new EffortHours.Analyzers.DotNet.DotNetProjectReader(fs, root)
                        .GetImmutableContextIdentity(previous, token) ?? "missing-context", token).ConfigureAwait(false);
            }
            else equivalent = await EquivalentLocalSourceAsync(previous, old, current, fs, root, context, token).ConfigureAwait(false);
            if (!equivalent) return null;
            replacements.Add(path, current);
        }
        RepositoryEvidence candidate = WithoutScopeDiagnostic(previous) with
        {
            Facts = [.. previous.Facts.Select(fact => fact.Kind == EvidenceKinds.File &&
                replacements.TryGetValue(fact.Scope, out EvidenceFact? replacement) ? replacement : fact)],
        };
        return RepositoryStockEvidenceEquality.SingletonHashes(previous, oldHashes, token) &&
            RepositoryStockEvidenceEquality.SingletonHashes(candidate, newHashes, token) ? candidate : null;
    }

    private static async Task<bool> EquivalentLocalSourceAsync(RepositoryEvidence repository, EvidenceFact old,
        EvidenceFact current, IRepositoryFileSystem fs, string root, string context, CancellationToken token)
    {
        if (Path.GetExtension(current.Scope).Equals(".sql", StringComparison.OrdinalIgnoreCase))
        {
            if (!RepositoryLocalAnalysisLineage.TryGetPrevious(fs, root, old, context, out SqlFileAnalysis before)) return false;
            SqlFileAnalysis after = await new SqlFileAnalysisReader(fs, root).ReadAsync(current, token).ConfigureAwait(false);
            if (before.Diagnostic is not null || after.Diagnostic is not null ||
                after.Analysis is not { StructurallyBalanced: true, UnterminatedConstruct: false, Truncated: false }) return false;
            if (!RepositoryLocalAnalysisValueEquality.Equal(before with { ImmutableCacheKey = null },
                    after with { ImmutableCacheKey = null })) return false;
            await RepositoryLocalAnalysisLineage.StoreAsync(fs, root, current, context, after, token).ConfigureAwait(false);
            return true;
        }
        if (!RepositoryLocalAnalysisLineage.TryGetPrevious(fs, root, old, context, out JavaScriptFileAnalysis previous)) return false;
        JavaScriptPackageReadResult packages = await new JavaScriptPackageReader(new RepositoryTextReader(fs, root))
            .ReadAsync(repository, token).ConfigureAwait(false);
        if (packages.Diagnostics.Count > 0) return false;
        JavaScriptPackageModel? package = JavaScriptRepositoryAnalyzer.FindOwningPackage(current.Scope, packages.Packages);
        JavaScriptFileAnalysis next = await new JavaScriptSourceAnalyzer(new RepositoryTextReader(fs, root), context)
            .AnalyzeAsync(current, package, token).ConfigureAwait(false);
        return previous.Diagnostics.Count == 0 && next.Diagnostics.Count == 0 &&
            RepositoryLocalAnalysisValueEquality.Equal(previous, next);
    }
}
