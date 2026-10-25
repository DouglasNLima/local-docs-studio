using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;

// This type is in the validation executable only, never the portable payload.
// Explicit child-process environment opts in to read-only, process-local observations.
public static class StartupHook
{
    public static void Initialize()
    {
        var root = Environment.GetEnvironmentVariable("LENS_FIND_OBSERVATION_ROOT");
        if (root is null) return;
        root = Path.GetFullPath(root);
        var args = Environment.GetCommandLineArgs();
        var stateIndex = Array.IndexOf(args, "--state-directory");
        if (stateIndex < 0 || stateIndex + 1 >= args.Length || !Path.GetFullPath(args[stateIndex + 1]).Equals(Path.Combine(root, "state"), StringComparison.OrdinalIgnoreCase)
            || !Path.GetFullPath(Environment.ProcessPath!).Equals(Path.Combine(root, "payload", "LensDocsStudio.Native.exe"), StringComparison.OrdinalIgnoreCase)
            || !root.Contains(Path.DirectorySeparatorChar + "artifacts" + Path.DirectorySeparatorChar + "native" + Path.DirectorySeparatorChar + "development-host" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Find probe requires the explicitly prepared synthetic portable process.");
        using var marker = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "host-sandbox.json")));
        var identity = marker.RootElement;
        if (identity.GetProperty("qualification").GetString() != "DEVELOPMENT_HOST / HOST_LOCAL_VALIDATION" || identity.GetProperty("cleanMachineCertified").GetBoolean()
            || !typeof(StartupHook).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.EndsWith("+" + identity.GetProperty("harnessRevision").GetString(), StringComparison.Ordinal)
            || Digest(File.ReadAllBytes(Environment.ProcessPath!)) != identity.GetProperty("executableSha256").GetString()) throw new IOException("Find probe identity mismatch.");
        var source = File.ReadAllText(Path.Combine(root, "workspace", "acceptance-lf.md"));
        var editorNewline = Environment.GetEnvironmentVariable("LENS_FIND_OBSERVATION_VARIANT") == "FindEditorEnterObserved";
        var stream = new StreamWriter(new FileStream(Path.Combine(root, "ui", "native-observations.jsonl"), FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
        var count = 0;
        void Record(object value) { if (count++ < 200) stream.WriteLine(JsonSerializer.Serialize(value)); }
        Record(new { atUtc = DateTimeOffset.UtcNow, observation = "probe-loaded", processId = Environment.ProcessId, sourceSha256 = Digest(Encoding.UTF8.GetBytes(source)), probeSha256 = Digest(File.ReadAllBytes(typeof(StartupHook).Assembly.Location)), runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription });
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            if (Application.Current?.MainWindow is not Window window || window.FindName("Editor") is not TextEditor editor || window.FindName("FindText") is not TextBox query)
            { Record(new { atUtc = DateTimeOffset.UtcNow, observation = "probe-unavailable" }); return; }
            // ApplicationIdle can precede asynchronous command-line document loading.
            // Observe that existing load; never set the document or extend the caller's
            // unchanged attach-before-input deadline. Do not record other source text.
            editor.TextChanged += AttachWhenSourceIsReady;
            window.Closed += StopPendingObservation;
            if (editor.Document.Text != source)
                Record(new { atUtc = DateTimeOffset.UtcNow, observation = "probe-waiting-for-synthetic-source" });
            AttachWhenSourceIsReady(null, EventArgs.Empty);

            void StopPendingObservation(object? sender, EventArgs e)
            {
                editor.TextChanged -= AttachWhenSourceIsReady;
                Record(new { atUtc = DateTimeOffset.UtcNow, observation = "probe-closed-before-source-ready" }); stream.Dispose();
            }
            void AttachWhenSourceIsReady(object? sender, EventArgs e)
            {
                if (editor.Document.Text != source) return;
                editor.TextChanged -= AttachWhenSourceIsReady;
                window.Closed -= StopPendingObservation;
                void Snapshot(string observation, KeyEventArgs? key = null)
                {
                    // Stop recording if the process is no longer displaying the prepared synthetic source.
                    if (editor.Document.Text != source && !(editorNewline && editor.Document.Text == "\n" + source)) { Record(new { atUtc = DateTimeOffset.UtcNow, observation = "source-differs-probe-stopped" }); count = 200; return; }
                    Record(new { atUtc = DateTimeOffset.UtcNow, observation, key = key?.Key.ToString(), handled = key?.Handled,
                        hwnd = new WindowInteropHelper(window).Handle.ToInt64(), foreground = GetForegroundWindow().ToInt64(),
                        originalSource = (key?.OriginalSource as FrameworkElement)?.Name, focus = (Keyboard.FocusedElement as FrameworkElement)?.Name,
                        query = query.Text, context = (window.FindName("FindContext") as TextBlock)?.Text,
                        selectionStart = editor.SelectionStart, selectionLength = editor.SelectionLength, selectedText = editor.SelectedText, caretOffset = editor.CaretOffset,
                        sourceSha256 = Digest(Encoding.UTF8.GetBytes(editor.Document.Text)),
                        previewSelection = (window.FindName("Preview") as FlowDocumentReader)?.Selection?.Text,
                        focusInPreview = (window.FindName("Preview") as FlowDocumentReader)?.IsKeyboardFocusWithin });
                }
                window.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler((_, e) => Snapshot("window-preview-key", e)), true);
                query.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler((_, e) => Snapshot("find-preview-key", e)), true);
                // Class handlers run before instance handlers. This read-only observer is
                // registered last at UIElement and filters to this synthetic FindText.
                // It distinguishes already-handled input from the XAML handler's own result.
                EventManager.RegisterClassHandler(typeof(UIElement), Keyboard.KeyDownEvent, new KeyEventHandler((sender, e) =>
                {
                    if (ReferenceEquals(sender, query)) Snapshot("find-after-class-before-instance", e);
                }), true);
                query.AddHandler(Keyboard.KeyDownEvent, new KeyEventHandler((_, e) => Snapshot("find-bubbling-key", e)), true);
                window.AddHandler(Keyboard.KeyDownEvent, new KeyEventHandler((_, e) =>
                {
                    Snapshot("window-bubbling-key", e);
                    if (e.Key == Key.Enter && ReferenceEquals(e.OriginalSource, query))
                        window.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => Snapshot("find-post-dispatch", e)));
                }), true);
                editor.TextArea.SelectionChanged += (_, _) => Snapshot("native-selection-changed");
                editor.TextArea.Caret.PositionChanged += (_, _) => Snapshot("native-caret-changed");
                editor.TextChanged += (_, _) => Snapshot("native-source-changed");
                window.AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((_, _) => Snapshot("native-focus-changed")), true);
                query.TextChanged += (_, _) => Snapshot("query-changed");
                window.Closed += (_, _) => { Snapshot("window-closed"); stream.Dispose(); };
                Snapshot("probe-attached");
            }
        }));
    }
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
}
