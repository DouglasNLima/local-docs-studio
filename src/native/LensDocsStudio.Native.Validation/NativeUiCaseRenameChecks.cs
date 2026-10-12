using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckCaseOnlyRename(Window owner, AutomationElement editor, UIA3Automation automation, int process, string source, string target)
    {
        var before = ReadEditorText(editor); var sourceSaved = File.ReadAllText(source); var targetSaved = File.ReadAllBytes(target);
        var originalName = Path.GetFileName(target); var upperName = Path.GetFileNameWithoutExtension(target).ToUpperInvariant() + Path.GetExtension(target);
        var originalReference = Uri.EscapeDataString(originalName); var upperReference = Uri.EscapeDataString(upperName);
        Find(owner, "NavigateForward").Click();
        Wait(() => editor.Patterns.Value.Pattern.Value.Value == File.ReadAllText(target), "Forward did not restore the document for case-only rename validation.");
        Rename(upperName);
        Wait(() => Directory.EnumerateFiles(Path.GetDirectoryName(target)!).Any(path => Path.GetFileName(path) == upperName)
            && File.ReadAllText(source).Contains(upperReference, StringComparison.Ordinal), "The case-only rename did not update the actual filename and incoming reference.");
        Find(owner, "NavigateBack").Click();
        Wait(() => editor.Patterns.Value.Pattern.Value.Value == before.Replace(originalReference, upperReference, StringComparison.Ordinal),
            "Case-only rename did not refresh the affected open draft or navigation history.");
        Find(owner, "NavigateForward").Click();
        Wait(() => editor.Patterns.Value.Pattern.Value.Value == File.ReadAllText(target), "Forward did not restore the case-renamed document.");
        Rename(originalName);
        Wait(() => Directory.EnumerateFiles(Path.GetDirectoryName(target)!).Any(path => Path.GetFileName(path) == originalName)
            && File.ReadAllText(source) == sourceSaved, "Renaming the document back did not restore the original file/link spelling.");
        Find(owner, "NavigateBack").Click();
        Wait(() => editor.Patterns.Value.Pattern.Value.Value == before, "Returning from the restored filename lost the source draft.");
        if (!File.ReadAllBytes(target).SequenceEqual(targetSaved)) throw new IOException("Case-only renaming changed document bytes.");
        void Rename(string filename)
        {
            FocusWindow(owner); Keyboard.Type(VirtualKeyShort.F2);
            Window? pathWindow = null;
            Wait(() => (pathWindow = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Rename or move document")))?.AsWindow()) is not null,
                "F2 did not open the case-only rename path dialogue.");
            Find(pathWindow!, "WorkspacePathInput").AsTextBox().Text = filename;
            Find(pathWindow!, "ConfirmWorkspacePath").Patterns.Invoke.Pattern.Invoke();
            Window? review = null;
            Wait(() => (review = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(process).And(cf.ByName("Review document move")))?.AsWindow()) is not null,
                "A case-only filename was rejected before the reviewed move.");
            Find(review!, "ApplyDocumentMove").Patterns.Invoke.Pattern.Invoke();
        }
    }
}
