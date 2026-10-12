using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using LensDocsStudio.Core.Workspace;

namespace LensDocsStudio.Core.Parsing;

public sealed class WikiLinkInline : ContainerInline
{
    public string Target { get; init; } = "";
    public string Label { get; init; } = "";
    public SourceSpan TargetSpan { get; init; }
}

public static class WikiLinks
{
    public static IReadOnlyList<WikiLinkInline> Read(string source) => new MarkdownService().Parse(source).Syntax.Descendants<WikiLinkInline>().ToArray();
    internal static void FlattenNestedLinks(MarkdownDocument document)
    {
        foreach (var wiki in document.Descendants<WikiLinkInline>().ToArray())
            for (var parent = wiki.Parent; parent is not null; parent = parent.Parent)
                if (parent is LinkInline)
                {
                    wiki.ReplaceBy(new LiteralInline(wiki.Label) { Span = wiki.Span, Line = wiki.Line, Column = wiki.Column }, false);
                    break;
                }
    }
}

internal sealed class WikiLinkInlineParser : InlineParser
{
    public WikiLinkInlineParser() { OpeningCharacters = ['[']; }
    public override bool Match(InlineProcessor processor, ref StringSlice slice)
    {
        if (slice.PeekChar() != '[') return false;
        var start = slice.Start; var contentStart = start + 2;
        var close = slice.Text.IndexOf("]]", contentStart, Math.Min(4096, Math.Max(0, slice.End - contentStart + 1)), StringComparison.Ordinal);
        if (close <= contentStart) return false;
        var content = slice.Text[contentStart..close];
        if (content.IndexOfAny(['\r', '\n', '[', ']']) >= 0) return false;
        var separator = content.IndexOf('|'); var targetSource = separator < 0 ? content : content[..separator];
        var target = targetSource.Trim(); if (target.Length == 0) return false;
        var label = separator < 0 ? target : content[(separator + 1)..].Trim(); if (label.Length == 0) label = target;
        var targetStart = contentStart + targetSource.Length - targetSource.TrimStart().Length;
        var inline = new WikiLinkInline
        {
            Target = target, Label = label, IsClosed = true,
            Span = new(processor.GetSourcePosition(start, out var line, out var column), processor.GetSourcePosition(close + 1)),
            TargetSpan = new(processor.GetSourcePosition(targetStart), processor.GetSourcePosition(targetStart + target.Length - 1)),
            Line = line, Column = column
        };
        inline.AppendChild(new LiteralInline(label) { Span = inline.Span, Line = line, Column = column });
        processor.Inline = inline; slice.Start = close + 2; return true;
    }
}

public sealed record WikiLinkResolution(string? Path, string Fragment, IReadOnlyList<string> Candidates, string? Error)
{
    public bool Found => Path is not null;
}

/// <summary>Resolves only a supplied, bounded catalogue of workspace-relative documents.</summary>
public sealed class WikiLinkIndex
{
    private readonly string[] paths;
    public IReadOnlyList<string> Paths => Array.AsReadOnly(paths);
    public WikiLinkIndex(IEnumerable<string> documents)
    {
        var supplied = documents.Take(20001).ToArray();
        if (supplied.Length > 20000) throw new IOException("Wikilink lookup is limited to 20,000 documents.");
        paths = supplied.Select(NormalisePath).Where(path => path is not null && WorkspaceService.IsDocument(path))
            .Select(path => path!).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToArray();
    }
    public WikiLinkResolution Resolve(string target, string currentPath)
    {
        target = target.Trim().Replace('\\', '/');
        if (target.Length > 4096 || target.Any(char.IsControl) || target.Contains(':') || target.Contains('?') || target.StartsWith('/'))
            return new(null, "", [], "This wikilink is not a workspace document path.");
        var parts = target.Split('#', 2); var reference = Uri.UnescapeDataString(parts[0]);
        if (reference.Any(char.IsControl) || reference.Contains(':') || reference.Contains('?') || reference.StartsWith('/') || reference.StartsWith('\\'))
            return new(null, "", [], "This wikilink is not a workspace document path.");
        reference = reference.Replace('\\', '/');
        var fragment = parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : "";
        if (fragment.Any(char.IsControl)) return new(null, "", [], "The wikilink section is invalid.");
        var current = NormalisePath(currentPath);
        if (current is null) return new(null, fragment, [], "The source document is outside this workspace.");
        if (reference.Length == 0) return paths.Contains(current, StringComparer.OrdinalIgnoreCase)
            ? new(paths.First(path => path.Equals(current, StringComparison.OrdinalIgnoreCase)), fragment, [], null)
            : new(null, fragment, [], "The source document is not in this workspace.");
        var directory = current.Contains('/') ? current[..(current.LastIndexOf('/') + 1)] : "";
        var keys = new[] { NormalisePath(reference), NormalisePath(directory + reference) }.Where(key => key is not null).Distinct().ToArray();
        foreach (var comparison in new[] { StringComparison.Ordinal, StringComparison.OrdinalIgnoreCase })
        foreach (var key in keys)
        {
            var matches = paths.Where(path => path.Equals(key, comparison) || WithoutExtension(path).Equals(key, comparison)).ToArray();
            if (matches.Length > 0) return Select(matches, fragment);
        }
        if (!reference.Contains('/'))
        {
            foreach (var comparison in new[] { StringComparison.Ordinal, StringComparison.OrdinalIgnoreCase })
            {
                var matches = paths.Where(path => System.IO.Path.GetFileName(path).Equals(reference, comparison)
                    || WithoutExtension(System.IO.Path.GetFileName(path)).Equals(reference, comparison)).ToArray();
                if (matches.Length > 0) return Select(matches, fragment);
            }
        }
        return new(null, fragment, [], "No workspace document matches this wikilink.");
    }
    private static WikiLinkResolution Select(string[] matches, string fragment) => matches.Length == 1
        ? new(matches[0], fragment, matches, null) : new(null, fragment, matches, "This wikilink is ambiguous. Include its folder and extension.");
    public static string WithoutExtension(string path) => WorkspaceService.IsDocument(path) ? path[..^System.IO.Path.GetExtension(path).Length] : path;
    internal static string? NormalisePath(string path)
    {
        path = path.Trim().Replace('\\', '/');
        if (path.Length is 0 or > 4096 || path.StartsWith('/') || path.Contains(':') || path.Contains('?') || path.Any(char.IsControl)) return null;
        var parts = new List<string>();
        foreach (var part in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;
            if (part == "..") { if (parts.Count == 0) return null; parts.RemoveAt(parts.Count - 1); continue; }
            if (part.EndsWith(' ') || part.EndsWith('.') || part.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0) return null;
            parts.Add(part);
        }
        return parts.Count == 0 ? null : string.Join('/', parts);
    }
}
