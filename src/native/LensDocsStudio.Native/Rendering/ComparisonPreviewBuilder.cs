using System.Windows.Media;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Platform;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Rendering;

// The second view uses the same parsing, diagram, image, maths and native
// construction services as the main reader. The caller owns cancellation and
// validates the document/version/theme before attaching a completed result.
internal sealed class ComparisonPreviewBuilder(MarkdownService markdown, DiagramService diagrams, NativeSvgService svg,
    PreviewImageService images, NativeMathService maths)
{
    internal sealed record Prepared(ParsedDocument Parsed, Dictionary<FencedCodeBlock, DiagramResult> Diagrams,
        PreparedCodeSyntax Syntax, Dictionary<FencedCodeBlock, DrawingImage> Drawings,
        IReadOnlyDictionary<string, PreviewImage> Images, IReadOnlyDictionary<int, PreviewFormula> Formulae);
    public async Task<Prepared> PrepareAsync(string source, bool standalone, bool dark, string theme,
        string? path, string? workspace, CancellationToken cancellation)
    {
        var parsed = await Task.Run(() =>
        {
            var document = markdown.Parse(source, standalone);
            var results = new Dictionary<FencedCodeBlock, DiagramResult>();
            foreach (var block in document.Syntax.Descendants<FencedCodeBlock>())
            {
                cancellation.ThrowIfCancellationRequested();
                if (block.Info is "mermaid" or "mmd") results[block] = diagrams.Render(block.Lines.ToString(), dark, theme, cancellation);
            }
            return (document, results, syntax: CodeSyntaxService.Prepare(document, cancellation));
        }, cancellation);
        return await StaWorker.RunAsync(() =>
        {
            var drawings = new Dictionary<FencedCodeBlock, DrawingImage>();
            foreach (var (block, diagram) in parsed.results.ToArray())
            {
                cancellation.ThrowIfCancellationRequested();
                if (!diagram.Success) continue;
                try { drawings[block] = svg.Render(diagram.Svg!, cancellation); }
                catch (Exception error) when (error is not OutOfMemoryException && error is not OperationCanceledException)
                { parsed.results[block] = new(diagram.Source, null, "Native diagram rendering failed: " + error.Message) { FailureKind = DiagramFailureKind.NativeConversion }; }
            }
            return new Prepared(parsed.document, parsed.results, parsed.syntax, drawings,
                images.Prepare(parsed.document, path, workspace, cancellation), maths.Prepare(parsed.document, dark, cancellation));
        }, cancellation);
    }
}
