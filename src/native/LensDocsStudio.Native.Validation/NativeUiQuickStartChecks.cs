using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckQuickStart(Window owner, AutomationElement editor, UIA3Automation automation, int process, string output, string source)
    {
        var saved = File.ReadAllBytes(source); var original = ReadEditorText(editor);
        FocusWindow(owner); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N);
        Wait(() => ReadEditorText(editor).Length == 0 && Visible("QuickStartHelp"), "A new empty draft has no quick-start guidance.");
        if (!Visible("QuickStartWorkspace") || !Visible("QuickStartDocument")) throw new IOException("Quick start hides a native open action.");
        Find(owner, "QuickStartHelp").AsButton().Invoke();
        Window? guide = null;
        Wait(() => (guide = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Native guide")))?.AsWindow()) is not null,
            "Quick start did not open the offline guide.");
        Find(guide!, "CloseHelp").AsButton().Invoke();
        Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Native guide"))) is null, "Quick-start help did not close.");
        FocusWindow(owner); editor.Click(); Keyboard.Type("Café");
        Wait(() => ReadEditorText(editor) == "Café" && !Visible("QuickStartHelp"), "Typing did not replace quick-start guidance without losing source.");
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
        Keyboard.Type(VirtualKeyShort.BACK);
        Wait(() => ReadEditorText(editor).Length == 0 && Visible("QuickStartHelp"), "Emptying the draft did not restore optional guidance.");
        owner.CaptureToFile(Path.Combine(output, "native-quick-start.png"));
        Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_W);
        Window? unsaved = null;
        Wait(() => (unsaved = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Unsaved document")))?.AsWindow()) is not null,
            "Closing the edited draft did not preserve its unsaved-document prompt.");
        unsaved!.FindFirstDescendant(cf => cf.ByName("No").And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.Button)))!.AsButton().Invoke();
        Wait(() => ReadEditorText(editor) == original, "Closing the empty quick-start draft did not restore the previous document.");
        if (!File.ReadAllBytes(source).SequenceEqual(saved)) throw new IOException("Quick start changed the original workspace file.");
        File.WriteAllText(Path.Combine(output, "native-quick-start.json"), JsonSerializer.Serialize(new { passed = true, offlineHelp = true, typingPreserved = true, originalBytesPreserved = true }));
        bool Visible(string id) => owner.FindFirstDescendant(cf => cf.ByAutomationId(id)) is { IsOffscreen: false };
    }
}
