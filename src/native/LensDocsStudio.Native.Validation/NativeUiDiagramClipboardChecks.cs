using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using Markdig.Syntax;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    // Called only by the guarded, actual desktop suite on disposable hosted Windows.
    private static void CheckDiagramImageCopy(Window window, Window drawingWindow, AutomationElement editor, string source, string output,
        UIA3Automation automation, int processId)
    {
        var original = ReadEditorText(editor); var saved = File.ReadAllBytes(source);
        var parsed = new MarkdownService().Parse(original);
        var code = parsed.Syntax.Descendants<FencedCodeBlock>().Single(block => block.Info is "mermaid" or "mmd");
        var themeHelp = Find(window, "DiagramPreview" + code.Span.Start).Properties.HelpText.Value;
        const string prefix = "Diagram theme: ";
        if (!themeHelp.StartsWith(prefix, StringComparison.Ordinal)) throw new IOException("The copied diagram has no resolved palette.");
        var theme = themeHelp[prefix.Length..];
        var expected = StaWorker.RunAsync(() =>
        {
            var diagram = new DiagramService().Render(code.Lines.ToString(), theme: theme);
            if (!diagram.Success) throw new IOException("The independent clipboard reference diagram did not render.");
            return NativeSvgService.ToPng(new NativeSvgService().Render(diagram.Svg!));
        }).GetAwaiter().GetResult();
        using var input = new MemoryStream(expected);
        var expectedBitmap = NativeRasterService.Read(input);
        var expectedPixels = DiagramPixels(expectedBitmap);
        var clipboard = NativeUiClipboard.GetDataObject(); uint owned = 0;
        var copies = new List<object>();
        try
        {
            foreach (var zoom in new[] { 175, 35 })
            {
                Find(drawingWindow, "DiagramZoom").Patterns.RangeValue.Pattern.SetValue(zoom);
                Wait(() => Find(drawingWindow, "DiagramZoomLabel").Name == zoom + "%", "The diagram zoom did not reach the copy fixture scale.");
                NativeUiClipboard.SetText("Diagram image copy sentinel"); owned = GetClipboardSequenceNumber();
                var copy = Find(drawingWindow, "DiagramCopyImage");
                FocusWindow(drawingWindow); copy.Focus();
                Wait(() => copy.Properties.HasKeyboardFocus.Value, "Copy image cannot receive keyboard focus.");
                Keyboard.Type(VirtualKeyShort.SPACE);
                Wait(() => NativeUiClipboard.GetImage() is not null
                    && Find(drawingWindow, "DiagramCopyStatus").Name == "Diagram image copied to the Windows clipboard.",
                    "Copy image did not put the drawing on the Windows clipboard.");
                owned = GetClipboardSequenceNumber();
                var image = NativeUiClipboard.GetImage() ?? throw new IOException("The copied diagram disappeared from the clipboard.");
                CheckPixels(image);
                copies.Add(new { Zoom = zoom, image.PixelWidth, image.PixelHeight, ExactPixels = true,
                    PixelSha256 = Convert.ToHexString(SHA256.HashData(DiagramPixels(image))),
                    Formats = NativeUiClipboard.GetDataObject()!.GetFormats(false) });
            }
            drawingWindow.Close();
            Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(processId).And(cf.ByAutomationId("DiagramWindow"))) is null,
                "The diagram window did not close before checking retained clipboard pixels.");
            var retained = NativeUiClipboard.GetImage() ?? throw new IOException("Closing the drawing window lost the copied diagram.");
            CheckPixels(retained);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(retained));
            using (var destination = File.Create(Path.Combine(output, "native-diagram-clipboard.png"))) encoder.Save(destination);
            if (ReadEditorText(editor) != original || !File.ReadAllBytes(source).SequenceEqual(saved))
                throw new IOException("Diagram image copying changed edited or saved Markdown.");
            File.WriteAllText(Path.Combine(output, "native-ui-diagram-clipboard.json"), JsonSerializer.Serialize(new
            {
                Passed = true, Theme = theme, Copies = copies, RetainedAfterWindowClose = true, SourcePreserved = true,
                Limitation = "Actual cross-process Windows clipboard pixels; external Office and Azure DevOps paste destinations are separate."
            }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally { RestoreClipboard(clipboard, owned); }

        void CheckPixels(BitmapSource image)
        {
            var actualPixels = DiagramPixels(image);
            if (image.PixelWidth != expectedBitmap.PixelWidth || image.PixelHeight != expectedBitmap.PixelHeight
                || !actualPixels.SequenceEqual(expectedPixels))
            {
                File.WriteAllBytes(Path.Combine(output, "native-diagram-clipboard-reference.png"), expected);
                var imageEncoder = new PngBitmapEncoder(); imageEncoder.Frames.Add(BitmapFrame.Create(image));
                using (var destination = File.Create(Path.Combine(output, "native-diagram-clipboard-mismatch.png"))) imageEncoder.Save(destination);
                File.WriteAllText(Path.Combine(output, "native-ui-diagram-clipboard-failure.json"), JsonSerializer.Serialize(new
                {
                    ExpectedWidth = expectedBitmap.PixelWidth, ExpectedHeight = expectedBitmap.PixelHeight,
                    ActualWidth = image.PixelWidth, ActualHeight = image.PixelHeight,
                    ExpectedPixelSha256 = Convert.ToHexString(SHA256.HashData(expectedPixels)),
                    ActualPixelSha256 = Convert.ToHexString(SHA256.HashData(actualPixels)),
                    DifferentBytes = expectedPixels.Length == actualPixels.Length ? expectedPixels.Zip(actualPixels).Count(pair => pair.First != pair.Second) : -1
                }, new JsonSerializerOptions { WriteIndented = true }));
                throw new IOException("Copied diagram pixels differ from the independent native rendering, or zoom changed the clipboard resolution.");
            }
        }
    }

    private static byte[] DiagramPixels(BitmapSource image)
    {
        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[checked(converted.PixelWidth * converted.PixelHeight * 4)];
        converted.CopyPixels(pixels, converted.PixelWidth * 4, 0); return pixels;
    }
}
