using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
        var stream = new StreamWriter(new FileStream(Path.Combine(root, "ui", "native-observations.jsonl"), FileMode.CreateNew), new UTF8Encoding(false)) { AutoFlush = true };
        var count = 0;
        void Record(object value) { if (count++ < 200) stream.WriteLine(JsonSerializer.Serialize(value)); }
        Record(new { atUtc = DateTimeOffset.UtcNow, observation = "probe-loaded", processId = Environment.ProcessId, sourceSha256 = Digest(Encoding.UTF8.GetBytes(source)), probeSha256 = Digest(File.ReadAllBytes(typeof(StartupHook).Assembly.Location)), runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription });
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
        {
            if (Application.Current?.MainWindow is not Window window || window.FindName("Editor") is not TextEditor editor || window.FindName("FindText") is not TextBox query)
            { Record(new { atUtc = DateTimeOffset.UtcNow, observation = "probe-unavailable" }); return; }
            void Snapshot(string observation, KeyEventArgs? key = null)
            {
                // Stop recording if the process is no longer displaying the prepared synthetic source.
                if (editor.Document.Text != source) { Record(new { atUtc = DateTimeOffset.UtcNow, observation = "source-differs-probe-stopped" }); count = 200; return; }
                Record(new { atUtc = DateTimeOffset.UtcNow, observation, key = key?.Key.ToString(), handled = key?.Handled,
                    originalSource = (key?.OriginalSource as FrameworkElement)?.Name, focus = (Keyboard.FocusedElement as FrameworkElement)?.Name,
                    query = query.Text, context = (window.FindName("FindContext") as TextBlock)?.Text,
                    selectionStart = editor.SelectionStart, selectionLength = editor.SelectionLength, selectedText = editor.SelectedText, caretOffset = editor.CaretOffset,
                    sourceSha256 = Digest(Encoding.UTF8.GetBytes(editor.Document.Text)) });
            }
            window.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler((_, e) => Snapshot("window-preview-key", e)), true);
            query.AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler((_, e) => Snapshot("find-preview-key", e)), true);
            query.AddHandler(Keyboard.KeyDownEvent, new KeyEventHandler((_, e) => Snapshot("find-bubbling-key", e)), true);
            window.AddHandler(Keyboard.KeyDownEvent, new KeyEventHandler((_, e) => Snapshot("window-bubbling-key", e)), true);
            editor.TextArea.SelectionChanged += (_, _) => Snapshot("native-selection-changed");
            query.TextChanged += (_, _) => Snapshot("query-changed");
            window.Closed += (_, _) => { Snapshot("window-closed"); stream.Dispose(); };
            Snapshot("probe-attached");
        }));
    }
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
