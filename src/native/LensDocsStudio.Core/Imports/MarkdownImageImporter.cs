using System.Text;
using LensDocsStudio.Core.Parsing;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace LensDocsStudio.Core.Imports;

public sealed class MarkdownImageImporter
{
    public ImportedDocument Import(string source, CancellationToken cancellationToken = default)
    {
        if (source.Length > 32 * 1024 * 1024) throw new IOException("Markdown image paste is limited to 32 million source characters.");
        cancellationToken.ThrowIfCancellationRequested();
        var images = new MarkdownService().Parse(source).Syntax.Descendants<LinkInline>()
            .Where(i => i.IsImage && (i.Url ?? "").StartsWith("data:", StringComparison.OrdinalIgnoreCase)).OrderBy(i => i.Span.Start);
        var assets = new Dictionary<string, ImportedAsset>(StringComparer.Ordinal);
        var result = new StringBuilder(); var position = 0; long bytes = 0;
        foreach (var image in images)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (image.Span.Start < position) throw new IOException("Embedded image source positions overlap.");
            if (!assets.TryGetValue(image.Url!, out var asset))
            {
                if (assets.Count >= 128) throw new IOException("Markdown paste is limited to 128 distinct embedded images.");
                var data = EmbeddedImageData.Decode(image.Url!);
                if ((bytes += data.Length) > 32 * 1024 * 1024) throw new IOException("Pasted images exceed the 32 MB aggregate limit.");
                var reference = "assets/images/paste-" + Guid.NewGuid().ToString("N") + ".png";
                assets.Add(image.Url!, asset = new(reference, data, "pasted-image"));
            }
            result.Append(source, position, image.Span.Start - position);
            result.Append(MarkdownImageSource.WithDestination(source, image, asset.Reference));
            position = image.Span.End + 1;
        }
        result.Append(source, position, source.Length - position);
        return new(result.ToString(), assets.Values.ToArray(), []);
    }
}
