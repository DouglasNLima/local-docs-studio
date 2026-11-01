using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using ICSharpCode.AvalonEdit;
using LensDocsStudio.Core.State;
using LensDocsStudio.Core.Workspace;
using LensDocsStudio.Native.Controls;
using LensDocsStudio.Native.Platform;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Validation;

// Compiled-XAML routed-event checks. No OS input or visible window; portable GUI
// acceptance remains a separate gate on the independently verified package.
internal static class NativeFindRoutingChecks
{
    public static void Run(string output)
    {
        SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));
        if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Fresh Find event evidence required.");
        Directory.CreateDirectory(output);
        var state = Path.Combine(output, "state");
        var dialogs = new DesktopDialogs();
        using var model = new WorkspaceViewModel(new DocumentStore(Path.Combine(state, "writes")), new SessionStore(state), new WorkspaceService(), dialogs);
        var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        ThemeService.Apply(app.Resources, "Light");
        var window = new MainWindow(model, dialogs, []);
        var checks = new List<string>();
        try
        {
            var editor = (TextEditor)window.FindName("Editor");
            var query = (TextBox)window.FindName("FindText");
            var bar = (FrameworkElement)window.FindName("FindBar");
            const string source = "# Café\n\nCafé second\n";
            editor.Document.Text = source; query.Text = "Café"; bar.Visibility = Visibility.Visible;
            // An unshown control isolates the pinned AvalonEdit peer lifecycle. This
            // proves why polling only the parent's Text pattern cannot create it.
            var isolatedEditor = new MarkdownEditor { Text = source };
            var parentPeer = UIElementAutomationPeer.CreatePeerForElement(isolatedEditor);
            Check(parentPeer.GetPattern(PatternInterface.Text) is null,
                "Fresh AvalonEdit parent has no Text provider before its child peer exists");
            var childPeer = UIElementAutomationPeer.CreatePeerForElement(isolatedEditor.TextArea);
            var provider = parentPeer.GetPattern(PatternInterface.Text);
            Check(ReferenceEquals(provider, childPeer) && provider is ITextProvider textProvider
                && textProvider.DocumentRange.GetText(-1) == source && isolatedEditor.Text == source
                && isolatedEditor.SelectionStart == 0 && isolatedEditor.SelectionLength == 0,
                "Child peer discovery exposes the exact source Text provider without input or source mutation");
            using var host = new HwndSource(new HwndSourceParameters("Hidden Find event test") { Width = 1, Height = 1, WindowStyle = unchecked((int)0x80000000) });
            var enter = RaiseKey(query, Keyboard.PreviewKeyDownEvent, Key.Enter);
            PumpUntil(() => editor.SelectionLength == 4);
            Check(enter.Handled && editor.SelectionStart == 2 && editor.SelectionLength == 4 && editor.SelectedText == "Café", "Compiled FindText preview route selects the first exact Unicode occurrence once");
            var bubble = RaiseKey(query, Keyboard.KeyDownEvent, Key.Enter, enter.Handled);
            Check(bubble.Handled && editor.SelectionStart == 2, "Handled bubbling phase cannot invoke a duplicate search");
            var next = RaiseKey(query, Keyboard.PreviewKeyDownEvent, Key.Enter);
            PumpUntil(() => editor.SelectionStart == 8);
            Check(next.Handled && editor.SelectionStart == 8 && editor.SelectedText == "Café", "A separate next Enter advances exactly one occurrence");
            var editorEnter = RaiseKey(editor.TextArea, Keyboard.PreviewKeyDownEvent, Key.Enter);
            Check(!editorEnter.Handled && editor.SelectionStart == 8 && editor.Document.Text == source, "Visible Find bar does not intercept editor preview Enter or mutate source");
            var ordinary = RaiseKey(query, Keyboard.PreviewKeyDownEvent, Key.A);
            Check(!ordinary.Handled && editor.SelectionStart == 8, "Ordinary query typing is not handled as navigation");
            editor.Select(source.Length, 0);
            editor.Document.Insert(editor.CaretOffset, "x");
            var refresh = (Task<bool>)typeof(MainWindow).GetMethod("RefreshFindAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!;
            PumpUntil(() => refresh.IsCompleted);
            Check(refresh.GetAwaiter().GetResult() && editor.CaretOffset == source.Length + 1 && editor.SelectionLength == 0
                && editor.Document.Text == source + "x", "Actual AvalonEdit document edit refreshes matches without selecting or revealing a result");
            editor.Document.UndoStack.Undo();
            Refresh();
            Check(editor.Document.Text == source && editor.SelectionLength == 0 && editor.CaretOffset == source.Length,
                "Undo refresh retains the natural AvalonEdit caret");
            editor.Document.UndoStack.Redo(); Refresh();
            Check(editor.Document.Text == source + "x" && editor.CaretOffset == source.Length + 1 && editor.SelectionLength == 0,
                "Redo refresh retains exact source and caret ownership");
            editor.Document.UndoStack.Undo();
            foreach (var character in " text Café")
            {
                editor.Document.Insert(editor.CaretOffset, character.ToString());
                var offset = editor.CaretOffset; Refresh();
                Check(editor.CaretOffset == offset && editor.SelectionLength == 0,
                    "Sequential AvalonEdit character refresh leaves caret at " + offset);
            }
            var counter = (TextBlock)window.FindName("FindCounter");
            Check(counter.Text == "0 of 3", "Inserted accented occurrence updates the total without an active result");
            editor.Document.Remove(editor.CaretOffset - 1, 1); Refresh();
            Check(counter.Text == "0 of 2" && editor.SelectionLength == 0, "Backspace-style deletion removes the occurrence without navigation");
            editor.Select(source.Length, 0); editor.Document.Remove(editor.CaretOffset, 1); Refresh();
            Check(editor.CaretOffset == source.Length && editor.SelectionLength == 0, "Delete-style removal preserves the insertion point");
            // The worker cannot publish while this dispatcher callback owns the thread.
            query.Text = ""; query.Text = "Café";
            var pending = (Task)typeof(MainWindow).GetMethod("NavigateFindAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [1])!;
            editor.Document.Insert(editor.CaretOffset, "newer edit");
            var newerCaret = editor.CaretOffset;
            PumpUntil(() => pending.IsCompleted); pending.GetAwaiter().GetResult(); Refresh();
            Check(editor.CaretOffset == newerCaret && editor.SelectionLength == 0 && counter.Text == "0 of 2",
                "A newer source version cancels an explicit navigation awaiting its worker");
            query.Text = "Caf";
            pending = (Task)typeof(MainWindow).GetMethod("NavigateFindAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [-1])!;
            RaiseKey(editor.TextArea, Keyboard.PreviewKeyDownEvent, Key.Left);
            editor.CaretOffset = 0;
            PumpUntil(() => pending.IsCompleted); pending.GetAwaiter().GetResult();
            Check(editor.CaretOffset == 0 && editor.SelectionLength == 0,
                "New editor keyboard interaction cancels delayed navigation even without a source edit");
            editor.Document.Text = source; editor.Select(source.Length, 0);
            query.Text = ""; query.Text = "Café";
            pending = (Task)typeof(MainWindow).GetMethod("NavigateFindAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [1])!;
            RaiseKey(editor.TextArea, Keyboard.PreviewKeyDownEvent, Key.V, handled: true);
            PumpUntil(() => pending.IsCompleted); pending.GetAwaiter().GetResult();
            Check(editor.CaretOffset == source.Length && editor.SelectionLength == 0 && editor.Document.Text == source,
                "A handled editor shortcut cancels delayed navigation before its asynchronous edit changes the document");
            query.Text = ""; query.Text = "Café";
            var routedNavigation = RaiseKey(editor.TextArea, Keyboard.PreviewKeyDownEvent, Key.F3, handled: true);
            pending = (Task)typeof(MainWindow).GetMethod("NavigateFindAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [1])!;
            editor.TextArea.RaiseEvent(routedNavigation);
            PumpUntil(() => pending.IsCompleted); pending.GetAwaiter().GetResult();
            Check(editor.SelectionStart == 2 && editor.SelectedText == "Café",
                "A second phase of the same routed key preserves its newly authorised navigation");
            editor.Document.Text = source; query.Text = "Café";
            pending = (Task)typeof(MainWindow).GetMethod("NavigateFindAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [1])!;
            PumpUntil(() => pending.IsCompleted); pending.GetAwaiter().GetResult();
            ((TextBox)window.FindName("ReplaceText")).Text = "Tea";
            typeof(MainWindow).GetMethod("ReplaceOne", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [window, new RoutedEventArgs()]);
            PumpUntil(() => editor.SelectedText == "Café" && editor.SelectionStart == 7);
            Check(editor.Document.Text == "# Tea\n\nCafé second\n" && counter.Text == "1 of 1" && editor.SelectionStart == 7,
                "Explicit Replace One advances from its replacement anchor to the remaining occurrence");
            editor.Document.UndoStack.Undo(); Refresh();
            Check(editor.Document.Text == source, "Replace One Undo restores exact Unicode source");
            editor.Select(2, 4);
            typeof(MainWindow).GetMethod("ReplaceOne", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [window, new RoutedEventArgs()]);
            editor.Select(editor.Document.TextLength, 0); editor.Document.Insert(editor.CaretOffset, "newer");
            newerCaret = editor.CaretOffset; Refresh();
            Check(editor.Document.Text == "# Tea\n\nCafé second\nnewer" && editor.CaretOffset == newerCaret && editor.SelectionLength == 0
                && counter.Text == "0 of 1", "A newer edit cancels replacement follow-up navigation without losing either edit");
            editor.Document.Text = source;
            var escape = RaiseKey(query, Keyboard.PreviewKeyDownEvent, Key.Escape);
            Check(escape.Handled && bar.Visibility == Visibility.Collapsed && editor.Document.Text == source, "FindText Escape closes the bar without changing source");
            File.WriteAllText(Path.Combine(output, "completion.json"), JsonSerializer.Serialize(new { status = "PASS", scope = "COMPILED_XAML_ROUTING_AND_UNSHOWN_AUTOMATION_PEER_LIFECYCLE; NOT PORTABLE GUI", source = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, checks }, new JsonSerializerOptions { WriteIndented = true }));

            KeyEventArgs RaiseKey(UIElement target, RoutedEvent routedEvent, Key key, bool handled = false)
            {
                var args = new KeyEventArgs(Keyboard.PrimaryDevice, host, Environment.TickCount, key) { RoutedEvent = routedEvent, Handled = handled };
                target.RaiseEvent(args); return args;
            }
            void Check(bool value, string message) { if (!value) throw new IOException(message); checks.Add(message); }
            void Refresh()
            {
                var task = (Task<bool>)typeof(MainWindow).GetMethod("RefreshFindAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null)!;
                PumpUntil(() => task.IsCompleted);
                Check(task.IsCompletedSuccessfully && task.Result, "Current immutable source refresh completes");
            }
            void PumpUntil(Func<bool> ready)
            {
                var limit = System.Diagnostics.Stopwatch.StartNew();
                while (!ready() && limit.Elapsed < TimeSpan.FromSeconds(5))
                {
                    var frame = new System.Windows.Threading.DispatcherFrame();
                    window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () => frame.Continue = false);
                    System.Windows.Threading.Dispatcher.PushFrame(frame);
                    Thread.Sleep(1);
                }
            }
        }
        finally { typeof(MainWindow).GetMethod("CompleteClose", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null); }
    }
}
