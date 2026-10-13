using System.Net;
using System.Text;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;

namespace ContinuousViewer.Model;

public sealed record SelectionPayload(string UnicodeText, string Rtf, string CfHtml, string Markdown);
public sealed class LogicalSelection(LogicalDocument document)
{
    public LogicalDocument Document { get; } = document;
    public int Anchor { get; private set; }
    public int Focus { get; private set; }
    public int Start => Math.Min(Anchor, Focus);
    public int End => Math.Max(Anchor, Focus);
    public event EventHandler? Changed;
    public void Set(int anchor, int focus)
    {
        Anchor = Document.Snap(anchor); Focus = Document.Snap(focus, focus >= anchor); Changed?.Invoke(this, EventArgs.Empty);
    }
    public void Move(int direction, bool extend)
    {
        var next = Document.Step(Focus, direction); Set(extend ? Anchor : next, next);
    }
    public SelectionPayload Copy()
    {
        var plain = Document.Text[Start..End]; var html = new StringBuilder("<div>");
        var rtf = new StringBuilder("{\\rtf1\\ansi\\deff0\\uc1 "); var markdown = new StringBuilder();
        foreach (var piece in Document.Pieces.Where(p => p.End > Start && p.Start < End))
        {
            var cursor = Math.Max(Start, piece.Start); var limit = Math.Min(End, piece.End);
            foreach (var run in piece.Runs.Where(r => r.Start + r.Text.Length > cursor && r.Start < limit))
            {
                if (run.Start > cursor) Append(Document.Text[cursor..Math.Min(run.Start, limit)], MarkdownEmphasis.None, null, false);
                var begin = Math.Max(cursor, run.Start); var stop = Math.Min(limit, run.Start + run.Text.Length);
                Append(Document.Text[begin..stop], run.Style, run.Link, run.Code); cursor = stop;
            }
            if (cursor < limit) Append(Document.Text[cursor..limit], MarkdownEmphasis.None, null, false);
        }
        html.Append("</div>"); rtf.Append('}');
        return new(plain, rtf.ToString(), HtmlClipboardFormat.Encode(html.ToString()), markdown.ToString());

        void Append(string value, MarkdownEmphasis style, string? link, bool code)
        {
            var encoded = WebUtility.HtmlEncode(value).Replace("\n", "<br>", StringComparison.Ordinal).Replace("\t", "&#9;", StringComparison.Ordinal);
            var bold = style.HasFlag(MarkdownEmphasis.Bold); var italic = style.HasFlag(MarkdownEmphasis.Italic);
            if (bold) encoded = "<strong>" + encoded + "</strong>";
            if (italic) encoded = "<em>" + encoded + "</em>";
            if (code) encoded = "<code>" + encoded + "</code>";
            if (link is not null && (link.StartsWith('#') || Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" or "mailto"))
                encoded = "<a href=\"" + WebUtility.HtmlEncode(link) + "\">" + encoded + "</a>";
            html.Append(encoded); rtf.Append('{');
            if (bold) rtf.Append("\\b "); if (italic) rtf.Append("\\i ");
            if (style.HasFlag(MarkdownEmphasis.Strike)) rtf.Append("\\strike ");
            if (style.HasFlag(MarkdownEmphasis.Underline)) rtf.Append("\\ul ");
            foreach (var character in value)
                if (character == '\n') rtf.Append("\\line "); else if (character == '\t') rtf.Append("\\tab ");
                else if (character is '\\' or '{' or '}') rtf.Append('\\').Append(character);
                else if (character > 127) rtf.Append("\\u").Append((short)character).Append('?'); else rtf.Append(character);
            rtf.Append('}');
            var escaped = value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("*", "\\*", StringComparison.Ordinal)
                .Replace("[", "\\[", StringComparison.Ordinal).Replace("]", "\\]", StringComparison.Ordinal);
            if (code) escaped = "`` " + value + " ``";
            if (italic) escaped = "*" + escaped + "*"; if (bold) escaped = "**" + escaped + "**";
            markdown.Append(escaped);
        }
    }
}
