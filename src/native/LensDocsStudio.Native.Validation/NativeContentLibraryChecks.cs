using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using LensDocsStudio.Core.Editing;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Validation;

internal static class NativeContentLibraryChecks
{
    public static void Run(string output)
    {
        var dispatcher = Dispatcher.CurrentDispatcher; var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        try
        {
            var pending = CheckAsync(output); var frame = new DispatcherFrame(); var expired = false;
            var timer = new DispatcherTimer(DispatcherPriority.Send, dispatcher) { Interval = TimeSpan.FromSeconds(25) };
            timer.Tick += (_, _) => { expired = true; frame.Continue = false; };
            _ = pending.ContinueWith(_ => dispatcher.BeginInvoke(DispatcherPriority.Send, () => frame.Continue = false), TaskScheduler.Default);
            timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
            if (expired) throw new TimeoutException("Native content library validation exceeded its deadline.");
            pending.GetAwaiter().GetResult();
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }
    private static async Task CheckAsync(string output)
    {
        var root = Path.Combine(output, "content-library-window-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var store = new LocalLibraryStore(Path.Combine(root, "library"));
        var library = new LocalLibraryData([new("template", "Café handbook", "# Café handbook\r\n\r\nRetained **Markdown**.", 0, "handbook.md")],
            [new("snippet", "Review checklist", "- [ ] Review source\r\n- [x] Keep evidence\r\n", 0)],
            [new("profile", "Team documentation", true, new("Café site", "Retained description", "dark"), 0)]);
        var snapshot = await store.SaveAsync(await store.ReadAsync(), new(1, library, LocalLibraryData.Empty));
        foreach (var dark in new[] { false, true })
        foreach (var size in new[] { new Size(920, 650), new Size(680, 520) })
        {
            var window = new ContentLibraryWindow(store, snapshot, root);
            var content = (Grid)window.Content; window.Content = null; ThemeService.Apply(content.Resources, dark ? "Dark" : "Light");
            var surface = new Border { Background = (Brush)content.Resources["WindowBrush"], Child = content };
            System.Windows.Documents.TextElement.SetForeground(surface, (Brush)content.Resources["TextBrush"]);
            using var host = new HwndSource(new HwndSourceParameters("Native content library validation") { Width = (int)size.Width, Height = (int)size.Height, WindowStyle = unchecked((int)0x80000000) });
            host.RootVisual = surface; surface.Measure(size); surface.Arrange(new Rect(size)); surface.UpdateLayout();
            var entries = (ListBox)window.FindName("Entries"); var use = (Button)window.FindName("UseButton"); var preview = (TextBox)window.FindName("Preview");
            if (!use.IsEnabled || preview.Text != library.Templates[0].Content || entries.ActualHeight < 100
                || use.TranslatePoint(new Point(0, use.ActualHeight), surface).Y > size.Height)
                throw new IOException("The content library lost source preview, selection or accessible control layout.");
            var peer = new System.Windows.Automation.Peers.ListBoxAutomationPeer(entries);
            if (!peer.GetChildren().Any(child => child.GetName() == "Café handbook")) throw new IOException("The native content library lost accessible entry labels.");
            foreach (var label in new Control[] { (Label)window.FindName("FindLabel"), (CheckBox)window.FindName("Archived") })
                if (label.Foreground is not SolidColorBrush foreground || surface.Background is not SolidColorBrush background
                    || NativeSyntaxChecks.Contrast(foreground.Color, background.Color) < 4.5)
                    throw new IOException("The content library lost readable theme colours for " + label.Name + ".");
            var filter = (TextBox)window.FindName("Filter"); filter.Text = "missing";
            if (entries.Items.Count != 0 || use.IsEnabled) throw new IOException("Filtering the library left an unavailable entry enabled.");
            filter.Clear(); ((ComboBox)window.FindName("Kind")).SelectedIndex = 2;
            if ((string)use.Content != "Apply profile" || !preview.Text.Contains("Theme: dark", StringComparison.Ordinal)) throw new IOException("The profile category lost its readable defaults or action.");
            ((ComboBox)window.FindName("Kind")).SelectedIndex = 0; surface.UpdateLayout();
            var capture = new RenderTargetBitmap((int)size.Width, (int)size.Height, 96, 96, PixelFormats.Pbgra32); capture.Render(surface);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(capture));
            using (var file = File.Create(Path.Combine(output, "native-content-library-" + (dark ? "dark" : "light") + "-" + size.Width + ".png"))) encoder.Save(file);
            host.RootVisual = null; surface.Child = null; window.Close();
        }
        var defaults = library.Profiles[0].DocsSite!; var options = new DocsSiteWindow("Ignored fallback", defaults);
        if (options.Options.Title != defaults.Title || options.Options.Description != defaults.Description || options.Options.Theme != defaults.Theme)
            throw new IOException("The Docs Site options did not apply the selected profile defaults.");
        options.Close();
        using var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(root, "recovery")), new SessionStore(root), new WorkspaceService(), new DesktopDialogs());
        model.NewDocumentCommand.Execute(null); var original = model.ActiveDocument!; original.EditorDocument.Text = "Unsaved original";
        await model.OpenLibraryTemplateAsync(library.Templates[0]);
        if (model.Documents.Count != 2 || original.EditorDocument.Text != "Unsaved original" || model.ActiveDocument!.FilePath is not null
            || model.ActiveDocument.EditorDocument.Text != library.Templates[0].Content || !model.ActiveDocument.IsDirty)
            throw new IOException("Opening a library template replaced an existing document or lost unsaved source.");
        var mermaid = new LocalLibraryItem("mermaid", "Editable diagram", "flowchart LR\nA --> B", 0, "diagram.mmd");
        await model.OpenLibraryTemplateAsync(mermaid);
        if (Path.GetExtension(model.ActiveDocument!.EffectivePath) != ".mmd" || model.ActiveDocument.EditorDocument.Text != mermaid.Content)
            throw new IOException("A standalone Mermaid template lost its native draft document type.");
        var session = await new SessionStore(root).ReadAsync<SessionState>("session.json");
        if (session?.Documents.Count != 3 || !session.Documents.Any(document => document.Text == "Unsaved original")) throw new IOException("Template creation did not persist the complete existing session.");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { await model.OpenLibraryTemplateAsync(library.Templates[0], cancelled.Token); throw new InvalidOperationException("Cancelled template opening completed."); }
        catch (OperationCanceledException) { }
        if (model.Documents.Count != 3) throw new IOException("Cancelled template opening added a partial document.");
        var editor = new TextEditor { Text = "Before\r\n\r\nAfter" }; var before = editor.Text; editor.Document.UndoStack.ClearAll(); editor.Select(8, 0);
        var snippet = library.Snippets[0].Content;
        EditorFormattingService.Apply(editor, MarkdownEditService.PlanBlock(editor.Text, 8, 0, snippet, snippet.Trim('\r', '\n')));
        var inserted = editor.Text;
        if (editor.SelectedText != snippet.Trim('\r', '\n')) throw new IOException("Snippet insertion did not select its complete editable content.");
        editor.Document.UndoStack.Undo(); if (editor.Text != before) throw new IOException("Snippet insertion did not form one Undo action.");
        editor.Document.UndoStack.Redo(); if (editor.Text != inserted) throw new IOException("Snippet insertion Redo did not restore the complete snippet.");
        File.WriteAllText(Path.Combine(output, "native-content-library.json"), JsonSerializer.Serialize(new
        {
            Passed = true, Checks = new[] { "Light/dark content library retains accessible controls and source previews at normal and minimum size",
                "Category/filter actions select available entries and expose readable export-profile defaults", "Docs Site defaults apply without a document or preference write",
                "Templates create new unsaved tabs, persist existing session content and retain standalone Mermaid type; cancellation adds no partial tab",
                "Snippet insertion selects editable content and retains complete Undo/Redo" }, Limitation = "Native interaction and file dialogues require desktop CI."
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
