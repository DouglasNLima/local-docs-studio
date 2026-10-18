using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckOutlineModernisation(Window window, AutomationElement editor, string output, string source)
    {
        var original = ReadEditorText(editor); var bytes = File.ReadAllBytes(source);
        const string fixture = "# Root\n\n## Branch\n\n### Leaf\n\n## Other\n\nPortable outline content.\n";
        var clipboard = NativeUiClipboard.GetDataObject(); uint owned = 0;
        Exception? primary = null;
        NativeOutlineFocusProbe? focusProbe = null;
        var outline = Find(window, "Outline").AsListBox(); var filter = Find(window, "OutlineFilter").AsTextBox();
        try
        {
            focusProbe = NativeOutlineFocusProbe.StartIfRequested();
            if (Environment.GetEnvironmentVariable("LENS_NATIVE_OUTLINE_FOCUS_PROBE") == "repaired") FocusWindow(window);
            editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
            NativeUiClipboard.SetText(fixture); owned = GetClipboardSequenceNumber();
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.SHIFT, VirtualKeyShort.KEY_V);
            Wait(() => ReadEditorText(editor) == fixture && outline.Items.Length == 4, "The native outline did not expose its current hierarchy.");
            var previewStatus = Find(window, "PreviewStatus").Name;
            Group("Collapse all heading groups"); Wait(() => outline.Items.Length == 1 && outline.Items[0].Name == "Root", "Collapsing heading groups retained hidden descendants.");
            filter.Text = "Leaf"; Wait(() => outline.Items.Length == 1 && outline.Items[0].Name == "Leaf", "Outline filtering did not reveal a matching collapsed descendant.");
            filter.Text = ""; Wait(() => outline.Items.Length == 1 && outline.Items[0].Name == "Root", "Clearing outline filtering lost its retained collapsed groups.");
            Group("Expand all heading groups"); Wait(() => outline.Items.Length == 4, "Expanding outline groups lost a current heading.");
            outline.Items.Single(item => item.Name == "Branch").Click();
            Wait(() => Find(window, "CaretStatus").Name.StartsWith("Ln 3,", StringComparison.Ordinal), "Outline navigation did not reach the selected heading's exact source line.");
            // Mouse navigation deliberately returns editor focus. Focus the real outline
            // for its independent keyboard group command.
            outline.Focus(); Keyboard.Type(VirtualKeyShort.LEFT);
            Wait(() => outline.Items.Length == 3 && outline.Items.All(item => item.Name != "Leaf"), "Outline Left did not collapse its selected heading group.");
            Keyboard.Type(VirtualKeyShort.RIGHT); Wait(() => outline.Items.Length == 4, "Outline Right did not restore its selected heading group.");
            editor.Focus(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
            Wait(() => outline.SelectedItem?.Name == "Other", "The current outline section did not follow the actual editor caret.");
            if (Find(window, "PreviewStatus").Name != previewStatus) throw new IOException("Outline controls reparsed or rebuilt the preview.");
            if (ReadEditorText(editor) != fixture || !File.ReadAllBytes(source).SequenceEqual(bytes)) throw new IOException("Outline controls changed source content.");
            File.WriteAllText(Path.Combine(output, "native-outline-modernisation.json"), JsonSerializer.Serialize(new {
                Passed = true, HierarchyCollapseExpand = true, FilterKeepsCollapsedGroups = true, ExactSourceLine = true,
                KeyboardGroups = true, CurrentCaretSection = true, NoPreviewRebuild = true, SourceBytesPreserved = true }));
        }
        catch (Exception error)
        {
            primary = error;
            Observe("failure-before-cleanup");
            try { File.WriteAllText(Path.Combine(output, "native-outline-modernisation-failure.json"), JsonSerializer.Serialize(new { Error = error.ToString() })); } catch { }
            try { window.CaptureToFile(Path.Combine(output, "native-outline-modernisation-failure.png")); } catch (Exception) { }
            throw;
        }
        finally
        {
            try { focusProbe?.Dispose(); } catch when (primary is not null) { }
            try
            {
                filter.Text = ""; Group("Expand all heading groups"); editor.Focus();
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                Wait(() => ReadEditorText(editor) == original, "The outline fixture did not retain its complete insertion Undo.");
            }
            catch (Exception error) when (primary is not null)
            { try { File.WriteAllText(Path.Combine(output, "native-outline-modernisation-cleanup.json"), JsonSerializer.Serialize(new { Error = error.ToString() })); } catch { } }
            finally { try { RestoreClipboard(clipboard, owned); } catch when (primary is not null) { } }
        }
        // Synthetic-fixture diagnostics only: bounded metadata, no document text or paths.
        // Each probe is independent and must never replace the original assertion failure.
        void Observe(string phase)
        {
            var state = new Dictionary<string, object?> { ["Phase"] = phase, ["ExpectedHeadingCount"] = 4,
                ["ExpectedEditorLength"] = fixture.Length, ["ExpectedEditorSha256"] = Hash(fixture), ["OwnedClipboardSequence"] = owned };
            Probe("Editor", () => { var value = editor.Patterns.Value.Pattern.Value.Value; return new { Length = value.Length, Sha256 = Hash(value), ExactFixture = value == fixture, Original = value == original, editor.Properties.HasKeyboardFocus.Value }; });
            Probe("Outline", () => new { Count = outline.Items.Length, Items = outline.Items.Take(16).Select(item => new { NameSha256 = Hash(item.Name), item.IsSelected }).ToArray() });
            Probe("Preview", () => new { Status = Find(window, "PreviewStatus").Name, Diagnostic = Find(window, "PreviewStatus").Properties.HelpText.Value });
            Probe("ClipboardSequence", () => GetClipboardSequenceNumber());
            Probe("ForegroundIsApplication", () => { GetWindowThreadProcessId(GetForegroundWindow(), out var pid); return pid == window.Properties.ProcessId.Value; });
            Probe("Controls", () => window.FindAllDescendants().Take(256).Select(item => new { item.AutomationId, Type = item.ControlType.ToString(), item.Properties.HasKeyboardFocus.Value, item.BoundingRectangle }).ToArray());
            try { File.WriteAllText(Path.Combine(output, "native-outline-state-" + phase + ".json"), JsonSerializer.Serialize(state)); } catch { }
            void Probe(string name, Func<object?> read) { try { state[name] = read(); } catch (Exception ex) { state[name] = new { Unavailable = ex.GetType().Name }; } }
        }
        static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        void Group(string name) => (window.FindFirstDescendant(cf => cf.ByControlType(ControlType.Button).And(cf.ByName(name)))
            ?? throw new IOException("The actual outline group command is unavailable: " + name)).AsButton().Invoke();
    }
}
