using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LensDocsStudio.Core.Rendering;
using LensDocsStudio.Native.Rendering;

namespace LensDocsStudio.LayoutExperiment;

internal sealed class MediaActions
{
    private readonly ExperimentWindow owner;
    private Window? zoom;
    private CancellationTokenSource lifetime = new();
    public MediaActions(ExperimentWindow owner)
    {
        this.owner = owner; owner.Surface.MediaAction += Act;
        owner.PreparationRequested += () => { lifetime.Cancel(); lifetime.Dispose(); lifetime = new(); };
        owner.Installed += _ => { zoom?.Close(); lifetime.Cancel(); lifetime.Dispose(); lifetime = new(); };
        owner.Closed += (_, _) => { zoom?.Close(); lifetime.Cancel(); lifetime.Dispose(); };
    }
    private async void Act(ControlBox box)
    {
        var snapshot = owner.Surface.Snapshot;
        if (snapshot is null || snapshot.Version != owner.CurrentVersion || snapshot.Model.Source != owner.LatestSource) return;
        var cancellation = lifetime.Token;
        if (snapshot?.Model.Root.Descendants().FirstOrDefault(node => node.Id == box.Node && node.Kind == "Media") is not { Media: { } image, Target: { } source }) return;
        try
        {
            owner.Trace?.Stage("media-command-enter", new { box.Label, snapshot.Version, ClipboardSequence = LensDocsStudio.Native.Platform.NativeClipboardService.Sequence });
            if (box.Label == "Zoom")
            {
                zoom?.Close();
                var visual = new Image { Source = image, Stretch = Stretch.Uniform, Margin = new Thickness(16) };
                System.Windows.Automation.AutomationProperties.SetName(visual, "Mermaid diagram");
                zoom = new Window { Owner = owner, Title = "EXPERIMENT ONLY — Mermaid zoom", Width = 900, Height = 650,
                    Content = new ScrollViewer { Content = visual } };
                zoom.Show(); return;
            }
            if (box.Label == "Copy source")
            {
                var sequence = LensDocsStudio.Native.Platform.NativeClipboardService.Sequence;
                await owner.Surface.ClipboardWorker.Plain(source, sequence, cancellation); return;
            }
            var dialog = new Microsoft.Win32.SaveFileDialog { Title = "EXPERIMENT ONLY — Export diagram", FileName = "diagram." + box.Label.ToLowerInvariant(),
                Filter = box.Label == "SVG" ? "SVG drawing|*.svg" : "PNG image|*.png", AddExtension = true };
            owner.Trace?.Stage("dialogue-enter", new { box.Label, Thread = Environment.CurrentManagedThreadId, OwnerHandle = new System.Windows.Interop.WindowInteropHelper(owner).Handle.ToInt64() });
            bool? accepted;
            try { accepted = dialog.ShowDialog(owner); }
            finally { owner.Trace?.Stage("dialogue-exit", new { box.Label }); }
            owner.Trace?.Stage("dialogue-result", new { box.Label, Accepted = accepted, dialog.FileName });
            if (accepted != true) return;
            var output = dialog.FileName;
            owner.Surface.InteractionStages.Add(new { Stage = "media-save-accepted", box.Label, Output = output, Version = snapshot.Version, Timestamp = System.Diagnostics.Stopwatch.GetTimestamp() });
            var bytes = await ExperimentWindow.Worker(() =>
            {
                owner.Trace?.Stage("media-encoding-enter", new { box.Label });
                try
                {
                    if (box.Label == "PNG") return NativeSvgService.ToPng(image);
                    var diagram = new DiagramService().Render(source, ExperimentAppearance.IsDark(snapshot.Appearance));
                    owner.Trace?.Stage("media-svg-generated", new { box.Label });
                    return System.Text.Encoding.UTF8.GetBytes(new NativeSvgService().Prepare(diagram.Svg ?? throw new IOException(diagram.Error)));
                }
                finally { owner.Trace?.Stage("media-encoding-exit", new { box.Label }); }
            });
            owner.Trace?.Stage("media-worker-delivered", new { box.Label, Bytes = bytes.Length });
            cancellation.ThrowIfCancellationRequested();
            if (!ReferenceEquals(owner.Surface.Snapshot, snapshot) || snapshot.Version != owner.CurrentVersion) throw new IOException("The diagram changed before export completed.");
            owner.Trace?.Stage("media-write-enter", new { box.Label, Output = output });
            await File.WriteAllBytesAsync(output, bytes, cancellation);
            owner.Trace?.Stage("media-write-exit", new { box.Label });
            owner.Surface.InteractionStages.Add(new { Stage = "media-save-completed", box.Label, Output = output, Bytes = bytes.Length, Timestamp = System.Diagnostics.Stopwatch.GetTimestamp() });
        }
        catch (Exception error) when (error is IOException or OperationCanceledException or System.Runtime.InteropServices.ExternalException)
        { owner.Surface.InteractionStages.Add(new { Stage = "media-action-failure", box.Label, Error = error.Message }); }
        finally { owner.Trace?.Stage("media-command-exit", new { box.Label }); }
    }
}
