using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace LensDocsStudio.Core.SourceAuthoring;

/// <summary>A declaration occurrence addresses its original tokens; effective values never replace earlier declarations.</summary>
public sealed record SourceDeclaration(string Identity, string Name, string Value, Match Tokens);

public static class SourceDeclarations
{
    public static Match Header(SourceLine line, string keyword, bool showData = true) => SourceText.Pattern(
        @"^[ \t]*" + keyword + (showData ? @"(?:[ \t]+showData)?" : "") + @"(?:[ \t]+title[ \t]+(?<label>.+?))?[ \t]*$", RegexOptions.IgnoreCase).Match(line.Body);
    public static SourceText SetTitle(SourceText text, string header, SourceDeclaration? title, string value)
    {
        SourceText.Label(value);
        return title is null ? text.Insert(text.Index(header) + 1, "    title " + value) : text.Patch(title.Identity, title.Tokens.Groups["label"], value);
    }
    public static void PlainLabel(string value, string delimiters)
    {
        SourceText.Label(value);
        if (value.IndexOfAny(delimiters.ToCharArray()) >= 0)
            throw new ArgumentException("This name contains a grammar separator (" + delimiters + "). The pinned renderer has no supported escape for this field; remove the separator. Pending input is retained.");
    }
    public static XElement[] Groups(string svg, string className) => XDocument.Parse(svg).Descendants()
        .Where(element => ((string?)element.Attribute("class") ?? "").Split(' ').Contains(className)).ToArray();
    // SVG wrapping divides a label into tspans. Compare complete visible text, ignoring only layout whitespace.
    public static string VisibleText(XElement element) => string.Concat(element.Descendants().Where(e => e.Name.LocalName == "text")
        .SelectMany(e => e.Value).Where(c => !char.IsWhiteSpace(c)));
    public static string TextValue(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
    public static void RejectDirectiveName(string label, string directives)
    {
        if (label.StartsWith("%%", StringComparison.Ordinal) || SourceText.Pattern(@"^(?:" + directives + @")(?:\s|$)", RegexOptions.IgnoreCase).IsMatch(label))
            throw new ArgumentException("This name would be interpreted as a comment or directive. Choose a different name; pending input is retained.");
    }
}
