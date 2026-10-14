using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using System.Diagnostics;
using System.Text.Json;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckAbout(Window owner, AutomationElement editor, UIA3Automation automation, int process, string output, string source, string executable)
    {
        var original = ReadEditorText(editor); var saved = File.ReadAllBytes(source);
        var identity = (FileVersionInfo.GetVersionInfo(Path.GetFullPath(executable)).ProductVersion
            ?? throw new IOException("The tested executable has no build identity.")).Split('+', 2);
        if (identity.Length != 2 || identity[1].Length != 40) throw new IOException("The tested native build does not identify its exact Git source.");
        FocusWindow(owner); Find(owner, "HelpMenu").Click(); Find(owner, "AboutNativePreview").Patterns.Invoke.Pattern.Invoke();
        Window? about = null;
        AutomationElement? acknowledge = null;
        Wait(() =>
        {
            about = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process)
                .And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Window)).And(cf.ByName("About Lens Docs Studio")))?.AsWindow();
            if (about is null) return false;
            var body = string.Join('\n', about.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Text)).Select(element => element.Name));
            acknowledge = about.FindFirstDescendant(cf => cf.ByName("OK").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)));
            return acknowledge is { IsEnabled: true, IsOffscreen: false } && body.Contains("Version: " + identity[0], StringComparison.Ordinal)
                && body.Contains("Source: " + identity[1], StringComparison.Ordinal);
        }, "About did not expose the exact executable version/source and a ready OK button.");
        about!.CaptureToFile(Path.Combine(output, "native-about.png"));
        acknowledge!.AsButton().Invoke();
        Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("About Lens Docs Studio"))) is null, "About did not close.");
        if (ReadEditorText(editor) != original || !File.ReadAllBytes(source).SequenceEqual(saved)) throw new IOException("About changed document state.");
        File.WriteAllText(Path.Combine(output, "native-about.json"), JsonSerializer.Serialize(new { passed = true, version = identity[0], sourceRevision = identity[1], sourcePreserved = true }));
    }
    private static void CheckHelpGuide(Window owner, AutomationElement editor, UIA3Automation automation, int process, string output, string source)
    {
        var original = ReadEditorText(editor); var saved = File.ReadAllText(source);
        FocusWindow(owner); Keyboard.Type(VirtualKeyShort.F1);
        Window? guide = null;
        Wait(() => (guide = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Native guide")))?.AsWindow()) is not null,
            "F1 did not open the bundled native guide.");
        var query = Find(guide!, "HelpQuery").AsTextBox();
        Find(guide!, "HelpContent").Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
        Wait(() => query.Properties.HasKeyboardFocus.Value, "Ctrl+F inside help did not focus its topic search.");
        query.Text = "browser library";
        Wait(() => Find(guide!, "HelpTopics").AsListBox().Items.Length == 1
            && Find(guide!, "HelpContent").Patterns.Text.Pattern.DocumentRange.GetText(-1).Contains("Import JSON", StringComparison.Ordinal),
            "Help topic search did not find and render the bundled browser migration instructions.");
        guide!.CaptureToFile(Path.Combine(output, "native-help.png"));
        query.Text = "missing-topic-298714";
        Wait(() => Find(guide!, "HelpTopics").AsListBox().Items.Length == 0
            && Find(guide!, "HelpContent").Patterns.Text.Pattern.DocumentRange.GetText(-1).Contains("No matching topics", StringComparison.Ordinal),
            "Empty help search retained stale topic content.");
        query.Text = "";
        Wait(() => Find(guide!, "HelpTopics").AsListBox().Items.Length == 12, "Clearing help search did not restore all bundled topics.");
        Keyboard.Type(VirtualKeyShort.ESCAPE);
        Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Native guide"))) is null,
            "Escape did not close the native guide.");
        if (ReadEditorText(editor) != original || File.ReadAllText(source) != saved)
            throw new IOException("Using the native help guide changed the active document.");
    }
}
