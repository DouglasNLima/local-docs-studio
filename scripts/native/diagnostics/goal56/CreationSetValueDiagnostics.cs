using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace LensDocsStudio.Native.Validation;

internal static class CreationSetValueDiagnostics
{
    private static readonly List<object> observations = [];
    private static readonly Stopwatch timer = Stopwatch.StartNew();

    public static void BeforeSave(AutomationElement editor, Window main, UIA3Automation automation, string output)
    {
        observations.Add(new { Phase = "before-save", Milliseconds = timer.Elapsed.TotalMilliseconds, Editor = Describe(editor),
            Focused = Observe(() => Describe(automation.FocusedElement())), Window = Describe(main), Foreground = Foreground() });
    }

    public static void SetValue(AutomationElement editor, string text, Window main, UIA3Automation automation, string output)
    {
        var started = timer.Elapsed.TotalMilliseconds;
        var readOnly = Observe(() => editor.Patterns.Value.Pattern.IsReadOnly.Value);
        var observed = timer.Elapsed.TotalMilliseconds;
        try
        {
            editor.Patterns.Value.Pattern.SetValue(text);
            observations.Add(new { Phase = "set-value-pass", StartedMilliseconds = started, ReadOnlyObservedMilliseconds = observed,
                CompletedMilliseconds = timer.Elapsed.TotalMilliseconds, ReadOnly = readOnly });
        }
        catch (Exception error)
        {
            var failed = timer.Elapsed.TotalMilliseconds;
            observations.Add(new { Phase = "set-value-fail", StartedMilliseconds = started, ReadOnlyObservedMilliseconds = observed,
                FailedMilliseconds = failed, ReadOnly = readOnly, Exception = Error(error), Editor = Describe(editor),
                CurrentEditor = Observe(() => Describe(main.FindFirstDescendant(cf => cf.ByAutomationId("MarkdownEditor")))),
                Focused = Observe(() => Describe(automation.FocusedElement())), Window = Describe(main), Foreground = Foreground(),
                NativeResponsive = Observe(() => SendMessageTimeout((nint)main.Properties.NativeWindowHandle.Value, 0, 0, 0, 0x23, 100, out _) != 0),
                DispatcherQueue = "Not exposed by the external UIA client; native responsiveness does not establish queue idleness." });
            throw;
        }
        finally
        {
            File.WriteAllText(Path.Combine(output, "set-value-diagnostics.json"), JsonSerializer.Serialize(observations, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    private static object Describe(AutomationElement? element) => element is null ? new { Missing = true } : new
    {
        AutomationId = Observe(() => element.AutomationId), ProcessId = Observe(() => element.Properties.ProcessId.Value),
        RuntimeId = Observe(() => element.FrameworkAutomationElement.RuntimeId.Value), ControlType = Observe(() => element.ControlType.ToString()),
        Hwnd = Observe(() => element.Properties.NativeWindowHandle.Value.ToInt64()), Enabled = Observe(() => element.IsEnabled),
        HasKeyboardFocus = Observe(() => element.Properties.HasKeyboardFocus.Value), Offscreen = Observe(() => element.IsOffscreen),
        ValuePattern = Observe(() => element.Patterns.Value.IsSupported), TextPattern = Observe(() => element.Patterns.Text.IsSupported),
        ReadOnly = Observe(() => element.Patterns.Value.Pattern.IsReadOnly.Value)
    };
    private static object Foreground()
    {
        var handle = GetForegroundWindow(); var thread = GetWindowThreadProcessId(handle, out var pid);
        return new { Hwnd = handle.ToInt64(), ProcessId = pid, ThreadId = thread };
    }
    private static object Observe<T>(Func<T> read) { try { return new { Value = read() }; } catch (Exception error) { return Error(error); } }
    private static object Error(Exception error) => new { Type = error.GetType().FullName, HResult = $"0x{error.HResult:X8}", error.Message, Detail = error.ToString() };
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint handle, out int pid);
    [DllImport("user32.dll")] private static extern nint SendMessageTimeout(nint handle, uint message, nuint wParam, nint lParam, uint flags, uint milliseconds, out nuint result);
}
