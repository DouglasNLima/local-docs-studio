using System.Text.RegularExpressions;
using System.Diagnostics;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Editor;

public sealed partial class MarkdownHighlighting : DocumentColorizingTransformer
{
    public bool Dark { get; set; }
    public bool HighContrast { get; set; }
    public const int MaximumLineCharacters = 4096;

    protected override void ColorizeLine(DocumentLine line)
    {
        if (HighContrast || line.Length > MaximumLineCharacters) return;
        var text = CurrentContext.Document.GetText(line);
        var matches = new List<Match>(); var watch = Stopwatch.StartNew();
        try
        {
            foreach (Match match in Tokens().Matches(text))
            {
                if (matches.Count >= 512 || watch.ElapsedMilliseconds >= 8) return;
                matches.Add(match);
            }
        }
        catch (RegexMatchTimeoutException) { return; }
        // Apply only after the bounded scan succeeds, so fallback uses consistent inherited text.
        foreach (var match in matches)
        {
            var role = match.Value.StartsWith('#') ? CodeTokenRole.Keyword : match.Value.StartsWith('`') ? CodeTokenRole.String : CodeTokenRole.Name;
            var brush = CodeSyntaxColours.For(role, Dark);
            ChangeLinePart(line.Offset + match.Index, line.Offset + match.Index + match.Length, visual => visual.TextRunProperties.SetForegroundBrush(brush));
        }
    }
    [GeneratedRegex(@"^#{1,6}\s.*$|\*\*[^*]+\*\*|`[^`]+`|!?\[[^\]]*\]\([^)]*\)|^```.*$", RegexOptions.None, 25)] private static partial Regex Tokens();
}
