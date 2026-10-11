using System.Text.RegularExpressions;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;

namespace LensDocsStudio.Native.Editor;

public sealed partial class MarkdownHighlighting : DocumentColorizingTransformer
{
    protected override void ColorizeLine(DocumentLine line)
    {
        var text = CurrentContext.Document.GetText(line);
        foreach (Match match in Tokens().Matches(text))
        {
            var colour = match.Value.StartsWith('#') ? Colors.DarkOrange : match.Value.StartsWith('`') ? Colors.SteelBlue : Colors.MediumSeaGreen;
            ChangeLinePart(line.Offset + match.Index, line.Offset + match.Index + match.Length, visual => visual.TextRunProperties.SetForegroundBrush(new SolidColorBrush(colour)));
        }
    }
    [GeneratedRegex(@"^#{1,6}\s.*$|\*\*[^*]+\*\*|`[^`]+`|!?\[[^\]]*\]\([^)]*\)|^```.*$")] private static partial Regex Tokens();
}
