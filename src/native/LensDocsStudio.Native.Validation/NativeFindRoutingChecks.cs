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
            var escape = RaiseKey(query, Keyboard.PreviewKeyDownEvent, Key.Escape);
            Check(escape.Handled && bar.Visibility == Visibility.Collapsed && editor.Document.Text == source, "FindText Escape closes the bar without changing source");
            File.WriteAllText(Path.Combine(output, "completion.json"), JsonSerializer.Serialize(new { status = "PASS", scope = "COMPILED_XAML_ROUTING_AND_UNSHOWN_AUTOMATION_PEER_LIFECYCLE; NOT PORTABLE GUI", source = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion, runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, checks }, new JsonSerializerOptions { WriteIndented = true }));

            KeyEventArgs RaiseKey(UIElement target, RoutedEvent routedEvent, Key key, bool handled = false)
            {
                var args = new KeyEventArgs(Keyboard.PrimaryDevice, host, Environment.TickCount, key) { RoutedEvent = routedEvent, Handled = handled };
                target.RaiseEvent(args); return args;
            }
            void Check(bool value, string message) { if (!value) throw new IOException(message); checks.Add(message); }
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
