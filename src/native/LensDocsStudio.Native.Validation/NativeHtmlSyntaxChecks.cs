using System.Text.Json;
using AngleSharp.Html.Parser;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Exports;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.Native.Validation;

internal static class NativeHtmlSyntaxChecks
{
    public static void Run(string output)
    {
        const string code = "/* First comment\r\n   Comment continues */\r\npublic string Describe()\r\n{\r\n\tvar label = \"Café <img src=x onerror=run()> 😀\";\r\n    return label + 42;\r\n}";
        const string fallback = "    PlainFallbackToken  retains\tspaces and tabs.";
        var source = "```csharp\r\n" + code + "\r\n```\r\n\r\n```unknown-language\r\n" + fallback + "\r\n```";
        var preparation = new ExportPreparationService(new MarkdownService(), new DiagramService(), new NativeSvgService());
        var content = preparation.Prepare(source, null, null, false);
        var html = new HtmlExporter().Export(content);
        File.WriteAllText(Path.Combine(output, "native-html-syntax.html"), html);
        var clipboard = new ClipboardExporter().Export(content);
        File.WriteAllText(Path.Combine(output, "native-html-syntax-clipboard.html"), HtmlClipboardFormat.Decode(clipboard.Html));
        foreach (var markup in new[] { html, HtmlClipboardFormat.Decode(clipboard.Html) })
        {
            var document = new HtmlParser().ParseDocument(markup);
            var blocks = document.QuerySelectorAll("pre code");
            if (blocks.Length != 2 || blocks[0].TextContent != code.Replace("\r\n", "\n") || blocks[1].TextContent != fallback)
                throw new IOException("HTML syntax colouring lost exact Unicode, indentation, tabs or line breaks.");
            foreach (var (role, colour) in new[] { ("keyword", "5B21B6"), ("comment", "466147"), ("string", "166534"), ("number", "92400E") })
            {
                var spans = document.QuerySelectorAll(".code-syntax-" + role);
                if (spans.Length == 0 || spans.Any(span => span.GetAttribute("style") != "color: #" + colour + ";"))
                    throw new IOException("HTML syntax colour was lost for " + role + ".");
            }
            if (blocks[1].Children.Length != 0 || document.QuerySelectorAll("script,img,[onerror]").Length != 0)
                throw new IOException("HTML code became active markup or the unknown-language fallback changed.");
        }
        var largeCode = string.Join('\n', Enumerable.Range(0, 800).Select(i => $"// BudgetRow_{i:D4} with complete retained content and spacing."));
        var largeSource = "```csharp\n" + largeCode + "\n```";
        var large = preparation.Prepare(largeSource, null, null, false);
        var largeHtml = new HtmlExporter().Export(large);
        File.WriteAllText(Path.Combine(output, "native-html-syntax-budget.html"), largeHtml);
        var largeBlock = new HtmlParser().ParseDocument(largeHtml).QuerySelector("pre code")!;
        if (largeBlock.TextContent != largeCode || largeBlock.Children.Length != 0) throw new IOException("HTML syntax budget fallback lost complete plain code.");
        if (content.Document.Source != source || large.Document.Source != largeSource) throw new IOException("HTML syntax colouring changed Markdown source.");
        File.WriteAllText(Path.Combine(output, "native-html-syntax.json"), JsonSerializer.Serialize(new { passed = 5, fallbackRows = 800, checks = new[]
        {
            "Actual native preparation preserves fixed token colours in HTML and decoded CF_HTML",
            "Unicode, indentation, tabs and line breaks remain exact",
            "Code stays inert text and unknown languages stay plain",
            "Over-budget code retains all 800 complete plain rows",
            "Markdown source remains unchanged"
        } }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
