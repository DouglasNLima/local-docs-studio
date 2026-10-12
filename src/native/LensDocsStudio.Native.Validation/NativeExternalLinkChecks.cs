using System.Text.Json;
using System.Windows;
using System.Windows.Documents;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;
using UglyToad.PdfPig.Actions;

namespace LensDocsStudio.Native.Validation;

/// <summary>Inspects native controls and PDF bytes without opening links or accessing the desktop.</summary>
internal static class NativeExternalLinkChecks
{
    public static void Run(string output)
    {
        const string source = "# External links\n\n"
            + "[Café **reference**](https://example.org/review?q=one&mode=two#result)\n\n"
            + "[HTTP reference](http://example.org/path)\n\n"
            + "[Email reviewer](mailto:review@example.org?subject=Review)\n\n"
            + "<https://example.org/automatic>\n\n<review@example.org>\n\n"
            + "[Local label](file:///C:/private/readme.md) [Active label](javascript:example) [Relative label](../document.md)\n\n"
            + "<file:///C:/private/readme.md> <javascript:example>\n\n"
            + "[" + "A wrapped external reference with enough words to continue across the next line and retain its clickable destination throughout the complete label." + "](https://example.org/wrapped)";
        var parsed = new MarkdownService().Parse(source);
        var checks = new List<string>();
        foreach (var dark in new[] { false, true })
        {
            var invoked = new List<string>();
            var rendered = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, dark, new Dictionary<FencedCodeBlock, DiagramResult>(), invoked.Add, (_, _) => { });
            var text = new TextRange(rendered.Document.ContentStart, rendered.Document.ContentEnd).Text;
            foreach (var label in new[] { "https://example.org/automatic", "review@example.org", "file:///C:/private/readme.md", "javascript:example" })
                if (!text.Contains(label, StringComparison.Ordinal)) throw new IOException("Native preview lost an autolink label: " + label);
            var links = rendered.Document.Blocks.OfType<Paragraph>().SelectMany(p => p.Inlines.OfType<Hyperlink>()).ToArray();
            var automatic = links.Single(l => System.Windows.Automation.AutomationProperties.GetName(l) == "https://example.org/automatic");
            var email = links.Single(l => System.Windows.Automation.AutomationProperties.GetName(l) == "review@example.org");
            automatic.RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));
            email.RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));
            if (!invoked.SequenceEqual(new[] { "https://example.org/automatic", "mailto:review@example.org" }))
                throw new IOException("Autolink activation lost its exact navigation reference.");
            checks.Add((dark ? "Dark" : "Light") + " preview retains autolink text, names and navigation callbacks");
        }
        var content = new ExportContent(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>());
        var bytes = new PdfExporter().Export(content);
        File.WriteAllBytes(Path.Combine(output, "native-external-links.pdf"), bytes);
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
        var annotations = pdf.GetPages().SelectMany(p => p.GetAnnotations()).ToArray();
        var destinations = annotations.Select(a => a.Action).OfType<UriAction>().Select(a => a.Uri).ToArray();
        var expected = new[] { "https://example.org/review?q=one&mode=two#result", "http://example.org/path",
            "mailto:review@example.org?subject=Review", "https://example.org/automatic", "mailto:review@example.org", "https://example.org/wrapped" };
        if (!destinations.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal)))
            throw new IOException("PDF external link destinations differ: " + string.Join(", ", destinations));
        if (annotations.Any(a => a.Action is not UriAction)) throw new IOException("PDF generated an unexpected action for an external link.");
        if (annotations.Any(a => a.Rectangle.Width <= 0 || a.Rectangle.Height <= 0)) throw new IOException("A PDF hyperlink has an empty clickable rectangle.");
        checks.Add("Actual PDF annotations retain HTTP, HTTPS, email, query and fragment destinations");
        if (destinations.Count(d => d == "https://example.org/wrapped") < 2) throw new IOException("The wrapped PDF link did not preserve each clickable line.");
        checks.Add("Wrapped PDF links preserve multiple clickable lines");
        var pdfText = string.Join(" ", pdf.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text));
        foreach (var label in new[] { "Café reference", "https://example.org/automatic", "review@example.org", "Local label", "Active label", "Relative label", "file:///C:/private/readme.md", "javascript:example" })
            if (!pdfText.Contains(label, StringComparison.Ordinal)) throw new IOException("PDF lost a selectable link label: " + label);
        checks.Add("Allowed and unsupported link labels remain selectable without unsafe PDF actions");
        if (parsed.Source != source) throw new IOException("Link export changed Markdown source.");
        checks.Add("Markdown source remains unchanged");
        File.WriteAllText(Path.Combine(output, "native-external-links.json"), JsonSerializer.Serialize(new { passed = checks.Count, destinations, checks }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
