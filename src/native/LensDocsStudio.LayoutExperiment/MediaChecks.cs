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
    public static async Task Run(ExperimentWindow window, string output)
    {
        var fixture = ContinuousPreviewFixtures.Create(output, "mixed")[0]; var path = Path.Combine(output, "source.md");
        File.WriteAllText(path, fixture.Source); window.SourcePath = path; window.WorkspaceRoot = output;
        await window.Request(fixture.Source);
        await Exercise(window, output, "candidate", window);
        if (window.Surface.Snapshot?.Model.Source != fixture.Source || File.ReadAllText(path) != fixture.Source) throw new IOException("Media export changed source.");
    }
    internal static async Task Exercise(System.Windows.Window owner, string output, string route, ExperimentWindow? candidate = null)
    {
        using var trace = new OperationTrace(owner); if (candidate is not null) candidate.Trace = trace;
        var gaps = new List<double>(); var watch = Stopwatch.StartNew(); var last = 0d;
        var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) => { var at = watch.Elapsed.TotalMilliseconds; gaps.Add(at - last); last = at; }; timer.Start();
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "--media-client", output, Environment.ProcessId.ToString(), new System.Windows.Interop.WindowInteropHelper(owner).Handle.ToInt64().ToString(), route }) start.ArgumentList.Add(argument);
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
    public static int Client(string output, int processId, long handle, string route)
    {
        var checks = new List<string>(); using var clipboard = new IndependentUiaClient.ClipboardOwner();
        var timeline = new List<object>();
        void At(string stage, object? detail = null) => timeline.Add(new { Stage = stage, Timestamp = Stopwatch.GetTimestamp(), Thread = Environment.CurrentManagedThreadId, Detail = detail });
        try
        {
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
                AutomationElement? dialogue = null;
                IntPtr dialogueHandle = IntPtr.Zero;
                IndependentUiaClient.Wait(() => { dialogueHandle = GetLastActivePopup(ownerHandle); dialogue = dialogueHandle == IntPtr.Zero || dialogueHandle == ownerHandle ? null : AutomationElement.FromHandle(dialogueHandle); return dialogue is not null && dialogue.Current.ProcessId == processId; });
                At("dialogue-found", new { Handle = dialogueHandle.ToInt64(), OwnerEnabled = OperationTrace.IsWindowEnabled(ownerHandle), dialogue!.Current.Name });
                var response = Stopwatch.GetTimestamp(); var ack = SendMessageTimeout(dialogueHandle, 0, IntPtr.Zero, IntPtr.Zero, 2, 100, out _);
                At("dialogue-native-response", new { Success = ack != IntPtr.Zero, Milliseconds = (Stopwatch.GetTimestamp() - response) * 1000d / Stopwatch.Frequency });
                if (ack == IntPtr.Zero || OperationTrace.IsWindowEnabled(ownerHandle)) throw new IOException("The owned dialogue is unresponsive or did not disable its owner.");
                AutomationElement? fileName = null;
                IndependentUiaClient.Wait(() => (fileName = dialogue!.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit), new PropertyCondition(AutomationElement.NameProperty, "File name:"))))?.Current.IsEnabled == true);
                fileName!.SetFocus(); IndependentUiaClient.Wait(() => fileName.Current.HasKeyboardFocus);
                clipboard.SetSentinel(path); IndependentUiaClient.Chord(0x11, 0x41); IndependentUiaClient.Chord(0x11, 0x56);
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
                if (ack == IntPtr.Zero) throw new IOException("The application did not respond immediately after dialogue closure.");
                if (cancel) { if (File.Exists(path)) throw new IOException("Cancel wrote a file."); checks.Add(format + " actual Cancel, no file and immediate owner recovery"); continue; }
                IndependentUiaClient.Wait(() => File.Exists(path) && new FileInfo(path).Length > 0);
                if (format == "SVG")
                {
                    var svg = XDocument.Load(path); if (svg.Root?.Name.LocalName != "svg" || !svg.Root.Descendants().Any()) throw new IOException("Exported SVG has no real drawing.");
                }
                else
                {
                    using var file = File.OpenRead(path); var png = BitmapDecoder.Create(file, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    if (png.Frames.Count != 1 || png.Frames[0].PixelWidth <= 0 || png.Frames[0].PixelHeight <= 0) throw new IOException("Exported PNG did not decode.");
                }
                checks.Add("Actual native " + format + " save dialogue, chosen path and decoded drawing");
                At("chosen-file-decoded", new { format, path, Bytes = new FileInfo(path).Length });
            }
            Program.Write(Path.Combine(output, "media-client.json"), new { Status = "PASS", Route = route, Checks = checks, Timeline = timeline }); return 0;
        }
        catch (Exception error) { Program.Write(Path.Combine(output, "media-client.json"), new { Status = "FAIL", Checks = checks, Timeline = timeline, Error = error.ToString() }); return 1; }
    }
}
