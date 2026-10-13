using System.Windows.Media;
using ContinuousViewer.Model;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace ContinuousViewer;

public sealed class DocumentLoader : IDisposable
{
    private CancellationTokenSource? pending;
    private int revision;
    public async Task<bool> LoadAsync(VirtualDocumentView viewer, string source, string? path = null, string? root = null)
    {
        var request = ++revision; pending?.Cancel(); pending?.Dispose(); var cancellation = pending = new();
        var token = cancellation.Token;
        try
        {
            var model = await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                var parsed = new MarkdownService().Parse(source); // Markdig's synchronous parse itself is not interruptible.
                token.ThrowIfCancellationRequested(); return LogicalDocument.Build(parsed, token);
            }, token);
            var media = await PrepareMedia(model, path, root, token);
            if (request != revision || token.IsCancellationRequested) return false;
            viewer.SetDocument(model, media); return true;
        }
        catch (OperationCanceledException) { return false; }
    }
    public static Task<IReadOnlyDictionary<int, ImageSource>> PrepareMedia(LogicalDocument model, string? path, string? root, CancellationToken cancellation = default)
        => StaWorker.RunAsync<IReadOnlyDictionary<int, ImageSource>>(() =>
        {
            var output = new Dictionary<int, ImageSource>();
            var images = new PreviewImageService().Prepare(model.Parsed, path, root, cancellation);
            var maths = new NativeMathService().Prepare(model.Parsed, false, cancellation);
            foreach (var piece in model.Pieces)
            {
                cancellation.ThrowIfCancellationRequested();
                if (piece.Kind == PieceKind.Image && piece.Resource is { } reference && images.TryGetValue(reference, out var image) && image.Pixels is { } pixels)
                    output[piece.Id] = pixels;
                else if (piece.Kind == PieceKind.Maths && maths.TryGetValue(piece.SourceStart, out var formula) && formula.Drawing is { } drawing)
                    output[piece.Id] = drawing;
                else if (piece.Kind == PieceKind.Diagram && piece.Resource is { } code)
                {
                    var diagram = new DiagramService().Render(code);
                    if (diagram.Success) output[piece.Id] = new NativeSvgService().Render(diagram.Svg!);
                }
            }
            return output;
        }, cancellation);
    public void Dispose() { revision++; pending?.Cancel(); pending?.Dispose(); pending = null; }
}
