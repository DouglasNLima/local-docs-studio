using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using FlaUI.UIA3;
using LensDocsStudio.Core.State;

namespace LensDocsStudio.Native.Validation;

internal static partial class NativeUiChecks
{
    public static int RunInstalledDrafts(string executable, string state, string output)
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true"
            || Environment.GetEnvironmentVariable("RUNNER_OS") != "Windows"
            || Environment.GetEnvironmentVariable("RUNNER_ENVIRONMENT") != "github-hosted")
        { Console.Error.WriteLine("Installed draft validation requires the disposable Windows GitHub-hosted runner."); return 1; }
        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { completion.SetResult(CreateInstalledDrafts(executable, state, output)); }
            catch (Exception error) { Console.Error.WriteLine(error); completion.SetResult(1); }
        }) { IsBackground = true, Name = "Historical installed draft automation" };
        thread.SetApartmentState(ApartmentState.MTA); thread.Start();
        return completion.Task.GetAwaiter().GetResult();
    }

    private static int CreateInstalledDrafts(string executable, string state, string output)
    {
        executable = Path.GetFullPath(executable); state = Path.GetFullPath(state); output = Path.GetFullPath(output);
        var fixture = Directory.GetParent(state)!.FullName;
        using var prior = JsonDocument.Parse(ReadObservedText(Path.Combine(fixture, "native-ui.json")));
        if (!prior.RootElement.GetProperty("Passed").GetBoolean()
            || !string.Equals(prior.RootElement.GetProperty("FixtureRoot").GetString(), fixture, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Draft preparation requires a completed isolated UI fixture.");
        var before = ReadSession();
        var target = before.Documents.First(document => document.Path is not null && document.Text == document.SavedText);
        var targetIndex = before.Documents.ToList().FindIndex(document => document.Id == target.Id);
        var sourceBytes = File.ReadAllBytes(target.Path!);
        const string addition = " Historical unsaved Café and Ω.";
        const string untitled = "Untitled historical Café draft Ω — retain me.";
        Directory.CreateDirectory(output);
        Process? started = null; Application? app = null; Window? window = null;
        using var automation = new UIA3Automation();
        try
        {
            var start = new ProcessStartInfo(executable) { UseShellExecute = false };
            start.ArgumentList.Add("--state-directory"); start.ArgumentList.Add(state);
            started = Process.Start(start) ?? throw new IOException("The original installed application could not start.");
            app = Application.Attach(started.Id);
            window = app.GetMainWindow(automation, TimeSpan.FromSeconds(20)) ?? throw new IOException("The historical draft window did not appear.");
            var editor = Find(window, "MarkdownEditor");
            var active = before.Documents.Single(document => document.Id == before.ActiveDocument);
            Wait(() => ReadEditorText(editor) == active.Text, "The original application did not finish restoring its active document.");
            var tabs = Find(window, "DocumentTabs").FindAllChildren(cf => cf.ByControlType(ControlType.TabItem));
            if (tabs.Length != before.Documents.Count) throw new IOException("Draft preparation lost existing tabs.");
            tabs[targetIndex].Patterns.SelectionItem.Pattern.Select();
            Wait(() => ReadEditorText(editor) == target.Text, "The original application did not restore the selected saved document.");
            FocusWindow(window); editor.Click();
            // Prepare exact recovery text through the original editor's native value provider.
            // OS keyboard delivery is exercised separately by the full desktop suite.
            editor.Patterns.Value.Pattern.SetValue(target.Text + addition);
            Wait(() => ReadEditorText(editor) == target.Text + addition, "The original editor did not retain the unsaved saved-file change.");
            Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_N);
            Wait(() => ReadEditorText(editor).Length == 0
                && Find(window, "DocumentTabs").FindAllChildren(cf => cf.ByControlType(ControlType.TabItem)).Length == before.Documents.Count + 1,
                "The original application did not create an untitled draft.");
            editor.Patterns.Value.Pattern.SetValue(untitled);
            Wait(() => ReadEditorText(editor) == untitled, "The original editor did not retain the exact untitled draft.");
            window.CaptureToFile(Path.Combine(output, "seeded-drafts.png"));
            // The original application's own Closing handler writes its historical schema.
            // Declining file saves retains recovery text while leaving workspace bytes intact.
            CloseRetainingDrafts(window, automation, started, before.Documents.Count(HasUnsavedText) + 2);
            var after = ReadSession();
            var changed = after.Documents.Single(document => document.Id == target.Id);
            var draft = after.Documents.Single(document => document.Path is null && document.Text == untitled);
            if (after.Documents.Count != before.Documents.Count + 1 || changed.Text != target.Text + addition
                || changed.SavedText != target.SavedText || after.ActiveDocument != draft.Id
                || !sourceBytes.AsSpan().SequenceEqual(File.ReadAllBytes(target.Path!)))
                throw new IOException("Original application shutdown lost draft text, baseline or workspace bytes.");
            File.WriteAllText(Path.Combine(output, "seeded-drafts.json"), JsonSerializer.Serialize(new
            {
                passed = true, stateWrittenByOriginalApplication = true, originalSourceBytesPreserved = true,
                documents = after.Documents.Count, unsavedSavedDocuments = after.Documents.Count(document => document.Path is not null && HasUnsavedText(document)),
                untitledDrafts = after.Documents.Count(document => document.Path is null && HasUnsavedText(document)),
                limitation = "Synthetic actual-UI edits and normal recovery persistence; no modern schema is written by the harness."
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch
        {
            NativeUiFailureDiagnostics.Capture(started, output);
            try { window?.CaptureToFile(Path.Combine(output, "native-ui-failure.png")); } catch { }
            throw;
        }
        finally
        {
            if (started is { HasExited: false }) started.Kill(entireProcessTree: true);
            app?.Dispose(); started?.Dispose();
        }
        SessionState ReadSession() => JsonSerializer.Deserialize<SessionState>(ReadObservedText(Path.Combine(state, "session.json")))
            ?? throw new IOException("The historical draft session is empty.");
    }

    private static bool HasUnsavedText(RecoveredDocument document) => document.Path is null
        ? document.Text.Length != 0 : document.Text != document.SavedText;

    private static void CloseRetainingDrafts(Window window, UIA3Automation automation, Process process, int dirtyDocuments)
    {
        window.Close();
        for (var index = 0; index < dirtyDocuments; index++)
        {
            Window? prompt = null;
            // Resolve only this test process's native dialogue before asking its provider.
            // An unrelated or closing desktop provider must not decide draft retention.
            Wait(() => (prompt = NativeWindowDiscovery.FindTopLevelWindows("#32770", "Unsaved document", process.Id)
                .Select(handle => automation.FromHandle(handle).AsWindow()).SingleOrDefault()) is not null, "The expected unsaved-document prompt did not appear.");
            var handle = prompt!.Properties.NativeWindowHandle.Value;
            var decline = prompt.FindFirstDescendant(cf => cf.ByName("No").And(cf.ByControlType(ControlType.Button)))
                ?? throw new IOException("The unsaved-document prompt has no decline-save action.");
            decline.AsButton().Invoke();
            Wait(() => !NativeWindowDiscovery.Exists(handle), "The answered draft prompt did not close.");
        }
        Wait(() => process.HasExited, "The application did not persist drafts and close safely.");
        if (process.ExitCode != 0) throw new IOException("The draft-preserving application shutdown failed.");
    }
}
