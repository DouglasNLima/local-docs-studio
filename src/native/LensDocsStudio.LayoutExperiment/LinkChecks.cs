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
    public static async Task Run(ExperimentWindow window, string output)
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true") throw new IOException("Actual external-application link checks require a disposable hosted desktop.");
        var uri = "https://example.com/#lens-layout-experiment-" + Guid.NewGuid().ToString("N");
        var source = "# Link route\n\n[Open child](child/destination.md)\n\n[External destination](" + uri + ")\n\nParent destination marker.\n";
        var child = "# Child\n\nChild destination marker.\n\n[Back to parent](../source.md)\n";
        var sourcePath = Path.Combine(output, "source.md"); var childPath = Path.Combine(output, "child", "destination.md");
        Directory.CreateDirectory(Path.GetDirectoryName(childPath)!); File.WriteAllText(sourcePath, source); File.WriteAllText(childPath, child);
        window.Hide(); var rows = new List<object>();
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
                var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
                foreach (var value in new[] { "--link-client", destination, new WindowInteropHelper(owner).Handle.ToInt64().ToString(), side, uri }) start.ArgumentList.Add(value);
                using var client = (await Task.Run(() => Process.Start(start)))!;
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60)); await client.WaitForExitAsync(deadline.Token);
                if (client.ExitCode != 0) throw new IOException("Independent actual link activation failed: " + side);
                rows.Add(new { Adapter = side, Status = "PASS", MaximumInputGapMilliseconds = gaps.DefaultIfEmpty().Max(), InputGapsMilliseconds = gaps });
            }
            finally { timer.Stop(); if (stock is not null) await stock.Close(); }
        }
        if (File.ReadAllText(sourcePath) != source || File.ReadAllText(childPath) != child) throw new IOException("Link activation changed workspace source.");
        Program.Write(Path.Combine(output, "links.json"), new { Status = "PASS", SourceUnchanged = true, Rows = rows });
    }
    public static int Client(string output, long ownerValue, string side, string uri)
    {
        var checks = new List<string>(); var stages = new List<object>();
        void At(string name) => stages.Add(new { Stage = name, Timestamp = Stopwatch.GetTimestamp() });
        try
        {
            var handle = new IntPtr(ownerValue); var owner = AutomationElement.FromHandle(handle);
            AutomationElement Document() => owner.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, side == "stock" ? "DocumentPreview" : "ExperimentalDocument"))!;
            string Text() => ((TextPattern)Document().GetCurrentPattern(TextPattern.Pattern)).DocumentRange.GetText(-1);
            void Invoke(string name)
            {
                var link = Document().FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink), new PropertyCondition(AutomationElement.NameProperty, name))) ?? throw new IOException("Actual link missing: " + name);
                ((InvokePattern)link.GetCurrentPattern(InvokePattern.Pattern)).Invoke(); At("invoked-" + name);
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
                    using var process = Process.GetProcessById(candidate.Current.ProcessId);
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
    }
}
