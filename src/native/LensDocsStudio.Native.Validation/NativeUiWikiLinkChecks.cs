using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckWikiLinks(Window owner, AutomationElement editor, UIA3Automation automation, int process, string output, string sourcePath, string targetPath)
    {
        var before = editor.Patterns.Value.Pattern.Value.Value; var saved = File.ReadAllText(sourcePath); var targetSaved = File.ReadAllText(targetPath);
        var previous = System.Windows.Clipboard.GetDataObject(); uint owned = 0;
        try
        {
            var targetName = Path.GetFileName(targetPath); var prefix = "\n\n[[" + targetName[..3];
            System.Windows.Clipboard.SetText(prefix); owned = GetClipboardSequenceNumber();
            FocusWindow(owner); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_V);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == before + prefix, "The native wiki completion fixture did not reach the editor.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SPACE);
            Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByAutomationId("WikiLinkCompletion"))) is not null,
                "Ctrl+Space did not open native workspace link suggestions.");
            owner.CaptureToFile(Path.Combine(output, "native-wikilink-completion.png"));
            Keyboard.Type(VirtualKeyShort.RETURN);
            var completed = before + "\n\n[[" + targetName + "]]";
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == completed, "Enter did not insert the full selected wiki path.");
            AutomationElement? wikiLink = null;
            // WPF exposes document hyperlinks through TextPattern children, outside ordinary tree lookup.
            Wait(() => (wikiLink = Find(owner, "DocumentPreview").Patterns.Text.Pattern.DocumentRange.GetChildren()
                .FirstOrDefault(element => element.Properties.Name.ValueOrDefault == targetName)) is not null,
                "The completed wikilink did not render as an accessible native link.");
            wikiLink!.Patterns.Invoke.Pattern.Invoke();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == targetSaved, "The native wikilink did not open its existing workspace document.");
            Find(owner, "NavigateBack").Click();
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == completed, "Back navigation lost the unsaved wiki source.");
            FocusWindow(owner); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == before + prefix, "One Undo did not restore the incomplete wikilink.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == before, "Undo did not restore source before the wiki fixture.");
            const string missing = "\n\n[[Missing native wiki target|Missing wiki target]]";
            System.Windows.Clipboard.SetText(missing); owned = GetClipboardSequenceNumber();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_V);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == before + missing
                && (wikiLink = Find(owner, "DocumentPreview").Patterns.Text.Pattern.DocumentRange.GetChildren()
                    .FirstOrDefault(element => element.Properties.Name.ValueOrDefault == "Missing wiki target")) is not null,
                "The unresolved wikilink did not render its current alias.");
            wikiLink!.Patterns.Invoke.Pattern.Invoke();
            Wait(() => Find(owner, "Status").Name.StartsWith("No workspace document matches this wikilink.", StringComparison.Ordinal),
                "The unresolved wikilink did not report its missing workspace target.");
            if (editor.Patterns.Value.Pattern.Value.Value != before + missing) throw new IOException("An unresolved wiki target changed the active document.");
            FocusWindow(owner); editor.Click(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
            Wait(() => editor.Patterns.Value.Pattern.Value.Value == before, "Unresolved wiki insertion Undo changed the original source.");
            if (File.ReadAllText(sourcePath) != saved || File.ReadAllText(targetPath) != targetSaved)
                throw new IOException("Wiki completion, navigation or Undo changed a saved source document.");
        }
        finally { RestoreClipboard(previous, owned); }
    }
}
