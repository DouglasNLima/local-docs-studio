using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.Core.Content;

public sealed record HelpTopic(string Title, string Anchor, string Source);

public static class NativeHelpGuide
{
    public static IReadOnlyList<HelpTopic> Topics { get; } = Read();
    public static IReadOnlyList<HelpTopic> Search(string? query)
    {
        query = (query ?? "").Trim();
        if (query.Length > 256) query = query[..256];
        return query.Length == 0 ? Topics : Topics.Where(topic => topic.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || topic.Source.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToArray();
    }
    private static IReadOnlyList<HelpTopic> Read()
    {
        using var stream = typeof(NativeHelpGuide).Assembly.GetManifestResourceStream("Content.native-help.md")
            ?? throw new InvalidOperationException("The bundled native guide is missing.");
        using var reader = new StreamReader(stream); var source = reader.ReadToEnd();
        var headings = new MarkdownService().Parse(source).Headings.Where(heading => heading.Level == 2).ToArray();
        return Array.AsReadOnly(headings.Select((heading, index) => new HelpTopic(heading.Title, heading.Anchor,
            source[heading.SourceStart..(index + 1 < headings.Length ? headings[index + 1].SourceStart : source.Length)].TrimEnd())).ToArray());
    }
}
