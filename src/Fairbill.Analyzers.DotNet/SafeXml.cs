using System.Xml;
using System.Xml.Linq;
using Fairbill.Analysis;

namespace Fairbill.Analyzers.DotNet;

internal static class SafeXml
{
    public static async Task<XDocument> LoadAsync(
        IRepositoryFileSystem fileSystem,
        string path,
        CancellationToken cancellationToken)
    {
        XmlReaderSettings settings = new()
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            XmlResolver = null,
        };

        await using Stream stream = fileSystem.OpenRead(path, 16 * 1024);
        using XmlReader reader = XmlReader.Create(stream, settings);
        return await XDocument.LoadAsync(
            reader,
            LoadOptions.None,
            cancellationToken).ConfigureAwait(false);
    }
}
