using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    private static void RunHostPickerCancel(int processId, Window main, AutomationElement editor, UIA3Automation automation, string sourcePath, string source, string output)
    {
        AssertHostForeground(processId, main); editor.Focus();
        var selected = editor.Patterns.Text.Pattern.DocumentRange.FindText("Café", false, false) ?? throw new IOException("Synthetic selection missing.");
        AssertHostForeground(processId, main); selected.Select();
        AssertHostForeground(processId, main); Find(main, "MermaidCreation").AsButton().Invoke();
        Window? picker = null;
        Wait(() => { AssertHostForeground(processId); return (picker = HostWindowById(automation, processId, "MermaidDiagramPicker")) is not null; }, "Integrated Mermaid picker did not open.");
        AssertHostForeground(processId, picker);
        if (Find(picker!, "MermaidPickerFamilies").AsListBox().Items.Length != 24) throw new IOException("Integrated picker lost its 24 families.");
        Find(picker!, "MermaidPickerCancel").AsButton().Invoke();
        Wait(() => { AssertHostForeground(processId); return HostWindowById(automation, processId, "MermaidDiagramPicker") is null; }, "Picker Cancel did not close.");
        AssertHostForeground(processId, main);
        var ranges = editor.Patterns.Text.Pattern.GetSelection();
        if (ranges.Length != 1 || ranges[0].GetText(-1) != "Café" || ranges[0].CompareEndpoints(FlaUI.Core.Definitions.TextPatternRangeEndpoint.Start, selected, FlaUI.Core.Definitions.TextPatternRangeEndpoint.Start) != 0
            || ReadEditorText(editor) != source || File.ReadAllText(sourcePath) != source)
            throw new IOException("Picker Cancel changed captured selection or synthetic source.");
        WriteNew(Path.Combine(output, "picker-cancel.json"), new { atUtc = DateTimeOffset.UtcNow, status = "PASS", families = 24, selectedText = "Café", sourceSha256 = Hash(sourcePath), scope = "Actual picker invocation/Cancel and captured source selection; not all-family local acceptance" });
    }

    private static void RunHostOwnerConflict(int processId, Window main, AutomationElement editor, UIA3Automation automation,
        string sandbox, string output, string sourcePath, string source)
    {
        Window? visual = null; var sequence = 0;
        var observations = Path.Combine(sandbox, "ui", "owner-observations");
        OpenDraft();
        Control("MermaidNodeLabel").AsTextBox().Text = "Cancelled private edit";
        Control("MermaidUpdateNode").AsButton().Invoke();
        Observe(() => Control("MermaidApply").IsEnabled, "Private Cancel fixture did not validate.");
        Control("MermaidCancel").AsButton().Invoke(); Closed();
        if (ReadEditorText(editor) != source || File.ReadAllText(sourcePath) != source) throw new IOException("Private Cancel changed owner/saved source.");
        OpenDraft();
        Control("MermaidNodeLabel").AsTextBox().Text = "Private conflict";
        Control("MermaidUpdateNode").AsButton().Invoke();
        Observe(() => Control("MermaidApply").IsEnabled, "Private conflict fixture did not validate.");
        var privateSource = ReadEditorText(Control("MermaidDraftSource"));
        var external = source + "\nExternal synthetic owner\n";
        var before = Snapshot();
        var expectedStart = Process.GetProcessById(processId).StartTime.ToUniversalTime().Ticks;
        var explicitEditors = main.FindAllDescendants(cf => cf.ByAutomationId("MarkdownEditor"));
        if (before.ProcessId != processId || before.ProcessStartTicks != expectedStart
            || before.MainHwnd != main.Properties.NativeWindowHandle.Value.ToInt64() || before.VisualHwnd != visual!.Properties.NativeWindowHandle.Value.ToInt64()
            || before.SourceSha256 != Digest(source) || before.PrivateSha256 != Digest(privateSource) || before.SavedSha256 != Hash(sourcePath)
            || explicitEditors.Length != 1 || editor.Properties.ProcessId.Value != processId || editor.Patterns.Value.Pattern.IsReadOnly.Value
            || !explicitEditors[0].FrameworkAutomationElement.RuntimeId.Value.SequenceEqual(editor.FrameworkAutomationElement.RuntimeId.Value))
            throw new HostFindInputBlockedException("Explicit synthetic owner/editor/native-session/source binding differs; no mutation sent.");
        var current = Snapshot(); NativeHostOwnerPolicy.BeforeMutation(before, current);
        if (ReadEditorText(editor) != source || ReadEditorText(Control("MermaidDraftSource")) != privateSource || File.ReadAllText(sourcePath) != source)
            throw new HostFindInputBlockedException("Synthetic source changed at the mutation boundary.");
        AssertHostForeground(processId, visual);
        // This one explicit-target UIA mutation is authorised only by the complete
        // native + Win32 + UIA policy above. It never changes OS foreground/focus.
        editor.Patterns.Value.Pattern.SetValue(external);
        AssertHostForeground(processId, visual);
        var after = Snapshot(); NativeHostOwnerPolicy.AfterMutation(before, after, Digest(external));
        Control("MermaidApply").AsButton().Invoke();
        Observe(() => Control("MermaidStatus").Name.Contains("changed", StringComparison.OrdinalIgnoreCase), "Stale-owner Apply was not rejected.");
        if (ReadEditorText(Control("MermaidDraftSource")) != privateSource || ReadEditorText(editor) != external || File.ReadAllText(sourcePath) != source)
            throw new IOException("Stale Apply rejection changed exact private/external/saved source.");
        var rejected = Snapshot(); NativeHostOwnerPolicy.AfterMutation(before, rejected, Digest(external));
        AssertHostForeground(processId, visual); visual.CaptureToFile(Path.Combine(output, "stale-owner.png"));
        WriteNew(Path.Combine(output, "stale-owner-proof.json"), new { atUtc = DateTimeOffset.UtcNow, status = "PASS", mutationCount = 1,
            scope = "ACTUAL_PORTABLE_NONMODAL_STALE_OWNER", before, current, after, rejected,
            statusText = Control("MermaidStatus").Name, privateSource, external, savedSource = source });
        Control("MermaidCancel").AsButton().Invoke(); Closed();
        AssertHostForeground(processId, main); editor.Patterns.Value.Pattern.SetValue(source);
        AssertHostForeground(processId, main); editor.Focus();
        AssertHostForeground(processId, main, editor); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
        Observe(() => ReadObservedText(sourcePath) == source && ReadEditorText(editor) == source, "Owner did not return to the exact saved baseline.");

        AutomationElement Control(string id) { AssertHostForeground(processId, visual); return Find(visual!, id); }
        void Observe(Func<bool> condition, string failure) => Wait(() => { AssertHostForeground(processId); return condition(); }, failure);
        void OpenDraft()
        {
            AssertHostForeground(processId, main);
            var action = main.FindAllDescendants().Single(element => (element.Properties.AutomationId.ValueOrDefault ?? "").StartsWith("DiagramEditVisually", StringComparison.Ordinal));
            AssertHostForeground(processId, main); action.AsButton().Invoke();
            Observe(() => (visual = HostWindowById(automation, processId, "MermaidVisualEditor")) is not null, "Owned visual editor did not open.");
            Observe(() => Control("MermaidStatus").Name.StartsWith("Ready", StringComparison.Ordinal), "Owned visual editor did not become ready.");
            Control("MermaidElements").AsListBox().Select(0);
        }
        void Closed() => Observe(() => HostWindowById(automation, processId, "MermaidVisualEditor") is null, "Private Cancel did not close the owned visual editor.");
        HostOwnerSnapshot Snapshot()
        {
            AssertHostForeground(processId, visual);
            var request = Path.Combine(observations, "request-" + ++sequence + ".json");
            WriteNew(request, new { sequence });
            var response = Path.Combine(observations, "response-" + sequence + ".json");
            HostOwnerSnapshot? value = null;
            Observe(() =>
            {
                AssertHostForeground(processId, visual);
                if (!File.Exists(response)) return false;
                var text = ReadObservedText(response); if (text.Length == 0) return false;
                using var document = JsonDocument.Parse(text);
                if (document.RootElement.TryGetProperty("status", out _)) throw new HostFindInputBlockedException("Native owner observation rejected: " + text);
                value = JsonSerializer.Deserialize<HostOwnerSnapshot>(text); return true;
            }, "Native owner observation did not complete within the existing deadline.");
            if (value?.Sequence != sequence) throw new HostFindInputBlockedException("Native observation sequence differs.");
            return value;
        }
        static string Digest(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }
}
