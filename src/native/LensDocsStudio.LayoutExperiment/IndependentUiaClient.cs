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
    public static int Run(string output, int processId, string expectedHash)
    {
        var checks = new List<string>();
        try
        {
            using var target = Process.GetProcessById(processId);
            Wait(() => { target.Refresh(); return target.MainWindowHandle != IntPtr.Zero; });
            var window = AutomationElement.FromHandle(target.MainWindowHandle);
            var document = window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "ExperimentalDocument"))
                ?? throw new IOException("Real document AutomationElement missing.");
            var text = (TextPattern)document.GetCurrentPattern(TextPattern.Pattern);
            var actual = text.DocumentRange.GetText(-1);
            Require(Program.Hash(Encoding.UTF8.GetBytes(actual)) == expectedHash, "Complete document text through independent UIA", checks);
            var list = document.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.List));
            Require(list.Count == 1, "One semantic UIA list", checks);
            var items = list[0].FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem));
            Require(items.Count == 4000 && items[3999].Current.Name.StartsWith("word4000", StringComparison.Ordinal), "All 4,000 real UIA items in order", checks);
            var last = text.DocumentRange.FindText("word4000", false, false) ?? throw new IOException("UIA FindText failed.");
            last.Select(); Require(text.GetSelection().Single().GetText(-1) == "word4000", "UIA selection", checks);
            Require((int)last.GetAttributeValue(TextPattern.FontWeightAttribute) == 700, "Actual bold text attribute", checks);
            last.ScrollIntoView(true);
            Wait(() => last.GetBoundingRectangles().Length > 0);
            var rectangle = last.GetBoundingRectangles()[0];
            var point = text.RangeFromPoint(new Point(rectangle.Left + 1, rectangle.Top + rectangle.Height / 2));
            point.ExpandToEnclosingUnit(TextUnit.Word);
            Require(point.GetText(-1).Contains("word4000", StringComparison.Ordinal), "UIA point geometry on distant final text", checks);
            var itemRange = text.RangeFromChild(items[3999]);
            Require(itemRange.GetText(-1).StartsWith("word4000", StringComparison.Ordinal), "UIA list-item range relationship", checks);
            var clone = last.Clone(); clone.MoveEndpointByUnit(TextPatternRangeEndpoint.Start, TextUnit.Character, 4);
            Require(clone.GetText(-1) == "4000", "UIA character endpoint movement", checks);
            var first = text.DocumentRange.FindText("word1", false, false)!; first.ScrollIntoView(true);
            SetForegroundWindow(target.MainWindowHandle); document.SetFocus();
            Wait(() => AutomationElement.FocusedElement.Current.ProcessId == processId);
            Chord(0x11, 0x24); // Ctrl+Home
            Chord(0x10, 0x28); // Shift+Down across the first list boundary
            Wait(() => text.GetSelection().Single().GetText(-1).Contains("\r\n", StringComparison.Ordinal));
            checks.Add("Real keyboard extension across items, focus retained");
            first.ScrollIntoView(true);
            var second = text.DocumentRange.FindText("word2", false, false)!;
            Wait(() => first.GetBoundingRectangles().Length > 0 && second.GetBoundingRectangles().Length > 0);
            var a = first.GetBoundingRectangles()[0]; var b = second.GetBoundingRectangles()[0];
            SetCursorPos((int)a.Left + 1, (int)(a.Top + a.Height / 2)); mouse_event(2, 0, 0, 0, UIntPtr.Zero);
            for (var step = 1; step <= 8; step++)
            { SetCursorPos((int)(a.Left + (b.Right - a.Left) * step / 8), (int)(a.Top + a.Height / 2 + (b.Top - a.Top) * step / 8)); Thread.Sleep(15); }
            mouse_event(4, 0, 0, 0, UIntPtr.Zero);
            Wait(() => text.GetSelection().Single().GetText(-1).Contains("\r\n", StringComparison.Ordinal));
            checks.Add("Real mouse drag across items");
            Chord(0x11, 0x41); Wait(() => text.GetSelection().Single().GetText(-1) == actual);
            Chord(0x11, 0x43); Wait(() => Clipboard.ContainsText() && Clipboard.GetText() == actual);
            Require(Clipboard.ContainsData(DataFormats.Rtf), "Actual Unicode/Text/RTF clipboard formats", checks);
            Require(AutomationElement.FocusedElement.Current.ProcessId == processId, "Focus retained after copy", checks);
            Program.Write(Path.Combine(output, "independent-uia.json"), new { Status = "PASS", ClientProcess = Environment.ProcessId, ProviderProcess = processId,
                Transport = "Windows UI Automation across process boundary", Checks = checks, PhysicalScreenReader = "NOT CERTIFIED", HtmlClipboard = "MISSING" });
            return 0;
        }
        catch (Exception error)
        { Program.Write(Path.Combine(output, "independent-uia.json"), new { Status = "FAIL", Checks = checks, Error = error.ToString() }); return 1; }
        finally { Key(0x11, true); Key(0x10, true); mouse_event(4, 0, 0, 0, UIntPtr.Zero); }
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
