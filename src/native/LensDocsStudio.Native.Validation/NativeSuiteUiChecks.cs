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
    public static int RunMermaidSuite(string executable, string output, bool highContrast, bool baseline = false, bool darkBaseline = false) => Task.Run(() =>
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        using var clipboard = NativeUiClipboard.Start(); using var contrast = highContrast ? new NativeTestHighContrast() : null;
        using var display = new NativeTestDisplay(output); using var automation = new UIA3Automation();
        var captureRoot = output; var measurements = new List<object>(); var checks = new List<string>(); Application? app = null; Window? visual = null;
        try
        {
            if (highContrast && !System.Windows.SystemParameters.HighContrast) throw new IOException("Actual Windows High Contrast was not enabled.");
            foreach (var theme in highContrast ? new[] { "System" } : baseline ? new[] { darkBaseline ? "Dark" : "Light" } : new[] { "Light", "Dark" })
            foreach (var family in new[] { "Flowchart", "Sequence", "State", "ER", "Class" })
            {
                var prefix = family == "Flowchart" ? "Mermaid" : family == "ER" ? "Er" : family;
                var root = Path.Combine(output, theme + "-" + family); Directory.CreateDirectory(root);
                captureRoot = root; var state = Path.Combine(root, "state"); Directory.CreateDirectory(state);
                File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new UserPreferences(Theme: theme, AutomaticUpdateChecks: false)));
                var source = NativeSuitePerformanceChecks.NavigationSource(family);
                var path = Path.Combine(root, family + ".mmd"); File.WriteAllText(path, source);
                var start = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false };
                foreach (var argument in new[] { "--state-directory", state, root, path }) start.ArgumentList.Add(argument);
                var coldProcess = Stopwatch.StartNew(); app = Application.Launch(start); var main = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("Suite fixture did not open.");
                Wait(() => main.FindFirstDescendant(cf => cf.ByAutomationId("DiagramEditVisually0")) is not null, "Suite visual action did not appear.");
                var editorOpen = Stopwatch.StartNew(); Find(main, "DiagramEditVisually0").AsButton().Invoke();
                Wait(() => (visual = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByAutomationId(prefix + "VisualEditor")))?.AsWindow()) is not null, "Suite editor did not open.");
                Wait(() => Find(visual!, family == "Flowchart" ? "MermaidInsertProcess" : prefix + "Apply").IsEnabled, "Suite canonical mapping did not finish.");
                var originalApplyEnabled = Find(visual!, prefix + "Apply").IsEnabled;
                if (!baseline && Find(visual!, prefix + "Zoom").Patterns.RangeValue.Pattern.Value.Value < 60)
                    throw new IOException("Initial editor fitting reduced labels below the shared readable zoom threshold.");
                using var nativeProcess = Process.GetProcessById(app.ProcessId); nativeProcess.Refresh();
                measurements.Add(new { Family = family, Theme = theme, ColdProcessIncludingDocumentPreviewAndUiaMs = coldProcess.Elapsed.TotalMilliseconds, EditorOpenIncludingUiaMs = editorOpen.Elapsed.TotalMilliseconds, WorkingSetAtFirstUsefulEditor = nativeProcess.WorkingSet64, Method = "Fresh app process per case; includes UIA transport and is separate from native phase timings" });
                Capture("initial");
                var list = Find(visual!, family == "Sequence" ? "SequenceParticipants" : prefix + "Elements");
                if (baseline)
                {
                    CaptureSuiteBaseline(visual!, main, list, family, prefix, source, name => Capture(name));
                    checks.Add(theme + " " + family + ": immutable consolidated baseline captures; owning source unchanged");
                    app.Kill(); app.Dispose(); app = null; visual = null; continue;
                }
                list.Focus(); Keyboard.Type(VirtualKeyShort.HOME);
                var summary = Find(visual!, prefix + "SelectionSummary"); Wait(() => !summary.Name.StartsWith("Select ", StringComparison.Ordinal), "Suite keyboard selection did not reach inspector.");
                var selectedName = list.Patterns.Selection.Pattern.Selection.Value.Single().Name;
                ObserveNavigation("initial", list, selectedName);
                if (string.IsNullOrWhiteSpace(selectedName)) throw new IOException("Suite selected entry has no accessible name.");
                if (family is "Flowchart" or "State" or "Sequence" && !selectedName.Contains("complete long documentation context", StringComparison.Ordinal))
                    throw new IOException("Suite full navigation name was truncated.");
                checks.Add(theme + " " + family + ": keyboard SelectionItem retains the full semantic description");
                var fieldId = family switch { "Flowchart" => "MermaidNodeLabel", "Sequence" => "SequenceParticipantId", _ => prefix + "NodeId" };
                var field = Find(visual!, fieldId).AsTextBox(); var fieldBefore = field.Text; var summaryBefore = summary.Name;
                field.Text = fieldBefore + "Pending"; Wait(() => !Find(visual!, prefix + "Apply").IsEnabled, "Pending properties did not block Apply.");
                ObserveNavigation("pending field", list, selectedName);
                var panel = Find(visual!, prefix + "SourcePanel"); var zoom = Find(visual!, prefix + "Zoom").Patterns.RangeValue.Pattern.Value.Value;
                panel.Patterns.ExpandCollapse.Pattern.Expand(); panel.Patterns.ExpandCollapse.Pattern.Collapse();
                ObserveNavigation("source visibility", list, selectedName);
                list.Focus(); ObserveNavigation("list focus", list, selectedName);
                var retainedEntry = list.Patterns.Selection.Pattern.Selection.Value.Single(); retainedEntry.FrameworkAutomationElement.SetFocus();
                Wait(() => retainedEntry.Properties.HasKeyboardFocus.Value, "UIA did not focus the exact selected navigation occurrence before Down.");
                ObserveNavigation("exact row focus", list, selectedName); Keyboard.Type(VirtualKeyShort.DOWN);
                ObserveNavigation("blocked Down", list, selectedName);
                if (field.Text != fieldBefore + "Pending" || summary.Name != summaryBefore || Find(visual!, prefix + "Zoom").Patterns.RangeValue.Pattern.Value.Value != zoom)
                    throw new IOException("Source visibility or rejected selection lost pending context/zoom.");
                Capture("pending"); Find(visual!, prefix + "ResetProperties").AsButton().Invoke();
                Wait(() => field.Text == fieldBefore && Find(visual!, prefix + "Apply").IsEnabled == originalApplyEnabled, "Reset did not restore validated property values.");
                ObserveNavigation("reset", list, selectedName);
                Wait(() => list.Patterns.Selection.Pattern.Selection.Value.Any(item => item.Patterns.SelectionItem.Pattern.IsSelected.Value && item.Name == selectedName && !item.IsOffscreen), "Rejected selection lost the exact retained SelectionItem or left it outside its viewport.");
                checks.Add(theme + " " + family + ": pending values survive source visibility and rejected selection; explicit Reset restores them");
                if (family is "Class" or "ER")
                {
                    var longText = family == "Class" ? "OpenDocumentWithCompleteLongMethodDescription" : "completeLongAttributeDescriptionForAccessibleNavigation";
                    list.Focus(); Keyboard.Type(VirtualKeyShort.HOME); Keyboard.Type(VirtualKeyShort.DOWN); Keyboard.Type(VirtualKeyShort.DOWN);
                    var longEntry = list.Patterns.Selection.Pattern.Selection.Value.Single();
                    Wait(() => Find(visual!, prefix + "AttributeName").AsTextBox().Text.Contains(longText, StringComparison.Ordinal), "The exact long member/attribute occurrence did not reach the inspector.");
                    if (!longEntry.Name.Contains(longText, StringComparison.Ordinal)) throw new IOException("Long member accessible name was truncated.");
                    checks.Add(theme + " " + family + ": full long member name, exact selected occurrence and persistent parent context");
                    var itemBounds = longEntry.BoundingRectangle; Mouse.MoveTo(itemBounds.Left + itemBounds.Width / 2, itemBounds.Top + itemBounds.Height / 2);
                    Wait(() => automation.GetDesktop().FindAllDescendants(cf => cf.ByProcessId(app.ProcessId).And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.ToolTip)))
                        .Any(tip => tip.Name.Contains(longText, StringComparison.Ordinal) || tip.FindAllDescendants().Any(text => text.Name.Contains(longText, StringComparison.Ordinal))), "Full long navigation tooltip did not appear.");
                    Capture("long-navigation-tooltip", true);
                }
                var tabStart = family switch { "Class" or "ER" => prefix + "AttributeType", "Sequence" => "SequenceParticipantId", _ => prefix + "NodeId" };
                var tabNext = family switch { "Class" or "ER" => prefix + "AttributeName", "Sequence" => "SequenceActor", "Flowchart" => "MermaidNodeLabel", _ => prefix + "Label" };
                Find(visual!, tabStart).Focus(); Keyboard.Type(VirtualKeyShort.TAB);
                Wait(() => Find(visual!, tabNext).Properties.HasKeyboardFocus.Value, "Inspector Tab did not reach the next native property control.");
                checks.Add(theme + " " + family + ": property keyboard tab order remains native and predictable");
                foreach (var (width, height) in new[] { (880, 600), (1024, 768), (1280, 800), (1440, 900), (1920, 1080) })
                {
                    visual!.Patterns.Transform.Pattern.Move(0, 0); visual.Patterns.Transform.Pattern.Resize(width, height);
                    Wait(() => Math.Abs(visual.BoundingRectangle.Width - width) < 3 && visual.BoundingRectangle.Contains(Find(visual, prefix + "Apply").BoundingRectangle), "Suite minimum-size footer containment failed.");
                    panel.Patterns.ExpandCollapse.Pattern.Expand(); Capture("source-expanded-" + width + "x" + height);
                    panel.Patterns.ExpandCollapse.Pattern.Collapse(); Capture("source-collapsed-" + width + "x" + height);
                    if (summary.IsOffscreen || !visual.BoundingRectangle.Contains(summary.BoundingRectangle)) throw new IOException("Suite inspector heading escaped the window.");
                    var splitter = Find(visual, prefix + "NavigationSplitter"); splitter.Focus(); Keyboard.Type(VirtualKeyShort.RIGHT);
                    if (!splitter.Properties.IsKeyboardFocusable.Value) throw new IOException("Suite navigation splitter lacks a keyboard alternative.");
                }
                checks.Add(theme + " " + family + ": heading, footer, adjustable source and keyboard splitter remain contained at five sizes");
                if (family != "Flowchart")
                {
                    var inspector = Find(visual!, prefix + "InspectorViewport");
                    if (inspector.Patterns.Scroll.IsSupported && inspector.Patterns.Scroll.Pattern.VerticallyScrollable.Value) inspector.Patterns.Scroll.Pattern.SetScrollPercent(-1, 100);
                    if (summary.IsOffscreen || Find(visual!, prefix + "Update").IsOffscreen) throw new IOException("Suite selected context/actions disappeared while scrolling.");
                    list.Focus(); Keyboard.Type(VirtualKeyShort.HOME); Keyboard.Type(VirtualKeyShort.DOWN);
                    Wait(() => summary.Name != summaryBefore, "A new exact navigation occurrence did not change inspector context.");
                    if (inspector.Patterns.Scroll.IsSupported && inspector.Patterns.Scroll.Pattern.VerticalScrollPercent.Value > 1) throw new IOException("New selection inherited an unrelated inspector scroll position.");
                    Capture("new-selection-context");
                }
                var sourceId = family == "Flowchart" ? "MermaidDraftSource" : prefix + "Source";
                panel.Patterns.ExpandCollapse.Pattern.Expand();
                if (ReadEditorText(Find(visual!, sourceId)) != source || ReadEditorText(Find(main, "MarkdownEditor")) != source || File.ReadAllText(path) != source)
                    throw new IOException("Suite presentation changed private or owning source.");
                Find(visual!, prefix + "Cancel").AsButton().Invoke();
                Wait(() => automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByAutomationId(prefix + "VisualEditor"))) is null, "Suite Cancel did not close unchanged draft.");
                app.Kill(); app.Dispose(); app = null; visual = null;
            }
            File.WriteAllText(Path.Combine(output, "suite-ui.json"), JsonSerializer.Serialize(new { Status = "PASS", ActualHighContrast = highContrast, Checks = checks, Measurements = measurements }, new JsonSerializerOptions { WriteIndented = true })); return 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "suite-failure.txt"), error.ToString()); visual?.CaptureToFile(Path.Combine(output, "suite-failure.png"));
            if (visual is not null)
                File.WriteAllLines(Path.Combine(output, "suite-failure-controls.txt"), visual.FindAllDescendants().Select(item =>
                    item.AutomationId + " | " + item.Name + " | off-screen=" + item.IsOffscreen + " | selected="
                    + (item.Patterns.SelectionItem.IsSupported ? item.Patterns.SelectionItem.Pattern.IsSelected.Value.ToString() : "unavailable") + " | " + item.BoundingRectangle));
            return 1;
        }
        finally { app?.Kill(); app?.Dispose(); }
        void Capture(string name, bool keepPointer = false) { if (!keepPointer) Mouse.MoveTo(visual!.BoundingRectangle.Left + 20, visual.BoundingRectangle.Top + 16); _ = DwmFlush(); visual!.CaptureToFile(Path.Combine(captureRoot, (highContrast ? "HighContrast" : "Normal") + "-" + visual.Title.Replace('/', '-') + "-" + name + ".png")); }
        void ObserveNavigation(string stage, AutomationElement list, string expected)
        {
            var selected = list.Patterns.Selection.Pattern.Selection.Value;
            var focused = automation.FocusedElement();
            File.AppendAllText(Path.Combine(output, "suite-navigation.txt"), stage + " | expected=" + expected + " | selected=" + selected.Length + " | window-enabled=" + visual!.IsEnabled
                + " | focused=" + focused?.AutomationId + " / " + focused?.Name + " | list-hwnd=" + list.Properties.NativeWindowHandle.ValueOrDefault + " | list=" + list.BoundingRectangle + Environment.NewLine
                + string.Join(Environment.NewLine, selected.Select(item => item.Name + " | selected=" + item.Patterns.SelectionItem.Pattern.IsSelected.Value + " | off-screen=" + item.IsOffscreen + " | " + item.BoundingRectangle)) + Environment.NewLine);
        }
    }).GetAwaiter().GetResult();

    private static void CaptureSuiteBaseline(Window visual, Window main, AutomationElement list, string family, string prefix, string source, Action<string> capture)
    {
        // Capture the immutable old UI as it is. New layout/accessibility assertions run
        // separately against the candidate; missing old features are never counted as passes.
        list.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem)).First().Patterns.SelectionItem.Pattern.Select();
        if (family is "Class" or "ER")
        {
            var longName = family == "Class" ? "OpenDocumentWithCompleteLongMethodDescription" : "completeLongAttributeDescriptionForAccessibleNavigation";
            list.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem)).First(item => item.Name.Contains(longName, StringComparison.Ordinal)).Patterns.SelectionItem.Pattern.Select();
        }
        capture("selected-long-name");
        var panel = Find(visual, prefix + "SourcePanel"); panel.Patterns.ExpandCollapse.Pattern.Expand();
        var lower = family switch { "Class" => "ClassParameters", "ER" => "ErAttributeComment", "Flowchart" => "MermaidNodeLabel", _ => prefix + "Label" };
        Wait(() => visual.FindFirstDescendant(cf => cf.ByAutomationId(lower)) is not null, "Baseline property context did not appear.");
        Find(visual, lower).Focus(); capture("inspector-lower");
        var field = Find(visual, family is "Class" or "ER" ? prefix + "AttributeName" : family == "Flowchart" ? "MermaidNodeLabel" : prefix + "Label").AsTextBox();
        field.Text += "Pending"; capture("pending"); Find(visual, prefix + "ResetProperties").AsButton().Invoke();
        foreach (var (width, height) in new[] { (880, 600), (1024, 768), (1280, 800), (1440, 900), (1920, 1080) })
        {
            visual.Patterns.Transform.Pattern.Move(0, 0); visual.Patterns.Transform.Pattern.Resize(width, height);
            Wait(() => Math.Abs(visual.BoundingRectangle.Width - width) < 3 && visual.BoundingRectangle.Contains(Find(visual, prefix + "Apply").BoundingRectangle), "Baseline window containment did not settle.");
            panel.Patterns.ExpandCollapse.Pattern.Expand(); capture("source-expanded-" + width + "x" + height);
            panel.Patterns.ExpandCollapse.Pattern.Collapse(); capture("source-collapsed-" + width + "x" + height);
        }
        Find(visual, prefix + "Fit").AsButton().Invoke(); capture("fit");
        Find(visual, prefix + "ActualSize").AsButton().Invoke(); capture("actual-size");
        Find(visual, prefix + "Zoom").Patterns.RangeValue.Pattern.SetValue(175);
        var viewport = Find(visual, prefix + "Viewport");
        if (viewport.Patterns.Scroll.IsSupported)
        {
            var scroll = viewport.Patterns.Scroll.Pattern;
            scroll.SetScrollPercent(scroll.HorizontallyScrollable.Value ? 40 : -1, scroll.VerticallyScrollable.Value ? 40 : -1);
        }
        capture("zoomed-scrolled");
        panel.Patterns.ExpandCollapse.Pattern.Expand();
        var sourceId = family == "Flowchart" ? "MermaidDraftSource" : prefix + "Source";
        var validateId = family == "Flowchart" ? "MermaidValidateSource" : prefix + "Validate";
        ReplaceSource("not a Mermaid header\n"); Validate();
        Wait(() => !Find(visual, prefix + "Apply").IsEnabled, "Baseline invalid source was not protected."); capture("validation-error");
        ReplaceSource(source); Validate();
        Wait(() => Find(visual, family == "Flowchart" ? "MermaidInsertProcess" : prefix + "Apply").IsEnabled, "Baseline original source did not validate again.");
        var insertId = family switch { "Flowchart" => "MermaidInsertProcess", "Sequence" => "SequenceAddParticipant", "State" => "StateAddState", "ER" => "ErAddEntity", _ => "ClassAddClass" };
        Find(visual, insertId).AsButton().Invoke(); if (family != "Flowchart") Find(visual, prefix + "Update").AsButton().Invoke();
        Wait(() => Find(visual, prefix + "Undo").IsEnabled && Find(visual, family == "Flowchart" ? "MermaidInsertProcess" : prefix + "Apply").IsEnabled, "Baseline insertion did not complete."); capture("new-element");
        Find(visual, prefix + "Undo").AsButton().Invoke();
        Wait(() => ReadEditorText(Find(visual, sourceId)) == source, "Baseline Undo did not restore exact source.");
        if (ReadEditorText(Find(main, "MarkdownEditor")) != source) throw new IOException("Baseline capture changed the owning source.");
        void ReplaceSource(string value)
        {
            Find(visual, sourceId).Patterns.Value.Pattern.SetValue(value);
            Wait(() => ReadEditorText(Find(visual, sourceId)) == value, "Baseline source input did not arrive exactly.");
        }
        void Validate()
        {
            var before = Find(visual, prefix + "Status").Name; Find(visual, validateId).AsButton().Invoke();
            Wait(() => Find(visual, validateId).IsEnabled && Find(visual, prefix + "Status").Name != before, "Baseline source validation did not complete.");
        }
    }
}
