using System.Diagnostics;
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
    public static int RunWorkspaceViewports(string executable, string output) => Task.Run(() =>
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        using var display = new NativeTestDisplay(output);
        try { CheckWorkspaceViewportOwnership(executable, output, true); display.Complete(); return 0; }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }).GetAwaiter().GetResult();

    private static void CheckWorkspaceViewportOwnership(string executable, string output, bool pagesFirst = false)
    {
        var root = Path.Combine(output, "workspace-viewport"); Directory.CreateDirectory(root);
        var texts = new Dictionary<string, string>(); var observations = new List<object>(); var readingDiagnostics = new List<object>();
        foreach (var name in new[] { "A", "B", "C" })
        {
            texts[name] = "# Document " + name + "\n\n" + string.Concat(Enumerable.Range(1, 90).Select(index =>
                $"## Region {index:000}\n\nDocument {name} has a distinct portable source region {index:000}, with **formatted text**.\n\n"
                + (name == "A" && index == 65 ? "[Visit section](C.md#region-040)\n\n" : "")));
            File.WriteAllText(Path.Combine(root, name + ".md"), texts[name]);
        }
        using var automation = new UIA3Automation();
        foreach (var mode in pagesFirst ? new[] { "Pages", "Continuous" } : new[] { "Continuous", "Pages" })
        foreach (var synchronised in new[] { false, true })
        {
            var launch = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false };
            launch.Environment["LENS_NATIVE_VIEWPORT_DIAGNOSTICS"] = "1";
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
                SetSynchronisation("FollowCaretControl", synchronised);
                SetSynchronisation("SyncScrollControl", synchronised);
                SetSynchronisation("SyncSelectionControl", false);
                Wait(() =>
                {
                    var preferencePath = Path.Combine(root, mode + synchronised, "preferences.json");
                    if (!File.Exists(preferencePath)) return false;
                    using var input = new FileStream(preferencePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var desktop = JsonSerializer.Deserialize<UserPreferences>(input)?.Desktop ?? new DesktopPreferences();
                    return desktop.FollowCaret == synchronised && desktop.SyncScroll == synchronised && !desktop.SyncSelection;
                }, "The requested synchronisation preferences did not finish applying.");
                ScrollEditorTo(75, "Region 065");
                ReadAt("A", "Region 065");
                var aOffset = editor.Patterns.Scroll.Pattern.VerticalScrollPercent.Value;
                Open("B"); AtTop("B");
                ScrollEditorTo(40, "Region 035");
                ReadAt("B", "Region 035");
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
                Open("A");
                AutomationElement? link = null;
                Wait(() => (link = preview.Patterns.Text.Pattern.DocumentRange.GetChildren().FirstOrDefault(element => element.Name == "Visit section")) is not null, "The native anchor link was unavailable.");
                link!.Patterns.Invoke.Pattern.Invoke();
                Wait(() => ReadEditorText(editor) == texts["C"] && HasPreview("C") && Visible(preview, "Region 040"), "Link navigation did not honour the requested section.");
                if (mode == "Pages")
                {
                    var traces = ViewportTraceLines()
                        .Select(line => JsonSerializer.Deserialize<JsonElement>(line)).ToArray();
                    var duringOperation = traces.Last(item => item.GetProperty("Operation").GetString()!.StartsWith("anchor-target", StringComparison.Ordinal));
                    var settled = traces.Last(item => item.GetProperty("Operation").GetString()!.StartsWith("anchor-final-layout", StringComparison.Ordinal));
                    if (Math.Abs(duringOperation.GetProperty("PreviewHeight").GetDouble() - settled.GetProperty("PreviewHeight").GetDouble()) > .5)
                        throw new IOException("The operation status control changed the native reader's available height.");
                }
                Find(window, "NavigateBack").AsButton().Invoke();
                Wait(() => ReadEditorText(editor) == texts["A"] && HasPreview("A") && Visible(preview, "Region 065"), "Back did not restore the previous document's reading position.");
                Find(window, "NavigateForward").AsButton().Invoke();
                Wait(() => ReadEditorText(editor) == texts["C"] && HasPreview("C") && Visible(preview, "Region 040"), "Forward did not restore the requested anchor.");
                foreach (var pair in texts) if (File.ReadAllText(Path.Combine(root, pair.Key + ".md")) != pair.Value) throw new IOException("Viewport navigation mutated disk source.");
                observations.Add(new { ReadingMode = mode, Synchronised = synchronised, FirstOpenTop = true, ReturnOwnPosition = true, SidebarExistingTab = true, RapidLatestWins = true, AnchorBackForward = true, AEditorPercent = aOffset, BEditorPercent = bOffset });

                void SetSynchronisation(string id, bool enabled)
                {
                    var control = window.FindFirstDescendant(cf => cf.ByAutomationId(id));
                    if (control is { IsOffscreen: false })
                    {
                        control.AsCheckBox().IsChecked = enabled;
                        return;
                    }
                    // The always-visible zoom percentage leaves secondary commands in
                    // WPF's native overflow at this width. Exercise the actual control;
                    // retain the persisted-preference and viewport assertions below.
                    var toolbar = window.FindFirstDescendant(cf => cf.ByName("Preview commands"))
                        ?? throw new IOException("The preview toolbar is unavailable.");
                    var more = toolbar.FindFirstDescendant(cf => cf.ByName("More commands"))
                        ?? throw new IOException("The preview overflow command is unavailable.");
                    if (more.IsOffscreen) throw new IOException("The preview overflow command is clipped.");
                    more.Click();
                    try
                    {
                        Wait(() => (control = automation.GetDesktop().FindAllDescendants(cf => cf.ByProcessId(app.ProcessId).And(cf.ByAutomationId(id)))
                            .FirstOrDefault(item => !item.IsOffscreen)) is not null, "Preview overflow did not expose " + id);
                        control!.AsCheckBox().IsChecked = enabled;
                    }
                    finally
                    {
                        if (more.Patterns.Toggle.Pattern.ToggleState.Value == FlaUI.Core.Definitions.ToggleState.On)
                            more.Patterns.Toggle.Pattern.Toggle();
                    }
                }
                void Open(string name, bool wait = true)
                {
                    var tree = Find(window, "WorkspaceTree");
                    var entry = tree.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.TreeItem))
                        .FirstOrDefault(item => item.Name.StartsWith(name + ".md", StringComparison.Ordinal)) ?? throw new IOException("Sidebar document missing: " + name);
                    entry.AsTreeItem().Select(); entry.Focus(); Keyboard.Type(VirtualKeyShort.RETURN);
                    Wait(() => ReadEditorText(editor) == texts[name], "Sidebar did not activate " + name);
                    if (wait) Wait(() => HasPreview(name), "Current document preview missing: " + name);
                }
                bool HasPreview(string name) => Find(window, "PreviewStatus").Name.StartsWith("Preview updated", StringComparison.Ordinal)
                    && preview.Patterns.Text.PatternOrDefault is { } text && text.DocumentRange.GetText(-1).Contains("Document " + name, StringComparison.Ordinal);
                FlaUI.Core.ITextRange PreviewRange(string text) => preview.Patterns.Text.Pattern.DocumentRange.FindText(text, false, false) ?? throw new IOException("Missing preview text: " + text);
                void ScrollEditorTo(double percent, string target)
                {
                    var beforeSync = SyncCompletions("editor-scroll");
                    editor.Focus(); editor.Patterns.Scroll.Pattern.SetScrollPercent(-1, percent);
                    // This ownership test performs two distinct reading actions. Complete
                    // the requested editor alignment before beginning the preview action;
                    // UIA ScrollIntoView does not deliver a mouse-wheel cancellation event.
                    if (synchronised) Wait(() => SyncCompletions("editor-scroll") > beforeSync, "The initial editor scroll did not finish synchronising before preview reading.");
                    RecordReading("editor-aligned", target);
                }
                void ReadAt(string name, string text)
                {
                    var focusRange = preview.Patterns.Text.Pattern.GetVisibleRanges()
                        .Select(range => range.FindText("Document " + name, false, false)).FirstOrDefault(range => range is not null)
                        ?? throw new IOException("No visible native text was available to focus the reader.");
                    var focusBox = focusRange.GetBoundingRectangles().First(box => preview.BoundingRectangle.Contains(box));
                    Mouse.Click(new System.Drawing.Point(focusBox.Left + focusBox.Width * (name == "A" ? 1 : 2) / 3, focusBox.Top + focusBox.Height / 2));
                    var alreadyVisible = Visible(preview, text); var syncStart = SyncCompletions();
                    RecordReading("preview-focused", text);
                    // WPF UIA can treat an already-visible range's viewport coordinate
                    // as an absolute document offset. Do not request redundant scrolling;
                    // the same visibility/ownership assertions remain mandatory below.
                    if (!alreadyVisible) PreviewRange(text).ScrollIntoView(true);
                    RecordReading(alreadyVisible ? "reading-target-already-visible" : "scroll-requested", text);
                    Wait(() => Visible(preview, text), "Document " + name + " did not reach its reading position.");
                    if (synchronised && !alreadyVisible)
                        Wait(() => SyncCompletions() > syncStart, "Document " + name + "'s focused preview scroll did not finish synchronising.");
                }
                int SyncCompletions(string origin = "preview-scroll")
                {
                    return ViewportTraceLines().Count(line => line.Contains("sync-complete " + origin, StringComparison.Ordinal));
                }
                string[] ViewportTraceLines()
                {
                    var tracePath = Path.Combine(root, mode + synchronised, "viewport-navigation.jsonl");
                    if (!File.Exists(tracePath)) return [];
                    using var stream = new FileStream(tracePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream);
                    return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Where(line => line.TrimEnd().EndsWith('}')).ToArray();
                }
                void RecordReading(string phase, string target)
                {
                    var focus = automation.FocusedElement();
                    readingDiagnostics.Add(new { Mode = mode, Synchronised = synchronised, Phase = phase, Target = target,
                        FocusedId = focus?.AutomationId, FocusedType = focus?.ControlType.ToString(), EditorPercent = editor.Patterns.Scroll.Pattern.VerticalScrollPercent.Value,
                        PreviewBounds = preview.BoundingRectangle.ToString(), TargetBounds = PreviewRange(target).GetBoundingRectangles().Select(box => box.ToString()).ToArray(),
                        VisibleText = preview.Patterns.Text.Pattern.GetVisibleRanges().Select(range => range.GetText(160)).ToArray() });
                    File.WriteAllText(Path.Combine(root, "reading-actions.json"), JsonSerializer.Serialize(readingDiagnostics, new JsonSerializerOptions { WriteIndented = true }));
                }
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
