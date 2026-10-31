using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.Diagnostics;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Native.Validation;

// Added only to disposable diagnostic checkouts. Release builds have no barrier or native probe.
internal static class ReadinessControls
{
    internal static int Run(string executable, string output, string mode)
    {
        output = Path.GetFullPath(output); Directory.CreateDirectory(output);
        if (mode is not ("released" or "never")) throw new ArgumentException("Unknown readiness control.");
        using var contrast = new NativeTestHighContrast();
        using var display = new NativeTestDisplay(output);
        using var automation = new UIA3Automation { TransactionTimeout = TimeSpan.FromSeconds(3), ConnectionTimeout = TimeSpan.FromSeconds(3) };
        var state = Path.Combine(output, "state"); Directory.CreateDirectory(state);
        File.WriteAllText(Path.Combine(state, "preferences.json"), JsonSerializer.Serialize(new UserPreferences(AutomaticUpdateChecks: false)));
        var path = Path.Combine(output, "readiness.md"); File.WriteAllText(path, "# Before Save Ω\n");
        const string saved = "# Real held Save Ω😀\n\n```mermaid\narchitecture-beta\nservice api(server)[API]\n```\n";
        var info = new ProcessStartInfo(Path.GetFullPath(executable)) { UseShellExecute = false };
        info.Environment["LENS_DIAGNOSTIC_MODE"] = "barrier";
        foreach (var argument in new[] { "--state-directory", state, output, path }) info.ArgumentList.Add(argument);
        using var app = Application.Launch(info);
        var calls = 0; object? held = null, completed = null; string? expectedFailure = null; double? readinessElapsed = null;
        try
        {
            var main = NativeExpansionLaunch.Prepare(app, automation, Stopwatch.StartNew(), path, output, 1, true);
            var editor = main.FindFirstDescendant(cf => cf.ByAutomationId("MarkdownEditor")) ?? throw new InvalidOperationException("Control editor missing.");
            editor.Patterns.Value.Pattern.SetValue(saved); editor.Focus();
            var ready = new NativeUiChecks.EditorSaveReadiness(main, editor, app.ProcessId, path, saved, Path.Combine(output, "save-readiness.json"));
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_S);
            NativeUiChecks.Wait(() => SaveTimeline.BarrierState == 1 && File.ReadAllText(path) == saved
                && editor.Patterns.Value.Pattern.IsReadOnly.Value, "The real post-disk Save barrier was not observed.");
            using var heldState = JsonDocument.Parse(editor.Properties.ItemStatus.Value);
            held = heldState.RootElement.Clone();
            var initialNativeCalls = heldState.RootElement.GetProperty("SetValueCalls").GetInt32();
            var editorIdentity = heldState.RootElement.GetProperty("EditorIdentity").GetInt32();
            var documentIdentity = heldState.RootElement.GetProperty("DocumentIdentity").GetInt32();
            Require(heldState.RootElement.GetProperty("Main").GetProperty("IsSaving").GetBoolean(), "Native Save did not remain active.");
            Require(!ready.Observe() && calls == 0, "Readiness incorrectly accepted the held Save.");
            if (mode == "never")
            {
                try { ready.Await(); }
                catch (InvalidOperationException error) when (error.Message.StartsWith("Save readiness failed:", StringComparison.Ordinal)) { expectedFailure = error.ToString(); }
                Require(expectedFailure is not null && SaveTimeline.BarrierState == 1 && calls == 0, "Never-completing Save did not fail safely at readiness.");
                using var failedReceipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "save-readiness.json")));
                readinessElapsed = failedReceipt.RootElement.GetProperty("ElapsedMilliseconds").GetDouble();
                // This checks timeout accounting, never extends the predicate's 20 s deadline.
                // Allow only sub-second return/scheduling overhead after that deadline failed.
                Require(failedReceipt.RootElement.GetProperty("Status").GetString() == "FAIL"
                    && failedReceipt.RootElement.GetProperty("DeadlineMilliseconds").GetInt32() == 20000
                    && readinessElapsed >= 20000 && readinessElapsed < 21000, "Readiness timeout did not respect its original bounded wait.");
                using var timeoutState = JsonDocument.Parse(editor.Properties.ItemStatus.Value);
                completed = timeoutState.RootElement.Clone();
                Require(timeoutState.RootElement.GetProperty("Main").GetProperty("IsSaving").GetBoolean()
                    && editor.Patterns.Value.Pattern.IsReadOnly.Value && timeoutState.RootElement.GetProperty("SetValueCalls").GetInt32() == initialNativeCalls
                    && timeoutState.RootElement.GetProperty("EditorIdentity").GetInt32() == editorIdentity
                    && timeoutState.RootElement.GetProperty("DocumentIdentity").GetInt32() == documentIdentity && File.ReadAllText(path) == saved,
                    "The negative control stopped holding Save or executed SetValue.");
                main.CaptureToFile(Path.Combine(output, "expected-readiness-failure.png"));
            }
            else
            {
                SaveTimeline.ReleaseBarrier(); ready.Await();
                using var finished = JsonDocument.Parse(editor.Properties.ItemStatus.Value);
                Require(!finished.RootElement.GetProperty("Main").GetProperty("IsSaving").GetBoolean()
                    && !editor.Patterns.Value.Pattern.IsReadOnly.Value, "Native Save/read-only did not complete.");
                var session = JsonSerializer.Deserialize<SessionState>(File.ReadAllText(Path.Combine(state, "session.json")))!;
                Require(session.Documents.Single().Path == path && session.Documents.Single().Text == saved
                    && session.Workspace == output && session.ActiveDocument == session.Documents.Single().Id,
                    "Remaining persistence lost source/workspace state.");
                calls++; editor.Patterns.Value.Pattern.SetValue("# One writable mutation after Save Ω😀\n");
                using var after = JsonDocument.Parse(editor.Properties.ItemStatus.Value); completed = after.RootElement.Clone();
                Require(calls == 1 && after.RootElement.GetProperty("SetValueCalls").GetInt32() == initialNativeCalls + 1
                    && after.RootElement.GetProperty("EditorIdentity").GetInt32() == editorIdentity
                    && after.RootElement.GetProperty("DocumentIdentity").GetInt32() == documentIdentity
                    && editor.Patterns.Value.Pattern.Value.Value == "# One writable mutation after Save Ω😀\n"
                    && File.ReadAllText(path) == saved, "One-time mutation or retained source identity failed.");
            }
            File.WriteAllText(Path.Combine(output, "control-result.json"), JsonSerializer.Serialize(new {
                Status = "PASS", Mode = mode, ControlOutcome = mode == "never" ? "EXPECTED_READINESS_FAILURE" : "READINESS_AND_ONE_MUTATION_PASS",
                ActualHighContrast = NativeTestHighContrast.IsEnabledNow, DeadlineMilliseconds = 20000,
                FollowUpSetValueCalls = calls, Held = held, Completed = completed, ExpectedReadinessError = expectedFailure,
                ReadinessElapsedMilliseconds = readinessElapsed,
                Utc = DateTimeOffset.UtcNow, Attribution = "ORIGINAL_FAILURE_CAUSALLY_ATTRIBUTED_NOT_ESTABLISHED"
            }, new JsonSerializerOptions { WriteIndented = true }));
            display.Complete(); return 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(output, "control-error.txt"), error.ToString()); Console.Error.WriteLine(error); return 1;
        }
        finally { SaveTimeline.ReleaseBarrier(); SaveTimeline.Flush(); if (!app.HasExited) app.Kill(); }
    }
    private static void Require(bool condition, string failure) { if (!condition) throw new InvalidOperationException(failure); }
}
