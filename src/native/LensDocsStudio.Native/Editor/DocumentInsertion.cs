using ICSharpCode.AvalonEdit.Document;

namespace LensDocsStudio.Native.Editor;

/// <summary>A pending insertion follows edits without deleting text added while its asset was loading.</summary>
public sealed class DocumentInsertion
{
    private readonly TextDocument document;
    private readonly TextAnchor start;
    private readonly TextAnchor end;
    private readonly string selected;
    public DocumentInsertion(TextDocument document, int offset, int length)
    {
        this.document = document;
        selected = document.GetText(offset, length);
        start = document.CreateAnchor(offset); end = document.CreateAnchor(offset + length);
        start.SurviveDeletion = end.SurviveDeletion = true;
        start.MovementType = AnchorMovementType.BeforeInsertion; end.MovementType = AnchorMovementType.AfterInsertion;
    }
    public int Insert(string text, bool block = false)
    {
        var offset = start.Offset;
        var length = Math.Max(0, end.Offset - offset);
        if (document.GetText(offset, length) != selected) length = 0;
        if (block)
        {
            var before = document.GetText(Math.Max(0, offset - 4), Math.Min(4, offset)).Replace("\r", "");
            var after = document.GetText(offset + length, Math.Min(4, document.TextLength - offset - length)).Replace("\r", "");
            var prefix = offset == 0 || before.EndsWith("\n\n", StringComparison.Ordinal) ? "" : before.EndsWith('\n') ? "\n" : "\n\n";
            var suffix = offset + length == document.TextLength || after.StartsWith("\n\n", StringComparison.Ordinal) ? "" : after.StartsWith('\n') ? "\n" : "\n\n";
            text = prefix + text.Trim('\r', '\n') + suffix;
        }
        document.Replace(offset, length, text);
        return offset + text.Length;
    }
}
