using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using LensDocsStudio.Core.Exports;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativeContextChecks
{
    public static void Run(string output)
    {
        const string source = "# Examples\r\n\r\n~~~js\r\nconst label = 'Café <safe>';\r\n  keepIndent();\r\n~~~\r\n\r\n    indented text\r\n      keep spaces\r\n";
        var parsed = new MarkdownService().Parse(source); var copied = new List<PreviewTextCopy>();
        var preview = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, false, new Dictionary<FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { }, copyCode: copied.Add);
        var blocks = parsed.Syntax.Descendants<CodeBlock>().ToArray();
        if (blocks.Length != 2) throw new IOException("The code context fixture did not contain both fenced and indented code.");
        foreach (var block in blocks)
        {
            var element = preview.SourceElements[block.Span.Start];
            if (element.ContextMenu is not { } menu || menu.Items.Count != 2) throw new IOException("The native code block has no copy context menu.");
            foreach (var item in menu.Items.OfType<MenuItem>()) item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        }
        if (copied.Count != 4 || copied[0] != new PreviewTextCopy("const label = 'Café <safe>';\r\n  keepIndent();", false)
            || copied[2] != new PreviewTextCopy("indented text\r\n  keep spaces", false))
            throw new IOException("Copy code lost Unicode, literal syntax or indentation: " + JsonSerializer.Serialize(copied));
        if (copied[1] != new PreviewTextCopy("~~~js\r\nconst label = 'Café <safe>';\r\n  keepIndent();\r\n~~~", true)
            || copied[3] != new PreviewTextCopy("    indented text\r\n      keep spaces", true))
            throw new IOException("Code Markdown copy lost its fences, indentation or source line endings: " + JsonSerializer.Serialize(copied));
        var plain = new TextRange(preview.Document.ContentStart, preview.Document.ContentEnd).Text;
        var html = new HtmlExporter().RenderFragment(new(parsed, new Dictionary<int, ExportImage>(), new Dictionary<string, ExportImage>()));
        if (plain.Contains("Copy code", StringComparison.Ordinal) || html.Contains("Copy code", StringComparison.Ordinal) || parsed.Source != source)
            throw new IOException("Code context actions leaked into document text or changed source.");
        var oversized = "```\n" + new string('x', CodeContextMenu.MaximumCopyCharacters + 1) + "\n```";
        var tooLarge = new MarkdownService().Parse(oversized).Syntax.Descendants<CodeBlock>().Single();
        var guarded = CodeContextMenu.Create(tooLarge, oversized, copied.Add);
        foreach (var item in guarded.Items.OfType<MenuItem>())
        {
            if (item.IsEnabled) throw new IOException("The code copy limit did not disable an oversized action.");
            item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        }
        if (copied.Count != 4) throw new IOException("An oversized code action still dispatched copied data.");
        const string quoted = "> ```cs\r\n> string label = \"Retain context\";\r\n> ```";
        var quotedBlock = new MarkdownService().Parse(quoted).Syntax.Descendants<CodeBlock>().Single();
        var quotedMenu = CodeContextMenu.Create(quotedBlock, quoted, copied.Add);
        ((MenuItem)quotedMenu.Items[1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        if (copied[^1] != new PreviewTextCopy(quoted, true)) throw new IOException("Markdown source copy lost the first line's quote context: " + JsonSerializer.Serialize(copied[^1]));
        File.WriteAllText(Path.Combine(output, "native-context.json"), JsonSerializer.Serialize(new
        {
            Passed = true,
            Checks = new[] { "Code context actions copy literal Unicode code with retained indentation", "Markdown copy retains exact fences, indentation and CRLF source",
                "Context actions stay outside selectable document text and exported HTML", "Oversized copy actions are disabled and cannot dispatch content",
                "Markdown source copy retains the opening quote context for nested code" }
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
