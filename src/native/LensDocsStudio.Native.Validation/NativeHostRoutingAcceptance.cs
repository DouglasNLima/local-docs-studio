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
    // Each invocation owns one fresh portable process and one independent scenario.
    private static int RunHostRoutingAcceptance(string executable, string sandbox, string revision, string tree, string variant)
    {
        var output = Path.Combine(sandbox, "ui");
        var workspace = Path.Combine(sandbox, "workspace");
        var path = Path.Combine(workspace, variant == "InvalidMermaid" ? "invalid.mmd" : "acceptance-lf.md");
        var source = File.ReadAllText(path); var originalHash = Hash(path);
        WriteNew(Path.Combine(output, "before.json"), new { atUtc = DateTimeOffset.UtcNow, revision, tree, variant, source, originalHash, fullShell = "NOT RUN" });
        using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromSeconds(3), ConnectionTimeout = TimeSpan.FromSeconds(3) };
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = workspace };
        info.Environment["LENS_NATIVE_PREVIEW_DIAGNOSTICS"] = "1";
        if (variant.EndsWith("Observed", StringComparison.Ordinal))
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DOTNET_STARTUP_HOOKS"))) throw new IOException("Inherited startup hooks are forbidden.");
            info.Environment["DOTNET_STARTUP_HOOKS"] = typeof(StartupHook).Assembly.Location;
            info.Environment["LENS_FIND_OBSERVATION_ROOT"] = sandbox;
            info.Environment["LENS_FIND_OBSERVATION_VARIANT"] = variant;
        }
        foreach (var arg in new[] { "--state-directory", Path.Combine(sandbox, "state"), workspace, path }) info.ArgumentList.Add(arg);
        using var app = Application.Launch(info);
        Window? main = null; AutomationElement? editor = null; var stage = "owner-readiness"; var actionSent = false;
        try
        {
            main = app.GetMainWindow(automation, TimeSpan.FromSeconds(25)) ?? throw new IOException("Owner did not open within 25 seconds.");
            CaptureHostRuntime(Process.GetProcessById(app.ProcessId), executable, output, revision, tree);
            stage = "operator-handoff"; AwaitHostOperatorForeground(app.ProcessId, main, executable, output, "initial");
            Guard(); editor = Find(main, "MarkdownEditor");
            stage = "source-readiness";
            ObserveUntil(() => ReadEditorText(editor) == source, "Exact synthetic source did not load.");
            if (variant.EndsWith("Observed", StringComparison.Ordinal))
                ObserveUntil(() => File.Exists(Path.Combine(output, "native-observations.jsonl")) && ReadObservedText(Path.Combine(output, "native-observations.jsonl")).Contains("probe-attached", StringComparison.Ordinal), "Read-only observer did not attach.");
            stage = "preview-readiness";
            // The first preview can still be pending after the source and observer are ready.
            // Observe its actual provider before reading selection; never repeat input.
            ObserveInitialProviders();
            Snapshot("ready");

            if (variant == "InvalidMermaid")
            {
                stage = "invalid-source";
                if (source != NativeHostMermaidFixtureChecks.Invalid) throw new IOException("Invalid source differs from the independently validated fixture.");
                ObserveUntil(() => main.FindFirstDescendant(cf => cf.ByAutomationId("DiagramError0"))?.Name == NativeHostMermaidFixtureChecks.Error, "Exact actionable native error did not appear.");
                if (main.FindFirstDescendant(cf => cf.ByAutomationId("DiagramPreview0")) is not null || main.FindFirstDescendant(cf => cf.ByAutomationId("DiagramActionPNG")) is not null)
                    throw new IOException("Rejected source exposed accepted diagram/image output.");
                Guard(); editor.Focus(); ObserveUntil(() => editor.Properties.HasKeyboardFocus.Value && !editor.Patterns.Value.Pattern.IsReadOnly.Value, "Invalid source is not directly editable.");
                Preserve(); Snapshot("invalid-source");
                stage = "valid-companion";
                Guard(editor); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_P);
                Window? picker = null;
                ObserveUntil(() => (picker = HostWindowById(automation, app.ProcessId, "CommandSearchWindow")) is not null, "Quick Open did not appear.", ownerOnly: true);
                AssertHostForeground(app.ProcessId, picker);
                var query = Find(picker!, "CommandSearchQuery");
                AssertHostForeground(app.ProcessId, picker, query); query.Patterns.Value.Pattern.SetValue("standalone.mmd");
                ObserveUntil(() => Find(picker!, "CommandSearchResults").AsListBox().Items.Count(item => item.Name == "standalone.mmd") == 1, "Exact valid companion was not found.", ownerOnly: true);
                AssertHostForeground(app.ProcessId, picker, query); actionSent = true; Keyboard.Type(VirtualKeyShort.RETURN);
                ObserveUntil(() => HostWindowById(automation, app.ProcessId, "CommandSearchWindow") is null, "Quick Open did not close.", ownerOnly: true);
                var valid = File.ReadAllText(Path.Combine(workspace, "standalone.mmd"));
                ObserveUntil(() => ReadEditorText(editor) == valid && main.FindFirstDescendant(cf => cf.ByAutomationId("DiagramPreview0")) is not null && main.FindFirstDescendant(cf => cf.ByAutomationId("DiagramError0")) is null, "Valid companion did not render normally.");
                if (Hash(path) != originalHash) throw new IOException("Invalid source bytes changed.");
                Snapshot("valid-companion");
            }
            else
            {
                stage = "open-find"; Guard(); editor.Focus();
                ObserveUntil(() => editor.Properties.HasKeyboardFocus.Value, "Editor did not acquire focus.");
                Guard(editor); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
                var query = Find(main, "FindText");
                ObserveUntil(() => query.Properties.HasKeyboardFocus.Value && Find(main, "FindContext").Name == "Find in editor", "Editor Find context/focus differs.");
                Guard(query); query.Patterns.Value.Pattern.SetValue("Café");

                if (variant == "FindEditorEnterObserved")
                {
                    stage = "editor-enter"; Guard(); editor.Focus();
                    ObserveUntil(() => editor.Properties.HasKeyboardFocus.Value, "Editor did not regain focus with Find visible.");
                    Guard(editor); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.HOME);
                    Snapshot("before-enter");
                    Guard(editor); actionSent = true; Keyboard.Type(VirtualKeyShort.RETURN);
                    ObserveUntil(() => ReadEditorText(editor) == "\n" + source, "Ordinary editor Enter did not insert exactly one newline.");
                    var native = LastNative();
                    if (native.GetProperty("caretOffset").GetInt32() != 1 || native.GetProperty("selectionLength").GetInt32() != 0 || query.Patterns.Value.Pattern.Value.Value != "Café" || Hash(path) != originalHash)
                        throw new IOException("Editor Enter changed search selection, caret, query or saved source.");
                    Snapshot("after-enter");
                    stage = "editor-save-undo"; Guard(editor); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
                    ObserveUntil(() => ReadObservedText(path) == "\n" + source, "Save did not persist the exact newline.");
                    Guard(editor); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_Z);
                    ObserveUntil(() => ReadEditorText(editor) == source, "One Undo did not restore the exact original buffer.");
                    Guard(editor); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
                    ObserveUntil(() => Hash(path) == originalHash, "Save after Undo did not restore original bytes.");
                    Guard(); Find(main, "CloseFind").AsButton().Invoke();
                    ObserveUntil(() => editor.Properties.HasKeyboardFocus.Value, "Close Find did not restore editor focus.");
                    Preserve(); Snapshot("after-undo-save-close");
                }
                else
                {
                    stage = "preview-context"; Guard(query); Keyboard.Type(VirtualKeyShort.ESCAPE);
                    ObserveUntil(() => editor.Properties.HasKeyboardFocus.Value, "Escape from editor Find did not restore editor focus.");
                    var preview = Find(main, "DocumentPreview");
                    var range = preview.Patterns.Text.Pattern.DocumentRange.FindText("Only synthetic", false, false) ?? throw new IOException("Preview literal is missing.");
                    var bounds = range.GetBoundingRectangles().First(rect => rect.Width > 0 && rect.Height > 0);
                    Guard(); Mouse.Click(new System.Drawing.Point((int)(bounds.Left + bounds.Width / 2), (int)(bounds.Top + bounds.Height / 2)));
                    Guard(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
                    ObserveUntil(() => query.Properties.HasKeyboardFocus.Value && Find(main, "FindContext").Name == "Find in preview", "Ctrl+F did not identify the focused preview.");
                    Guard(query); query.Patterns.Value.Pattern.SetValue("Café"); Snapshot("before-enter");
                    Guard(query); actionSent = true; Keyboard.Type(VirtualKeyShort.RETURN);
                    ObserveUntil(() =>
                    {
                        var selected = preview.Patterns.Text.Pattern.GetSelection();
                        var first = preview.Patterns.Text.Pattern.DocumentRange.FindText("Café", false, false);
                        return selected.Length == 1 && selected[0].GetText(-1) == "Café" && first is not null
                            && selected[0].CompareEndpoints(FlaUI.Core.Definitions.TextPatternRangeEndpoint.Start, first, FlaUI.Core.Definitions.TextPatternRangeEndpoint.Start) == 0;
                    }, "Preview Enter did not select the first exact literal once.");
                    // Navigation is asynchronous; request only a read-only UIA observation.
                    Snapshot("after-enter");
                    Guard(query); Keyboard.Type(VirtualKeyShort.ESCAPE);
                    ObserveUntil(() => LastNative().GetProperty("focusInPreview").GetBoolean() && !query.Properties.HasKeyboardFocus.Value && !editor.Properties.HasKeyboardFocus.Value, "Escape did not return focus to preview.");
                    if (LastNative().GetProperty("previewSelection").GetString() != "Café" || query.Patterns.Value.Pattern.Value.Value != "Café")
                        throw new IOException("Native preview selection or exact query changed.");
                    Preserve(); Snapshot("after-escape");
                    Guard(); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
                    ObserveUntil(() => query.Properties.HasKeyboardFocus.Value && Find(main, "FindContext").Name == "Find in preview", "Preview context did not survive Escape.");
                    Guard(query); Find(main, "CloseFind").AsButton().Invoke();
                    ObserveUntil(() => LastNative().GetProperty("focusInPreview").GetBoolean() && !query.Properties.HasKeyboardFocus.Value, "Close Find did not return preview focus.");
                    Guard(); editor.Focus(); ObserveUntil(() => editor.Properties.HasKeyboardFocus.Value, "Editor focus did not return.");
                    Guard(editor); Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_F);
                    ObserveUntil(() => query.Properties.HasKeyboardFocus.Value && Find(main, "FindContext").Name == "Find in editor", "Ctrl+F from editor did not restore editor context.");
                    Guard(query); Find(main, "CloseFind").AsButton().Invoke();
                    ObserveUntil(() => editor.Properties.HasKeyboardFocus.Value, "Close Find did not restore editor focus.");
                    Preserve(); Snapshot("after-context-round-trip");
                }
                var returnEvents = ReadObservedText(Path.Combine(output, "native-observations.jsonl")).Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => JsonSerializer.Deserialize<JsonElement>(line))
                    .Count(row => row.GetProperty("observation").GetString() == "window-preview-key" && row.GetProperty("key").GetString() == "Return");
                if (returnEvents != 1) throw new IOException("Expected one real Return in this fresh session.");
            }
            stage = "normal-close"; Guard(); main.Close(); Wait(() => app.HasExited, "Owned synthetic app did not close normally.");
            WriteNew(Path.Combine(output, "completion.json"), new { atUtc = DateTimeOffset.UtcNow, status = "PASS", scope = "FOCUSED_PORTABLE_GUI_ONLY", revision, tree, variant, actionSent, originalHash, fullShell = "NOT RUN" });
            return 0;
        }
        catch (Exception error)
        {
            try { if (main is not null) Snapshot("failure", false); } catch (Exception observationError) { WriteNew(Path.Combine(output, "observation-error.json"), new { error = observationError.ToString() }); }
            WriteNew(Path.Combine(output, "completion.json"), new { atUtc = DateTimeOffset.UtcNow, status = error is HostFindInputBlockedException ? "BLOCKED" : "FAIL", revision, tree, variant, stage, actionSent, error = error.ToString(), processId = app.ProcessId, fullShell = "NOT RUN" });
            throw;
        }

        void Guard(AutomationElement? focus = null) => AssertHostForeground(app.ProcessId, main, focus);
        void ObserveUntil(Func<bool> condition, string failure, bool ownerOnly = false) => Wait(() => { AssertHostForeground(app.ProcessId, ownerOnly ? null : main); return condition(); }, failure);
        void ObserveInitialProviders()
        {
            var samples = new List<object>(); var clock = Stopwatch.StartNew(); var ready = false;
            var hwnd = main!.Properties.NativeWindowHandle.Value;
            try
            {
                ObserveUntil(() =>
                {
                    var editorReady = false; var previewReady = false; var previewFound = false;
                    string? editorError = null; string? previewError = null;
                    try
                    {
                        editorReady = HasTextPattern(editor!, out editorError);
                        if (editorReady)
                        {
                            var preview = main!.FindFirstDescendant(cf => cf.ByAutomationId("DocumentPreview"));
                            previewFound = preview is not null;
                            if (preview is not null) previewReady = HasTextPattern(preview, out previewError);
                        }
                        return ready = editorReady && previewReady;
                    }
                    finally
                    {
                        if (samples.Count < 256) samples.Add(new { atUtc = DateTimeOffset.UtcNow, elapsedMilliseconds = clock.Elapsed.TotalMilliseconds,
                            editorReady, previewFound, previewReady, editorError, previewError, foreground = GetForegroundWindow().ToInt64() });
                    }
                }, "Initial editor/preview TextPattern providers did not become available.");
            }
            finally
            {
                object? previewState = null;
                if (GetForegroundWindow() == hwnd && HostNormalConsole())
                {
                    try
                    {
                        var status = main.FindFirstDescendant(cf => cf.ByAutomationId("PreviewStatus"));
                        previewState = new { status = status?.Name, diagnostic = status?.Properties.HelpText.ValueOrDefault };
                    }
                    catch (Exception error) { previewState = new { observationError = error.ToString() }; }
                }
                WriteNew(Path.Combine(output, "initial-provider-observations.json"), new { revision, tree, processId = app.ProcessId,
                    hwnd = hwnd.ToInt64(), ready, actionSent = false, observationBudgetSeconds = 20, samples, previewState });
            }
        }
        static bool HasTextPattern(AutomationElement element, out string? error)
        {
            error = null;
            try { _ = element.Patterns.Text.Pattern; return true; }
            catch (Exception exception)
            {
                error = exception.ToString();
                // FlaUI IsSupported uses this same getter and catches only this exception.
                // Preserve its readiness result while retaining the underlying failure.
                if (exception is FlaUI.Core.Exceptions.PatternNotSupportedException) return false;
                throw;
            }
        }
        void Preserve() { if (ReadEditorText(editor!) != source || Hash(path) != originalHash) throw new IOException("Original source was not preserved."); }
        JsonElement LastNative() => ReadObservedText(Path.Combine(output, "native-observations.jsonl")).Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonSerializer.Deserialize<JsonElement>(line)).Last(row => row.TryGetProperty("selectionStart", out _));
        void Snapshot(string name, bool capture = true)
        {
            var hwnd = main!.Properties.NativeWindowHandle.Value;
            if (capture) { Guard(); main.CaptureToFile(Path.Combine(output, name + ".png")); }
            var preview = main.FindFirstDescendant(cf => cf.ByAutomationId("DocumentPreview"));
            WriteNew(Path.Combine(output, name + ".json"), new { atUtc = DateTimeOffset.UtcNow, revision, tree, variant, processId = app.ProcessId, processStartUtc = Process.GetProcessById(app.ProcessId).StartTime.ToUniversalTime(), hwnd = hwnd.ToInt64(), foreground = GetForegroundWindow().ToInt64(), dpi = HostWindowDpi(hwnd), sourceHash = Hash(path),
                ownerText = editor is null ? null : ReadEditorText(editor), editorSelection = editor?.Patterns.Text.Pattern.GetSelection().Select(r => r.GetText(-1)).ToArray(),
                previewSelection = preview?.Patterns.Text.Pattern.GetSelection().Select(r => r.GetText(-1)).ToArray(),
                previewStatus = main.FindFirstDescendant(cf => cf.ByAutomationId("PreviewStatus"))?.Name,
                previewDiagnostics = main.FindFirstDescendant(cf => cf.ByAutomationId("PreviewStatus"))?.Properties.HelpText.ValueOrDefault,
                controls = main.FindAllDescendants().Where(e => e.Properties.HasKeyboardFocus.ValueOrDefault || e.Properties.AutomationId.ValueOrDefault is "FindText" or "FindContext" or "DiagramError0" or "DiagramPreview0").Select(e => new { id = e.Properties.AutomationId.ValueOrDefault, name = e.Name, focused = e.Properties.HasKeyboardFocus.ValueOrDefault }).ToArray() });
        }
    }
}

