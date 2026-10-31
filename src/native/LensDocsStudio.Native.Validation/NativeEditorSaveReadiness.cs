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
        private Stopwatch? clock;
        private static readonly TimeSpan budget = TimeSpan.FromSeconds(20);

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
            VerifyIdentity();
            if (!editor.Patterns.Value.IsSupported || editor.Patterns.Value.Pattern.Value.Value != expected)
                throw new InvalidOperationException("Save readiness captured an unexpected editor source.");
        }

        internal bool Observe()
        {
            VerifyIdentity();
            var diskMatches = ReadBytes().SequenceEqual(bytes);
            var enabled = Provider(() => editor.IsEnabled);
            var valueSupported = Provider(() => editor.Patterns.Value.IsSupported);
            var value = valueSupported ? Provider(() => editor.Patterns.Value.Pattern) : null;
            bool? readOnly = value is not null ? Provider(() => value.IsReadOnly.Value) : null;
            var sourceMatches = value is not null && Provider(() => value.Value.Value) == expected;
            var saveEnabled = Provider(() => save.IsEnabled);
            var savedStatus = Provider(() => status.Name) == "Saved " + Path.GetFileName(path);
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
                    FirstDiskReady = firstDiskReady, Last = last, Error = error }, new JsonSerializerOptions { WriteIndented = true }));
            }
        }

        private byte[] ReadBytes()
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var buffer = new MemoryStream(); input.CopyTo(buffer); return buffer.ToArray();
        }
        private AutomationElement Required(string id) => window.FindFirstDescendant(cf => cf.ByAutomationId(id))
            ?? throw new InvalidOperationException("Save readiness control unavailable: " + id);
        private T Provider<T>(Func<T> read)
        {
            if (clock is not null)
            {
                var remaining = budget - clock.Elapsed;
                if (remaining <= TimeSpan.Zero) throw new InvalidOperationException("Save readiness failed: the original deadline expired.");
                var limit = remaining < TimeSpan.FromSeconds(3) ? remaining : TimeSpan.FromSeconds(3);
                editor.Automation.TransactionTimeout = limit; editor.Automation.ConnectionTimeout = limit;
            }
            return read();
        }
        private void VerifyIdentity()
        {
            if (Provider(() => window.Properties.ProcessId.Value) != pid || Provider(() => editor.Properties.ProcessId.Value) != pid
                || Provider(() => window.Properties.NativeWindowHandle.Value) != hwnd
                || !Provider(() => window.FrameworkAutomationElement.RuntimeId.Value).SequenceEqual(windowId)
                || !Provider(() => editor.FrameworkAutomationElement.RuntimeId.Value).SequenceEqual(editorId)
                || !Provider(() => tab.FrameworkAutomationElement.RuntimeId.Value).SequenceEqual(tabId)
                || !Provider(() => tab.Patterns.SelectionItem.Pattern.IsSelected.Value)
                || !string.Equals(Provider(() => location.Properties.HelpText.Value), path, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Provider(() => tab.Properties.HelpText.Value), path, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Save readiness lost the original application, window, editor or source identity.");
            var walker = editor.Automation.TreeWalkerFactory.GetRawViewWalker();
            AutomationElement? ancestor = editor;
            for (var depth = 0; ancestor is not null && depth < 32; depth++, ancestor = Provider(() => walker.GetParent(ancestor)))
                if (Provider(() => ancestor.FrameworkAutomationElement.RuntimeId.Value).SequenceEqual(windowId)) return;
            throw new InvalidOperationException("Save readiness editor is no longer owned by its original window.");
        }
    }
}
