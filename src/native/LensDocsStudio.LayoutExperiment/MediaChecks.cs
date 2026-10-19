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
    public static async Task Run(ExperimentWindow window, string output)
    {
        var fixture = ContinuousPreviewFixtures.Create(output, "mixed")[0]; var path = Path.Combine(output, "source.md");
        File.WriteAllText(path, fixture.Source); window.SourcePath = path; window.WorkspaceRoot = output;
        await window.Request(fixture.Source);
        var gaps = new List<double>(); var watch = Stopwatch.StartNew(); var last = 0d;
        var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) => { var at = watch.Elapsed.TotalMilliseconds; gaps.Add(at - last); last = at; }; timer.Start();
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "--media-client", output, Environment.ProcessId.ToString() }) start.ArgumentList.Add(argument);
        using var client = (await Task.Run(() => Process.Start(start)))!;
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60)); await client.WaitForExitAsync(deadline.Token);
            if (client.ExitCode != 0 || window.Surface.Snapshot?.Model.Source != fixture.Source || File.ReadAllText(path) != fixture.Source) throw new IOException("Independent media export or source currency failed.");
            Program.Write(Path.Combine(output, "media.json"), new { Status = "PASS", SourceUnchanged = true, MaximumInputGapMilliseconds = gaps.DefaultIfEmpty().Max(), InputGapsMilliseconds = gaps, InteractionStages = window.Surface.InteractionStages, Events = window.Events.ToArray() });
        }
        catch (Exception error)
        {
            if (!client.HasExited) client.Kill(true);
            Program.Write(Path.Combine(output, "media.json"), new { Status = "FAIL", Error = error.ToString(), MaximumInputGapMilliseconds = gaps.DefaultIfEmpty().Max(), InputGapsMilliseconds = gaps, InteractionStages = window.Surface.InteractionStages, Events = window.Events.ToArray() }); throw;
        }
        finally { timer.Stop(); }
        WindowCapture.Save(window, Path.Combine(output, "shown-media.png"));
    }
    public static int Client(string output, int processId)
    {
        var checks = new List<string>(); using var clipboard = new IndependentUiaClient.ClipboardOwner();
        try
        {
            using var process = Process.GetProcessById(processId); var owner = AutomationElement.FromHandle(process.MainWindowHandle);
            var document = owner.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, "ExperimentalDocument"))!;
            var media = document.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, "Mermaid diagram"))!;
            var text = (TextPattern)document.GetCurrentPattern(TextPattern.Pattern); text.RangeFromChild(media).ScrollIntoView(true);
            var commands = media.FindAll(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)).Cast<AutomationElement>().ToArray();
            foreach (var format in new[] { "SVG", "PNG" })
            {
                var path = Path.GetFullPath(Path.Combine(output, "actual-dialogue-export." + format.ToLowerInvariant()));
                if (File.Exists(path)) throw new IOException("The independent export requires a fresh output path.");
                ((InvokePattern)commands.Single(command => command.Current.Name == format).GetCurrentPattern(InvokePattern.Pattern)).Invoke();
                AutomationElement? dialogue = null;
                IndependentUiaClient.Wait(() => { var handle = FindWindow(null, "EXPERIMENT ONLY — Export diagram"); dialogue = handle == IntPtr.Zero ? null : AutomationElement.FromHandle(handle); return dialogue is not null && dialogue.Current.ProcessId == processId; });
                AutomationElement? fileName = null;
                IndependentUiaClient.Wait(() => (fileName = dialogue!.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit), new PropertyCondition(AutomationElement.NameProperty, "File name:"))))?.Current.IsEnabled == true);
                fileName!.SetFocus(); IndependentUiaClient.Wait(() => fileName.Current.HasKeyboardFocus);
                clipboard.SetSentinel(path); IndependentUiaClient.Chord(0x11, 0x41); IndependentUiaClient.Chord(0x11, 0x56);
                IndependentUiaClient.Wait(() => ((ValuePattern)fileName.GetCurrentPattern(ValuePattern.Pattern)).Current.Value == path);
                var save = dialogue!.FindFirst(TreeScope.Descendants, new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button), new PropertyCondition(AutomationElement.AutomationIdProperty, "1")))!;
                Program.Write(Path.Combine(output, "save-dialogue-" + format + ".json"), new { FileName = ((ValuePattern)fileName.GetCurrentPattern(ValuePattern.Pattern)).Current.Value, SaveName = save.Current.Name, save.Current.AutomationId, save.Current.IsEnabled });
                if (!save.Current.Name.Contains("Save", StringComparison.OrdinalIgnoreCase)) throw new IOException("The selected dialogue command is not Save.");
                IndependentUiaClient.Chord(0x0D); // Actual Enter commits the filename edit and default Save command.
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
            }
            Program.Write(Path.Combine(output, "media-client.json"), new { Status = "PASS", Checks = checks }); return 0;
        }
        catch (Exception error) { Program.Write(Path.Combine(output, "media-client.json"), new { Status = "FAIL", Checks = checks, Error = error.ToString() }); return 1; }
    }
}
