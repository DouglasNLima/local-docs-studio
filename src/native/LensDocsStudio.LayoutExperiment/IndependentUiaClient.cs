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
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
    private static void Key(byte key, bool up = false) => keybd_event(key, 0, (up ? 2u : 0u) | (key is >= 0x21 and <= 0x28 ? 1u : 0u), UIntPtr.Zero);
    private static void Chord(params byte[] keys)
    {
        // Hold each real key long enough for the target input queue to observe
        // modifier state. This models key holds, not application readiness.
        foreach (var key in keys) { Key(key); Thread.Sleep(30); }
        foreach (var key in keys.Reverse()) { Key(key, true); Thread.Sleep(30); }
    }
    public static int Run(string output, int processId, string expectedHash, string scenario)
    {
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
            Trace("distant-range");
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
            Trace("keyboard");
            var first = text.DocumentRange.FindText(names.Item1, false, false)!; first.ScrollIntoView(true);
            SetForegroundWindow(target.MainWindowHandle); document.SetFocus();
            Wait(() => AutomationElement.FocusedElement.Current.ProcessId == processId);
            Chord(0x11, 0x24); // Ctrl+Home
            Chord(0x10, 0x28); // Shift+Down across the first list boundary
            Wait(() => text.GetSelection().Single().GetText(-1).Contains("\r\n", StringComparison.Ordinal));
            checks.Add("Real keyboard extension across items, focus retained");
            Trace("mouse");
            first.ScrollIntoView(true);
            var second = text.DocumentRange.FindText(names.Item2, false, false)!;
            Wait(() => first.GetBoundingRectangles().Length > 0 && second.GetBoundingRectangles().Length > 0);
            var a = first.GetBoundingRectangles()[0]; var b = second.GetBoundingRectangles()[0];
            SetCursorPos((int)a.Left + 1, (int)(a.Top + a.Height / 2)); mouse_event(2, 0, 0, 0, UIntPtr.Zero);
            for (var step = 1; step <= 8; step++)
            { SetCursorPos((int)(a.Left + (b.Right - a.Left) * step / 8), (int)(a.Top + a.Height / 2 + (b.Top - a.Top) * step / 8)); Thread.Sleep(15); }
            mouse_event(4, 0, 0, 0, UIntPtr.Zero);
            Wait(() => text.GetSelection().Single().GetText(-1).Contains("\r\n", StringComparison.Ordinal));
            checks.Add("Real mouse drag across items");
            Trace("select-all");
            Chord(0x11, 0x41); Wait(() => text.GetSelection().Single().GetText(-1) == actual);
            Trace("copy");
            Clipboard.SetText("Layout experiment copy sentinel " + Guid.NewGuid());
            var copySequence = LensDocsStudio.Native.Platform.NativeClipboardService.Sequence;
            Chord(0x11, 0x43); Wait(() => LensDocsStudio.Native.Platform.NativeClipboardService.Sequence != copySequence && Clipboard.ContainsText() && Clipboard.GetText().Contains(names.Item3, StringComparison.Ordinal));
            var copied = Clipboard.GetText();
            var expected = scenario == "list" ? string.Join("\r\n", Enumerable.Range(1, 4000).Select(i => "•\tword" + i)) + "\r\n" : actual;
            if (scenario != "mixed") Require(copied == expected, "Exact selected marker/tab/line-ending serialisation", checks);
            Trace("rtf-decode");
            var rawRtf = Clipboard.GetData(DataFormats.Rtf);
            var rtf = rawRtf is string value ? value : throw new IOException("RTF string payload missing.");
            var decodedDocument = new System.Windows.Documents.FlowDocument();
            var decoded = new System.Windows.Documents.TextRange(decodedDocument.ContentStart, decodedDocument.ContentEnd);
            decoded.Load(new MemoryStream(Encoding.UTF8.GetBytes(rtf)), DataFormats.Rtf);
            File.WriteAllText(Path.Combine(output, "selected-text.txt"), copied); File.WriteAllText(Path.Combine(output, "decoded-rtf-text.txt"), decoded.Text); File.WriteAllText(Path.Combine(output, "selected.rtf"), rtf);
            Require(decoded.Text.TrimEnd('\r', '\n') == copied.TrimEnd('\r', '\n'), "RTF actually decoded with complete logical text", checks);
            if (scenario == "list") Require(DecodedRuns(decodedDocument.Blocks.OfType<System.Windows.Documents.Paragraph>().First().Inlines).Any(run => run.Text.Contains("word1", StringComparison.Ordinal) && run.FontWeight == FontWeights.Bold), "Decoded RTF actual bold style", checks);
            Trace("xaml-decode");
            Require(Clipboard.ContainsData(DataFormats.Xaml) && System.Xml.Linq.XElement.Parse(Clipboard.GetData(DataFormats.Xaml) as string ?? throw new IOException("Xaml string payload missing.")).Descendants().Any(element => element.Name.LocalName == "Run"), "Actual Xaml selected-range content", checks);
            Require(Clipboard.ContainsData(DataFormats.Rtf), "Actual Unicode/Text/RTF clipboard formats", checks);
            Require(AutomationElement.FocusedElement.Current.ProcessId == processId, "Focus retained after copy", checks);
            Trace("complete");
            Program.Write(Path.Combine(output, "independent-uia.json"), new { Status = "PASS", ClientProcess = Environment.ProcessId, ProviderProcess = processId,
                Transport = "Windows UI Automation across process boundary", Checks = checks, PhysicalScreenReader = "NOT CERTIFIED", HtmlClipboard = "MISSING" });
            return 0;
        }
        catch (Exception error)
        { Program.Write(Path.Combine(output, "independent-uia.json"), new { Status = "FAIL", Checks = checks, Error = error.ToString() }); return 1; }
        finally { Key(0x11, true); Key(0x10, true); mouse_event(4, 0, 0, 0, UIntPtr.Zero); }
    }
    private static IEnumerable<System.Windows.Documents.Run> DecodedRuns(System.Windows.Documents.InlineCollection inlines)
    {
        foreach (var inline in inlines) { if (inline is System.Windows.Documents.Run run) yield return run;
            if (inline is System.Windows.Documents.Span span) foreach (var nested in DecodedRuns(span.Inlines)) yield return nested; }
    }
    private static void Wait(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(15)) { if (condition()) return; Thread.Sleep(25); }
        throw new TimeoutException("The independent UIA/input condition did not complete within 15 seconds.");
    }
    private static void Require(bool value, string name, List<string> checks)
    { if (!value) throw new IOException(name); checks.Add(name); }
}
