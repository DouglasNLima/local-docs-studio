using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace LensDocsStudio.LayoutExperiment;

/// <summary>A separate process uses the OS UIA transport and real Windows input.</summary>
internal static class IndependentUiaClient
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string title);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] private static extern bool SetPhysicalCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
    internal sealed class ClipboardOwner : IDisposable
    {
        private readonly System.Windows.Threading.Dispatcher dispatcher;
        private readonly Thread thread;
        public int ThreadId => thread.ManagedThreadId;
        public ClipboardOwner()
        {
            var ready = new TaskCompletionSource<System.Windows.Threading.Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
            thread = new Thread(() => { ready.SetResult(System.Windows.Threading.Dispatcher.CurrentDispatcher); System.Windows.Threading.Dispatcher.Run(); }) { IsBackground = true, Name = "Independent client clipboard owner STA" };
            thread.SetApartmentState(ApartmentState.STA); thread.Start(); dispatcher = ready.Task.GetAwaiter().GetResult();
        }
        public void SetSentinel(string text) => dispatcher.Invoke(() => Clipboard.SetText(text));
        public void Dispose() { dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Background); thread.Join(1000); }
    }
    private static void Key(byte key, bool up = false) => keybd_event(key, 0, (up ? 2u : 0u) | (key is >= 0x21 and <= 0x28 ? 1u : 0u), UIntPtr.Zero);
    internal static void Chord(params byte[] keys)
    {
        // Hold each real key long enough for the target input queue to observe
        // modifier state. This models key holds, not application readiness.
        foreach (var key in keys) { Key(key); Thread.Sleep(30); }
        foreach (var key in keys.Reverse()) { Key(key, true); Thread.Sleep(30); }
    }
    internal static void ChordObserved(InputAcknowledgement acknowledgement, IntPtr window, params byte[] keys)
    {
        // Modifiers remain physically held until the target has delivered Input.
        // A UIA read after key release alone cannot establish that ordering.
        try
        {
            foreach (var key in keys) { Key(key); Thread.Sleep(30); }
            acknowledgement.AfterRealInput(window);
        }
        finally { foreach (var key in keys.Reverse()) { Key(key, true); Thread.Sleep(30); } }
        acknowledgement.AfterRealInput(window);
    }
    public static int Run(string output, int processId, string expectedHash, string scenario, string acknowledgementName)
    {
        using var acknowledgement = new InputAcknowledgement(acknowledgementName);
        SetThreadDpiAwarenessContext(new IntPtr(-4)); // UIA rectangles are physical screen coordinates.
        using var clipboardOwner = new ClipboardOwner();
        var checks = new List<string>(); var stages = new List<object>();
        void Trace(string stage) { stages.Add(new { Stage = stage, Timestamp = Stopwatch.GetTimestamp() }); Program.Write(Path.Combine(output, "uia-client-stages.json"), stages); }
        try
        {
            using var target = Process.GetProcessById(processId);
            Wait(() => { target.Refresh(); return target.MainWindowHandle != IntPtr.Zero; });
            var window = AutomationElement.FromHandle(target.MainWindowHandle);
            var document = window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "ExperimentalDocument"))
                ?? throw new IOException("Real document AutomationElement missing.");
            var text = (TextPattern)document.GetCurrentPattern(TextPattern.Pattern);
            Trace("document-text");
            var actual = text.DocumentRange.GetText(-1);
            Require(Program.Hash(Encoding.UTF8.GetBytes(actual)) == expectedHash, "Complete document text through independent UIA", checks);
            var names = scenario switch
            {
                "list" => ("word1", "word2", "word4000"), "table" => ("Row", "Row1", "Value1000"),
                "paragraphs" => ("Paragraph1:", "Paragraph2:", "Paragraph1200:"), "quote" => ("Quoted1", "Quoted2", "Quoted1200"),
                "rich" => ("Heading", "Plain", "Final marker."), "unicode-bidi" => ("Unicode", "Latin", "Final marker."),
                "nested-lists" => ("First", "continuation", "After list."), "table-wrap" => ("Left", "first", "After table."),
                "table-spans" => ("AAAAA", "E", "After spanned table."), "table-blocks" => ("First paragraph.", "Rich cell", "After nested table."),
                "collapsed-details" => ("Hidden section", "After fold.", "After fold."),
                "details" => ("Large section", "Hidden1", "Hidden1200"), "mixed" => ("Mixed document", "Deep target", "Mixed final marker."),
                _ => throw new ArgumentException("Unknown UIA scenario.")
            };
            Trace("semantic-tree");
            var lists = document.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.List));
            if (scenario is "list" or "mixed")
            {
                Require(lists.Count == 1, "One real semantic UIA list", checks);
                var items = lists[0].FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
                Require(items.Count == (scenario == "list" ? 4000 : 1000), "Complete semantic UIA item count and order", checks);
                var lastItem = items[items.Count - 1];
                Require(text.RangeFromChild(lastItem).GetText(-1).Contains(scenario == "list" ? "word4000" : "Mixed item1000", StringComparison.Ordinal), "List item/range relationship", checks);
            }
            if (scenario is "table" or "mixed")
            {
                var table = document.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Table)) ?? throw new IOException("Actual UIA table missing.");
                var grid = (GridPattern)table.GetCurrentPattern(GridPattern.Pattern);
                Require(grid.Current.RowCount == (scenario == "table" ? 1001 : 301) && grid.Current.ColumnCount == 2, "Complete table GridPattern geometry", checks);
                var cell = grid.GetItem(grid.Current.RowCount - 1, 1);
                Require(text.RangeFromChild(cell).GetText(-1).Contains(scenario == "table" ? "Value1000" : "Value300", StringComparison.Ordinal), "Cell document range and logical text", checks);
                var cellPattern = (GridItemPattern)cell.GetCurrentPattern(GridItemPattern.Pattern);
                Require(cellPattern.Current.Column == 1 && cellPattern.Current.ContainingGrid == table, "Cell/table relationship", checks);
                Require(((TablePattern)table.GetCurrentPattern(TablePattern.Pattern)).Current.GetColumnHeaders().Length == 2, "Table header relationships", checks);
            }
            if (scenario is "table-spans" or "table-blocks")
            {
                var table = document.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Table))!;
                var grid = (GridPattern)table.GetCurrentPattern(GridPattern.Pattern);
                if (scenario == "table-spans")
                {
                    Require(grid.Current.RowCount == 3 && grid.Current.ColumnCount == 3, "Spanned table complete row/column structure", checks);
                    var wideCell = grid.GetItem(0, 0); var tallCell = grid.GetItem(0, 2); var lowerCell = grid.GetItem(1, 0);
                    Require(grid.GetItem(0, 1) == wideCell && ((GridItemPattern)wideCell.GetCurrentPattern(GridItemPattern.Pattern)).Current.ColumnSpan == 2, "Column span exposes the same containing cell", checks);
                    Require(grid.GetItem(1, 2) == tallCell && grid.GetItem(2, 0) == lowerCell && ((GridItemPattern)tallCell.GetCurrentPattern(GridItemPattern.Pattern)).Current.RowSpan == 2, "Row span exposes correct relationships", checks);
                    Require(text.RangeFromChild(grid.GetItem(2, 2)).GetText(-1).Contains("CCCCC", StringComparison.Ordinal), "Distant spanned cell range", checks);
                }
                else
                {
                    Require(text.RangeFromChild(grid.GetItem(0, 0)).GetText(-1).Contains("First paragraph.\r\nSecond paragraph.", StringComparison.Ordinal), "Multi-paragraph cell text range", checks);
                    Require(grid.GetItem(0, 1).FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.List)) is not null, "Nested list remains a real semantic child of its table cell", checks);
                }
            }
            Trace("distant-range");
            if (scenario == "rich")
            {
                Require((int)text.DocumentRange.FindText("bold", false, false)!.GetAttributeValue(TextPattern.FontWeightAttribute) == 700, "Rich bold attribute", checks);
                Require((bool)text.DocumentRange.FindText("italic", false, false)!.GetAttributeValue(TextPattern.IsItalicAttribute), "Rich italic attribute", checks);
                Require((string)text.DocumentRange.FindText("code", false, false)!.GetAttributeValue(TextPattern.FontNameAttribute) == "Consolas", "Inline code font attribute", checks);
                Require((double)text.DocumentRange.FindText("Heading", false, false)!.GetAttributeValue(TextPattern.FontSizeAttribute) == 18, "Heading font size in points", checks);
            }
            var last = text.DocumentRange.FindText(names.Item3, false, false) ?? throw new IOException("UIA FindText failed.");
            last.Select(); Require(text.GetSelection().Single().GetText(-1) == names.Item3, "UIA selection", checks);
            if (scenario == "list") Require((int)last.GetAttributeValue(TextPattern.FontWeightAttribute) == 700, "Actual bold text attribute", checks);
            last.ScrollIntoView(true);
            Wait(() => last.GetBoundingRectangles().Length > 0);
            var rectangle = last.GetBoundingRectangles()[0];
            Program.Write(Path.Combine(output, "uia-point-geometry.json"), new { Range = rectangle.ToString(), Document = document.Current.BoundingRectangle.ToString(), Window = window.Current.BoundingRectangle.ToString() });
            var point = text.RangeFromPoint(new Point(rectangle.Left + 1, rectangle.Top + rectangle.Height / 2));
            point.ExpandToEnclosingUnit(TextUnit.Word);
            Require(point.GetText(-1).Contains(names.Item3.Split(' ')[0], StringComparison.Ordinal), "UIA point geometry on distant final text", checks);
            var clone = last.Clone(); clone.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, 4);
            Require(clone.GetText(-1) == names.Item3[4..], "UIA character endpoint movement", checks);
            var unitProbe = last.Clone(); unitProbe.MoveEndpointByRange(TextPatternRangeEndpoint.End, last, TextPatternRangeEndpoint.Start);
            unitProbe.ExpandToEnclosingUnit(TextUnit.Paragraph);
            Require(unitProbe.GetText(-1).Contains(names.Item3, StringComparison.Ordinal), "TextUnit.Paragraph expansion", checks);
            var visible = text.GetVisibleRanges();
            Require(visible.Length > 0 && visible.SelectMany(range => range.GetBoundingRectangles()).All(rect => document.Current.BoundingRectangle.Contains(rect)), "Visible range rectangles clipped to actual document viewport", checks);
            if (scenario == "rich")
            {
                Require(text.DocumentRange.FindAttribute(TextPattern.IsItalicAttribute, true, false)?.GetText(-1) == "italic", "Actual FindAttribute for italic text", checks);
                Require((TextDecorationLineStyle)text.DocumentRange.FindText("under", false, false)!.GetAttributeValue(TextPattern.UnderlineStyleAttribute) == TextDecorationLineStyle.Single, "Actual underline attribute", checks);
                Require((TextDecorationLineStyle)text.DocumentRange.FindText("strike", false, false)!.GetAttributeValue(TextPattern.StrikethroughStyleAttribute) == TextDecorationLineStyle.Single, "Actual strikethrough attribute", checks);
            }
            if (scenario is "rich" or "unicode-bidi")
            {
                var cluster = text.DocumentRange.FindText("é", false, false)!.Clone(); cluster.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, -1);
                Require(cluster.GetText(-1) == "", "Actual combining cluster remains one UIA character", checks);
                var emoji = text.DocumentRange.FindText("😀", false, false)!; emoji.MoveEndpointByUnit(TextPatternRangeEndpoint.End, TextUnit.Character, -1);
                Require(emoji.GetText(-1) == "", "Actual surrogate pair remains one UIA character", checks);
                var link = document.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink))!;
                Require(text.RangeFromChild(link).GetText(-1).Contains(scenario == "rich" ? "Deep link" : "rich link", StringComparison.Ordinal), "Real link and range relationship", checks);
                ((InvokePattern)link.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                var navigated = text.GetSelection().Single(); navigated.ExpandToEnclosingUnit(TextUnit.Paragraph);
                Require(navigated.GetText(-1).Contains("Final heading", StringComparison.Ordinal), "Actual internal-link navigation", checks);
            }
            Trace("keyboard");
            var first = text.DocumentRange.FindText(names.Item1, false, false)!; first.ScrollIntoView(true);
            SetForegroundWindow(target.MainWindowHandle); document.SetFocus();
            Wait(() => AutomationElement.FocusedElement.Current.ProcessId == processId);
            var insertion = first.Clone(); insertion.MoveEndpointByRange(TextPatternRangeEndpoint.End, insertion, TextPatternRangeEndpoint.Start); insertion.Select();
            ChordObserved(acknowledgement, target.MainWindowHandle, 0x11, 0x24); // Stock read-only Ctrl+Home scrolls; insertion is explicitly selected above.
            ChordObserved(acknowledgement, target.MainWindowHandle, 0x10, 0x28); // Shift+Down across the first boundary.
            Wait(() => text.GetSelection().Single().GetText(-1).Contains("\r\n", StringComparison.Ordinal));
            checks.Add("Real keyboard extension across items, focus retained");
            Trace("mouse");
            first.ScrollIntoView(true);
            var second = text.DocumentRange.FindText(names.Item2, false, false)!;
            Wait(() => first.GetBoundingRectangles().Length > 0 && second.GetBoundingRectangles().Length > 0);
            var a = first.GetBoundingRectangles()[0]; var b = second.GetBoundingRectangles()[0];
            Program.Write(Path.Combine(output, "mouse-drag-geometry.json"), new { First = a.ToString(), Second = b.ToString(), Document = document.Current.BoundingRectangle.ToString(), Foreground = GetForegroundWindow().ToInt64(), ExpectedWindow = target.MainWindowHandle.ToInt64() });
            Require(GetForegroundWindow() == target.MainWindowHandle && document.Current.BoundingRectangle.Contains(a.TopLeft) && document.Current.BoundingRectangle.Contains(b.BottomRight), "Real foreground and physical drag points remain in the shown document", checks);
            SetPhysicalCursorPos((int)a.Left + 1, (int)(a.Top + a.Height / 2)); mouse_event(2, 0, 0, 0, UIntPtr.Zero);
            acknowledgement.AfterRealInput(target.MainWindowHandle);
            for (var step = 1; step <= 8; step++)
            { SetPhysicalCursorPos((int)(a.Left + (b.Right - a.Left) * step / 8), (int)(a.Top + a.Height / 2 + (b.Top - a.Top) * step / 8)); Thread.Sleep(15); }
            mouse_event(4, 0, 0, 0, UIntPtr.Zero);
            acknowledgement.AfterRealInput(target.MainWindowHandle);
            Wait(() => { var selected = text.GetSelection().Single().GetText(-1); return selected.Contains("\r\n", StringComparison.Ordinal) && selected.Contains(names.Item1, StringComparison.Ordinal) && selected.Contains(names.Item2, StringComparison.Ordinal); });
            checks.Add("Real mouse drag across items");
            Trace("select-all");
            Chord(0x11, 0x41); Wait(() => text.GetSelection().Single().GetText(-1) == actual);
            Trace("copy");
            clipboardOwner.SetSentinel("Layout experiment copy sentinel " + Guid.NewGuid());
            var copySequence = LensDocsStudio.Native.Platform.NativeClipboardService.Sequence;
            Chord(0x11, 0x43); Wait(() => LensDocsStudio.Native.Platform.NativeClipboardService.Sequence != copySequence && Clipboard.ContainsText() && Clipboard.GetText().Contains(names.Item3, StringComparison.Ordinal));
            var copied = Clipboard.GetText();
            var expected = scenario == "list" ? string.Join("\r\n", Enumerable.Range(1, 4000).Select(i => "•\tword" + i)) + "\r\n" : actual + "\r\n";
            File.WriteAllText(Path.Combine(output, "selected-text-before-assertion.txt"), copied); File.WriteAllText(Path.Combine(output, "expected-selected-text.txt"), expected);
            Trace("rtf-decode");
            var rawRtf = Clipboard.GetData(DataFormats.Rtf);
            var rtf = rawRtf is string value ? value : throw new IOException("RTF string payload missing.");
            var decodedDocument = new System.Windows.Documents.FlowDocument();
            var decoded = new System.Windows.Documents.TextRange(decodedDocument.ContentStart, decodedDocument.ContentEnd);
            decoded.Load(new MemoryStream(Encoding.UTF8.GetBytes(rtf)), DataFormats.Rtf);
            File.WriteAllText(Path.Combine(output, "selected-text.txt"), copied); File.WriteAllText(Path.Combine(output, "decoded-rtf-text.txt"), decoded.Text); File.WriteAllText(Path.Combine(output, "selected.rtf"), rtf);
            Trace("stock-serialisation-oracle-after-actual-copy");
            var stockExpected = StockOracle(output, out var nativeInterchange);
            var stockPlain = new System.Windows.Documents.TextRange(nativeInterchange.Document.ContentStart, nativeInterchange.Document.ContentEnd).Text;
            File.WriteAllText(Path.Combine(output, "expected-selected-text.txt"), stockPlain);
            Require(copied == stockPlain, "Exact actual selected marker/tab/line-ending/media serialisation against stock", checks);
            Require(decoded.Text == stockExpected, "RTF actually decoded with stock-compatible logical text (including native interchange transformations)", checks);
            if (scenario == "list") Require(DecodedBlocks(decodedDocument.Blocks).OfType<System.Windows.Documents.Paragraph>().SelectMany(paragraph => DecodedRuns(paragraph.Inlines)).Any(run => run.Text.Contains("word1", StringComparison.Ordinal) && run.FontWeight == FontWeights.Bold), "Decoded RTF actual bold style", checks);
            Trace("xaml-decode");
            Require(Clipboard.ContainsData(DataFormats.Xaml) && System.Xml.Linq.XElement.Parse(Clipboard.GetData(DataFormats.Xaml) as string ?? throw new IOException("Xaml string payload missing.")).Descendants().Any(element => element.Name.LocalName == "Run"), "Actual Xaml selected-range content", checks);
            Require(Clipboard.ContainsData(DataFormats.Rtf), "Actual Unicode/Text/RTF clipboard formats", checks);
            if (scenario == "mixed")
            {
                var bytes = Clipboard.GetData(DataFormats.XamlPackage) switch
                {
                    MemoryStream package => package.ToArray(), byte[] package => package,
                    _ => throw new IOException("Actual XamlPackage bytes missing.")
                };
                using var expectedPackage = new MemoryStream(); new System.Windows.Documents.TextRange(nativeInterchange.Document.ContentStart, nativeInterchange.Document.ContentEnd).Save(expectedPackage, DataFormats.XamlPackage);
                var expectedMedia = InterchangeChecks.Media(InterchangeChecks.Decode(expectedPackage.ToArray(), DataFormats.XamlPackage));
                var actualMedia = InterchangeChecks.Media(InterchangeChecks.Decode(bytes, DataFormats.XamlPackage));
                Program.Write(Path.Combine(output, "actual-xamlpackage-media.json"), new { Stock = expectedMedia, Candidate = actualMedia });
                // The unchanged workload contains twelve inline bitmap images. Public
                // WPF XamlPackage omits the BlockUIContainer diagram in both routes.
                Require(expectedMedia.Length == 12 && System.Text.Json.JsonSerializer.Serialize(expectedMedia) == System.Text.Json.JsonSerializer.Serialize(actualMedia), "Actual clipboard XamlPackage decoded inline-image dimensions and pixels; stock also omits the block diagram", checks);
                Require(!nativeInterchange.Document.ContentStart.HasValidLayout, "Stock interchange oracle remains unformatted after package serialisation", checks);
            }
            Require(AutomationElement.FocusedElement.Current.ProcessId == processId, "Focus retained after copy", checks);
            if (scenario == "rich")
            {
                Trace("selected-rich-range-copy");
                var selected = text.DocumentRange.FindText("bold italic and code & escaped", false, false)!; selected.Select();
                clipboardOwner.SetSentinel("Partial rich-copy sentinel " + Guid.NewGuid()); var sequence = LensDocsStudio.Native.Platform.NativeClipboardService.Sequence;
                Chord(0x11, 0x43); Wait(() => LensDocsStudio.Native.Platform.NativeClipboardService.Sequence != sequence && Clipboard.GetText() == "bold italic and code & escaped");
                var partial = new System.Windows.Documents.FlowDocument(); var range = new System.Windows.Documents.TextRange(partial.ContentStart, partial.ContentEnd);
                range.Load(new MemoryStream(Encoding.UTF8.GetBytes((Clipboard.GetData(DataFormats.Rtf) as string ?? throw new IOException("Partial RTF missing.")))), DataFormats.Rtf);
                var runs = DecodedBlocks(partial.Blocks).OfType<System.Windows.Documents.Paragraph>().SelectMany(paragraph => DecodedRuns(paragraph.Inlines)).ToArray();
                Require(runs.Any(run => run.Text.Contains("bold", StringComparison.Ordinal) && run.FontWeight == FontWeights.Bold)
                    && runs.Any(run => run.Text.Contains("italic", StringComparison.Ordinal) && run.FontStyle == FontStyles.Italic)
                    && runs.Any(run => run.Text.Contains("code", StringComparison.Ordinal) && run.FontFamily.Source == "Consolas"), "Actual cross-run partial copy and decoded bold/italic/code fidelity", checks);
            }
            if (scenario == "list")
            {
                var partial = text.DocumentRange.FindText("rd1\r\nwor", false, false)!;
                var source = File.ReadAllText(Path.Combine(output, "fixture.md"));
                var fromSource = source.IndexOf("word1", StringComparison.Ordinal) + 2; var toSource = source.IndexOf("word2", StringComparison.Ordinal) + 3;
                Require(nativeInterchange.TextMap.TryPreviewRange(fromSource, toSource - fromSource, out var from, out var to), "Stock partial list source mapping", checks);
                var expectedPartial = new System.Windows.Documents.TextRange(from!, to!);
                partial.Select(); clipboardOwner.SetSentinel("Partial cross-item sentinel " + Guid.NewGuid()); var sequence = LensDocsStudio.Native.Platform.NativeClipboardService.Sequence;
                Chord(0x11, 0x43); Wait(() => LensDocsStudio.Native.Platform.NativeClipboardService.Sequence != sequence && Clipboard.GetText().Contains("rd1", StringComparison.Ordinal));
                Require(Clipboard.GetText() == expectedPartial.Text, "Actual partial cross-item marker/tab clipboard text", checks);
                using var stockPartial = new MemoryStream(); expectedPartial.Save(stockPartial, DataFormats.Rtf);
                var actualPartial = InterchangeChecks.Decode(Encoding.UTF8.GetBytes(Clipboard.GetData(DataFormats.Rtf) as string ?? throw new IOException("Partial list RTF missing.")), DataFormats.Rtf);
                var expectedDecoded = InterchangeChecks.Decode(stockPartial.ToArray(), DataFormats.Rtf);
                Require(new System.Windows.Documents.TextRange(actualPartial.ContentStart, actualPartial.ContentEnd).Text == new System.Windows.Documents.TextRange(expectedDecoded.ContentStart, expectedDecoded.ContentEnd).Text
                    && System.Text.Json.JsonSerializer.Serialize(TargetedChecks.Styles(actualPartial)) == System.Text.Json.JsonSerializer.Serialize(TargetedChecks.Styles(expectedDecoded)), "Actual partial cross-item decoded RTF text and styles", checks);
            }
            if (scenario == "mixed")
            {
                Trace("actual-media-actions");
                var media = document.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Mermaid diagram"))!;
                text.RangeFromChild(media).ScrollIntoView(true);
                var commands = media.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)).Cast<AutomationElement>().ToArray();
                Require(commands.Select(command => command.Current.Name).SequenceEqual(new[] { "Zoom", "SVG", "PNG", "Copy source" }), "Actual accessible Mermaid commands", checks);
                ((InvokePattern)commands[0].GetCurrentPattern(InvokePattern.Pattern)).Invoke(); AutomationElement? zoom = null;
                Wait(() => { var handle = FindWindow(null, "EXPERIMENT ONLY — Mermaid zoom"); zoom = handle == IntPtr.Zero ? null : AutomationElement.FromHandle(handle); return zoom is not null && zoom.Current.ProcessId == processId; });
                Require(zoom!.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Image)) is not null, "Real native Mermaid zoom window and image", checks);
                ((WindowPattern)zoom.GetCurrentPattern(WindowPattern.Pattern)).Close();
                clipboardOwner.SetSentinel("Diagram source sentinel"); ((InvokePattern)commands[^1].GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                Wait(() => Clipboard.GetText().Contains("graph", StringComparison.OrdinalIgnoreCase) || Clipboard.GetText().Contains("flowchart", StringComparison.OrdinalIgnoreCase)); checks.Add("Actual Mermaid source copy");
                SetForegroundWindow(target.MainWindowHandle); document.SetFocus();
            }
            Trace("whole-document-html-copy");
            clipboardOwner.SetSentinel("Rendered copy sentinel " + Guid.NewGuid());
            Chord(0x10, 0x79); // Shift+F10 opens the real context menu.
            AutomationElement? renderedCommand = null;
            Wait(() =>
            {
                // The WPF popup is attached to the host's automation tree on this
                // platform. Walk chrome/menus while excluding semantic document subtrees.
                var queue = new Queue<AutomationElement>(); queue.Enqueue(window); var visited = new List<object>();
                while (queue.TryDequeue(out var owner) && visited.Count < 80 && renderedCommand is null)
                {
                    foreach (AutomationElement child in owner.FindAll(TreeScope.Children, System.Windows.Automation.Condition.TrueCondition))
                    {
                        var information = child.Current;
                        visited.Add(new { information.Name, information.AutomationId, ControlType = information.ControlType.ProgrammaticName });
                        if (information.AutomationId == "ExperimentalCopyRendered") { renderedCommand = child; break; }
                        if (!information.AutomationId.StartsWith("Node-", StringComparison.Ordinal)) queue.Enqueue(child);
                    }
                }
                Program.Write(Path.Combine(output, "uia-owned-menu-windows.json"), visited);
                return renderedCommand is not null;
            });
            ((InvokePattern)renderedCommand!.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            Wait(() => Clipboard.ContainsData(DataFormats.Html));
            var html = LensDocsStudio.Core.Exports.HtmlClipboardFormat.Decode(Clipboard.GetData(DataFormats.Html) as string ?? throw new IOException("CF_HTML payload missing."));
            var htmlDocument = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);
            Require(htmlDocument.Body!.TextContent.Contains(names.Item3, StringComparison.Ordinal), "Actual whole-document HTML copy through the existing product exporter", checks);
            if (scenario == "mixed") Require(htmlDocument.QuerySelectorAll("img").Length == 13 && htmlDocument.QuerySelectorAll("img").All(image => image.GetAttribute("src")?.StartsWith("data:image/png", StringComparison.Ordinal) == true), "Whole-document rich copy contains all actual workspace/Mermaid PNG media", checks);
            if (scenario == "list") Require(htmlDocument.QuerySelectorAll("li").Length == 4000 && htmlDocument.QuerySelectorAll("strong").Length == 4000, "Whole-document HTML list structure and style fidelity", checks);
            Trace("complete");
            Program.Write(Path.Combine(output, "independent-uia.json"), new { Status = "PASS", ClientProcess = Environment.ProcessId, ProviderProcess = processId,
                Transport = "Windows UI Automation across process boundary", Checks = checks, PhysicalScreenReader = "NOT CERTIFIED", HtmlClipboard = "PASS: actual whole-document route and CF_HTML decoding" });
            return 0;
        }
        catch (Exception error)
        { Program.Write(Path.Combine(output, "independent-uia.json"), new { Status = "FAIL", Checks = checks, Error = error.ToString() }); return 1; }
        finally { Key(0x11, true); Key(0x10, true); mouse_event(4, 0, 0, 0, UIntPtr.Zero); }
    }
    internal static string StockOracle(string output) => StockOracle(output, out _);
    private static string StockOracle(string output, out LensDocsStudio.Native.Rendering.PreviewResult native)
    {
        // Test oracle stays in this independent process, after actual first copy.
        // It cannot warm the provider's serializer/JIT or supply live UIA/selection.
        var path = Path.Combine(output, "fixture.md"); var source = File.ReadAllText(path);
        var parsed = new LensDocsStudio.Core.Parsing.MarkdownService().Parse(source);
        var prepared = StockPreparation.Create(parsed, path, output);
        native = new LensDocsStudio.Native.Rendering.NativeMarkdownRenderer(new LensDocsStudio.Native.Rendering.NativeSvgService())
            .Render(parsed, false, prepared.Diagrams, _ => { }, (_, _) => { }, preparedDrawings: prepared.Drawings, preparedImages: prepared.Images, codeSyntax: prepared.Syntax);
        using var stream = new MemoryStream(); new System.Windows.Documents.TextRange(native.Document.ContentStart, native.Document.ContentEnd).Save(stream, DataFormats.Rtf); stream.Position = 0;
        var document = new System.Windows.Documents.FlowDocument(); var range = new System.Windows.Documents.TextRange(document.ContentStart, document.ContentEnd); range.Load(stream, DataFormats.Rtf);
        if (native.Document.ContentStart.HasValidLayout) throw new IOException("Independent serialisation oracle unexpectedly acquired layout.");
        File.WriteAllText(Path.Combine(output, "stock-decoded-rtf-oracle.txt"), range.Text); return range.Text;
    }
    private static IEnumerable<System.Windows.Documents.Block> DecodedBlocks(System.Windows.Documents.BlockCollection blocks)
    {
        foreach (var block in blocks)
        {
            yield return block;
            var children = block switch
            {
                System.Windows.Documents.Section section => DecodedBlocks(section.Blocks),
                System.Windows.Documents.List list => list.ListItems.SelectMany(item => DecodedBlocks(item.Blocks)),
                System.Windows.Documents.Table table => table.RowGroups.SelectMany(group => group.Rows).SelectMany(row => row.Cells).SelectMany(cell => DecodedBlocks(cell.Blocks)),
                _ => []
            };
            foreach (var child in children) yield return child;
        }
    }
    private static IEnumerable<System.Windows.Documents.Run> DecodedRuns(System.Windows.Documents.InlineCollection inlines)
    {
        foreach (var inline in inlines) { if (inline is System.Windows.Documents.Run run) yield return run;
            if (inline is System.Windows.Documents.Span span) foreach (var nested in DecodedRuns(span.Inlines)) yield return nested; }
    }
    internal static void Wait(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(15)) { if (condition()) return; Thread.Sleep(25); }
        throw new TimeoutException("The independent UIA/input condition did not complete within 15 seconds.");
    }
    private static void Require(bool value, string name, List<string> checks)
    { if (!value) throw new IOException(name); checks.Add(name); }
}
