using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Interop;
using System.Windows.Threading;

namespace LensDocsStudio.LayoutExperiment;

internal static class LinkChecks
{
    [DllImport("user32.dll")] private static extern IntPtr GetLastActivePopup(IntPtr window);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(System.Drawing.Point point);
    [DllImport("user32.dll")] private static extern bool IsChild(IntPtr parent, IntPtr child);
    [DllImport("user32.dll")] private static extern bool SetPhysicalCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
    public static async Task Run(ExperimentWindow window, string output)
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true") throw new IOException("Actual external-application link checks require a disposable hosted desktop.");
        var uri = "https://example.com/#lens-layout-experiment-" + Guid.NewGuid().ToString("N");
        var source = "# Link route\n\n[Open child](child/destination.md)\n\n[External destination](" + uri + ")\n\nParent destination marker.\n";
        var child = "# Child\n\nChild destination marker.\n\n[Back to parent](../source.md)\n";
        var sourcePath = Path.Combine(output, "source.md"); var childPath = Path.Combine(output, "child", "destination.md");
        Directory.CreateDirectory(Path.GetDirectoryName(childPath)!); File.WriteAllText(sourcePath, source); File.WriteAllText(childPath, child);
        window.Hide(); var rows = new List<object>(); var failed = false; var candidateMaximum = 0d;
        foreach (var side in new[] { "stock", "candidate" })
        {
            var destination = Path.Combine(output, side); Directory.CreateDirectory(destination);
            StockWindowForChecks? stock = null;
            if (side == "stock") stock = await StockWindowForChecks.Open(sourcePath, output, Path.Combine(destination, "isolated-state"));
            else
            {
                window.Content = null; window.Show(); _ = new DiagnosticStudio(window, source, sourcePath);
                var waiting = Stopwatch.StartNew();
                while (window.Surface.Snapshot?.Model.Source != source) { if (waiting.Elapsed.TotalSeconds > 15) throw new TimeoutException("Link studio did not become current."); await Task.Delay(10); }
            }
            var owner = stock is null ? (System.Windows.Window)window : stock.Window;
            var gaps = new List<double>(); var last = Stopwatch.GetTimestamp();
            var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += (_, _) => { var at = Stopwatch.GetTimestamp(); gaps.Add((at - last) * 1000d / Stopwatch.Frequency); last = at; }; timer.Start();
            try
            {
                using var acknowledgement = new InputAcknowledgement(owner);
                var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
                foreach (var value in new[] { "--link-client", destination, new WindowInteropHelper(owner).Handle.ToInt64().ToString(), side, uri, acknowledgement.Name }) start.ArgumentList.Add(value);
                using var client = (await Task.Run(() => Process.Start(start)))!;
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                try { await client.WaitForExitAsync(deadline.Token); }
                finally { if (!client.HasExited) client.Kill(true); }
                Program.Write(Path.Combine(destination, "input-acknowledgements.json"), acknowledgement.Receipts);
                if (client.ExitCode != 0) throw new IOException("Independent actual link activation failed: " + side);
                rows.Add(new { Adapter = side, Status = "PASS", MaximumInputGapMilliseconds = gaps.DefaultIfEmpty().Max(), InputGapsMilliseconds = gaps });
            }
            catch (Exception error)
            { failed = true; rows.Add(new { Adapter = side, Status = "FAIL", Error = error.ToString(), MaximumInputGapMilliseconds = gaps.DefaultIfEmpty().Max(), InputGapsMilliseconds = gaps }); }
            finally { timer.Stop(); if (side == "candidate") candidateMaximum = gaps.DefaultIfEmpty().Max(); if (stock is not null) await stock.Close(); }
        }
        if (File.ReadAllText(sourcePath) != source || File.ReadAllText(childPath) != child) throw new IOException("Link activation changed workspace source.");
        Program.Write(Path.Combine(output, "links.json"), new { Status = failed ? "FAIL" : "PASS", SourceUnchanged = true, Rows = rows,
            MaximumInputGapMilliseconds = candidateMaximum });
        if (failed) throw new IOException("Actual stock/candidate link route checks failed; both sides retained.");
    }
    public static int Client(string output, long ownerValue, string side, string uri, string acknowledgementName)
    {
        var checks = new List<string>(); var stages = new List<object>();
        void At(string name) => stages.Add(new { Stage = name, Timestamp = Stopwatch.GetTimestamp() });
        try
        {
            IndependentUiaClient.InitialiseOwnedInput(output);
            using var acknowledgement = new InputAcknowledgement(acknowledgementName);
            var handle = new IntPtr(ownerValue); var owner = AutomationElement.FromHandle(handle);
            AutomationElement Document() => owner.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, side == "stock" ? "DocumentPreview" : "ExperimentalDocument"))!;
            string Text() => ((TextPattern)Document().GetCurrentPattern(TextPattern.Pattern)).DocumentRange.GetText(-1);
            void Invoke(string name)
            {
                // Stock WPF does not expose a matching Name for every hyperlink.
                // Drive the actual text geometry with real mouse input on both sides.
                var text = (TextPattern)Document().GetCurrentPattern(TextPattern.Pattern);
                var range = text.DocumentRange.FindText(name, false, false) ?? throw new IOException("Actual link text missing: " + name);
                range.ScrollIntoView(true); System.Threading.Thread.Sleep(100);
                var rectangle = range.GetBoundingRectangles().FirstOrDefault();
                if (rectangle.IsEmpty || rectangle.Width <= 0 || rectangle.Height <= 0) throw new IOException("Actual link has no visible geometry: " + name);
                SetForegroundWindow(handle); owner.SetFocus();
                var point = new System.Drawing.Point((int)(rectangle.Left + rectangle.Width / 2), (int)(rectangle.Top + rectangle.Height / 2));
                IndependentUiaClient.Wait(() => GetForegroundWindow() == handle && (WindowFromPoint(point) == handle || IsChild(handle, WindowFromPoint(point))));
                stages.Add(new { Stage = "verified-click-target", Name = name, Foreground = GetForegroundWindow().ToInt64(), Target = WindowFromPoint(point).ToInt64(), point.X, point.Y, Timestamp = Stopwatch.GetTimestamp() });
                SetPhysicalCursorPos(point.X, point.Y); mouse_event(2, 0, 0, 0, UIntPtr.Zero);
                try { acknowledgement.AfterRealInput(handle); } finally { mouse_event(4, 0, 0, 0, UIntPtr.Zero); }
                At("clicked-" + name);
            }
            IndependentUiaClient.Wait(() => Text().Contains("Parent destination marker.", StringComparison.Ordinal));
            Invoke("Open child"); IndependentUiaClient.Wait(() => Text().Contains("Child destination marker.", StringComparison.Ordinal));
            Invoke("Back to parent"); IndependentUiaClient.Wait(() => Text().Contains("Parent destination marker.", StringComparison.Ordinal));
            checks.Add("Actual parent-to-child and child-to-parent workspace routes preserve the root");
            foreach (var cancel in new[] { true, false })
            {
                Invoke("External destination"); AutomationElement? dialogue = null;
                IndependentUiaClient.Wait(() => { var popup = GetLastActivePopup(handle); dialogue = popup == handle || popup == IntPtr.Zero ? null : AutomationElement.FromHandle(popup); return dialogue?.Current.Name == "External link"; });
                if (OperationTrace.IsWindowEnabled(handle)) throw new IOException("External confirmation did not own its modal window.");
                SetForegroundWindow(new IntPtr(dialogue!.Current.NativeWindowHandle)); IndependentUiaClient.Chord((byte)(cancel ? 0x1B : 0x0D));
                IndependentUiaClient.Wait(() => OperationTrace.IsWindowEnabled(handle)); checks.Add(cancel ? "Real external confirmation Cancel" : "Real external confirmation OK");
            }
            AutomationElement? browser = null; AutomationElement? address = null;
            IndependentUiaClient.Wait(() =>
            {
                foreach (var candidate in AutomationElement.RootElement.FindAll(TreeScope.Children, Condition.TrueCondition).Cast<AutomationElement>())
                {
                    var processId = candidate.Current.ProcessId; if (processId <= 0) continue;
                    using var process = Process.GetProcessById(processId);
                    if (process.ProcessName is not ("msedge" or "chrome" or "firefox" or "iexplore")) continue;
                    var edits = candidate.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
                    foreach (var edit in edits.Cast<AutomationElement>())
                        if (edit.TryGetCurrentPattern(ValuePattern.Pattern, out var value) && ((ValuePattern)value).Current.Value.Contains(uri.Split('#')[1], StringComparison.Ordinal))
                        { browser = candidate; address = edit; return true; }
                }
                return false;
            });
            var actual = ((ValuePattern)address!.GetCurrentPattern(ValuePattern.Pattern)).Current.Value;
            if (!actual.Contains("example.com", StringComparison.OrdinalIgnoreCase) || !actual.Contains(uri.Split('#')[1], StringComparison.Ordinal)) throw new IOException("The default browser received a different destination.");
            checks.Add("Actual default browser address contains the exact destination nonce; network page availability is not asserted");
            Program.Write(Path.Combine(output, "external-destination.json"), new { Expected = uri, Actual = actual, BrowserProcess = browser!.Current.ProcessId });
            SetForegroundWindow(new IntPtr(browser!.Current.NativeWindowHandle)); IndependentUiaClient.Chord(0x11, 0x57); // Close only this verified test tab.
            Program.Write(Path.Combine(output, "links-client.json"), new { Status = "PASS", Checks = checks, Stages = stages }); return 0;
        }
        catch (Exception error) { Program.Write(Path.Combine(output, "links-client.json"), new { Status = "FAIL", Error = error.ToString(), Checks = checks, Stages = stages }); return 1; }
        finally { IndependentUiaClient.ReleaseOwnedInput(); mouse_event(4, 0, 0, 0, UIntPtr.Zero); }
    }
}
