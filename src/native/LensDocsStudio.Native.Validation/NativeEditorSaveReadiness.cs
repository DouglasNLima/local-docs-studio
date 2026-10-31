using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    // Validation only: retain the original providers before Save; never reacquire an editor
    // or repeat a mutation to make readiness pass. These controlled fixtures are UTF-8 without BOM.
    internal sealed class EditorSaveReadiness
    {
        private readonly Window window;
        private readonly AutomationElement editor, save, status, location, tab;
        private readonly int pid;
        private readonly nint hwnd;
        private readonly int[] windowId, editorId, tabId;
        private readonly string path, expected;
        private readonly byte[] bytes;
        private readonly string evidence;
        private object? firstDiskReady, last;
        private int observations;
        private bool observationBudgetExhausted;
        private Stopwatch? clock;
        private static readonly TimeSpan budget = TimeSpan.FromSeconds(20);

        internal static TimeSpan? ProviderTimeout(TimeSpan remaining)
        {
            // UIA rejects transaction timeouts below 50 ms. Never round up past the
            // remaining deadline: leave the existing Wait to finish its bounded wait.
            var milliseconds = Math.Floor(Math.Min(remaining.TotalMilliseconds, 3000));
            return milliseconds >= 50 ? TimeSpan.FromMilliseconds(milliseconds) : null;
        }

        internal EditorSaveReadiness(Window window, AutomationElement editor, int processId, string path, string expected, string evidence)
        {
            this.window = window; this.editor = editor; pid = processId;
            this.path = Path.GetFullPath(path); this.expected = expected; this.evidence = evidence;
            bytes = new UTF8Encoding(false, true).GetBytes(expected);
            // A changed disk value witnesses this Save, even if its brief disabled transition
            // is missed. Idempotent Saves require a different supported generation observation.
            if (ReadBytes().SequenceEqual(bytes)) throw new InvalidOperationException("Save readiness requires changed fixture bytes before Save.");
            save = Required("SaveDocument"); status = Required("Status"); location = Required("DocumentLocation");
            var selected = window.FindAllDescendants(cf => cf.ByControlType(ControlType.TabItem))
                .Where(item => item.Patterns.SelectionItem.IsSupported && item.Patterns.SelectionItem.Pattern.IsSelected.Value).ToArray();
            if (selected.Length != 1) throw new InvalidOperationException("Save readiness requires exactly one selected source tab.");
            tab = selected[0];
            hwnd = window.Properties.NativeWindowHandle.Value;
            windowId = window.FrameworkAutomationElement.RuntimeId.Value.ToArray();
            editorId = editor.FrameworkAutomationElement.RuntimeId.Value.ToArray(); tabId = tab.FrameworkAutomationElement.RuntimeId.Value.ToArray();
            if (!VerifyIdentity()) throw new InvalidOperationException("Save readiness could not capture source identity.");
            if (!editor.Patterns.Value.IsSupported || editor.Patterns.Value.Pattern.Value.Value != expected)
                throw new InvalidOperationException("Save readiness captured an unexpected editor source.");
        }

        internal bool Observe()
        {
            if (!VerifyIdentity()) return false;
            var diskMatches = ReadBytes().SequenceEqual(bytes);
            if (!Provider(() => editor.IsEnabled, out var enabled)
                || !Provider(() => editor.Patterns.Value.IsSupported, out var valueSupported)) return false;
            bool? readOnly = null; var sourceMatches = false;
            if (valueSupported)
            {
                if (!Provider(() => editor.Patterns.Value.Pattern, out var value)
                    || !Provider(() => value.IsReadOnly.Value, out var actualReadOnly)
                    || !Provider(() => value.Value.Value, out var source)) return false;
                readOnly = actualReadOnly; sourceMatches = source == expected;
            }
            if (!Provider(() => save.IsEnabled, out var saveEnabled) || !Provider(() => status.Name, out var saved)) return false;
            var savedStatus = saved == "Saved " + Path.GetFileName(path);
            last = new { Utc = DateTimeOffset.UtcNow, DiskMatches = diskMatches, Enabled = enabled,
                ValuePattern = valueSupported, ReadOnly = readOnly, SourceMatches = sourceMatches,
                SaveCommandEnabled = saveEnabled, SavedStatus = savedStatus, Pid = pid, Hwnd = hwnd.ToInt64(),
                EditorRuntimeId = editorId, WindowRuntimeId = windowId, TabRuntimeId = tabId, SourcePath = path };
            observations++;
            if (diskMatches && firstDiskReady is null) firstDiskReady = last;
            return diskMatches && enabled && valueSupported && readOnly == false && sourceMatches && saveEnabled && savedStatus;
        }

        internal void Await()
        {
            clock = Stopwatch.StartNew(); string? error = null;
            var automation = editor.Automation;
            var transactionTimeout = automation.TransactionTimeout; var connectionTimeout = automation.ConnectionTimeout;
            try
            {
                Wait(Observe, "Save readiness failed: expected saved bytes, original editor/source ownership, enabled ValuePattern, writable editor and completed Save command/status.", clock, budget);
            }
            catch (Exception failure) { error = failure.ToString(); throw; }
            finally
            {
                var elapsed = clock.Elapsed.TotalMilliseconds; clock = null;
                automation.TransactionTimeout = transactionTimeout; automation.ConnectionTimeout = connectionTimeout;
                File.WriteAllText(evidence, JsonSerializer.Serialize(new { Status = error is null ? "PASS" : "FAIL",
                    DeadlineMilliseconds = 20000, ElapsedMilliseconds = elapsed, Observations = observations,
                    FirstDiskReady = firstDiskReady, Last = last, ObservationBudgetExhausted = observationBudgetExhausted,
                    Error = error }, new JsonSerializerOptions { WriteIndented = true }));
            }
        }

        private byte[] ReadBytes()
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var buffer = new MemoryStream(); input.CopyTo(buffer); return buffer.ToArray();
        }
        private AutomationElement Required(string id) => window.FindFirstDescendant(cf => cf.ByAutomationId(id))
            ?? throw new InvalidOperationException("Save readiness control unavailable: " + id);
        private bool Provider<T>(Func<T> read, out T value)
        {
            if (clock is not null)
            {
                var limit = ProviderTimeout(budget - clock.Elapsed);
                if (limit is null) { observationBudgetExhausted = true; value = default!; return false; }
                editor.Automation.TransactionTimeout = limit.Value; editor.Automation.ConnectionTimeout = limit.Value;
            }
            value = read(); return true;
        }
        private bool VerifyIdentity()
        {
            if (!Provider(() => window.Properties.ProcessId.Value, out var windowPid)
                || !Provider(() => editor.Properties.ProcessId.Value, out var editorPid)
                || !Provider(() => window.Properties.NativeWindowHandle.Value, out var currentHwnd)
                || !Provider(() => window.FrameworkAutomationElement.RuntimeId.Value, out var currentWindowId)
                || !Provider(() => editor.FrameworkAutomationElement.RuntimeId.Value, out var currentEditorId)
                || !Provider(() => tab.FrameworkAutomationElement.RuntimeId.Value, out var currentTabId)
                || !Provider(() => tab.Patterns.SelectionItem.Pattern.IsSelected.Value, out var selected)
                || !Provider(() => location.Properties.HelpText.Value, out var locationPath)
                || !Provider(() => tab.Properties.HelpText.Value, out var tabPath)) return false;
            if (windowPid != pid || editorPid != pid || currentHwnd != hwnd
                || !currentWindowId.SequenceEqual(windowId) || !currentEditorId.SequenceEqual(editorId) || !currentTabId.SequenceEqual(tabId)
                || !selected || !string.Equals(locationPath, path, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(tabPath, path, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Save readiness lost the original application, window, editor or source identity.");
            var walker = editor.Automation.TreeWalkerFactory.GetRawViewWalker();
            AutomationElement? ancestor = editor;
            for (var depth = 0; ancestor is not null && depth < 32; depth++)
            {
                if (!Provider(() => ancestor.FrameworkAutomationElement.RuntimeId.Value, out var ancestorId)) return false;
                if (ancestorId.SequenceEqual(windowId)) return true;
                if (!Provider(() => walker.GetParent(ancestor), out ancestor)) return false;
            }
            throw new InvalidOperationException("Save readiness editor is no longer owned by its original window.");
        }
    }
}
