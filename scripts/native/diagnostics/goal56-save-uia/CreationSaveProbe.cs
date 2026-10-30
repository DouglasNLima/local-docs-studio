using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using LensDocsStudio.Core.Diagnostics;

namespace LensDocsStudio.Native.Validation;

internal static class CreationSaveProbe
{
    private static int saves, values;
    public static void BeforeSave()
    {
        var id = $"save-trigger-{++saves:D4}"; SaveTimeline.Mark(id); SaveTimeline.Event("client.save.requested", new { Trigger = id });
    }
    public static void SetValue(AutomationElement editor, string text, Window main, UIA3Automation automation, string output)
    {
        var id = $"set-value-{++values:D4}";
        SaveTimeline.Mark(id);
        SaveTimeline.Event("client.set-value.before", Snapshot(editor, main, automation));
        try
        {
            SaveTimeline.Event("client.set-value.requested", new { Id = id });
            editor.Patterns.Value.Pattern.SetValue(text);
            SaveTimeline.Event("client.set-value.completed", new { Id = id });
            SaveTimeline.Event("client.set-value.after", Snapshot(editor, main, automation));
        }
        catch (Exception error)
        {
            // The original element and exception are retained. Reacquisition is failure-only.
            SaveTimeline.Event("client.set-value.failed", new { Id = id, Error = SaveTimeline.Error(error) });
            SaveTimeline.Event("client.failure.snapshot", Snapshot(editor, main, automation));
            SaveTimeline.Event("client.failure.reacquired", Observe(() => Describe(main.FindFirstDescendant(cf => cf.ByAutomationId("MarkdownEditor")))));
            SaveTimeline.Flush(); throw;
        }
    }
    internal static object Snapshot(AutomationElement editor, Window main, UIA3Automation automation) => new {
        Editor = Describe(editor), Window = Describe(main), Focused = Observe(() => Describe(automation.FocusedElement())), Foreground = Foreground(),
        NativeState = Observe(() => editor.Properties.ItemStatus.Value),
        Limit = "Separate observations precede/follow the call; provider entry records the actual native call boundary. Dispatcher hooks do not establish queue idleness." };
    private static object Describe(AutomationElement? element) => element is null ? new { Missing = true } : new {
        AutomationId = Observe(() => element.AutomationId), ProcessId = Observe(() => element.Properties.ProcessId.Value),
        RuntimeId = Observe(() => element.FrameworkAutomationElement.RuntimeId.Value), ControlType = Observe(() => element.ControlType.ToString()),
        Hwnd = Observe(() => element.Properties.NativeWindowHandle.Value.ToInt64()), Enabled = Observe(() => element.IsEnabled),
        HasKeyboardFocus = Observe(() => element.Properties.HasKeyboardFocus.Value), Offscreen = Observe(() => element.IsOffscreen),
        ValuePattern = Observe(() => element.Patterns.Value.IsSupported), TextPattern = Observe(() => element.Patterns.Text.IsSupported),
        ReadOnly = Observe(() => element.Patterns.Value.Pattern.IsReadOnly.Value) };
    private static object Foreground()
    {
        var handle = GetForegroundWindow(); var thread = GetWindowThreadProcessId(handle, out var pid);
        return new { Hwnd = handle.ToInt64(), ProcessId = pid, ThreadId = thread };
    }
    private static object Observe<T>(Func<T> read) { try { return new { Supported = true, Value = read() }; } catch (Exception error) { return new { Supported = false, Error = SaveTimeline.Error(error) }; } }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint handle, out int pid);
}
