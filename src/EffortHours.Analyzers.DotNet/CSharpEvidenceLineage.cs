using System.Security.Cryptography;
using System.Text;
using EffortHours.Analysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace EffortHours.Analyzers.DotNet;

internal static class CSharpEvidenceLineage
{
    private const int MaximumNeutralChangeCharacters = 64 * 1024;

    private static readonly CSharpParseOptions ParseOptions = new(
        LanguageVersion.Preview,
        DocumentationMode.Parse,
        SourceCodeKind.Regular);

    public static CSharpSyntaxTree Parse(
        SourceText sourceText,
        string relativePath,
        CancellationToken cancellationToken) =>
        (CSharpSyntaxTree)CSharpSyntaxTree.ParseText(
            sourceText,
            ParseOptions,
            relativePath,
            cancellationToken);

    public static int CountSyntaxErrors(
        SyntaxTree tree,
        CancellationToken cancellationToken) => tree.GetDiagnostics(cancellationToken)
        .Count(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

    internal static SourceText CreateSourceText(byte[] bytes)
    {
        using MemoryStream stream = new(bytes, writable: false);
        using StreamReader reader = new(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false),
            detectEncodingFromByteOrderMarks: true);
        return SourceText.From(
            reader.ReadToEnd(),
            Encoding.UTF8,
            SourceHashAlgorithm.Sha256);
    }

    public static async Task StoreAnalyzedVersionAsync(
        IRepositoryFileSystem fileSystem,
        string fullPath,
        string relativePath,
        string? contentId,
        SourceText sourceText,
        CSharpSyntaxTree tree,
        int syntaxErrors,
        CancellationToken cancellationToken)
    {
        if (!TryGetChangedLineage(
            fileSystem,
            fullPath,
            contentId,
            out RepositoryVersionedAnalysisCache? cache,
            out _))
        {
            return;
        }

        _ = await cache.GetOrCreateAsync(
            ArtifactKey(contentId!, relativePath),
            _ => Task.FromResult(
                new RepositoryVersionedAnalysisArtifact<CSharpEvidenceState>(
                    new CSharpEvidenceState(sourceText, tree, syntaxErrors),
                    RetainedTextBytes(sourceText))),
            cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<bool> TryAdvanceEvidenceAsync(
        IRepositoryFileSystem fileSystem,
        string fullPath,
        string relativePath,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        string? contentId = fileSystem.GetFileMetadata(fullPath).ContentId;
        if (!TryGetChangedLineage(
            fileSystem,
            fullPath,
            contentId,
            out RepositoryVersionedAnalysisCache? cache,
            out RepositoryFileVersion previousVersion) ||
            !cache.TryGetCompleted(
                ArtifactKey(previousVersion.ContentId, relativePath),
                out CSharpEvidenceState previous))
        {
            return false;
        }

        if (previous.SyntaxErrors != 0)
        {
            return false;
        }

        SourceText currentText = CreateSourceText(bytes);
        TextChange change = CreateSingleChange(previous.Text, currentText);
        if (change.Span.Length + change.NewText!.Length > MaximumNeutralChangeCharacters ||
            !IsEvidenceNeutralNumericChange(previous, change, cancellationToken))
        {
            return false;
        }

        _ = await cache.GetOrCreateAsync(
            ArtifactKey(contentId!, relativePath),
            _ => Task.FromResult(
                new RepositoryVersionedAnalysisArtifact<CSharpEvidenceState>(
                    previous with { Text = currentText },
                    RetainedTextBytes(currentText))),
            cancellationToken).ConfigureAwait(false);
        return true;
    }

    internal static string ArtifactKey(string contentId, string relativePath)
    {
        string identity = string.Join('\0', contentId, relativePath);
        string digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))
            .ToLowerInvariant();
        return $"dotnet-csharp-evidence/{DotNetEvidence.AnalyzerVersion}/{digest}";
    }

    private static bool TryGetChangedLineage(
        IRepositoryFileSystem fileSystem,
        string fullPath,
        string? contentId,
        out RepositoryVersionedAnalysisCache cache,
        out RepositoryFileVersion previousVersion)
    {
        if (contentId is not null &&
            fileSystem is IRepositoryVersionedAnalysisProvider
            {
                VersionedAnalysisCache: { } availableCache,
            } provider &&
            provider.TryGetPreviousFileVersion(fullPath, out previousVersion) &&
            !string.Equals(previousVersion.ContentId, contentId, StringComparison.Ordinal))
        {
            cache = availableCache;
            return true;
        }

        cache = null!;
        previousVersion = default;
        return false;
    }

    private static TextChange CreateSingleChange(SourceText before, SourceText after)
    {
        int prefix = 0;
        int maximumPrefix = Math.Min(before.Length, after.Length);
        while (prefix < maximumPrefix && before[prefix] == after[prefix])
        {
            prefix++;
        }

        int beforeEnd = before.Length;
        int afterEnd = after.Length;
        while (beforeEnd > prefix &&
            afterEnd > prefix &&
            before[beforeEnd - 1] == after[afterEnd - 1])
        {
            beforeEnd--;
            afterEnd--;
        }

        return new TextChange(
            TextSpan.FromBounds(prefix, beforeEnd),
            after.ToString(TextSpan.FromBounds(prefix, afterEnd)));
    }

    private static bool IsEvidenceNeutralNumericChange(
        CSharpEvidenceState previous,
        TextChange change,
        CancellationToken cancellationToken)
    {
        if (change.Span.IsEmpty || string.IsNullOrEmpty(change.NewText) ||
            change.NewText.Contains('\r') || change.NewText.Contains('\n'))
        {
            return false;
        }

        CompilationUnitSyntax templateRoot =
            (CompilationUnitSyntax)previous.TemplateTree.GetRoot(cancellationToken);
        SyntaxToken templateToken = templateRoot.FindToken(change.Span.Start);
        if (!templateToken.IsKind(SyntaxKind.NumericLiteralToken) ||
            !templateToken.Span.Contains(change.Span))
        {
            return false;
        }

        string previousTokenText = previous.Text.ToString(templateToken.Span);
        SyntaxToken previousToken = SyntaxFactory.ParseToken(previousTokenText);
        if (!IsExactNumericToken(previousToken, previousTokenText))
        {
            return false;
        }

        int relativeStart = change.Span.Start - templateToken.SpanStart;
        string currentTokenText = string.Concat(
            previousTokenText.AsSpan(0, relativeStart),
            change.NewText.AsSpan(),
            previousTokenText.AsSpan(relativeStart + change.Span.Length));
        SyntaxToken currentToken = SyntaxFactory.ParseToken(currentTokenText);
        return IsExactNumericToken(currentToken, currentTokenText);
    }

    private static bool IsExactNumericToken(SyntaxToken token, string text) =>
        token.IsKind(SyntaxKind.NumericLiteralToken) &&
        !token.ContainsDiagnostics &&
        token.FullSpan.Length == text.Length;

    private static long RetainedTextBytes(SourceText sourceText) =>
        checked((long)sourceText.Length * sizeof(char));
}

internal sealed record CSharpEvidenceState(
    SourceText Text,
    CSharpSyntaxTree TemplateTree,
    int SyntaxErrors);
