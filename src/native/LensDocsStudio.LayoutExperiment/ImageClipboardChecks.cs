using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.LayoutExperiment;

internal static class ImageClipboardChecks
{
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    public static async Task Run(ExperimentWindow window, string output)
    {
        _ = LensDocsStudio.Native.Validation.ContinuousPreviewFixtures.Create(output, "mixed");
        const string source = "Before ![Pixel](pixel.png) after.\n";
        var path = Path.Combine(output, "fixture.md"); File.WriteAllText(path, source);
        var gaps = new List<double>(); var timeline = new List<object>(); var last = Stopwatch.GetTimestamp(); var phase = "stock";
        var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) => { var now = Stopwatch.GetTimestamp(); var gap = (now - last) * 1000d / Stopwatch.Frequency;
            timeline.Add(new { Phase = phase, StartTimestamp = last, EndTimestamp = now, GapMilliseconds = gap }); if (phase == "candidate") gaps.Add(gap); last = now; }; timer.Start();
        try
        {
            foreach (var side in new[] { "stock", "candidate" })
            {
                phase = side; var folder = Path.Combine(output, side); Directory.CreateDirectory(folder); FlowDocumentScrollViewer? viewer = null;
                if (side == "stock")
                {
                    var parsed = new MarkdownService().Parse(source); var prepared = await ExperimentWindow.Worker(() => StockPreparation.Create(parsed, path, output));
                    var stock = await new NativeMarkdownRenderer(new NativeSvgService()).RenderAsync(parsed, false, prepared.Diagrams, _ => { }, (_, _) => { }, preparedDrawings: prepared.Drawings, preparedImages: prepared.Images, codeSyntax: prepared.Syntax);
                    AutomationProperties.SetAutomationId(stock.Document, "ImageClipboardOracle");
                    viewer = new FlowDocumentScrollViewer { Width = 640, Height = 300, IsToolBarVisible = false, Document = stock.Document }; window.Content = viewer;
                }
                else { window.Content = window.Scroller; window.SourcePath = path; window.WorkspaceRoot = output; await window.Request(source); }
                window.UpdateLayout(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
                using var acknowledgement = new InputAcknowledgement(window);
                var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
                foreach (var argument in new[] { "--image-clipboard-client", folder, Environment.ProcessId.ToString(), side, acknowledgement.Name }) start.ArgumentList.Add(argument);
                using var client = (await Task.Run(() => Process.Start(start)))!;
                try
                {
                    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30)); await client.WaitForExitAsync(deadline.Token);
                    if (client.ExitCode != 0) throw new IOException("Independent image-only Copy failed: " + side);
                }
                finally { if (!client.HasExited) client.Kill(true); if (viewer is not null) viewer.Document = null; }
            }
            phase = "diagnostic-comparison";
            var stockBytes = File.ReadAllBytes(Path.Combine(output, "stock", "bitmap-pixels.bin"));
            var candidateBytes = File.ReadAllBytes(Path.Combine(output, "candidate", "bitmap-pixels.bin"));
            var stockFormats = File.ReadAllText(Path.Combine(output, "stock", "formats.json"));
            var candidateFormats = File.ReadAllText(Path.Combine(output, "candidate", "formats.json"));
            using var stockReceipt = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "stock", "image-clipboard-client.json")));
            using var candidateReceipt = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "candidate", "image-clipboard-client.json")));
            var dimensionsAndTextEqual = new[] { "PixelWidth", "PixelHeight", "PlainText" }.All(name => stockReceipt.RootElement.GetProperty(name).GetRawText() == candidateReceipt.RootElement.GetProperty(name).GetRawText());
            if (!stockBytes.SequenceEqual(candidateBytes) || stockFormats != candidateFormats || !dimensionsAndTextEqual) throw new IOException("Actual selected-image clipboard formats, text, dimensions or pixels differ from stock.");
            Program.Write(Path.Combine(output, "image-clipboard.json"), new { Status = "PASS", FormatsAndDecodedBitmapPixels = "PASS", MaximumInputGapMilliseconds = gaps.DefaultIfEmpty().Max(), CandidateInputGapsMilliseconds = gaps, Timeline = timeline,
                InteractionStages = window.Surface.InteractionStages,
                Oracle = "Actual shown stock and candidate TextPattern image selection plus real Ctrl+C in separate clients; the stock viewer is detached before candidate setup" });
        }
        catch (Exception error)
        { Program.Write(Path.Combine(output, "image-clipboard.json"), new { Status = "FAIL", Error = error.ToString(), MaximumInputGapMilliseconds = gaps.DefaultIfEmpty().Max(), CandidateInputGapsMilliseconds = gaps, Timeline = timeline, InteractionStages = window.Surface.InteractionStages }); throw; }
        finally { timer.Stop(); }
    }
    public static int Client(string output, int processId, string side, string acknowledgementName)
    {
        try
        {
            using var acknowledgement = new InputAcknowledgement(acknowledgementName); using var clipboard = new IndependentUiaClient.ClipboardOwner();
            using var process = Process.GetProcessById(processId); SetForegroundWindow(process.MainWindowHandle);
            var window = AutomationElement.FromHandle(process.MainWindowHandle);
            var document = window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, side == "stock" ? "ImageClipboardOracle" : "ExperimentalDocument"))!;
            var text = (TextPattern)document.GetCurrentPattern(TextPattern.Pattern);
            // Select the embedded character through the actual text range. Stock's
            // bare framework viewer does not consistently surface an image child.
            var range = text.DocumentRange.FindText("Before ", false, false) ?? throw new IOException("Image prefix missing.");
            range.MoveEndpointByRange(System.Windows.Automation.Text.TextPatternRangeEndpoint.Start, range, System.Windows.Automation.Text.TextPatternRangeEndpoint.End);
            range.MoveEndpointByUnit(System.Windows.Automation.Text.TextPatternRangeEndpoint.End, System.Windows.Automation.Text.TextUnit.Character, 1);
            range.ScrollIntoView(true); range.Select(); document.SetFocus();
            clipboard.SetSentinel("Image-only Copy sentinel");
            IndependentUiaClient.ChordObserved(acknowledgement, process.MainWindowHandle, 0x11, 0x43);
            IndependentUiaClient.Wait(Clipboard.ContainsImage);
            var data = Clipboard.GetDataObject()!; var formats = data.GetFormats(false).Order(StringComparer.Ordinal).ToArray();
            var beforeBitmapText = Clipboard.GetText(); var unicode = data.GetData(DataFormats.UnicodeText, false); var ansi = data.GetData(DataFormats.Text, false);
            var bitmap = new FormatConvertedBitmap(Clipboard.GetImage()!, PixelFormats.Pbgra32, null, 0);
            var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4]; bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
            File.WriteAllBytes(Path.Combine(output, "bitmap-pixels.bin"), pixels); Program.Write(Path.Combine(output, "formats.json"), formats);
            Program.Write(Path.Combine(output, "image-clipboard-client.json"), new { Status = "PASS", ClientProcess = Environment.ProcessId, ProviderProcess = processId, Transport = "Independent Windows UI Automation and real Ctrl+C", Formats = formats, bitmap.PixelWidth, bitmap.PixelHeight, PixelSha256 = Program.Hash(pixels),
                PlainText = unicode as string, UnicodeType = unicode?.GetType().FullName, AnsiText = ansi as string, AnsiType = ansi?.GetType().FullName,
                TextBeforeBitmap = beforeBitmapText, TextAfterBitmap = Clipboard.GetText(), RangeText = range.GetText(-1) }); return 0;
        }
        catch (Exception error) { Program.Write(Path.Combine(output, "image-clipboard-client.json"), new { Status = "FAIL", Error = error.ToString() }); return 1; }
    }
}
