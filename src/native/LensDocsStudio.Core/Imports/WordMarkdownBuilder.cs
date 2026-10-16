using System.Text;

namespace LensDocsStudio.Core.Imports;

// Package limits cannot bound repeated relationship targets or generated Markdown.
// Check before each append, including intermediate runs, links and table cells.
internal sealed class WordMarkdownBuilder(int maximumCharacters)
{
    private readonly StringBuilder text = new();
    public int Length => text.Length;

    public WordMarkdownBuilder Append(string? value) => Append(value.AsSpan());
    public WordMarkdownBuilder Append(ReadOnlySpan<char> value)
    {
        Check(value.Length); text.Append(value); return this;
    }
    public WordMarkdownBuilder Append(WordMarkdownBuilder value)
    {
        Check(value.Length); text.Append(value.text); return this;
    }
    public WordMarkdownBuilder Append(char value, int count = 1)
    {
        Check(count); text.Append(value, count); return this;
    }
    public WordMarkdownBuilder AppendLine(string? value = null) => Append(value).Append('\n');
    public WordMarkdownBuilder AppendLine(ReadOnlySpan<char> value) => Append(value).Append('\n');

    public WordMarkdownBuilder AppendEscaped(string value, CancellationToken cancellationToken)
    {
        // Bound temporary entity expansion as well as the retained result.
        for (var offset = 0; offset < value.Length; offset += 4096)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Append(HtmlMarkdownConverter.Escape(value.Substring(offset, Math.Min(4096, value.Length - offset))));
        }
        return this;
    }

    public void RemoveLastNewLine() => text.Length--;
    public override string ToString() => text.ToString();
    private void Check(int count)
    {
        if (count > maximumCharacters - text.Length)
            throw new InvalidDataException("The Word document exceeds the imported text limit.");
    }
}
