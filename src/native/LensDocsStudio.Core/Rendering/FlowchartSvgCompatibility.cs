using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Mermaider.Models;

namespace LensDocsStudio.Core.Rendering;

/// <summary>Retains two group style properties omitted by the pinned SVG writer.
/// Layout, routing, labels and all node/edge geometry remain renderer-owned.</summary>
internal static partial class FlowchartSvgCompatibility
{
    public static string Apply(string svg, PositionedGraph? graph, string? warning)
    {
        if (graph is null) return svg;
        var styled = Groups(graph.Groups).Where(group => group.InlineStyle is { } style
            && (style.ContainsKey("color") || style.ContainsKey("stroke-dasharray"))).ToArray();
        if (styled.Length == 0 && warning is null) return svg;
        using var reader = XmlReader.Create(new StringReader(svg), new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 8_000_000 });
        var document = XDocument.Load(reader); var root = document.Root!; var ns = root.Name.Namespace;
        foreach (var group in styled)
        {
            var container = root.Descendants(ns + "g").SingleOrDefault(element => (string?)element.Attribute("data-id") == group.Id
                && ((string?)element.Attribute("class"))?.Split(' ').Contains("subgraph") == true);
            var box = container?.Elements(ns + "rect").FirstOrDefault();
            if (box is null) throw new NotSupportedException("The native subgraph style target could not be verified; original source is retained.");
            if (group.InlineStyle!.TryGetValue("stroke-dasharray", out var dash))
            {
                if (dash.Length > 96 || !Dash().IsMatch(dash)) throw new NotSupportedException("Use a bounded numeric stroke-dasharray for a native subgraph.");
                box.SetAttributeValue("stroke-dasharray", dash);
            }
            if (group.InlineStyle.TryGetValue("color", out var colour))
            {
                if (!Colour().IsMatch(colour)) throw new NotSupportedException("Use a hexadecimal or named colour for a native subgraph title.");
                // The pinned renderer emits headers as root text, separately from
                // their data-id group. Verify both its label and owned coordinates.
                var title = root.Elements(ns + "text").SingleOrDefault(element => element.Value == group.Label
                    && Number(element, "x") is { } x && Math.Abs(x - group.X - 12) < .01
                    && Number(element, "y") is { } y && y >= group.Y && y <= group.Y + 30);
                if (title is null) throw new NotSupportedException("This subgraph title's styling cannot be associated safely. Use a single-line native subgraph title.");
                title.SetAttributeValue("fill", colour); title.SetAttributeValue("data-subgraph-id", group.Id);
            }
        }
        if (warning is not null) root.AddFirst(new XElement(ns + "desc", warning));
        return document.ToString(SaveOptions.DisableFormatting);
    }
    private static IEnumerable<PositionedGroup> Groups(IEnumerable<PositionedGroup> groups)
    { foreach (var group in groups) { yield return group; foreach (var child in Groups(group.Children)) yield return child; } }
    private static double? Number(XElement element, string attribute) => double.TryParse((string?)element.Attribute(attribute), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
    [GeneratedRegex(@"^(?:none|\d{1,4}(?:\.\d{1,3})?(?:[ ,]+\d{1,4}(?:\.\d{1,3})?){0,15})$", RegexOptions.CultureInvariant, 50)] private static partial Regex Dash();
    [GeneratedRegex(@"^(?:#[0-9a-fA-F]{3}|#[0-9a-fA-F]{6}|#[0-9a-fA-F]{8}|[a-zA-Z]{1,32})$", RegexOptions.CultureInvariant, 50)] private static partial Regex Colour();
}
