using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Threading;
using LensDocsStudio.Native.Validation;

namespace LensDocsStudio.LayoutExperiment;

/// <summary>Actual source/tab/search interaction through a separate OS UIA client.</summary>
internal static class StudioChecks
{
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool SetPhysicalCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    public static async Task Run(ExperimentWindow window, string output)
    {
        var source = ContinuousPreviewFixtures.Create(output, "mixed")[0].Source;
        var path = Path.Combine(output, "source.md"); File.WriteAllText(path, source);
        var original = File.ReadAllBytes(path); var installations = new List<object>();
        var invalid = false;
        window.Installed += snapshot =>
        {
            var current = snapshot.Version == window.CurrentVersion && snapshot.Model.Source == window.LatestSource
                && snapshot.Appearance == ExperimentAppearance.Capture(window.ThemePreference);
            invalid |= !current; installations.Add(new { snapshot.Version, Current = current, snapshot.Appearance, SourceHash = Program.Hash(Encoding.UTF8.GetBytes(snapshot.Model.Source)) });
        };
        _ = new DiagnosticStudio(window, source, path);
        await Wait(() => window.Surface.Snapshot?.Model.Source == source);
        var gaps = new List<double>(); var timeline = new List<object>(); var watch = Stopwatch.StartNew(); var last = 0d;
        var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) => { var at = watch.Elapsed.TotalMilliseconds; gaps.Add(at - last); timeline.Add(new { Timestamp = Stopwatch.GetTimestamp(), GapMilliseconds = at - last }); last = at; };
        timer.Start();
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "--studio-client", output, Environment.ProcessId.ToString() }) start.ArgumentList.Add(argument);
        using var client = (await Task.Run(() => Process.Start(start)))!;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90)); await client.WaitForExitAsync(deadline.Token);
            if (client.ExitCode != 0 || invalid || !original.SequenceEqual(File.ReadAllBytes(path))) throw new IOException("Diagnostic studio interaction, installation currency or workspace byte preservation failed.");
            Program.Write(Path.Combine(output, "studio.json"), new { Status = "PASS", IndependentClient = "PASS", WorkspaceBytesUnchanged = true,
                Installations = installations, MaximumInputGapMilliseconds = gaps.DefaultIfEmpty().Max(), InputGapsMilliseconds = gaps, InputTimeline = timeline, Events = window.Events.ToArray() });
        }
        catch (Exception error)
        {
            if (!client.HasExited) client.Kill(true);
            Program.Write(Path.Combine(output, "studio.json"), new { Status = "FAIL", Error = error.ToString(), Installations = installations,
                MaximumInputGapMilliseconds = gaps.DefaultIfEmpty().Max(), InputGapsMilliseconds = gaps, InputTimeline = timeline, Events = window.Events.ToArray() }); throw;
        }
        finally { timer.Stop(); }
    }
    public static int Client(string output, int processId)
    {
        var checks = new List<string>(); var stages = new List<object>();
        void Stage(string stage) { stages.Add(new { Stage = stage, Timestamp = Stopwatch.GetTimestamp() }); Program.Write(Path.Combine(output, "studio-client-stages.json"), stages); }
        try
        {
            SetThreadDpiAwarenessContext(new IntPtr(-4));
            using var process = Process.GetProcessById(processId); SetForegroundWindow(process.MainWindowHandle);
            var window = AutomationElement.FromHandle(process.MainWindowHandle);
            AutomationElement Element(string id) => window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, id)) ?? throw new IOException("Missing diagnostic element: " + id);
            var document = Element("ExperimentalDocument");
            TextPattern Text() => (TextPattern)document.GetCurrentPattern(TextPattern.Pattern);
            string Content() { try { return Text().DocumentRange.GetText(-1); } catch (ElementNotAvailableException) { return ""; } }
            void Invoke(string label) => ((InvokePattern)Element("Experimental" + label).GetCurrentPattern(InvokePattern.Pattern)).Invoke();
            var editor = Element("ExperimentalSourceEditor");
            using var clipboard = new IndependentUiaClient.ClipboardOwner();
            void ReplaceSource(string value)
            {
                clipboard.SetSentinel(value); var bounds = editor.Current.BoundingRectangle; SetPhysicalCursorPos((int)bounds.Left + 80, (int)bounds.Top + 25); mouse_event(2, 0, 0, 0, UIntPtr.Zero); mouse_event(4, 0, 0, 0, UIntPtr.Zero); IndependentUiaClient.Chord(0x11, 0x41); IndependentUiaClient.Chord(0x11, 0x56);
            }
            var original = File.ReadAllText(Path.Combine(output, "source.md"));
            Stage("search");
            ((ValuePattern)Element("ExperimentalSearch").GetCurrentPattern(ValuePattern.Pattern)).SetValue("Deep target"); Invoke("Nextmatch");
            IndependentUiaClient.Wait(() => Text().GetSelection().Single().GetText(-1) == "Deep target"); checks.Add("Actual source search, highlighting and next navigation");
            Invoke("Previousmatch"); IndependentUiaClient.Wait(() => Text().GetSelection().Single().GetText(-1) == "Deep target"); checks.Add("Previous search navigation");
            ((ValuePattern)Element("ExperimentalSearch").GetCurrentPattern(ValuePattern.Pattern)).SetValue("");
            Stage("leading-edit-and-anchor");
            var final = Text().DocumentRange.FindText("Mixed final marker.", false, false)!; final.Select(); final.ScrollIntoView(true);
            var before = final.GetBoundingRectangles()[0]; var oldRange = final.Clone();
            var edited = "Leading source edit.\n\n" + original; ReplaceSource(edited);
            IndependentUiaClient.Wait(() => Content().StartsWith("Leading source edit.", StringComparison.Ordinal));
            var afterRange = Text().DocumentRange.FindText("Mixed final marker.", false, false)!;
            var after = afterRange.GetBoundingRectangles();
            if (after.Length == 0 || Math.Abs(after[0].Top - before.Top) > 1) throw new IOException("Source-based distant viewport anchor moved after a leading edit.");
            var stale = false; try { oldRange.GetText(-1); } catch (ElementNotAvailableException) { stale = true; }
            if (!stale) throw new IOException("Independent old range survived replacement.");
            checks.Add("Leading edit preserves distant source viewport anchor; independent stale range rejected");
            Stage("new-document"); Invoke("Newdocument"); IndependentUiaClient.Wait(() => Content().Contains("New document", StringComparison.Ordinal));
            var second = "# Second document\n\nA **bold** second source.\n\n<details>\n<summary>Closed section</summary>\n\nHidden target.\n</details>\n\n## Last heading\n\nFinal second marker.\n";
            ReplaceSource(second); IndependentUiaClient.Wait(() => Content().Contains("Final second marker.", StringComparison.Ordinal));
            if (Content().Contains("Hidden target.", StringComparison.Ordinal)) throw new IOException("Closed details exposed hidden text.");
            Stage("expand-and-lifetime");
            var details = document.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ClassNameProperty, "SemanticDetails"))!;
            ((ExpandCollapsePattern)details.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand(); IndependentUiaClient.Wait(() => Content().Contains("Hidden target.", StringComparison.Ordinal));
            checks.Add("Actual UIA details expansion with complete hidden geometry");
            Stage("outline");
            var outline = Element("ExperimentalOutline"); var heading = outline.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)).Cast<AutomationElement>().Last();
            ((SelectionItemPattern)heading.GetCurrentPattern(SelectionItemPattern.Pattern)).Select();
            IndependentUiaClient.Wait(() => { var selected = Text().GetSelection().Single().Clone(); selected.ExpandToEnclosingUnit(System.Windows.Automation.Text.TextUnit.Paragraph); return selected.GetText(-1).Contains("Last heading", StringComparison.Ordinal); }); checks.Add("Actual outline/source navigation");
            Stage("close-and-reopen"); Invoke("Closedocument"); IndependentUiaClient.Wait(() => Content().Contains("Mixed final marker.", StringComparison.Ordinal));
            if (!Content().StartsWith("Leading source edit.", StringComparison.Ordinal)) throw new IOException("Switching lost unsaved source.");
            Invoke("Reopenclosed"); IndependentUiaClient.Wait(() => Content().Contains("Final second marker.", StringComparison.Ordinal));
            if (!Content().Contains("Hidden target.", StringComparison.Ordinal)) throw new IOException("Reopen lost details state.");
            checks.Add("Document closure/reopen preserves unsaved source and per-document details state");
            Stage("rapid-edits");
            ReplaceSource(second + "\nRapid edit zero.\n"); ReplaceSource(second + "\nRapid edit winning.\n");
            IndependentUiaClient.Wait(() => Content().Contains("Rapid edit winning.", StringComparison.Ordinal));
            if (Content().Contains("Rapid edit zero.", StringComparison.Ordinal)) throw new IOException("Obsolete source installed.");
            checks.Add("Real source input, replacements and winning current preview");
            Stage("complete"); Program.Write(Path.Combine(output, "studio-client.json"), new { Status = "PASS", Checks = checks }); return 0;
        }
        catch (Exception error) { Program.Write(Path.Combine(output, "studio-client.json"), new { Status = "FAIL", Checks = checks, Error = error.ToString() }); return 1; }
    }
    private static async Task Wait(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew(); while (!condition()) { if (watch.Elapsed.TotalSeconds > 15) throw new TimeoutException("Diagnostic current preview did not become ready."); await Task.Delay(10); }
    }
}
