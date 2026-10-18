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
    public static int RunMermaidSuite(string executable, string output, bool highContrast) => Task.Run(() =>
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        using var clipboard = NativeUiClipboard.Start(); using var contrast = highContrast ? new NativeTestHighContrast() : null;
        using var display = new NativeTestDisplay(output); using var automation = new UIA3Automation();
        var captureRoot = output; var checks = new List<string>(); Application? app = null; Window? visual = null;
        try
        {
            if (highContrast && !System.Windows.SystemParameters.HighContrast) throw new IOException("Actual Windows High Contrast was not enabled.");
            foreach (var theme in highContrast ? new[] { "System" } : new[] { "Light", "Dark" })
            foreach (var family in new[] { "Flowchart", "Sequence", "State", "ER", "Class" })
            {
                var prefix = family == "Flowchart" ? "Mermaid" : family == "ER" ? "Er" : family;
                var root = Path.Combine(output, theme + "-" + family); Directory.CreateDirectory(root);
                captureRoot = root; var state = Path.Combine(root, "state"); Directory.CreateDirectory(state);
                File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new UserPreferences(Theme: theme, AutomaticUpdateChecks: false)));
                var source = NativeSuitePerformanceChecks.Source(family, false)
                    .Replace("Node 0 with context", "Node 0 with a complete long documentation context for accessible navigation", StringComparison.Ordinal)
                    .Replace("Participant 0", "Participant 0 with a complete long documentation context for accessible navigation", StringComparison.Ordinal)
                    .Replace("State 0 with context", "State 0 with a complete long documentation context for accessible navigation", StringComparison.Ordinal)
                    .Replace("description", "completeLongAttributeDescriptionForAccessibleNavigation", StringComparison.Ordinal)
                    .Replace("Open(path, options)", "OpenDocumentWithCompleteLongMethodDescription(path, options, cancellationToken)", StringComparison.Ordinal);
                var path = Path.Combine(root, family + ".mmd"); File.WriteAllText(path, source);
                var start = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false };
                foreach (var argument in new[] { "--state-directory", state, root, path }) start.ArgumentList.Add(argument);
                app = Application.Launch(start); var main = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("Suite fixture did not open.");
                Wait(() => main.FindFirstDescendant(cf => cf.ByAutomationId("DiagramEditVisually0")) is not null, "Suite visual action did not appear.");
                Find(main, "DiagramEditVisually0").AsButton().Invoke();
                Wait(() => (visual = automation.GetDesktop().FindFirstDescendant(cf => cf.ByProcessId(app.ProcessId).And(cf.ByAutomationId(prefix + "VisualEditor")))?.AsWindow()) is not null, "Suite editor did not open.");
                Wait(() => Find(visual!, family == "Flowchart" ? "MermaidInsertProcess" : prefix + "Apply").IsEnabled, "Suite canonical mapping did not finish.");
                var originalApplyEnabled = Find(visual!, prefix + "Apply").IsEnabled;
                Capture("initial");
                var list = Find(visual!, family == "Sequence" ? "SequenceParticipants" : prefix + "Elements");
                list.Focus(); Keyboard.Type(VirtualKeyShort.HOME);
                var summary = Find(visual!, prefix + "SelectionSummary"); Wait(() => !summary.Name.StartsWith("Select ", StringComparison.Ordinal), "Suite keyboard selection did not reach inspector.");
                var selectedName = list.FindAllDescendants().First(item => item.Patterns.SelectionItem.IsSupported && item.Patterns.SelectionItem.Pattern.IsSelected.Value).Name;
                if (string.IsNullOrWhiteSpace(selectedName)) throw new IOException("Suite selected entry has no accessible name.");
                if (family is "Flowchart" or "State" or "Sequence" && !selectedName.Contains("complete long documentation context", StringComparison.Ordinal))
                    throw new IOException("Suite full navigation name was truncated.");
                checks.Add(theme + " " + family + ": keyboard SelectionItem retains the full semantic description");
                var fieldId = family switch { "Flowchart" => "MermaidNodeLabel", "Sequence" => "SequenceParticipantId", _ => prefix + "NodeId" };
                var field = Find(visual!, fieldId).AsTextBox(); var fieldBefore = field.Text; var summaryBefore = summary.Name;
                field.Text = fieldBefore + "Pending"; Wait(() => !Find(visual!, prefix + "Apply").IsEnabled, "Pending properties did not block Apply.");
                var panel = Find(visual!, prefix + "SourcePanel"); var zoom = Find(visual!, prefix + "Zoom").Patterns.RangeValue.Pattern.Value.Value;
                panel.Patterns.ExpandCollapse.Pattern.Expand(); panel.Patterns.ExpandCollapse.Pattern.Collapse();
                list.Focus(); Keyboard.Type(VirtualKeyShort.DOWN);
                if (field.Text != fieldBefore + "Pending" || summary.Name != summaryBefore || Find(visual!, prefix + "Zoom").Patterns.RangeValue.Pattern.Value.Value != zoom)
                    throw new IOException("Source visibility or rejected selection lost pending context/zoom.");
                Capture("pending"); Find(visual!, prefix + "ResetProperties").AsButton().Invoke();
                Wait(() => field.Text == fieldBefore && Find(visual!, prefix + "Apply").IsEnabled == originalApplyEnabled, "Reset did not restore validated property values.");
                checks.Add(theme + " " + family + ": pending values survive source visibility and rejected selection; explicit Reset restores them");
                if (family is "Class" or "ER")
                {
                    var longText = family == "Class" ? "OpenDocumentWithCompleteLongMethodDescription" : "completeLongAttributeDescriptionForAccessibleNavigation";
                    list.Focus(); Keyboard.Type(VirtualKeyShort.HOME); Keyboard.Type(VirtualKeyShort.DOWN); Keyboard.Type(VirtualKeyShort.DOWN);
                    var longEntry = list.FindAllDescendants(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.ListItem)).Single(item => item.Patterns.SelectionItem.IsSupported && item.Patterns.SelectionItem.Pattern.IsSelected.Value);
                    Wait(() => Find(visual!, prefix + "AttributeName").AsTextBox().Text.Contains(longText, StringComparison.Ordinal), "The exact long member/attribute occurrence did not reach the inspector.");
                    if (!longEntry.Name.Contains(longText, StringComparison.Ordinal)) throw new IOException("Long member accessible name was truncated.");
                    checks.Add(theme + " " + family + ": full long member name, exact selected occurrence and persistent parent context");
                    var itemBounds = longEntry.BoundingRectangle; Mouse.MoveTo(itemBounds.Left + itemBounds.Width / 2, itemBounds.Top + itemBounds.Height / 2);
                    Wait(() => automation.GetDesktop().FindAllDescendants(cf => cf.ByProcessId(app.ProcessId).And(cf.ByControlType(FlaUI.Core.Definitions.ControlType.ToolTip)))
                        .Any(tip => tip.Name.Contains(longText, StringComparison.Ordinal) || tip.FindAllDescendants().Any(text => text.Name.Contains(longText, StringComparison.Ordinal))), "Full long navigation tooltip did not appear.");
                    Capture("long-navigation-tooltip", true);
                }
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
            File.WriteAllText(Path.Combine(output, "suite-ui.json"), JsonSerializer.Serialize(new { Status = "PASS", ActualHighContrast = highContrast, Checks = checks }, new JsonSerializerOptions { WriteIndented = true })); return 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "suite-failure.txt"), error.ToString()); visual?.CaptureToFile(Path.Combine(output, "suite-failure.png")); return 1;
        }
        finally { app?.Kill(); app?.Dispose(); }
        void Capture(string name, bool keepPointer = false) { if (!keepPointer) Mouse.MoveTo(visual!.BoundingRectangle.Left + 20, visual.BoundingRectangle.Top + 16); _ = DwmFlush(); visual!.CaptureToFile(Path.Combine(captureRoot, (highContrast ? "HighContrast" : "Normal") + "-" + visual.Title.Replace('/', '-') + "-" + name + ".png")); }
    }).GetAwaiter().GetResult();
}
