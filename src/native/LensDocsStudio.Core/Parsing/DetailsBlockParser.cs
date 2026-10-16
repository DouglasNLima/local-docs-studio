using System.Net;
using System.Text.RegularExpressions;
using Markdig.Parsers;
using Markdig.Syntax;

namespace LensDocsStudio.Core.Parsing;

public sealed class DocumentDetailsBlock(BlockParser parser, string summary, bool initiallyExpanded, int bodyStart, int closingStart, int closingEnd) : ContainerBlock(parser)
{
    public string Summary { get; } = summary;
    public bool InitiallyExpanded { get; } = initiallyExpanded;
    internal int BodyStart { get; } = bodyStart;
    internal int ClosingStart { get; } = closingStart;
    internal int ClosingEnd { get; } = closingEnd;
}

/// <summary>Parses complete passive details sections while letting Markdig retain original child source positions.</summary>
internal sealed partial class DetailsBlockParser : BlockParser
{
    private const int MaximumSectionCharacters = 1024 * 1024;
    private const int MaximumSections = 256;
    private static readonly object RegionsKey = new();
    private sealed record Region(int Start, int BodyStart, int ClosingStart, int ClosingEnd, string Summary, bool Expanded);
    private sealed record Candidate(int Start, int BodyStart, string? Summary, bool Expanded);

    public DetailsBlockParser() { OpeningCharacters = ['<']; }
    public override BlockState TryOpen(BlockProcessor processor)
    {
        if (processor.IsCodeIndent || processor.CurrentContainer is not (MarkdownDocument or DocumentDetailsBlock)
            || !processor.Line.AsSpan().StartsWith("<details", StringComparison.OrdinalIgnoreCase)) return BlockState.None;
        if (processor.Document.GetData(RegionsKey) is not Dictionary<int, Region> regions)
        {
            regions = ReadRegions(processor.Line.Text); processor.Document.SetData(RegionsKey, regions);
        }
        if (!regions.TryGetValue(processor.Start, out var region)) return BlockState.None;
        var details = new DocumentDetailsBlock(this, region.Summary, region.Expanded, region.BodyStart, region.ClosingStart, region.ClosingEnd)
        { Span = new(processor.Start, processor.Line.End), Line = processor.LineIndex, Column = processor.Column };
        processor.NewBlocks.Push(details); return BlockState.ContinueDiscard;
    }
    public override BlockState TryContinue(BlockProcessor processor, Block block)
    {
        var details = (DocumentDetailsBlock)block;
        if (processor.CurrentLineStartPosition >= details.ClosingStart)
        { block.Span.End = details.ClosingEnd; return BlockState.BreakDiscard; }
        block.Span.End = processor.Line.End;
        return processor.CurrentLineStartPosition < details.BodyStart ? BlockState.ContinueDiscard : BlockState.Continue;
    }

    private static Dictionary<int, Region> ReadRegions(string source)
    {
        var regions = new Dictionary<int, Region>(); var stack = new Stack<Candidate>();
        var position = 0; var comment = false; string? rawTag = null; char fence = '\0'; var fenceLength = 0; var candidates = 0;
        while (position < source.Length)
        {
            var lineStart = position; var newline = source.IndexOf('\n', position); var next = newline < 0 ? source.Length : newline + 1;
            var end = newline < 0 ? source.Length : newline; if (end > position && source[end - 1] == '\r') end--;
            var line = source.AsSpan(position, end - position); position = next;
            var indent = 0; while (indent < line.Length && line[indent] == ' ') indent++;
            var trimmed = line[indent..].TrimEnd(" \t");
            if (fence != '\0')
            {
                if (indent <= 3 && trimmed.Length >= fenceLength && trimmed[0] == fence)
                {
                    var count = 0; while (count < trimmed.Length && trimmed[count] == fence) count++;
                    if (count >= fenceLength && trimmed[count..].Trim().IsEmpty) fence = '\0';
                }
                continue;
            }
            if (comment) { if (line.Contains("-->", StringComparison.Ordinal)) comment = false; continue; }
            if (rawTag is not null) { if (line.Contains("</" + rawTag + ">", StringComparison.OrdinalIgnoreCase)) rawTag = null; continue; }
            var commentStart = line.IndexOf("<!--", StringComparison.Ordinal);
            if (commentStart >= 0) { comment = !line[(commentStart + 4)..].Contains("-->", StringComparison.Ordinal); continue; }
            if (indent > 3 || trimmed.IsEmpty) continue;
            if (trimmed[0] is '`' or '~')
            {
                var count = 1; while (count < trimmed.Length && trimmed[count] == trimmed[0]) count++;
                if (count >= 3 && (trimmed[0] != '`' || !trimmed[count..].Contains('`'))) { fence = trimmed[0]; fenceLength = count; }
                continue;
            }
            if (trimmed[0] != '<') continue;
            foreach (var name in new[] { "script", "style", "pre", "textarea" })
                if (trimmed.StartsWith("<" + name, StringComparison.OrdinalIgnoreCase)
                    && trimmed.Length > name.Length + 1 && (char.IsWhiteSpace(trimmed[name.Length + 1]) || trimmed[name.Length + 1] == '>'))
                { if (!trimmed.Contains("</" + name + ">", StringComparison.OrdinalIgnoreCase)) rawTag = name; break; }
            if (rawTag is not null) continue;
            if (trimmed.Equals("</details>", StringComparison.OrdinalIgnoreCase))
            {
                if (stack.TryPop(out var candidate) && candidate.Summary is not null && end - candidate.Start <= MaximumSectionCharacters && regions.Count < MaximumSections)
                    regions.Add(candidate.Start, new(candidate.Start, candidate.BodyStart, lineStart, end - 1, candidate.Summary, candidate.Expanded));
                continue;
            }
            if (!trimmed.StartsWith("<details", StringComparison.OrdinalIgnoreCase) || trimmed.Length < 9 || trimmed[8] is not ('>' or ' ' or '\t')) continue;
            if (++candidates > 4096) break;
            var opening = trimmed.Length <= 1024 ? Opening().Match(trimmed.ToString()) : Match.Empty;
            var attributes = opening.Groups["attribute"].Captures.Select(capture => capture.Value.ToLowerInvariant()).ToArray();
            string? summary = null; var bodyStart = next;
            if (stack.Count < 8 && opening.Success && attributes.Distinct(StringComparer.Ordinal).Count() == attributes.Length && next < source.Length)
            {
                var summaryEnd = source.IndexOf('\n', next); if (summaryEnd < 0) summaryEnd = source.Length;
                if (summaryEnd - next <= 4096)
                {
                    var match = Summary().Match(source[next..summaryEnd].TrimEnd('\r'));
                    if (match.Success)
                    {
                        var text = WebUtility.HtmlDecode(match.Groups[1].Value).Trim();
                        if (text.Length is > 0 and <= 512 && !text.Any(char.IsControl)) { summary = text; bodyStart = Math.Min(source.Length, summaryEnd + 1); }
                    }
                }
            }
            stack.Push(new(lineStart + indent, bodyStart, summary, attributes.Contains("open", StringComparer.Ordinal)));
        }
        return regions;
    }
    [GeneratedRegex("^<details(?:[ \\t]+(?<attribute>data-details-block|open)(?:[ \\t]*=[ \\t]*(?:\"[^\"<>]*\"|'[^'<>]*'))?)*[ \\t]*>$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)]
    private static partial Regex Opening();
    [GeneratedRegex(@"^ {0,3}<summary>([^<\r\n]*)</summary>[ \t]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)]
    private static partial Regex Summary();
}
