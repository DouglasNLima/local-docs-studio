using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Controls;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.UxProbe;
using LensDocsStudio.Native.Validation;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Qualification;

internal static class ApplicationObservation
{
    private const int WaitSeconds = 20;
    private static async Task<T> Ui<T>(Window window, Func<T> action) => await window.Dispatcher.InvokeAsync(action, DispatcherPriority.Input).Task.WaitAsync(TimeSpan.FromSeconds(WaitSeconds));
    private static async Task Until(Window window, Func<bool> predicate)
    {
        var start = Stopwatch.GetTimestamp();
        do { if (await Ui(window, predicate)) return; await Task.Delay(8); } while (Stopwatch.GetElapsedTime(start).TotalSeconds < WaitSeconds);
        throw new TimeoutException("Original 20-second UI deadline exceeded.");
    }
    private static IEnumerable<DependencyObject> Tree(DependencyObject node)
    {
        yield return node;
        if (node is Visual or System.Windows.Media.Media3D.Visual3D)
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
                foreach (var child in Tree(VisualTreeHelper.GetChild(node, i))) yield return child;
    }
    private static Rect Bounds(FrameworkElement element) => new(element.PointToScreen(new Point()), element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight)));
    private static string Colour(Brush brush) => brush is SolidColorBrush solid && solid.Color.A == 255 && solid.Opacity == 1
        ? solid.Color.ToString() : throw new IOException("Actual resolved opaque solid brush is unavailable.");
    private static string BufferHash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    internal static int Run(string fixtures)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
        ThemeService.Apply(app.Resources, "System"); // Application control observation, distinct from the original preflight.
        var state = Path.Combine(Program.Output, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new UserPreferences(Theme: "System", Layout: "Editor", AutomaticUpdateChecks: false, ShowOutline: false)));
        var source = File.ReadAllBytes(Path.Combine(fixtures, "mixed.md"));
        var path = Path.Combine(Program.Output, "mixed.md"); File.WriteAllBytes(path, source);
        File.Copy(Path.Combine(fixtures, "pixel.png"), Path.Combine(Program.Output, "pixel.png"));
        var dialogs = new DesktopDialogs();
        var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(state, "writes")), new SessionStore(state), new WorkspaceService(), dialogs);
        if (SystemParameters.WorkArea.Width < 1280 || SystemParameters.WorkArea.Height < 800) throw new IOException("The fixed viewport does not fit this host; no smaller substitute.");
        var window = new MainWindow(model, dialogs, [path]) { Width = 1280, Height = 800, Left = SystemParameters.WorkArea.Left, Top = SystemParameters.WorkArea.Top,
            WindowStartupLocation = WindowStartupLocation.Manual };
        var code = 1; var observations = new List<object>(); var notifications = new List<object>(); bool notificationOverflow = false;
        HwndSource? hwnd = null;
        nint Hook(nint handle, int message, nint wparam, nint lparam, ref bool handled)
        {
            if (message is 0x001a or 0x031a or 0x0320)
            {
                if (notifications.Count < 1024) notifications.Add(new { processId = Environment.ProcessId, tick = Stopwatch.GetTimestamp(), message });
                else notificationOverflow = true;
            }
            return 0; // Passive; never mark a real Windows notification handled.
        }
        window.Show();
        _ = Task.Run(async () =>
        {
            bool held = false; nint handle = 0;
            try
            {
                await Until(window, () => window.IsLoaded && !model.IsBusy && model.ActiveDocument?.EditorDocument.Text == Encoding.UTF8.GetString(source));
                handle = await Ui(window, () => { window.Activate(); var h = new WindowInteropHelper(window).Handle; Desktop.SetForegroundWindow(h); hwnd = HwndSource.FromHwnd(h); hwnd.AddHook(Hook); return h; });
                Desktop.OwnsForeground();
                var editor = await Ui(window, () => (TextEditor)window.FindName("Editor"));
                var button = await Ui(window, () => Tree(window).OfType<Button>().Single(b => b.IsVisible && b.IsEnabled && AutomationProperties.GetName(b) == "New document (Ctrl+N)"));
                await Until(window, () => editor.IsVisible && editor.IsMeasureValid && editor.IsArrangeValid && button.IsArrangeValid);
                Desktop.Move(await Ui(window, () => { var b = Bounds(editor); return new Point(b.Left + b.Width / 2, b.Top + b.Height / 2); }));
                Desktop.Click(); await Until(window, () => editor.IsKeyboardFocusWithin);
                Desktop.Key(0x24, 0x11); // Ctrl+Home, then a fixed non-empty selection; no source edit.
                for (int i = 0; i < 8; i++) Desktop.Key(0x27, 0x10);
                await Until(window, () => editor.SelectionStart == 0 && editor.SelectionLength == 8);
                var original = await Ui(window, () => new { hash = BufferHash(editor.Text), editor.SelectionStart, editor.SelectionLength,
                    caret = editor.CaretOffset, dirty = model.ActiveDocument!.IsDirty, id = model.ActiveDocument.Id, count = model.Documents.Count });
                var fileHash = Program.Hash(path);
                foreach (var phase in new[] { "initial-hc", "normal", "returned-hc" })
                {
                    var expectedHc = phase != "normal";
                    var transitionStarted = Stopwatch.GetTimestamp();
                    if (phase != "initial-hc") Appearance.Set(expectedHc); // Actual OS API and broadcast; no synthetic notification.
                    await Until(window, () => SystemParameters.HighContrast == expectedHc && button.IsVisible && button.IsArrangeValid);
                    var before = await Ui(window, () => new { tick = Stopwatch.GetTimestamp(), actualHc = Appearance.Read().Enabled, wpfHc = SystemParameters.HighContrast,
                        source = BufferHash(editor.Text), focus = editor.IsKeyboardFocusWithin, editor.SelectionStart, editor.SelectionLength,
                        window.ActualWidth, window.ActualHeight, dpi = VisualTreeHelper.GetDpi(window).PixelsPerInchX });
                    if (before.ActualWidth != 1280 || before.ActualHeight != 800 || before.source != original.hash || !before.focus ||
                        before.SelectionStart != original.SelectionStart || before.SelectionLength != original.SelectionLength)
                        throw new IOException("Fixed geometry, source, focus or selection changed before press.");
                    Desktop.Move(await Ui(window, () => { var b = Bounds(button); if (!Desktop.ClientBounds(handle).Contains(b)) throw new IOException("Pressed target clipped."); return new Point(b.Left + b.Width / 2, b.Top + b.Height / 2); }));
                    await Until(window, () => button.IsMouseOver);
                    var inputTick = Stopwatch.GetTimestamp(); Desktop.LeftDown(); held = true;
                    await Until(window, () => button.IsPressed);
                    var pair = await Ui(window, () =>
                    {
                        var surface = button.Template.FindName("Surface", button) as Border ?? throw new IOException("Actual template surface unavailable.");
                        var icon = button.Content as CommandIcon ?? throw new IOException("Actual New command icon unavailable.");
                        if (icon.Kind != "New" || !button.IsPressed || icon.Opacity != 1 || button.Opacity != 1 || surface.Opacity != 1)
                            throw new IOException("Pressed icon/state is not the fixed opaque control.");
                        return new { tick = Stopwatch.GetTimestamp(), processId = Environment.ProcessId, pressed = button.IsPressed,
                            background = Colour(surface.Background), foreground = Colour(button.Foreground), iconForeground = Colour(icon.Foreground),
                            name = AutomationProperties.GetName(button), uiaName = UIElementAutomationPeer.CreatePeerForElement(button)?.GetName(),
                            kind = icon.Kind, focus = editor.IsKeyboardFocusWithin, bounds = Bounds(button).ToString(),
                            highlightText = SystemColors.HighlightTextColor.ToString() };
                    });
                    var frame = Desktop.Capture(Desktop.ClientBounds(handle));
                    var file = phase + "-pressed.png"; Desktop.Save(frame, Path.Combine(Program.Output, file));
                    var stillPressed = await Ui(window, () => button.IsPressed && Colour(((Border)button.Template.FindName("Surface", button)).Background) == pair.background && Colour(button.Foreground) == pair.foreground);
                    var exit = Desktop.ClientBounds(handle); Desktop.Move(new Point(exit.Right - 4, exit.Bottom - 4));
                    await Until(window, () => !button.IsMouseOver); Desktop.LeftUp(); held = false;
                    await Until(window, () => !button.IsPressed);
                    var after = await Ui(window, () => new { tick = Stopwatch.GetTimestamp(), source = BufferHash(editor.Text), focus = editor.IsKeyboardFocusWithin,
                        editor.SelectionStart, editor.SelectionLength, caret = editor.CaretOffset, dirty = model.ActiveDocument!.IsDirty,
                        id = model.ActiveDocument.Id, count = model.Documents.Count });
                    var ratio = NativeSyntaxChecks.Contrast((Color)ColorConverter.ConvertFromString(pair.foreground), (Color)ColorConverter.ConvertFromString(pair.background));
                    var iconRatio = NativeSyntaxChecks.Contrast((Color)ColorConverter.ConvertFromString(pair.iconForeground), (Color)ColorConverter.ConvertFromString(pair.background));
                    var preserved = after.source == original.hash && after.SelectionStart == original.SelectionStart && after.SelectionLength == original.SelectionLength &&
                        after.caret == original.caret && after.dirty == original.dirty && after.id == original.id && after.count == original.count && after.focus && Program.Hash(path) == fileHash;
                    var passed = preserved && stillPressed && pair.focus && ratio >= 4.5 && iconRatio >= 4.5 &&
                        (!expectedHc || pair.foreground == pair.highlightText && pair.iconForeground == pair.highlightText);
                    observations.Add(new { phase, processId = Environment.ProcessId, expectedHc, transitionStarted, inputTick, before, pair, after,
                        file, pngSha256 = Program.Hash(Path.Combine(Program.Output, file)), frame.Sha256, frame.Started, frame.DwmReturned, frame.PixelsStarted, frame.PixelsFinished,
                        stillPressed, preserved, ratio, iconRatio, requirement = 4.5, acceptance = passed ? "PASS" : "FAIL" });
                    Program.Write(phase + ".json", observations[^1]);
                    if (!passed) throw new IOException("Actual pressed control, contrast, focus, selection or source acceptance failed.");
                }
                code = 0;
            }
            catch (Exception error) { Program.Write("application-failure.json", new { processId = Environment.ProcessId, error = error.ToString(), acceptance = "FAIL" }); }
            finally
            {
                try
                {
                    if (held)
                    {
                        var area = Desktop.ClientBounds(handle);
                        Desktop.Move(new Point(area.Right - 4, area.Bottom - 4));
                        Desktop.LeftUp();
                    }
                }
                catch (Exception error) { code = 1; Program.Write("input-cleanup-failure.json", new { error = error.ToString() }); }
                await Ui(window, () => { hwnd?.RemoveHook(Hook); return true; });
                if (notificationOverflow) code = 1;
                Program.Write("result.json", new { mode = "application", processId = Environment.ProcessId, acceptance = code == 0 ? "PASS" : "FAIL",
                    observations, notifications, notificationOverflow, sourceSha256 = Convert.ToHexString(SHA256.HashData(source)),
                    scope = "Exact compiled MainWindow hosted by validation; not installed-app startup certification; fixed software captures, no compositor guarantee" });
                await window.Dispatcher.InvokeAsync(() => app.Shutdown(code));
            }
        });
        return app.Run();
    }
}
