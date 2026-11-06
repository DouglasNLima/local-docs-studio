using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    // Actual pointer, keyboard and OS appearance checks run only on disposable
    // hosted Windows through Program's guard. They do not certify physical users.
    public static int RunReadingProductivity(string executable, string output, string root, bool highContrast) => Task.Run(() =>
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        using var contrast = highContrast ? new NativeTestHighContrast() : null;
        using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromSeconds(3), ConnectionTimeout = TimeSpan.FromSeconds(3) };
        var checks = new List<string>(); var state = Path.Combine(output, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new UserPreferences(AutomaticUpdateChecks: false, Theme: "Dark", Layout: "Editor")));
        var left = Path.Combine(output, "left.md"); var right = Path.Combine(output, "right.md");
        var spc = File.ReadAllText(Path.Combine(root, "tests/fixtures/native-flowchart/spc-application-architecture.md"));
        File.WriteAllText(left, spc); File.WriteAllText(right, "# Right\n\nRight source");
        var originalLeft = File.ReadAllBytes(left);
        var info = new ProcessStartInfo(executable) { UseShellExecute = false };
        foreach (var argument in new[] { "--state-directory", state, left, right }) info.ArgumentList.Add(argument);
        using var app = Application.Launch(info); Window? main = null;
        try
        {
            main = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("The reading-productivity window did not open.");
            FocusWindow(main); var editor = Find(main, "MarkdownEditor");
            Wait(() => ReadEditorText(editor) == "# Right\n\nRight source", "The right document did not load.");
            Wait(() => Find(main, "DocumentStatistics").Name.Contains("3 words", StringComparison.Ordinal), "Current-buffer reading statistics were not announced.");
            Check(!highContrast || System.Windows.SystemParameters.HighContrast, "Requested operating-system High Contrast is active");
            editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END); Keyboard.Type(VirtualKeyShort.RETURN); Keyboard.Type(VirtualKeyShort.RETURN); Keyboard.Type("/heading 2");
            Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByAutomationId("SlashCommands").And(cf.ByProcessId(app.ProcessId))) is not null, "The actual slash popup did not open.");
            Keyboard.Type(VirtualKeyShort.RETURN);
            Wait(() => ReadEditorText(editor).EndsWith("## ", StringComparison.Ordinal), "The keyboard slash command did not insert H2.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => ReadEditorText(editor).EndsWith("/heading 2", StringComparison.Ordinal), "Slash insertion was not one native Undo operation.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
            Check(ReadEditorText(editor).EndsWith("## ", StringComparison.Ordinal), "Actual keyboard slash execution retains one-step Undo/Redo");
            var rightBuffer = ReadEditorText(editor);
            Find(main, "FileMenu").Click(); Find(main, "OpenBeside").Patterns.Invoke.Pattern.Invoke();
            Wait(() => !Find(main, "ComparisonPreview").IsOffscreen, "The second native preview did not become visible.");
            Wait(() => main.FindFirstDescendant(cf => cf.ByName("Native Mermaid retains label text and line breaks, but small, subscript, superscript and highlight typography is not supported. Original source is unchanged; these labels remain protected in Visual Edit.")) is not null,
                "The exact SPC typography limitation was not visible in the real secondary preview.");
            Check(File.ReadAllBytes(left).SequenceEqual(originalLeft), "Opening the exact SPC beside another document does not write its Markdown");
            main.CaptureToFile(Path.Combine(output, "editor-preview-spc.png"));
            Find(main, "ComparisonMode1").AsComboBox().Select("Editor"); Find(main, "ActivateComparison1").AsButton().Invoke();
            Wait(() => ReadEditorText(editor) == spc, "The left activation did not bind its authoritative source.");
            editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END); Keyboard.Type(VirtualKeyShort.RETURN); Keyboard.Type("Left pending");
            var leftBuffer = ReadEditorText(editor);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F); Find(main, "FindText").AsTextBox().Text = "Left pending";
            Wait(() => Find(main, "FindCounter").Name == "1 of 1", "Find did not use the active left buffer.");
            Find(main, "ActivateComparison0").AsButton().Invoke();
            Wait(() => ReadEditorText(editor) == rightBuffer, "Activating right lost its unsaved source.");
            Check(main.FindFirstDescendant(cf => cf.ByAutomationId("FindText")) is not { IsOffscreen: false }, "The left Find session does not leak into the right document");
            editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            Wait(() => File.ReadAllText(right) == rightBuffer, "Save did not persist the active right document.");
            Check(File.ReadAllBytes(left).SequenceEqual(originalLeft), "Saving right leaves the dirty secondary left file untouched");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_S);
            Window? saveWindow = null;
            Wait(() => (saveWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByName("Save document")))?.AsWindow()) is not null,
                "The active comparison document's Save As dialogue did not open.");
            FocusWindow(saveWindow!); TextBox? filename = null;
            Wait(() => (filename = saveWindow!.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Edit).And(cf.ByName("File name:")))?.AsTextBox())?.IsEnabled == true,
                "The Save As filename input did not become ready.");
            var renamed = Path.Combine(output, "right-copy.md"); filename!.Focus();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A); Keyboard.Type(renamed);
            Wait(() => filename.Text == renamed, "The Save As filename did not match the intended output.");
            (saveWindow!.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName("Save")))
                ?? throw new IOException("The Save As button is unavailable.")).AsButton().Click();
            Wait(() => File.Exists(renamed) && File.ReadAllText(renamed) == rightBuffer, "Save As did not retain the active right source.");
            FocusWindow(main);
            Check(File.ReadAllBytes(left).SequenceEqual(originalLeft) && File.ReadAllText(right) == rightBuffer,
                "Actual native Save As preserves both the other document and the previous right file");
            Find(main, "ActivateComparison1").AsButton().Invoke();
            Wait(() => Find(main, "FindText").AsTextBox().Text == "Left pending" && ReadEditorText(editor) == leftBuffer, "Left Find/source state was not restored.");
            editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => ReadEditorText(editor) == spc, "Left Undo affected the wrong document or lost its own history.");
            Check(File.ReadAllText(right) == rightBuffer, "Left Undo cannot change the right document or its saved file");
            Find(main, "ComparisonMode0").AsComboBox().Select("Preview"); Find(main, "ComparisonMode1").AsComboBox().Select("Preview");
            Wait(() => !Find(main, "DocumentPreview").IsOffscreen && !Find(main, "ComparisonPreview").IsOffscreen, "Preview/Preview did not expose both readers.");
            main.CaptureToFile(Path.Combine(output, "preview-preview.png"));
            Find(main, "CloseComparison").AsButton().Invoke();
            Check(ReadEditorText(editor) == spc && File.ReadAllText(right) == rightBuffer, "Closing comparison retains the active source and the other saved document");
            File.WriteAllText(Path.Combine(output, "completion.json"), JsonSerializer.Serialize(new { status = "PASS", highContrast,
                source = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion,
                scope = "GITHUB_HOSTED_WINDOWS_POINTER_KEYBOARD_UIA; NOT PHYSICAL_MACHINE_CERTIFICATION", checks }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
            void Check(bool condition, string message) { if (!condition) throw new IOException(message); checks.Add(message); }
        }
        catch (Exception error)
        {
            main?.CaptureToFile(Path.Combine(output, "failure.png"));
            File.WriteAllText(Path.Combine(output, "completion.json"), JsonSerializer.Serialize(new { status = "FAIL", highContrast, checks, error = error.ToString() }));
            Console.Error.WriteLine(error); return 1;
        }
        finally { if (!app.HasExited) app.Kill(); }
    }).GetAwaiter().GetResult();
}
