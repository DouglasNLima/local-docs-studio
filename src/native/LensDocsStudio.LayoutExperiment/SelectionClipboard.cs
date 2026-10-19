using System.Globalization;
using System.Net;
using System.Text;
using System.Windows;
using System.Xml.Linq;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;

namespace LensDocsStudio.LayoutExperiment;

/// <summary>Serialises the selected semantic range without a candidate FlowDocument/viewer.</summary>
internal static class SelectionClipboard
{
    internal sealed record Part(string Text, StyledRun? Run, double Size = 15);
    internal sealed record Payload(string Text, string Rtf, string Xaml);
    public static Payload Prepare(ListModel model, int start, int end)
    {
        var parts = Parts(model, start, end).ToArray(); var plain = string.Concat(parts.Select(part => part.Text));
        var section = new XElement(Wpf + "Section", new XAttribute("FontFamily", "Segoe UI"), new XAttribute("FontSize", "15"));
        foreach (var child in model.Root.Children) if (Xaml(child, model, start, end) is { } value) section.Add(value);
        return new(plain, Rtf(parts), section.ToString(SaveOptions.DisableFormatting));
    }
    public static DataObject Create(Payload payload)
    {
        var data = new DataObject(); data.SetData(DataFormats.UnicodeText, payload.Text); data.SetData(DataFormats.Text, payload.Text);
        data.SetData(DataFormats.Rtf, payload.Rtf); data.SetData(DataFormats.Xaml, payload.Xaml);
        return data;
    }
    private static readonly XNamespace Wpf = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    public static IEnumerable<Part> Parts(ListModel model, int start, int end)
    {
        var markers = new Dictionary<int, string>();
        foreach (var list in model.Root.Descendants().Where(node => node.Kind == "List"))
        {
            for (var index = 0; index < list.Children.Length; index++)
            {
                var item = list.Children[index];
                markers[item.Start] = list.Ordered ? (list.Number + index).ToString(CultureInfo.InvariantCulture) + ".\t" : "•\t";
            }
        }
        var paragraphNodes = model.Root.Descendants().Where(node => node.Paragraph >= 0).ToDictionary(node => node.Paragraph);
        for (var itemIndex = 0; itemIndex < model.Items.Length; itemIndex++)
        {
            var item = model.Items[itemIndex];
            if (item.Start > end || item.Start + item.Length + 2 < start) continue;
            if (item.Start >= start && item.Start <= end && markers.TryGetValue(item.Start, out var marker)) yield return new(marker, null);
            var node = paragraphNodes[itemIndex];
            var size = node.Kind == "Code" ? 13 : node.Kind == "Heading" ? node.Level switch { 1 => 30, 2 => 24, 3 => 20, _ => 17 } : 15;
            foreach (var run in item.Runs)
            {
                var left = Math.Max(start, run.Start); var right = Math.Min(end, run.Start + run.Text.Length);
                if (right > left) yield return new(model.Text[left..right], run, size);
            }
            var separatorStart = item.Start + item.Length;
            var next = itemIndex + 1;
            var separatorEnd = next < model.Items.Length ? model.Items[next].Start : model.Text.Length;
            var a = Math.Max(start, separatorStart); var b = Math.Min(end, separatorEnd);
            if (b > a) yield return new(model.Text[a..b], null);
        }
    }
    private static string Rtf(IEnumerable<Part> parts)
    {
        var result = new StringBuilder(@"{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}{\f1 Consolas;}}\uc1 ");
        foreach (var part in parts)
        {
            result.Append(@"\f").Append(part.Run?.Code == true ? 1 : 0).Append(@"\fs").Append((int)(part.Size * 1.5))
                .Append(part.Run?.Bold == true ? @"\b " : @"\b0 ").Append(part.Run?.Italic == true ? @"\i " : @"\i0 ");
            foreach (var character in part.Text)
            {
                if (character == '\r') continue;
                if (character == '\n') result.Append(@"\par ");
                else if (character == '\t') result.Append(@"\tab ");
                else if (character is '\\' or '{' or '}') result.Append('\\').Append(character);
                else if (character > 127) result.Append(@"\u").Append((short)character).Append('?');
                else result.Append(character);
            }
        }
        return result.Append('}').ToString();
    }
    private static XElement? Xaml(SemanticNode node, ListModel model, int start, int end)
    {
        if (node.End <= start || node.Start >= end) return null;
        if (node.Paragraph >= 0)
        {
            var paragraph = new XElement(Wpf + "Paragraph");
            foreach (var run in model.Items[node.Paragraph].Runs)
            {
                var left = Math.Max(start, run.Start); var right = Math.Min(end, run.Start + run.Text.Length); if (right <= left) continue;
                var value = new XElement(Wpf + "Run", new XAttribute("Text", model.Text[left..right]));
                if (run.Bold) value.SetAttributeValue("FontWeight", "Bold"); if (run.Italic) value.SetAttributeValue("FontStyle", "Italic");
                if (run.Code) value.SetAttributeValue("FontFamily", "Consolas");
                paragraph.Add(value);
            }
            return paragraph;
        }
        var kind = node.Kind switch { "List" => "List", "ListItem" => "ListItem", "Table" => "Table", "Row" => "TableRow", "Cell" => "TableCell", _ => "Section" };
        var result = new XElement(Wpf + kind);
        if (node.Kind == "List") { result.SetAttributeValue("MarkerStyle", node.Ordered ? "Decimal" : "Disc"); result.SetAttributeValue("StartIndex", Math.Max(1, node.Number)); }
        var children = node.Children.Select(child => Xaml(child, model, start, end)).Where(child => child is not null);
        if (node.Kind == "Table") result.Add(new XElement(Wpf + "TableRowGroup", children)); else result.Add(children);
        return result;
    }
}
