using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static class NativePreviewInputSafetyChecks
{
    // Each hostile/complex input runs in a separate disposable process; native termination cannot be caught by managed code.
    public static void Run(string output, string fixture)
    {
        Directory.CreateDirectory(output);
        var prefix = fixture switch
        {
            "combining" => "a" + new string('\u0301', 65536),
            "malformed" => "**Unclosed Markdown [link](\n\nUnpaired source: \uD800 / \uDC00\n",
            "ordinary" => "# Café 😀\n\n**é** and Ελληνικά with [link](#final-marker).\n",
            _ => throw new ArgumentException("Choose a known input safety fixture.")
        };
        var source = prefix + "\n\n# Final marker\n\nComplete retained content.\n";
        var bytes = MemoryMarshal.AsBytes(source.AsSpan()).ToArray();
        File.WriteAllBytes(Path.Combine(output, "source-utf16.bin"), bytes);
        var parsed = new MarkdownService().Parse(source);
        var result = new NativeMarkdownRenderer(new NativeSvgService()).Render(parsed, false,
            new Dictionary<FencedCodeBlock, DiagramResult>(), _ => { }, (_, _) => { });
        var originalPreviewText = result.ReadText();
        if (parsed.Source != source || !originalPreviewText.Contains("Complete retained content.", StringComparison.Ordinal)
            || fixture == "combining" && !originalPreviewText.Contains(prefix, StringComparison.Ordinal))
            throw new IOException("Production preview construction lost source or distant content.");
        Write("BEFORE native layout", false);
        var viewer = new DocumentPreview();
        viewer.SetPreview(result, source.Length, "Continuous");
        using var host = new HwndSource(new HwndSourceParameters("Isolated production input safety")
            { Width = 640, Height = 300, WindowStyle = unchecked((int)0x80000000) });
        host.RootVisual = viewer;
        viewer.Measure(new Size(640, 300)); viewer.Arrange(new Rect(0, 0, 640, 300)); viewer.UpdateLayout();
        if (parsed.Source != source || result.ReadText() != originalPreviewText
            || viewer.ViewingMode != FlowDocumentReaderViewingMode.Scroll)
            throw new IOException("Production layout changed the complete input or explicit reading choice.");
        Write("AFTER native layout", true);
        host.RootVisual = null;
        void Write(string stage, bool completed) => File.WriteAllText(Path.Combine(output, "input-safety.json"), JsonSerializer.Serialize(new
        {
            fixture, stage, completed, sourceCharacters = source.Length, sourceSha256 = Convert.ToHexString(SHA256.HashData(bytes)),
            sourcePreserved = parsed.Source == source, productionViewer = "FlowDocument", experimentalViewerIncluded = false,
            limitation = "One isolated hidden-HWND fixture process on hosted Windows; no universal Unicode or resource certification."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
