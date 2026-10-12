using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
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
