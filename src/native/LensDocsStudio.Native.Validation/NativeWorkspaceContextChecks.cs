using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LensDocsStudio.Core.Parsing;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.Rendering;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Validation;

internal static class NativeWorkspaceContextChecks
{
    public static void Run(string output)
    {
        Directory.CreateDirectory(output);
        _ = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        try
        {
            var fixture = Path.Combine(output, "workspace-context-" + Guid.NewGuid().ToString("N"));
            var first = Path.Combine(fixture, "first"); var second = Path.Combine(fixture, "second");
            Directory.CreateDirectory(Path.Combine(first, "docs")); Directory.CreateDirectory(Path.Combine(first, "assets")); Directory.CreateDirectory(second);
            var source = Path.Combine(first, "docs", "note.md"); var other = Path.Combine(first, "other.md");
            var secondSource = Path.Combine(second, "index.md"); var state = Path.Combine(fixture, "state");
            const string original = "# Prior Café\n\n![Retained pixel](../assets/pixel.png)\n\n[Other](../other.md)\n";
            const string draft = original + "\nUnsaved Ω remains available.\n";
            File.WriteAllText(source, original); File.WriteAllText(other, "# Other document"); File.WriteAllText(secondSource, "# Second workspace");
            var imagePath = Path.Combine(first, "assets", "pixel.png");
            var bitmap = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[] { 200, 100, 40, 255 }, 4);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var imageFile = File.Create(imagePath)) encoder.Save(imageFile);
            var imageBytes = File.ReadAllBytes(imagePath);
            using var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(state, "write-recovery")), new SessionStore(state), new WorkspaceService(), new DesktopDialogs());
            Complete(model.SetWorkspaceAsync(first)); Complete(model.OpenPathAsync(source));
            var tab = model.ActiveDocument ?? throw new IOException("The first document did not open.");
            var observations = new List<Observation> { Observe("Initial workspace", original) };
            tab.EditorDocument.Text = draft;
            Complete(model.SetWorkspaceAsync(second)); Complete(model.OpenPathAsync(secondSource));
            observations.Add(Observe("After switching workspace", original));
            var draftRetained = tab.EditorDocument.Text == draft && tab.IsDirty;
            var saved = model.SaveTabAsync(tab, false); Complete(saved);
            observations.Add(Observe("After normal save", draft));
            Complete(model.PersistSessionAsync());
            using var session = JsonDocument.Parse(File.ReadAllText(Path.Combine(state, "session.json")));
            var recovered = session.RootElement.GetProperty("Documents").EnumerateArray().Single(entry => entry.GetProperty("Path").GetString() == source);
            var recoveryRootRetained = recovered.TryGetProperty("WorkspaceRoot", out var root) && root.GetString() == first;
            var passed = observations.All(item => item.Passed) && draftRetained && saved.Result && recoveryRootRetained
                && File.ReadAllBytes(imagePath).SequenceEqual(imageBytes) && File.ReadAllText(other) == "# Other document";
            File.WriteAllText(Path.Combine(output, "native-workspace-context.json"), JsonSerializer.Serialize(new
            {
                Passed = passed, Fixture = fixture, ExpectedRoot = first, ActiveRoot = model.WorkspaceRoot,
                Observations = observations, DraftRetained = draftRetained, Saved = saved.Result, RecoveryRootRetained = recoveryRootRetained,
                ImageBytesRetained = File.ReadAllBytes(imagePath).SequenceEqual(imageBytes),
                Limitation = "Windowless view-model calls and saved session fields; actual restarted desktop behaviour is verified separately."
            }, new JsonSerializerOptions { WriteIndented = true }));
            if (!passed) throw new IOException("An existing tab lost its workspace context, linked content, draft or saved recovery context.");

            Observation Observe(string stage, string expectedSaved)
            {
                var prepared = new PreviewImageService().Prepare(new MarkdownService().Parse(tab.EditorDocument.Text), tab.FilePath, tab.EffectiveWorkspace).Values.Single();
                string? linkError = null; var linkResolved = false;
                try { linkResolved = File.ReadAllText(new WorkspacePath(tab.EffectiveWorkspace!).ResolveLink(source, "../other.md")) == "# Other document"; }
                catch (Exception ex) when (ex is IOException or ArgumentException) { linkError = ex.Message; }
                var sourcePreserved = File.ReadAllText(source) == expectedSaved;
                return new(stage, tab.EffectiveWorkspace, prepared.Pixels is not null, prepared.Error, linkResolved, linkError, sourcePreserved,
                    string.Equals(tab.EffectiveWorkspace, first, StringComparison.OrdinalIgnoreCase) && prepared.Pixels is not null && linkResolved && sourcePreserved);
            }
        }
        finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
    }

    private sealed record Observation(string Stage, string? ActualRoot, bool ImageResolved, string? ImageError, bool LinkResolved,
        string? LinkError, bool SavedSourceExpected, bool Passed);

    private static void Complete(Task task)
    {
        var timer = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            if (timer.Elapsed > TimeSpan.FromSeconds(25)) throw new TimeoutException("The workspace context operation exceeded its fixture deadline.");
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () => frame.Continue = false);
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }
}
