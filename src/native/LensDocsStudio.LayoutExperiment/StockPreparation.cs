using System.Windows.Media;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.LayoutExperiment;

internal sealed record StockPreparation(IReadOnlyDictionary<FencedCodeBlock, DiagramResult> Diagrams,
    IReadOnlyDictionary<FencedCodeBlock, DrawingImage> Drawings, IReadOnlyDictionary<string, PreviewImage> Images, PreparedCodeSyntax Syntax)
{
    public static StockPreparation Create(ParsedDocument parsed, string path, string root, bool dark = false)
    {
        var diagrams = new Dictionary<FencedCodeBlock, DiagramResult>(); var drawings = new Dictionary<FencedCodeBlock, DrawingImage>();
        var service = new DiagramService(); var svg = new NativeSvgService();
        foreach (var code in parsed.Syntax.Descendants<FencedCodeBlock>().Where(code => code.Info == "mermaid"))
        {
            var result = service.Render(code.Lines.ToString(), dark); diagrams.Add(code, result);
            if (result.Svg is not null) { var drawing = svg.Render(result.Svg); drawing.Freeze(); drawings.Add(code, drawing); }
        }
        return new(diagrams, drawings, new PreviewImageService().Prepare(parsed, path, root), CodeSyntaxService.Prepare(parsed));
    }
}
