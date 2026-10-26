using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using LensDocsStudio.Native;
using LensDocsStudio.Native.Editor;
using LensDocsStudio.Native.ViewModels;

namespace LensDocsStudio.Native.Validation;

// Opt-in read-only observer in the validation executable, never a shipping payload/API.
// Requests select no control and contain no source or command to execute.
internal static class NativeHostOwnerProbe
{
    public static void Initialise()
    {
        var root = Environment.GetEnvironmentVariable("LENS_OWNER_OBSERVATION_ROOT");
        if (root is null) return;
        root = Path.GetFullPath(root);
        LensDocsStudio.Core.Workspace.WorkspacePath.RejectReparsePoints(root);
        var args = Environment.GetCommandLineArgs(); var stateIndex = Array.IndexOf(args, "--state-directory");
        using var marker = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "host-sandbox.json")));
        var identity = marker.RootElement;
        if (stateIndex < 0 || stateIndex + 1 >= args.Length
            || Path.GetFullPath(args[stateIndex + 1]) != Path.Combine(root, "state", "ui-shell")
            || Path.GetFullPath(Environment.ProcessPath!) != Path.Combine(root, "payload", "LensDocsStudio.Native.exe")
            || !root.Contains(Path.DirectorySeparatorChar + "artifacts" + Path.DirectorySeparatorChar + "native" + Path.DirectorySeparatorChar + "development-host" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || identity.GetProperty("qualification").GetString() != "DEVELOPMENT_HOST / HOST_LOCAL_VALIDATION"
            || identity.GetProperty("cleanMachineCertified").GetBoolean()
            || identity.GetProperty("harnessRevision").GetString() != identity.GetProperty("sourceRevision").GetString()
            || !typeof(NativeHostOwnerProbe).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.EndsWith("+" + identity.GetProperty("harnessRevision").GetString(), StringComparison.Ordinal)
            || Digest(File.ReadAllBytes(Environment.ProcessPath!)) != identity.GetProperty("executableSha256").GetString())
            throw new IOException("Owner observation requires this prepared synthetic portable and exact validation identity.");
        var sourcePath = Path.Combine(root, "workspace", "acceptance-lf.md");
        var output = Path.Combine(root, "ui", "owner-observations"); Directory.CreateDirectory(output);
        var ids = new Dictionary<object, int>(ReferenceEqualityComparer.Instance); var next = 0; var sequence = 0;
        var versions = new List<(TextDocument Document, ITextSourceVersion Version, int Id)>();
        var observedWindows = new HashSet<Window>();
        StreamWriter? focusLog = null;
        var focusEvents = 0;
        void ObserveFocus(MainWindow main, MermaidVisualEditorWindow visual, TextEditor editor)
        {
            // Attach only after the existing packaged synthetic owner/session checks.
            // These observers never select, focus, activate or change a document.
            focusLog ??= new StreamWriter(new FileStream(Path.Combine(output, "focus-events.jsonl"), FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
            void Record(string observation, Window window, object? newFocus = null)
            {
                if (focusEvents++ >= 128) return;
                var stack = Environment.StackTrace;
                var foreground = GetForegroundWindow();
                GetWindowThreadProcessId(foreground, out var foregroundPid);
                try
                {
                    focusLog.WriteLine(JsonSerializer.Serialize(new { atUtc = DateTimeOffset.UtcNow, observation,
                        processId = Environment.ProcessId, window = window == main ? "owner" : "private",
                        hwnd = new WindowInteropHelper(window).Handle.ToInt64(), foreground = foreground.ToInt64(), foregroundPid,
                        focusType = newFocus?.GetType().FullName, focusName = (newFocus as FrameworkElement)?.Name,
                        ownerFocused = editor.IsKeyboardFocusWithin, ownerSourceSha256 = Digest(Encoding.UTF8.GetBytes(editor.Document.Text)),
                        stack = stack.Length <= 16000 ? stack : stack[..16000], scope = "Bounded process-local read-only focus/source trace; no foreign title or content" }));
                }
                catch (IOException) { focusEvents = 128; }
                catch (UnauthorizedAccessException) { focusEvents = 128; }
            }
            void Attach(Window window)
            {
                window.AddHandler(Keyboard.PreviewGotKeyboardFocusEvent,
                    new KeyboardFocusChangedEventHandler((_, e) => Record("preview-focus", window, e.NewFocus)), true);
                window.Activated += (_, _) => Record("activated", window);
                window.Deactivated += (_, _) => Record("deactivated", window);
            }
            if (observedWindows.Add(main))
            {
                Attach(main);
                editor.TextChanged += (_, _) => Record("owner-source-changed", main);
                main.Closed += (_, _) => { Record("owner-closed", main); focusEvents = 128; focusLog.Dispose(); };
            }
            if (observedWindows.Add(visual)) Attach(visual);
        }
        int Id(object value) { if (!ids.TryGetValue(value, out var id)) ids.Add(value, id = ++next); return id; }
        int VersionId(TextDocument document)
        {
            var version = document.Version;
            foreach (var entry in versions)
                if (ReferenceEquals(entry.Document, document) && entry.Version.CompareAge(version) == 0) return entry.Id;
            var id = ++next; versions.Add((document, version, id)); return id;
        }
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(100) };
        timer.Tick += (_, _) =>
        {
            var request = Path.Combine(output, "request-" + (sequence + 1) + ".json");
            if (!File.Exists(request) || sequence >= 100) return;
            var response = Path.Combine(output, "response-" + ++sequence + ".json");
            object result;
            try
            {
                LensDocsStudio.Core.Workspace.WorkspacePath.RejectReparsePoints(request);
                LensDocsStudio.Core.Workspace.WorkspacePath.RejectReparsePoints(response);
                using var requested = JsonDocument.Parse(File.ReadAllText(request));
                if (requested.RootElement.GetProperty("sequence").GetInt32() != sequence) throw new IOException("Observation sequence mismatch.");
                if (Application.Current?.MainWindow is not MainWindow main || typeof(MainWindow).Assembly.Location != Path.Combine(root, "payload", "LensDocsStudio.Native.dll"))
                    throw new IOException("Actual packaged MainWindow is unavailable.");
                var visual = main.OwnedWindows.OfType<MermaidVisualEditorWindow>().Single();
                var editor = main.FindName("Editor") as TextEditor ?? throw new IOException("Explicit owner editor missing.");
                var draftEditor = visual.FindName("SourceEditor") as TextEditor ?? throw new IOException("Explicit private editor missing.");
                var model = (WorkspaceViewModel)Field(main, "model");
                var apply = (Delegate)Field(visual, "applyToDocument");
                var captured = apply.Target ?? throw new IOException("Captured document session missing.");
                var session = (MermaidDocumentEditSession)(captured.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Single(field => field.FieldType == typeof(MermaidDocumentEditSession)).GetValue(captured) ?? throw new IOException("Captured session unavailable."));
                if (!ReferenceEquals(visual.Owner, main) || !ReferenceEquals(session.Owner, model.ActiveDocument)
                    || !ReferenceEquals(editor.Document, session.Owner.EditorDocument) || !model.Documents.Contains(session.Owner)
                    || session.Owner.EffectivePath != sourcePath || session.Owner.IsSaving || editor.IsReadOnly
                    || AutomationProperties.GetAutomationId(editor) != "MarkdownEditor")
                    throw new IOException("Explicit synthetic document/editor/session identity changed; no source observed.");
                ObserveFocus(main, visual, editor);
                var version = (ITextSourceVersion)Field(session, "version");
                var draft = (LensDocsStudio.Core.Mermaid.MermaidVisualDraft)Field(visual, "draft");
                var mainHwnd = new WindowInteropHelper(main).Handle; var visualHwnd = new WindowInteropHelper(visual).Handle;
                _ = GetWindowThreadProcessId(mainHwnd, out var mainPid); _ = GetWindowThreadProcessId(visualHwnd, out var visualPid);
                var visible = 0;
                EnumWindows((handle, _) => { GetWindowThreadProcessId(handle, out var pid); if (pid == Environment.ProcessId && IsWindowVisible(handle)) visible++; return true; }, IntPtr.Zero);
                using var process = Process.GetCurrentProcess();
                result = new HostOwnerSnapshot {
                    AtUtc = DateTimeOffset.UtcNow, Sequence = sequence, ProcessId = process.Id, ProcessStartTicks = process.StartTime.ToUniversalTime().Ticks,
                    MainHwnd = mainHwnd.ToInt64(), VisualHwnd = visualHwnd.ToInt64(), MainPid = mainPid, VisualPid = visualPid,
                    MainOwner = GetWindow(mainHwnd, 4).ToInt64(), VisualOwner = GetWindow(visualHwnd, 4).ToInt64(), Foreground = GetForegroundWindow().ToInt64(),
                    VisibleWindows = visible, MainEnabled = IsWindowEnabled(mainHwnd), VisualEnabled = IsWindowEnabled(visualHwnd), NormalConsole = NormalConsole(process),
                    ExactEditorAndOwner = true,
                    ReadyPrivateDraft = draft.IsDirty && draft.CanApply && draft.Source == draftEditor.Document.Text && !(bool)Field(visual, "busy"),
                    MainIdentity = Id(main), VisualIdentity = Id(visual), DocumentIdentity = Id(editor.Document), SessionIdentity = Id(session),
                    OwnerVersion = VersionId(editor.Document), PrivateVersion = VersionId(draftEditor.Document), CapturedVersionAge = version.CompareAge(editor.Document.Version),
                    OwnerGeneration = (long)Field(main, "mermaidOwnerGeneration"), SourceSha256 = Digest(Encoding.UTF8.GetBytes(editor.Document.Text)),
                    PrivateSha256 = Digest(Encoding.UTF8.GetBytes(draftEditor.Document.Text)), SavedSha256 = Digest(File.ReadAllBytes(sourcePath)) };
            }
            catch (Exception error) { result = new { status = "BLOCKED", sequence, error = error.ToString() }; }
            using var stream = new FileStream(response, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(stream, result, result.GetType(), new JsonSerializerOptions { WriteIndented = true });
        };
        timer.Start();
    }

    private static object Field(object owner, string name) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(owner)
        ?? throw new IOException("Expected source-pinned private observation field is missing: " + name);
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static bool NormalConsole(Process process)
    {
        if (process.SessionId != WTSGetActiveConsoleSessionId()) return false;
        var desktop = OpenInputDesktop(0, false, 1); if (desktop == IntPtr.Zero) return false;
        var buffer = Marshal.AllocHGlobal(1024);
        try { return GetUserObjectInformation(desktop, 2, buffer, 1024, out _) && Marshal.PtrToStringUni(buffer) == "Default"; }
        finally { Marshal.FreeHGlobal(buffer); CloseDesktop(desktop); }
    }
    private delegate bool EnumWindowCallback(IntPtr handle, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr handle, uint command);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsWindowEnabled(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern int WTSGetActiveConsoleSessionId();
    [DllImport("user32.dll")] private static extern IntPtr OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll")] private static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetUserObjectInformation(IntPtr handle, int index, IntPtr info, int length, out int needed);
}
