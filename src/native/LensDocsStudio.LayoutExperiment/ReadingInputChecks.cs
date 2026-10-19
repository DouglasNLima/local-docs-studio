using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Text;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.LayoutExperiment;

/// <summary>Fresh-process pre-copy reading controls; never an acceptance replacement.</summary>
internal static class ReadingInputChecks
{
    public static async Task Run(ExperimentWindow window, string output, string adapter, string apartment)
    {
        if (adapter is not ("stock" or "candidate") || apartment is not ("STA" or "MTA")) throw new ArgumentException("Choose stock/candidate and STA/MTA.");
        if (ExperimentAppearance.ActualHighContrast) throw new IOException("The reading control requires initial normal appearance.");
        var source = FixtureCatalog.Create(output, "list")[0].Source;
        var path = Path.Combine(output, "fixture.md"); File.WriteAllText(path, source);
        window.SourcePath = path; window.WorkspaceRoot = output;
        var setup = Stopwatch.StartNew();
        FlowDocumentScrollViewer? stock = null;
        if (adapter == "candidate") await window.Request(source, "Light");
        else
        {
            var parsed = await Task.Run(() => new MarkdownService().Parse(source));
            var prepared = await ExperimentWindow.Worker(() => StockPreparation.Create(parsed, path, output));
            var result = await new NativeMarkdownRenderer(new NativeSvgService()).RenderAsync(parsed, false, prepared.Diagrams, _ => { }, (_, _) => { },
                preparedDrawings: prepared.Drawings, preparedImages: prepared.Images, codeSyntax: prepared.Syntax);
            AutomationProperties.SetAutomationId(result.Document, "ReadingInputDocument");
            stock = new FlowDocumentScrollViewer { Document = result.Document, Width = 640, Height = 300, IsToolBarVisible = false };
            window.Content = stock;
        }
        window.UpdateLayout(); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        var setupMilliseconds = setup.Elapsed.TotalMilliseconds;
        // No stock correctness helper here: it focuses and copies before returning.
        using var trace = new OperationTrace(window);
        using var acknowledgement = new InputAcknowledgement(window);
        var timeline = new List<object>(); var last = Stopwatch.GetTimestamp(); var maximum = 0d;
        var allocated = GC.GetTotalAllocatedBytes(); var collections = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
        var heartbeat = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        void Input() { var at = Stopwatch.GetTimestamp(); var gap = (at - last) * 1000d / Stopwatch.Frequency; timeline.Add(new { StartTimestamp = last, EndTimestamp = at, Milliseconds = gap }); maximum = Math.Max(maximum, gap); last = at; }
        heartbeat.Tick += (_, _) => Input(); heartbeat.Start();
        var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "--reading-input-client", output, Environment.ProcessId.ToString(), adapter, acknowledgement.Name, apartment }) info.ArgumentList.Add(argument);
        Process? client = null; string? error = null;
        try
        {
            trace.Stage("reading-client-launch", new { adapter, apartment, KeyboardFocus = System.Windows.Input.Keyboard.FocusedElement?.GetType().Name });
            client = await Task.Run(() => Process.Start(info)) ?? throw new IOException("Independent reading client did not start.");
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90)); await client.WaitForExitAsync(deadline.Token);
            if (client.ExitCode != 0) throw new IOException("Independent reading client failed; original observations retained.");
            await window.Dispatcher.InvokeAsync(Input, DispatcherPriority.Input);
        }
        catch (Exception exception) { error = exception.ToString(); if (client is { HasExited: false }) client.Kill(true); throw; }
        finally
        {
            heartbeat.Stop(); trace.Dispose(); client?.Dispose();
            Program.Write(Path.Combine(output, "reading-input.json"), new { Status = error is null ? "OBSERVED" : "FAIL", DiagnosticOnly = true, Adapter = adapter, ClientApartment = apartment,
                Error = error, CompleteSetupMilliseconds = setupMilliseconds, SetupOutsideInteraction = true, NoExplicitFocusOrCopyPrewarm = true,
                FixtureSha256 = Program.Hash(System.Text.Encoding.UTF8.GetBytes(source)), Width = 640, Height = 300, Zoom = 100,
                Dpi = System.Windows.Media.VisualTreeHelper.GetDpi(window).PixelsPerInchX, MaximumInputGapMilliseconds = maximum, Observed100MillisecondBudgetPass = maximum <= 100,
                InputTimeline = timeline, AllocatedBytes = GC.GetTotalAllocatedBytes() - allocated,
                NaturalCollections = Enumerable.Range(0, 3).Select(index => GC.CollectionCount(index) - collections[index]).ToArray(), ForcedCollection = false });
            Program.Write(Path.Combine(output, "reading-trace.json"), trace.Events);
            Program.Write(Path.Combine(output, "input-acknowledgements.json"), acknowledgement.Receipts);
            if (stock is not null) stock.Document = null;
        }
    }

    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
    [DllImport("user32.dll")] private static extern bool SetPhysicalCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint x, uint y, uint data, UIntPtr extra);
    public static int Client(string output, int processId, string adapter, string acknowledgementName, string apartment)
    {
        var result = 1;
        var thread = new Thread(() => result = Drive()) { Name = "Independent first reading input " + apartment };
        thread.SetApartmentState(apartment == "STA" ? ApartmentState.STA : ApartmentState.MTA); thread.Start(); thread.Join(); return result;
        int Drive()
        {
            var stages = new List<object>();
            void At(string name, object? detail = null) => stages.Add(new { Stage = name, Timestamp = Stopwatch.GetTimestamp(), Thread = Environment.CurrentManagedThreadId, Detail = detail });
            using var acknowledgement = new InputAcknowledgement(acknowledgementName);
            try
            {
                SetThreadDpiAwarenessContext(new IntPtr(-4));
                IndependentUiaClient.InitialiseOwnedInput(output);
                using var process = Process.GetProcessById(processId);
                At("client-start", new { Apartment = Thread.CurrentThread.GetApartmentState().ToString() });
                var owner = AutomationElement.FromHandle(process.MainWindowHandle);
                var document = owner.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, adapter == "stock" ? "ReadingInputDocument" : "ExperimentalDocument")) ?? throw new IOException("Reading document unavailable.");
                var text = (TextPattern)document.GetCurrentPattern(TextPattern.Pattern);
                At("document-text-enter"); var actual = text.DocumentRange.GetText(-1); At("document-text-exit", new { Length = actual.Length });
                if (!actual.Contains("word4000", StringComparison.Ordinal)) throw new IOException("Complete real text missing.");
                At("semantic-tree-enter");
                var lists = document.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.List));
                var count = lists.Count == 0 ? 0 : lists[0].FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)).Count;
                At("semantic-tree-exit", new { Lists = lists.Count, Items = count });
                At("distant-range-enter"); var distant = text.DocumentRange.FindText("word4000", false, false)!; distant.Select(); distant.ScrollIntoView(true);
                IndependentUiaClient.Wait(() => distant.GetBoundingRectangles().Length > 0); At("distant-range-exit");
                At("first-range-enter"); var first = text.DocumentRange.FindText("word1", false, false)!; first.ScrollIntoView(true); At("first-range-exit");
                At("foreground-enter"); var foreground = SetForegroundWindow(process.MainWindowHandle); At("foreground-exit", new { Result = foreground });
                At("focus-enter"); document.SetFocus(); At("focus-returned");
                IndependentUiaClient.Wait(() => AutomationElement.FocusedElement.Current.ProcessId == processId); At("focus-confirmed");
                At("insertion-enter"); var insertion = first.Clone(); insertion.MoveEndpointByRange(TextPatternRangeEndpoint.End, insertion, TextPatternRangeEndpoint.Start); insertion.Select(); At("insertion-exit");
                Chord("ctrl-home", 0x11, 0x24); Chord("shift-down", 0x10, 0x28);
                IndependentUiaClient.Wait(() => text.GetSelection().Single().GetText(-1).Contains("\r\n", StringComparison.Ordinal)); At("keyboard-selection-visible");
                first.ScrollIntoView(true); var second = text.DocumentRange.FindText("word2", false, false)!;
                IndependentUiaClient.Wait(() => first.GetBoundingRectangles().Length > 0 && second.GetBoundingRectangles().Length > 0);
                var a = first.GetBoundingRectangles()[0]; var b = second.GetBoundingRectangles()[0];
                At("mouse-enter"); SetPhysicalCursorPos((int)a.Left + 1, (int)(a.Top + a.Height / 2)); mouse_event(2, 0, 0, 0, UIntPtr.Zero); acknowledgement.AfterRealInput(process.MainWindowHandle);
                for (var step = 1; step <= 8; step++) { SetPhysicalCursorPos((int)(a.Left + (b.Right - a.Left) * step / 8), (int)(a.Top + a.Height / 2 + (b.Top - a.Top) * step / 8)); Thread.Sleep(15); }
                mouse_event(4, 0, 0, 0, UIntPtr.Zero); acknowledgement.AfterRealInput(process.MainWindowHandle);
                IndependentUiaClient.Wait(() => text.GetSelection().Single().GetText(-1).Contains("word2", StringComparison.Ordinal)); At("mouse-selection-visible");
                distant.ScrollIntoView(true); IndependentUiaClient.Wait(() => distant.GetBoundingRectangles().Length > 0); At("distant-navigation-visible");
                if (AutomationElement.FocusedElement.Current.ProcessId != processId) throw new IOException("Reading focus was lost.");
                At("complete"); Program.Write(Path.Combine(output, "reading-client.json"), new { Status = "PASS", Stages = stages, ClientProcess = Environment.ProcessId }); return 0;
                void Chord(string name, params byte[] keys)
                {
                    At(name + "-enter");
                    try { foreach (var key in keys) { At("key-down-sent", new { Key = key }); keybd_event(key, 0, key is >= 0x21 and <= 0x28 ? 1u : 0u, UIntPtr.Zero); Thread.Sleep(30); } acknowledgement.AfterRealInput(process.MainWindowHandle); At(name + "-held-input-delivered"); }
                    finally { foreach (var key in keys.Reverse()) { keybd_event(key, 0, 2u | (key is >= 0x21 and <= 0x28 ? 1u : 0u), UIntPtr.Zero); At("key-up-sent", new { Key = key }); Thread.Sleep(30); } }
                    acknowledgement.AfterRealInput(process.MainWindowHandle); At(name + "-exit");
                }
            }
            catch (Exception exception) { Program.Write(Path.Combine(output, "reading-client.json"), new { Status = "FAIL", Error = exception.ToString(), Stages = stages }); return 1; }
            finally { IndependentUiaClient.ReleaseOwnedInput(); mouse_event(4, 0, 0, 0, UIntPtr.Zero); }
        }
    }
}
