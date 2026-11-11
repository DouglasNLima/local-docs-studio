using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.State;

internal static class PointerControl
{
    public static int Run(string executable, string output)
    {
        const string source = "# Root\n\n## First child\n\nParagraph.\n\n## Second child\n\nLast paragraph.\n";
        var path = Path.Combine(output, "pointer.md"); File.WriteAllText(path, source);
        var bytes = File.ReadAllBytes(path); var state = Path.Combine(output, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new UserPreferences(AutomaticUpdateChecks: false, ShowOutline: true)));
        var start = new ProcessStartInfo(executable) { UseShellExecute = false };
        foreach (var arg in new[] { "--state-directory", state, path }) start.ArgumentList.Add(arg);
        using var automation = new UIA3Automation(); using var app = Application.Launch(start);
        var checks = new List<string>(); Window? window = null;
        var original = Assembly.Load("LensDocsStudio.Native.Validation").GetType("LensDocsStudio.Native.Validation.NativeUiChecks", true)!;
        void Wait(Func<bool> ready, string message) => original.GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(m => m.Name == "Wait" && m.GetParameters().Length == 3).Invoke(null, [ready, message, 20]);
        void Check(bool condition, string message) { if (!condition) throw new IOException(message); checks.Add(message); }
        try
        {
            window = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("Owned pointer window did not open.");
            original.GetMethod("FocusWindow", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [window]);
            var editor = window.FindFirstDescendant(cf => cf.ByAutomationId("MarkdownEditor")) ?? throw new IOException("Owned editor not found.");
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == source, "Pointer fixture source did not load.");
            _ = editor.FindAllDescendants();
            editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            Wait(() => Caret() == source.Length, "Pointer setup did not reach the distinct natural end caret.");
            Check(Caret() != source.IndexOf("## First child", StringComparison.Ordinal), "Pointer target differs from the starting editor caret");
            var heading = window.FindFirstDescendant(cf => cf.ByName("Heading level 2: First child")) ?? throw new IOException("Owned heading not found.");
            heading.Click();
            Wait(() => Caret() == source.IndexOf("## First child", StringComparison.Ordinal), "Actual heading click did not navigate to its source.");
            Check(editor.Patterns.Text.Pattern.GetSelection().Single().GetText(-1).Length == 0, "Actual pointer navigation collapses editor selection at the heading");
            Check(heading.Properties.HasKeyboardFocus.Value && !editor.Properties.HasKeyboardFocus.Value, "Actual pointer navigation retains heading keyboard focus");
            Check(editor.Patterns.Value.Pattern.Value.Value == source && File.ReadAllBytes(path).SequenceEqual(bytes), "Actual pointer navigation preserves source and saved bytes");
            window.CaptureToFile(Path.Combine(output, "pointer.png"));
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Passed = true, Checks = checks })); return 0;
            int Caret()
            {
                var selection = editor.Patterns.Text.Pattern.GetSelection().Single();
                var prefix = editor.Patterns.Text.Pattern.DocumentRange.Clone();
                prefix.MoveEndpointByRange(TextPatternRangeEndpoint.End, selection, TextPatternRangeEndpoint.Start);
                return prefix.GetText(-1).Length;
            }
        }
        catch (Exception error)
        {
            window?.CaptureToFile(Path.Combine(output, "failure.png"));
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Passed = false, Checks = checks, Error = error.ToString() }));
            Console.Error.WriteLine(error); return 1;
        }
        finally { if (!app.HasExited) app.Kill(); }
    }
}
