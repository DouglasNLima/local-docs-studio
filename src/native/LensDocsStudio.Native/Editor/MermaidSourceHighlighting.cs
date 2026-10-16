using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using System.Windows.Media;

namespace LensDocsStudio.Native.Editor;

public sealed partial class MermaidSourceHighlighting(bool dark) : DocumentColorizingTransformer
{
    public bool Dark { get; set; } = dark;
    protected override void ColorizeLine(DocumentLine line)
    {
        if (System.Windows.SystemParameters.HighContrast || line.Length > 4096) return;
        var text = CurrentContext.Document.GetText(line);
        try
        {
            foreach (Match match in Tokens().Matches(text).Cast<Match>().Take(256))
                ChangeLinePart(line.Offset + match.Index, line.Offset + match.Index + match.Length,
                    visual => visual.TextRunProperties.SetForegroundBrush(new SolidColorBrush((Color)ColorConverter.ConvertFromString(
                        match.Value.StartsWith("%%", StringComparison.Ordinal) ? Dark ? "#9BA8A5" : "#567065" : Dark ? "#9CC4EF" : "#245585"))));
        }
        catch (RegexMatchTimeoutException) { }
    }
    [GeneratedRegex("%%.*$|\\b(?:flowchart|graph|subgraph|end|direction|classDef|class|style)\\b|\"[^\"]*\"|-->|-\\.->|==>|<-->", RegexOptions.None, 25)]
    private static partial Regex Tokens();
}
