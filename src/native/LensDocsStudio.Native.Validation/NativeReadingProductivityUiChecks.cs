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
        using var clipboardApartment = NativeUiClipboard.Start();
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
            Check(System.Windows.SystemParameters.HighContrast == highContrast, "The requested operating-system appearance is active");
            var clipboard = NativeUiClipboard.GetDataObject(); uint ownedClipboard = 0;
            try
            {
                (main.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName("Copy Table of Contents")))
                    ?? throw new IOException("The outline Copy Table of Contents command is unavailable.")).AsButton().Invoke();
                Window? toc = null;
                Wait(() => (toc = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window)).And(cf.ByName("Copy Table of Contents")))?.AsWindow()) is not null,
                    "The outline TOC dialogue did not open.");
                var tocWindow = toc ?? throw new IOException("The TOC window is unavailable.");
                var generated = (tocWindow.FindFirstDescendant(cf => cf.ByName("Generated table of contents")) ?? throw new IOException("The generated TOC preview is unavailable.")).AsTextBox();
                Wait(() => generated.Text.Trim() == "- [Right](#right)", "The TOC did not retain the actual rendered heading anchor.");
                var listStyle = (tocWindow.FindFirstDescendant(cf => cf.ByName("Table of contents style")) ?? throw new IOException("The TOC style is unavailable.")).AsComboBox();
                listStyle.Select("Numbered list"); listStyle.Collapse();
                var wrapper = tocWindow.FindFirstDescendant(cf => cf.ByName("Collapsible wrapper")) ?? throw new IOException("The TOC wrapper is unavailable.");
                wrapper.Patterns.Toggle.Pattern.Toggle();
                Wait(() => wrapper.AsCheckBox().IsChecked == true, "The TOC wrapper toggle did not become checked.");
                Wait(() => generated.Text.Contains("<details>", StringComparison.Ordinal) && generated.Text.Contains("1. [Right](#right)", StringComparison.Ordinal),
                    "The actual TOC options did not generate a numbered collapsible index.");
                (tocWindow.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button).And(cf.ByName("Copy Table of Contents"))) ?? throw new IOException("The TOC copy button is unavailable.")).AsButton().Invoke();
                Wait(() => NativeUiClipboard.GetText() == generated.Text, "Copy TOC did not put the exact generated index on the clipboard.");
                ownedClipboard = GetClipboardSequenceNumber(); FocusWindow(tocWindow); Keyboard.Type(VirtualKeyShort.ESCAPE);
                FocusWindow(main);
                Check(ReadEditorText(editor) == "# Right\n\nRight source", "Actual outline TOC options and clipboard copying preserve the source document");
            }
            finally { RestoreClipboard(clipboard, ownedClipboard); }

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
            var secondaryMode = Find(main, "ComparisonMode1").AsComboBox(); secondaryMode.Select("Editor"); secondaryMode.Collapse();
            Find(main, "ActivateComparison1").AsButton().Invoke();
            Wait(() => ReadEditorText(editor) == spc, "The left activation did not bind its authoritative source.");
            editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END); Keyboard.Type(VirtualKeyShort.RETURN);
            var leftBeforeTyping = spc + "\n";
            Wait(() => ReadEditorText(editor) == leftBeforeTyping, "The left document did not receive the new paragraph.");
            var leftBeforeFinalKey = leftBeforeTyping + "Left pendin";
            Keyboard.Type("Left pendin");
            Wait(() => ReadEditorText(editor) == leftBeforeFinalKey, "The left typing prefix did not finish.");
            // Delimit the final ordinary keystroke through real caret input;
            // comparison must retain AvalonEdit's own undo grouping.
            Keyboard.Type(VirtualKeyShort.LEFT); Keyboard.Type(VirtualKeyShort.RIGHT); Keyboard.Type("g");
            var leftBuffer = leftBeforeFinalKey + "g";
            Wait(() => ReadEditorText(editor) == leftBuffer, "The left typing operation did not finish before switching documents.");
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
            Wait(() => ReadEditorText(editor) == leftBeforeFinalKey, "Left Undo affected the wrong document or lost its own history.");
            Check(File.ReadAllText(right) == rightBuffer, "Left Undo cannot change the right document or its saved file");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Y);
            Wait(() => ReadEditorText(editor) == leftBuffer, "Left Redo did not restore its own final keystroke.");
            Check(File.ReadAllText(right) == rightBuffer, "Left Redo retains the right document's saved source");
            var primaryMode = Find(main, "ComparisonMode0").AsComboBox(); primaryMode.Select("Preview"); primaryMode.Collapse();
            secondaryMode.Select("Preview"); secondaryMode.Collapse();
            Wait(() => !Find(main, "DocumentPreview").IsOffscreen && !Find(main, "ComparisonPreview").IsOffscreen, "Preview/Preview did not expose both readers.");
            main.CaptureToFile(Path.Combine(output, "preview-preview.png"));
            Find(main, "CloseComparison").AsButton().Invoke();
            Check(ReadEditorText(editor) == leftBuffer && File.ReadAllText(right) == rightBuffer, "Closing comparison retains the active source and the other saved document");
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
