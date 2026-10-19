using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using LensDocsStudio.Native.Validation;

namespace LensDocsStudio.LayoutExperiment;

internal static class MediaChecks
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string? className, string title);
    [DllImport("user32.dll")] private static extern IntPtr GetLastActivePopup(IntPtr window);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
    public static async Task Run(ExperimentWindow window, string output, bool staClientControl = false)
    {
        var fixture = ContinuousPreviewFixtures.Create(output, "mixed")[0]; var path = Path.Combine(output, "source.md");
        File.WriteAllText(path, fixture.Source); window.SourcePath = path; window.WorkspaceRoot = output;
        await window.Request(fixture.Source);
        await Exercise(window, output, "candidate", window, staClientControl);
        if (window.Surface.Snapshot?.Model.Source != fixture.Source || File.ReadAllText(path) != fixture.Source) throw new IOException("Media export changed source.");
    }
    internal static async Task Exercise(System.Windows.Window owner, string output, string route, ExperimentWindow? candidate = null, bool staClientControl = false)
    {
        using var trace = new OperationTrace(owner); if (candidate is not null) candidate.Trace = trace;
        var gaps = new List<double>(); var watch = Stopwatch.StartNew(); var last = 0d;
        var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) => { var at = watch.Elapsed.TotalMilliseconds; gaps.Add(at - last); last = at; }; timer.Start();
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "--media-client", output, Environment.ProcessId.ToString(), new System.Windows.Interop.WindowInteropHelper(owner).Handle.ToInt64().ToString(), route, staClientControl ? "STA" : "MTA" }) start.ArgumentList.Add(argument);
        using var client = (await Task.Run(() => Process.Start(start)))!;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60)); await client.WaitForExitAsync(deadline.Token);
            if (client.ExitCode != 0) throw new IOException("Independent media export failed.");
            Program.Write(Path.Combine(output, "media.json"), new { Status = "PASS", Route = route, SourceUnchanged = true, MaximumInputGapMilliseconds = gaps.DefaultIfEmpty().Max(), InputGapsMilliseconds = gaps, InteractionStages = candidate?.Surface.InteractionStages, Events = candidate?.Events.ToArray() });
        }
        catch (Exception error)
        {
            if (!client.HasExited) client.Kill(true);
            Program.Write(Path.Combine(output, "media.json"), new { Status = "FAIL", Route = route, Error = error.ToString(), MaximumInputGapMilliseconds = gaps.DefaultIfEmpty().Max(), InputGapsMilliseconds = gaps, InteractionStages = candidate?.Surface.InteractionStages, Events = candidate?.Events.ToArray() }); throw;
        }
        finally { timer.Stop(); trace.Dispose(); if (candidate is not null) candidate.Trace = null; Program.Write(Path.Combine(output, "media-trace.json"), trace.Events); }
        WindowCapture.Save(owner, Path.Combine(output, "shown-media.png"));
    }
    public static int RunClient(string output, int processId, long handle, string route, bool staControl)
    {
        // UIA owns no windows on this thread. OLE retains its separate pumped
        // STA. The product's native dialogue stays on its window dispatcher.
        // Keep the previous STA client as an explicit comparative control.
        var result = 1;
        var client = new Thread(() => result = Client(output, processId, handle, route)) { Name = "Independent native dialogue UIA client" };
        client.SetApartmentState(staControl ? ApartmentState.STA : ApartmentState.MTA);
        client.Start(); client.Join(); return result;
    }
    private static int Client(string output, int processId, long handle, string route)
    {
        var checks = new List<string>(); using var clipboard = new IndependentUiaClient.ClipboardOwner();
        var responseFailures = new List<string>();
        var timeline = new List<object>();
        using var journal = File.CreateText(Path.Combine(output, "media-client-timeline.jsonl")); journal.AutoFlush = true;
        void At(string stage, object? detail = null)
        {
            var row = new { Stage = stage, Timestamp = Stopwatch.GetTimestamp(), Thread = Environment.CurrentManagedThreadId, Detail = detail };
            timeline.Add(row); journal.WriteLine(System.Text.Json.JsonSerializer.Serialize(row));
        }
        try
        {
            At("client-start", new { Apartment = Thread.CurrentThread.GetApartmentState().ToString(), NativeThread = NativeThread(), Process = Environment.ProcessId,
                ProbeVersion = "native-dialogue-client-v2-call-boundaries" });
            var ownerHandle = new IntPtr(handle); var owner = AutomationElement.FromHandle(ownerHandle);
            AutomationElement scope = owner;
            if (route == "candidate")
            {
                var document = owner.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "ExperimentalDocument"))!;
                var media = document.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Mermaid diagram"))!;
                var text = (TextPattern)document.GetCurrentPattern(TextPattern.Pattern); text.RangeFromChild(media).ScrollIntoView(true); scope = media;
            }
            foreach (var format in new[] { "SVG", "PNG" })
            foreach (var cancel in new[] { false, true })
            {
                var path = Path.GetFullPath(Path.Combine(output, (cancel ? "cancelled" : "actual") + "-dialogue-export." + format.ToLowerInvariant()));
                if (File.Exists(path)) throw new IOException("The independent export requires a fresh output path.");
                var command = scope.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button), new PropertyCondition(AutomationElement.NameProperty, format)))!;
                At("command-invoke", new { format, cancel });
                ((InvokePattern)command.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                At("command-invoke-returned", new { format, cancel });
                AutomationElement? dialogue = null;
                IntPtr dialogueHandle = IntPtr.Zero;
                IndependentUiaClient.Wait(() =>
                {
                    dialogueHandle = GetLastActivePopup(ownerHandle);
                    if (dialogueHandle == IntPtr.Zero || dialogueHandle == ownerHandle) return false;
                    At("dialogue-uia-from-handle-enter", new { Handle = dialogueHandle.ToInt64() });
                    dialogue = AutomationElement.FromHandle(dialogueHandle);
                    At("dialogue-uia-from-handle-exit");
                    At("dialogue-uia-process-read-enter");
                    var dialogueProcess = dialogue.Current.ProcessId;
                    At("dialogue-uia-process-read-exit", new { Process = dialogueProcess });
                    return dialogueProcess == processId;
                });
                At("dialogue-found", new { Handle = dialogueHandle.ToInt64(), OwnerEnabled = OperationTrace.IsWindowEnabled(ownerHandle), dialogue!.Current.Name });
                var response = Stopwatch.GetTimestamp(); var ack = SendMessageTimeout(dialogueHandle, 0, IntPtr.Zero, IntPtr.Zero, 2, 100, out _);
                At("dialogue-native-response", new { Success = ack != IntPtr.Zero, Milliseconds = (Stopwatch.GetTimestamp() - response) * 1000d / Stopwatch.Frequency });
                if (ack == IntPtr.Zero || OperationTrace.IsWindowEnabled(ownerHandle)) throw new IOException("The owned dialogue is unresponsive or did not disable its owner.");
                AutomationElement? fileName = null;
                IndependentUiaClient.Wait(() => (fileName = dialogue!.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit), new PropertyCondition(AutomationElement.NameProperty, "File name:"))))?.Current.IsEnabled == true);
                fileName!.SetFocus(); IndependentUiaClient.Wait(() => fileName.Current.HasKeyboardFocus);
                At("filename-clipboard-enter", new { OleThread = clipboard.ThreadId, Sequence = LensDocsStudio.Native.Platform.NativeClipboardService.Sequence });
                clipboard.SetSentinel(path);
                At("filename-clipboard-exit", new { OleThread = clipboard.ThreadId, Sequence = LensDocsStudio.Native.Platform.NativeClipboardService.Sequence });
                IndependentUiaClient.Chord(0x11, 0x41); IndependentUiaClient.Chord(0x11, 0x56);
                IndependentUiaClient.Wait(() => ((ValuePattern)fileName.GetCurrentPattern(ValuePattern.Pattern)).Current.Value == path);
                At("dialogue-real-input-confirmed", new { format, cancel, Value = path });
                var save = dialogue!.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button), new PropertyCondition(AutomationElement.AutomationIdProperty, "1")))!;
                Program.Write(Path.Combine(output, "save-dialogue-" + format + (cancel ? "-cancel" : "-save") + ".json"), new { FileName = ((ValuePattern)fileName.GetCurrentPattern(ValuePattern.Pattern)).Current.Value, SaveName = save.Current.Name, save.Current.AutomationId, save.Current.IsEnabled });
                if (!save.Current.Name.Contains("Save", StringComparison.OrdinalIgnoreCase)) throw new IOException("The selected dialogue command is not Save.");
                At("dialogue-confirm-input", new { format, cancel });
                IndependentUiaClient.Chord((byte)(cancel ? 0x1B : 0x0D));
                IndependentUiaClient.Wait(() => OperationTrace.IsWindowEnabled(ownerHandle));
                At("owner-enabled-after-dialogue");
                response = Stopwatch.GetTimestamp(); ack = SendMessageTimeout(ownerHandle, 0, IntPtr.Zero, IntPtr.Zero, 2, 100, out _);
                At("owner-immediate-native-response", new { Success = ack != IntPtr.Zero, Milliseconds = (Stopwatch.GetTimestamp() - response) * 1000d / Stopwatch.Frequency });
                if (ack == IntPtr.Zero) responseFailures.Add(format + (cancel ? " Cancel" : " Save") + ": owner native acknowledgement missed the unchanged 100 ms bound");
                if (cancel) { if (File.Exists(path)) throw new IOException("Cancel wrote a file."); checks.Add(format + " actual Cancel, no file; owner acknowledgement recorded separately"); continue; }
                IndependentUiaClient.Wait(() => File.Exists(path) && new FileInfo(path).Length > 0);
                if (format == "SVG")
                {
                    var svg = XDocument.Load(path); if (svg.Root?.Name.LocalName != "svg" || !svg.Root.Descendants().Any()) throw new IOException("Exported SVG has no real drawing.");
                }
                else
                {
                    // Keep WPF decoding on the existing pumped STA too; the
                    // UIA MTA must not acquire a WPF dispatcher/native window.
                    var dimensions = clipboard.Read(() =>
                    {
                        using var file = File.OpenRead(path); var png = BitmapDecoder.Create(file, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                        return (Frames: png.Frames.Count, Width: png.Frames[0].PixelWidth, Height: png.Frames[0].PixelHeight);
                    });
                    if (dimensions.Frames != 1 || dimensions.Width <= 0 || dimensions.Height <= 0) throw new IOException("Exported PNG did not decode.");
                }
                checks.Add("Actual native " + format + " save dialogue, chosen path and decoded drawing");
                At("chosen-file-decoded", new { format, path, Bytes = new FileInfo(path).Length });
            }
            if (responseFailures.Count > 0) throw new IOException("Native response assertions failed; subsequent Save/Cancel/file checks were completed independently: " + string.Join("; ", responseFailures));
            Program.Write(Path.Combine(output, "media-client.json"), new { Status = "PASS", Route = route, Checks = checks, Timeline = timeline, ResponseBudgetFailures = responseFailures }); return 0;
        }
        catch (Exception error) { Program.Write(Path.Combine(output, "media-client.json"), new { Status = "FAIL", Checks = checks, Timeline = timeline, ResponseBudgetFailures = responseFailures, Error = error.ToString() }); return 1; }
    }
    [DllImport("kernel32.dll", EntryPoint = "GetCurrentThreadId")] private static extern uint NativeThread();
}
