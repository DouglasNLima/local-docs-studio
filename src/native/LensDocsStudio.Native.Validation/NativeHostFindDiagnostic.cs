using System.Diagnostics;
using System.Runtime.InteropServices;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    // Diagnostic only: one action per fresh guarded session, never a shell acceptance.
    private static int RunHostFindDiagnostic(string executable, string sandbox, string revision, string tree, string variant)
    {
        var output = Path.Combine(sandbox, "ui");
        var sourcePath = Path.Combine(sandbox, "workspace", "acceptance-lf.md");
        var source = File.ReadAllText(sourcePath); var sourceHash = Hash(sourcePath);
        var occurrences = Enumerable.Range(0, source.Length).Where(i => source.AsSpan(i).StartsWith("Café", StringComparison.Ordinal)).ToArray();
        WriteNew(Path.Combine(output, "diagnostic-before.json"), new { atUtc = DateTimeOffset.UtcNow, revision, tree, variant, sourceHash, occurrences, query = "Café", rangeUnits = "UTF-16 code units", fullShell = "NOT RUN" });
        using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromSeconds(3), ConnectionTimeout = TimeSpan.FromSeconds(3) };
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(sourcePath)! };
        if (variant.EndsWith("Observed", StringComparison.Ordinal))
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_STARTUP_HOOKS"))) throw new IOException("Do not combine the isolated observer with inherited startup hooks.");
            info.Environment["DOTNET_STARTUP_HOOKS"] = typeof(StartupHook).Assembly.Location;
            info.Environment["LENS_FIND_OBSERVATION_ROOT"] = sandbox;
        }
        foreach (var arg in new[] { "--state-directory", Path.Combine(sandbox, "state"), Path.GetDirectoryName(sourcePath)!, sourcePath }) info.ArgumentList.Add(arg);
        using var app = Application.Launch(info);
        Window? main = null; AutomationElement? editor = null; var stage = "owner-readiness"; var sent = false; string? failure = null;
        try
        {
            main = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("Owner did not open within 25 seconds.");
            CaptureHostRuntime(Process.GetProcessById(app.ProcessId), executable, output, revision, tree);
            editor = Find(main, "MarkdownEditor");
            Wait(() => ReadEditorText(editor) == source, "Exact synthetic editor source did not load.");
            if (variant.EndsWith("Observed", StringComparison.Ordinal))
                Wait(() => File.Exists(Path.Combine(output, "native-observations.jsonl")) && ReadObservedText(Path.Combine(output, "native-observations.jsonl")).Contains("probe-attached", StringComparison.Ordinal), "Read-only native observer did not attach; no input sent.");
            stage = "foreground"; AssertForeground(); editor.Focus();
            Wait(() => editor.Properties.HasKeyboardFocus.Value, "Actual MarkdownEditor did not acquire focus.");
            Observe("editor-focused");
            stage = "open-find"; AssertInputTarget(editor); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
            Wait(() => Find(main, "FindContext").Name == "Find in editor", "Ctrl+F did not open Find in editor.");
            var query = Find(main, "FindText");
            Wait(() => query.Properties.HasKeyboardFocus.Value, "FindText did not acquire keyboard focus.");
            AssertInputTarget(query);
            if (variant.StartsWith("FindTyped", StringComparison.Ordinal)) Keyboard.Type("Café"); else query.Patterns.Value.Pattern.SetValue("Café");
            Wait(() => query.Properties.HasKeyboardFocus.Value && query.Patterns.Value.Pattern.Value.Value == "Café", "Exact query and focus were not established.");
            Observe("before-action");
            // Observations cannot authorise input if foreground/focus has since moved.
            AssertInputTarget(query);
            stage = "single-action"; sent = true;
            if (variant.StartsWith("FindNext", StringComparison.Ordinal)) Find(main, "FindNext").AsButton().Click(); else Keyboard.Type(VirtualKeyShort.RETURN);
            Wait(() => { AssertForeground(); return ExactSelection(); }, "Exact Unicode selection was not exposed after the single action.");
            Observe("after-action");
            AssertForeground();
            if (variant.EndsWith("Observed", StringComparison.Ordinal)) AssertNativeSelection();
            if (ReadEditorText(editor) != source || Hash(sourcePath) != sourceHash) throw new IOException("Source changed; stop without further input.");
            stage = "close-find-observation"; AssertForeground(); Find(main, "CloseFind").AsButton().Invoke();
            Wait(() => { AssertForeground(); return editor.Properties.HasKeyboardFocus.Value; }, "Close Find did not return editor focus.");
            Observe("after-close-find");
            if (!ExactSelection()) throw new IOException("Close Find changed the exact selected range.");
            if (variant.EndsWith("Observed", StringComparison.Ordinal)) AssertNativeSelection();
            if (ReadEditorText(editor) != source || Hash(sourcePath) != sourceHash) throw new IOException("Source changed after closing Find.");
            stage = "normal-close"; AssertForeground(); main.Close(); Wait(() => app.HasExited, "Owned synthetic app did not close normally.");
            WriteNew(Path.Combine(output, "completion.json"), new { atUtc = DateTimeOffset.UtcNow, status = failure is null ? "PASS" : "FAIL", scope = "BOUNDED_FIND_DIAGNOSTIC_ONLY", revision, tree, variant, sent, failure, sourceHash, fullShell = "NOT RUN", nativeSelection = variant.EndsWith("Observed", StringComparison.Ordinal) ? "See native-observations.jsonl alongside UIA ranges; this result retains the independent UIA assertion" : "Not observed in-process; UIA alone cannot establish absence of native selection" });
            return failure is null ? 0 : 1;
        }
        catch (Exception error)
        {
            try { if (main is not null && editor is not null) Observe("failure-observation"); }
            catch (Exception observationError) { failure = observationError.ToString(); }
            WriteNew(Path.Combine(output, "completion.json"), new { atUtc = DateTimeOffset.UtcNow, status = error is HostFindInputBlockedException ? "BLOCKED" : "FAIL", revision, tree, variant, stage, sent, failure, error = error.ToString(), processId = app.ProcessId, fullShell = "NOT RUN" });
            throw;
        }

        void AssertForeground()
        {
            var hwnd = main!.Properties.NativeWindowHandle.Value;
            GetWindowThreadProcessId(hwnd, out var owner);
            var desktop = HostFindOpenInputDesktop(0, false, 1);
            var accessible = false;
            if (desktop != IntPtr.Zero)
            {
                var buffer = Marshal.AllocHGlobal(1024);
                try { if (HostFindGetUserObjectInformation(desktop, 2, buffer, 1024, out _)) accessible = Marshal.PtrToStringUni(buffer) == "Default"; }
                finally { Marshal.FreeHGlobal(buffer); HostFindCloseDesktop(desktop); }
            }
            if (owner != app.ProcessId || GetForegroundWindow() != hwnd || !accessible || Process.GetCurrentProcess().SessionId != HostActiveConsoleSession())
                throw new HostFindInputBlockedException("Exact candidate HWND must remain foreground on the unlocked Default console desktop; no activation or input retry is permitted.");
        }
        bool ExactSelection()
        {
            var ranges = editor!.Patterns.Text.Pattern.GetSelection();
            if (ranges.Length != 1 || ranges[0].GetText(-1) != "Café") return false;
            var prefix = editor.Patterns.Text.Pattern.DocumentRange.Clone();
            prefix.MoveEndpointByRange(FlaUI.Core.Definitions.TextPatternRangeEndpoint.End, ranges[0], FlaUI.Core.Definitions.TextPatternRangeEndpoint.Start);
            return prefix.GetText(-1).Length == 2;
        }
        void AssertNativeSelection()
        {
            var records = ReadObservedText(Path.Combine(output, "native-observations.jsonl")).Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => System.Text.Json.JsonDocument.Parse(line)).ToArray();
            try
            {
                var snapshots = records.Select(record => record.RootElement).Where(record => record.TryGetProperty("selectionStart", out _)).ToArray();
                var last = snapshots.Last();
                if (last.GetProperty("selectionStart").GetInt32() != 2 || last.GetProperty("selectionLength").GetInt32() != 4 || last.GetProperty("selectedText").GetString() != "Café" || last.GetProperty("caretOffset").GetInt32() != 6
                    || last.GetProperty("query").GetString() != "Café" || last.GetProperty("sourceSha256").GetString() != sourceHash)
                    throw new IOException("Native AvalonEdit selection/query/source does not match exact UIA acceptance.");
                var returns = snapshots.Where(record => record.GetProperty("observation").GetString() == "window-preview-key" && record.GetProperty("key").GetString() == "Return").ToArray();
                if (returns.Length != (variant.StartsWith("FindNext", StringComparison.Ordinal) ? 0 : 1)) throw new IOException("Expected exactly one Return, or zero for the independent Next control.");
            }
            finally { foreach (var record in records) record.Dispose(); }
        }
        void AssertInputTarget(AutomationElement control)
        {
            AssertForeground();
            if (!control.Properties.HasKeyboardFocus.Value)
                throw new HostFindInputBlockedException("Input target changed; no action sent.");
        }

        void Observe(string name)
        {
            var ranges = new List<object>(); string? selectionError = null;
            try
            {
                foreach (var selected in editor!.Patterns.Text.Pattern.GetSelection())
                {
                    var prefix = editor.Patterns.Text.Pattern.DocumentRange.Clone();
                    prefix.MoveEndpointByRange(FlaUI.Core.Definitions.TextPatternRangeEndpoint.End, selected, FlaUI.Core.Definitions.TextPatternRangeEndpoint.Start);
                    var text = selected.GetText(-1); ranges.Add(new { text, start = prefix.GetText(-1).Length, length = text.Length });
                }
            }
            catch (Exception error) { selectionError = error.ToString(); }
            var handle = main!.Properties.NativeWindowHandle.Value;
            var thread = GetWindowThreadProcessId(handle, out var owner);
            var gui = new FindGuiThreadInfo { Size = Marshal.SizeOf<FindGuiThreadInfo>() };
            var gotGui = FindGetGUIThreadInfo(thread, ref gui);
            WriteNew(Path.Combine(output, name + ".json"), new { atUtc = DateTimeOffset.UtcNow, revision, tree, variant, processId = app.ProcessId, processStartUtc = Process.GetProcessById(app.ProcessId).StartTime.ToUniversalTime(), hwnd = handle.ToInt64(), owner, dpi = HostWindowDpi(handle), foreground = GetForegroundWindow().ToInt64(), gotGui, focusHwnd = gui.Focus.ToInt64(), caretHwnd = gui.Caret.ToInt64(), ranges, selectionError, sourceExact = ReadEditorText(editor!) == source, sourceHash = Hash(sourcePath), controls = main.FindAllDescendants().Where(e => e.Properties.HasKeyboardFocus.ValueOrDefault || e.Properties.AutomationId.ValueOrDefault is "FindText" or "FindContext" or "MarkdownEditor" or "CaretStatus").Select(e => new { id = e.Properties.AutomationId.ValueOrDefault, name = e.Name, focused = e.Properties.HasKeyboardFocus.ValueOrDefault, value = e.Patterns.Value.IsSupported ? e.Patterns.Value.Pattern.Value.Value : null }).ToArray() });
        }
    }
    private sealed class HostFindInputBlockedException(string message) : InvalidOperationException(message);
    [DllImport("user32.dll", EntryPoint = "OpenInputDesktop")] private static extern IntPtr HostFindOpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll", EntryPoint = "CloseDesktop")] private static extern bool HostFindCloseDesktop(IntPtr desktop);
    [DllImport("user32.dll", EntryPoint = "GetUserObjectInformationW")] private static extern bool HostFindGetUserObjectInformation(IntPtr handle, int index, IntPtr buffer, uint length, out uint needed);
    [StructLayout(LayoutKind.Sequential)] private struct FindGuiThreadInfo { public int Size, Flags; public IntPtr Active, Focus, Capture, MenuOwner, MoveSize, Caret; public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll", EntryPoint = "GetGUIThreadInfo")] private static extern bool FindGetGUIThreadInfo(uint thread, ref FindGuiThreadInfo info);
}
