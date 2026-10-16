using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void CheckWorkspaceViewportOwnership(string executable, string output)
    {
        var root = Path.Combine(output, "workspace-viewport"); Directory.CreateDirectory(root);
        var texts = new Dictionary<string, string>(); var observations = new List<object>();
        foreach (var name in new[] { "A", "B", "C" })
        {
            texts[name] = "# Document " + name + "\n\n" + string.Concat(Enumerable.Range(1, 90).Select(index =>
                $"## Region {index:000}\n\nDocument {name} has a distinct portable source region {index:000}, with **formatted text**.\n\n"));
            File.WriteAllText(Path.Combine(root, name + ".md"), texts[name]);
        }
        using var automation = new UIA3Automation();
        foreach (var mode in new[] { "Continuous", "Pages" })
        foreach (var synchronised in new[] { false, true })
        {
            var launch = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false };
            launch.ArgumentList.Add("--state-directory"); launch.ArgumentList.Add(Path.Combine(root, mode + synchronised));
            launch.ArgumentList.Add(root); launch.ArgumentList.Add(Path.Combine(root, "A.md"));
            using var app = Application.Launch(launch);
            var window = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("Viewport fixture did not open.");
            try
            {
                window.Patterns.Transform.Pattern.Resize(1280, 800);
                var editor = Find(window, "MarkdownEditor"); var preview = Find(window, "DocumentPreview");
                Wait(() => ReadEditorText(editor) == texts["A"] && HasPreview("A"), "First document did not finish its preview.");
                var reading = Find(window, "PreviewReadingMode").AsComboBox(); reading.Select(mode); reading.Collapse();
                Find(window, "FollowCaretControl").AsCheckBox().IsChecked = synchronised;
                Find(window, "SyncScrollControl").AsCheckBox().IsChecked = synchronised;
                Find(window, "SyncSelectionControl").AsCheckBox().IsChecked = false;
                editor.Focus(); editor.Patterns.Scroll.Pattern.SetScrollPercent(-1, 75);
                PreviewRange("Region 065").ScrollIntoView(true);
                Wait(() => Visible(preview, "Region 065"), "Document A did not reach its reading position.");
                var aOffset = editor.Patterns.Scroll.Pattern.VerticalScrollPercent.Value;
                Open("B"); AtTop("B");
                editor.Focus(); editor.Patterns.Scroll.Pattern.SetScrollPercent(-1, 40);
                PreviewRange("Region 035").ScrollIntoView(true);
                Wait(() => Visible(preview, "Region 035"), "Document B did not reach its reading position.");
                var bOffset = editor.Patterns.Scroll.Pattern.VerticalScrollPercent.Value;
                Open("A");
                Wait(() => Math.Abs(editor.Patterns.Scroll.Pattern.VerticalScrollPercent.Value - aOffset) < 3 && Visible(preview, "Region 065"), "Returning document A inherited another document's viewport.");
                window.CaptureToFile(Path.Combine(root, mode + "-sync-" + synchronised + "-restored.png"));
                Open("C"); AtTop("C");
                // Sidebar activation of an already-open file must use that same tab's state.
                Open("B");
                Wait(() => Math.Abs(editor.Patterns.Scroll.Pattern.VerticalScrollPercent.Value - bOffset) < 3 && Visible(preview, "Region 035"), "Sidebar activation did not restore document B's own viewport.");
                foreach (var name in new[] { "A", "C", "B", "C" }) Open(name, false);
                Wait(() => HasPreview("C") && Visible(preview, "Document C"), "A delayed preview replaced or repositioned the latest selected document.");
                foreach (var pair in texts) if (File.ReadAllText(Path.Combine(root, pair.Key + ".md")) != pair.Value) throw new IOException("Viewport navigation mutated disk source.");
                observations.Add(new { ReadingMode = mode, Synchronised = synchronised, FirstOpenTop = true, ReturnOwnPosition = true, SidebarExistingTab = true, RapidLatestWins = true, AEditorPercent = aOffset, BEditorPercent = bOffset });

                void Open(string name, bool wait = true)
                {
                    var tree = Find(window, "WorkspaceTree");
                    var entry = tree.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TreeItem))
                        .FirstOrDefault(item => item.Name.StartsWith(name + ".md", StringComparison.Ordinal)) ?? throw new IOException("Sidebar document missing: " + name);
                    entry.AsTreeItem().Select(); entry.Focus(); Keyboard.Type(VirtualKeyShort.RETURN);
                    Wait(() => ReadEditorText(editor) == texts[name], "Sidebar did not activate " + name);
                    if (wait) Wait(() => HasPreview(name), "Current document preview missing: " + name);
                }
                bool HasPreview(string name) => preview.Patterns.Text.Pattern.DocumentRange.GetText(-1).Contains("Document " + name, StringComparison.Ordinal)
                    && Find(window, "PreviewStatus").Name.StartsWith("Preview updated", StringComparison.Ordinal);
                FlaUI.Core.ITextRange PreviewRange(string text) => preview.Patterns.Text.Pattern.DocumentRange.FindText(text, false, false) ?? throw new IOException("Missing preview text: " + text);
                bool Visible(AutomationElement pane, string text) => PreviewRange(text).GetBoundingRectangles().Any(box => box.Width > 0 && box.Height > 0 && pane.BoundingRectangle.Contains(box));
                void AtTop(string name)
                {
                    Wait(() => editor.Patterns.Scroll.Pattern.VerticalScrollPercent.Value < 1 && Visible(preview, "Document " + name), "New document did not start at the top: " + name);
                    window.CaptureToFile(Path.Combine(root, mode + "-sync-" + synchronised + "-new-" + name + ".png"));
                }
            }
            catch (Exception error)
            {
                window.CaptureToFile(Path.Combine(root, "failure.png"));
                File.WriteAllText(Path.Combine(root, "failure.json"), JsonSerializer.Serialize(new { Mode = mode, Synchronised = synchronised, Error = error.ToString(), Observations = observations })); throw;
            }
            finally { if (!app.HasExited) app.Kill(); }
        }
        File.WriteAllText(Path.Combine(root, "viewport.json"), JsonSerializer.Serialize(new { Passed = true, Observations = observations }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
